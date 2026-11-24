namespace StudentCabinetMVVM.Services;

public class MauiSettingsService : ISettingsService
{
    public T Get<T>(string key, T defaultValue) => Preferences.Default.Get(key, defaultValue);

    public void Set<T>(string key, T value) => Preferences.Default.Set(key, value);
}
