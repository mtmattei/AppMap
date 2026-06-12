using Atlas.Core;
using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Atlas.App.Presentation.Controls;

/// <summary>
/// Draws the navigation edges of an AppModel as cubic Béziers with arrowheads.
/// Stroke brush AND dash pattern encode provenance, so the distinction survives
/// without color vision: observed = solid, declared = 5/5 dash, unreachable = 2/5 dash.
/// </summary>
public partial class EdgeLayer : Canvas
{
    private const double NodeWidth = 182;
    private const double NodeHeight = 78;
    private const double StrokeWidth = 1.6;
    private const double ArrowSize = 6.4;

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(AppModel), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public static readonly DependencyProperty ObservedBrushProperty = DependencyProperty.Register(
        nameof(ObservedBrush), typeof(Brush), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public static readonly DependencyProperty DeclaredBrushProperty = DependencyProperty.Register(
        nameof(DeclaredBrush), typeof(Brush), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public static readonly DependencyProperty UnreachableBrushProperty = DependencyProperty.Register(
        nameof(UnreachableBrush), typeof(Brush), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public static readonly DependencyProperty ShowObservedProperty = DependencyProperty.Register(
        nameof(ShowObserved), typeof(bool), typeof(EdgeLayer), new PropertyMetadata(true, OnLayerChanged));

    public static readonly DependencyProperty ShowDeclaredProperty = DependencyProperty.Register(
        nameof(ShowDeclared), typeof(bool), typeof(EdgeLayer), new PropertyMetadata(true, OnLayerChanged));

    public static readonly DependencyProperty ShowUnreachableProperty = DependencyProperty.Register(
        nameof(ShowUnreachable), typeof(bool), typeof(EdgeLayer), new PropertyMetadata(true, OnLayerChanged));

    public static readonly DependencyProperty HighlightedNodeIdsProperty = DependencyProperty.Register(
        nameof(HighlightedNodeIds), typeof(object), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public static readonly DependencyProperty HighlightedEdgeKeysProperty = DependencyProperty.Register(
        nameof(HighlightedEdgeKeys), typeof(object), typeof(EdgeLayer), new PropertyMetadata(null, OnLayerChanged));

    public AppModel? Source
    {
        get => (AppModel?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Brush? ObservedBrush
    {
        get => (Brush?)GetValue(ObservedBrushProperty);
        set => SetValue(ObservedBrushProperty, value);
    }

    public Brush? DeclaredBrush
    {
        get => (Brush?)GetValue(DeclaredBrushProperty);
        set => SetValue(DeclaredBrushProperty, value);
    }

    public Brush? UnreachableBrush
    {
        get => (Brush?)GetValue(UnreachableBrushProperty);
        set => SetValue(UnreachableBrushProperty, value);
    }

    public bool ShowObserved
    {
        get => (bool)GetValue(ShowObservedProperty);
        set => SetValue(ShowObservedProperty, value);
    }

    public bool ShowDeclared
    {
        get => (bool)GetValue(ShowDeclaredProperty);
        set => SetValue(ShowDeclaredProperty, value);
    }

    public bool ShowUnreachable
    {
        get => (bool)GetValue(ShowUnreachableProperty);
        set => SetValue(ShowUnreachableProperty, value);
    }

    /// <summary>Node ids of the active query result; any highlight dims unrelated edges.</summary>
    public object? HighlightedNodeIds
    {
        get => GetValue(HighlightedNodeIdsProperty);
        set => SetValue(HighlightedNodeIdsProperty, value);
    }

    /// <summary>Edge keys ("from&gt;to") of the active query result.</summary>
    public object? HighlightedEdgeKeys
    {
        get => GetValue(HighlightedEdgeKeysProperty);
        set => SetValue(HighlightedEdgeKeysProperty, value);
    }

    private readonly Dictionary<string, Atlas.Core.Point> _previewPositions = new();

    private static void OnLayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var layer = (EdgeLayer)d;
        if (e.Property == SourceProperty)
        {
            layer._previewPositions.Clear(); // a new snapshot carries the committed positions
        }

        layer.Rebuild();
    }

    /// <summary>Routes edges against a provisional node position while it is being dragged.</summary>
    public void PreviewNodePosition(string nodeId, Atlas.Core.Point position)
    {
        _previewPositions[nodeId] = position;
        Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();

        if (Source is not { } model)
        {
            return;
        }

        var nodesById = model.Nodes
            .Where(n => n.Position is not null)
            .ToDictionary(n => n.Id);

        var edgeKeys = HighlightedEdgeKeys as IReadOnlyList<string>;
        var nodeIds = HighlightedNodeIds as IReadOnlyList<string>;
        var highlightActive = (edgeKeys?.Count ?? 0) > 0 || (nodeIds?.Count ?? 0) > 0;

        foreach (var edge in model.Edges)
        {
            if (!IsKindVisible(edge.Kind)
                || !nodesById.TryGetValue(edge.From, out var from)
                || !nodesById.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            var isHighlighted = edgeKeys?.Contains(EdgeKey(edge)) == true;
            AddEdge(edge, PositionOf(from), PositionOf(to), isHighlighted, highlightActive);
        }
    }

    private Atlas.Core.Point PositionOf(AppNode node) =>
        _previewPositions.TryGetValue(node.Id, out var preview) ? preview : node.Position!;

    private static string EdgeKey(AppEdge edge) => GraphQueries.EdgeKey(edge);

    private bool IsKindVisible(EdgeKind kind) => kind switch
    {
        EdgeKind.Observed => ShowObserved,
        EdgeKind.Declared => ShowDeclared,
        EdgeKind.Unreachable => ShowUnreachable,
        _ => true,
    };

    private void AddEdge(AppEdge edge, Atlas.Core.Point from, Atlas.Core.Point to, bool isHighlighted, bool highlightActive)
    {
        // Forward edges run right-center -> left-center; back edges dip below both nodes.
        Windows.Foundation.Point start, end, control1, control2;
        var isBack = to.X < from.X;
        if (isBack)
        {
            start = new(from.X + NodeWidth / 2, from.Y + NodeHeight);
            end = new(to.X + NodeWidth / 2, to.Y + NodeHeight);
            var dip = Math.Max(start.Y, end.Y) + 70;
            control1 = new(start.X, dip);
            control2 = new(end.X, dip);
        }
        else
        {
            start = new(from.X + NodeWidth, from.Y + NodeHeight / 2);
            end = new(to.X, to.Y + NodeHeight / 2);
            var dx = Math.Max(46, Math.Abs(end.X - start.X) * 0.5);
            control1 = new(start.X + dx, start.Y);
            control2 = new(end.X - dx, end.Y);
        }

        var (brush, dashes, opacity) = StyleFor(edge.Kind);
        if (isHighlighted)
        {
            opacity = 1.0;
        }
        else if (highlightActive)
        {
            opacity = 0.08;
        }

        var figure = new PathFigure { StartPoint = start };
        figure.Segments.Add(new BezierSegment { Point1 = control1, Point2 = control2, Point3 = end });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var path = new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = isHighlighted ? 2.4 : StrokeWidth,
            Opacity = opacity,
            Tag = edge,
        };
        if (dashes is not null)
        {
            path.StrokeDashArray = dashes;
        }

        ToolTipService.SetToolTip(path, $"{edge.Trigger} · {edge.Kind}");
        Children.Add(path);
        Children.Add(BuildArrowHead(end, control2, brush, opacity, edge));
    }

    private (Brush? Brush, DoubleCollection? Dashes, double Opacity) StyleFor(EdgeKind kind) => kind switch
    {
        EdgeKind.Declared => (DeclaredBrush, new DoubleCollection { 5, 5 }, 0.65),
        EdgeKind.Unreachable => (UnreachableBrush, new DoubleCollection { 2, 5 }, 0.7),
        _ => (ObservedBrush, null, 1.0),
    };

    private static Path BuildArrowHead(Windows.Foundation.Point tip, Windows.Foundation.Point tangentOrigin, Brush? brush, double opacity, AppEdge edge)
    {
        var angle = Math.Atan2(tip.Y - tangentOrigin.Y, tip.X - tangentOrigin.X);
        Windows.Foundation.Point Wing(double offset)
        {
            var a = angle + offset;
            return new(tip.X - ArrowSize * Math.Cos(a), tip.Y - ArrowSize * Math.Sin(a));
        }

        var figure = new PathFigure { StartPoint = tip, IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment { Point = Wing(0.45) });
        figure.Segments.Add(new LineSegment { Point = Wing(-0.45) });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Path
        {
            Data = geometry,
            Fill = brush,
            Opacity = opacity,
            Tag = edge,
        };
    }
}
