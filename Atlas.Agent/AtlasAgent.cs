using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Atlas.Core;
using Uno.Extensions.Navigation;

namespace Atlas.Agent;

/// <summary>
/// Publishes the host app's navigation to a running Atlas viewer.
/// Reference this package (Debug builds only) and call Start once the host is built:
/// <code>AtlasAgent.Start(Host.Services, "MyApp");</code>
/// The agent connects out to the viewer on localhost and retries quietly while
/// Atlas is not running — it never affects the host app.
/// </summary>
public static class AtlasAgent
{
    public static IDisposable Start(IServiceProvider services, string appName, int port = AtlasChannel.DefaultPort)
    {
        var notifier = services.GetService(typeof(IRouteNotifier)) as IRouteNotifier
            ?? throw new InvalidOperationException(
                "IRouteNotifier is not registered — the host app must use Uno.Extensions Navigation (.UseNavigation()).");

        var connection = new AgentConnection(appName, port, services);
        notifier.RouteChanged += connection.OnRouteChanged;
        connection.Attach(() => notifier.RouteChanged -= connection.OnRouteChanged);
        return connection;
    }

    private sealed class AgentConnection : IDisposable
    {
        private readonly string _appName;
        private readonly int _port;
        private readonly IServiceProvider _services;
        private readonly ConcurrentQueue<AgentMessage> _outbox = new();
        private readonly SemaphoreSlim _signal = new(0);
        private readonly CancellationTokenSource _cts = new();
        private INavigator? _lastNavigator;
        private Action? _detach;

        public AgentConnection(string appName, int port, IServiceProvider services)
        {
            _appName = appName;
            _port = port;
            _services = services;
            _ = SendLoopAsync(_cts.Token);
        }

        public void Attach(Action detach) => _detach = detach;

        public void OnRouteChanged(object? sender, RouteChangedEventArgs e)
        {
            if (e.Navigator is not null)
            {
                _lastNavigator = e.Navigator;
            }

            var route = e.Navigator?.Route?.ToString();
            if (string.IsNullOrWhiteSpace(route))
            {
                return;
            }

            _outbox.Enqueue(new AgentMessage(_appName, route, DateTimeOffset.Now));
            _signal.Release();
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", _port, ct).ConfigureAwait(false);
                    var stream = client.GetStream();
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    using var reader = new StreamReader(stream, new UTF8Encoding(false));

                    await SendAsync(writer, new AgentMessage(_appName, null, DateTimeOffset.Now)).ConfigureAwait(false);
                    _ = ReceiveLoopAsync(reader, ct);

                    while (!ct.IsCancellationRequested)
                    {
                        await _signal.WaitAsync(ct).ConfigureAwait(false);
                        while (_outbox.TryDequeue(out var message))
                        {
                            await SendAsync(writer, message).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    // Viewer not running or connection dropped — retry quietly.
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
        }

        private static Task SendAsync(StreamWriter writer, AgentMessage message) =>
            writer.WriteLineAsync(JsonSerializer.Serialize(message, AppModelJson.Compact));

        private async Task ReceiveLoopAsync(StreamReader reader, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                    {
                        return;
                    }

                    AgentCommand? command;
                    try
                    {
                        command = JsonSerializer.Deserialize<AgentCommand>(line, AppModelJson.Compact);
                    }
                    catch (JsonException)
                    {
                        continue;
                    }

                    if (command is { Kind: AgentCommand.Navigate } && _lastNavigator is { } navigator)
                    {
                        await navigator.NavigateRouteAsync(this, command.Route).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException)
            {
                // connection dropped — SendLoop's retry will re-establish
            }
        }

        public void Dispose()
        {
            _detach?.Invoke();
            _cts.Cancel();
        }
    }
}
