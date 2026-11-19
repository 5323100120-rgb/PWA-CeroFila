using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FilaCero.Api.Security;
using FilaCero.Domain;
using Npgsql;
using NpgsqlTypes;

namespace FilaCero.Api.Data;

/// <summary>Persistencia PostgreSQL para integración; no implementa controles de producción.</summary>
public sealed class PostgresClinicStore(NpgsqlDataSource source, TokenService signer, IConfiguration config) : IClinicStore
{
    private readonly string _pepper = config["FINGERPRINT_PEPPER"] ?? "demo-change-me";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string Fingerprint(string v) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_pepper}:{v}")));

    public async Task<PatientDto> RegisterAsync(RegistrationRequest d)
    {
        var id = Guid.NewGuid();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO patients (id, full_name, clinic, curp_hash, nss_hash, phone_last4, document_name, document_mime, document_bytes)
            VALUES (@id, @name, @clinic, @curp, @nss, @phone, @filename, @mime, @bytes)
            """, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", d.FullName);
        cmd.Parameters.AddWithValue("clinic", d.Clinic);
        cmd.Parameters.AddWithValue("curp", (object?)(string.IsNullOrEmpty(d.Curp) ? null : Fingerprint(d.Curp)) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("nss", (object?)(string.IsNullOrEmpty(d.Nss) ? null : Fingerprint(d.Nss)) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)(d.Phone.Length >= 4 ? d.Phone[^4..] : null) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("filename", (object?)d.Document?.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mime", (object?)d.Document?.Mime ?? DBNull.Value);
        cmd.Parameters.Add("bytes", NpgsqlDbType.Bytea).Value = d.Document is null ? DBNull.Value : Convert.FromBase64String(d.Document.DataBase64);
        await cmd.ExecuteNonQueryAsync();
        return new(id, d.FullName, d.Clinic, d.Document?.Name);
    }

    public async Task<PatientDto?> GetPatientAsync(Guid id)
    {
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT id,full_name,clinic,document_name FROM patients WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? new(r.GetGuid(0), r.GetString(1), r.GetString(2), r.IsDBNull(3)? null : r.GetString(3)) : null;
    }

    public async Task<TriageDto?> GetTriageAsync(Guid id)
    {
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT level,color,title,message,answers_json FROM triage_evaluations WHERE patient_id=@patient", conn);
        cmd.Parameters.AddWithValue("patient", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        var answers = JsonSerializer.Deserialize<TriageAnswers>(r.GetString(4), Json)!;
        return new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),answers);
    }

    public async Task SaveTriageAsync(Guid id, TriageDto triage)
    {
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO triage_evaluations (patient_id,level,color,title,message,answers_json)
            VALUES (@patient,@level,@color,@title,@message,@answers)
            ON CONFLICT (patient_id) DO UPDATE SET level=EXCLUDED.level,color=EXCLUDED.color,
            title=EXCLUDED.title,message=EXCLUDED.message,answers_json=EXCLUDED.answers_json
            """, conn);
        cmd.Parameters.AddWithValue("patient",id);
        cmd.Parameters.AddWithValue("level",triage.Level);
        cmd.Parameters.AddWithValue("color",triage.Color);
        cmd.Parameters.AddWithValue("title",triage.Title);
        cmd.Parameters.AddWithValue("message",triage.Message);
        cmd.Parameters.Add("answers",NpgsqlDbType.Jsonb).Value=JsonSerializer.Serialize(triage.Answers, Json);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<TurnDto?> LoadTurnAsync(string where, object value)
    {
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand($"""
            SELECT t.id, t.code, t.clinic, t.priority, t.priority_color, t.status, t.room,
                   t.created_at, t.updated_at, t.nonce,
                   (SELECT count(*)::int FROM turns t2 WHERE t2.clinic=t.clinic AND t2.status='En espera'
                      AND t2.created_at<=t.created_at AND t2.id<>t.id) AS queue_ahead
            FROM turns t WHERE t.{where}=@value LIMIT 1
            """, conn);
        cmd.Parameters.AddWithValue("value",value);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? ReadTurn(r) : null;
    }

    private TurnDto ReadTurn(NpgsqlDataReader r)
    {
        var code = r.GetString(1);
        var nonce = r.GetString(9);
        return new(r.GetGuid(0),code,r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),
            r.IsDBNull(6)?null:r.GetString(6),r.GetInt32(10),
            new DateTimeOffset(r.GetDateTime(7), TimeSpan.Zero),new DateTimeOffset(r.GetDateTime(8),TimeSpan.Zero),signer.QrPayload(code,nonce));
    }

    public Task<TurnDto?> GetTurnAsync(Guid patientId) => LoadTurnAsync("patient_id",patientId);
    public Task<TurnDto?> GetTurnByCodeAsync(string code) => LoadTurnAsync("code",code);

    public async Task<TurnDto> IssueTurnAsync(Guid patientId)
    {
        // Bloqueo de paciente + unique(patient_id): evita crear dos turnos bajo solicitudes concurrentes.
        await using var conn = await source.OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();
        string clinic;
        await using (var patientCmd = new NpgsqlCommand("SELECT clinic FROM patients WHERE id=@id FOR UPDATE",conn,tx))
        {
            patientCmd.Parameters.AddWithValue("id",patientId);
            clinic = (string?)(await patientCmd.ExecuteScalarAsync()) ?? throw new InvalidOperationException("Paciente no encontrado.");
        }
        await using(var prior = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM turns WHERE patient_id=@id)",conn,tx))
        {
            prior.Parameters.AddWithValue("id",patientId);
            if ((bool)(await prior.ExecuteScalarAsync() ?? false))
            {
                await tx.CommitAsync();
                return (await GetTurnAsync(patientId))!;
            }
        }
        string level; string color;
        await using(var triage = new NpgsqlCommand("SELECT level,color FROM triage_evaluations WHERE patient_id=@id",conn,tx))
        {
            triage.Parameters.AddWithValue("id",patientId);
            await using var r=await triage.ExecuteReaderAsync();
            if(!await r.ReadAsync()) throw new InvalidOperationException("Completa el cuestionario primero.");
            level=r.GetString(0); color=r.GetString(1);
        }
        if(color=="rojo") throw new InvalidOperationException("Alerta urgente: busca atención inmediata, no una ficha digital.");
        await using(var cmd = new NpgsqlCommand("""
            INSERT INTO turns (id, patient_id, code, clinic, priority, priority_color, status, nonce)
            VALUES (@id,@patient,'A-' || lpad(nextval('turn_number_seq')::text, 3, '0'), @clinic,@priority,@color,'En espera',@nonce)
            """, conn,tx))
        {
            cmd.Parameters.AddWithValue("id",Guid.NewGuid());cmd.Parameters.AddWithValue("patient",patientId);
            cmd.Parameters.AddWithValue("clinic",clinic);cmd.Parameters.AddWithValue("priority",level);
            cmd.Parameters.AddWithValue("color",color);cmd.Parameters.AddWithValue("nonce",signer.NewNonce());
            await cmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return (await GetTurnAsync(patientId))!;
    }

    public async Task<IReadOnlyList<TurnDto>> ListTurnsAsync()
    {
        var list = new List<TurnDto>();
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("""
            SELECT t.id,t.code,t.clinic,t.priority,t.priority_color,t.status,t.room,t.created_at,t.updated_at,t.nonce,
            (SELECT count(*)::int FROM turns t2 WHERE t2.clinic=t.clinic AND t2.status='En espera' AND t2.created_at<=t.created_at AND t2.id<>t.id)
            FROM turns t ORDER BY t.created_at
            """,conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while(await r.ReadAsync()) list.Add(ReadTurn(r));
        return list;
    }

    public async Task<Guid?> GetTurnOwnerAsync(Guid id)
    {
        await using var conn=await source.OpenConnectionAsync();
        await using var cmd=new NpgsqlCommand("SELECT patient_id FROM turns WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id",id);
        return await cmd.ExecuteScalarAsync() is Guid value ? value : null;
    }

    public async Task<TurnDto?> UpdateTurnAsync(Guid id, string status, string? room)
    {
        await using var conn = await source.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE turns SET status=@status,room=@room,updated_at=now() WHERE id=@id RETURNING patient_id",conn);
        cmd.Parameters.AddWithValue("id",id);
        cmd.Parameters.AddWithValue("status",status);
        cmd.Parameters.AddWithValue("room",(object?)room??DBNull.Value);
        var patientId=await cmd.ExecuteScalarAsync();
        return patientId is Guid patient ? await GetTurnAsync(patient) : null;
    }
}
