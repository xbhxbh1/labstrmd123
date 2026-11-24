using FluentAssertions;

namespace StudentCabinet.UiTests;

/// <summary>
/// Два користувацькі сценарії з методички: «вхід у систему» і «створення нового запису».
/// Перед запуском потрібен тестовий сервер (stub/StudentCabinet.StubApi) і Appium-сервер.
/// </summary>
public class UserScenariosTests : IDisposable
{
    private AppSession? _session;

    private AppSession Session => _session ??= new AppSession();

    public void Dispose() => _session?.Dispose();

    [UiFact]
    public void Login_WithValidCredentials_OpensMainPageForTheAdministrator()
    {
        Session.LoginAsAdministrator();

        var currentUser = Session.WaitFor("CurrentUserLabel");
        currentUser.Text.Should().Contain("admin");
        Session.WaitFor("ManageUsersButton").Displayed.Should().BeTrue(
            "кнопку «Керування користувачами» бачить лише адміністратор");
    }

    [UiFact]
    public void Login_WithWrongPassword_ShowsErrorAndStaysOnLoginPage()
    {
        Session.WaitFor("LoginEntry").SendKeys("admin");
        Session.ById("PasswordEntry").SendKeys("wrong-password");
        Session.ById("LoginButton").Click();

        Session.WaitFor("LoginErrorLabel").Text.Should().NotBeNullOrEmpty();
    }

    [UiFact]
    public void CreateStudent_AddsANewRecordToTheList()
    {
        Session.LoginAsAdministrator();
        var name = $"Тестовий Студент {DateTime.Now:HHmmss}";

        var fullName = Session.WaitFor("FullNameEntry");
        fullName.SendKeys(name);
        Session.ById("GroupEntry").SendKeys("КН-31");
        Session.ById("AddStudentButton").Click();

        var list = Session.WaitFor("StudentsCollection");
        list.Text.Should().Contain(name);
    }
}
