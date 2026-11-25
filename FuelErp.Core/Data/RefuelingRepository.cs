using Microsoft.Extensions.Logging;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.Data;

public class RefuelingRepository : IRefuelingRepository
{
    private readonly AppDatabase _database;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<RefuelingRepository> _logger;

    public RefuelingRepository(AppDatabase database, IEncryptionService encryption, ILogger<RefuelingRepository> logger)
    {
        _database = database;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<List<Refueling>> GetAllAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var records = await db.Table<RefuelingRecord>().OrderByDescending(r => r.RefueledAt).ToListAsync();
            var vehicles = (await db.Table<VehicleRecord>().ToListAsync()).ToDictionary(v => v.Id);
            var tanks = (await db.Table<FuelTankRecord>().ToListAsync()).ToDictionary(t => t.Id);

            var result = new List<Refueling>();
            foreach (var record in records)
                result.Add(await ToModelAsync(record, vehicles, tanks));

            _logger.LogDebug("Прочитано заправок із таблиці Refuelings: {Count}", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати список заправок");
            throw;
        }
    }

    public async Task<Refueling?> GetByIdAsync(int id)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var record = await db.FindAsync<RefuelingRecord>(id);

            if (record is null)
            {
                _logger.LogWarning("Заправку Id={Id} не знайдено в БД", id);
                return null;
            }

            var vehicles = (await db.Table<VehicleRecord>().ToListAsync()).ToDictionary(v => v.Id);
            var tanks = (await db.Table<FuelTankRecord>().ToListAsync()).ToDictionary(t => t.Id);
            return await ToModelAsync(record, vehicles, tanks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати заправку Id={Id}", id);
            throw;
        }
    }

    public async Task<int> SaveAsync(Refueling refueling)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var encryptedDriver = await _encryption.EncryptAsync(refueling.DriverName);
            var now = DateTime.UtcNow;

            var record = new RefuelingRecord
            {
                Id = refueling.Id,
                VehicleId = refueling.VehicleId,
                TankId = refueling.TankId,
                DriverNameEncrypted = encryptedDriver,
                Liters = refueling.Liters,
                PricePerLiter = refueling.PricePerLiter,
                RefueledAt = refueling.RefueledAt,
                ReceiptPhotoPath = refueling.ReceiptPhotoPath,
                Latitude = refueling.Latitude,
                Longitude = refueling.Longitude,
                Notes = refueling.Notes ?? string.Empty,
                OperatorName = refueling.OperatorName,
                UpdatedAt = now
            };
            var isNew = record.Id == 0;

            // Запис заправки, зміна залишку в резервуарі та записи черги синхронізації - одна транзакція
            await db.RunInTransactionAsync(conn =>
            {
                var tank = conn.Find<FuelTankRecord>(record.TankId)
                           ?? throw new KeyNotFoundException($"Резервуар Id={record.TankId} не знайдено");
                var available = tank.CurrentLiters;

                if (!isNew)
                {
                    var previous = conn.Find<RefuelingRecord>(record.Id)
                                   ?? throw new KeyNotFoundException($"Заправку Id={record.Id} не знайдено");

                    if (previous.TankId == record.TankId)
                    {
                        available += previous.Liters;
                    }
                    else
                    {
                        // заправку перенесено в інший резервуар: пальне повертається в старий
                        var oldTank = conn.Find<FuelTankRecord>(previous.TankId);
                        if (oldTank is not null)
                        {
                            oldTank.CurrentLiters = Math.Min(oldTank.CapacityLiters,
                                Math.Round(oldTank.CurrentLiters + previous.Liters, 3));
                            oldTank.UpdatedAt = now;
                            conn.Update(oldTank);
                            conn.Insert(SyncQueueWriter.Create("FuelTank", "Update", oldTank));
                        }
                    }
                }

                if (record.Liters > available + 0.0001)
                    throw new InsufficientFuelException(tank.Name, available, record.Liters);

                tank.CurrentLiters = Math.Round(available - record.Liters, 3);
                tank.UpdatedAt = now;
                conn.Update(tank);
                conn.Insert(SyncQueueWriter.Create("FuelTank", "Update", tank));

                if (isNew)
                    conn.Insert(record);
                else
                    conn.Update(record);

                conn.Insert(SyncQueueWriter.Create("Refueling", isNew ? "Insert" : "Update", record));
            });

            refueling.Id = record.Id;
            _logger.LogInformation("Заправку збережено: Id={Id}, операція={Operation}, літрів={Liters}, фото={HasPhoto}, координати={HasLocation}",
                record.Id, isNew ? "INSERT" : "UPDATE", record.Liters, refueling.HasPhoto, refueling.HasLocation);
            _logger.LogDebug("ПІБ водія в БД (зашифровано): {Cipher}", encryptedDriver);

            return 1;
        }
        catch (InsufficientFuelException ex)
        {
            _logger.LogWarning("Заправку не збережено: {Reason}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти заправку (Id={Id})", refueling.Id);
            throw;
        }
    }

    public async Task<int> DeleteAsync(Refueling refueling)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var now = DateTime.UtcNow;
            var rows = 0;

            await db.RunInTransactionAsync(conn =>
            {
                var previous = conn.Find<RefuelingRecord>(refueling.Id);
                if (previous is null)
                    return;

                var tank = conn.Find<FuelTankRecord>(previous.TankId);
                if (tank is not null)
                {
                    tank.CurrentLiters = Math.Min(tank.CapacityLiters, Math.Round(tank.CurrentLiters + previous.Liters, 3));
                    tank.UpdatedAt = now;
                    conn.Update(tank);
                    conn.Insert(SyncQueueWriter.Create("FuelTank", "Update", tank));
                }

                rows = conn.Delete(previous);
                conn.Insert(SyncQueueWriter.Create("Refueling", "Delete", new { previous.Id, UpdatedAt = now }));
            });

            if (rows == 0)
                _logger.LogWarning("Видалення: заправку Id={Id} не знайдено в БД", refueling.Id);
            else
                _logger.LogInformation("Заправку видалено, пальне повернено в резервуар: Id={Id}", refueling.Id);

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося видалити заправку Id={Id}", refueling.Id);
            throw;
        }
    }

    private async Task<Refueling> ToModelAsync(
        RefuelingRecord record,
        IReadOnlyDictionary<int, VehicleRecord> vehicles,
        IReadOnlyDictionary<int, FuelTankRecord> tanks)
    {
        string driver;
        try
        {
            driver = await _encryption.DecryptAsync(record.DriverNameEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося розшифрувати ПІБ водія для заправки Id={Id}", record.Id);
            driver = "(не вдалося розшифрувати)";
        }

        vehicles.TryGetValue(record.VehicleId, out var vehicle);
        tanks.TryGetValue(record.TankId, out var tank);

        return new Refueling
        {
            Id = record.Id,
            VehicleId = record.VehicleId,
            TankId = record.TankId,
            DriverName = driver,
            Liters = record.Liters,
            PricePerLiter = record.PricePerLiter,
            RefueledAt = record.RefueledAt,
            ReceiptPhotoPath = record.ReceiptPhotoPath,
            Latitude = record.Latitude,
            Longitude = record.Longitude,
            Notes = record.Notes,
            OperatorName = record.OperatorName,
            VehiclePlate = vehicle?.PlateNumber ?? "?",
            TankName = tank?.Name ?? "?",
            FuelType = tank is null ? FuelType.Petrol95 : (FuelType)tank.FuelType
        };
    }
}
