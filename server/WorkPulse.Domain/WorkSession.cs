namespace WorkPulse.Domain;

public sealed class SessionBreak
{
    public Guid Id { get; }
    public BreakKind Kind { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }

    internal SessionBreak(Guid id, BreakKind kind, DateTimeOffset start)
    {
        Id = id; Kind = kind; StartedAt = start;
    }

    internal void Close(DateTimeOffset end)
    {
        Guard.Require(EndedAt is null && end > StartedAt, "Invalid break end.");
        EndedAt = end;
    }
}

/// <summary>A session belongs to the employee, not to a particular device.</summary>
public sealed class WorkSession
{
    private readonly List<SessionBreak> breaks = [];
    public Guid OrganizationId { get; }
    public Guid EmployeeId { get; }
    public Guid DepartmentId { get; }
    public Guid PolicyVersionId { get; }
    public Guid Id { get; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset? EndedAt { get; private set; }
    public EndReason? EndReason { get; private set; }
    public IReadOnlyList<SessionBreak> Breaks => breaks.AsReadOnly();

    private WorkSession(Guid organization, Guid employee, Guid department, Guid policy, Guid id, DateTimeOffset start)
    {
        OrganizationId = organization; EmployeeId = employee; DepartmentId = department;
        PolicyVersionId = policy; Id = id; StartedAt = start;
    }

    public static WorkSession Start(Guid organization, Guid employee, Guid department,
        Guid id, DateTimeOffset start, PolicySnapshot policy)
    {
        foreach (var (value, name) in new[] { (organization, "organization"), (employee, "employee"),
            (department, "department"), (id, "session") }) Guard.Id(value, name);
        Guard.Require(policy.OrganizationId == organization, "Policy belongs to another organization.");
        Guard.Require(policy.PublishedAt <= start, "Policy was not published at session start.");
        return new(organization, employee, department, policy.Id, id, start.ToUniversalTime());
    }

    public static WorkSession StartFromSignal(ActivityEvent signal, Guid department, Guid id, PolicySnapshot policy)
    {
        Guard.Id(signal.DeviceId, "device"); Guard.Id(signal.ClientEventId, "client event");
        Guard.Require(signal.Kind is EventKind.Login or EventKind.Focus or EventKind.Input,
            "Only login or real activity can start a session.");
        return Start(signal.OrganizationId, signal.EmployeeId, department, id, signal.OccurredAt, policy);
    }

    public void StartBreak(Guid id, BreakKind kind, DateTimeOffset at)
    {
        Guard.Id(id, "break");
        Guard.Require(Enum.IsDefined(kind), "Unknown break kind.");
        Guard.Require(EndedAt is null && at >= StartedAt, "Session is closed or break precedes it.");
        Guard.Require(breaks.All(b => b.EndedAt is not null), "Another break is open.");
        Guard.Require(breaks.All(b => b.Id != id), "Duplicate break id.");
        Guard.Require(breaks.All(b => b.EndedAt <= at), "Breaks must be chronological and non-overlapping.");
        Guard.Require(kind != BreakKind.Lunch || breaks.All(b => b.Kind != BreakKind.Lunch), "Only one lunch per session.");
        breaks.Add(new(id, kind, at.ToUniversalTime()));
    }

    public void EndBreak(DateTimeOffset at)
    {
        Guard.Require(EndedAt is null, "Session is closed.");
        var current = breaks.SingleOrDefault(b => b.EndedAt is null)
            ?? throw new BusinessRuleException("No open break.");
        current.Close(at.ToUniversalTime());
    }

    public void End(DateTimeOffset at, EndReason reason)
    {
        Guard.Require(Enum.IsDefined(reason), "Unknown end reason.");
        Guard.Require(EndedAt is null && at > StartedAt, "Invalid session end.");
        Guard.Require(breaks.All(b => at >= (b.EndedAt ?? b.StartedAt)), "Session end excludes a break.");
        var open = breaks.SingleOrDefault(b => b.EndedAt is null);
        if (open is not null) open.Close(at.ToUniversalTime());
        EndedAt = at.ToUniversalTime(); EndReason = reason;
    }
}
