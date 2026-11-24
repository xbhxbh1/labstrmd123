namespace StudentCabinetMVVM.Services;

/// <summary>Прості налаштування (обгортка над Preferences). Не призначена для секретів.</summary>
public interface ISettingsService
{
    T Get<T>(string key, T defaultValue);

    void Set<T>(string key, T value);
}
