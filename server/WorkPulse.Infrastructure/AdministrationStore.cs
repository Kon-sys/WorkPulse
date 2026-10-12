using WorkPulse.Application;

namespace WorkPulse.Infrastructure;

public sealed record PolicyRule(Guid ResourceId,string Category);
public sealed record PolicyInput(string Name,int IdleLimitSeconds,int NonTargetLimitSeconds,int LunchLimitSeconds,string TimeZone,IReadOnlyList<PolicyRule> Rules);

public sealed partial class WorkspaceStore
{
    public Task<object> CreateDepartment(UserAccess user,string code,string name)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        return (await db.One("INSERT INTO departments(organization_id,code,name) VALUES(@org,@code,@name) RETURNING *",("org",user.OrganizationId),("code",code),("name",name)))!;
    });
    public Task<object> CreateEmployee(UserAccess user,Guid department,string number,string name,string? position,DateOnly hired)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        return (await db.One("INSERT INTO employees(organization_id,department_id,employee_number,full_name,position_name,hired_on) VALUES(@org,@department,@number,@name,@position::text,@hired) RETURNING *",
            ("org",user.OrganizationId),("department",department),("number",number),("name",name),("position",position),("hired",hired)))!;
    });
    public Task<List<Dictionary<string,object?>>> Users(UserAccess user)=>ForUser(user,db=>
    {
        Admin(user);
        return db.Query("SELECT u.id,u.employee_id,u.email,u.is_active,coalesce((SELECT jsonb_agg(jsonb_build_object('role',r.role_code,'scope',r.scope,'departmentId',r.department_id)) FROM role_assignments r WHERE r.organization_id=u.organization_id AND r.user_id=u.id),'[]'::jsonb)::text AS grants FROM user_accounts u ORDER BY email LIMIT 1000");
    });
    public Task<object> CreateUser(UserAccess user,Guid? employee,string email,string passwordHash,string role,Guid? department)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        if(role is not ("admin" or "hr" or "manager" or "employee")) throw new RequestFault(400,"Неизвестная роль.");
        if(role=="employee" && employee is null || role=="manager" && department is null) throw new RequestFault(400,"Роль требует сотрудника или подразделение.");
        var id=Guid.NewGuid();
        await db.Query("INSERT INTO user_accounts(organization_id,id,employee_id,email,password_hash) VALUES(@org,@id,@employee::uuid,@email,@hash)",
            ("org",user.OrganizationId),("id",id),("employee",employee),("email",email.ToLowerInvariant()),("hash",passwordHash));
        await db.Query("INSERT INTO role_assignments(organization_id,user_id,role_code,scope,department_id) VALUES(@org,@id,@role,@scope,@department::uuid)",
            ("org",user.OrganizationId),("id",id),("role",role),("scope",role=="employee" ? "self" : role=="manager" ? "department" : "organization"),("department",role=="manager" ? department : null));
        return new {id,email,role};
    });
    public Task<bool> DisableUser(UserAccess user,Guid id)=>ForUser(user,async db=>
    {
        Admin(user);if(id==user.UserId) throw new RequestFault(409,"Нельзя отключить собственную учётную запись.");
        if(await db.One("UPDATE user_accounts SET is_active=false,security_stamp=gen_random_uuid() WHERE id=@id RETURNING id",("id",id)) is null) throw new RequestFault(404,"Учётная запись не найдена.");
        return true;
    });
    public Task<object> CreatePolicy(UserAccess user,PolicyInput input)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        if(input.Rules is null || input.Rules.Count>1000 || input.Rules.Any(r=>r.Category is not ("productive" or "non_target" or "neutral"))) throw new RequestFault(400,"Некорректные правила классификации.");
        var p=await db.One("INSERT INTO monitoring_policies(organization_id,name) VALUES(@org,@name) ON CONFLICT(organization_id,name) DO UPDATE SET name=EXCLUDED.name RETURNING id",("org",user.OrganizationId),("name",input.Name));
        var policy=(Guid)p!["id"]!;var version=Guid.NewGuid();
        await db.Query("INSERT INTO policy_versions(organization_id,id,policy_id,version_number,idle_limit_seconds,non_target_limit_seconds,lunch_limit_seconds,time_zone) SELECT @org,@id,@policy,coalesce(max(version_number),0)+1,@idle,@target,@lunch,@zone FROM policy_versions WHERE policy_id=@policy",
            ("org",user.OrganizationId),("id",version),("policy",policy),("idle",input.IdleLimitSeconds),("target",input.NonTargetLimitSeconds),("lunch",input.LunchLimitSeconds),("zone",input.TimeZone));
        foreach(var rule in input.Rules) await db.Query("INSERT INTO classification_rules(organization_id,policy_version_id,resource_id,category) VALUES(@org,@version,@resource,@category)",
            ("org",user.OrganizationId),("version",version),("resource",rule.ResourceId),("category",rule.Category));
        await db.Query("UPDATE policy_versions SET published_at=now() WHERE id=@id",("id",version));
        return new {id=version,policyId=policy};
    });
    public Task<object> AssignPolicy(UserAccess user,Guid version,string scope,Guid? target)=>ForUser<object>(user,async db=>
    {
        Admin(user);
        if(scope is not ("organization" or "department" or "employee") || (scope=="organization")!=(target is null)) throw new RequestFault(400,"Некорректная область политики.");
        await db.Query("SELECT id FROM organizations FOR UPDATE");
        await db.Query("UPDATE policy_assignments SET valid_until=now() WHERE scope=@scope AND coalesce(department_id,employee_id,organization_id)=@target AND valid_until IS NULL",("scope",scope),("target",target ?? user.OrganizationId));
        return (await db.One("INSERT INTO policy_assignments(organization_id,policy_version_id,scope,department_id,employee_id,valid_from) VALUES(@org,@version,@scope,@department::uuid,@employee::uuid,now()) RETURNING *",
            ("org",user.OrganizationId),("version",version),("scope",scope),("department",scope=="department" ? target : null),("employee",scope=="employee" ? target : null)))!;
    });
}
