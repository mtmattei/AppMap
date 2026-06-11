using Atlas.Core;
using Microsoft.UI.Xaml.Media;

namespace Atlas.App.Presentation.Controls;

public sealed partial class NodeCard : UserControl
{
    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(string), typeof(NodeCard), new PropertyMetadata(null, OnVisualStateInputChanged));

    public static readonly DependencyProperty HighlightedIdsProperty = DependencyProperty.Register(
        nameof(HighlightedIds), typeof(object), typeof(NodeCard), new PropertyMetadata(null, OnVisualStateInputChanged));

    public NodeCard()
    {
        this.InitializeComponent();
        DataContextChanged += (_, _) => UpdateVisualState();
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

    private static void OnVisualStateInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NodeCard)d).UpdateVisualState();

    private void UpdateVisualState()
    {
        var node = DataContext as AppNode;
        var isSelected = node is not null && node.Id == SelectedId;

        var highlightedIds = HighlightedIds as IReadOnlyList<string>;
        var highlightActive = highlightedIds is { Count: > 0 };
        var isHighlighted = highlightActive && node is not null && highlightedIds!.Contains(node.Id);

        Opacity = highlightActive && !isHighlighted && !isSelected ? 0.25 : 1.0;

        var key = isSelected || isHighlighted ? "AtlasLiveBrush" : "AtlasBorder2Brush";
        if (Application.Current.Resources.TryGetValue(key, out var brush))
        {
            Card.BorderBrush = (Brush)brush;
        }
    }
}
