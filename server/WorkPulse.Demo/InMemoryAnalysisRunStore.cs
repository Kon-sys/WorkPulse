using WorkPulse.Application;

namespace WorkPulse.Demo;

/// <summary>Demonstration adapter only. PostgreSQL persistence belongs to the next stage.</summary>
public sealed class InMemoryAnalysisRunStore : IAnalysisRunStore
{
    private readonly List<AnalysisRun> runs = [];
    public IReadOnlyList<AnalysisRun> Runs => runs.AsReadOnly();

    public void Append(AnalysisRun run)
    {
        if (runs.Any(r => r.Id == run.Id)) throw new InvalidOperationException("Run already exists.");
        runs.Add(run);
    }
}
