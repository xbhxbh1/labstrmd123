namespace FuelErp.Models;

public enum FuelType
{
    Petrol95,
    Diesel,
    Lubricant
}

public static class FuelTypeExtensions
{
    public static string ToDisplayName(this FuelType type) => type switch
    {
        FuelType.Petrol95 => "Бензин А-95",
        FuelType.Diesel => "Дизельне паливо",
        FuelType.Lubricant => "Мастильні матеріали",
        _ => type.ToString()
    };
}
