using Atlas.Core;

namespace Atlas.App.Presentation;

public partial record MapModel(IRuntimeBridge Bridge, IModelFilePicker Picker, ILogger<MapModel> Logger)
{
    // Named Graph because the MVUX generator reserves 'Model' on the generated ViewModel.
    // Starts as the static model; every observed route from a connected agent re-emits.
    public IFeed<AppModel> Graph => Feed.AsyncEnumerable(Bridge.Models);

    public IFeed<bool> RuntimeConnected => Feed.AsyncEnumerable(Bridge.Connection);

    public IFeed<string> RuntimeLabel =>
        RuntimeConnected.Select(connected => connected ? "RUNTIME · CONNECTED" : "STATIC · SAMPLE MODEL");

    // Empty state = no selection; MVUX models absence as None rather than null.
    public IState<AppNode> Selected => State<AppNode>.Empty(this);

    public IState<bool> ShowDeclared => State.Value(this, () => true);
    public IState<bool> ShowObserved => State.Value(this, () => true);
    public IState<bool> ShowUnreachable => State.Value(this, () => true);

    // Suggestion chips are a projection of the loaded model: recomputed on every Graph snapshot,
    // so they track Open-model and live route edges flipping declared→observed for free.
    public IFeed<IReadOnlyList<Suggestion>> Suggestions => Graph.Select(SuggestionEngine.For);

    // Intro prose names the app in front of the panel instead of a hardcoded "RoundsApp".
    public IFeed<string> AgentIntro => Graph.Select(m =>
        $"The agent reads the same graph you see. Ask about the structure of {m.App}, or point it at a screen to scope an edit.");

    public IState<QueryResult> AgentResult => State<QueryResult>.Empty(this);

    // 0 = Agent, 1 = Inspector. Selecting a node brings the inspector forward.
    public IState<int> PanelTab => State.Value(this, () => 0);

    public async ValueTask SelectNode(AppNode node, CancellationToken ct)
    {
        await Selected.UpdateAsync(_ => node, ct);
        await PanelTab.SetAsync(1, ct);
    }

    public ValueTask MoveNode(NodeMove move, CancellationToken ct)
    {
        Bridge.MoveNode(move.NodeId, move.X, move.Y);
        return default;
    }

    public IState<string> ScopedContext => State<string>.Empty(this);

    // Transient status line (SPEC: toast on Jump/Scope, same verb as the button).
    public IState<string> Notice => State<string>.Empty(this);

    private CancellationTokenSource? _noticeCts;

    private async ValueTask ShowNotice(string message, CancellationToken ct)
    {
        _noticeCts?.Cancel();
        var cts = _noticeCts = new CancellationTokenSource();
        await Notice.SetAsync(message, ct);
        try
        {
            await Task.Delay(2500, cts.Token);
            await Notice.SetAsync(null!, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A newer notice took over.
        }
    }

    public async ValueTask JumpTo(CancellationToken ct)
    {
        if (await Selected is { } node)
        {
            Bridge.RequestNavigate(node.Route);
            await ShowNotice($"Navigated → {node.Name}", ct);
        }
    }

    public async ValueTask ScopeEdit(CancellationToken ct)
    {
        var node = await Selected;
        var model = await Graph;
        if (node is not null && model is not null)
        {
            await ScopedContext.SetAsync(EditScope.For(model, node.Id).ToPromptContext(), ct);
            await ShowNotice("Scoped edit context ready", ct);
        }
    }

    public async ValueTask ClearScope(CancellationToken ct) =>
        await ScopedContext.SetAsync(null!, ct);

    public async ValueTask OpenModel(CancellationToken ct)
    {
        var json = await Picker.PickModelJsonAsync(ct);
        if (json is null)
        {
            return;
        }

        try
        {
            var model = AppModelJson.Deserialize(json);
            Bridge.OpenModel(model);
            await ShowNotice($"Model loaded → {model.App}", ct);
        }
        catch (System.Text.Json.JsonException ex)
        {
            Logger.LogWarning(ex, "The picked file is not a valid app model.");
            await ShowNotice("Not a valid app model file", ct);
        }
    }

    // Single dispatch for every chip. The bound Suggestion arrives as the CommandParameter via
    // utu:CommandExtensions.Command (the validated SelectNode pattern); PathsTo/TraceLive carry
    // the target node id in Arg, so no node id is ever hardcoded here.
    public async ValueTask RunSuggestion(Suggestion suggestion, CancellationToken ct) =>
        await RunQuery(m => suggestion.Query switch
        {
            QueryId.Orphans => GraphQueries.FindOrphans(m),
            QueryId.Duplicates => GraphQueries.FindDuplicates(m),
            QueryId.Unreachable => GraphQueries.FindUnreachable(m),
            QueryId.PathsTo => GraphQueries.FindPathsTo(m, suggestion.Arg!),
            QueryId.TraceLive => GraphQueries.FindPathsTo(m, suggestion.Arg!),
            _ => GraphQueries.FindOrphans(m),
        }, ct);

    public async ValueTask ClearHighlights(CancellationToken ct) =>
        await AgentResult.UpdateAsync(_ => null, ct);

    private async ValueTask RunQuery(Func<AppModel, QueryResult> query, CancellationToken ct)
    {
        var model = await Graph;
        if (model is not null)
        {
            await AgentResult.UpdateAsync(_ => query(model), ct);
        }
    }
}
