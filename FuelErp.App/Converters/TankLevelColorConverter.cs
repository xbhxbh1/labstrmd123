using System.Globalization;
using FuelErp.Models;

namespace FuelErp.Converters;

/// <summary>Колір індикатора резервуара за станом рівня: норма - зелений, низький - помаранчевий, критичний - червоний.</summary>
public class TankLevelColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TankLevelState.Critical => Colors.Red,
        TankLevelState.Low => Colors.Orange,
        _ => Colors.Green
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
