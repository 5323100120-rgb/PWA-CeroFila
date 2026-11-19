using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FilaCero.Domain;

namespace FilaCero.Api.Realtime;

public sealed class QueueHub
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, WebSocket>> _connected = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task ListenAsync(Guid patientId, WebSocket socket, CancellationToken cancellation)
    {
        var subscriptionId = Guid.NewGuid();
        var group = _connected.GetOrAdd(patientId, _ => new());
        group[subscriptionId] = socket;
        try
        {
            await SendAsync(socket, new { type = "connected", message = "Seguimiento activo" }, cancellation);
            var buffer = new byte[2048];
            while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
            {
                var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                if (received.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (Exception e) when (e is OperationCanceledException or WebSocketException) { }
        finally
        {
            group.TryRemove(subscriptionId, out _);
            if (group.IsEmpty) _connected.TryRemove(patientId, out _);
            if (socket.State == WebSocketState.Open)
                try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Fin", CancellationToken.None); } catch { }
        }
    }

    public async Task BroadcastAsync(Guid patientId, TurnDto turn)
    {
        if (!_connected.TryGetValue(patientId, out var watchers)) return;
        var update = new
        {
            type = "queue.updated",
            turn = new {turn.Id,turn.Code,turn.Clinic,turn.Status,turn.Room,turn.QueueAhead,turn.UpdatedAt},
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        foreach (var socket in watchers.Values)
        {
            if (socket.State != WebSocketState.Open) continue;
            try { await SendAsync(socket, update, CancellationToken.None); }
            catch (WebSocketException) { /* El receptor podrá reconectar al volver a primer plano. */ }
        }
    }

    private static Task SendAsync(WebSocket socket, object message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, Json));
        return socket.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,ct);
    }
}
