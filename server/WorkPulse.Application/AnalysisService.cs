using WorkPulse.Domain;

namespace WorkPulse.Application;

public sealed record AnalysisRun(
    Guid Id, Guid OrganizationId, Guid SessionId, Guid PolicyVersionId,
    string AlgorithmVersion, TimeSpan SignalLifetime, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd,
    IReadOnlyList<ActivityInterval> Intervals, IReadOnlyList<Violation> Violations);

/// <summary>Port: saving a new run never overwrites a previous reconstruction.</summary>
public interface IAnalysisRunStore
{
    void Append(AnalysisRun run);
}

public sealed class AnalysisService(ActivityReconstructor reconstructor, ViolationDetector detector, IAnalysisRunStore store)
{
    public const string AlgorithmVersion = "workpulse-timeline/1";

    public AnalysisRun Analyze(WorkSession session, PolicySnapshot policy, DateTimeOffset until,
        IReadOnlyCollection<Guid> devices, IReadOnlyCollection<ActivityEvent> events, AnalysisOptions options)
    {
        var intervals = reconstructor.Reconstruct(session, policy, until, devices, events, options);
        var run = new AnalysisRun(Guid.NewGuid(), session.OrganizationId, session.Id, policy.Id,
            AlgorithmVersion, options.SignalLifetime, intervals[0].Start, intervals[^1].End, intervals, detector.Detect(intervals, policy));
        store.Append(run);
        return run;
    }
}
