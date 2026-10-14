using System.Text.Json;
using WorkPulse.Application;
using WorkPulse.Domain;

namespace WorkPulse.Infrastructure;
public sealed record EmployeeReport(Guid EmployeeId,string Name,Guid DepartmentId,string DepartmentName,TimeMetrics Metrics,
    int Violations,int AssessedSchedules,int CompliantSchedules,double? ScheduleCompliance,IReadOnlyList<ActivityInterval> Intervals);
public sealed record DepartmentReport(Guid DepartmentId,string Name,TimeMetrics Metrics,int Violations,int AssessedSchedules,int CompliantSchedules,double? ScheduleCompliance);
public sealed record ReportResult(DateTimeOffset Start,DateTimeOffset End,IReadOnlyList<EmployeeReport> Employees,IReadOnlyList<DepartmentReport> Departments,string Basis);
public sealed partial class WorkspaceStore
{
    public Task<ReportResult> Report(UserAccess user,DateTimeOffset start,DateTimeOffset end,Guid? employee,Guid? department)=>ForUser(user,async db=>
    {
        start=start.ToUniversalTime();end=end.ToUniversalTime();
        if(end<=start || end-start>TimeSpan.FromDays(93) || end>DateTimeOffset.UtcNow)throw new RequestFault(400,"Выберите прошедший период от 1 секунды до 93 дней.");
        if(employee is { } requestedEmployee)await Employee(db,user,requestedEmployee);
        var args=Scope(user).Concat(new (string,object?)[]{("employee",employee),("department",department),("start",start),("end",end)}).ToArray();
        var people=await db.Query("SELECT e.id,e.full_name,e.department_id,d.name AS department_name FROM employees e JOIN departments d ON d.organization_id=e.organization_id AND d.id=e.department_id WHERE "+ReadScope+" AND (@employee::uuid IS NULL OR e.id=@employee) AND (@department::uuid IS NULL OR e.department_id=@department) ORDER BY e.full_name LIMIT 1001",args);
        if(people.Count>1000)throw new RequestFault(400,"Сузьте выборку: более 1000 сотрудников.");
        var sessions=await db.Query("SELECT s.employee_id,s.started_at,s.ended_at,r.result_snapshot::text AS snapshot FROM work_sessions s JOIN employees e ON e.organization_id=s.organization_id AND e.id=s.employee_id LEFT JOIN LATERAL (SELECT result_snapshot FROM analytics_runs WHERE organization_id=s.organization_id AND session_id=s.id AND status='completed' ORDER BY completed_at DESC,id LIMIT 1) r ON true WHERE "+ReadScope+" AND (@employee::uuid IS NULL OR e.id=@employee) AND (@department::uuid IS NULL OR e.department_id=@department) AND s.started_at<@end AND (s.ended_at IS NULL OR s.ended_at>@start) ORDER BY s.started_at LIMIT 10001",args);
        if(sessions.Count>10000)throw new RequestFault(400,"Сузьте период: более 10000 сессий.");
        var result=new List<EmployeeReport>();var totalIntervalCount=0;
        foreach(var person in people)
        {
            var id=(Guid)person["id"]!;var intervals=new List<ActivityInterval>();var violations=0;var assessed=0;var compliant=0;
            foreach(var row in sessions.Where(s=>(Guid)s["employee_id"]! == id))
            {
                var left=At(row["started_at"]!)>start?At(row["started_at"]!):start;
                var right=row["ended_at"] is { } ended && At(ended)<end?At(ended):end;
                if(right<=left)continue;
                var previousCount=intervals.Count;
                var run=row["snapshot"] is string json ? JsonSerializer.Deserialize<AnalysisRun>(json,SnapshotJson):null;
                var clipped=run?.Intervals.Select(i=>i with {Start=i.Start>left?i.Start:left,End=i.End<right?i.End:right}).Where(i=>i.End>i.Start).ToArray() ?? [];
                var cursor=left;
                foreach(var interval in clipped)
                {
                    if(interval.Start>cursor)intervals.Add(new(cursor,interval.Start,ActivityCategory.Unknown,[]));
                    intervals.Add(interval);cursor=interval.End;
                }
                if(cursor<right)intervals.Add(new(cursor,right,ActivityCategory.Unknown,[]));
                totalIntervalCount+=intervals.Count-previousCount;
                if(totalIntervalCount>200000)throw new RequestFault(400,"Сузьте период: более 200000 интервалов.");
                if(run is not null)
                {
                    violations+=run.Violations.Count(v=>v.Start<end && v.End>start);
                    // Compliance applies only when the whole assessed session is included, not a clipped fragment.
                    if(run.Schedule?.Compliant is { } ok && At(row["started_at"]!)>=start && row["ended_at"] is { } complete && At(complete)<=end)
                    {assessed++;if(ok)compliant++;}
                }
            }
            result.Add(new(id,(string)person["full_name"]!,(Guid)person["department_id"]!,(string)person["department_name"]!,MetricsCalculator.Calculate(intervals,start,end),violations,assessed,compliant,assessed>0?compliant*100d/assessed:null,intervals));
        }
        var departments=result.GroupBy(e=>e.DepartmentId).Select(g=>new DepartmentReport(g.Key,g.First().DepartmentName,MetricsCalculator.Calculate(g.SelectMany(e=>e.Intervals),start,end),g.Sum(e=>e.Violations),g.Sum(e=>e.AssessedSchedules),g.Sum(e=>e.CompliantSchedules),g.Sum(e=>e.AssessedSchedules)>0?g.Sum(e=>e.CompliantSchedules)*100d/g.Sum(e=>e.AssessedSchedules):null)).ToArray();
        return new ReportResult(start,end,result,departments,"Последний завершённый анализ каждой сессии; непокрытое время сессии – Unknown. Время вне сессий не интерпретируется как простой.");
    });
    public Task<object> CreateResource(UserAccess user,string kind,string identifier)=>ForUser<object>(user,async db=>
    {
        Admin(user);identifier=identifier.Trim().ToLowerInvariant();
        if(kind is not ("application" or "domain") || identifier.Length is <1 or >128 || (kind=="domain" ? !System.Text.RegularExpressions.Regex.IsMatch(identifier,@"^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$") : !System.Text.RegularExpressions.Regex.IsMatch(identifier,@"^[\p{L}\p{N}._-]+$")))throw new RequestFault(400,"Введите имя процесса или домен без URL/пути.");
        return (await db.One("INSERT INTO resources(organization_id,kind,identifier,display_name) VALUES(@org,@kind,@identifier,@identifier) ON CONFLICT(organization_id,kind,identifier) DO UPDATE SET display_name=EXCLUDED.display_name RETURNING *",("org",user.OrganizationId),("kind",kind),("identifier",identifier)))!;
    });
}
