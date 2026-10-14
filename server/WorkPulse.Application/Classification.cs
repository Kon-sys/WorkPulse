using WorkPulse.Domain;

namespace WorkPulse.Application;

/// <summary>Strategy boundary for classification without coupling the domain to a database.</summary>
public interface IResourceClassifier
{
    ActivityCategory Classify(Guid? resource, PolicySnapshot policy);
}

public sealed class PolicyResourceClassifier : IResourceClassifier
{
    public ActivityCategory Classify(Guid? resource, PolicySnapshot policy) => resource is not null
        && policy.ResourceCategories.TryGetValue(resource.Value, out var category) ? category : ActivityCategory.Neutral;
}

/// <summary>Specification for strictly exceeding a continuous interval limit.</summary>
public sealed class ExceedsLimitSpecification
{
    public bool IsSatisfiedBy(TimeSpan duration, TimeSpan limit) => duration > limit;
}

public sealed class ViolationDetector(ExceedsLimitSpecification specification)
{
    public IReadOnlyList<Violation> Detect(IReadOnlyList<ActivityInterval> intervals, PolicySnapshot policy)
    {
        var violations = new List<Violation>();
        foreach (var interval in intervals)
        {
            var rule = interval.Category switch
            {
                ActivityCategory.Idle => (ViolationKind.Idle, policy.IdleLimit),
                ActivityCategory.NonTarget => (ViolationKind.NonTarget, policy.NonTargetLimit),
                ActivityCategory.Lunch => (ViolationKind.LunchOverrun, policy.LunchLimit),
                _ => ((ViolationKind Kind, TimeSpan Limit)?)null
            };
            if (rule is { } r && specification.IsSatisfiedBy(interval.Duration, r.Limit))
                violations.Add(new(r.Kind, interval.Start, interval.End, r.Limit));
        }
        return violations.AsReadOnly();
    }
}
