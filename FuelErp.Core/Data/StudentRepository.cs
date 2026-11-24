using System.Text.Json;
using Microsoft.Extensions.Logging;
using SQLite;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.Data;

public class StudentRepository : IStudentRepository
{
    private readonly AppDatabase _database;
    private readonly IEncryptionService _encryption;
    private readonly ILogger<StudentRepository> _logger;

    public StudentRepository(AppDatabase database, IEncryptionService encryption, ILogger<StudentRepository> logger)
    {
        _database = database;
        _encryption = encryption;
        _logger = logger;
    }

    public async Task<List<Student>> GetAllAsync()
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var records = await db.Table<StudentRecord>().ToListAsync();

            var students = new List<Student>();
            foreach (var record in records)
                students.Add(await ToStudentAsync(record));

            _logger.LogDebug("Прочитано записів з таблиці Students: {Count}", students.Count);
            return students;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати список студентів із БД");
            throw;
        }
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var record = await db.FindAsync<StudentRecord>(id);

            if (record is null)
            {
                _logger.LogWarning("Студента з Id={Id} не знайдено в БД", id);
                return null;
            }

            return await ToStudentAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати студента з Id={Id}", id);
            throw;
        }
    }

    public async Task<int> SaveAsync(Student student)
    {
        try
        {
            var db = await _database.GetConnectionAsync();

            var record = new StudentRecord
            {
                Id = student.Id,
                FullNameEncrypted = await _encryption.EncryptAsync(student.FullName),
                Group = student.Group,
                AverageScore = student.AverageScore,
                UpdatedAt = DateTime.UtcNow
            };

            var isNew = record.Id == 0;
            var rows = isNew
                ? await db.InsertAsync(record)
                : await db.UpdateAsync(record);

            student.Id = record.Id;

            _logger.LogInformation("Студента збережено: Id={Id}, операція={Operation}",
                record.Id, isNew ? "INSERT" : "UPDATE");
            _logger.LogDebug("ПІБ у БД (зашифровано): {Cipher}", record.FullNameEncrypted);

            await EnqueueAsync(db, isNew ? "Insert" : "Update", JsonSerializer.Serialize(record));

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти студента (Id={Id})", student.Id);
            throw;
        }
    }

    public async Task<int> DeleteAsync(Student student)
    {
        try
        {
            var db = await _database.GetConnectionAsync();
            var rows = await db.DeleteAsync<StudentRecord>(student.Id);

            if (rows == 0)
                _logger.LogWarning("Видалення: запис з Id={Id} не знайдено в БД", student.Id);
            else
                _logger.LogInformation("Студента видалено: Id={Id}", student.Id);

            var payload = JsonSerializer.Serialize(new { student.Id, UpdatedAt = DateTime.UtcNow });
            await EnqueueAsync(db, "Delete", payload);

            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося видалити студента (Id={Id})", student.Id);
            throw;
        }
    }

    private async Task<Student> ToStudentAsync(StudentRecord record)
    {
        string fullName;
        try
        {
            fullName = await _encryption.DecryptAsync(record.FullNameEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося розшифрувати ПІБ студента Id={Id}", record.Id);
            fullName = "(не вдалося розшифрувати)";
        }

        return new Student
        {
            Id = record.Id,
            FullName = fullName,
            Group = record.Group,
            AverageScore = record.AverageScore
        };
    }

    private static Task EnqueueAsync(SQLiteAsyncConnection db, string operation, string payloadJson) =>
        db.InsertAsync(new SyncQueueItem
        {
            EntityType = "Student",
            Operation = operation,
            PayloadJson = payloadJson,
            CreatedAt = DateTime.UtcNow
        });
}
