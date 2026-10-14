using WorkPulse.Domain;

namespace WorkPulse.Application;

public sealed record ScheduleDay(int IsoWeekday,TimeOnly StartsAt,TimeOnly EndsAt,bool EndsNextDay);
public sealed record ScheduleEvaluation(bool? Compliant,string Reason,IReadOnlyList<Violation> Violations);
public static class ScheduleEvaluator
{
    public static ScheduleEvaluation Evaluate(WorkSession session,IReadOnlyList<ActivityInterval> intervals,
        IReadOnlyCollection<ActivityEvent> events,string timeZone,IReadOnlyList<ScheduleDay> days,int lateGrace,int earlyGrace)
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        var local=TimeZoneInfo.ConvertTime(session.StartedAt,zone).DateTime;
        var candidates=new List<(DateTimeOffset Start,DateTimeOffset End)>();
        foreach(var date in new[]{DateOnly.FromDateTime(local).AddDays(-1),DateOnly.FromDateTime(local)})
        {
            var weekday=((int)date.DayOfWeek+6)%7+1;
            foreach(var day in days.Where(d=>d.IsoWeekday==weekday))
            {
                DateTimeOffset? Convert(DateTime value)
                {
                    value=DateTime.SpecifyKind(value,DateTimeKind.Unspecified);
                    if(zone.IsInvalidTime(value)||zone.IsAmbiguousTime(value))return null;
                    return new DateTimeOffset(value,zone.GetUtcOffset(value)).ToUniversalTime();
                }
                var start=Convert(date.ToDateTime(day.StartsAt));
                var end=Convert(date.AddDays(day.EndsNextDay?1:0).ToDateTime(day.EndsAt));
                if(start is { } s && end is { } e && session.StartedAt<e && session.StartedAt>=s.AddHours(-4))candidates.Add((s,e));
            }
        }
        if(candidates.Count!=1)return new(null,"Нет однозначной смены (выходной, DST или начало вне окна смены).",[]);
        var shift=candidates[0];var violations=new List<Violation>();
        bool startKnown=events.Any(e=>e.OccurredAt==session.StartedAt && e.Kind is EventKind.Login or EventKind.Focus or EventKind.Input);
        bool endKnown=session.EndedAt is { } ended && intervals.Count>0 && intervals[^1].End==ended && intervals[^1].Category is not (ActivityCategory.Unknown or ActivityCategory.AuthorizedAbsence);
        if(startKnown && session.StartedAt>shift.Start.AddSeconds(lateGrace))
            violations.Add(new(ViolationKind.LateArrival,shift.Start,session.StartedAt,TimeSpan.FromSeconds(lateGrace)));
        if(endKnown && session.EndedAt is { } endAt && endAt<shift.End.AddSeconds(-earlyGrace))
            violations.Add(new(ViolationKind.EarlyDeparture,endAt,shift.End,TimeSpan.FromSeconds(earlyGrace)));
        return new(startKnown&&endKnown ? violations.Count==0 : null,
            startKnown&&endKnown ? "Начало и окончание подтверждены." : "Недостаточно данных о начале или окончании; неизвестность не считается нарушением.",violations);
    }
}
public sealed record TimeMetrics(double ProductiveSeconds,double NonTargetSeconds,double NeutralSeconds,double IdleSeconds,
    double UnknownSeconds,double ExcludedSeconds,double ObservedSeconds,double KnownSeconds,
    double? ProductiveShare,double? NonTargetShare,double? IdleShare,double? Coverage);
public static class MetricsCalculator
{
    public static TimeMetrics Calculate(IEnumerable<ActivityInterval> intervals,DateTimeOffset start,DateTimeOffset end)
    {
        if(end<=start)throw new RequestFault(400,"Некорректный период.");
        var total=new double[7];
        foreach(var i in intervals)
        {
            var left=i.Start>start?i.Start:start;var right=i.End<end?i.End:end;
            if(right>left)total[(int)i.Category]+=(right-left).TotalSeconds;
        }
        var known=total[0]+total[1]+total[2]+total[3];var excluded=total[4]+total[5];var observed=known+total[6];
        double? Share(double value,double denominator)=>denominator>0?value/denominator*100:null;
        return new(total[0],total[1],total[2],total[3],total[6],excluded,observed,known,
            Share(total[0],known),Share(total[1],known),Share(total[3],known),Share(known,observed));
    }
}
