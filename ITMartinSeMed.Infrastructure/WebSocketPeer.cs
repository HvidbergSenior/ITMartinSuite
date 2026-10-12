using System.Net.WebSockets;
using System.Text;
using ITMartinSeMed.Application;

namespace ITMartinSeMed.Infrastructure;

// One browser's WebSocket. Sends are serialised (WebSocket allows one send at a time).
public sealed class WebSocketPeer(WebSocket socket) : IPeer
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public bool IsOpen => socket.State == WebSocketState.Open;

    public async Task SendAsync(string json)
    {
        if (!IsOpen) return;
        await _sendLock.WaitAsync();
        try { await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None); }
        catch (WebSocketException) { }
        finally { _sendLock.Release(); }
    }

    public async Task CloseAsync()
    {
        try { await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None); } catch (WebSocketException) { }
    }

    /// <summary>Reads messages until the line closes. Pings are skipped; every other message goes to <paramref name="onMessage"/>.</summary>
    /// <returns>True when the browser said it is done ({"t":"slut"}), false when the line just closed or broke.</returns>
    public async Task<bool> ReadAsync(Func<string, Task> onMessage)
    {
        var buf = new byte[64 * 1024];
        try
        {
            while (IsOpen)
            {
                var count = 0;
                WebSocketReceiveResult r;
                do
                {
                    if (count == buf.Length) return false;   // a message over 64 KB is not a handshake message
                    r = await socket.ReceiveAsync(new ArraySegment<byte>(buf, count, buf.Length - count), CancellationToken.None);
                    count += r.Count;
                } while (!r.EndOfMessage);
                if (r.MessageType == WebSocketMessageType.Close) return false;
                var text = Encoding.UTF8.GetString(buf, 0, count);
                if (Rooms.IsPing(text)) continue;
                if (Rooms.IsEnd(text)) return true;
                await onMessage(text);
            }
        }
        catch (WebSocketException) { }
        return false;
    }
}
