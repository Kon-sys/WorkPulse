namespace WorkPulse.Domain;

public sealed class BusinessRuleException(string message) : Exception(message);

public enum BreakKind { Lunch, AuthorizedAbsence, Rest }
public enum EndReason { Manual, Logout }
public enum EventKind { Login, Focus, Input, Idle, Heartbeat, Logout }
public enum ActivityCategory { Productive, NonTarget, Neutral, Idle, Lunch, AuthorizedAbsence, Unknown }
public enum ViolationKind { Idle, NonTarget, LunchOverrun, LateArrival, EarlyDeparture }

public static class Guard
{
    public static void Id(Guid value, string name)
    {
        if (value == Guid.Empty) throw new BusinessRuleException($"{name} must not be empty.");
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new BusinessRuleException(message);
    }
}

public sealed record ActivityEvent(
    Guid OrganizationId, Guid EmployeeId, Guid DeviceId, Guid ClientEventId,
    DateTimeOffset OccurredAt, DateTimeOffset ReceivedAt, EventKind Kind, Guid? ResourceId = null);

public sealed record EventReference(Guid DeviceId, Guid ClientEventId);

public sealed record ActivityInterval(
    DateTimeOffset Start, DateTimeOffset End, ActivityCategory Category,
    IReadOnlyList<EventReference> Sources)
{
    public TimeSpan Duration => End - Start;
}

public sealed record Violation(
    ViolationKind Kind, DateTimeOffset Start, DateTimeOffset End, TimeSpan Limit)
{
    public TimeSpan Duration => End - Start;
}
