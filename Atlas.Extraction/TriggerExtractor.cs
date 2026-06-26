using System.Xml.Linq;
using Atlas.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Atlas.Extraction;

/// <summary>
/// Augments a route-extracted <see cref="AppModel"/> with the app's <b>lateral navigation flow</b>:
/// the page→page edges a user actually traverses, labelled with a best-effort trigger. Two sources:
///
/// <list type="bullet">
///   <item>XAML <c>uen:Navigation.Request="Target"</c> attached properties — trigger is the source
///     element's name/content.</item>
///   <item>Imperative <c>INavigator.NavigateRouteAsync("Target")</c> /
///     <c>NavigateViewModelAsync&lt;TModel&gt;()</c> / <c>NavigateViewAsync&lt;TView&gt;()</c> call sites —
///     trigger is the enclosing method name (the fixture's <c>StartRounding</c> style).</item>
/// </list>
///
/// <para><c>RegisterRoutes</c> declares the route <i>tree</i> (a flat shell→child fan-out); the real
/// flow lives on buttons and view-model methods. Back/parent/root/dialog qualifiers (<c>-</c>,
/// <c>../</c>, <c>/</c>, <c>!</c>, <c>./</c>) and data-bound targets are control directives or
/// statically unresolvable, so they are skipped — no false edge is emitted.</para>
///
/// Edges land as <see cref="EdgeKind.Declared"/> with the inferred <see cref="AppEdge.Trigger"/>.
/// An existing edge for the same hop gains the trigger rather than being duplicated. Runtime
/// provenance still wins later: the feed flips these to Observed as they fire.
/// </summary>
public static class TriggerExtractor
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] BackMethods =
        ["NavigateBackAsync", "NavigatePreviousAsync", "NavigateBackWithResultAsync"];

    // ---- public surface ------------------------------------------------------

    /// <summary>Merges XAML <c>Navigation.Request</c> triggers from the given XAML documents.</summary>
    public static AppModel AddXamlTriggers(AppModel model, IEnumerable<string> xamlSources) =>
        AddTriggers(model, xamlSources, []);

    /// <summary>Merges imperative <c>Navigate*Async</c> triggers from the given C# sources.</summary>
    public static AppModel AddCodeTriggers(AppModel model, IEnumerable<string> csSources) =>
        AddTriggers(model, [], csSources);

    /// <summary>Merges both XAML and code triggers in one pass over the supplied source contents.</summary>
    public static AppModel AddTriggers(AppModel model, IEnumerable<string> xamlSources, IEnumerable<string> csSources)
    {
        var merge = new Merge(model);
        var codes = csSources as IReadOnlyList<string> ?? csSources.ToList();

        // Index DataViewMap data types first so a NavigateDataAsync in any file can resolve them.
        foreach (var cs in codes) IndexDataViewMaps(merge, cs);
        foreach (var xaml in xamlSources) ApplyXaml(merge, xaml);
        foreach (var cs in codes) ApplyCode(merge, cs);
        return merge.ToModel();
    }

    /// <summary>File-reading convenience: dispatches each path to the XAML or code pass by extension.</summary>
    public static AppModel AddTriggersFromFiles(AppModel model, IEnumerable<string> paths)
    {
        var xaml = new List<string>();
        var cs = new List<string>();
        foreach (var path in paths)
        {
            (path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ? xaml : cs).Add(path);
        }
        return AddTriggers(model, xaml.Select(File.ReadAllText), cs.Select(File.ReadAllText));
    }

    // ---- XAML pass -----------------------------------------------------------

    private static void ApplyXaml(Merge merge, string xaml)
    {
        if (!TryParse(xaml, out var doc) || OwnerView(doc) is not { } view || merge.ByView(view) is not { } owner)
        {
            return;
        }

        foreach (var element in doc.Descendants())
        {
            var request = element.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "Navigation.Request")?.Value;

            // A request may lead with navigation qualifiers (-/Home, ./Detail, !Sheet); strip them
            // and resolve the route that follows. A pure qualifier (-, ../) or a data binding
            // ({Binding…}) leaves nothing routable, so it is skipped — no false edge.
            if (request is null || ForwardRoute(request) is not { } route || merge.ByRoute(route) is not { } target)
            {
                continue;
            }

            merge.Upsert(owner.Id, target.Id, XamlTriggerLabel(element));
        }
    }

    // The owning page is the XAML root's x:Class (last segment).
    private static string? OwnerView(XDocument doc)
    {
        var xClass = doc.Root?.Attribute(Xaml + "Class")?.Value;
        return xClass is null ? null : xClass[(xClass.LastIndexOf('.') + 1)..];
    }

    // Best-effort label for what fires the navigation: x:Name, else x:Uid, else the element's Content.
    private static string XamlTriggerLabel(XElement element) =>
        element.Attribute(Xaml + "Name")?.Value
        ?? element.Attribute(Xaml + "Uid")?.Value
        ?? element.Attribute("Content")?.Value
        ?? "";

    // ---- code pass -----------------------------------------------------------

    private static void ApplyCode(Merge merge, string csSource)
    {
        var root = CSharpSyntaxTree.ParseText(csSource).GetRoot();

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax { Name: { } nameNode })
            {
                continue;
            }

            var method = nameNode.Identifier.ValueText;
            if (!method.StartsWith("Navigate", StringComparison.Ordinal) ||
                !method.EndsWith("Async", StringComparison.Ordinal) ||
                Array.IndexOf(BackMethods, method) >= 0)
            {
                continue;
            }

            if (ResolveTarget(merge, nameNode, invocation) is not { } target ||
                OwnerNode(merge, invocation) is not { } owner)
            {
                continue;
            }

            merge.Upsert(owner.Id, target.Id, EnclosingMemberName(invocation));
        }
    }

    // Resolves the navigation target: a generic type arg (NavigateViewModelAsync<T>()), else a string
    // route literal (NavigateRouteAsync("x")), else a created data value (NavigateDataAsync(this, new T())).
    private static AppNode? ResolveTarget(Merge merge, SimpleNameSyntax nameNode, InvocationExpressionSyntax invocation)
    {
        if (nameNode is GenericNameSyntax { TypeArgumentList.Arguments: [var typeArg, ..] })
        {
            return merge.ByType(RouteExtractor.SimpleName(typeArg));
        }

        var args = invocation.ArgumentList.Arguments.Select(a => a.Expression).ToList();

        if (args.OfType<LiteralExpressionSyntax>().FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            is { } route)
        {
            return merge.ByRoute(route.Token.ValueText);
        }

        // NavigateDataAsync(this, new Item()) → resolve Item via the DataViewMap index.
        return args.OfType<ObjectCreationExpressionSyntax>().FirstOrDefault() is { } data
            ? merge.ByData(RouteExtractor.SimpleName(data.Type))
            : null;
    }

    // Records every DataViewMap<TView, TViewModel, TData> so its data type maps to the screen it opens.
    private static void IndexDataViewMaps(Merge merge, string csSource)
    {
        var root = CSharpSyntaxTree.ParseText(csSource).GetRoot();

        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (creation.Type is GenericNameSyntax { Identifier.ValueText: "DataViewMap", TypeArgumentList.Arguments: [var view, var viewModel, var data, ..] })
            {
                var node = merge.ByType(RouteExtractor.SimpleName(viewModel)) ?? merge.ByType(RouteExtractor.SimpleName(view));
                if (node is not null)
                {
                    merge.RegisterData(RouteExtractor.SimpleName(data), node);
                }
            }
        }
    }

    // The caller's owning node is its enclosing view-model (or code-behind view) type.
    private static AppNode? OwnerNode(Merge merge, SyntaxNode node) =>
        node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault() is { } type
            ? merge.ByType(type.Identifier.ValueText)
            : null;

    // Trigger = the method (or property) the navigation call lives in.
    private static string EnclosingMemberName(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax m: return m.Identifier.ValueText;
                case PropertyDeclarationSyntax p: return p.Identifier.ValueText;
                case ConstructorDeclarationSyntax c: return c.Identifier.ValueText;
            }
        }
        return "";
    }

    // ---- shared helpers ------------------------------------------------------

    private static bool TryParse(string xaml, out XDocument doc)
    {
        try
        {
            doc = XDocument.Parse(xaml);
            return true;
        }
        catch (System.Xml.XmlException)
        {
            doc = null!;
            return false;
        }
    }

    // Strips leading Uno navigation qualifiers (-, ../, /, !, ./) and returns the route that follows,
    // or null when nothing routable remains — a pure back/parent qualifier, or a {Binding} target.
    private static string? ForwardRoute(string request)
    {
        var i = 0;
        while (i < request.Length && request[i] is '-' or '/' or '.' or '!')
        {
            i++;
        }

        var route = request[i..];
        return route.Length > 0 && (char.IsLetter(route[0]) || route[0] == '_') ? route : null;
    }

    // Mutable merge context: node lookups + an edge set that labels hops in place instead of duplicating.
    private sealed class Merge
    {
        private readonly AppModel _model;
        private readonly List<AppEdge> _edges;
        private readonly Dictionary<(string From, string To), int> _byHop = new();
        private readonly Dictionary<string, AppNode> _byView = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AppNode> _byViewModel = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AppNode> _byRoute = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AppNode> _byData = new(StringComparer.Ordinal);

        public Merge(AppModel model)
        {
            _model = model;
            _edges = model.Edges.ToList();

            for (var i = 0; i < _edges.Count; i++)
            {
                _byHop.TryAdd((_edges[i].From, _edges[i].To), i);
            }

            foreach (var node in model.Nodes)
            {
                if (node.View.Length > 0) _byView.TryAdd(node.View, node);
                if (node.ViewModel.Length > 0) _byViewModel.TryAdd(node.ViewModel, node);
                RegisterRouteKeys(node);
            }
        }

        // A node is targetable by its full route path, its last path segment, or its name.
        private void RegisterRouteKeys(AppNode node)
        {
            if (node.Route.Length > 0)
            {
                _byRoute.TryAdd(node.Route, node);
                _byRoute.TryAdd(node.Route[(node.Route.LastIndexOf('/') + 1)..], node);
            }
            if (node.Name.Length > 0) _byRoute.TryAdd(node.Name, node);
        }

        public AppNode? ByView(string view) => _byView.GetValueOrDefault(view);
        public AppNode? ByRoute(string route) => _byRoute.GetValueOrDefault(route);
        public AppNode? ByData(string dataType) => _byData.GetValueOrDefault(dataType);

        // A generic type arg may name either the view-model or the view; prefer the view-model.
        public AppNode? ByType(string type) =>
            _byViewModel.GetValueOrDefault(type) ?? _byView.GetValueOrDefault(type);

        // Links a DataViewMap's data type to the node it opens, so NavigateDataAsync(this, new T())
        // can resolve T to that screen.
        public void RegisterData(string dataType, AppNode node) => _byData.TryAdd(dataType, node);

        public void Upsert(string from, string to, string trigger)
        {
            if (_byHop.TryGetValue((from, to), out var existing))
            {
                // Label an existing hop only if it has no trigger yet; never overwrite a richer label.
                if (_edges[existing].Trigger.Length == 0 && trigger.Length > 0)
                {
                    _edges[existing] = _edges[existing] with { Trigger = trigger };
                }
                return;
            }

            _byHop[(from, to)] = _edges.Count;
            _edges.Add(new AppEdge(from, to, EdgeKind.Declared, trigger));
        }

        public AppModel ToModel() => _model with { Edges = _edges };
    }
}
