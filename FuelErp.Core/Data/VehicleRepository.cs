using Microsoft.Extensions.Logging;
using FuelErp.Models;

namespace FuelErp.Data;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDatabase _database;
    private readonly ILogger<VehicleRepository> _logger;

    public VehicleRepository(AppDatabase database, ILogger<VehicleRepository> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<List<Vehicle>> GetAllAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var records = await db.Table<VehicleRecord>().OrderBy(r => r.PlateNumber).ToListAsync();

            _logger.LogDebug("Прочитано транспортних засобів: {Count}", records.Count);
            return records.Select(ToModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати список транспортних засобів");
            throw;
        }
    }

    public async Task<Vehicle?> GetByIdAsync(int id)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var record = await db.FindAsync<VehicleRecord>(id);

            if (record is null)
                _logger.LogWarning("Транспортний засіб Id={Id} не знайдено в БД", id);

            return record is null ? null : ToModel(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати транспортний засіб Id={Id}", id);
            throw;
        }
    }

    public async Task<int> SaveAsync(Vehicle vehicle)
    {
        var plate = Vehicle.NormalizePlate(vehicle.PlateNumber);

        try
        {
            var db = await _database.GetConnectionAsync();

            var duplicate = await db.Table<VehicleRecord>()
                .Where(r => r.PlateNumber == plate && r.Id != vehicle.Id)
                .FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                _logger.LogWarning("Спроба зареєструвати дубль транспортного засобу {Plate}", plate);
                throw new DuplicateVehicleException(plate);
            }

            var record = new VehicleRecord
            {
                Id = vehicle.Id,
                PlateNumber = plate,
                Model = vehicle.Model.Trim(),
                FuelType = (int)vehicle.FuelType,
                UpdatedAt = DateTime.UtcNow
            };

            var isNew = record.Id == 0;
            var rows = isNew ? await db.InsertAsync(record) : await db.UpdateAsync(record);

            vehicle.Id = record.Id;
            vehicle.PlateNumber = plate;

            await db.InsertAsync(SyncQueueWriter.Create("Vehicle", isNew ? "Insert" : "Update", record));
            _logger.LogInformation("Транспортний засіб збережено: Id={Id}, операція={Operation}",
                record.Id, isNew ? "INSERT" : "UPDATE");

            return rows;
        }
        catch (DuplicateVehicleException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти транспортний засіб {Plate}", plate);
            throw;
        }
    }

    private static Vehicle ToModel(VehicleRecord record) => new()
    {
        Id = record.Id,
        PlateNumber = record.PlateNumber,
        Model = record.Model,
        FuelType = (FuelType)record.FuelType
    };
}
