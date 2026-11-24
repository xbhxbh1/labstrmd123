using System.Globalization;
using StudentCabinetMVVM.ViewModels;

namespace StudentCabinetMVVM.Converters;

/// <summary>Перетворює елемент колекції маркерів ядра (широта/довгота) на Location для Pin.</summary>
public class PinLocationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is InspectionPinItem item ? new Location(item.Latitude, item.Longitude) : new Location(0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
