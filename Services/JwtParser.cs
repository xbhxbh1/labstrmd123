using System.Text;
using System.Text.Json;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public record JwtClaims(string UserName, UserRole Role, DateTimeOffset ExpiresAt);

/// <summary>
/// Читає claims із payload JWT. Підпис клієнт перевірити не може (ключ є лише на сервері),
/// тому отримані дані використовуються тільки для інтерфейсу, а справжню перевірку
/// ролі виконує сервер.
/// </summary>
public static class JwtParser
{
    public static bool TryParse(string? token, out JwtClaims claims)
    {
        claims = default!;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        var parts = token.Split('.');
        if (parts.Length != 3)
            return false;

        try
        {
            using var document = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            var root = document.RootElement;

            if (!root.TryGetProperty("sub", out var sub) || sub.ValueKind != JsonValueKind.String)
                return false;

            if (!TryReadRole(root, out var role))
                return false;

            if (!root.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var expSeconds))
                return false;

            claims = new JwtClaims(sub.GetString()!, role, DateTimeOffset.FromUnixTimeSeconds(expSeconds));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadRole(JsonElement root, out UserRole role)
    {
        role = default;

        if (!root.TryGetProperty("role", out var value))
            return false;

        // role може бути рядком або масивом рядків; якщо ролей кілька - береться найвища
        var names = value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(e => e.GetString())
            : new[] { value.GetString() };

        var found = false;
        foreach (var name in names)
        {
            if (Enum.TryParse<UserRole>(name, ignoreCase: true, out var parsed) && (!found || parsed > role))
            {
                role = parsed;
                found = true;
            }
        }

        return found;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}
