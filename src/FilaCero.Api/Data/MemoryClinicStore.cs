using System.Security.Cryptography;
using System.Text;
using FilaCero.Api.Security;
using FilaCero.Domain;

namespace FilaCero.Api.Data;

/// <summary>Almacenamiento efímero de desarrollo; NO para pacientes reales.</summary>
public sealed class MemoryClinicStore(TokenService signer, IConfiguration config) : IClinicStore
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, PatientDto> _patients = [];
    private readonly Dictionary<Guid, TriageDto> _triages = [];
    private readonly Dictionary<Guid, MemoryTurn> _turns = [];
    private readonly Dictionary<Guid, byte[]> _documents = [];
    private readonly string _pepper = config["FINGERPRINT_PEPPER"] ?? "demo-dont-use-with-real-data";
    private int _number;

    // El hash de identificadores evita conservar CURP/NSS en texto claro.
    private string Fingerprint(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_pepper}:{value}")));

    private sealed record MemoryTurn(Guid Id, Guid PatientId, string Code, string Clinic,
        string Priority, string Color, string Nonce, DateTimeOffset CreatedAt)
    {
        public string Status { get; set; } = QueueStatuses.Waiting;
        public string? Room { get; set; }
        public DateTimeOffset UpdatedAt { get; set; } = CreatedAt;
    }

    private TurnDto Map(MemoryTurn t)
    {
        var ahead = _turns.Values.Count(other => other.Clinic == t.Clinic && other.Status == QueueStatuses.Waiting && other.CreatedAt <= t.CreatedAt && other.Id != t.Id);
        return new(t.Id, t.Code, t.Clinic, t.Priority, t.Color, t.Status, t.Room, ahead,
            t.CreatedAt, t.UpdatedAt, signer.QrPayload(t.Code, t.Nonce));
    }

    public Task<PatientDto> RegisterAsync(RegistrationRequest d)
    {
        var id = Guid.NewGuid();
        var patient = new PatientDto(id, d.FullName, d.Clinic, d.Document?.Name);
        lock (_gate)
        {
            _patients[id] = patient;
            if (d.Document is not null) _documents[id] = Convert.FromBase64String(d.Document.DataBase64);
            // Ficticios: calcular, sin imprimir ni almacenar identificadores de identidad.
            _ = string.IsNullOrEmpty(d.Curp) ? "" : Fingerprint(d.Curp);
            _ = string.IsNullOrEmpty(d.Nss) ? "" : Fingerprint(d.Nss);
        }
        return Task.FromResult(patient);
    }
    public Task<PatientDto?> GetPatientAsync(Guid id) { lock (_gate) return Task.FromResult(_patients.GetValueOrDefault(id)); }
    public Task<TriageDto?> GetTriageAsync(Guid id) { lock (_gate) return Task.FromResult(_triages.GetValueOrDefault(id)); }
    public Task SaveTriageAsync(Guid id, TriageDto t) { lock (_gate) _triages[id] = t; return Task.CompletedTask; }
    public Task<TurnDto?> GetTurnAsync(Guid patientId)
    {
        lock (_gate) return Task.FromResult(_turns.Values.Where(t => t.PatientId == patientId).Select(Map).FirstOrDefault());
    }
    public Task<TurnDto> IssueTurnAsync(Guid patientId)
    {
        lock (_gate)
        {
            var existing = _turns.Values.FirstOrDefault(t => t.PatientId == patientId);
            if (existing is not null) return Task.FromResult(Map(existing));
            if (!_patients.TryGetValue(patientId, out var patient) || !_triages.TryGetValue(patientId, out var triage))
                throw new InvalidOperationException("Completa pre-registro y triaje antes de solicitar turno.");
            if (triage.Color == "rojo")
                throw new InvalidOperationException("Alerta urgente: busca atención inmediata, no una ficha digital.");
            var now = DateTimeOffset.UtcNow;
            var turn = new MemoryTurn(Guid.NewGuid(), patientId, $"A-{++_number:D3}", patient.Clinic,
                triage.Level, triage.Color, signer.NewNonce(), now);
            _turns.Add(turn.Id, turn);
            return Task.FromResult(Map(turn));
        }
    }
    public Task<TurnDto?> GetTurnByCodeAsync(string code)
    {
        lock (_gate) return Task.FromResult(_turns.Values.Where(t => t.Code == code).Select(Map).FirstOrDefault());
    }
    public Task<IReadOnlyList<TurnDto>> ListTurnsAsync()
    {
        lock (_gate) return Task.FromResult<IReadOnlyList<TurnDto>>(_turns.Values.OrderBy(t=>t.CreatedAt).Select(Map).ToArray());
    }
    public Task<Guid?> GetTurnOwnerAsync(Guid id)
    {
        lock (_gate) return Task.FromResult(_turns.TryGetValue(id, out var turn) ? (Guid?)turn.PatientId : null);
    }
    public Task<TurnDto?> UpdateTurnAsync(Guid id, string status, string? room)
    {
        lock (_gate)
        {
            if (!_turns.TryGetValue(id, out var t)) return Task.FromResult<TurnDto?>(null);
            t.Status = status; t.Room = room; t.UpdatedAt = DateTimeOffset.UtcNow;
            return Task.FromResult<TurnDto?>(Map(t));
        }
    }
}
