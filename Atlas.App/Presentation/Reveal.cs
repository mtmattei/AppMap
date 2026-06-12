using Microsoft.UI.Xaml.Media.Animation;

namespace Atlas.App.Presentation;

/// <summary>
/// Fades an element in/out as a bound value becomes present/absent, replacing the
/// hard Visibility snap. "Present" is anything except null, false, or a blank string,
/// so states like <c>Notice</c> bind directly without a converter. The element should
/// start hidden in XAML (Opacity="0" Visibility="Collapsed"): a first bound value of
/// "absent" equals the attached-property default and raises no change callback.
/// </summary>
public static class Reveal
{
    private const int FadeInMs = 150;
    private const int FadeOutMs = 200;

    public static readonly DependencyProperty WhenProperty = DependencyProperty.RegisterAttached(
        "When", typeof(object), typeof(Reveal), new PropertyMetadata(null, OnWhenChanged));

    public static object? GetWhen(DependencyObject obj) => obj.GetValue(WhenProperty);

    public static void SetWhen(DependencyObject obj, object? value) => obj.SetValue(WhenProperty, value);

    private static bool IsPresent(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => !string.IsNullOrWhiteSpace(s),
        _ => true,
    };

    private static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element || IsPresent(e.OldValue) == IsPresent(e.NewValue))
        {
            return;
        }

        if (IsPresent(e.NewValue))
        {
            element.Visibility = Visibility.Visible;
            Animate(element, to: 1, FadeInMs);
        }
        else
        {
            var storyboard = Animate(element, to: 0, FadeOutMs);
            storyboard.Completed += (_, _) =>
            {
                // A newer value may have re-revealed the element while this fade ran.
                if (!IsPresent(GetWhen(element)))
                {
                    element.Visibility = Visibility.Collapsed;
                }
            };
        }
    }

    private static Storyboard Animate(UIElement element, double to, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard { Children = { animation } };
        storyboard.Begin();
        return storyboard;
    }
}
