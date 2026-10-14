using WorkPulse.Application;
using WorkPulse.Domain;
using Xunit;

namespace WorkPulse.Tests;

public sealed class AccessAndPolicyTests
{
    private static readonly Guid Self = Guid.NewGuid();
    private static readonly Guid Coworker = Guid.NewGuid();
    private static readonly Guid Department = Guid.NewGuid();
    private static readonly Guid OtherDepartment = Guid.NewGuid();
    private static UserAccess User(string role,string scope,Guid? department=null) =>
        new(Guid.NewGuid(),Guid.NewGuid(),Self,"test@example.test",Guid.NewGuid(),[new(role,scope,department)]);

    [Theory]
    [InlineData("admin","organization",true,true,true)]
    [InlineData("hr","organization",true,true,false)]
    [InlineData("manager","department",true,true,false)]
    [InlineData("employee","self",false,false,false)]
    [InlineData("unknown","organization",false,false,false)]
    [InlineData("admin","self",false,false,false)]
    [InlineData("manager","organization",false,false,false)]
    public void CoworkerAccessRequiresValidRoleAndScope(string role,string scope,bool read,bool review,bool admin)
    {
        var user=User(role,scope,Department);
        Assert.Equal(read,user.CanRead(Coworker,Department));
        Assert.Equal(review,user.CanReview(Coworker,Department));
        Assert.Equal(admin,user.IsAdmin);
    }
    [Fact]
    public void ManagerCannotReadOrReviewAnotherDepartment()
    {
        var user=User("manager","department",Department);
        Assert.False(user.CanRead(Coworker,OtherDepartment));
        Assert.False(user.CanReview(Coworker,OtherDepartment));
    }
    [Fact]
    public void EmployeeReadsSelfButCannotReviewEvenSelf()
    {
        var user=User("employee","self");
        Assert.True(user.CanRead(Self,Department));
        Assert.False(user.CanRead(Coworker,Department));
        Assert.False(user.CanReview(Self,Department));
        Assert.False(user.CanRead(Coworker,OtherDepartment));
    }
    [Fact]
    public void NoGrantDeniesAllOperations()
    {
        var user=User("employee","self") with {Grants=[]};
        Assert.False(user.IsAdmin);
        Assert.False(user.CanRead(Self,Department));
        Assert.False(user.CanReview(Self,Department));
    }
    [Theory]
    [InlineData(0,10,10)] [InlineData(10,0,10)] [InlineData(10,10,0)]
    [InlineData(-1,10,10)] [InlineData(10,-1,10)] [InlineData(10,10,-1)]
    public void EveryPolicyLimitMustBePositive(int idle,int target,int lunch) =>
        Assert.Throws<BusinessRuleException>(()=>new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(idle),TimeSpan.FromSeconds(target),TimeSpan.FromSeconds(lunch),new Dictionary<Guid,ActivityCategory>()));
    [Theory]
    [InlineData(ActivityCategory.Idle)] [InlineData(ActivityCategory.Lunch)]
    [InlineData(ActivityCategory.AuthorizedAbsence)] [InlineData(ActivityCategory.Unknown)]
    public void ResourceRuleCannotPretendToBeTimeState(ActivityCategory category) =>
        Assert.Throws<BusinessRuleException>(()=>new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),new Dictionary<Guid,ActivityCategory>{{Guid.NewGuid(),category}}));
    [Theory]
    [InlineData(ActivityCategory.Productive)] [InlineData(ActivityCategory.Neutral)]
    [InlineData(ActivityCategory.AuthorizedAbsence)] [InlineData(ActivityCategory.Unknown)]
    public void NonViolationCategoriesDoNotGenerateViolations(ActivityCategory category)
    {
        var at=DateTimeOffset.UtcNow;
        var policy=new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),at.AddDays(-1),TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1),new Dictionary<Guid,ActivityCategory>());
        var result=new ViolationDetector(new()).Detect([new(at,at.AddHours(1),category,[])],policy);
        Assert.Empty(result);
    }
    [Theory]
    [InlineData(29,false)] [InlineData(30,false)] [InlineData(31,true)]
    public void LunchThresholdIsStrict(int seconds,bool violation)
    {
        var at=DateTimeOffset.UtcNow;
        var policy=new PolicySnapshot(Guid.NewGuid(),Guid.NewGuid(),at.AddDays(-1),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(30),new Dictionary<Guid,ActivityCategory>());
        var result=new ViolationDetector(new()).Detect([new(at,at.AddSeconds(seconds),ActivityCategory.Lunch,[])],policy);
        Assert.Equal(violation,result.Count==1);
        if(violation) Assert.Equal(ViolationKind.LunchOverrun,result[0].Kind);
    }
}
