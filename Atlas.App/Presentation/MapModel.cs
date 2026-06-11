using Atlas.Core;

namespace Atlas.App.Presentation;

public partial record MapModel(IRuntimeBridge Bridge)
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

    public IState<QueryResult> AgentResult => State<QueryResult>.Empty(this);

    // 0 = Agent, 1 = Inspector. Selecting a node brings the inspector forward.
    public IState<int> PanelTab => State.Value(this, () => 0);

    public async ValueTask SelectNode(AppNode node, CancellationToken ct)
    {
        await Selected.UpdateAsync(_ => node, ct);
        await PanelTab.SetAsync(1, ct);
    }

    public async ValueTask FindOrphans(CancellationToken ct) =>
        await RunQuery(GraphQueries.FindOrphans, ct);

    // v1 chip targets the fixture's high-risk flow, mirroring the prototype's query.
    public async ValueTask FindPaths(CancellationToken ct) =>
        await RunQuery(m => GraphQueries.FindPathsTo(m, "meds"), ct);

    public async ValueTask FindDuplicates(CancellationToken ct) =>
        await RunQuery(GraphQueries.FindDuplicates, ct);

    public async ValueTask FindUnreachable(CancellationToken ct) =>
        await RunQuery(GraphQueries.FindUnreachable, ct);

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
