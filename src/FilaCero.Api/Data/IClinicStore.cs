using FilaCero.Domain;

namespace FilaCero.Api.Data;

public interface IClinicStore
{
    Task<PatientDto> RegisterAsync(RegistrationRequest data);
    Task<PatientDto?> GetPatientAsync(Guid id);
    Task<TriageDto?> GetTriageAsync(Guid id);
    Task SaveTriageAsync(Guid id, TriageDto triage);
    Task<TurnDto?> GetTurnAsync(Guid patientId);
    Task<TurnDto> IssueTurnAsync(Guid patientId);
    Task<TurnDto?> GetTurnByCodeAsync(string code);
    Task<IReadOnlyList<TurnDto>> ListTurnsAsync();
    Task<TurnDto?> UpdateTurnAsync(Guid id, string status, string? room);
    Task<Guid?> GetTurnOwnerAsync(Guid turnId);
}
