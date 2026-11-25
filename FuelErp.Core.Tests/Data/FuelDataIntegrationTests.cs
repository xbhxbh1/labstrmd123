using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;
using SQLite;

namespace FuelErp.Tests.Data;

/// <summary>
/// Інтеграційні тести модуля обліку палива на справжній SQLite-базі (тимчасовий файл на кожен тест).
/// </summary>
public class FuelDataIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fuel_{Guid.NewGuid():N}.db3");
    private AppDatabase _database = null!;
    private VehicleRepository _vehicles = null!;
    private TankRepository _tanks = null!;
    private RefuelingRepository _refuelings = null!;

    public Task InitializeAsync()
    {
        _database = new AppDatabase(_dbPath);
        _vehicles = new VehicleRepository(_database, NullLogger<VehicleRepository>.Instance);
        _tanks = new TankRepository(_database, NullLogger<TankRepository>.Instance);
        _refuelings = new RefuelingRepository(_database, new AesEncryptionService(new FakeSecureStore()),
            NullLogger<RefuelingRepository>.Instance);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _database.CloseAsync();
        File.Delete(_dbPath);
    }

    private async Task<(Vehicle Vehicle, FuelTank Tank)> CreateVehicleAndTankAsync(double level = 1000)
    {
        var vehicle = new Vehicle { PlateNumber = "аа1234кк", Model = "MAN", FuelType = FuelType.Diesel };
        await _vehicles.SaveAsync(vehicle);
        var tank = new FuelTank { Name = "Резервуар 1", FuelType = FuelType.Diesel, CapacityLiters = 5000, CurrentLiters = level };
        await _tanks.SaveAsync(tank);
        return (vehicle, tank);
    }

    private static Refueling NewRefueling(Vehicle vehicle, FuelTank tank, double liters, string driver = "Коваленко Іван") => new()
    {
        VehicleId = vehicle.Id,
        TankId = tank.Id,
        DriverName = driver,
        Liters = liters,
        PricePerLiter = 50,
        RefueledAt = new DateTime(2026, 11, 24, 10, 0, 0),
        OperatorName = "operator"
    };

    private async Task<List<SyncQueueItem>> QueueAsync()
    {
        var db = await _database.GetConnectionAsync();
        return await db.Table<SyncQueueItem>().OrderBy(i => i.Id).ToListAsync();
    }

    [Fact]
    public async Task Vehicle_SaveThenGet_NormalizesThePlateNumber()
    {
        var (vehicle, _) = await CreateVehicleAndTankAsync();

        var loaded = await _vehicles.GetByIdAsync(vehicle.Id);

        loaded.Should().NotBeNull();
        loaded!.PlateNumber.Should().Be("АА1234КК");
        loaded.FuelType.Should().Be(FuelType.Diesel);
    }

    [Fact]
    public async Task Vehicle_DuplicatePlateNumber_IsRejectedRegardlessOfCase()
    {
        await CreateVehicleAndTankAsync();

        var act = () => _vehicles.SaveAsync(new Vehicle { PlateNumber = " АА1234кк ", Model = "Інший" });

        await act.Should().ThrowAsync<DuplicateVehicleException>();
        (await _vehicles.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Vehicle_UpdateKeepingItsOwnPlate_IsAllowed()
    {
        var (vehicle, _) = await CreateVehicleAndTankAsync();

        vehicle.Model = "MAN TGX";
        await _vehicles.SaveAsync(vehicle);

        (await _vehicles.GetByIdAsync(vehicle.Id))!.Model.Should().Be("MAN TGX");
    }

    [Fact]
    public async Task Refueling_SaveThenGet_ReturnsDecryptedDriverAndRelatedNames()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync();
        var refueling = NewRefueling(vehicle, tank, 100);

        await _refuelings.SaveAsync(refueling);
        var loaded = await _refuelings.GetByIdAsync(refueling.Id);

        loaded.Should().NotBeNull();
        loaded!.DriverName.Should().Be("Коваленко Іван");
        loaded.VehiclePlate.Should().Be("АА1234КК");
        loaded.TankName.Should().Be("Резервуар 1");
        loaded.FuelType.Should().Be(FuelType.Diesel);
        loaded.Total.Should().Be(5000);
    }

    [Fact]
    public async Task Refueling_DriverNameIsEncryptedInTheTable()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync();
        await _refuelings.SaveAsync(NewRefueling(vehicle, tank, 10));

        var db = await _database.GetConnectionAsync();
        var rows = await db.Table<RefuelingRecord>().ToListAsync();

        rows.Should().ContainSingle();
        rows[0].DriverNameEncrypted.Should().NotBeNullOrEmpty().And.NotContain("Коваленко");
    }

    [Fact]
    public async Task Refueling_PhotoPathAndCoordinates_ArePersisted()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync();
        var refueling = NewRefueling(vehicle, tank, 10);
        refueling.ReceiptPhotoPath = @"C:\photos\receipt_1.jpg";
        refueling.Latitude = 50.4501;
        refueling.Longitude = 30.5234;

        await _refuelings.SaveAsync(refueling);
        var loaded = await _refuelings.GetByIdAsync(refueling.Id);

        loaded!.ReceiptPhotoPath.Should().Be(@"C:\photos\receipt_1.jpg");
        loaded.HasLocation.Should().BeTrue();
        loaded.Latitude.Should().Be(50.4501);
    }

    [Fact]
    public async Task Refueling_Save_DecreasesTheTankLevel()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync(1000);

        await _refuelings.SaveAsync(NewRefueling(vehicle, tank, 300));

        (await _tanks.GetByIdAsync(tank.Id))!.CurrentLiters.Should().Be(700);
    }

    [Fact]
    public async Task Refueling_Update_AppliesOnlyTheDifferenceToTheTank()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync(1000);
        var refueling = NewRefueling(vehicle, tank, 300);
        await _refuelings.SaveAsync(refueling);

        refueling.Liters = 350;
        await _refuelings.SaveAsync(refueling);

        (await _tanks.GetByIdAsync(tank.Id))!.CurrentLiters.Should().Be(650);
        (await _refuelings.GetAllAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Refueling_MovedToAnotherTank_ReturnsFuelToTheOldOne()
    {
        var (vehicle, first) = await CreateVehicleAndTankAsync(1000);
        var second = new FuelTank { Name = "Резервуар 2", FuelType = FuelType.Diesel, CapacityLiters = 5000, CurrentLiters = 2000 };
        await _tanks.SaveAsync(second);
        var refueling = NewRefueling(vehicle, first, 300);
        await _refuelings.SaveAsync(refueling);

        refueling.TankId = second.Id;
        await _refuelings.SaveAsync(refueling);

        (await _tanks.GetByIdAsync(first.Id))!.CurrentLiters.Should().Be(1000);
        (await _tanks.GetByIdAsync(second.Id))!.CurrentLiters.Should().Be(1700);
    }

    [Fact]
    public async Task Refueling_MoreThanTheTankHolds_IsRejectedAndNothingChanges()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync(100);

        var act = () => _refuelings.SaveAsync(NewRefueling(vehicle, tank, 150));

        var error = await act.Should().ThrowAsync<InsufficientFuelException>();
        error.Which.AvailableLiters.Should().Be(100);
        (await _tanks.GetByIdAsync(tank.Id))!.CurrentLiters.Should().Be(100);
        (await _refuelings.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Refueling_Delete_ReturnsTheFuelToTheTank()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync(1000);
        var refueling = NewRefueling(vehicle, tank, 250);
        await _refuelings.SaveAsync(refueling);

        var rows = await _refuelings.DeleteAsync(refueling);

        rows.Should().Be(1);
        (await _tanks.GetByIdAsync(tank.Id))!.CurrentLiters.Should().Be(1000);
        (await _refuelings.GetByIdAsync(refueling.Id)).Should().BeNull();
    }

    [Fact]
    public async Task Refueling_Delete_UnknownRecord_ChangesNothing()
    {
        var (_, tank) = await CreateVehicleAndTankAsync(1000);

        var rows = await _refuelings.DeleteAsync(new Refueling { Id = 999 });

        rows.Should().Be(0);
        (await _tanks.GetByIdAsync(tank.Id))!.CurrentLiters.Should().Be(1000);
    }

    [Fact]
    public async Task EveryChange_IsAddedToTheOfflineSyncQueue()
    {
        var (vehicle, tank) = await CreateVehicleAndTankAsync(1000);
        var refueling = NewRefueling(vehicle, tank, 100);
        await _refuelings.SaveAsync(refueling);
        await _refuelings.DeleteAsync(refueling);

        var queue = await QueueAsync();

        queue.Select(q => $"{q.EntityType}:{q.Operation}").Should().ContainInOrder(
            "Vehicle:Insert", "FuelTank:Insert", "FuelTank:Update", "Refueling:Insert", "FuelTank:Update", "Refueling:Delete");
        queue.Where(q => q.EntityType == "Refueling" && q.Operation == "Insert").Single()
            .PayloadJson.Should().NotContain("Коваленко");
    }

    [Fact]
    public async Task FreshDatabase_HasTheCurrentSchemaVersion()
    {
        (await _database.GetSchemaVersionAsync()).Should().Be(AppDatabase.SchemaVersion);
    }

    [Fact]
    public async Task OldDatabase_IsMigratedFromVersion1To2WithoutLosingData()
    {
        var oldPath = Path.Combine(Path.GetTempPath(), $"fuel_v1_{Guid.NewGuid():N}.db3");
        try
        {
            // база версії 1: у таблиці Refuelings ще немає стовпця Notes
            var raw = new SQLiteConnection(oldPath);
            raw.Execute("CREATE TABLE Refuelings (Id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, VehicleId INTEGER, TankId INTEGER, " +
                        "DriverNameEncrypted TEXT, Liters REAL, PricePerLiter REAL, RefueledAt INTEGER, ReceiptPhotoPath TEXT, " +
                        "Latitude REAL, Longitude REAL, OperatorName TEXT, UpdatedAt INTEGER)");
            raw.Execute("INSERT INTO Refuelings (VehicleId, TankId, DriverNameEncrypted, Liters, PricePerLiter, RefueledAt, OperatorName, UpdatedAt) " +
                        "VALUES (1, 1, 'cipher', 42.5, 50, 0, 'operator', 0)");
            raw.Execute("PRAGMA user_version = 1");
            raw.Close();

            var migrated = new AppDatabase(oldPath);
            var db = await migrated.GetConnectionAsync();

            (await migrated.GetSchemaVersionAsync()).Should().Be(2);
            var columns = await db.GetTableInfoAsync("Refuelings");
            columns.Select(c => c.Name).Should().Contain("Notes");

            var rows = await db.Table<RefuelingRecord>().ToListAsync();
            rows.Should().ContainSingle();
            rows[0].Liters.Should().Be(42.5);
            rows[0].Notes.Should().BeEmpty();

            await migrated.CloseAsync();
        }
        finally
        {
            File.Delete(oldPath);
        }
    }

    [Fact]
    public async Task Seeder_CreatesDemoDataOnceWithConsistentLevelsAndNoSyncNoise()
    {
        var seeder = new FuelDataSeeder(_database, _tanks, _vehicles, _refuelings, NullLogger<FuelDataSeeder>.Instance);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        (await _tanks.GetAllAsync()).Should().HaveCount(3);
        (await _vehicles.GetAllAsync()).Should().HaveCount(4);
        (await _refuelings.GetAllAsync()).Should().HaveCount(5);
        (await _tanks.GetAllAsync()).Single(t => t.FuelType == FuelType.Petrol95).CurrentLiters.Should().Be(24000 - 40 - 35);
        (await QueueAsync()).Should().BeEmpty();
    }
}

public class FuelModelsTests
{
    [Theory]
    [InlineData(10000, 8000, TankLevelState.Normal)]
    [InlineData(10000, 2400, TankLevelState.Low)]
    [InlineData(10000, 900, TankLevelState.Critical)]
    [InlineData(0, 0, TankLevelState.Critical)]
    public void FuelTank_LevelState_FollowsTheFillPercentage(double capacity, double current, TankLevelState expected)
    {
        var tank = new FuelTank { CapacityLiters = capacity, CurrentLiters = current };

        tank.LevelState.Should().Be(expected);
        tank.FillFraction.Should().BeInRange(0, 1);
    }

    [Fact]
    public void Refueling_Total_IsLitersTimesPriceRoundedToKopecks()
    {
        var refueling = new Refueling { Liters = 33.333, PricePerLiter = 53.49 };

        refueling.Total.Should().Be(1782.98);
    }

    [Fact]
    public void Refueling_LocationText_DependsOnCoordinates()
    {
        new Refueling().HasLocation.Should().BeFalse();
        new Refueling { Latitude = 1, Longitude = 2 }.HasLocation.Should().BeTrue();
    }

    [Theory]
    [InlineData("  аа1234кк ", "АА1234КК")]
    [InlineData("ka9012bb", "KA9012BB")]
    [InlineData(null, "")]
    public void Vehicle_NormalizePlate_TrimsAndUppercases(string? input, string expected)
    {
        Vehicle.NormalizePlate(input).Should().Be(expected);
    }
}
