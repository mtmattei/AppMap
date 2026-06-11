using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Atlas.Core;

namespace Atlas.Runtime;

/// <summary>
/// Loopback TCP listener for Atlas agent connections. One NDJSON AgentMessage per line.
/// The agent connects out to the viewer, so target apps need no server of their own.
/// </summary>
public sealed class RuntimeListener : IDisposable
{
    public const int DefaultPort = AtlasChannel.DefaultPort;

    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public RuntimeListener(int port = DefaultPort)
    {
        _port = port;
    }

    public Exception? LastError { get; private set; }

    public event EventHandler<AgentMessage>? MessageReceived;

    public event EventHandler<bool>? ConnectionChanged;

    public void Start()
    {
        if (_listener is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _ = AcceptLoopAsync(_listener, _cts.Token);
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = ReadClientAsync(client, ct);
        }
    }

    private async Task ReadClientAsync(TcpClient client, CancellationToken ct)
    {
        ConnectionChanged?.Invoke(this, true);
        try
        {
            using (client)
            using (var reader = new StreamReader(client.GetStream()))
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                    {
                        return;
                    }

                    AgentMessage? message;
                    try
                    {
                        message = JsonSerializer.Deserialize<AgentMessage>(line, AppModelJson.Options);
                    }
                    catch (JsonException)
                    {
                        continue; // malformed line — skip, keep the channel alive
                    }

                    if (message is not null)
                    {
                        MessageReceived?.Invoke(this, message);
                    }
                }
            }
        }
        catch (IOException)
        {
            // client went away — normal when the target app closes
        }
        catch (Exception ex)
        {
            LastError = ex; // surfaced for diagnostics; the accept loop stays alive
        }
        finally
        {
            ConnectionChanged?.Invoke(this, false);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener = null;
    }
}
