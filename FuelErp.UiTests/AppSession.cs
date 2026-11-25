using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;

namespace FuelErp.UiTests;

/// <summary>
/// Сеанс Appium для Windows-збірки застосунку. Елементи шукаються за AutomationId
/// (у Windows він відображається як AccessibilityId), а не за текстом: текст залежить від мови інтерфейсу.
/// Для Android-емулятора замініть драйвер на AndroidDriver (UiAutomator2) і шукайте за MobileBy.Id.
/// </summary>
public sealed class AppSession : IDisposable
{
    public const string OperatorLogin = "operator";
    public const string OperatorPassword = "Operator#2024";
    public const string AccountantLogin = "accountant";
    public const string AccountantPassword = "Accountant#2024";

    public WindowsDriver Driver { get; }

    public WebDriverWait Wait { get; }

    public AppSession()
    {
        var appPath = Environment.GetEnvironmentVariable("UI_TEST_APP")
            ?? throw new InvalidOperationException(
                "Змінна середовища UI_TEST_APP має вказувати на FuelErp.exe зібраного застосунку.");
        var server = Environment.GetEnvironmentVariable("APPIUM_URL") ?? "http://127.0.0.1:4723";

        var options = new AppiumOptions
        {
            App = appPath,
            PlatformName = "Windows",
            AutomationName = "Windows"
        };

        Driver = new WindowsDriver(new Uri(server), options);
        Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
        Wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(15));
    }

    public IWebElement ById(string automationId) => Driver.FindElement(MobileBy.AccessibilityId(automationId));

    public bool IsShown(string automationId) =>
        Driver.FindElements(MobileBy.AccessibilityId(automationId)).Any(e => e.Displayed);

    public IWebElement WaitFor(string automationId) =>
        Wait.Until(d => d.FindElements(MobileBy.AccessibilityId(automationId)).FirstOrDefault(e => e.Displayed))!;

    public void Login(string login, string password)
    {
        var loginEntry = WaitFor("LoginEntry");
        loginEntry.Clear();
        loginEntry.SendKeys(login);

        var passwordEntry = ById("PasswordEntry");
        passwordEntry.Clear();
        passwordEntry.SendKeys(password);

        ById("LoginButton").Click();
    }

    public void LoginAsOperator()
    {
        Login(OperatorLogin, OperatorPassword);
        WaitFor("CurrentUserLabel");
    }

    public void LoginAsAccountant()
    {
        Login(AccountantLogin, AccountantPassword);
        WaitFor("CurrentUserLabel");
    }

    public void Dispose()
    {
        Driver.Quit();
        Driver.Dispose();
    }
}
