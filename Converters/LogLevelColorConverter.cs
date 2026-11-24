using System.Globalization;

namespace StudentCabinetMVVM.Converters;

public class LogLevelColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value as string) switch
        {
            "Fatal" => Colors.DarkRed,
            "Error" => Colors.Red,
            "Warning" => Colors.DarkOrange,
            "Information" => Colors.SeaGreen,
            _ => Colors.Gray
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
