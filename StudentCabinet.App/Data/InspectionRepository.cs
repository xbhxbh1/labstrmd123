using System.Text.Json;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Data;

public class InspectionRepository : IInspectionRepository
{
    private readonly AppDatabase _database;
    private readonly ILogger<InspectionRepository> _logger;

    public InspectionRepository(AppDatabase database, ILogger<InspectionRepository> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<List<RoomInspection>> GetAllAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var records = await db.Table<RoomInspectionRecord>()
                .OrderByDescending(r => r.InspectedAt)
                .ToListAsync();

            _logger.LogDebug("Прочитано актів огляду з таблиці RoomInspections: {Count}", records.Count);
            return records.Select(ToModel).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати список актів огляду");
            throw;
        }
    }

    public async Task<RoomInspection?> GetByIdAsync(int id)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var record = await db.FindAsync<RoomInspectionRecord>(id);

            if (record is null)
            {
                _logger.LogWarning("Акт огляду з Id={Id} не знайдено в БД", id);
                return null;
            }

            return ToModel(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати акт огляду Id={Id}", id);
            throw;
        }
    }

    public async Task<int> SaveAsync(RoomInspection inspection)
    {
        try
        {
            var db = await _database.GetConnectionAsync();

            var record = new RoomInspectionRecord
            {
                Id = inspection.Id,
                RoomNumber = inspection.RoomNumber,
                InspectedAt = inspection.InspectedAt,
                Description = inspection.Description,
                PhotoPath = inspection.PhotoPath,
                Latitude = inspection.Latitude,
                Longitude = inspection.Longitude,
                UpdatedAt = DateTime.UtcNow
            };

            var isNew = record.Id == 0;
            var rows = isNew
                ? await db.InsertAsync(record)
                : await db.UpdateAsync(record);

            inspection.Id = record.Id;

            _logger.LogInformation(
                "Акт огляду збережено: Id={Id}, операція={Operation}, фото={HasPhoto}, координати={HasLocation}",
                record.Id, isNew ? "INSERT" : "UPDATE", inspection.HasPhoto, inspection.HasLocation);

            // Зміна потрапляє в чергу офлайн-синхронізації (лабораторна робота 4)
            await db.InsertAsync(new SyncQueueItem
            {
                EntityType = "RoomInspection",
                Operation = isNew ? "Insert" : "Update",
                PayloadJson = JsonSerializer.Serialize(record),
                CreatedAt = DateTime.UtcNow
            });

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти акт огляду (аудиторія {Room})", inspection.RoomNumber);
            throw;
        }
    }

    private static RoomInspection ToModel(RoomInspectionRecord record) => new()
    {
        Id = record.Id,
        RoomNumber = record.RoomNumber,
        InspectedAt = record.InspectedAt,
        Description = record.Description,
        PhotoPath = record.PhotoPath,
        Latitude = record.Latitude,
        Longitude = record.Longitude
    };
}
