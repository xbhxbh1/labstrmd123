namespace FuelErp.Models;

/// <summary>Транспортний засіб, який заправляють (ТранспортнийЗасіб).</summary>
public class Vehicle
{
    public int Id { get; set; }

    public string PlateNumber { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public FuelType FuelType { get; set; }

    public string DisplayName => $"{PlateNumber} · {Model}";

    /// <summary>Номер записується без пробілів по краях і великими літерами, щоб «аа1234кк» і «АА1234КК» збігалися.</summary>
    public static string NormalizePlate(string? plate) => (plate ?? string.Empty).Trim().ToUpperInvariant();
}
