using System.Windows.Input;
using Atlas.Core;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Atlas.App.Presentation.Controls;

public sealed partial class NodeCard : UserControl
{
    private const double DragThreshold = 5;

    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(string), typeof(NodeCard), new PropertyMetadata(null, OnVisualStateInputChanged));

    public static readonly DependencyProperty HighlightedIdsProperty = DependencyProperty.Register(
        nameof(HighlightedIds), typeof(object), typeof(NodeCard), new PropertyMetadata(null, OnVisualStateInputChanged));

    public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register(
        nameof(MoveCommand), typeof(ICommand), typeof(NodeCard), new PropertyMetadata(null));

    private readonly TranslateTransform _dragTransform = new();
    private bool _pressed;
    private bool _dragging;
    private bool _hovered;
    private double _opacityTarget = 1.0;
    private Windows.Foundation.Point _dragStart;

    public NodeCard()
    {
        this.InitializeComponent();
        RenderTransform = _dragTransform;
        DataContextChanged += (_, _) =>
        {
            // A new model snapshot re-positions the card via layout; drop the drag offset.
            _dragTransform.X = 0;
            _dragTransform.Y = 0;
            UpdateVisualState();
        };
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => ResetDrag();
        PointerCanceled += (_, _) => ResetDrag();
        PointerEntered += (_, _) => { _hovered = true; UpdateVisualState(); };
        PointerExited += (_, _) => { _hovered = false; UpdateVisualState(); };
        ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Hand);
    }

    /// <summary>Id of the currently selected node; drives the selection ring.</summary>
    public string? SelectedId
    {
        get => (string?)GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>Node ids of the active query result; dims this card when it is not part of the answer.</summary>
    public object? HighlightedIds
    {
        get => GetValue(HighlightedIdsProperty);
        set => SetValue(HighlightedIdsProperty, value);
    }

    /// <summary>Executed with a NodeMove when the user drops the card at a new position.</summary>
    public ICommand? MoveCommand
    {
        get => (ICommand?)GetValue(MoveCommandProperty);
        set => SetValue(MoveCommandProperty, value);
    }

    /// <summary>Raised continuously while the card is dragged, with its provisional position.</summary>
    public event EventHandler<NodeMove>? DragDelta;

    private UIElement Reference => VisualTreeHelper.GetParent(this) as UIElement ?? this;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || DataContext is not AppNode { Position: not null })
        {
            return;
        }

        _pressed = true;
        _dragging = false;
        _dragStart = e.GetCurrentPoint(Reference).Position;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var position = e.GetCurrentPoint(Reference).Position;
        var dx = position.X - _dragStart.X;
        var dy = position.Y - _dragStart.Y;

        if (!_dragging && (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold))
        {
            _dragging = true;
            CapturePointer(e.Pointer);
        }

        if (_dragging)
        {
            _dragTransform.X = dx;
            _dragTransform.Y = dy;
            if (DataContext is AppNode { Position: { } origin } node)
            {
                DragDelta?.Invoke(this, new NodeMove(node.Id, Math.Max(0, origin.X + dx), Math.Max(0, origin.Y + dy)));
            }
            e.Handled = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && DataContext is AppNode { Position: { } origin } node)
        {
            var x = Math.Max(0, origin.X + _dragTransform.X);
            var y = Math.Max(0, origin.Y + _dragTransform.Y);
            MoveCommand?.Execute(new NodeMove(node.Id, x, y));
            ReleasePointerCaptures();
            e.Handled = true;
        }

        _pressed = false;
        _dragging = false;
    }

    private void ResetDrag()
    {
        _pressed = false;
        _dragging = false;
    }

    private static void OnVisualStateInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NodeCard)d).UpdateVisualState();

    private void UpdateVisualState()
    {
        var node = DataContext as AppNode;
        var isSelected = node is not null && node.Id == SelectedId;

        var highlightedIds = HighlightedIds as IReadOnlyList<string>;
        var highlightActive = highlightedIds is { Count: > 0 };
        var isHighlighted = highlightActive && node is not null && highlightedIds!.Contains(node.Id);

        AnimateOpacityTo(highlightActive && !isHighlighted && !isSelected ? 0.25 : 1.0);

        // Hard amber ring for selection/highlight; soft amber outline marks the live node
        // at rest; hover steps the neutral border up one tone.
        var key = isSelected || isHighlighted
            ? "AtlasLiveBrush"
            : node?.Status == NodeStatus.Live ? "AtlasLiveDimBrush"
            : _hovered ? "AtlasTextMuted2Brush" : "AtlasBorder2Brush";
        if (Application.Current.Resources.TryGetValue(key, out var brush))
        {
            Card.BorderBrush = (Brush)brush;
        }
    }

    // Dimming a third of the canvas at once reads as a scene change; ease it instead of snapping.
    private void AnimateOpacityTo(double target)
    {
        if (Math.Abs(target - _opacityTarget) < 0.01)
        {
            return;
        }

        _opacityTarget = target;
        var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, this);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
        new Microsoft.UI.Xaml.Media.Animation.Storyboard { Children = { animation } }.Begin();
    }
}
