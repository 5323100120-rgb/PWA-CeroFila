namespace FilaCero.Domain;

public sealed record DocumentUpload(string Name, string Mime, string DataBase64);
public sealed record RegistrationRequest(string FullName, string Curp, string Nss, string Phone,
    string Clinic, DocumentUpload? Document = null);
public sealed record PatientDto(Guid Id, string FullName, string Clinic, string? DocumentName);
public sealed record RegisterResponse(PatientDto Patient, string SessionToken, int ExpiresInSeconds);

public sealed record TriageAnswers(
    bool ChestPain, bool BreathingDifficulty, bool Unconscious,
    bool Fever, bool SeverePain, bool Dizziness,
    int? Systolic, int? Diastolic, string? Description);
public sealed record TriageDto(string Level, string Color, string Title, string Message,
    TriageAnswers Answers, bool Reviewed = false);
public sealed record TriageResponse(TriageDto Triage);

public sealed record TurnDto(Guid Id, string Code, string Clinic, string Priority,
    string PriorityColor, string Status, string? Room, int QueueAhead,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string QrPayload);
public sealed record TurnResponse(TurnDto? Turn);
public sealed record MeResponse(PatientDto? Patient, TriageDto? Triage, TurnDto? Turn);
public sealed record QueueEvent(string Type, TurnDto Turn, long Ts);
public sealed record ApiError(string Error, Dictionary<string,string>? Fields = null);
public sealed record HealthDto(bool Ok, string Mode, string Version);

public static class ClinicNames
{
    public static readonly string[] All = ["Clínica Centro", "Unidad Norte", "Unidad Sur"];
}
public static class QueueStatuses
{
    public const string Waiting = "En espera";
    public const string Called = "Llamado a consultorio";
    public const string Serving = "En atención";
    public const string Attended = "Atendido";
    public static readonly string[] All = [Waiting, Called, Serving, Attended];
}
