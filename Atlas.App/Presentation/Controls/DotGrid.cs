using Microsoft.UI.Xaml.Media;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Atlas.App.Presentation.Controls;

/// <summary>
/// A dot lattice across the canvas world rect. It lives in content space, so it
/// pans and scales with the graph — the parallax against the fixed chrome is what
/// gives the canvas depth. One Path with grouped geometries keeps it a single draw.
/// </summary>
public sealed partial class DotGrid : Canvas
{
    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill), typeof(Brush), typeof(DotGrid), new PropertyMetadata(null, OnGridChanged));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(DotGrid), new PropertyMetadata(32.0, OnGridChanged));

    public static readonly DependencyProperty DotRadiusProperty = DependencyProperty.Register(
        nameof(DotRadius), typeof(double), typeof(DotGrid), new PropertyMetadata(1.2, OnGridChanged));

    public DotGrid()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Regenerate();
    }

    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double DotRadius
    {
        get => (double)GetValue(DotRadiusProperty);
        set => SetValue(DotRadiusProperty, value);
    }

    private static void OnGridChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DotGrid)d).Regenerate();

    private void Regenerate()
    {
        Children.Clear();
        if (Fill is null || Spacing <= 0 || double.IsNaN(Width) || double.IsNaN(Height))
        {
            return;
        }

        var dots = new GeometryGroup();
        for (var x = Spacing; x < Width; x += Spacing)
        {
            for (var y = Spacing; y < Height; y += Spacing)
            {
                dots.Children.Add(new EllipseGeometry
                {
                    Center = new Windows.Foundation.Point(x, y),
                    RadiusX = DotRadius,
                    RadiusY = DotRadius,
                });
            }
        }

        Children.Add(new Path { Data = dots, Fill = Fill });
    }
}
