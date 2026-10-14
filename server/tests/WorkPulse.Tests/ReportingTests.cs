using WorkPulse.Application;
using WorkPulse.Domain;
using Xunit;
namespace WorkPulse.Tests;
public sealed class ReportingTests
{
    private static readonly DateTimeOffset At=new(2026,10,14,6,0,0,TimeSpan.Zero);
    [Fact]
    public void SharesExcludeUnknownLunchAndAuthorizedAbsence()
    {
        ActivityInterval I(int a,int b,ActivityCategory c)=>new(At.AddSeconds(a),At.AddSeconds(b),c,[]);
        var m=MetricsCalculator.Calculate([I(0,20,ActivityCategory.Productive),I(20,30,ActivityCategory.NonTarget),I(30,40,ActivityCategory.Idle),I(40,50,ActivityCategory.Unknown),I(50,60,ActivityCategory.Lunch),I(60,70,ActivityCategory.AuthorizedAbsence)],At,At.AddSeconds(70));
        Assert.Equal(50,m.ProductiveShare);Assert.Equal(25,m.NonTargetShare);Assert.Equal(25,m.IdleShare);
        Assert.Equal(80,m.Coverage);Assert.Equal(20,m.ExcludedSeconds);
    }
    [Fact]
    public void EmptyAndAllUnknownHaveNoProductivityDenominator()
    {
        var empty=MetricsCalculator.Calculate([],At,At.AddSeconds(60));
        Assert.Null(empty.ProductiveShare);Assert.Null(empty.Coverage);
        var unknown=MetricsCalculator.Calculate([new(At,At.AddSeconds(60),ActivityCategory.Unknown,[])],At,At.AddSeconds(60));
        Assert.Null(unknown.ProductiveShare);Assert.Equal(0,unknown.Coverage);
    }
    [Fact]
    public void ReportingClipsHalfOpenPeriod()
    {
        var m=MetricsCalculator.Calculate([new(At,At.AddSeconds(100),ActivityCategory.Productive,[])],At.AddSeconds(20),At.AddSeconds(30));
        Assert.Equal(10,m.KnownSeconds);Assert.Equal(100,m.ProductiveShare);
        Assert.Throws<RequestFault>(()=>MetricsCalculator.Calculate([],At,At));
    }
    private static WorkSession Session(DateTimeOffset start,DateTimeOffset? end=null)
    {
        var policy=new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),start.AddDays(-1),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),new Dictionary<Guid,ActivityCategory>());
        var s=WorkSession.Start(policy.OrganizationId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),start,policy);if(end is { } e)s.End(e,EndReason.Manual);return s;
    }
    private static ActivityEvent Focus(WorkSession s)=>new(s.OrganizationId,s.EmployeeId,Guid.NewGuid(),Guid.NewGuid(),s.StartedAt,s.StartedAt,EventKind.Focus);
    [Theory]
    [InlineData(300,false)] [InlineData(301,true)]
    public void LateArrivalStrictGrace(int delay,bool violated)
    {
        var s=Session(At.AddSeconds(delay),At.AddHours(9));
        var result=ScheduleEvaluator.Evaluate(s,[new(s.StartedAt,s.EndedAt!.Value,ActivityCategory.Productive,[])],[Focus(s)],"Europe/Minsk",[new(3,new(9,0),new(18,0),false)],300,300);
        Assert.Equal(violated,result.Violations.Any(v=>v.Kind==ViolationKind.LateArrival));
        Assert.Equal(!violated,result.Compliant);
    }
    [Theory]
    [InlineData(300,false)] [InlineData(301,true)]
    public void EarlyDepartureStrictGrace(int early,bool violated)
    {
        var s=Session(At,At.AddHours(9).AddSeconds(-early));
        var result=ScheduleEvaluator.Evaluate(s,[new(s.StartedAt,s.EndedAt!.Value,ActivityCategory.Productive,[])],[Focus(s)],"Europe/Minsk",[new(3,new(9,0),new(18,0),false)],300,300);
        Assert.Equal(violated,result.Violations.Any(v=>v.Kind==ViolationKind.EarlyDeparture));
    }
    [Fact]
    public void MissingStartOrEndIsNotAScheduleViolation()
    {
        var s=Session(At.AddMinutes(10),At.AddHours(8));
        var result=ScheduleEvaluator.Evaluate(s,[new(s.StartedAt,s.EndedAt!.Value,ActivityCategory.Unknown,[])],[],"Europe/Minsk",[new(3,new(9,0),new(18,0),false)],300,300);
        Assert.Null(result.Compliant);Assert.Empty(result.Violations);
    }
    [Fact]
    public void NightShiftUsesPreviousLocalDate()
    {
        var start=new DateTimeOffset(2026,10,13,22,0,0,TimeSpan.Zero); // Wed 01:00 Minsk, Tuesday shift 22:00-06:00.
        var s=Session(start,start.AddHours(5));
        var result=ScheduleEvaluator.Evaluate(s,[new(start,s.EndedAt!.Value,ActivityCategory.Productive,[])],[Focus(s)],"Europe/Minsk",[new(2,new(22,0),new(6,0),true)],300,300);
        Assert.Contains(result.Violations,v=>v.Kind==ViolationKind.LateArrival);
        Assert.DoesNotContain(result.Violations,v=>v.Kind==ViolationKind.EarlyDeparture);
    }
    [Fact]
    public void NoScheduleAndOpenSessionDoNotClaimCompliance()
    {
        var s=Session(At);Assert.Null(ScheduleEvaluator.Evaluate(s,[],[],"Europe/Minsk",[],300,300).Compliant);
        Assert.Null(ScheduleEvaluator.Evaluate(s,[],[Focus(s)],"Europe/Minsk",[new(3,new(9,0),new(18,0),false)],300,300).Compliant);
    }
    [Fact]
    public void AuthorizedAbsenceAtEndDoesNotBecomeEarlyDeparture()
    {
        var s=Session(At,At.AddHours(8));
        var result=ScheduleEvaluator.Evaluate(s,[new(At,s.EndedAt!.Value,ActivityCategory.AuthorizedAbsence,[])],[Focus(s)],"Europe/Minsk",[new(3,new(9,0),new(18,0),false)],300,300);
        Assert.DoesNotContain(result.Violations,v=>v.Kind==ViolationKind.EarlyDeparture);
        Assert.Null(result.Compliant);
    }
    [Fact]
    public void DifferentNonTargetResourcesFormOneContinuousViolation()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var device=Guid.NewGuid();
        var policy=new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),At.AddDays(-1),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),new Dictionary<Guid,ActivityCategory>{{a,ActivityCategory.NonTarget},{b,ActivityCategory.NonTarget}});
        var session=WorkSession.Start(policy.OrganizationId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),At,policy);
        ActivityEvent Focus(int seconds,Guid resource)=>new(session.OrganizationId,session.EmployeeId,device,Guid.NewGuid(),At.AddSeconds(seconds),At.AddSeconds(seconds),EventKind.Focus,resource);
        var intervals=new ActivityReconstructor(new PolicyResourceClassifier()).Reconstruct(session,policy,At.AddSeconds(40),[device],[Focus(0,a),Focus(20,b)],new(TimeSpan.FromSeconds(120)));
        Assert.Single(intervals);var violation=Assert.Single(new ViolationDetector(new()).Detect(intervals,policy));
        Assert.Equal(ViolationKind.NonTarget,violation.Kind);Assert.Equal(TimeSpan.FromSeconds(40),violation.Duration);
    }
}
