using Atlas.Core;

namespace Atlas.App.Presentation;

public partial record MapModel(IAppModelSource ModelSource)
{
    // Named Graph because the MVUX generator reserves 'Model' on the generated ViewModel.
    public IFeed<AppModel> Graph => Feed.Async(ModelSource.LoadAsync);

    // Empty state = no selection; MVUX models absence as None rather than null.
    public IState<AppNode> Selected => State<AppNode>.Empty(this);

    public IState<bool> ShowDeclared => State.Value(this, () => true);
    public IState<bool> ShowObserved => State.Value(this, () => true);
    public IState<bool> ShowUnreachable => State.Value(this, () => true);

    public async ValueTask SelectNode(AppNode node, CancellationToken ct) =>
        await Selected.UpdateAsync(_ => node, ct);
}
