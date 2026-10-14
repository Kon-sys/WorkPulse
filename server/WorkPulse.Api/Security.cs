using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using WorkPulse.Application;
using WorkPulse.Domain;
using WorkPulse.Infrastructure;

namespace WorkPulse.Api;

public static class Security
{
    public static UserAccess User(HttpContext c)=>(UserAccess)c.Items["access"]!;
    public static void AddWorkspaceSecurity(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPasswordHasher<string>,PasswordHasher<string>>();
        builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="WorkPulse.Csrf";o.Cookie.SameSite=SameSiteMode.Strict;});
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>
        {
            o.Cookie.Name="WorkPulse.Session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;
            o.Cookie.SecurePolicy=builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            o.ExpireTimeSpan=TimeSpan.FromHours(8);o.SlidingExpiration=false;
            o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};
            o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};
            o.Events.OnValidatePrincipal=async c=>
            {
                if(!Guid.TryParse(c.Principal?.FindFirstValue("organization"),out var org) || !Guid.TryParse(c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier),out var id)) {c.RejectPrincipal();return;}
                var user=await c.HttpContext.RequestServices.GetRequiredService<IdentityStore>().GetUser(org,id);
                if(user is null || user.SecurityStamp.ToString()!=c.Principal?.FindFirstValue("stamp")) {c.RejectPrincipal();await c.HttpContext.SignOutAsync();return;}
                c.HttpContext.Items["access"]=user;
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(o=>
        {
            o.RejectionStatusCode=429;
            o.AddPolicy("login",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",_=>new FixedWindowRateLimiterOptions {PermitLimit=20,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));
        });
    }
    public static void UseWorkspaceErrors(this WebApplication app)=>app.Use(async (context,next)=>
    {
        try {await next();}
        catch(Exception ex)
        {
            var status=ex switch {RequestFault f=>f.Status,BadHttpRequestException bad=>bad.StatusCode,AntiforgeryValidationException=>400,BusinessRuleException=>409,PostgresException p when p.SqlState is "23505" or "23P01"=>409,PostgresException p when p.SqlState.StartsWith("23")=>400,_=>500};
            var message=ex switch {RequestFault or BusinessRuleException=>ex.Message,AntiforgeryValidationException=>"Обновите страницу: CSRF-токен отсутствует или устарел.",PostgresException when status<500=>"Запрос нарушает ограничения данных.",_=>"Ошибка сервера."};
            if(status==500) app.Logger.LogError(ex,"Database request failed");
            await Results.Problem(statusCode:status,title:message).ExecuteAsync(context);
        }
    });
}
