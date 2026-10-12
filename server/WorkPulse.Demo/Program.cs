using System.Text;
using WorkPulse.Application;
using WorkPulse.Domain;
using WorkPulse.Demo;

Console.OutputEncoding = Encoding.UTF8;
var org = Guid.Parse("10000000-0000-0000-0000-000000000001");
var employee = Guid.Parse("30000000-0000-0000-0000-000000000001");
var device = Guid.Parse("40000000-0000-0000-0000-000000000001");
var resource = Guid.Parse("70000000-0000-0000-0000-000000000001");
var start = new DateTimeOffset(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
var policy = new PolicySnapshot(org, Guid.NewGuid(), start.AddDays(-1), TimeSpan.FromSeconds(30),
    TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(30), new Dictionary<Guid, ActivityCategory> { [resource] = ActivityCategory.Productive });
var session = WorkSession.Start(org, employee, Guid.NewGuid(), Guid.NewGuid(), start, policy);
session.StartBreak(Guid.NewGuid(), BreakKind.Lunch, start.AddMinutes(3));
session.EndBreak(start.AddMinutes(4));
session.End(start.AddMinutes(6), EndReason.Manual);
ActivityEvent Event(int seconds, EventKind kind) => new(org, employee, device, Guid.NewGuid(),
    start.AddSeconds(seconds), start.AddSeconds(seconds + 1), kind, kind == EventKind.Focus ? resource : null);
var events = new[] { Event(0, EventKind.Focus), Event(60, EventKind.Idle), Event(120, EventKind.Heartbeat),
    Event(240, EventKind.Focus), Event(300, EventKind.Heartbeat) };
var store = new InMemoryAnalysisRunStore();
var service = new AnalysisService(new(new PolicyResourceClassifier()), new(new()), store);
var run = service.Analyze(session, policy, session.EndedAt!.Value, [device], events, new(TimeSpan.FromMinutes(2)));
Console.WriteLine("WorkPulse – демонстрация алгоритмов ЛР8 (UTC, синтетические данные)");
foreach (var i in run.Intervals) Console.WriteLine($"{i.Start:HH:mm:ss} – {i.End:HH:mm:ss} | {i.Category,-18} | {i.Duration.TotalSeconds,4} с");
foreach (var v in run.Violations) Console.WriteLine($"Нарушение {v.Kind}: {v.Start:HH:mm:ss} – {v.End:HH:mm:ss}; лимит {v.Limit.TotalSeconds} с");
Console.WriteLine($"Всего {run.Intervals.Sum(i => i.Duration.TotalSeconds)} с; сохранён {store.Runs.Count} запуск в памяти.");
