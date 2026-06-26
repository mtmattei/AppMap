using Atlas.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Atlas.App.Presentation.Controls;

/// <summary>
/// Draws every navigation edge in a single Skia pass (one element, not one <c>Path</c> per edge) so
/// the canvas scales to large graphs. Stroke brush AND dash pattern encode provenance — observed =
/// solid, declared = 5/5 dash, unreachable = 2/5 dash — so the distinction survives without color.
/// Sizes itself to the content extent (nodes overflow the host Canvas), and redraws on any change
/// or while a node is being dragged.
/// </summary>
public partial class EdgeCanvas : SKCanvasElement
{
    private const double NodeWidth = 182;
    private const double NodeHeight = 78;
    private const double StrokeWidth = 1.6;
    private const double ArrowSize = 6.4;

    // Dash patterns are constant per kind; one shared effect each (never disposed — app lifetime).
    private static readonly SKPathEffect DeclaredDash = SKPathEffect.CreateDash([5f, 5f], 0);
    private static readonly SKPathEffect UnreachableDash = SKPathEffect.CreateDash([2f, 5f], 0);

    private readonly Dictionary<string, Atlas.Core.Point> _preview = new();

    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(AppModel), typeof(EdgeCanvas), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty ObservedBrushProperty = DependencyProperty.Register(
        nameof(ObservedBrush), typeof(Brush), typeof(EdgeCanvas), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty DeclaredBrushProperty = DependencyProperty.Register(
        nameof(DeclaredBrush), typeof(Brush), typeof(EdgeCanvas), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty UnreachableBrushProperty = DependencyProperty.Register(
        nameof(UnreachableBrush), typeof(Brush), typeof(EdgeCanvas), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty ShowObservedProperty = DependencyProperty.Register(
        nameof(ShowObserved), typeof(bool), typeof(EdgeCanvas), new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty ShowDeclaredProperty = DependencyProperty.Register(
        nameof(ShowDeclared), typeof(bool), typeof(EdgeCanvas), new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty ShowUnreachableProperty = DependencyProperty.Register(
        nameof(ShowUnreachable), typeof(bool), typeof(EdgeCanvas), new PropertyMetadata(true, OnVisualChanged));

    public static readonly DependencyProperty HighlightedNodeIdsProperty = DependencyProperty.Register(
        nameof(HighlightedNodeIds), typeof(object), typeof(EdgeCanvas), new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty HighlightedEdgeKeysProperty = DependencyProperty.Register(
        nameof(HighlightedEdgeKeys), typeof(object), typeof(EdgeCanvas), new PropertyMetadata(null, OnVisualChanged));

    public AppModel? Source
    {
        get => (AppModel?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Brush? ObservedBrush { get => (Brush?)GetValue(ObservedBrushProperty); set => SetValue(ObservedBrushProperty, value); }
    public Brush? DeclaredBrush { get => (Brush?)GetValue(DeclaredBrushProperty); set => SetValue(DeclaredBrushProperty, value); }
    public Brush? UnreachableBrush { get => (Brush?)GetValue(UnreachableBrushProperty); set => SetValue(UnreachableBrushProperty, value); }
    public bool ShowObserved { get => (bool)GetValue(ShowObservedProperty); set => SetValue(ShowObservedProperty, value); }
    public bool ShowDeclared { get => (bool)GetValue(ShowDeclaredProperty); set => SetValue(ShowDeclaredProperty, value); }
    public bool ShowUnreachable { get => (bool)GetValue(ShowUnreachableProperty); set => SetValue(ShowUnreachableProperty, value); }

    /// <summary>Node ids of the active query result; any highlight dims unrelated edges.</summary>
    public object? HighlightedNodeIds { get => GetValue(HighlightedNodeIdsProperty); set => SetValue(HighlightedNodeIdsProperty, value); }

    /// <summary>Edge keys ("from&gt;to") of the active query result.</summary>
    public object? HighlightedEdgeKeys { get => GetValue(HighlightedEdgeKeysProperty); set => SetValue(HighlightedEdgeKeysProperty, value); }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (EdgeCanvas)d;
        canvas._preview.Clear();      // a new snapshot carries the committed positions
        canvas.InvalidateMeasure();   // the content extent may have changed
        canvas.Invalidate();
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((EdgeCanvas)d).Invalidate();

    /// <summary>Routes edges against a provisional node position while it is being dragged.</summary>
    public void PreviewNodePosition(string nodeId, Atlas.Core.Point position)
    {
        _preview[nodeId] = position;
        Invalidate();
    }

    // The element must cover every edge: nodes overflow the host Canvas, and back edges dip below them.
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Source is not { } model)
        {
            return new Size(0, 0);
        }

        double maxX = 0, maxY = 0;
        foreach (var node in model.Nodes)
        {
            if (node.Position is { } p)
            {
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }
        }

        return new Size(maxX + NodeWidth + 48, maxY + NodeHeight + 96);
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (Source is not { } model)
        {
            return;
        }

        var nodes = model.Nodes.Where(n => n.Position is not null).ToDictionary(n => n.Id);
        var edgeKeys = HighlightedEdgeKeys as IReadOnlyList<string>;
        var nodeIds = HighlightedNodeIds as IReadOnlyList<string>;
        var highlightActive = (edgeKeys?.Count ?? 0) > 0 || (nodeIds?.Count ?? 0) > 0;

        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var fill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

        foreach (var edge in model.Edges)
        {
            if (!IsKindVisible(edge.Kind)
                || !nodes.TryGetValue(edge.From, out var from)
                || !nodes.TryGetValue(edge.To, out var to))
            {
                continue;
            }

            var isHighlighted = edgeKeys?.Contains(GraphQueries.EdgeKey(edge)) == true;
            DrawEdge(canvas, stroke, fill, edge, PositionOf(from), PositionOf(to), isHighlighted, highlightActive);
        }

        stroke.PathEffect = null; // don't let the shared dash effects ride the disposed paint
    }

    private Atlas.Core.Point PositionOf(AppNode node) =>
        _preview.TryGetValue(node.Id, out var preview) ? preview : node.Position!;

    private bool IsKindVisible(EdgeKind kind) => kind switch
    {
        EdgeKind.Observed => ShowObserved,
        EdgeKind.Declared => ShowDeclared,
        EdgeKind.Unreachable => ShowUnreachable,
        _ => true,
    };

    private void DrawEdge(
        SKCanvas canvas, SKPaint stroke, SKPaint fill, AppEdge edge,
        Atlas.Core.Point from, Atlas.Core.Point to, bool isHighlighted, bool highlightActive)
    {
        // Forward edges run right-center → left-center; back edges dip below both nodes.
        SKPoint start, end, control1, control2;
        if (to.X < from.X)
        {
            start = new((float)(from.X + NodeWidth / 2), (float)(from.Y + NodeHeight));
            end = new((float)(to.X + NodeWidth / 2), (float)(to.Y + NodeHeight));
            var dip = Math.Max(start.Y, end.Y) + 70f;
            control1 = new(start.X, dip);
            control2 = new(end.X, dip);
        }
        else
        {
            start = new((float)(from.X + NodeWidth), (float)(from.Y + NodeHeight / 2));
            end = new((float)to.X, (float)(to.Y + NodeHeight / 2));
            var dx = (float)Math.Max(46, Math.Abs(end.X - start.X) * 0.5);
            control1 = new(start.X + dx, start.Y);
            control2 = new(end.X - dx, end.Y);
        }

        var (brush, dash, opacity) = StyleFor(edge.Kind);
        if (isHighlighted)
        {
            opacity = 1.0;
        }
        else if (highlightActive)
        {
            opacity = 0.08;
        }

        var color = ToColor(brush, opacity);

        using var path = new SKPath();
        path.MoveTo(start);
        path.CubicTo(control1, control2, end);

        stroke.Color = color;
        stroke.StrokeWidth = isHighlighted ? 2.4f : (float)StrokeWidth;
        stroke.PathEffect = dash;
        canvas.DrawPath(path, stroke);

        // Filled arrowhead at the tip, aimed along the final tangent (control2 → end).
        var angle = Math.Atan2(end.Y - control2.Y, end.X - control2.X);
        SKPoint Wing(double offset)
        {
            var a = angle + offset;
            return new((float)(end.X - ArrowSize * Math.Cos(a)), (float)(end.Y - ArrowSize * Math.Sin(a)));
        }

        using var head = new SKPath();
        head.MoveTo(end);
        head.LineTo(Wing(0.45));
        head.LineTo(Wing(-0.45));
        head.Close();
        fill.Color = color;
        canvas.DrawPath(head, fill);
    }

    private (Brush? Brush, SKPathEffect? Dash, double Opacity) StyleFor(EdgeKind kind) => kind switch
    {
        EdgeKind.Declared => (DeclaredBrush, DeclaredDash, 0.65),
        EdgeKind.Unreachable => (UnreachableBrush, UnreachableDash, 0.7),
        _ => (ObservedBrush, null, 1.0),
    };

    private static SKColor ToColor(Brush? brush, double opacity) =>
        brush is SolidColorBrush solid
            ? new SKColor(solid.Color.R, solid.Color.G, solid.Color.B, (byte)Math.Clamp(opacity * 255, 0, 255))
            : SKColors.Transparent;
}
