namespace FuelErp.Data;

/// <summary>Початкові демонстраційні дані (резервуари, транспорт, кілька заправок), щоб екрани не були порожніми.</summary>
public interface IFuelDataSeeder
{
    Task SeedAsync();
}
