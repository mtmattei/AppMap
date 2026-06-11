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
public sealed class RuntimeBridge(IAppModelSource modelSource) : IRuntimeBridge, IDisposable
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

        var model = await modelSource.LoadAsync(ct);
        lock (_gate)
        {
            if (_current is null)
            {
                _current = model;
                _listener = new RuntimeListener();
                _listener.MessageReceived += OnMessage;
                _listener.ConnectionChanged += OnConnectionChanged;
                _listener.Start();
            }

            return _current;
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
            foreach (var subscriber in _modelSubscribers)
            {
                subscriber.Writer.TryWrite(_current);
            }
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
            foreach (var subscriber in _modelSubscribers)
            {
                subscriber.Writer.TryWrite(_current);
            }
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

    public void Dispose() => _listener?.Dispose();
}
