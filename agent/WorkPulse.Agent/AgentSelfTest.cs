using System.Net;
using System.Net.Http.Json;

namespace WorkPulse.Agent;

public static class AgentSelfTest
{
    public static async Task Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"workpulse-agent-test-"+Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var id=Guid.NewGuid();var outbox=new DiskOutbox(root,2);
            outbox.Append(new(id,DateTimeOffset.UtcNow,"Focus","synthetic.exe"));
            using var failed=new HttpClient(new ResponseHandler(_=>new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))) {BaseAddress=new Uri("http://localhost/")};
            try {await outbox.Flush(failed,"synthetic-token");throw new Exception("Failure expected.");} catch(HttpRequestException) { }
            if(outbox.Count!=1) throw new Exception("Offline event lost.");
            var published=Directory.GetFiles(root,"*.json").Single();File.Move(published,published+".pending");
            var restarted=new DiskOutbox(root,2);
            using var acknowledged=new HttpClient(new ResponseHandler(_=>new HttpResponseMessage(HttpStatusCode.OK) {Content=JsonContent.Create(new {accepted=Array.Empty<Guid>(),duplicates=new[]{id}})})) {BaseAddress=new Uri("http://localhost/")};
            if(await restarted.Flush(acknowledged,"synthetic-token")!=1 || restarted.Count!=0) throw new Exception("Acknowledgement failed.");
            var first=new AgentEvent(Guid.NewGuid(),DateTimeOffset.UtcNow,"Idle",null);
            restarted.Append(first);restarted.Append(first with {ClientEventId=Guid.NewGuid()});
            try {restarted.Append(first with {ClientEventId=Guid.NewGuid()});throw new Exception("Capacity expected.");} catch(InvalidOperationException) { }
            if(restarted.Count!=2) throw new Exception("Capacity dropped existing events.");
            Console.WriteLine("PASS offline preservation, crash publication recovery, restart, duplicate acknowledgement, bounded buffer. No Windows collection performed.");
        }
        finally {foreach(var file in Directory.GetFiles(root)) File.Delete(file);Directory.Delete(root);}
    }
    private sealed class ResponseHandler(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
    }
}
