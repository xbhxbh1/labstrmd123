using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class InspectionEditViewModelTests
{
    private readonly Mock<IInspectionRepository> _repo = new();
    private readonly Mock<IPhotoService> _photos = new();
    private readonly Mock<IDeviceLocationService> _location = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private InspectionEditViewModel CreateSut() =>
        new(_repo.Object, _photos.Object, _location.Object, _nav.Object, _dialogs.Object,
            NullLogger<InspectionEditViewModel>.Instance);

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("101", true)]
    public void SaveCommand_CanExecute_RequiresRoomNumber(string room, bool expected)
    {
        var vm = CreateSut();

        vm.RoomNumber = room;

        vm.SaveCommand.CanExecute(null).Should().Be(expected);
    }

    [Fact]
    public async Task Save_StoresInspectionWithPhotoAndCoordinatesAndGoesBack()
    {
        RoomInspection? saved = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<RoomInspection>()))
            .Callback<RoomInspection>(i => saved = i)
            .ReturnsAsync(1);
        var vm = CreateSut();
        vm.RoomNumber = " 205 ";
        vm.Description = " Тріщина на стіні ";
        vm.PhotoPath = @"C:\photos\room_205.jpg";
        vm.Latitude = 50.45;
        vm.Longitude = 30.52;

        await vm.SaveCommand.ExecuteAsync(null);

        saved.Should().NotBeNull();
        saved!.RoomNumber.Should().Be("205");
        saved.Description.Should().Be("Тріщина на стіні");
        saved.PhotoPath.Should().Be(@"C:\photos\room_205.jpg");
        saved.Latitude.Should().Be(50.45);
        saved.Longitude.Should().Be(30.52);
        _nav.Verify(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task Save_WhenRepositoryThrows_ShowsAlertAndStaysOnPage()
    {
        _repo.Setup(r => r.SaveAsync(It.IsAny<RoomInspection>())).ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();
        vm.RoomNumber = "101";

        await vm.SaveCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Помилка", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _nav.Verify(n => n.GoBackAsync(It.IsAny<IDictionary<string, object>?>()), Times.Never);
    }

    [Fact]
    public async Task TakePhoto_Success_ShowsThumbnailPath()
    {
        _photos.Setup(p => p.TakePhotoAsync("101")).ReturnsAsync(new PhotoResult(PhotoStatus.Success, @"C:\p\a.jpg"));
        var vm = CreateSut();
        vm.RoomNumber = "101";

        await vm.TakePhotoCommand.ExecuteAsync(null);

        vm.PhotoPath.Should().Be(@"C:\p\a.jpg");
        vm.HasPhoto.Should().BeTrue();
    }

    [Theory]
    [InlineData(PhotoStatus.PermissionDenied, "Немає дозволу")]
    [InlineData(PhotoStatus.NotSupported, "Камера недоступна")]
    [InlineData(PhotoStatus.Failed, "Помилка")]
    public async Task TakePhoto_Failure_ExplainsTheProblemToTheUser(PhotoStatus status, string expectedTitle)
    {
        _photos.Setup(p => p.TakePhotoAsync(It.IsAny<string>())).ReturnsAsync(new PhotoResult(status));
        var vm = CreateSut();

        await vm.TakePhotoCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(expectedTitle, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task TakePhoto_CancelledByUser_ShowsNoAlert()
    {
        _photos.Setup(p => p.TakePhotoAsync(It.IsAny<string>())).ReturnsAsync(new PhotoResult(PhotoStatus.Cancelled));
        var vm = CreateSut();

        await vm.TakePhotoCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetLocation_Success_StoresCoordinates()
    {
        _location.Setup(l => l.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocationResult(LocationStatus.Success, 50.4501, 30.5234));
        var vm = CreateSut();

        await vm.GetLocationCommand.ExecuteAsync(null);

        vm.HasLocation.Should().BeTrue();
        vm.Latitude.Should().Be(50.4501);
        vm.LocationText.Should().MatchRegex("50[,.]45010");
    }

    [Theory]
    [InlineData(LocationStatus.PermissionDenied, "Немає дозволу")]
    [InlineData(LocationStatus.FeatureDisabled, "Геолокацію вимкнено")]
    [InlineData(LocationStatus.NotSupported, "Геолокація недоступна")]
    [InlineData(LocationStatus.Unavailable, "Координати не отримано")]
    public async Task GetLocation_Failure_ExplainsTheProblemToTheUser(LocationStatus status, string expectedTitle)
    {
        _location.Setup(l => l.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new LocationResult(status));
        var vm = CreateSut();

        await vm.GetLocationCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync(expectedTitle, It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.HasLocation.Should().BeFalse();
    }

    [Fact]
    public void ApplyQuery_IncidentFlag_ChangesTitleAndPrefillsDescription()
    {
        var vm = CreateSut();

        vm.ApplyQuery(new Dictionary<string, object> { ["IsIncident"] = true });

        vm.Title.Should().Be("Повідомлення про інцидент");
        vm.Description.Should().StartWith("ІНЦИДЕНТ");
    }
}

public class InspectionsViewModelTests
{
    private readonly Mock<IInspectionRepository> _repo = new();
    private readonly Mock<IMapLauncher> _map = new();
    private readonly Mock<INetworkStatusService> _network = new();
    private readonly Mock<ISyncService> _sync = new();
    private readonly Mock<IShakeDetectionService> _shake = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();

    public InspectionsViewModelTests()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RoomInspection>());
        _sync.Setup(s => s.GetPendingCountAsync()).ReturnsAsync(2);
        _network.Setup(n => n.IsOnline).Returns(true);
        _map.Setup(m => m.SupportsEmbeddedMap).Returns(true);
    }

    private InspectionsViewModel CreateSut() =>
        new(_repo.Object, _map.Object, _network.Object, _sync.Object, _shake.Object, _nav.Object,
            _dialogs.Object, TestHelpers.SynchronousMainThread().Object, NullLogger<InspectionsViewModel>.Instance);

    [Fact]
    public async Task OnAppearing_StartsSensorLoadsListAndShowsQueueSize()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RoomInspection>
        {
            new() { Id = 1, RoomNumber = "101" },
            new() { Id = 2, RoomNumber = "102" }
        });
        var vm = CreateSut();

        await vm.OnAppearingAsync();

        _shake.Verify(s => s.Start(), Times.Once);
        vm.Inspections.Should().HaveCount(2);
        vm.PendingCount.Should().Be(2);
        vm.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task OnDisappearing_StopsTheSensor()
    {
        var vm = CreateSut();
        await vm.OnAppearingAsync();

        vm.OnDisappearing();

        _shake.Verify(s => s.Stop(), Times.Once);
    }

    [Fact]
    public async Task Shake_NavigatesToIncidentForm()
    {
        IDictionary<string, object>? passed = null;
        _nav.Setup(n => n.GoToAsync("incident/new", It.IsAny<IDictionary<string, object>?>()))
            .Callback<string, IDictionary<string, object>?>((_, p) => passed = p)
            .Returns(Task.CompletedTask);
        var vm = CreateSut();
        await vm.OnAppearingAsync();

        _shake.Raise(s => s.ShakeDetected += null, EventArgs.Empty);

        passed.Should().NotBeNull();
        passed!["IsIncident"].Should().Be(true);
    }

    [Fact]
    public async Task Shake_AfterPageDisappeared_IsIgnored()
    {
        var vm = CreateSut();
        await vm.OnAppearingAsync();
        vm.OnDisappearing();

        _shake.Raise(s => s.ShakeDetected += null, EventArgs.Empty);

        _nav.Verify(n => n.GoToAsync("incident/new", It.IsAny<IDictionary<string, object>?>()), Times.Never);
    }

    [Fact]
    public async Task NetworkChange_UpdatesIndicator()
    {
        var vm = CreateSut();
        await vm.OnAppearingAsync();

        _network.Raise(n => n.OnlineChanged += null, _network.Object, false);

        vm.IsOnline.Should().BeFalse();
        vm.NetworkText.Should().Contain("Офлайн");
    }

    [Fact]
    public async Task QueueChange_RefreshesPendingCounter()
    {
        var vm = CreateSut();
        await vm.OnAppearingAsync();
        _sync.Setup(s => s.GetPendingCountAsync()).ReturnsAsync(0);

        _sync.Raise(s => s.QueueChanged += null, EventArgs.Empty);

        vm.PendingCount.Should().Be(0);
    }

    [Fact]
    public async Task OpenMap_WithoutEmbeddedMap_ExplainsTheAlternative()
    {
        _map.Setup(m => m.SupportsEmbeddedMap).Returns(false);
        var vm = CreateSut();

        await vm.OpenMapCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Карта", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _nav.Verify(n => n.GoToAsync("inspections/map", It.IsAny<IDictionary<string, object>?>()), Times.Never);
    }

    [Fact]
    public async Task OpenMap_WithEmbeddedMap_NavigatesToMapPage()
    {
        var vm = CreateSut();

        await vm.OpenMapCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("inspections/map", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task ShowOnMap_WithCoordinates_OpensExternalMap()
    {
        var vm = CreateSut();

        await vm.ShowOnMapCommand.ExecuteAsync(new RoomInspection { RoomNumber = "101", Latitude = 50.1, Longitude = 30.2 });

        _map.Verify(m => m.OpenAsync(50.1, 30.2, It.Is<string>(s => s.Contains("101"))), Times.Once);
    }

    [Fact]
    public async Task ShowOnMap_WithoutCoordinates_ShowsAlert()
    {
        var vm = CreateSut();

        await vm.ShowOnMapCommand.ExecuteAsync(new RoomInspection { RoomNumber = "101" });

        _map.Verify(m => m.OpenAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Немає координат", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task OpenDetails_PassesTheSelectedInspection()
    {
        IDictionary<string, object>? passed = null;
        _nav.Setup(n => n.GoToAsync("inspection/detail", It.IsAny<IDictionary<string, object>?>()))
            .Callback<string, IDictionary<string, object>?>((_, p) => passed = p)
            .Returns(Task.CompletedTask);
        var inspection = new RoomInspection { Id = 9 };
        var vm = CreateSut();

        await vm.OpenDetailsCommand.ExecuteAsync(inspection);

        passed!["SelectedInspection"].Should().BeSameAs(inspection);
    }

    [Fact]
    public void ShowShakeSimulation_OnlyWithoutAccelerometer()
    {
        _shake.Setup(s => s.IsSupported).Returns(false);

        CreateSut().ShowShakeSimulation.Should().BeTrue();
    }
}

public class InspectionsMapViewModelTests
{
    [Fact]
    public async Task Load_AddsPinsOnlyForInspectionsWithCoordinates()
    {
        var repo = new Mock<IInspectionRepository>();
        repo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RoomInspection>
        {
            new() { RoomNumber = "101", Latitude = 50.0, Longitude = 30.0, Description = "a" },
            new() { RoomNumber = "102" }
        });
        var permissions = new Mock<IPermissionService>();
        var vm = new InspectionsMapViewModel(repo.Object, permissions.Object, new Mock<INavigationService>().Object,
            NullLogger<InspectionsMapViewModel>.Instance);

        await vm.LoadCommand.ExecuteAsync(null);

        vm.Pins.Should().ContainSingle().Which.RoomNumber.Should().Be("101");
        permissions.Verify(p => p.EnsureAsync(DevicePermission.Location), Times.Once);
    }
}

public class InspectionDetailViewModelTests
{
    private readonly Mock<IMapLauncher> _map = new();
    private readonly Mock<IDialogService> _dialogs = new();

    private InspectionDetailViewModel CreateSut() =>
        new(_map.Object, new Mock<INavigationService>().Object, _dialogs.Object,
            NullLogger<InspectionDetailViewModel>.Instance);

    [Fact]
    public async Task OpenOnMap_WhenMapAppFails_ShowsAlert()
    {
        _map.Setup(m => m.OpenAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();
        vm.ApplyQuery(new Dictionary<string, object>
        {
            ["SelectedInspection"] = new RoomInspection { RoomNumber = "7", Latitude = 1, Longitude = 2 }
        });

        await vm.OpenOnMapCommand.ExecuteAsync(null);

        _dialogs.Verify(d => d.AlertAsync("Карти недоступні", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
