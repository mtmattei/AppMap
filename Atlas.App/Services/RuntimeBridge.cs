using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Atlas.Core;
using Atlas.Runtime;

namespace Atlas.App.Services;

/// <summary>
/// Hosts the Atlas listener and folds observed routes into the model stream.
/// The static model is the starting point; every agent route event produces a
/// new merged snapshot pushed to all subscribers.
/// </summary>
public sealed class RuntimeBridge(IAppModelSource modelSource, ILayoutStore layoutStore, StartupOptions startup, IRecentModels recent) : IRuntimeBridge, IDisposable
{
    private readonly object _gate = new();
    private readonly List<Channel<AppModel>> _modelSubscribers = [];
    private readonly List<Channel<bool>> _connectionSubscribers = [];
    private RuntimeListener? _listener;
    private AppModel? _current;
    private string? _previousNodeId;
    private bool _connected;

    public async IAsyncEnumerable<AppModel> Models([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<AppModel>();
        var current = await EnsureStartedAsync(ct);
        lock (_gate)
        {
            _modelSubscribers.Add(channel);
            current = _current ?? current;
        }

        try
        {
            yield return current;
            await foreach (var model in channel.Reader.ReadAllAsync(ct))
            {
                yield return model;
            }
        }
        finally
        {
            lock (_gate)
            {
                _modelSubscribers.Remove(channel);
            }
        }
    }

    public async IAsyncEnumerable<bool> Connection([EnumeratorCancellation] CancellationToken ct)
    {
        await EnsureStartedAsync(ct);
        var channel = Channel.CreateUnbounded<bool>();
        bool current;
        lock (_gate)
        {
            _connectionSubscribers.Add(channel);
            current = _connected;
        }

        try
        {
            yield return current;
            await foreach (var connected in channel.Reader.ReadAllAsync(ct))
            {
                yield return connected;
            }
        }
        finally
        {
            lock (_gate)
            {
                _connectionSubscribers.Remove(channel);
            }
        }
    }

    private async ValueTask<AppModel> EnsureStartedAsync(CancellationToken ct)
    {
        lock (_gate)
        {
            if (_current is not null)
            {
                return _current;
            }
        }

        var model = ApplyStoredLayout(await LoadInitialAsync(ct));
        lock (_gate)
        {
            if (_current is null)
            {
                _current = model;
                try
                {
                    _listener = new RuntimeListener();
                    _listener.MessageReceived += OnMessage;
                    _listener.ConnectionChanged += OnConnectionChanged;
                    _listener.Start();
                }
                catch (Exception)
                {
                    // No sockets on this platform (e.g. WebAssembly) — the viewer stays static.
                    _listener = null;
                }
            }

            return _current;
        }
    }

    // A launch-arg / file-association path wins over the embedded sample; a bad path falls back.
    private async ValueTask<AppModel> LoadInitialAsync(CancellationToken ct)
    {
        if (startup.ModelPath is { Length: > 0 } path && File.Exists(path))
        {
            try
            {
                var model = AppModelJson.Deserialize(await File.ReadAllTextAsync(path, ct));
                recent.Add(path);
                return model;
            }
            catch (Exception)
            {
                // Unreadable or invalid launch model — fall back to the embedded sample.
            }
        }

        return await modelSource.LoadAsync(ct);
    }

    /// <summary>Replaces the current model (e.g. a model file opened by the user).</summary>
    public void OpenModel(AppModel model)
    {
        lock (_gate)
        {
            _current = ApplyStoredLayout(model);
            _previousNodeId = null;
            Broadcast();
        }
    }

    private AppModel ApplyStoredLayout(AppModel model)
    {
        if (layoutStore.Load(model.App) is not { Count: > 0 } stored)
        {
            return model;
        }

        return model with
        {
            Nodes = model.Nodes
                .Select(n => stored.TryGetValue(n.Id, out var p) ? n with { Position = p } : n)
                .ToList(),
        };
    }

    // Pushes the current model to every live feed subscriber. Callers hold _gate.
    private void Broadcast()
    {
        if (_current is not { } current)
        {
            return;
        }

        foreach (var subscriber in _modelSubscribers)
        {
            subscriber.Writer.TryWrite(current);
        }
    }

    private void OnMessage(object? sender, AgentMessage message)
    {
        lock (_gate)
        {
            if (_current is not { } model)
            {
                return;
            }

            if (message.Route is null)
            {
                // New agent session: the next route only places the live node, no edge.
                _previousNodeId = null;
                return;
            }

            if (ModelMerger.ResolveNode(model, message.Route) is not { } node)
            {
                return;
            }

            _current = ModelMerger.ApplyRoute(model, _previousNodeId, node.Id, message.Timestamp);
            _previousNodeId = node.Id;
            Broadcast();
        }
    }

    public void MoveNode(string nodeId, double x, double y)
    {
        lock (_gate)
        {
            if (_current is not { } model)
            {
                return;
            }

            _current = model with
            {
                Nodes = model.Nodes
                    .Select(n => n.Id == nodeId ? n with { Position = new Atlas.Core.Point(x, y) } : n)
                    .ToList(),
            };
            layoutStore.Save(
                _current.App,
                _current.Nodes.Where(n => n.Position is not null).ToDictionary(n => n.Id, n => n.Position!));
            Broadcast();
        }
    }

    public void ApplyLayout(IReadOnlyDictionary<string, Atlas.Core.Point> positions)
    {
        lock (_gate)
        {
            if (_current is not { } model)
            {
                return;
            }

            _current = model with
            {
                Nodes = model.Nodes
                    .Select(n => positions.TryGetValue(n.Id, out var p) ? n with { Position = p } : n)
                    .ToList(),
            };
            layoutStore.Save(
                _current.App,
                _current.Nodes.Where(n => n.Position is not null).ToDictionary(n => n.Id, n => n.Position!));
            Broadcast();
        }
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        lock (_gate)
        {
            _connected = connected;
            foreach (var subscriber in _connectionSubscribers)
            {
                subscriber.Writer.TryWrite(connected);
            }
        }
    }

    public bool IsConnected
    {
        get { lock (_gate) { return _connected; } }
    }

    public void RequestNavigate(string route)
    {
        lock (_gate)
        {
            _listener?.Send(new AgentCommand(AgentCommand.Navigate, route));
        }
    }

    public void Dispose() => _listener?.Dispose();
}
