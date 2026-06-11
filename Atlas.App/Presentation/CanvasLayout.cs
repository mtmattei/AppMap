using Atlas.Core;
using Microsoft.UI.Xaml.Controls;

namespace Atlas.App.Presentation;

/// <summary>
/// Arranges ItemsRepeater children at the absolute position carried by their
/// AppNode data context. The map is a fixed-coordinate graph, not a flow layout.
/// </summary>
public partial class CanvasLayout : NonVirtualizingLayout
{
    protected override Windows.Foundation.Size MeasureOverride(NonVirtualizingLayoutContext context, Windows.Foundation.Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in context.Children)
        {
            child.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            var position = GetPosition(child);
            width = Math.Max(width, position.X + child.DesiredSize.Width);
            height = Math.Max(height, position.Y + child.DesiredSize.Height);
        }

        return new Windows.Foundation.Size(width, height);
    }

    protected override Windows.Foundation.Size ArrangeOverride(NonVirtualizingLayoutContext context, Windows.Foundation.Size finalSize)
    {
        foreach (var child in context.Children)
        {
            var position = GetPosition(child);
            child.Arrange(new Windows.Foundation.Rect(position.X, position.Y, child.DesiredSize.Width, child.DesiredSize.Height));
        }

        return finalSize;
    }

    private static Atlas.Core.Point GetPosition(UIElement child) =>
        (child as FrameworkElement)?.DataContext is AppNode { Position: { } position }
            ? position
            : new Atlas.Core.Point(0, 0);
}
