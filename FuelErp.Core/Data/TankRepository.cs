using Microsoft.Extensions.Logging;
using FuelErp.Models;

namespace FuelErp.Data;

public class TankRepository : ITankRepository
{
    private readonly AppDatabase _database;
    private readonly ILogger<TankRepository> _logger;

    public TankRepository(AppDatabase database, ILogger<TankRepository> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<List<FuelTank>> GetAllAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var records = await db.Table<FuelTankRecord>().OrderBy(r => r.Name).ToListAsync();

            _logger.LogDebug("Прочитано резервуарів: {Count}", records.Count);
            return records.Select(ToModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати список резервуарів");
            throw;
        }
    }

    public async Task<FuelTank?> GetByIdAsync(int id)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var record = await db.FindAsync<FuelTankRecord>(id);

            if (record is null)
                _logger.LogWarning("Резервуар Id={Id} не знайдено в БД", id);

            return record is null ? null : ToModel(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати резервуар Id={Id}", id);
            throw;
        }
    }

    public async Task<int> SaveAsync(FuelTank tank)
    {
        try
        {
            var db = await _database.GetConnectionAsync();

            var record = new FuelTankRecord
            {
                Id = tank.Id,
                Name = tank.Name.Trim(),
                FuelType = (int)tank.FuelType,
                CapacityLiters = tank.CapacityLiters,
                CurrentLiters = tank.CurrentLiters,
                UpdatedAt = DateTime.UtcNow
            };

            var isNew = record.Id == 0;
            var rows = isNew ? await db.InsertAsync(record) : await db.UpdateAsync(record);
            tank.Id = record.Id;

            await db.InsertAsync(SyncQueueWriter.Create("FuelTank", isNew ? "Insert" : "Update", record));
            _logger.LogInformation("Резервуар збережено: Id={Id}, операція={Operation}",
                record.Id, isNew ? "INSERT" : "UPDATE");

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти резервуар {Name}", tank.Name);
            throw;
        }
    }

    private static FuelTank ToModel(FuelTankRecord record) => new()
    {
        Id = record.Id,
        Name = record.Name,
        FuelType = (FuelType)record.FuelType,
        CapacityLiters = record.CapacityLiters,
        CurrentLiters = record.CurrentLiters
    };
}
