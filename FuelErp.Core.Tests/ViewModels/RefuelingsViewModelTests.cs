using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class RefuelingsViewModelTests
{
    private readonly Mock<IRefuelingRepository> _repo = new();
    private readonly Mock<IFuelDataSeeder> _seeder = new();
    private readonly Mock<ISyncService> _sync = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<IAppStateService> _state = new();
    private readonly Mock<INavigationService> _nav = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<INetworkStatusService> _network = new();
    private readonly Mock<ILogger<RefuelingsViewModel>> _logger = new();

    public RefuelingsViewModelTests()
    {
        _repo.Setup(r => r.GetAllAsync()).ReturnsAsync(Sample());
        _repo.Setup(r => r.DeleteAsync(It.IsAny<Refueling>())).ReturnsAsync(1);
        _sync.Setup(s => s.GetPendingCountAsync()).ReturnsAsync(3);
        _sync.Setup(s => s.SyncAsync()).ReturnsAsync(0);
        _state.SetupProperty(s => s.PendingDeepLink, null);
        _network.Setup(n => n.IsOnline).Returns(true);
    }

    private static List<Refueling> Sample() => new()
    {
        new() { Id = 1, VehiclePlate = "АА1234КК", DriverName = "Коваленко Іван", TankName = "Резервуар №2", FuelType = FuelType.Diesel, Liters = 100, PricePerLiter = 50 },
        new() { Id = 2, VehiclePlate = "АЕ5678ІН", DriverName = "Шевченко Олена", TankName = "Резервуар №1", FuelType = FuelType.Petrol95, Liters = 40, PricePerLiter = 53.5, Notes = "картка" },
        new() { Id = 3, VehiclePlate = "КА9012ВВ", DriverName = "Бойко Андрій", TankName = "Резервуар №2", FuelType = FuelType.Diesel, Liters = 60, PricePerLiter = 50 }
    };

    private RefuelingsViewModel CreateSut() =>
        new(_repo.Object, _seeder.Object, _sync.Object, _session.Object, _auth.Object, _state.Object, _nav.Object,
            _dialogs.Object, TestHelpers.SynchronousMainThread().Object, _network.Object, _logger.Object);

    private void SignInAs(UserRole? role)
    {
        _session.Setup(s => s.CurrentRole).Returns(role);
        _session.Setup(s => s.UserName).Returns(role is null ? null : "user");
        _session.Setup(s => s.IsInRole(It.IsAny<UserRole>())).Returns((UserRole r) => r == role);
    }

    [Fact]
    public async Task Load_SeedsDemoDataAndShowsAllRefuelings()
    {
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _seeder.Verify(s => s.SeedAsync(), Times.Once);
        vm.Refuelings.Should().HaveCount(3);
        vm.PendingCount.Should().Be(3);
        vm.SummaryText.Should().Contain("Заправок: 3");
    }

    [Fact]
    public async Task Load_WhenRepositoryThrows_LogsErrorAndShowsAlert()
    {
        _repo.Setup(r => r.GetAllAsync()).ThrowsAsync(new InvalidOperationException("db"));
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
        _dialogs.Verify(d => d.AlertAsync("Помилка", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        vm.Refuelings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("АА1234", 1)]
    [InlineData("шевченко", 1)]
    [InlineData("резервуар №2", 2)]
    [InlineData("КАРТКА", 1)]
    [InlineData("немає такого", 0)]
    [InlineData("  ", 3)]
    public async Task Search_FiltersByPlateDriverTankOrNotes(string query, int expected)
    {
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SearchText = query;

        vm.Refuelings.Should().HaveCount(expected);
    }

    [Fact]
    public async Task Filter_ByFuelType_KeepsOnlyThatFuel()
    {
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedFilter = vm.Filters.Single(f => f.Type == FuelType.Diesel);

        vm.Refuelings.Should().OnlyContain(r => r.FuelType == FuelType.Diesel).And.HaveCount(2);
        vm.SummaryText.Should().MatchRegex("літрів: 160[,.]0");
    }

    [Fact]
    public async Task SearchAndFilter_WorkTogether()
    {
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        vm.SelectedFilter = vm.Filters.Single(f => f.Type == FuelType.Diesel);
        vm.SearchText = "Бойко";

        vm.Refuelings.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Theory]
    [InlineData(UserRole.StationOperator, true, false, false)]
    [InlineData(UserRole.Accountant, false, true, true)]
    public void RoleFlags_DependOnTheCurrentRole(UserRole role, bool canCreate, bool canDelete, bool isAccountant)
    {
        SignInAs(role);
        var vm = CreateSut();

        vm.CanCreate.Should().Be(canCreate);
        vm.CanDelete.Should().Be(canDelete);
        vm.IsAccountant.Should().Be(isAccountant);
        vm.CurrentUserText.Should().Contain(role.ToDisplayName());
    }

    [Fact]
    public async Task NewRefueling_AsOperator_OpensTheForm()
    {
        SignInAs(UserRole.StationOperator);
        var vm = CreateSut();

        await vm.NewRefuelingCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("refueling/edit", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task NewRefueling_AsAccountant_IsDeniedWithoutNavigation()
    {
        SignInAs(UserRole.Accountant);
        var vm = CreateSut();

        await vm.NewRefuelingCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>?>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Delete_AsOperator_IsDeniedAndRepositoryNotCalled()
    {
        SignInAs(UserRole.StationOperator);
        var vm = CreateSut();

        await vm.DeleteRefuelingCommand.ExecuteAsync(Sample()[0]);

        _repo.Verify(r => r.DeleteAsync(It.IsAny<Refueling>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Delete_AsAccountant_AfterConfirmationRemovesTheRecord()
    {
        SignInAs(UserRole.Accountant);
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);
        var target = vm.Refuelings.First(r => r.Id == 2);

        await vm.DeleteRefuelingCommand.ExecuteAsync(target);

        _repo.Verify(r => r.DeleteAsync(target), Times.Once);
        vm.Refuelings.Should().NotContain(r => r.Id == 2);
        vm.StatusMessage.Should().Contain("видалено");
    }

    [Fact]
    public async Task Delete_AsAccountant_WhenUserCancels_KeepsTheRecord()
    {
        SignInAs(UserRole.Accountant);
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);
        var vm = CreateSut();

        await vm.DeleteRefuelingCommand.ExecuteAsync(Sample()[0]);

        _repo.Verify(r => r.DeleteAsync(It.IsAny<Refueling>()), Times.Never);
    }

    [Fact]
    public async Task Delete_WhenRepositoryThrows_LogsErrorAndKeepsTheList()
    {
        SignInAs(UserRole.Accountant);
        _dialogs.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _repo.Setup(r => r.DeleteAsync(It.IsAny<Refueling>())).ThrowsAsync(new InvalidOperationException());
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.DeleteRefuelingCommand.ExecuteAsync(vm.Refuelings[0]);

        _logger.VerifyLogged(LogLevel.Error, Times.Once());
        vm.Refuelings.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("report")]
    [InlineData("users")]
    public async Task AccountantOnlyScreens_AreDeniedForTheOperator(string route)
    {
        SignInAs(UserRole.StationOperator);
        var vm = CreateSut();

        await (route == "report" ? vm.OpenReportCommand.ExecuteAsync(null) : vm.OpenStaffCommand.ExecuteAsync(null));

        _nav.Verify(n => n.GoToAsync(route, It.IsAny<IDictionary<string, object>?>()), Times.Never);
        _dialogs.Verify(d => d.AlertAsync("Доступ заборонено", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("report")]
    [InlineData("users")]
    public async Task AccountantOnlyScreens_AreOpenedForTheAccountant(string route)
    {
        SignInAs(UserRole.Accountant);
        var vm = CreateSut();

        await (route == "report" ? vm.OpenReportCommand.ExecuteAsync(null) : vm.OpenStaffCommand.ExecuteAsync(null));

        _nav.Verify(n => n.GoToAsync(route, It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task OpenDetails_PassesTheSelectedRefuelingToTheCard()
    {
        IDictionary<string, object>? passed = null;
        _nav.Setup(n => n.GoToAsync("refueling/detail", It.IsAny<IDictionary<string, object>?>()))
            .Callback<string, IDictionary<string, object>?>((_, p) => passed = p)
            .Returns(Task.CompletedTask);
        var refueling = Sample()[1];
        var vm = CreateSut();

        await vm.OpenDetailsCommand.ExecuteAsync(refueling);

        passed!["SelectedRefueling"].Should().BeSameAs(refueling);
    }

    [Fact]
    public async Task Logout_SignsOutAndReturnsToLoginPage()
    {
        var vm = CreateSut();

        await vm.LogoutCommand.ExecuteAsync(null);

        _auth.Verify(a => a.Logout(), Times.Once);
        _nav.Verify(n => n.GoToAsync("//LoginPage", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task ApplyQuery_SavedRefueling_ShowsResultAndReloadsTheList()
    {
        var vm = CreateSut();
        await vm.LoadCommand.ExecuteAsync(null);
        _repo.Invocations.Clear();

        vm.ApplyQuery(new Dictionary<string, object> { ["SavedRefueling"] = Sample()[0] });
        await Task.Delay(50);

        vm.StatusMessage.Should().StartWith("Збережено");
        _repo.Verify(r => r.GetAllAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DeepLink_ToExistingRefueling_OpensItsCard()
    {
        _state.Object.PendingDeepLink = "fuelerp://refueling/2";
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("refueling/detail", It.Is<IDictionary<string, object>>(p =>
            ((Refueling)p["SelectedRefueling"]).Id == 2)), Times.Once);
        _state.Object.PendingDeepLink.Should().BeNull();
    }

    [Theory]
    [InlineData("fuelerp://refueling/999")]
    [InlineData("fuelerp://refueling/abc")]
    [InlineData("https://example.com/refueling/1")]
    public async Task DeepLink_WithUnknownTarget_IsIgnoredWithAWarning(string link)
    {
        _state.Object.PendingDeepLink = link;
        var vm = CreateSut();

        await vm.LoadCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("refueling/detail", It.IsAny<IDictionary<string, object>?>()), Times.Never);
        _logger.VerifyLogged(LogLevel.Warning, Times.AtLeastOnce());
    }

    [Fact]
    public void NetworkChange_UpdatesTheOnlineIndicator()
    {
        var vm = CreateSut();

        _network.Raise(n => n.OnlineChanged += null, _network.Object, false);

        vm.IsOnline.Should().BeFalse();
        vm.NetworkText.Should().Contain("Офлайн");
    }

    [Fact]
    public async Task QueueChange_RefreshesThePendingCounter()
    {
        var vm = CreateSut();
        _sync.Setup(s => s.GetPendingCountAsync()).ReturnsAsync(7);

        _sync.Raise(s => s.QueueChanged += null, EventArgs.Empty);
        await Task.Delay(50);

        vm.PendingCount.Should().Be(7);
        vm.PendingText.Should().Contain("7");
    }

    [Fact]
    public async Task ManualSync_RunsTheSyncService()
    {
        var vm = CreateSut();

        await vm.SyncCommand.ExecuteAsync(null);

        _sync.Verify(s => s.SyncAsync(), Times.Once);
    }
}
