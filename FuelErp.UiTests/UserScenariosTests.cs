using FluentAssertions;

namespace FuelErp.UiTests;

/// <summary>
/// Користувацькі сценарії: «вхід у систему» (за двома ролями) і «створення нового запису».
/// Перед запуском потрібен тестовий сервер (stub/FuelErp.StubApi) і Appium-сервер.
/// </summary>
public class UserScenariosTests : IDisposable
{
    private AppSession? _session;

    private AppSession Session => _session ??= new AppSession();

    public void Dispose() => _session?.Dispose();

    [UiFact]
    public void Login_AsOperator_ShowsTheNewRefuelingButtonButNotTheAccountantScreens()
    {
        Session.LoginAsOperator();

        Session.WaitFor("CurrentUserLabel").Text.Should().Contain("operator");
        Session.IsShown("NewRefuelingButton").Should().BeTrue("оператор реєструє заправки");
        Session.IsShown("ReportButton").Should().BeFalse("звіт бачить лише бухгалтер");
        Session.IsShown("StaffButton").Should().BeFalse();
    }

    [UiFact]
    public void Login_AsAccountant_ShowsTheReportButNotTheNewRefuelingButton()
    {
        Session.LoginAsAccountant();

        Session.WaitFor("CurrentUserLabel").Text.Should().Contain("accountant");
        Session.IsShown("ReportButton").Should().BeTrue();
        Session.IsShown("StaffButton").Should().BeTrue();
        Session.IsShown("NewRefuelingButton").Should().BeFalse("бухгалтер не реєструє заправки");
    }

    [UiFact]
    public void Login_WithWrongPassword_ShowsErrorAndStaysOnLoginPage()
    {
        Session.Login("operator", "wrong-password");

        Session.WaitFor("LoginErrorLabel").Text.Should().NotBeNullOrEmpty();
        Session.IsShown("CurrentUserLabel").Should().BeFalse();
    }

    [UiFact]
    public void CreateVehicle_AddsANewRecordToTheList()
    {
        Session.LoginAsOperator();
        var plate = $"ТЕ{DateTime.Now:HHmm}ТТ";

        Session.WaitFor("VehiclesButton").Click();
        Session.WaitFor("PlateEntry").SendKeys(plate);
        Session.ById("ModelEntry").SendKeys("Тестовий автомобіль");
        Session.ById("AddVehicleButton").Click();

        Session.WaitFor("VehiclesCollection").Text.Should().Contain(plate);
    }
}
