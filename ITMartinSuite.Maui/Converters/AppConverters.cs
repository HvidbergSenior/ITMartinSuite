using System.Globalization;

namespace ITMartinSuite.Maui.Converters;

public class NullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null || (value is string s && string.IsNullOrEmpty(s));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}

public class NotNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && !(value is string s && string.IsNullOrEmpty(s));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}

public class NotEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !string.IsNullOrEmpty(value as string);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}

public class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class TypeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value as string) switch
        {
            "Offer" => Color.FromArgb("#2ecc71"),
            "Info"  => Color.FromArgb("#3498db"),
            _       => Color.FromArgb("#e74c3c"),
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}

public class TypeSelectedColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var selected = value as string;
        var param = parameter as string;
        if (selected == param)
        {
            return param switch
            {
                "Offer" => Color.FromArgb("#2ecc71"),
                "Info"  => Color.FromArgb("#3498db"),
                _       => Color.FromArgb("#e74c3c"),
            };
        }
        return Color.FromArgb("#2a2a4a");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
