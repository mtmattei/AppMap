using Atlas.Core;
using Microsoft.UI.Xaml.Media;

namespace Atlas.App.Presentation.Controls;

public sealed partial class NodeCard : UserControl
{
    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(string), typeof(NodeCard), new PropertyMetadata(null, OnSelectedIdChanged));

    public NodeCard()
    {
        this.InitializeComponent();
        DataContextChanged += (_, _) => UpdateSelectionVisual();
    }

    /// <summary>Id of the currently selected node; drives the selection ring.</summary>
    public string? SelectedId
    {
        get => (string?)GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    private static void OnSelectedIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((NodeCard)d).UpdateSelectionVisual();

    private void UpdateSelectionVisual()
    {
        var isSelected = DataContext is AppNode node && node.Id == SelectedId;
        var key = isSelected ? "AtlasLiveBrush" : "AtlasBorder2Brush";
        if (Application.Current.Resources.TryGetValue(key, out var brush))
        {
            Card.BorderBrush = (Brush)brush;
        }
    }
}
