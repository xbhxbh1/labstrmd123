using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using FuelErp.Models;
using FuelErp.Services;
using FuelErp.Tests.TestSupport;
using FuelErp.ViewModels;

namespace FuelErp.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly FakeSettings _settings = new();
    private readonly Mock<IThemeService> _theme = new();
    private readonly Mock<INavigationService> _nav = new();

    private SettingsViewModel CreateSut() => new(_settings, _theme.Object, _nav.Object);

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData("Light", true, false)]
    [InlineData("Dark", false, true)]
    public void InitialTheme_UsesSavedValueOrSystemTheme(string? saved, bool systemDark, bool expectedDark)
    {
        if (saved is not null)
            _settings.Set(SettingsViewModel.ThemeKey, saved);
        _theme.Setup(t => t.IsSystemThemeDark).Returns(systemDark);

        var vm = CreateSut();

        vm.IsDarkTheme.Should().Be(expectedDark);
        vm.ThemeName.Should().Be(expectedDark ? "Темна" : "Світла");
    }

    [Fact]
    public void ChangingTheme_AppliesItAndStoresTheChoice()
    {
        var vm = CreateSut();

        vm.IsDarkTheme = true;

        _theme.Verify(t => t.Apply(true), Times.Once);
        _settings.Get(SettingsViewModel.ThemeKey, "").Should().Be("Dark");
    }

    [Fact]
    public async Task OpenLogs_NavigatesToLogsPage()
    {
        var vm = CreateSut();

        await vm.OpenLogsCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("logs", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }
}

public class LoginViewModelTests
{
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<IBiometricService> _bio = new();
    private readonly Mock<INavigationService> _nav = new();

    private LoginViewModel CreateSut() =>
        new(_auth.Object, _bio.Object, _nav.Object, NullLogger<LoginViewModel>.Instance);

    [Theory]
    [InlineData("", "pw", false)]
    [InlineData("admin", "", false)]
    [InlineData("   ", "pw", false)]
    [InlineData("admin", "pw", true)]
    public void LoginCommand_CanExecute_RequiresLoginAndPassword(string login, string password, bool expected)
    {
        var vm = CreateSut();

        vm.Login = login;
        vm.Password = password;

        vm.LoginCommand.CanExecute(null).Should().Be(expected);
    }

    [Fact]
    public async Task Login_WithValidCredentials_NavigatesToMainPageAndClearsPassword()
    {
        _auth.Setup(a => a.LoginAsync("admin", "pw")).ReturnsAsync(AuthResult.Ok());
        var vm = CreateSut();
        vm.Login = " admin ";
        vm.Password = "pw";

        await vm.LoginCommand.ExecuteAsync(null);

        _nav.Verify(n => n.GoToAsync("//MainPage", It.IsAny<IDictionary<string, object>?>()), Times.Once);
        vm.Password.Should().BeEmpty();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShowsErrorAndStaysOnPage()
    {
        _auth.Setup(a => a.LoginAsync("admin", "bad")).ReturnsAsync(AuthResult.Fail("Невірний логін або пароль."));
        var vm = CreateSut();
        vm.Login = "admin";
        vm.Password = "bad";

        await vm.LoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Невірний логін або пароль.");
        vm.HasError.Should().BeTrue();
        _nav.Verify(n => n.GoToAsync(It.IsAny<string>(), It.IsAny<IDictionary<string, object>?>()), Times.Never);
    }

    [Theory]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, true)]
    public async Task Initialize_OffersBiometricsOnlyWithStoredTokenAndAvailableHello(
        bool hasToken, bool available, bool expectedButton, bool expectedHint)
    {
        _auth.Setup(a => a.HasStoredTokenAsync()).ReturnsAsync(hasToken);
        _bio.Setup(b => b.IsAvailableAsync()).ReturnsAsync(available);
        var vm = CreateSut();

        await vm.InitializeCommand.ExecuteAsync(null);

        vm.CanUseBiometrics.Should().Be(hasToken && available && expectedButton);
        vm.HasBiometricHint.Should().Be(expectedHint);
    }

    [Fact]
    public async Task BiometricLogin_Verified_SignsInWithStoredTokenAndNavigates()
    {
        _bio.Setup(b => b.AuthenticateAsync(It.IsAny<string>())).ReturnsAsync(true);
        _auth.Setup(a => a.LoginWithStoredTokenAsync()).ReturnsAsync(AuthResult.Ok());
        var vm = CreateSut();

        await vm.BiometricLoginCommand.ExecuteAsync(null);

        _auth.Verify(a => a.LoginWithStoredTokenAsync(), Times.Once);
        _nav.Verify(n => n.GoToAsync("//MainPage", It.IsAny<IDictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task BiometricLogin_NotVerified_DoesNotTouchTheToken()
    {
        _bio.Setup(b => b.AuthenticateAsync(It.IsAny<string>())).ReturnsAsync(false);
        var vm = CreateSut();

        await vm.BiometricLoginCommand.ExecuteAsync(null);

        _auth.Verify(a => a.LoginWithStoredTokenAsync(), Times.Never);
        vm.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public async Task BiometricLogin_ExpiredToken_ShowsErrorAndHidesBiometricButton()
    {
        _bio.Setup(b => b.AuthenticateAsync(It.IsAny<string>())).ReturnsAsync(true);
        _auth.Setup(a => a.LoginWithStoredTokenAsync()).ReturnsAsync(AuthResult.Fail("Термін дії минув"));
        _auth.Setup(a => a.HasStoredTokenAsync()).ReturnsAsync(false);
        var vm = CreateSut();
        vm.CanUseBiometrics = true;

        await vm.BiometricLoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Термін дії минув");
        vm.CanUseBiometrics.Should().BeFalse();
    }
}
