using FuelErp.Models;

namespace FuelErp.ViewModels;

/// <summary>Варіант фільтра за видом палива у списку заправок (Type = null означає «усі»).</summary>
public record FuelFilterOption(FuelType? Type, string Name)
{
    public static IReadOnlyList<FuelFilterOption> All { get; } = new[]
    {
        new FuelFilterOption(null, "Усі види"),
        new FuelFilterOption(FuelType.Petrol95, FuelType.Petrol95.ToDisplayName()),
        new FuelFilterOption(FuelType.Diesel, FuelType.Diesel.ToDisplayName()),
        new FuelFilterOption(FuelType.Lubricant, FuelType.Lubricant.ToDisplayName())
    };
}

public record FuelTypeOption(FuelType Type, string Name)
{
    public static IReadOnlyList<FuelTypeOption> All { get; } = Enum.GetValues<FuelType>()
        .Select(t => new FuelTypeOption(t, t.ToDisplayName()))
        .ToList();
}
