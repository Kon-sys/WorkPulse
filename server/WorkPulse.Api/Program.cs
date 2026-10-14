using System.Text.Json.Serialization;
using Npgsql;
using WorkPulse.Api;
using WorkPulse.Infrastructure;
using WorkPulse.Application;

ReportExport.ConfigureFont();
var builder=WebApplication.CreateBuilder(args);
var connection=builder.Configuration.GetConnectionString("WorkPulse") ?? throw new InvalidOperationException("Set ConnectionStrings__WorkPulse using server/api.ps1.");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connection));
builder.Services.AddSingleton<TenantDatabase>();
builder.Services.AddSingleton<IdentityStore>();
builder.Services.AddSingleton<WorkspaceStore>();
builder.Services.AddSingleton<IEventInbox>(s=>s.GetRequiredService<WorkspaceStore>());
builder.Services.AddSingleton<IAnalysisResults>(s=>s.GetRequiredService<WorkspaceStore>());
builder.Services.AddSingleton<IResourceClassifier,PolicyResourceClassifier>();
builder.Services.AddSingleton<ActivityReconstructor>();
builder.Services.AddSingleton<ExceedsLimitSpecification>();
builder.Services.AddSingleton<ViolationDetector>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.AddWorkspaceSecurity();
var app=builder.Build();
await using(var guardConnection=await app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync())
await using(var guardCommand=new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname=current_user",guardConnection))
{
    if(await guardCommand.ExecuteScalarAsync() is true) throw new InvalidOperationException("API must use a limited database role, never the database owner or a BYPASSRLS role.");
}
if(args.Contains("--seed-users")) {await DemoBootstrap.Run(app);return;}
app.UseWorkspaceErrors();
app.UseStatusCodePages();app.UseRateLimiter();app.UseAuthentication();app.UseAuthorization();
app.MapGet("/health/live",()=>Results.Ok(new {status="alive"}));
app.MapWorkspaceEndpoints();
if(app.Environment.IsDevelopment()) app.MapOpenApi();
app.Run();
public partial class Program;
