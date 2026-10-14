using System.Net;
using System.Text.Json;
using WorkPulse.Agent;

if(args.Contains("--self-test")) {await AgentSelfTest.Run();return;}
if(!OperatingSystem.IsWindows() || !Environment.UserInteractive) throw new InvalidOperationException("Run manually in an interactive Windows session.");
var configFile=Environment.GetEnvironmentVariable("WORKPULSE_AGENT_CONFIG") ?? "appsettings.Local.json";
var config=JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(configFile),new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Missing agent configuration.");
var address=new Uri(config.ServerUrl);
if(address.Scheme!="https" && !(address.Scheme=="http" && address.IsLoopback)) throw new InvalidOperationException("HTTPS is required outside localhost.");
if(config.PollSeconds is <1 or >30 || config.IdleSeconds is <1 or >3600 || string.IsNullOrWhiteSpace(config.Token) || config.Token.Split('.').Length!=3) throw new InvalidOperationException("Invalid agent configuration.");
var bufferPath=Path.GetFullPath(config.BufferDirectory);
Directory.CreateDirectory(bufferPath);
// An enrollment owns one buffer and one collector. Re-enrollment must use a fresh buffer.
using var processLock=new FileStream(Path.Combine(bufferPath,"collector.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(config.Token.Split('.')[..2].Aggregate((a,b)=>a+"."+b))));
var identityFile=Path.Combine(bufferPath,"enrollment.txt");
if(File.Exists(identityFile) && File.ReadAllText(identityFile)!=fingerprint) throw new InvalidOperationException("Buffer belongs to another enrollment. Preserve it and select a new buffer directory.");
File.WriteAllText(identityFile,fingerprint);
using var client=new HttpClient {BaseAddress=new Uri(config.ServerUrl.TrimEnd('/')+"/"),Timeout=TimeSpan.FromSeconds(10)};
using var stop=new CancellationTokenSource();
var durationIndex=Array.IndexOf(args,"--seconds");
if(durationIndex>=0)
{
    if(durationIndex+1>=args.Length || !int.TryParse(args[durationIndex+1],out var seconds) || seconds is <1 or >3600) throw new InvalidOperationException("Duration must be 1..3600 seconds.");
    stop.CancelAfter(TimeSpan.FromSeconds(seconds));
}
Console.CancelKeyPress+=(_,e)=>{e.Cancel=true;stop.Cancel();};
var outbox=new DiskOutbox(bufferPath);
Console.WriteLine("WorkPulse agent: process name and input/idle state only. Ctrl+C stops collection. No background installation.");
var lastHeartbeat=DateTimeOffset.MinValue;var lastTransportHeartbeat=DateTimeOffset.MinValue;var nextDelivery=DateTimeOffset.MinValue;var retrySeconds=5;Observation? previous=null;
void Enqueue(string kind,string? application)
{
    var now=DateTimeOffset.UtcNow;now=new DateTimeOffset(now.Ticks-now.Ticks%10,TimeSpan.Zero);
    outbox.Append(new(Guid.NewGuid(),now,kind,application));
}
try {Enqueue("Login",null);} catch(InvalidOperationException ex) {Console.WriteLine(ex.Message);}
while(!stop.IsCancellationRequested)
{
    try
    {
        var current=WindowsObservation.Read(config.IdleSeconds);
        if(current.Idle)
        {
            if(previous?.Idle!=true || DateTimeOffset.UtcNow-lastHeartbeat>=TimeSpan.FromSeconds(30)) {Enqueue("Idle",null);lastHeartbeat=DateTimeOffset.UtcNow;}
        }
        else
        {
            if(previous is null || previous.Idle || current.Application!=previous.Application || DateTimeOffset.UtcNow-lastHeartbeat>=TimeSpan.FromSeconds(30)) {Enqueue("Focus",current.Application);lastHeartbeat=DateTimeOffset.UtcNow;}
            if(previous is not null && current.LastInputTick!=previous.LastInputTick) Enqueue("Input",current.Application);
        }
        previous=current;
        if(DateTimeOffset.UtcNow-lastTransportHeartbeat>=TimeSpan.FromSeconds(30)) {Enqueue("Heartbeat",null);lastTransportHeartbeat=DateTimeOffset.UtcNow;}
    }
    catch(Exception ex) when(ex is IOException or InvalidOperationException)
    {
        Console.WriteLine($"Collection paused: {ex.Message} Pending: {outbox.Count}.");
    }
    // Delivery still runs when a full buffer pauses collection; successful delivery frees capacity.
    if(DateTimeOffset.UtcNow>=nextDelivery)
    {
        try {await outbox.Flush(client,config.Token,stop.Token);retrySeconds=5;}
        catch(OperationCanceledException) when(stop.IsCancellationRequested) {break;}
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            Console.WriteLine($"Delivery paused: {ex.Message} Pending: {outbox.Count}.");
            nextDelivery=DateTimeOffset.UtcNow.AddSeconds(retrySeconds);retrySeconds=Math.Min(retrySeconds*2,60);
            if(ex is HttpRequestException {StatusCode:HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden}) stop.Cancel();
        }
    }
    try {await Task.Delay(TimeSpan.FromSeconds(config.PollSeconds),stop.Token);} catch(OperationCanceledException) {break;}
}
try {Enqueue("Logout",null);await outbox.Flush(client,config.Token);} catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException) {Console.WriteLine($"Shutdown delivery pending: {ex.Message}");}
Console.WriteLine($"Stopped. Buffered events: {outbox.Count}.");

public sealed record AgentConfig(string ServerUrl,string Token,string BufferDirectory,int PollSeconds=2,int IdleSeconds=30);
