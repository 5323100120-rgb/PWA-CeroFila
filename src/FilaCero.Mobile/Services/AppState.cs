using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Devices;
using System.Text.Json;
using FilaCero.Domain;

namespace FilaCero.Mobile.Services;

/// <summary>Estado de la app: token y último pase se guardan en SecureStorage del sistema.</summary>
public sealed class AppState
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string SessionKey = "fc_maui_session_v1";
    private const string TicketKey = "fc_maui_pass_v1";
    // Se conserva únicamente para reconocer instalaciones anteriores en modo DEBUG.
    private const string LegacyUrlKey = "fc_maui_server_url_v1";

    public event EventHandler? Changed;
    public string ServerUrl { get; private set; } = "";
    public string? SessionToken { get; private set; }
    public PatientDto? Patient { get; private set; }
    public TriageDto? Triage { get; private set; }
    public TurnDto? Turn { get; private set; }
    public bool OfflinePass { get; private set; }
    public string Notice { get; private set; } = "Versión académica. No uses datos reales.";
    public string ConnectionStatus { get; private set; } = "Sin conexión";
    public bool Ready { get; private set; }

    private void Update() => MainThread.BeginInvokeOnMainThread(() => Changed?.Invoke(this,EventArgs.Empty));

    public async Task InitializeAsync()
    {
        if(Ready) return;
        try
        {
            // El paciente no introduce ninguna dirección en la aplicación.
            // Para emulador Android la API local se encuentra de forma automática.
            ServerUrl = ApiConfiguration.ResolveBaseUrl();
#if DEBUG
            // Mantiene funcionando instalaciones previas en teléfonos físicos,
            // que ya tenían una dirección configurada antes del rediseño.
            if (string.IsNullOrWhiteSpace(ServerUrl))
                ServerUrl = Preferences.Default.Get(LegacyUrlKey, "");
#endif
            SessionToken=await SecureStorage.Default.GetAsync(SessionKey);
            var saved=await SecureStorage.Default.GetAsync(TicketKey);
            if(!string.IsNullOrWhiteSpace(saved))
            {
                var pass=JsonSerializer.Deserialize<TurnDto>(saved,Json);
                if(pass?.QrPayload.StartsWith("FC1|A-",StringComparison.Ordinal)==true)
                {Turn=pass;OfflinePass=true;}
            }
        }
        catch (Exception e)
        {
            // Algunos dispositivos invalidan SecureStorage al restaurar copias de seguridad.
            Notice=$"No se pudo recuperar la sesión local: {e.Message}";
        }
        finally {Ready=true;Update();}
    }

    public async Task SetRegisteredAsync(RegisterResponse response)
    {
        await SecureStorage.Default.SetAsync(SessionKey,response.SessionToken);
        SecureStorage.Default.Remove(TicketKey);
        SessionToken=response.SessionToken;
        Patient=response.Patient;Triage=null;Turn=null;OfflinePass=false;
        SetNotice("Registro completo. Continúa al cuestionario de triaje.");
    }

    public void SetTriage(TriageDto triage)
    {
        Triage=triage;
        SetNotice(triage.Title);
    }

    public async Task SetTicketAsync(TurnDto turn, bool offline=false)
    {
        await SecureStorage.Default.SetAsync(TicketKey,JsonSerializer.Serialize(turn,Json));
        Turn=turn;OfflinePass=offline;
        Update();
    }

    public async Task RefreshAsync(FilaCeroApi api)
    {
        if(string.IsNullOrWhiteSpace(ServerUrl) || string.IsNullOrWhiteSpace(SessionToken)) return;
        try
        {
            var result = await api.GetMeAsync();
            if(result.Patient is null)
            {
                SecureStorage.Default.Remove(SessionKey);
                SessionToken=null;Patient=null;Triage=null;OfflinePass=Turn is not null;
                SetNotice("Sesión caducada. Tu último QR continúa disponible sin conexión.");
                return;
            }
            Patient=result.Patient;Triage=result.Triage;
            if(result.Turn is not null) await SetTicketAsync(result.Turn);
            else if(Turn is null) OfflinePass=false;
            else OfflinePass=true; // No borrar el último pase sin confirmación explícita del usuario.
            SetNotice("Información sincronizada.");
        }
        catch(Exception e) when (e is HttpRequestException or TaskCanceledException or ApiException)
        {
            OfflinePass=Turn is not null;
            SetNotice("No fue posible consultar el servidor; se conserva el último pase digital.");
        }
        Update();
    }

    public async Task ApplyQueueUpdateAsync(QueuePatch patch)
    {
        if(Turn is null || patch.Id!=Turn.Id) return;
        var modified=Turn with
        {
            Status=patch.Status,Room=patch.Room,QueueAhead=patch.QueueAhead,UpdatedAt=patch.UpdatedAt
        };
        await SetTicketAsync(modified);
        SetNotice($"Turno {modified.Code}: {modified.Status}{(string.IsNullOrWhiteSpace(modified.Room)?"":$" · {modified.Room}")}");
        if(modified.Status==QueueStatuses.Called)
        {
            try { Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(450)); }
            catch (Exception) { /* No todos los equipos permiten vibrar. */ }
        }
    }

    public void SetConnection(string status) {ConnectionStatus=status;Update();}
    public void SetNotice(string text) {Notice=text;Update();}

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(SessionKey);
        SecureStorage.Default.Remove(TicketKey);
        SessionToken=null;Patient=null;Triage=null;Turn=null;OfflinePass=false;
        SetNotice("Se eliminaron la sesión y el último QR del dispositivo.");
        return Task.CompletedTask;
    }
}

public sealed record QueuePatch(Guid Id,string Code,string Clinic,string Status,string? Room,int QueueAhead,DateTimeOffset UpdatedAt);
