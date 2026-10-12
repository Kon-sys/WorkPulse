using System.Text.Json;
using System.Text.Json.Serialization;
using WorkPulse.Application;
using WorkPulse.Domain;

namespace WorkPulse.Infrastructure;

public sealed partial class WorkspaceStore
{
    private static readonly JsonSerializerOptions SnapshotJson=new(JsonSerializerDefaults.Web) {Converters={new JsonStringEnumConverter()}};
    private static DateTimeOffset At(object value)=>new((DateTime)value,TimeSpan.Zero);
    private static string DbCategory(ActivityCategory category)=>category switch
    {
        ActivityCategory.NonTarget=>"non_target",ActivityCategory.AuthorizedAbsence=>"authorized_absence",_=>category.ToString().ToLowerInvariant()
    };
    private static string DbViolation(ViolationKind kind)=>kind switch {ViolationKind.NonTarget=>"non_target",ViolationKind.LunchOverrun=>"lunch_overrun",_=>"idle"};
    private static BreakKind BreakType(string kind)=>kind switch {"lunch"=>BreakKind.Lunch,"authorized_absence"=>BreakKind.AuthorizedAbsence,"rest"=>BreakKind.Rest,_=>throw new RequestFault(400,"Неизвестный перерыв.")};

    private static async Task<PolicySnapshot> Policy(TenantTransaction db,Guid organization,Guid id)
    {
        var p=await db.One("SELECT * FROM policy_versions WHERE id=@id AND published_at IS NOT NULL",("id",id)) ?? throw new RequestFault(409,"Нужна опубликованная политика.");
        var rules=await db.Query("SELECT resource_id,category FROM classification_rules WHERE policy_version_id=@id",("id",id));
        return new(organization,id,At(p["published_at"]!),TimeSpan.FromSeconds((int)p["idle_limit_seconds"]!),
            TimeSpan.FromSeconds((int)p["non_target_limit_seconds"]!),TimeSpan.FromSeconds((int)p["lunch_limit_seconds"]!),
            rules.ToDictionary(r=>(Guid)r["resource_id"]!,r=>(string)r["category"]! switch {"productive"=>ActivityCategory.Productive,"non_target"=>ActivityCategory.NonTarget,_=>ActivityCategory.Neutral}));
    }

    private static async Task<WorkSession> LoadSession(TenantTransaction db,Guid organization,Dictionary<string,object?> s,PolicySnapshot policy)
    {
        var session=WorkSession.Start(organization,(Guid)s["employee_id"]!,(Guid)s["department_id"]!,(Guid)s["id"]!,At(s["started_at"]!),policy);
        foreach(var b in await db.Query("SELECT * FROM session_breaks WHERE session_id=@id ORDER BY started_at",("id",session.Id)))
        {
            session.StartBreak((Guid)b["id"]!,BreakType((string)b["kind"]!),At(b["started_at"]!));
            if(b["ended_at"] is { } end) session.EndBreak(At(end));
        }
        if(s["ended_at"] is { } closed) session.End(At(closed),(string)s["end_reason"]! == "logout" ? EndReason.Logout : EndReason.Manual);
        return session;
    }

    public Task<List<Dictionary<string,object?>>> Sessions(UserAccess user,Guid employee)=>ForUser(user,async db=>
    {
        await Employee(db,user,employee);
        return await db.Query("SELECT s.*,coalesce((SELECT jsonb_agg(to_jsonb(b) ORDER BY b.started_at) FROM session_breaks b WHERE b.organization_id=s.organization_id AND b.session_id=s.id),'[]'::jsonb)::text AS breaks FROM work_sessions s WHERE employee_id=@id ORDER BY started_at DESC LIMIT 200",("id",employee));
    });

    private static void OwnSession(UserAccess user,Guid employee)
    {
        if(user.EmployeeId!=employee || !user.Grants.Any(g=>g.Role=="employee" && g.Scope=="self"))
            throw new RequestFault(403,"Управлять сессией может только сам сотрудник.");
    }

    public Task<object> StartSession(UserAccess user,Guid employee)=>ForUser<object>(user,async db=>
    {
        OwnSession(user,employee); await Employee(db,user,employee);
        return await StartAt(db,user.OrganizationId,employee,DateTimeOffset.UtcNow);
    });

    private static async Task<Dictionary<string,object?>> StartAt(TenantTransaction db,Guid organization,Guid employee,DateTimeOffset at)
    {
        var e=await db.One("SELECT * FROM employees WHERE id=@id AND terminated_on IS NULL FOR UPDATE",("id",employee)) ?? throw new RequestFault(409,"Сотрудник недоступен.");
        var existing=await db.One("SELECT * FROM work_sessions WHERE employee_id=@id AND ended_at IS NULL",("id",employee));
        if(existing is not null) return existing;
        var assignment=await db.One("SELECT a.policy_version_id FROM policy_assignments a JOIN policy_versions v ON v.organization_id=a.organization_id AND v.id=a.policy_version_id WHERE a.valid_from<=@at AND (a.valid_until IS NULL OR a.valid_until>@at) AND v.published_at<=@at AND (a.scope='organization' OR a.department_id=@department OR a.employee_id=@employee) ORDER BY CASE a.scope WHEN 'employee' THEN 0 WHEN 'department' THEN 1 ELSE 2 END LIMIT 1",("at",at),("department",e["department_id"]),("employee",employee))
            ?? throw new RequestFault(409,"Нет действующей опубликованной политики.");
        var policy=await Policy(db,organization,(Guid)assignment["policy_version_id"]!);
        var session=WorkSession.Start(organization,employee,(Guid)e["department_id"]!,Guid.NewGuid(),at,policy);
        return (await db.One("INSERT INTO work_sessions(organization_id,id,employee_id,department_id,policy_version_id,started_at) VALUES(@org,@id,@employee,@department,@policy,@at) RETURNING *",
            ("org",organization),("id",session.Id),("employee",employee),("department",session.DepartmentId),("policy",policy.Id),("at",session.StartedAt)))!;
    }

    public Task<object> ChangeSession(UserAccess user,Guid id,string operation,string? breakKind)=>ForUser<object>(user,async db=>
    {
        var s=await db.One("SELECT * FROM work_sessions WHERE id=@id FOR UPDATE",("id",id)) ?? throw new RequestFault(404,"Сессия не найдена.");
        var employee=(Guid)s["employee_id"]!;
        await Employee(db,user,employee);
        if(operation=="absence") {await Employee(db,user,employee,true);} else if(operation!="break-end") OwnSession(user,employee);
        var policy=await Policy(db,user.OrganizationId,(Guid)s["policy_version_id"]!);
        var session=await LoadSession(db,user.OrganizationId,s,policy);var now=DateTimeOffset.UtcNow;
        if(operation=="end")
        {
            session.End(now,EndReason.Manual);
            await db.Query("UPDATE session_breaks SET ended_at=@at WHERE session_id=@id AND ended_at IS NULL",("at",now),("id",id));
            await db.Query("UPDATE work_sessions SET ended_at=@at,end_reason='manual' WHERE id=@id",("at",now),("id",id));
        }
        else if(operation=="break-end")
        {
            var current=session.Breaks.SingleOrDefault(b=>b.EndedAt is null);
            if(current?.Kind==BreakKind.AuthorizedAbsence) await Employee(db,user,employee,true); else OwnSession(user,employee);
            session.EndBreak(now);
            await db.Query("UPDATE session_breaks SET ended_at=@at WHERE session_id=@id AND ended_at IS NULL",("at",now),("id",id));
        }
        else
        {
            var kind=operation=="absence" ? BreakKind.AuthorizedAbsence : BreakType(breakKind ?? "lunch");
            if(kind==BreakKind.AuthorizedAbsence && operation!="absence") throw new RequestFault(403,"Отсутствие разрешает HR или руководитель.");
            var breakId=Guid.NewGuid();session.StartBreak(breakId,kind,now);
            await db.Query("INSERT INTO session_breaks(organization_id,id,session_id,kind,source,started_at) VALUES(@org,@id,@session,@kind,'manual',@at)",
                ("org",user.OrganizationId),("id",breakId),("session",id),("kind",kind==BreakKind.AuthorizedAbsence ? "authorized_absence" : kind.ToString().ToLowerInvariant()),("at",now));
        }
        return new {id,operation,at=now};
    });

    public Task<object> Ingest(DeviceAccess device,IReadOnlyList<EventInput> inputs)=>database.Execute<object>(device.OrganizationId,null,async db=>
    {
        if(inputs.Count is <1 or >500 || inputs.Any(e=>e is null)) throw new RequestFault(400,"Пакет должен содержать от 1 до 500 событий.");
        _=await db.One("SELECT id FROM employees WHERE id=@id FOR UPDATE",("id",device.EmployeeId)) ?? throw new RequestFault(409,"Сотрудник не найден.");
        _=await db.One("SELECT id FROM devices WHERE id=@id AND retired_at IS NULL FOR SHARE",("id",device.DeviceId)) ?? throw new RequestFault(401,"Устройство отозвано.");
        var accepted=new List<Guid>();var duplicates=new List<Guid>();
        _=await db.One("SELECT id FROM device_credentials WHERE id=@id AND device_id=@device AND revoked_at IS NULL FOR SHARE",("id",device.CredentialId),("device",device.DeviceId)) ?? throw new RequestFault(401,"Токен устройства отозван.");
        foreach(var original in inputs.OrderBy(e=>e.OccurredAt).ThenBy(e=>e.ClientEventId))
        {
            // PostgreSQL timestamps have microsecond precision. Normalize before comparing repeat deliveries.
            var timestamp=original.OccurredAt.ToUniversalTime();
            var input=original with {OccurredAt=new DateTimeOffset(timestamp.Ticks-timestamp.Ticks%10,TimeSpan.Zero)};
            Guard.Id(input.ClientEventId,"client event");
            if(!Enum.IsDefined(input.Kind) || input.OccurredAt>DateTimeOffset.UtcNow.AddMinutes(5)) throw new RequestFault(400,"Некорректное событие или время в будущем.");
            var application=input.Application?.Trim().ToLowerInvariant();
            if(application is {Length:>128} || (application is not null && (application.Length==0 || application.Any(c=>!char.IsLetterOrDigit(c) && c is not ('.' or '-' or '_'))))) throw new RequestFault(400,"Нужен идентификатор процесса без пути и заголовка окна.");
            var previous=await db.One("SELECT e.*,r.identifier FROM activity_events e LEFT JOIN resources r ON r.organization_id=e.organization_id AND r.id=e.resource_id WHERE e.device_id=@device AND e.client_event_id=@client",("device",device.DeviceId),("client",input.ClientEventId));
            if(previous is not null)
            {
                if(At(previous["occurred_at"]!)!=input.OccurredAt || (string)previous["event_type"]! != input.Kind.ToString().ToLowerInvariant() || (string?)previous["identifier"]!=application)
                    throw new RequestFault(409,"Повторный идентификатор имеет другое содержимое.");
                duplicates.Add(input.ClientEventId);continue;
            }
            Guid? resource=null;
            if(application is not null)
            {
                var r=await db.One("INSERT INTO resources(organization_id,kind,identifier,display_name) VALUES(@org,'application',@app,@app) ON CONFLICT(organization_id,kind,identifier) DO UPDATE SET identifier=EXCLUDED.identifier RETURNING id",("org",device.OrganizationId),("app",application));
                resource=(Guid)r!["id"]!;
            }
            var session=await db.One("SELECT * FROM work_sessions WHERE employee_id=@employee AND started_at<=@at AND (ended_at IS NULL OR ended_at>@at) FOR UPDATE",("employee",device.EmployeeId),("at",input.OccurredAt));
            if(session is null && input.Kind is EventKind.Login or EventKind.Focus or EventKind.Input)
            {
                session=await StartAt(db,device.OrganizationId,device.EmployeeId,input.OccurredAt);
                if(At(session["started_at"]!)>input.OccurredAt) throw new RequestFault(409,"Позднее событие предшествует открытой сессии.");
            }
            await db.Query("INSERT INTO activity_events(organization_id,employee_id,device_id,session_id,client_event_id,event_type,occurred_at,resource_id) VALUES(@org,@employee,@device,@session::uuid,@client,@kind,@at,@resource::uuid)",
                ("org",device.OrganizationId),("employee",device.EmployeeId),("device",device.DeviceId),("session",session?["id"]),("client",input.ClientEventId),("kind",input.Kind.ToString().ToLowerInvariant()),("at",input.OccurredAt.ToUniversalTime()),("resource",resource));
            accepted.Add(input.ClientEventId);
        }
        return new {accepted,duplicates};
    });

    public Task<object> Analyze(UserAccess user,Guid sessionId,DateTimeOffset until,int signalLifetime)=>ForUser<object>(user,async db=>
    {
        until=until.ToUniversalTime();
        if(until>DateTimeOffset.UtcNow || signalLifetime is <1 or >86400) throw new RequestFault(400,"Некорректный период или срок сигнала.");
        var s=await db.One("SELECT * FROM work_sessions WHERE id=@id FOR UPDATE",("id",sessionId)) ?? throw new RequestFault(404,"Сессия не найдена.");
        await Employee(db,user,(Guid)s["employee_id"]!,true);
        if(until-At(s["started_at"]!)>TimeSpan.FromDays(7)) throw new RequestFault(400,"Период анализа ограничен 7 днями.");
        var policy=await Policy(db,user.OrganizationId,(Guid)s["policy_version_id"]!);
        var session=await LoadSession(db,user.OrganizationId,s,policy);
        var rows=await db.Query("SELECT * FROM activity_events WHERE employee_id=@employee AND occurred_at>=@start AND occurred_at<@end AND (session_id=@session OR session_id IS NULL) ORDER BY occurred_at LIMIT 100001",("employee",session.EmployeeId),("start",session.StartedAt),("end",session.EndedAt is { } end && end<until ? end : until),("session",session.Id));
        if(rows.Count>100000) throw new RequestFault(400,"Слишком много событий для одного анализа.");
        var events=rows.Select(e=>new ActivityEvent(user.OrganizationId,session.EmployeeId,(Guid)e["device_id"]!,(Guid)e["client_event_id"]!,At(e["occurred_at"]!),At(e["received_at"]!),Enum.Parse<EventKind>((string)e["event_type"]!,true),(Guid?)e["resource_id"])).ToArray();
        var registered=(await db.Query("SELECT id FROM devices WHERE employee_id=@employee AND registered_at<@end AND (retired_at IS NULL OR retired_at>@start)",("employee",session.EmployeeId),("end",until),("start",session.StartedAt))).Select(d=>(Guid)d["id"]!).ToArray();
        var devices=registered.Concat(events.Select(e=>e.DeviceId)).Distinct().ToArray();
        if(devices.Length==0) throw new RequestFault(409,"Нет устройств для анализа.");
        var intervals=reconstructor.Reconstruct(session,policy,until,devices,events,new AnalysisOptions(TimeSpan.FromSeconds(signalLifetime)));
        var run=new AnalysisRun(Guid.NewGuid(),user.OrganizationId,session.Id,policy.Id,AnalysisService.AlgorithmVersion,TimeSpan.FromSeconds(signalLifetime),intervals[0].Start,intervals[^1].End,intervals,detector.Detect(intervals,policy));
        await db.Query("INSERT INTO analytics_runs(organization_id,id,session_id,policy_version_id,signal_lifetime_seconds,period_start,period_end,algorithm_version,status,result_snapshot) VALUES(@org,@id,@session,@policy,@ttl,@start,@end,@version,'running',@snapshot::jsonb)",
            ("org",user.OrganizationId),("id",run.Id),("session",session.Id),("policy",policy.Id),("ttl",signalLifetime),("start",run.PeriodStart),("end",run.PeriodEnd),("version",run.AlgorithmVersion),("snapshot",JsonSerializer.Serialize(run,SnapshotJson)));
        var eventIds=rows.ToDictionary(e=>new EventReference((Guid)e["device_id"]!,(Guid)e["client_event_id"]!),e=>(Guid)e["id"]!);
        foreach(var interval in run.Intervals)
        {
            var id=Guid.NewGuid();
            await db.Query("INSERT INTO activity_intervals(organization_id,id,run_id,session_id,category,started_at,ended_at) VALUES(@org,@id,@run,@session,@category,@start,@end)",
                ("org",user.OrganizationId),("id",id),("run",run.Id),("session",session.Id),("category",DbCategory(interval.Category)),("start",interval.Start),("end",interval.End));
            foreach(var source in interval.Sources.Distinct()) await db.Query("INSERT INTO interval_sources(organization_id,interval_id,event_id) VALUES(@org,@interval,@event)",("org",user.OrganizationId),("interval",id),("event",eventIds[source]));
        }
        foreach(var violation in run.Violations)
        {
            var key=$"{session.Id}:{DbViolation(violation.Kind)}:{violation.Start:O}:{violation.End:O}";
            await db.Query("INSERT INTO violations(organization_id,employee_id,session_id,kind,started_at,ended_at,deduplication_key,latest_run_id) VALUES(@org,@employee,@session,@kind,@start,@end,@key,@run) ON CONFLICT(organization_id,deduplication_key) DO UPDATE SET latest_run_id=EXCLUDED.latest_run_id",
                ("org",user.OrganizationId),("employee",session.EmployeeId),("session",session.Id),("kind",DbViolation(violation.Kind)),("start",violation.Start),("end",violation.End),("key",key),("run",run.Id));
        }
        await db.Query("UPDATE analytics_runs SET status='completed',completed_at=clock_timestamp() WHERE id=@id",("id",run.Id));
        return run;
    });

    public Task<List<Dictionary<string,object?>>> Runs(UserAccess user,Guid employee)=>ForUser(user,async db=>
    {
        await Employee(db,user,employee);
        return await db.Query("SELECT r.id,r.session_id,r.period_start,r.period_end,r.created_at,r.algorithm_version,r.signal_lifetime_seconds FROM analytics_runs r JOIN work_sessions s ON s.organization_id=r.organization_id AND s.id=r.session_id WHERE s.employee_id=@id AND r.status='completed' ORDER BY r.created_at DESC LIMIT 200",("id",employee));
    });

    public Task<string> Run(UserAccess user,Guid id)=>ForUser(user,async db=>
    {
        var row=await db.One("SELECT r.result_snapshot::text AS snapshot,s.employee_id FROM analytics_runs r JOIN work_sessions s ON s.organization_id=r.organization_id AND s.id=r.session_id WHERE r.id=@id AND r.status='completed'",("id",id)) ?? throw new RequestFault(404,"Результат не найден.");
        await Employee(db,user,(Guid)row["employee_id"]!);return (string)row["snapshot"]!;
    });
}
