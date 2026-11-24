# UI-тести (Appium)

Дві групи сценаріїв: «вхід у систему» і «створення нового запису» (`UserScenariosTests`).
Елементи шукаються за `AutomationId`, який задано в XAML (`LoginEntry`, `PasswordEntry`, `LoginButton`,
`FullNameEntry`, `GroupEntry`, `AddStudentButton`, `StudentsCollection`, ...).

За замовчуванням тести **пропускаються** (звичайний `dotnet test` і CI їх не виконують).

## Запуск на Windows

1. Зберіть застосунок: `dotnet publish FuelErp.App -f net8.0-windows10.0.19041.0 -c Release -p:WindowsPackageType=None -o publish`
2. Запустіть тестовий сервер: `dotnet run --project stub/FuelErp.StubApi`
3. Встановіть і запустіть Appium з драйвером Windows (потрібен Node.js):
   ```powershell
   npm install -g appium
   appium driver install --source=npm appium-windows-driver
   appium
   ```
   Для драйвера потрібні WinAppDriver і режим розробника Windows.
4. Виконайте тести:
   ```powershell
   $env:RUN_UI_TESTS = "1"
   $env:UI_TEST_APP  = "C:\шлях\до\publish\FuelErp.exe"
   dotnet test FuelErp.UiTests
   ```

## Android

Для емулятора Android замініть `WindowsDriver` на `AndroidDriver` (драйвер UiAutomator2), укажіть
`AppPackage`/`App` (APK) і шукайте елементи через `MobileBy.Id("LoginEntry")`.
