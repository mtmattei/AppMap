using Atlas.Core;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace Atlas.App.Presentation;

/// <summary>Maps a NodeKind to its accent brush. Brushes are assigned from theme resources in XAML.</summary>
public sealed partial class NodeKindToBrushConverter : IValueConverter
{
    public Brush? Shell { get; set; }
    public Brush? Page { get; set; }
    public Brush? Dialog { get; set; }

    public object? Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            NodeKind.Shell => Shell,
            NodeKind.Dialog => Dialog,
            _ => Page,
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Uppercases the value's string form, for chrome labels.</summary>
public sealed partial class UpperConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language) =>
        value?.ToString()?.ToUpperInvariant() ?? string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Maps a bool to one of two brushes assigned from theme resources in XAML.</summary>
public sealed partial class BoolToBrushConverter : IValueConverter
{
    public Brush? TrueBrush { get; set; }
    public Brush? FalseBrush { get; set; }

    public object? Convert(object? value, Type targetType, object parameter, string language) =>
        value is true ? TrueBrush : FalseBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the bound index equals the converter parameter — drives tab panes.</summary>
public sealed partial class IndexVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Serializes an AppNode to the JSON the agent receives — the inspector's model peek.</summary>
public sealed partial class NodeJsonConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language) =>
        value is AppNode node
            ? System.Text.Json.JsonSerializer.Serialize(node, AppModelJson.Options)
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the NodeStatus name equals the converter parameter (e.g. 'Live', 'Orphan').</summary>
public sealed partial class StatusVisibleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, string language) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
