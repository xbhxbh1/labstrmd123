using Microsoft.Extensions.Logging;
using FuelErp.Models;

namespace FuelErp.Data;

public class FuelDataSeeder : IFuelDataSeeder
{
    private readonly AppDatabase _database;
    private readonly ITankRepository _tanks;
    private readonly IVehicleRepository _vehicles;
    private readonly IRefuelingRepository _refuelings;
    private readonly ILogger<FuelDataSeeder> _logger;

    public FuelDataSeeder(
        AppDatabase database,
        ITankRepository tanks,
        IVehicleRepository vehicles,
        IRefuelingRepository refuelings,
        ILogger<FuelDataSeeder> logger)
    {
        _database = database;
        _tanks = tanks;
        _vehicles = vehicles;
        _refuelings = refuelings;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var queueWasEmpty = await db.Table<SyncQueueItem>().CountAsync() == 0;
            var seeded = false;

            if ((await _tanks.GetAllAsync()).Count == 0)
            {
                // різні рівні, щоб було видно індикацію: нормальний, низький і критичний
                await _tanks.SaveAsync(new FuelTank { Name = "Резервуар №1", FuelType = FuelType.Petrol95, CapacityLiters = 30000, CurrentLiters = 24000 });
                await _tanks.SaveAsync(new FuelTank { Name = "Резервуар №2", FuelType = FuelType.Diesel, CapacityLiters = 40000, CurrentLiters = 9000 });
                await _tanks.SaveAsync(new FuelTank { Name = "Ємність мастил", FuelType = FuelType.Lubricant, CapacityLiters = 2000, CurrentLiters = 160 });
                seeded = true;
            }

            if ((await _vehicles.GetAllAsync()).Count == 0)
            {
                await _vehicles.SaveAsync(new Vehicle { PlateNumber = "АА1234КК", Model = "MAN TGS", FuelType = FuelType.Diesel });
                await _vehicles.SaveAsync(new Vehicle { PlateNumber = "АЕ5678ІН", Model = "Škoda Octavia", FuelType = FuelType.Petrol95 });
                await _vehicles.SaveAsync(new Vehicle { PlateNumber = "КА9012ВВ", Model = "Mercedes Sprinter", FuelType = FuelType.Diesel });
                await _vehicles.SaveAsync(new Vehicle { PlateNumber = "ВС3456АХ", Model = "Toyota Camry", FuelType = FuelType.Petrol95 });
                seeded = true;
            }

            if ((await _refuelings.GetAllAsync()).Count == 0)
            {
                var tanks = await _tanks.GetAllAsync();
                var vehicles = await _vehicles.GetAllAsync();
                var petrol = tanks.First(t => t.FuelType == FuelType.Petrol95);
                var diesel = tanks.First(t => t.FuelType == FuelType.Diesel);
                var oil = tanks.First(t => t.FuelType == FuelType.Lubricant);
                var now = DateTime.Now;

                await AddAsync(vehicles[0], diesel, "Коваленко Іван Петрович", 120, 51.90, now.AddDays(-2).AddHours(-3));
                await AddAsync(vehicles[1], petrol, "Шевченко Олена Андріївна", 40, 53.50, now.AddDays(-1).AddHours(-5));
                await AddAsync(vehicles[2], diesel, "Бойко Андрій Миколайович", 80, 51.90, now.AddDays(-1));
                await AddAsync(vehicles[3], petrol, "Мельник Тарас Олегович", 35, 53.50, now.AddHours(-4));
                await AddAsync(vehicles[0], oil, "Коваленко Іван Петрович", 15, 180.00, now.AddHours(-2));
                seeded = true;
            }

            // демонстраційні дані не потрібно відправляти на сервер
            if (seeded && queueWasEmpty)
                await db.DeleteAllAsync<SyncQueueItem>();

            if (seeded)
                _logger.LogInformation("Створено демонстраційні дані: резервуари, транспорт і заправки");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося створити демонстраційні дані");
            throw;
        }
    }

    private Task<int> AddAsync(Vehicle vehicle, FuelTank tank, string driver, double liters, double price, DateTime at) =>
        _refuelings.SaveAsync(new Refueling
        {
            VehicleId = vehicle.Id,
            TankId = tank.Id,
            DriverName = driver,
            Liters = liters,
            PricePerLiter = price,
            RefueledAt = at,
            OperatorName = "operator"
        });
}
