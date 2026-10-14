using WorkPulse.Application;

namespace WorkPulse.Infrastructure;

public sealed partial class WorkspaceStore(TenantDatabase database,WorkPulse.Application.ActivityReconstructor reconstructor,WorkPulse.Application.ViolationDetector detector) : IEventInbox,IAnalysisResults
{
    private const string ReadScope="(@all OR e.id=@self::uuid OR e.department_id=ANY(@departments))";
    private static (string,object?)[] Scope(UserAccess user)=>[
        ("all",user.IsAdmin || user.Grants.Any(g=>g.Role=="hr" && g.Scope=="organization")),
        ("self",user.Grants.Any(g=>g.Role=="employee" && g.Scope=="self") ? user.EmployeeId : null),
        ("departments",user.Grants.Where(g=>g.Role=="manager" && g.Scope=="department" && g.DepartmentId is not null).Select(g=>g.DepartmentId!.Value).Distinct().ToArray())];
    public Task<T> ForUser<T>(UserAccess user, Func<TenantTransaction,Task<T>> action)=>database.Execute(user.OrganizationId,user.UserId,action);
    public static void Admin(UserAccess user)
    {
        if(!user.IsAdmin) throw new RequestFault(403,"Требуется роль администратора организации.");
    }
    public static async Task<Dictionary<string,object?>> Employee(TenantTransaction db,UserAccess user,Guid id,bool review=false)
    {
        var row=await db.One("SELECT * FROM employees WHERE id=@id",("id",id)) ?? throw new RequestFault(404,"Сотрудник не найден.");
        var allowed=review ? user.CanReview(id,(Guid)row["department_id"]!) : user.CanRead(id,(Guid)row["department_id"]!);
        if(!allowed) throw new RequestFault(404,"Сотрудник не найден.");
        return row;
    }
    public Task<List<Dictionary<string,object?>>> Employees(UserAccess user)=>ForUser(user,async db=>
        (await db.Query("SELECT e.*,d.name AS department_name FROM employees e JOIN departments d ON d.organization_id=e.organization_id AND d.id=e.department_id WHERE "+ReadScope+" ORDER BY e.full_name LIMIT 1000",Scope(user)))
        .Where(e=>user.CanRead((Guid)e["id"]!,(Guid)e["department_id"]!)).ToList());

    public Task<List<Dictionary<string,object?>>> Departments(UserAccess user)=>ForUser(user,async db=>
    {
        var visible=(await EmployeesInTransaction(db,user)).Select(e=>(Guid)e["department_id"]!).ToHashSet();
        return (await db.Query("SELECT * FROM departments ORDER BY name LIMIT 1000")).Where(d=>user.IsAdmin || visible.Contains((Guid)d["id"]!)).ToList();
    });
    private static async Task<List<Dictionary<string,object?>>> EmployeesInTransaction(TenantTransaction db,UserAccess user)=>
        (await db.Query("SELECT e.* FROM employees e WHERE "+ReadScope+" LIMIT 1000",Scope(user))).Where(e=>user.CanRead((Guid)e["id"]!,(Guid)e["department_id"]!)).ToList();

    public Task<List<Dictionary<string,object?>>> Devices(UserAccess user)=>ForUser(user,async db=>
    {
        var ids=(await EmployeesInTransaction(db,user)).Select(e=>(Guid)e["id"]!).ToHashSet();
        return (await db.Query("SELECT d.* FROM devices d JOIN employees e ON e.organization_id=d.organization_id AND e.id=d.employee_id WHERE "+ReadScope+" ORDER BY d.registered_at DESC LIMIT 1000",Scope(user))).Where(d=>ids.Contains((Guid)d["employee_id"]!)).ToList();
    });

    public Task<object> CreateDevice(UserAccess user,Guid employee,string name,string platform)=>ForUser<object>(user,async db=>
    {
        Admin(user); await Employee(db,user,employee);
        var device=Guid.NewGuid();var credential=Guid.NewGuid(); var token=IdentityStore.NewToken(user.OrganizationId,credential);
        await db.Query("INSERT INTO devices(organization_id,id,employee_id,name,platform) VALUES(@org,@id,@employee,@name,@platform)",
            ("org",user.OrganizationId),("id",device),("employee",employee),("name",name),("platform",platform));
        await db.Query("INSERT INTO device_credentials(organization_id,id,device_id,token_hash) VALUES(@org,@id,@device,@hash)",
            ("org",user.OrganizationId),("id",credential),("device",device),("hash",IdentityStore.HashToken(token)));
        return new {id=device,credentialId=credential,token};
    });

    public Task<object> RotateDevice(UserAccess user,Guid device)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        _=await db.One("SELECT id FROM devices WHERE id=@id AND retired_at IS NULL FOR UPDATE",("id",device)) ?? throw new RequestFault(404,"Устройство не найдено.");
        await db.Query("UPDATE device_credentials SET revoked_at=now() WHERE device_id=@id AND revoked_at IS NULL",("id",device));
        var credential=Guid.NewGuid();var token=IdentityStore.NewToken(user.OrganizationId,credential);
        await db.Query("INSERT INTO device_credentials(organization_id,id,device_id,token_hash) VALUES(@org,@id,@device,@hash)",
            ("org",user.OrganizationId),("id",credential),("device",device),("hash",IdentityStore.HashToken(token)));
        return new {id=device,credentialId=credential,token};
    });

    public Task<bool> RetireDevice(UserAccess user,Guid device)=>ForUser(user,async db=>
    {
        Admin(user);
        if(await db.One("UPDATE devices SET retired_at=coalesce(retired_at,now()) WHERE id=@id RETURNING id",("id",device)) is null) throw new RequestFault(404,"Устройство не найдено.");
        await db.Query("UPDATE device_credentials SET revoked_at=now() WHERE device_id=@id AND revoked_at IS NULL",("id",device));
        return true;
    });

    public Task<List<Dictionary<string,object?>>> Policies(UserAccess user)=>ForUser(user,db=>db.Query("SELECT p.name,v.*,(SELECT coalesce(jsonb_agg(jsonb_build_object('isoWeekday',iso_weekday,'startsAt',starts_at,'endsAt',ends_at,'endsNextDay',ends_next_day)),'[]'::jsonb)::text FROM schedule_days WHERE organization_id=v.organization_id AND policy_version_id=v.id) AS schedule FROM monitoring_policies p JOIN policy_versions v ON v.organization_id=p.organization_id AND v.policy_id=p.id ORDER BY p.name,v.version_number DESC LIMIT 1000"));
    public Task<List<Dictionary<string,object?>>> Resources(UserAccess user)=>ForUser(user,db=>db.Query("SELECT * FROM resources ORDER BY identifier LIMIT 1000"));

    public Task<List<Dictionary<string,object?>>> Violations(UserAccess user)=>ForUser(user,async db=>
    {
        var ids=(await EmployeesInTransaction(db,user)).Select(e=>(Guid)e["id"]!).ToHashSet();
        return (await db.Query("SELECT v.*,e.full_name,e.department_id FROM violations v JOIN employees e ON e.organization_id=v.organization_id AND e.id=v.employee_id WHERE "+ReadScope+" AND (v.latest_run_id IS NULL OR v.latest_run_id=(SELECT r.id FROM analytics_runs r WHERE r.organization_id=v.organization_id AND r.session_id=v.session_id AND r.status='completed' ORDER BY r.completed_at DESC,r.id LIMIT 1)) ORDER BY v.detected_at DESC LIMIT 1000",Scope(user)))
            .Where(v=>ids.Contains((Guid)v["employee_id"]!)).ToList();
    });
    public Task<object> ReviewViolation(UserAccess user,Guid id,string status,string? comment)=>ForUser<object>(user,async db=>
    {
        var violation=await db.One("SELECT * FROM violations WHERE id=@id FOR UPDATE",("id",id)) ?? throw new RequestFault(404,"Нарушение не найдено.");
        await Employee(db,user,(Guid)violation["employee_id"]!,true);
        if(status is not ("reviewed" or "dismissed")) throw new RequestFault(400,"Недопустимый статус.");
        await db.Query("UPDATE violations SET status=@status,reviewed_by=@user WHERE id=@id",("status",status),("user",user.UserId),("id",id));
        if(!string.IsNullOrWhiteSpace(comment)) await db.Query("INSERT INTO violation_comments(organization_id,violation_id,author_id,body) VALUES(@org,@id,@user,@body)",
            ("org",user.OrganizationId),("id",id),("user",user.UserId),("body",comment));
        return new {id,status};
    });
    public Task<object> ViolationDetails(UserAccess user,Guid id)=>ForUser<object>(user,async db=>
    {
        var row=await db.One("SELECT * FROM violations WHERE id=@id",("id",id)) ?? throw new RequestFault(404,"Нарушение не найдено.");
        await Employee(db,user,(Guid)row["employee_id"]!);
        var comments=await db.Query("SELECT c.body,c.created_at,u.email FROM violation_comments c JOIN user_accounts u ON u.organization_id=c.organization_id AND u.id=c.author_id WHERE c.violation_id=@id ORDER BY c.created_at",("id",id));
        return new {violation=row,comments};
    });
}
