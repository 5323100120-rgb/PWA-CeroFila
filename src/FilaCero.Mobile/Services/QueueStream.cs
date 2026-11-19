using Microsoft.Maui.ApplicationModel;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace FilaCero.Mobile.Services;

/// <summary>Notificaciones WebSocket sólo mientras la aplicación está en primer plano.</summary>
public sealed class QueueStream(AppState state)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private CancellationTokenSource? _cancel;

    public void Start()
    {
        Stop();
        if(string.IsNullOrEmpty(state.ServerUrl) || string.IsNullOrEmpty(state.SessionToken)) return;
        var token=state.SessionToken;
        var url=state.ServerUrl;
        _cancel=new CancellationTokenSource();
        _=ListenLoopAsync(url,token,_cancel.Token);
    }
    public void Stop() { _cancel?.Cancel(); _cancel=null;state.SetConnection("Sin conexión"); }

    private async Task ListenLoopAsync(string baseUrl,string session,CancellationToken ct)
    {
        var baseUri=new Uri(baseUrl);
        var ws = new UriBuilder(baseUri) { Scheme=baseUri.Scheme=="https"?"wss":"ws",Path="/ws/queues",Query="" }.Uri;
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var client=new ClientWebSocket();
                client.Options.AddSubProtocol("filacero");
                client.Options.AddSubProtocol("fc-session."+session);
                await client.ConnectAsync(ws,ct);
                state.SetConnection("En vivo");
                var bytes=new byte[4096];
                while(client.State==WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    using var resultStream=new MemoryStream();
                    WebSocketReceiveResult frame;
                    do
                    {
                        frame=await client.ReceiveAsync(new ArraySegment<byte>(bytes),ct);
                        if(frame.MessageType==WebSocketMessageType.Close) break;
                        if(resultStream.Length+frame.Count>64_000) throw new WebSocketException("Evento demasiado largo.");
                        resultStream.Write(bytes,0,frame.Count);
                    } while(!frame.EndOfMessage);
                    if(frame.MessageType==WebSocketMessageType.Close) break;
                    var content=Encoding.UTF8.GetString(resultStream.ToArray());
                    using var document=JsonDocument.Parse(content);
                    if(document.RootElement.TryGetProperty("type",out var type) && type.GetString()=="queue.updated" &&
                       document.RootElement.TryGetProperty("turn",out var turnJson))
                    {
                        var update=turnJson.Deserialize<QueuePatch>(Json);
                        if(update is not null)
                            await MainThread.InvokeOnMainThreadAsync(() => state.ApplyQueueUpdateAsync(update));
                    }
                }
            }
            catch(Exception e) when(e is WebSocketException or HttpRequestException or TaskCanceledException or JsonException)
            { /* Una caída de red se maneja con reconexión sin eliminar el pase. */ }
            finally { state.SetConnection("Sin conexión"); }
            try { await Task.Delay(5000,ct); }
            catch(OperationCanceledException) { break; }
        }
    }
}
