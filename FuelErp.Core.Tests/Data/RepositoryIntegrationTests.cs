using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;

namespace FuelErp.Tests.Data;

/// <summary>
/// Інтеграційні тести на справжній SQLite-базі: тимчасовий файл створюється перед кожним тестом
/// і видаляється після нього (IAsyncLifetime).
/// </summary>
public class StudentRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db3");
    private AppDatabase _database = null!;
    private StudentRepository _repo = null!;

    public Task InitializeAsync()
    {
        _database = new AppDatabase(_dbPath);
        _repo = new StudentRepository(_database, new AesEncryptionService(new FakeSecureStore()),
            NullLogger<StudentRepository>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _database.CloseAsync();
        File.Delete(_dbPath);
    }

    private async Task<List<SyncQueueItem>> QueueAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<SyncQueueItem>().OrderBy(i => i.Id).ToListAsync();
    }

    [Fact]
    public async Task Save_ThenGetById_ReturnsTheSameStudent()
    {
        var student = new Student { FullName = "Іваненко Іван Іванович", Group = "КН-31", AverageScore = 4.5 };

        await _repo.SaveAsync(student);
        var loaded = await _repo.GetByIdAsync(student.Id);

        student.Id.Should().BeGreaterThan(0);
        loaded.Should().NotBeNull();
        loaded!.FullName.Should().Be("Іваненко Іван Іванович");
        loaded.Group.Should().Be("КН-31");
        loaded.AverageScore.Should().Be(4.5);
    }

    [Fact]
    public async Task Save_StoresFullNameEncryptedInTheDatabaseFile()
    {
        await _repo.SaveAsync(new Student { FullName = "Іваненко Іван Іванович", Group = "КН-31" });

        var db = await _database.GetConnectionAsync();
        var rows = await db.Table<StudentRecord>().ToListAsync();

        rows.Should().ContainSingle();
        rows[0].FullNameEncrypted.Should().NotBeNullOrEmpty().And.NotContain("Іваненко");
    }

    [Fact]
    public async Task Update_ChangesTheStoredRecord()
    {
        var student = new Student { FullName = "Іваненко Іван", Group = "КН-31", AverageScore = 3.0 };
        await _repo.SaveAsync(student);

        student.AverageScore = 4.9;
        await _repo.SaveAsync(student);
        var loaded = await _repo.GetByIdAsync(student.Id);

        loaded!.AverageScore.Should().Be(4.9);
        (await _repo.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_RemovesTheRecord()
    {
        var student = new Student { FullName = "Іваненко Іван" };
        await _repo.SaveAsync(student);

        var rows = await _repo.DeleteAsync(student);

        rows.Should().Be(1);
        (await _repo.GetByIdAsync(student.Id)).Should().BeNull();
        (await _repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_ReturnsEveryStudentWithDecryptedNames()
    {
        await _repo.SaveAsync(new Student { FullName = "Перший" });
        await _repo.SaveAsync(new Student { FullName = "Другий" });

        var all = await _repo.GetAllAsync();

        all.Select(s => s.FullName).Should().BeEquivalentTo(new[] { "Перший", "Другий" });
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        (await _repo.GetByIdAsync(12345)).Should().BeNull();
    }

    [Fact]
    public async Task EveryChange_IsAddedToTheOfflineSyncQueueInOrder()
    {
        var student = new Student { FullName = "Іваненко Іван" };

        await _repo.SaveAsync(student);      // Insert
        student.AverageScore = 4.0;
        await _repo.SaveAsync(student);      // Update
        await _repo.DeleteAsync(student);    // Delete
        var queue = await QueueAsync();

        queue.Select(q => q.Operation).Should().Equal("Insert", "Update", "Delete");
        queue.Should().OnlyContain(q => q.EntityType == "Student");
        queue[0].PayloadJson.Should().NotContain("Іваненко");
    }
}

public class InspectionRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db3");
    private AppDatabase _database = null!;
    private InspectionRepository _repo = null!;

    public Task InitializeAsync()
    {
        _database = new AppDatabase(_dbPath);
        _repo = new InspectionRepository(_database, NullLogger<InspectionRepository>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _database.CloseAsync();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Save_KeepsPhotoPathAndCoordinates()
    {
        var inspection = new RoomInspection
        {
            RoomNumber = "205",
            InspectedAt = new DateTime(2026, 11, 24, 10, 30, 0),
            Description = "Тріщина на стіні",
            PhotoPath = @"C:\photos\room_205.jpg",
            Latitude = 50.4501,
            Longitude = 30.5234
        };

        await _repo.SaveAsync(inspection);
        var loaded = await _repo.GetByIdAsync(inspection.Id);

        loaded.Should().NotBeNull();
        loaded!.RoomNumber.Should().Be("205");
        loaded.PhotoPath.Should().Be(@"C:\photos\room_205.jpg");
        loaded.Latitude.Should().Be(50.4501);
        loaded.Longitude.Should().Be(30.5234);
        loaded.HasLocation.Should().BeTrue();
    }

    [Fact]
    public async Task Save_WithoutCoordinates_StoresNulls()
    {
        var inspection = new RoomInspection { RoomNumber = "101", InspectedAt = DateTime.Now };

        await _repo.SaveAsync(inspection);
        var loaded = await _repo.GetByIdAsync(inspection.Id);

        loaded!.Latitude.Should().BeNull();
        loaded.Longitude.Should().BeNull();
        loaded.HasLocation.Should().BeFalse();
        loaded.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_ReturnsNewestInspectionFirst()
    {
        await _repo.SaveAsync(new RoomInspection { RoomNumber = "old", InspectedAt = new DateTime(2026, 1, 1) });
        await _repo.SaveAsync(new RoomInspection { RoomNumber = "new", InspectedAt = new DateTime(2026, 6, 1) });

        var all = await _repo.GetAllAsync();

        all.Select(i => i.RoomNumber).Should().Equal("new", "old");
    }

    [Fact]
    public async Task Save_AddsRecordToTheSyncQueue()
    {
        await _repo.SaveAsync(new RoomInspection { RoomNumber = "101", InspectedAt = DateTime.Now });
        var db = await _database.GetConnectionAsync();

        var queue = await db.Table<SyncQueueItem>().ToListAsync();

        queue.Should().ContainSingle();
        queue[0].EntityType.Should().Be("RoomInspection");
        queue[0].Operation.Should().Be("Insert");
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        (await _repo.GetByIdAsync(999)).Should().BeNull();
    }
}
