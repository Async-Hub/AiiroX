using AiiroX.Core.Models;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Globalization;

namespace AiiroX.ViewModels;

/// <summary>Converts a bool (IsConnected) to a status color.</summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? Color.Parse("#22BB77") : Color.Parse("#AAAAAA");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>Converts a MessageRole to a HorizontalAlignment for chat bubbles.</summary>
public sealed class RoleToAlignmentConverter : IValueConverter
{
    public static readonly RoleToAlignmentConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MessageRole.User ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>Converts a MessageRole to a background color for chat bubbles.</summary>
public sealed class RoleToColorConverter : IValueConverter
{
    public static readonly RoleToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is MessageRole.User ? Color.Parse("#5B5BD6") : Color.Parse("#F0F0FA");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>Returns true when a string is not null or empty. Used for optional string visibility.</summary>
public sealed class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}

/// <summary>
/// Returns true when a string has content (not null or empty).
/// Delegates to <see cref="NullToBoolConverter"/>; kept as a distinct type so XAML bindings
/// remain semantically self-documenting.
/// </summary>
public sealed class NullOrEmptyToBoolConverter : IValueConverter
{
    public static readonly NullOrEmptyToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => NullToBoolConverter.Instance.Convert(value, targetType, parameter, culture);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}
