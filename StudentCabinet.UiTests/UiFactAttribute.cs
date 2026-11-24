namespace StudentCabinet.UiTests;

/// <summary>
/// Тест, що потребує запущеного Appium-сервера і застосунку. Без RUN_UI_TESTS=1 він пропускається,
/// тому звичайний dotnet test (і CI) не падає на машині без емулятора чи драйвера.
/// </summary>
public sealed class UiFactAttribute : FactAttribute
{
    public UiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_UI_TESTS") != "1")
            Skip = "UI-тест потребує Appium-сервера та запущеного застосунку: встановіть RUN_UI_TESTS=1 (див. README).";
    }
}
