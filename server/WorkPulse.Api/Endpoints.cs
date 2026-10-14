using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using WorkPulse.Application;
using WorkPulse.Infrastructure;
using static WorkPulse.Api.Security;

namespace WorkPulse.Api;

public static class Endpoints
{
    public static void MapWorkspaceEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/auth/csrf",(HttpContext c,IAntiforgery csrf)=>Results.Ok(new {token=csrf.GetAndStoreTokens(c).RequestToken}));
        app.MapPost("/api/v1/auth/login",Login).RequireRateLimiting("login");
        var api=app.MapGroup("/api/v1").RequireAuthorization();
        api.AddEndpointFilter(async (invocation,next)=>
        {
            var c=invocation.HttpContext;
            if(HttpMethods.IsPost(c.Request.Method) || HttpMethods.IsPut(c.Request.Method) || HttpMethods.IsDelete(c.Request.Method) || HttpMethods.IsPatch(c.Request.Method))
                await c.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(c);
            return await next(invocation);
        });
        api.MapGet("/auth/me",(HttpContext c)=>User(c));
        api.MapPost("/auth/logout",async (HttpContext c)=>{await c.SignOutAsync();return Results.NoContent();});
        api.MapGet("/employees",(HttpContext c,WorkspaceStore s)=>s.Employees(User(c)));
        api.MapPost("/employees",(EmployeeInput i,HttpContext c,WorkspaceStore s)=>s.CreateEmployee(User(c),i.DepartmentId,InputValidation.Text(i.EmployeeNumber),InputValidation.Text(i.FullName),i.PositionName,i.HiredOn));
        api.MapGet("/departments",(HttpContext c,WorkspaceStore s)=>s.Departments(User(c)));
        api.MapPost("/departments",(DepartmentInput i,HttpContext c,WorkspaceStore s)=>s.CreateDepartment(User(c),InputValidation.Text(i.Code),InputValidation.Text(i.Name)));
        api.MapGet("/users",(HttpContext c,WorkspaceStore s)=>s.Users(User(c)));
        api.MapPost("/users",(UserInput i,HttpContext c,WorkspaceStore s,IPasswordHasher<string> h)=>
        {
            InputValidation.Password(i.Password);var email=InputValidation.Text(i.Email).ToLowerInvariant();
            return s.CreateUser(User(c),i.EmployeeId,email,h.HashPassword(email,i.Password),i.Role,i.DepartmentId);
        });
        api.MapDelete("/users/{id:guid}",(Guid id,HttpContext c,WorkspaceStore s)=>s.DisableUser(User(c),id));
        api.MapGet("/devices",(HttpContext c,WorkspaceStore s)=>s.Devices(User(c)));
        api.MapPost("/devices",(DeviceInput i,HttpContext c,WorkspaceStore s)=>s.CreateDevice(User(c),i.EmployeeId,InputValidation.Text(i.Name),i.Platform));
        api.MapPost("/devices/{id:guid}/credentials",(Guid id,HttpContext c,WorkspaceStore s)=>s.RotateDevice(User(c),id));
        api.MapDelete("/devices/{id:guid}",(Guid id,HttpContext c,WorkspaceStore s)=>s.RetireDevice(User(c),id));
        api.MapGet("/policies",(HttpContext c,WorkspaceStore s)=>s.Policies(User(c)));
        api.MapPost("/policies",(PolicyInput i,HttpContext c,WorkspaceStore s)=>s.CreatePolicy(User(c),i with {Name=InputValidation.Text(i.Name)}));
        api.MapPost("/policies/{id:guid}/assignments",(Guid id,AssignmentInput i,HttpContext c,WorkspaceStore s)=>s.AssignPolicy(User(c),id,i.Scope,i.TargetId));
        api.MapPost("/resources",(ResourceInput i,HttpContext c,WorkspaceStore s)=>s.CreateResource(User(c),i.Kind,InputValidation.Text(i.Identifier,128)));
        api.MapGet("/resources",(HttpContext c,WorkspaceStore s)=>s.Resources(User(c)));
        api.MapGet("/employees/{id:guid}/sessions",(Guid id,HttpContext c,WorkspaceStore s)=>s.Sessions(User(c),id));
        api.MapPost("/employees/{id:guid}/sessions",(Guid id,HttpContext c,WorkspaceStore s)=>s.StartSession(User(c),id));
        api.MapPost("/sessions/{id:guid}/end",(Guid id,HttpContext c,WorkspaceStore s)=>s.ChangeSession(User(c),id,"end",null));
        api.MapPost("/sessions/{id:guid}/breaks",(Guid id,BreakInput i,HttpContext c,WorkspaceStore s)=>s.ChangeSession(User(c),id,"break",i.Kind));
        api.MapPost("/sessions/{id:guid}/breaks/end",(Guid id,HttpContext c,WorkspaceStore s)=>s.ChangeSession(User(c),id,"break-end",null));
        api.MapPost("/sessions/{id:guid}/absence",(Guid id,HttpContext c,WorkspaceStore s)=>s.ChangeSession(User(c),id,"absence",null));
        api.MapPost("/sessions/{id:guid}/analysis",(Guid id,AnalysisInput i,HttpContext c,WorkspaceStore s)=>s.Analyze(User(c),id,i.Until,i.SignalLifetimeSeconds));
        api.MapGet("/employees/{id:guid}/analysis-runs",(Guid id,HttpContext c,WorkspaceStore s)=>s.Runs(User(c),id));
        api.MapGet("/analysis-runs/{id:guid}",async (Guid id,HttpContext c,IAnalysisResults s)=>Results.Content(await s.Run(User(c),id),"application/json"));
        api.MapGet("/reports",(DateTimeOffset start,DateTimeOffset end,Guid? employeeId,Guid? departmentId,HttpContext c,WorkspaceStore s)=>s.Report(User(c),start,end,employeeId,departmentId));
        api.MapGet("/reports/export/{format}",async (string format,DateTimeOffset start,DateTimeOffset end,Guid? employeeId,Guid? departmentId,HttpContext c,WorkspaceStore s)=>
        {
            if(format is not ("pdf" or "xlsx"))throw new RequestFault(400,"Формат: pdf или xlsx.");
            var report=await s.Report(User(c),start,end,employeeId,departmentId);
            return Results.File(format=="pdf"?ReportExport.Pdf(report):ReportExport.Xlsx(report),format=="pdf"?"application/pdf":"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"WorkPulse-{start:yyyyMMdd}-{end:yyyyMMdd}.{format}");
        });
        api.MapGet("/violations",(HttpContext c,WorkspaceStore s)=>s.Violations(User(c)));
        api.MapGet("/violations/{id:guid}",(Guid id,HttpContext c,WorkspaceStore s)=>s.ViolationDetails(User(c),id));
        api.MapPatch("/violations/{id:guid}",(Guid id,ReviewInput i,HttpContext c,WorkspaceStore s)=>s.ReviewViolation(User(c),id,i.Status,i.Comment));
        app.MapPost("/api/v1/agent/events",async (EventBatch batch,HttpContext c,IdentityStore identity,IEventInbox store)=>
        {
            var header=c.Request.Headers.Authorization.ToString();
            var device=header.StartsWith("Bearer ",StringComparison.Ordinal) ? await identity.AuthenticateDevice(header[7..]) : null;
            if(device is null) return Results.Problem(statusCode:401,title:"Токен устройства недействителен.");
            if(batch.Events is null) throw new RequestFault(400,"Нужен массив событий.");
            return Results.Ok(await store.Ingest(device,batch.Events));
        });
    }
    private static async Task<IResult> Login(LoginInput input,HttpContext c,IdentityStore identity,IPasswordHasher<string> hasher,IAntiforgery csrf)
    {
        await csrf.ValidateRequestAsync(c);InputValidation.Password(input.Password);
        if(input.OrganizationId==Guid.Empty) return Results.Problem(statusCode:401,title:"Неверные учётные данные.");
        var email=InputValidation.Text(input.Email).ToLowerInvariant();var login=await identity.FindLogin(input.OrganizationId,email);
        var hash=login?.PasswordHash ?? hasher.HashPassword("dummy","not-a-real-password");
        var verified=hasher.VerifyHashedPassword(email,hash,input.Password)!=PasswordVerificationResult.Failed;
        if(login is null || login.PasswordHash is null || !verified || login.LockedUntil is { } locked && locked>DateTime.UtcNow)
        {
            if(login is not null) await identity.RecordLogin(input.OrganizationId,login.Access.UserId,false);
            return Results.Problem(statusCode:401,title:"Неверные учётные данные или вход временно заблокирован.");
        }
        await identity.RecordLogin(input.OrganizationId,login.Access.UserId,true);
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,login.Access.UserId.ToString()),new Claim("organization",input.OrganizationId.ToString()),new Claim("stamp",login.Access.SecurityStamp.ToString())};
        await c.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
        return Results.Ok(login.Access);
    }
}
