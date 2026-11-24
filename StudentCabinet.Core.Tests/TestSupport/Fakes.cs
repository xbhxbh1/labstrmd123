using System.Text;
using System.Text.Json;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.Tests.TestSupport;

/// <summary>Захищене сховище в пам'яті (замість SecureStorage).</summary>
public class FakeSecureStore : ISecureStoreService
{
    public Dictionary<string, string> Values { get; } = new();

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(Values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string value)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }

    public void Remove(string key) => Values.Remove(key);
}

/// <summary>Налаштування в пам'яті (замість Preferences).</summary>
public class FakeSettings : ISettingsService
{
    public Dictionary<string, object> Values { get; } = new();

    public T Get<T>(string key, T defaultValue) =>
        Values.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

    public void Set<T>(string key, T value) => Values[key] = value!;
}

public static class JwtFactory
{
    public static string Create(string subject, object role, DateTimeOffset expires)
    {
        var header = Base64Url("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        var payload = Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = subject,
            ["role"] = role,
            ["exp"] = expires.ToUnixTimeSeconds()
        }));

        return $"{header}.{payload}.signature";
    }

    private static string Base64Url(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
