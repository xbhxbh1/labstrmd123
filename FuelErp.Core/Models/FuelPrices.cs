namespace FuelErp.Models;

/// <summary>Орієнтовні ціни за літр, які підставляються у форму заправки (оператор може їх змінити).</summary>
public static class FuelPrices
{
    public static double For(FuelType type) => type switch
    {
        FuelType.Petrol95 => 53.50,
        FuelType.Diesel => 51.90,
        FuelType.Lubricant => 180.00,
        _ => 0
    };
}
