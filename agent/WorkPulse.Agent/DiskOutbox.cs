using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorkPulse.Agent;

public sealed record AgentEvent(Guid ClientEventId,DateTimeOffset OccurredAt,string Kind,string? Application);
public sealed record BatchAcknowledgement(Guid[] Accepted,Guid[] Duplicates);

public sealed class DiskOutbox
{
    private readonly string directory;
    private readonly int capacity;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public DiskOutbox(string directory,int capacity=10000)
    {
        this.directory=directory;this.capacity=capacity;
        Directory.CreateDirectory(directory);
        foreach(var pending in Directory.GetFiles(directory,"*.json.pending"))
        {
            // Complete atomic publication after a crash. A partial/corrupt file is preserved and reported.
            try { _=JsonSerializer.Deserialize<AgentEvent>(File.ReadAllText(pending),Json) ?? throw new JsonException("Empty pending event."); }
            catch(JsonException ex) {throw new InvalidOperationException("Incomplete outbox file preserved; inspect the local buffer before restarting.",ex);}
            File.Move(pending,pending[..^8]);
        }
    }
    public int Count => Directory.Exists(directory) ? Directory.EnumerateFiles(directory,"*.json").Count() : 0;
    public void Append(AgentEvent item)
    {
        Directory.CreateDirectory(directory);
        if(Count>=capacity) throw new InvalidOperationException("Offline buffer is full. Collection is paused; existing events are preserved.");
        var file=Path.Combine(directory,$"{item.OccurredAt.UtcTicks:D19}-{item.ClientEventId}.json");
        var temporary=file+".pending";
        using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {
            JsonSerializer.Serialize(stream,item,Json);stream.Flush(true);
        }
        File.Move(temporary,file);
    }
    public async Task<int> Flush(HttpClient client,string token,CancellationToken cancellation=default)
    {
        if(!Directory.Exists(directory)) return 0;
        var files=Directory.GetFiles(directory,"*.json").Order().Take(100).ToArray();
        if(files.Length==0) return 0;
        var items=files.Select(file=>JsonSerializer.Deserialize<AgentEvent>(File.ReadAllText(file),Json) ?? throw new InvalidOperationException("Invalid buffered event.")).ToArray();
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/v1/agent/events") {Content=JsonContent.Create(new {events=items})};
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await client.SendAsync(request,cancellation);
        if(!response.IsSuccessStatusCode) throw new HttpRequestException($"Server returned {(int)response.StatusCode}. Buffer preserved.",null,response.StatusCode);
        var acknowledgement=await response.Content.ReadFromJsonAsync<BatchAcknowledgement>(Json,cancellation) ?? throw new HttpRequestException("Missing acknowledgement.");
        var confirmed=acknowledgement.Accepted.Concat(acknowledgement.Duplicates).ToHashSet();var deleted=0;
        for(var i=0;i<files.Length;i++) if(confirmed.Contains(items[i].ClientEventId)) {File.Delete(files[i]);deleted++;}
        return deleted;
    }
}
