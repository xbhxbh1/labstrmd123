namespace StudentCabinetMVVM.Services;

/// <summary>Захищене сховище для токенів і ключів (обгортка над SecureStorage).</summary>
public interface ISecureStoreService
{
    Task<string?> GetAsync(string key);

    Task SetAsync(string key, string value);

    void Remove(string key);
}
