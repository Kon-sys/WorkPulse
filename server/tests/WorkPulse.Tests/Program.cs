using WorkPulse.Application;
using WorkPulse.Domain;

var tests = new (string Name, Action Test)[]
{
    ("session guards and single lunch", Tests.SessionGuards),
    ("night shift uses absolute time", Tests.NightShift),
    ("only login or activity can start a session", Tests.StartSignals),
    ("foreign tenant and unpublished policy rejected", Tests.PolicyOwnership),
    ("missing data is unknown, not idle", Tests.Unknown),
    ("idle needs confirmation from every device", Tests.AllDevicesIdle),
    ("latest input selects device; no double accounting", Tests.MultipleDevices),
    ("equal timestamps select deterministic device", Tests.Tie),
    ("duplicate delivery is idempotent; conflict rejected", Tests.Duplicates),
    ("out-of-order delivery does not change result", Tests.Reordered),
    ("expiry produces unknown; heartbeat cannot revive stale state", Tests.Expiry),
    ("breaks override activity and interrupt violation", Tests.Breaks),
    ("strict threshold, continuous non-target across resources", Tests.Threshold),
    ("late event creates a new immutable run", Tests.LateEvent),
    ("tenant/device/period/options validation", Tests.Validation),
    ("unknown resource is neutral; snapshot is immutable", Tests.Classification),
    ("half-open boundaries, period clipping", Tests.Boundaries),
    ("device logout does not close employee session", Tests.DeviceLogout),
    ("random timelines have total coverage and are permutation invariant", Tests.RandomTimelines)
};
int failures = 0;
foreach (var (name, test) in tests)
{
    try { test(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failures++; Console.Error.WriteLine($"FAIL {name}: {e}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} test groups passed.");
return failures == 0 ? 0 : 1;

internal static class Tests
{
    private static readonly Guid Org = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Employee = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid D1 = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid D2 = Guid.Parse("40000000-0000-0000-0000-000000000002");
    private static readonly Guid Productive = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid NonTarget = Guid.Parse("70000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 23, 59, 0, TimeSpan.Zero);
    private static PolicySnapshot Policy() => new(Org, Guid.NewGuid(), Start.AddDays(-1), TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30),
        new Dictionary<Guid, ActivityCategory> { [Productive] = ActivityCategory.Productive, [NonTarget] = ActivityCategory.NonTarget });
    private static WorkSession Session(PolicySnapshot p) => WorkSession.Start(Org, Employee, Guid.NewGuid(), Guid.NewGuid(), Start, p);
    private static ActivityEvent E(int seconds, EventKind kind, Guid? resource = null, Guid? device = null) =>
        new(Org, Employee, device ?? D1, Guid.NewGuid(), Start.AddSeconds(seconds), Start.AddSeconds(seconds + 1), kind, resource);
    private static IReadOnlyList<ActivityInterval> Timeline(WorkSession s, PolicySnapshot p, int seconds,
        IReadOnlyCollection<ActivityEvent> events, IReadOnlyCollection<Guid>? devices = null, int lifetime = 120) =>
        new ActivityReconstructor(new PolicyResourceClassifier()).Reconstruct(s, p, Start.AddSeconds(seconds),
            devices ?? [D1], events, new(TimeSpan.FromSeconds(lifetime)));
    private static IReadOnlyList<Violation> Detect(IReadOnlyList<ActivityInterval> i, PolicySnapshot p) => new ViolationDetector(new()).Detect(i, p);
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    private static void Reject(Action action)
    {
        try { action(); } catch (BusinessRuleException) { return; }
        throw new Exception("Expected BusinessRuleException.");
    }
    private static string Shape(IReadOnlyList<ActivityInterval> intervals) => string.Join(";", intervals.Select(i =>
        $"{i.Start:O}/{i.End:O}/{i.Category}/{string.Join(',', i.Sources)}"));

    public static void SessionGuards()
    {
        var s = Session(Policy()); s.StartBreak(Guid.NewGuid(), BreakKind.Lunch, Start.AddSeconds(10));
        Reject(() => s.StartBreak(Guid.NewGuid(), BreakKind.Rest, Start.AddSeconds(11)));
        Reject(() => s.EndBreak(Start.AddSeconds(10)));
        s.EndBreak(Start.AddSeconds(20));
        Reject(() => s.StartBreak(Guid.NewGuid(), BreakKind.Lunch, Start.AddSeconds(30)));
        Reject(() => s.StartBreak(Guid.NewGuid(), BreakKind.Rest, Start.AddSeconds(15)));
        Reject(() => s.End(Start.AddSeconds(19), EndReason.Manual));
        s.StartBreak(Guid.NewGuid(), BreakKind.AuthorizedAbsence, Start.AddSeconds(30));
        s.End(Start.AddSeconds(40), EndReason.Logout);
        Equal(Start.AddSeconds(40), s.Breaks[1].EndedAt!.Value);
        Reject(() => s.End(Start.AddSeconds(50), EndReason.Manual));
        Reject(() => s.StartBreak(Guid.NewGuid(), BreakKind.Rest, Start.AddSeconds(50)));
    }

    public static void NightShift()
    {
        var s = Session(Policy()); s.End(Start.AddHours(8), EndReason.Manual);
        Equal(TimeSpan.FromHours(8), s.EndedAt!.Value - s.StartedAt);
    }

    public static void StartSignals()
    {
        var p = Policy();
        foreach (var kind in new[] { EventKind.Login, EventKind.Focus, EventKind.Input })
            Equal(Start, WorkSession.StartFromSignal(E(0, kind), Guid.NewGuid(), Guid.NewGuid(), p).StartedAt);
        foreach (var kind in new[] { EventKind.Idle, EventKind.Heartbeat, EventKind.Logout })
            Reject(() => WorkSession.StartFromSignal(E(0, kind), Guid.NewGuid(), Guid.NewGuid(), p));
    }

    public static void PolicyOwnership()
    {
        var p = new PolicySnapshot(Guid.NewGuid(), Guid.NewGuid(), Start.AddDays(-1), TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), new Dictionary<Guid, ActivityCategory>());
        Reject(() => Session(p));
        p = new(Org, Guid.NewGuid(), Start.AddSeconds(1), TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), new Dictionary<Guid, ActivityCategory>());
        Reject(() => Session(p));
    }

    public static void Unknown()
    {
        var p = Policy(); var i = Timeline(Session(p), p, 300, []);
        Equal(1, i.Count); Equal(ActivityCategory.Unknown, i[0].Category); Equal(0, Detect(i, p).Count);
    }

    public static void AllDevicesIdle()
    {
        var p = Policy(); var s = Session(p);
        var one = E(0, EventKind.Idle);
        Equal(ActivityCategory.Unknown, Timeline(s, p, 60, [one], [D1, D2])[0].Category);
        var i = Timeline(s, p, 60, [one, E(0, EventKind.Idle, device: D2)], [D1, D2]);
        Equal(ActivityCategory.Idle, i[0].Category); Equal(1, Detect(i, p).Count);
        var active = Timeline(s, p, 60, [one, E(0, EventKind.Focus, Productive, D2)], [D1, D2]);
        Equal(ActivityCategory.Productive, active[0].Category);
    }

    public static void MultipleDevices()
    {
        var p = Policy(); var i = Timeline(Session(p), p, 60,
            [E(0, EventKind.Focus, Productive), E(0, EventKind.Focus, NonTarget, D2),
             E(10, EventKind.Input, device: D2), E(20, EventKind.Input)], [D1, D2]);
        Equal("Productive,NonTarget,Productive", string.Join(',', i.Select(x => x.Category)));
        Equal(60d, i.Sum(x => x.Duration.TotalSeconds));
    }

    public static void Tie()
    {
        var p = Policy(); var i = Timeline(Session(p), p, 60,
            [E(0, EventKind.Focus, NonTarget, D2), E(0, EventKind.Focus, Productive)], [D2, D1]);
        Equal(ActivityCategory.Productive, i[0].Category);
    }

    public static void Duplicates()
    {
        var p = Policy(); var s = Session(p); var e = E(0, EventKind.Focus, Productive);
        Equal(Shape(Timeline(s, p, 60, [e])), Shape(Timeline(s, p, 60, [e, e with { ReceivedAt = Start.AddMinutes(10) }])));
        Reject(() => Timeline(s, p, 60, [e, e with { ResourceId = NonTarget }]));
    }

    public static void Reordered()
    {
        var p = Policy(); var s = Session(p); var events = new[] { E(0, EventKind.Focus, NonTarget), E(30, EventKind.Input), E(50, EventKind.Idle) };
        Equal(Shape(Timeline(s, p, 100, events)), Shape(Timeline(s, p, 100, events.Reverse().ToArray())));
    }

    public static void Expiry()
    {
        var p = Policy(); var s = Session(p);
        var i = Timeline(s, p, 90, [E(0, EventKind.Focus, Productive), E(50, EventKind.Heartbeat)], lifetime: 30);
        Equal("Productive,Unknown", string.Join(',', i.Select(x => x.Category)));
        Equal(30d, i[0].Duration.TotalSeconds);
        Equal(ActivityCategory.Unknown, Timeline(s, p, 30, [E(0, EventKind.Login)])[0].Category);
        var continuous = Timeline(s, p, 60, [E(0, EventKind.Focus, Productive), E(30, EventKind.Heartbeat)], lifetime: 30);
        Equal(1, continuous.Count); Equal(ActivityCategory.Productive, continuous[0].Category);
    }

    public static void Breaks()
    {
        var p = Policy(); var s = Session(p);
        s.StartBreak(Guid.NewGuid(), BreakKind.Lunch, Start.AddSeconds(20)); s.EndBreak(Start.AddSeconds(60));
        s.StartBreak(Guid.NewGuid(), BreakKind.AuthorizedAbsence, Start.AddSeconds(70)); s.EndBreak(Start.AddSeconds(90));
        var i = Timeline(s, p, 100, [E(0, EventKind.Idle)]);
        Equal("Idle,Lunch,Idle,AuthorizedAbsence,Idle", string.Join(',', i.Select(x => x.Category)));
        var v = Detect(i, p); Equal(1, v.Count); Equal(ViolationKind.LunchOverrun, v[0].Kind);
    }

    public static void Threshold()
    {
        var p = Policy(); var s = Session(p); var e = E(0, EventKind.Focus, NonTarget);
        Equal(0, Detect(Timeline(s, p, 30, [e]), p).Count);
        Equal(1, Detect(Timeline(s, p, 31, [e]), p).Count);
        var another = Guid.NewGuid(); var classifier = new PolicyResourceClassifier();
        Equal(ActivityCategory.Neutral, classifier.Classify(another, p));
        var i = Timeline(s, p, 40, [e, E(20, EventKind.Input, NonTarget)]);
        Equal(1, i.Count); Equal(1, Detect(i, p).Count);
    }

    private sealed class Store : IAnalysisRunStore
    {
        public List<AnalysisRun> Runs { get; } = [];
        public void Append(AnalysisRun run) => Runs.Add(run);
    }

    public static void LateEvent()
    {
        var p = Policy(); var s = Session(p); var store = new Store();
        var service = new AnalysisService(new(new PolicyResourceClassifier()), new(new()), store);
        var e = E(0, EventKind.Focus, Productive);
        var first = service.Analyze(s, p, Start.AddSeconds(60), [D1], [e], new(TimeSpan.FromSeconds(120)));
        var before = Shape(first.Intervals);
        var second = service.Analyze(s, p, Start.AddSeconds(60), [D1],
            [e, E(20, EventKind.Idle) with { ReceivedAt = Start.AddMinutes(20) }], new(TimeSpan.FromSeconds(120)));
        Equal(2, store.Runs.Count); Equal(before, Shape(first.Intervals));
        Equal(false, first.Id == second.Id); Equal(1, second.Violations.Count);
    }

    public static void Validation()
    {
        var p = Policy(); var s = Session(p); var e = E(0, EventKind.Idle);
        Reject(() => Timeline(s, p, 60, [e with { OrganizationId = Guid.NewGuid() }]));
        Reject(() => Timeline(s, p, 60, [e with { EmployeeId = Guid.NewGuid() }]));
        Reject(() => Timeline(s, p, 60, [e with { DeviceId = D2 }]));
        Reject(() => Timeline(s, p, 60, [e with { OccurredAt = Start.AddSeconds(-1) }]));
        Reject(() => Timeline(s, p, 60, [e], lifetime: 0));
        Reject(() => Timeline(s, Policy(), 60, [e]));
        Reject(() => Timeline(s, p, 60, [e], [D1, D1]));
    }

    public static void Classification()
    {
        var map = new Dictionary<Guid, ActivityCategory> { [Productive] = ActivityCategory.Productive };
        var p = new PolicySnapshot(Org, Guid.NewGuid(), Start.AddDays(-1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), map);
        map[Productive] = ActivityCategory.NonTarget;
        var classifier = new PolicyResourceClassifier();
        Equal(ActivityCategory.Productive, classifier.Classify(Productive, p));
        Equal(ActivityCategory.Neutral, classifier.Classify(Guid.NewGuid(), p));
        Equal(ActivityCategory.Neutral, classifier.Classify(null, p));
    }

    public static void Boundaries()
    {
        var p = Policy(); var s = Session(p); s.End(Start.AddSeconds(60), EndReason.Manual);
        var i = Timeline(s, p, 100, [E(0, EventKind.Focus, Productive)]);
        Equal(Start.AddSeconds(60), i[^1].End);
        Reject(() => Timeline(s, p, 100, [E(60, EventKind.Idle)]));
        var open = Session(p); open.StartBreak(Guid.NewGuid(), BreakKind.Lunch, Start.AddSeconds(50));
        Equal(10d, Timeline(open, p, 60, [])[1].Duration.TotalSeconds);
    }

    public static void DeviceLogout()
    {
        var p = Policy(); var s = Session(p);
        var i = Timeline(s, p, 90, [E(0, EventKind.Focus, Productive), E(0, EventKind.Focus, NonTarget, D2),
            E(30, EventKind.Logout)], [D1, D2]);
        Equal(ActivityCategory.NonTarget, i[^1].Category);
        Equal(true, s.EndedAt is null);
    }

    public static void RandomTimelines()
    {
        var random = new Random(814301);
        for (int iteration = 0; iteration < 100; iteration++)
        {
            var p = Policy(); var s = Session(p);
            var events = Enumerable.Range(0, 40).Select(_ => E(random.Next(0, 300),
                (EventKind)random.Next(0, 6), random.Next(0, 2) == 0 ? Productive : NonTarget,
                random.Next(0, 2) == 0 ? D1 : D2)).ToArray();
            var i = Timeline(s, p, 300, events, [D1, D2], 40);
            Equal(Start, i[0].Start); Equal(Start.AddSeconds(300), i[^1].End);
            Equal(300d, i.Sum(x => x.Duration.TotalSeconds));
            for (int j = 0; j < i.Count; j++)
            {
                Equal(true, i[j].Duration > TimeSpan.Zero);
                if (j > 0) { Equal(i[j - 1].End, i[j].Start); Equal(false, i[j - 1].Category == i[j].Category); }
            }
            Equal(Shape(i), Shape(Timeline(s, p, 300, events.OrderBy(_ => random.Next()).ToArray(), [D2, D1], 40)));
        }
    }
}
