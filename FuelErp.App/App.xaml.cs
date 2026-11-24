using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using FuelErp.Services;
using FuelErp.ViewModels;

namespace FuelErp;

public partial class App : Application
{
    private readonly IAppStateService _state;
    private readonly ILogger<App> _logger;

    public App(IAppStateService state, ILogger<App> logger)
    {
        InitializeComponent();

        _state = state;
        _logger = logger;
        _state.Restore();

        ApplySavedTheme();
        _logger.LogInformation("Застосунок створено, початок сесії {Start:HH:mm:ss}", _state.SessionStartedAt);
    }

    private void ApplySavedTheme()
    {
        var saved = Preferences.Get(SettingsViewModel.ThemeKey, string.Empty);

        if (saved == "Dark")
            UserAppTheme = AppTheme.Dark;
        else if (saved == "Light")
            UserAppTheme = AppTheme.Light;
    }

    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        _state.PendingDeepLink = uri.ToString();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

        window.Stopped += (s, e) =>
        {
            _logger.LogInformation("Життєвий цикл: Stopped, застосунок згорнуто, збереження стану");
            _state.Save();
        };

        window.Resumed += (s, e) =>
        {
            _logger.LogInformation("Життєвий цикл: Resumed, застосунок повернуто, відновлення стану");
            _state.Restore();
        };

        window.Destroying += (s, e) =>
        {
            _logger.LogInformation("Життєвий цикл: Destroying, звільнення ресурсів");
            _state.Save();
        };

        return window;
    }
}
