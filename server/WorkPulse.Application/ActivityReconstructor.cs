using WorkPulse.Domain;

namespace WorkPulse.Application;

public sealed record AnalysisOptions(TimeSpan SignalLifetime)
{
    public void Validate() => Guard.Require(SignalLifetime > TimeSpan.Zero && SignalLifetime <= TimeSpan.FromDays(1),
        "Signal lifetime must be positive and at most one day.");
}

/// <summary>Half-open timeline built in event time, independent of delivery order.</summary>
public sealed class ActivityReconstructor(IResourceClassifier classifier)
{
    private sealed class DeviceState
    {
        public ActivityEvent? Observation;
        public ActivityEvent? Focus;
        public ActivityEvent? Input;
        public bool? Idle;
        public bool LoggedOut;

        public void Apply(ActivityEvent e)
        {
            Observation = e;
            switch (e.Kind)
            {
                case EventKind.Login:
                    LoggedOut = false; Idle = null; Focus = null; Input = null; break;
                case EventKind.Focus:
                    LoggedOut = false; Idle = false; Focus = e; break;
                case EventKind.Input:
                    LoggedOut = false; Idle = false; Input = e;
                    if (e.ResourceId is not null) Focus = e;
                    break;
                case EventKind.Idle:
                    LoggedOut = false; Idle = true; break;
                case EventKind.Logout:
                    LoggedOut = true; Idle = null; Focus = null; Input = null; break;
                case EventKind.Heartbeat: break;
            }
        }
    }

    public IReadOnlyList<ActivityInterval> Reconstruct(WorkSession session, PolicySnapshot policy,
        DateTimeOffset until, IReadOnlyCollection<Guid> devices, IReadOnlyCollection<ActivityEvent> events,
        AnalysisOptions options)
    {
        options.Validate();
        Guard.Require(policy.Id == session.PolicyVersionId && policy.OrganizationId == session.OrganizationId,
            "Analysis must use the session policy version.");
        var end = session.EndedAt is { } closed && closed < until ? closed : until;
        Guard.Require(end > session.StartedAt, "Empty analysis period.");
        Guard.Require(devices.Count > 0 && devices.Distinct().Count() == devices.Count, "Expected unique device ids.");
        foreach (var id in devices) Guard.Id(id, "device");
        foreach (var e in events)
        {
            Guard.Id(e.ClientEventId, "client event");
            Guard.Require(Enum.IsDefined(e.Kind), "Invalid event kind.");
            Guard.Require(e.ResourceId != Guid.Empty, "Invalid resource id.");
            Guard.Require(e.OrganizationId == session.OrganizationId && e.EmployeeId == session.EmployeeId
                && devices.Contains(e.DeviceId), "Event belongs to another tenant, employee or device.");
            Guard.Require(e.OccurredAt >= session.StartedAt && (session.EndedAt is null || e.OccurredAt < session.EndedAt),
                "Event is outside its session.");
        }

        var unique = events.GroupBy(e => (e.DeviceId, e.ClientEventId)).Select(group =>
        {
            var first = group.First();
            Guard.Require(group.All(e => e.OccurredAt == first.OccurredAt && e.Kind == first.Kind && e.ResourceId == first.ResourceId),
                "Conflicting payload for the same client event.");
            return group.OrderBy(e => e.ReceivedAt).First();
        }).Where(e => e.OccurredAt < end).OrderBy(e => e.OccurredAt).ThenBy(e => e.DeviceId)
            .ThenBy(e => e.Kind).ThenBy(e => e.ClientEventId).ToArray();

        var points = new SortedSet<DateTimeOffset> { session.StartedAt, end };
        foreach (var e in unique)
        {
            points.Add(e.OccurredAt);
            if (end - e.OccurredAt > options.SignalLifetime) points.Add(e.OccurredAt + options.SignalLifetime);
        }
        foreach (var b in session.Breaks)
        {
            if (b.StartedAt > session.StartedAt && b.StartedAt < end) points.Add(b.StartedAt);
            if (b.EndedAt is { } be && be < end) points.Add(be);
        }
        var states = devices.ToDictionary(id => id, _ => new DeviceState());
        var times = points.ToArray();
        var result = new List<ActivityInterval>();
        int cursor = 0;
        for (int i = 0; i < times.Length - 1; i++)
        {
            var start = times[i];
            while (cursor < unique.Length && unique[cursor].OccurredAt <= start)
            {
                var e = unique[cursor++];
                var state = states[e.DeviceId];
                // After loss of coverage, a heartbeat alone cannot revive stale focus/idle evidence.
                if (state.Observation is { } prior && e.OccurredAt - prior.OccurredAt > options.SignalLifetime)
                {
                    state.Focus = null; state.Input = null; state.Idle = null;
                }
                state.Apply(e);
            }
            var currentBreak = session.Breaks.FirstOrDefault(b => b.StartedAt <= start && (b.EndedAt is null || b.EndedAt > start));
            ActivityCategory category;
            var sources = new HashSet<EventReference>();
            if (currentBreak is not null)
                category = currentBreak.Kind switch
                {
                    BreakKind.Lunch => ActivityCategory.Lunch,
                    BreakKind.AuthorizedAbsence => ActivityCategory.AuthorizedAbsence,
                    _ => ActivityCategory.Unknown // Rest has no approved classification/limit.
                };
            else
            {
                var present = states.Where(p => p.Value.Observation is { } o && !p.Value.LoggedOut
                    && start - o.OccurredAt < options.SignalLifetime).ToArray();
                var active = present.Where(p => p.Value.Idle == false)
                    .OrderByDescending(p => p.Value.Input?.OccurredAt ?? p.Value.Focus?.OccurredAt)
                    .ThenBy(p => p.Key).ToArray();
                if (active.Length > 0)
                    category = classifier.Classify(active[0].Value.Focus?.ResourceId, policy);
                else
                    category = present.Length == states.Count && present.All(p => p.Value.Idle == true)
                        ? ActivityCategory.Idle : ActivityCategory.Unknown;
                foreach (var s in present.Select(p => p.Value))
                    foreach (var e in new[] { s.Observation, s.Focus, s.Input })
                        if (e is not null) sources.Add(new(e.DeviceId, e.ClientEventId));
            }
            var next = new ActivityInterval(start, times[i + 1], category, Array.AsReadOnly(sources
                .OrderBy(s => s.DeviceId).ThenBy(s => s.ClientEventId).ToArray()));
            if (result.Count > 0 && result[^1].Category == category && result[^1].End == start)
            {
                var previous = result[^1];
                result[^1] = previous with { End = next.End, Sources = Array.AsReadOnly(previous.Sources.Concat(next.Sources)
                    .Distinct().OrderBy(s => s.DeviceId).ThenBy(s => s.ClientEventId).ToArray()) };
            }
            else result.Add(next);
        }
        return result.AsReadOnly();
    }
}
