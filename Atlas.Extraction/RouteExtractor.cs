using Atlas.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atlas.Extraction;

/// <summary>
/// Builds a static <see cref="AppModel"/> from a Uno.Extensions <c>RegisterRoutes</c>
/// method by parsing its <c>ViewMap</c>/<c>RouteMap</c> registrations with Roslyn.
///
/// Only the <b>declared structure</b> is recovered: nodes, composed route paths, and
/// parent→child (<see cref="EdgeKind.Declared"/>) edges with their <c>IsDefault</c> flag.
/// Runtime provenance (observed / unreachable), layout positions, XAML elements and
/// theme tokens are out of static scope and left at neutral defaults — the runtime feed
/// and downstream layout fill those in.
/// </summary>
public static class RouteExtractor
{
    private static readonly string[] ViewSuffixes = ["Page", "Sheet", "Flyout", "Dialog", "View"];
    private static readonly string[] ModelSuffixes = ["Model", "ViewModel"];

    /// <summary>Parses the C# file at <paramref name="path"/> (e.g. an app's App.xaml.cs).</summary>
    public static AppModel ExtractFromFile(string path, string appName, DateTimeOffset generatedAt) =>
        Extract(File.ReadAllText(path), appName, generatedAt);

    /// <summary>Parses a C# source string containing a <c>RegisterRoutes</c> registration.</summary>
    public static AppModel Extract(string source, string appName, DateTimeOffset generatedAt)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var views = ViewMapIndex.Build(root);

        var nodes = new List<AppNode>();
        var edges = new List<AppEdge>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        // Resolves a RouteMap's DependsOn route name back to a node id; populated as we walk.
        // Keyed by both composed route path and bare segment so a DependsOn can name either.
        var idByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var pendingDeps = new List<(string DependsOn, string DependentId)>();

        foreach (var routeMap in TopLevelRouteMaps(root))
        {
            Walk(routeMap, parentPath: null, parentId: null, views, nodes, edges, ids, idByKey, pendingDeps);
        }

        // A DependsOn declares that the dependency is navigated before the dependent route;
        // mirror the fixture's direction with a Declared edge from dependency → dependent.
        foreach (var (dependsOn, dependentId) in pendingDeps)
        {
            if (idByKey.TryGetValue(dependsOn, out var dependencyId))
            {
                edges.Add(new AppEdge(dependencyId, dependentId, EdgeKind.Declared, Trigger: "", DependsOn: true));
            }
        }

        return new AppModel(
            App: appName,
            GeneratedAt: generatedAt,
            Source: ModelSource.Static,
            SchemaVersion: "1.0",
            Nodes: nodes,
            Edges: edges);
    }

    // ---- RouteMap walk -------------------------------------------------------

    private static void Walk(
        BaseObjectCreationExpressionSyntax routeMap,
        string? parentPath,
        string? parentId,
        ViewMapIndex views,
        List<AppNode> nodes,
        List<AppEdge> edges,
        HashSet<string> ids,
        Dictionary<string, string> idByKey,
        List<(string DependsOn, string DependentId)> pendingDeps)
    {
        var (segment, viewExpr, nestedExpr, isDefault, dependsOn) = ReadRouteMapArgs(routeMap);
        var (view, viewModel) = views.Resolve(viewExpr);

        var nested = NestedCreations(nestedExpr).ToList();
        var path = ComposePath(parentPath, segment);
        var name = FriendlyName(segment, viewModel, view);
        var id = UniqueId(name, ids);

        // Register both keys a later DependsOn might use to find this node. First writer wins
        // so a duplicate segment deeper in the tree can't hijack an earlier sibling's name.
        if (segment.Length > 0) idByKey.TryAdd(segment, id);
        if (path.Length > 0) idByKey.TryAdd(path, id);
        if (dependsOn.Length > 0) pendingDeps.Add((dependsOn, id));

        nodes.Add(new AppNode(
            Id: id,
            Name: name,
            Kind: nested.Count > 0 ? NodeKind.Shell : NodeKind.Page,
            Route: path,
            View: view,
            ViewModel: viewModel,
            Status: NodeStatus.Normal,
            Files: [],
            Elements: [],
            Tokens: [],
            Position: null));

        if (parentId is not null)
        {
            edges.Add(new AppEdge(parentId, id, EdgeKind.Declared, Trigger: "", IsDefault: isDefault));
        }

        foreach (var child in nested)
        {
            Walk(child, path, id, views, nodes, edges, ids, idByKey, pendingDeps);
        }
    }

    private static (string Segment, ExpressionSyntax? View, ExpressionSyntax? Nested, bool IsDefault, string DependsOn)
        ReadRouteMapArgs(BaseObjectCreationExpressionSyntax routeMap)
    {
        var segment = "";
        ExpressionSyntax? view = null;
        ExpressionSyntax? nested = null;
        var isDefault = false;
        var dependsOn = "";
        var positional = 0;

        foreach (var arg in routeMap.ArgumentList?.Arguments ?? default)
        {
            if (arg.NameColon is { } named)
            {
                switch (named.Name.Identifier.ValueText)
                {
                    case "View": view = arg.Expression; break;
                    case "Nested": nested = arg.Expression; break;
                    case "IsDefault": isDefault = arg.Expression.IsKind(SyntaxKind.TrueLiteralExpression); break;
                    case "DependsOn" when arg.Expression is LiteralExpressionSyntax dep:
                        dependsOn = dep.Token.ValueText; break;
                }
            }
            else
            {
                // First positional argument is the route segment (the RouteMap "path").
                if (positional == 0 && arg.Expression is LiteralExpressionSyntax literal)
                {
                    segment = literal.Token.ValueText;
                }
                positional++;
            }
        }

        return (segment, view, nested, isDefault, dependsOn);
    }

    // A child route's full path is parent + "/" + segment; an empty parent or segment collapses cleanly.
    private static string ComposePath(string? parentPath, string segment) =>
        string.IsNullOrEmpty(parentPath) ? segment
        : segment.Length == 0 ? parentPath
        : $"{parentPath}/{segment}";

    private static IEnumerable<BaseObjectCreationExpressionSyntax> NestedCreations(ExpressionSyntax? nested)
    {
        IEnumerable<ExpressionSyntax> elements = nested switch
        {
            CollectionExpressionSyntax collection =>
                collection.Elements.OfType<ExpressionElementSyntax>().Select(e => e.Expression),
            ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
            ArrayCreationExpressionSyntax array =>
                array.Initializer?.Expressions ?? Enumerable.Empty<ExpressionSyntax>(),
            InitializerExpressionSyntax initializer => initializer.Expressions,
            _ => Enumerable.Empty<ExpressionSyntax>(),
        };

        return elements.OfType<BaseObjectCreationExpressionSyntax>();
    }

    // ---- top-level RouteMaps -------------------------------------------------

    // The RouteMaps passed directly to routes.Register(...); their Nested children are walked recursively.
    private static IEnumerable<ObjectCreationExpressionSyntax> TopLevelRouteMaps(SyntaxNode root)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax member ||
                member.Name.Identifier.ValueText != "Register")
            {
                continue;
            }

            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                if (arg.Expression is ObjectCreationExpressionSyntax creation &&
                    SimpleName(creation.Type) == "RouteMap")
                {
                    yield return creation;
                }
            }
        }
    }

    // ---- naming --------------------------------------------------------------

    private static string FriendlyName(string segment, string viewModel, string view)
    {
        if (segment.Length > 0) return segment;
        if (viewModel.Length > 0) return StripSuffix(viewModel, ModelSuffixes);
        if (view.Length > 0) return StripSuffix(view, ViewSuffixes);
        return "Root";
    }

    private static string UniqueId(string name, HashSet<string> ids)
    {
        var baseId = name.Length == 0 ? "root" : char.ToLowerInvariant(name[0]) + name[1..];
        var id = baseId;
        var n = 2;
        while (!ids.Add(id))
        {
            id = $"{baseId}{n++}";
        }
        return id;
    }

    private static string StripSuffix(string value, string[] suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (value.Length > suffix.Length && value.EndsWith(suffix, StringComparison.Ordinal))
            {
                return value[..^suffix.Length];
            }
        }
        return value;
    }

    internal static string ConventionViewModel(string view)
    {
        foreach (var suffix in ViewSuffixes)
        {
            if (view.EndsWith(suffix, StringComparison.Ordinal))
            {
                return view[..^suffix.Length] + "Model";
            }
        }
        return view + "Model";
    }

    internal static string SimpleName(TypeSyntax type) => type switch
    {
        GenericNameSyntax generic => generic.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        QualifiedNameSyntax qualified => SimpleName(qualified.Right),
        _ => type.ToString(),
    };

    // ---- ViewMap registry ----------------------------------------------------

    /// <summary>
    /// Indexes every <c>ViewMap</c>/<c>DataViewMap</c> registration so a RouteMap's
    /// <c>FindByView&lt;T&gt;</c>/<c>FindByViewModel&lt;T&gt;</c> reference can be resolved to a
    /// view+view-model pair, falling back to the <c>XxxPage → XxxModel</c> naming convention.
    /// </summary>
    private sealed class ViewMapIndex
    {
        private readonly Dictionary<string, string> _vmByView = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _viewByVm = new(StringComparer.Ordinal);

        public static ViewMapIndex Build(SyntaxNode root)
        {
            var index = new ViewMapIndex();

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (SimpleName(creation.Type) is not ("ViewMap" or "DataViewMap"))
                {
                    continue;
                }

                string? view = null, viewModel = null;

                if (creation.Type is GenericNameSyntax generic)
                {
                    var args = generic.TypeArgumentList.Arguments;
                    if (args.Count >= 1) view = args[0].ToString();
                    if (args.Count >= 2) viewModel = args[1].ToString();
                }

                foreach (var arg in creation.ArgumentList?.Arguments ?? default)
                {
                    if (arg.Expression is TypeOfExpressionSyntax typeOf)
                    {
                        var target = arg.NameColon?.Name.Identifier.ValueText;
                        if (target == "ViewModel") viewModel = typeOf.Type.ToString();
                        else if (target == "View") view = typeOf.Type.ToString();
                    }
                }

                if (view is not null && viewModel is not null)
                {
                    index._vmByView[view] = viewModel;
                    index._viewByVm[viewModel] = view;
                }
                else if (view is not null)
                {
                    index._vmByView.TryAdd(view, ConventionViewModel(view));
                }
            }

            return index;
        }

        public (string View, string ViewModel) Resolve(ExpressionSyntax? viewExpr)
        {
            var (findKind, type) = ReadFind(viewExpr);

            string? view = null, viewModel = null;
            if (findKind == "FindByView") view = type;
            else if (findKind == "FindByViewModel") viewModel = type;

            if (view is not null && viewModel is null)
            {
                viewModel = _vmByView.TryGetValue(view, out var vm) ? vm : ConventionViewModel(view);
            }
            else if (viewModel is not null && view is null)
            {
                view = _viewByVm.TryGetValue(viewModel, out var v) ? v : "";
            }

            return (view ?? "", viewModel ?? "");
        }

        // Reads views.FindByView<T>() / views.FindByViewModel<T>() → ("FindByView", "T").
        private static (string? Kind, string? Type) ReadFind(ExpressionSyntax? expr)
        {
            if (expr is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax member &&
                member.Name is GenericNameSyntax generic &&
                generic.TypeArgumentList.Arguments.Count == 1)
            {
                return (generic.Identifier.ValueText, generic.TypeArgumentList.Arguments[0].ToString());
            }

            return (null, null);
        }
    }
}
