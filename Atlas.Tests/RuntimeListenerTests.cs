using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Atlas.Core;
using Atlas.Runtime;

namespace Atlas.Tests;

public class RuntimeListenerTests
{
    [Fact]
    public async Task Listener_receives_ndjson_messages()
    {
        const int port = 19743; // off the default to avoid colliding with a running viewer
        using var listener = new RuntimeListener(port);
        var received = new List<AgentMessage>();
        var connected = new TaskCompletionSource<bool>();
        var got = new TaskCompletionSource<bool>();

        listener.ConnectionChanged += (_, isConnected) => { if (isConnected) connected.TrySetResult(true); };
        listener.MessageReceived += (_, m) =>
        {
            lock (received) { received.Add(m); }
            if (m.Route is not null) got.TrySetResult(true);
        };
        listener.Start();

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port);

        var hello = new AgentMessage("RoundsApp.Mobile", null, DateTimeOffset.UtcNow);
        var route = new AgentMessage("RoundsApp.Mobile", "patients/7", DateTimeOffset.UtcNow);
        var payload = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(hello, AppModelJson.Compact) + "\n"
            + "{not json}\n" // malformed lines must not kill the channel
            + JsonSerializer.Serialize(route, AppModelJson.Compact) + "\n");
        var stream = client.GetStream();
        await stream.WriteAsync(payload, 0, payload.Length);
        await stream.FlushAsync();

        await Task.WhenAny(got.Task, Task.Delay(5000));

        Assert.True(connected.Task.IsCompleted, "connection event not raised");
        int count;
        lock (received) { count = received.Count; }
        Assert.True(got.Task.IsCompleted, $"route message not received; received={count}; listener error: {listener.LastError}");
        lock (received)
        {
            Assert.Equal(2, received.Count);
            Assert.Null(received[0].Route);
            Assert.Equal("patients/7", received[1].Route);
        }
    }
}
