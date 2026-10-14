using WorkPulse.Domain;

namespace WorkPulse.Application;

public sealed record DeviceAccess(Guid OrganizationId,Guid DeviceId,Guid EmployeeId,Guid CredentialId);
public sealed record EventInput(Guid ClientEventId,DateTimeOffset OccurredAt,EventKind Kind,string? Application);
public interface IEventInbox
{
    Task<object> Ingest(DeviceAccess device,IReadOnlyList<EventInput> events);
}
public interface IAnalysisResults
{
    Task<string> Run(UserAccess user,Guid id);
}
