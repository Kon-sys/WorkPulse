using System.Collections.ObjectModel;

namespace WorkPulse.Domain;

/// <summary>Immutable published policy; changing rules creates a new version.</summary>
public sealed class PolicySnapshot
{
    public Guid OrganizationId { get; }
    public Guid Id { get; }
    public DateTimeOffset PublishedAt { get; }
    public TimeSpan IdleLimit { get; }
    public TimeSpan NonTargetLimit { get; }
    public TimeSpan LunchLimit { get; }
    public IReadOnlyDictionary<Guid, ActivityCategory> ResourceCategories { get; }

    public PolicySnapshot(Guid organization, Guid id, DateTimeOffset publishedAt,
        TimeSpan idleLimit, TimeSpan nonTargetLimit, TimeSpan lunchLimit,
        IReadOnlyDictionary<Guid, ActivityCategory> resourceCategories)
    {
        Guard.Id(organization, "organization"); Guard.Id(id, "policy");
        Guard.Require(idleLimit > TimeSpan.Zero && nonTargetLimit > TimeSpan.Zero && lunchLimit > TimeSpan.Zero,
            "Policy limits must be positive.");
        foreach (var pair in resourceCategories)
        {
            Guard.Id(pair.Key, "resource");
            Guard.Require(pair.Value is ActivityCategory.Productive or ActivityCategory.NonTarget or ActivityCategory.Neutral,
                "Invalid resource category.");
        }
        OrganizationId = organization; Id = id; PublishedAt = publishedAt.ToUniversalTime();
        IdleLimit = idleLimit; NonTargetLimit = nonTargetLimit; LunchLimit = lunchLimit;
        ResourceCategories = new ReadOnlyDictionary<Guid, ActivityCategory>(new Dictionary<Guid, ActivityCategory>(resourceCategories));
    }
}
