using Microsoft.UI.Xaml.Media;

namespace Atlas.App.Presentation.Controls;

/// <summary>
/// A minimal Solution-Explorer-style collapsible section: a hairline-topped header
/// (caret + title + optional count) that toggles a content area. Styled by
/// InspectorSectionStyle in Themes/Inspector.xaml.
/// </summary>
public sealed partial class InspectorSection : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(InspectorSection), new PropertyMetadata(null));

    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(object), typeof(InspectorSection), new PropertyMetadata(null));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(InspectorSection), new PropertyMetadata(true, OnIsOpenChanged));

    private FrameworkElement? _caret;
    private UIElement? _body;

    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _caret = GetTemplateChild("PART_Caret") as FrameworkElement;
        _body = GetTemplateChild("PART_Body") as UIElement;
        if (GetTemplateChild("PART_Header") is Microsoft.UI.Xaml.Controls.Grid header)
        {
            header.Tapped += (_, _) => IsOpen = !IsOpen;
            header.PointerEntered += (_, _) => SetHeaderHover(header, hovered: true);
            header.PointerExited += (_, _) => SetHeaderHover(header, hovered: false);
        }

        Apply(animate: false);
    }

    private static void SetHeaderHover(Microsoft.UI.Xaml.Controls.Grid header, bool hovered)
    {
        header.Background = hovered && Application.Current.Resources.TryGetValue("AtlasSurfaceBrush", out var brush)
            ? (Brush)brush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InspectorSection)d).Apply(animate: true);

    private void Apply(bool animate)
    {
        if (_body is not null)
        {
            _body.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_caret?.RenderTransform is not RotateTransform rotate)
        {
            return;
        }

        var target = IsOpen ? 90 : 0;
        if (!animate)
        {
            rotate.Angle = target;
            return;
        }

        var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            To = target,
            Duration = new Duration(TimeSpan.FromMilliseconds(140)),
            EnableDependentAnimation = true,
        };
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, rotate);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Angle");
        new Microsoft.UI.Xaml.Media.Animation.Storyboard { Children = { animation } }.Begin();
    }
}
