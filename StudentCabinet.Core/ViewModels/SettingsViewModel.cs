using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public const string ThemeKey = "app_theme";

    private readonly ISettingsService _settings;
    private readonly IThemeService _theme;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeName))]
    private bool _isDarkTheme;

    public SettingsViewModel(ISettingsService settings, IThemeService theme, INavigationService navigation)
    {
        _settings = settings;
        _theme = theme;
        _navigation = navigation;

        var saved = _settings.Get(ThemeKey, string.Empty);
        _isDarkTheme = saved == "Dark" || (saved == string.Empty && _theme.IsSystemThemeDark);
    }

    public string ThemeName => IsDarkTheme ? "Темна" : "Світла";

    partial void OnIsDarkThemeChanged(bool value)
    {
        _theme.Apply(value);
        _settings.Set(ThemeKey, value ? "Dark" : "Light");
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();

    [RelayCommand]
    private Task OpenLogsAsync() => _navigation.GoToAsync("logs");
}
