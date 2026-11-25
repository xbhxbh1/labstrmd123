using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class RefuelingEditViewModelTests
{
    private readonly Mock<IRefuelingRepository> _refuelings = new();
    private readonly Mock<IVehicleRepository> _vehicles = new();
    private readonly Mock<ITankRepository> _tanks = new();
    private readonly Mock<IPhotoService> _photos = new();
    private readonly Mock<IDeviceLocationService> _location = new();
    private readonly Mock<IAppStateService> _state = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private readonly Vehicle _truck = new() { Id = 1, PlateNumber = "АА1234КК", Model = "MAN", FuelType = FuelType.Diesel };
    private readonly Vehicle _car = new() { Id = 2, PlateNumber = "АЕ5678ІН", Model = "Škoda", FuelType = FuelType.Petrol95 };
    private readonly FuelTank _dieselTank = new() { Id = 10, Name = "Резервуар №2", FuelType = FuelType.Diesel, CapacityLiters = 40000, CurrentLiters = 5000 };
    private readonly FuelTank _petrolTank = new() { Id = 11, Name = "Резервуар №1", FuelType = FuelType.Petrol95, CapacityLiters = 30000, CurrentLiters = 20000 };
    private readonly FuelTank _oilTank = new() { Id = 12, Name = "Ємність мастил", FuelType = FuelType.Lubricant, CapacityLiters = 2000, CurrentLiters = 150 };

    public RefuelingEditViewModelTests()
    {
        _vehicles.Setup(v => v.GetAllAsync()).ReturnsAsync(new List<Vehicle> { _truck, _car });
        _tanks.Setup(t => t.GetAllAsync()).ReturnsAsync(new List<FuelTank> { _dieselTank, _petrolTank, _oilTank });
        _refuelings.Setup(r => r.SaveAsync(It.IsAny<Refueling>())).ReturnsAsync(1);
        _state.SetupProperty(s => s.Draft, null);
        _session.Setup(s => s.UserName).Returns("operator");
        _session.Setup(s => s.IsInRole(UserRole.StationOperator)).Returns(true);
        _session.Setup(s => s.CurrentRole).Returns(UserRole.StationOperator);
    }

    private RefuelingEditViewModel CreateSut() =>
        new(_refuelings.Object, _vehicles.Object, _tanks.Object, _photos.Object, _location.Object, _state.Object,
            _session.Object, _nav.Object, _dialogs.Object, NullLogger<RefuelingEditViewModel>.Instance);

    private async Task<RefuelingEditViewModel> LoadedSutAsync()
    {
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);
        return vm;
    }

    private static void FillValidForm(RefuelingEditViewModel vm, Vehicle vehicle, FuelTank tank, string liters = "100")
    {
        vm.SelectedVehicle = vehicle;
        vm.SelectedTank = tank;
        vm.DriverName = "Коваленко Іван";
        vm.LitersText = liters;
        vm.PriceText = "51,9";
    }

    [Fact]
    public async Task Load_FillsTheVehicleAndTankPickers()
    {
        var vm = await LoadedSutAsync();

        vm.Vehicles.Should().HaveCount(2);
        vm.Tanks.Should().HaveCount(3);
    }

    [Fact]
    public async Task SelectingATank_PrefillsThePriceForItsFuelType()
    {
        var vm = await LoadedSutAsync();

        vm.SelectedTank = _petrolTank;

        vm.PriceText.Should().Be("53.50");
    }

    [Fact]
    public async Task SelectingATank_DoesNotOverwriteAPriceTheOperatorTyped()
    {
        var vm = await LoadedSutAsync();
        vm.PriceText = "60";

        vm.SelectedTank = _petrolTank;

        vm.PriceText.Should().Be("60");
    }

    [Theory]
    [InlineData("", "Іваненко", "100", "50", false)]
    [InlineData("АА1234КК", "", "100", "50", false)]
    [InlineData("АА1234КК", "Іваненко", "0", "50", false)]
    [InlineData("АА1234КК", "Іваненко", "abc", "50", false)]
    [InlineData("АА1234КК", "Іваненко", "100", "0", false)]
    [InlineData("АА1234КК", "Іваненко", "100", "51,9", true)]
    [InlineData("АА1234КК", "Іваненко", "100.5", "51.9", true)]
    public async Task SaveCommand_CanExecute_DependsOnTheFormFields(string plate, string driver, string liters, string price, bool expected)
    {
        var vm = await LoadedSutAsync();
        vm.SelectedVehicle = plate.Length == 0 ? null : _truck;
        vm.SelectedTank = _dieselTank;
        vm.DriverName = driver;
        vm.LitersText = liters;
        vm.PriceText = price;

        vm.SaveCommand.CanExecute(null).Should().Be(expected);
    }

    [Fact]
    public async Task Validation_MoreLitersThanTheTankHolds_ShowsTheAvailableAmount()
    {
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank, "6000");

        vm.SaveCommand.CanExecute(null).Should().BeFalse();
        vm.ValidationMessage.Should().Contain("лише").And.Contain("5");
    }

    [Fact]
    public async Task Validation_VehicleFuelDoesNotMatchTheTank_IsRejected()
    {
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _car, _dieselTank, "10");

        vm.SaveCommand.CanExecute(null).Should().BeFalse();
        vm.ValidationMessage.Should().Contain("працює на").And.Contain("в резервуарі");
    }

    [Fact]
    public async Task Validation_LubricantTankFitsAnyVehicle()
    {
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _car, _oilTank, "10");

        vm.SaveCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task TotalText_ShowsLitersTimesPrice()
    {
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank, "100");
        vm.PriceText = "50";

        vm.TotalText.Should().Contain("5").And.Contain("000");
    }

    [Fact]
    public async Task Save_AsOperator_StoresTheRefuelingAndReturnsItToTheList()
    {
        Refueling? saved = null;
        _refuelings.Setup(r => r.SaveAsync(It.IsAny<Refueling>())).Callback<Refueling>(r => saved = r).ReturnsAsync(1);
        IDictionary<string, object>? back = null;
        _nav.Setup(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()))
            .Callback<IDictionary<string, object>?>(p => back = p).Returns(Task.CompletedTask);
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank, "120,5");
        vm.ReceiptPhotoPath = @"C:\photos\receipt.jpg";
        vm.Latitude = 50.45;
        vm.Longitude = 30.52;
        vm.Notes = " картка ";

        await vm.SaveCommand.ExecuteAsync(null);

        saved.Should().NotBeNull();
        saved!.VehicleId.Should().Be(1);
        saved.TankId.Should().Be(10);
        saved.DriverName.Should().Be("Коваленко Іван");
        saved.Liters.Should().Be(120.5);
        saved.PricePerLiter.Should().Be(51.9);
        saved.OperatorName.Should().Be("operator");
        saved.ReceiptPhotoPath.Should().Be(@"C:\photos\receipt.jpg");
        saved.Notes.Should().Be("картка");
        back!["SavedRefueling"].Should().BeSameAs(saved);
        _state.Object.Draft.Should().BeNull();
    }

    [Fact]
    public async Task Save_AsAccountant_IsDeniedInTheCommand()
    {
        _session.Setup(s => s.IsInRole(UserRole.StationOperator)).Returns(false);
        _session.Setup(s => s.CurrentRole).Returns(UserRole.Accountant);
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank);

        await vm.SaveCommand.ExecuteAsync(null);

        _refuelings.Verify(r => r.SaveAsync(It.IsAny<Refueling>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Save_WhenTheRepositoryReportsInsufficientFuel_ExplainsAndStaysOnTheForm()
    {
        _refuelings.Setup(r => r.SaveAsync(It.IsAny<Refueling>()))
            .ThrowsAsync(new InsufficientFuelException("Резервуар №2", 50, 100));
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank);

        await vm.SaveCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Недостатньо палива", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _nav.Verify(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()), Times.Never);
    }

    [Fact]
    public async Task Save_WhenTheRepositoryFails_ShowsAnAlert()
    {
        _refuelings.Setup(r => r.SaveAsync(It.IsAny<Refueling>())).ThrowsAsync(new InvalidOperationException());
        var vm = await LoadedSutAsync();
        FillValidForm(vm, _truck, _dieselTank);

        await vm.SaveCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Помилка", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task EditingFields_StoresADraftInTheAppState()
    {
        var vm = await LoadedSutAsync();

        vm.SelectedVehicle = _truck;
        vm.DriverName = "Чернетка";
        vm.LitersText = "25";

        _state.Object.Draft.Should().NotBeNull();
        _state.Object.Draft!.VehicleId.Should().Be(1);
        _state.Object.Draft.DriverName.Should().Be("Чернетка");
        _state.Object.Draft.LitersText.Should().Be("25");
    }

    [Fact]
    public async Task Load_WithAStoredDraft_RestoresTheForm()
    {
        _state.Object.Draft = new RefuelingDraft
        {
            VehicleId = 2,
            TankId = 11,
            DriverName = "Незавершений",
            LitersText = "33",
            PriceText = "54",
            Notes = "нотатка",
            Latitude = 1.5,
            Longitude = 2.5
        };

        var vm = await LoadedSutAsync();

        vm.SelectedVehicle.Should().BeSameAs(_car);
        vm.SelectedTank.Should().BeSameAs(_petrolTank);
        vm.DriverName.Should().Be("Незавершений");
        vm.LitersText.Should().Be("33");
        vm.PriceText.Should().Be("54");
        vm.HasLocation.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_DiscardsTheDraftAndGoesBack()
    {
        var vm = await LoadedSutAsync();
        vm.DriverName = "Щось";

        await vm.CancelCommand.ExecuteAsync(null);

        _state.Object.Draft.Should().BeNull();
        _nav.Verify(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task Editing_ExistingRefueling_FillsTheFormAndKeepsItsIdentity()
    {
        var existing = new Refueling
        {
            Id = 77, VehicleId = 1, TankId = 10, DriverName = "Коваленко Іван", Liters = 200, PricePerLiter = 51.9,
            RefueledAt = new DateTime(2026, 11, 20), OperatorName = "operator", Notes = "стара"
        };
        Refueling? saved = null;
        _refuelings.Setup(r => r.SaveAsync(It.IsAny<Refueling>())).Callback<Refueling>(r => saved = r).ReturnsAsync(1);
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object> { ["SelectedRefueling"] = existing });
        await vm.LoadCommand.ExecuteAsync(null);

        vm.Title.Should().Contain("Редагування");
        vm.SelectedVehicle.Should().BeSameAs(_truck);
        vm.LitersText.Should().Be("200");
        vm.AvailableLiters.Should().Be(5200);

        vm.LitersText = "250";
        await vm.SaveCommand.ExecuteAsync(null);

        saved!.Id.Should().Be(77);
        saved.RefueledAt.Should().Be(new DateTime(2026, 11, 20));
        saved.Liters.Should().Be(250);
        _state.Object.Draft.Should().BeNull("форма редагування не створює чернетки нової заправки");
    }

    [Fact]
    public async Task TakePhoto_Success_StoresThePath()
    {
        _photos.Setup(p => p.TakePhotoAsync(It.IsAny<string>())).ReturnsAsync(new PhotoResult(PhotoStatus.Success, @"C:\p\a.jpg"));
        var vm = await LoadedSutAsync();

        await vm.TakePhotoCommand.ExecuteAsync(null);

        vm.ReceiptPhotoPath.Should().Be(@"C:\p\a.jpg");
        vm.HasPhoto.Should().BeTrue();
    }

    [Theory]
    [InlineData(PhotoStatus.PermissionDenied, "Немає дозволу")]
    [InlineData(PhotoStatus.NotSupported, "Камера недоступна")]
    [InlineData(PhotoStatus.Failed, "Помилка")]
    public async Task TakePhoto_Failure_ExplainsTheProblem(PhotoStatus status, string expectedTitle)
    {
        _photos.Setup(p => p.TakePhotoAsync(It.IsAny<string>())).ReturnsAsync(new PhotoResult(status));
        var vm = await LoadedSutAsync();

        await vm.TakePhotoCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(expectedTitle, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task TakePhoto_CancelledByTheUser_ShowsNoAlert()
    {
        _photos.Setup(p => p.TakePhotoAsync(It.IsAny<string>())).ReturnsAsync(new PhotoResult(PhotoStatus.Cancelled));
        var vm = await LoadedSutAsync();

        await vm.TakePhotoCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetLocation_Success_StoresTheCoordinates()
    {
        _location.Setup(l => l.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationResult(LocationStatus.Success, 50.4501, 30.5234));
        var vm = await LoadedSutAsync();

        await vm.GetLocationCommand.ExecuteAsync(null);

        vm.HasLocation.Should().BeTrue();
        vm.LocationText.Should().MatchRegex("50[,.]45010");
    }

    [Theory]
    [InlineData(LocationStatus.PermissionDenied, "Немає дозволу")]
    [InlineData(LocationStatus.FeatureDisabled, "Геолокацію вимкнено")]
    [InlineData(LocationStatus.NotSupported, "Геолокація недоступна")]
    [InlineData(LocationStatus.Unavailable, "Координати не отримано")]
    public async Task GetLocation_Failure_ExplainsTheProblem(LocationStatus status, string expectedTitle)
    {
        _location.Setup(l => l.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new LocationResult(status));
        var vm = await LoadedSutAsync();

        await vm.GetLocationCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(expectedTitle, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.HasLocation.Should().BeFalse();
    }

    [Fact]
    public async Task Load_WhenTheReferenceDataFails_ShowsAnAlert()
    {
        _vehicles.Setup(v => v.GetAllAsync()).ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Помилка", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
