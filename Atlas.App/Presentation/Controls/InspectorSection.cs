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
        if (GetTemplateChild("PART_Header") is UIElement header)
        {
            header.Tapped += (_, _) => IsOpen = !IsOpen;
        }

        Apply();
    }

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((InspectorSection)d).Apply();

    private void Apply()
    {
        if (_body is not null)
        {
            _body.Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_caret?.RenderTransform is RotateTransform rotate)
        {
            rotate.Angle = IsOpen ? 90 : 0;
        }
    }
}
