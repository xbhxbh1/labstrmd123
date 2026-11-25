using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class RefuelingDetailViewModelTests
{
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<IMapLauncher> _map = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private RefuelingDetailViewModel CreateSut() =>
        new(_session.Object, _map.Object, _nav.Object, _dialogs.Object, NullLogger<RefuelingDetailViewModel>.Instance);

    [Theory]
    [InlineData(UserRole.StationOperator, true)]
    [InlineData(UserRole.Accountant, false)]
    public void CanEdit_OnlyForTheOperator(UserRole role, bool expected)
    {
        _session.Setup(s => s.IsInRole(It.IsAny<UserRole>())).Returns((UserRole r) => r == role);

        CreateSut().CanEdit.Should().Be(expected);
    }

    [Fact]
    public async Task Edit_AsOperator_NavigatesToTheFormWithTheRecord()
    {
        _session.Setup(s => s.IsInRole(UserRole.StationOperator)).Returns(true);
        IDictionary<string, object>? passed = null;
        _nav.Setup(n => n.GoToAsync("refueling/edit", It.IsAny<IDictionary<string, object>?>()))
            .Callback<string, IDictionary<string, object>?>((_, p) => passed = p).Returns(Task.CompletedTask);
        var refueling = new Refueling { Id = 5 };
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object> { ["SelectedRefueling"] = refueling });

        await vm.EditCommand.ExecuteAsync(null);

        passed!["SelectedRefueling"].Should().BeSameAs(refueling);
    }

    [Fact]
    public async Task Edit_AsAccountant_IsDenied()
    {
        _session.Setup(s => s.IsInRole(It.IsAny<UserRole>())).Returns(false);
        var vm = CreateSut();

        await vm.EditCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>?>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void ApplyQuery_SavedRefueling_ReplacesTheDisplayedRecord()
    {
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object> { ["SelectedRefueling"] = new Refueling { Id = 1, Liters = 10 } });

        vm.ApplyQuery(new Dictionary<string, object> { ["SavedRefueling"] = new Refueling { Id = 1, Liters = 25 } });

        vm.Refueling.Liters.Should().Be(25);
    }

    [Fact]
    public async Task OpenOnMap_WithCoordinates_OpensTheExternalMap()
    {
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object>
        {
            ["SelectedRefueling"] = new Refueling { VehiclePlate = "АА1234КК", Latitude = 50.1, Longitude = 30.2 }
        });

        await vm.OpenOnMapCommand.ExecuteAsync(null);

        _map.Verify(m => m.OpenAsync(50.1, 30.2, It.Is<string>(s => s.Contains("АА1234КК"))), Times.Once);
    }

    [Fact]
    public async Task OpenOnMap_WithoutCoordinates_ShowsAnAlert()
    {
        var vm = CreateSut();

        await vm.OpenOnMapCommand.ExecuteAsync(null);

        _map.Verify(m => m.OpenAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Немає координат", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OpenOnMap_WhenTheMapAppFails_ShowsAnAlert()
    {
        _map.Setup(m => m.OpenAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>())).ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object>
        {
            ["SelectedRefueling"] = new Refueling { Latitude = 1, Longitude = 2 }
        });

        await vm.OpenOnMapCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Карти недоступні", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}

public class TanksViewModelTests
{
    [Fact]
    public async Task Load_ShowsTanksAndCountsTheOnesThatNeedRefilling()
    {
        var repo = new Mock<ITankRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<FuelTank>
        {
            new() { Name = "A", CapacityLiters = 1000, CurrentLiters = 900 },
            new() { Name = "B", CapacityLiters = 1000, CurrentLiters = 200 },
            new() { Name = "C", CapacityLiters = 1000, CurrentLiters = 50 }
        });
        var seeder = new Mock<IFuelDataSeeder>();
        var vm = new TanksViewModel(repo.Object, seeder.Object, new Mock<INavigationService>().Object,
            NullLogger<TanksViewModel>.Instance);

        await vm.LoadCommand.ExecuteAsync(null);

        seeder.Verify(s => s.SeedAsync(), Times.Once);
        vm.Tanks.Should().HaveCount(3);
        vm.Status.Should().Contain("потребують поповнення: 2");
    }

    [Fact]
    public async Task Load_WhenTheRepositoryFails_ShowsAStatusInsteadOfCrashing()
    {
        var repo = new Mock<ITankRepository>();
        repo.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException());
        var vm = new TanksViewModel(repo.Object, new Mock<IFuelDataSeeder>().Object, new Mock<INavigationService>().Object,
            NullLogger<TanksViewModel>.Instance);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("Не вдалося");
    }
}

public class VehiclesViewModelTests
{
    private readonly Mock<IVehicleRepository> _repo = new();

    private VehiclesViewModel CreateSut()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Vehicle>());
        return new VehiclesViewModel(_repo.Object, new Mock<IFuelDataSeeder>().Object,
            new Mock<INavigationService>().Object, NullLogger<VehiclesViewModel>.Instance);
    }

    [Theory]
    [InlineData("", "MAN", false)]
    [InlineData("AB1", "MAN", false)]
    [InlineData("АА1234КК", "", false)]
    [InlineData("АА1234КК", "MAN", true)]
    public void AddVehicleCommand_CanExecute_RequiresPlateAndModel(string plate, string model, bool expected)
    {
        var vm = CreateSut();

        vm.NewPlate = plate;
        vm.NewModel = model;

        vm.AddVehicleCommand.CanExecute(null).Should().Be(expected);
    }

    [Fact]
    public async Task AddVehicle_SavesItAndClearsTheForm()
    {
        Vehicle? saved = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Vehicle>())).Callback<Vehicle>(v => saved = v).ReturnsAsync(1);
        var vm = CreateSut();
        vm.NewPlate = "ка9012вв";
        vm.NewModel = "Sprinter";
        vm.NewFuelType = vm.FuelTypes.Single(f => f.Type == FuelType.Diesel);

        await vm.AddVehicleCommand.ExecuteAsync(null);

        saved!.PlateNumber.Should().Be("ка9012вв");
        saved.FuelType.Should().Be(FuelType.Diesel);
        vm.NewPlate.Should().BeEmpty();
        vm.NewModel.Should().BeEmpty();
        vm.Status.Should().Contain("Додано");
    }

    [Fact]
    public async Task AddVehicle_Duplicate_ShowsTheMessageFromTheRepository()
    {
        _repo.Setup(r => r.SaveAsync(It.IsAny<Vehicle>())).ThrowsAsync(new DuplicateVehicleException("АА1234КК"));
        var vm = CreateSut();
        vm.NewPlate = "АА1234КК";
        vm.NewModel = "MAN";

        await vm.AddVehicleCommand.ExecuteAsync(null);

        vm.Status.Should().Contain("АА1234КК").And.Contain("уже");
    }
}

public class ReportViewModelTests
{
    private readonly Mock<IRefuelingRepository> _repo = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private ReportViewModel CreateSut() =>
        new(_repo.Object, _session.Object, _nav.Object, _dialogs.Object, NullLogger<ReportViewModel>.Instance);

    [Fact]
    public async Task Load_AsAccountant_GroupsTheTotalsByFuelType()
    {
        _session.Setup(s => s.IsInRole(UserRole.Accountant)).Returns(true);
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Refueling>
        {
            new() { FuelType = FuelType.Diesel, Liters = 100, PricePerLiter = 50 },
            new() { FuelType = FuelType.Diesel, Liters = 50, PricePerLiter = 50 },
            new() { FuelType = FuelType.Petrol95, Liters = 40, PricePerLiter = 50 }
        });
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Lines.Should().HaveCount(2);
        var diesel = vm.Lines.Single(l => l.FuelName == FuelType.Diesel.ToDisplayName());
        diesel.Count.Should().Be(2);
        diesel.Liters.Should().Be(150);
        diesel.Amount.Should().Be(7500);
        vm.TotalText.Should().Contain("заправок 3");
    }

    [Fact]
    public async Task Load_AsOperator_IsDeniedAndGoesBack()
    {
        _session.Setup(s => s.IsInRole(It.IsAny<UserRole>())).Returns(false);
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _repo.Verify(r => r.GetAllAsync(), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _nav.Verify(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }
}
