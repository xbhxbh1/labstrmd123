namespace StudentCabinetMVVM.Services;

public class MauiThemeService : IThemeService
{
    public bool IsSystemThemeDark => Application.Current?.RequestedTheme == AppTheme.Dark;

    public void Apply(bool dark)
    {
        if (Application.Current is not null)
            Application.Current.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
    }
}
