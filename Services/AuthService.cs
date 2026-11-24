using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public class AuthService : IAuthService
{
    private const string TokenKey = "auth_token";

    private readonly IAuthApi _api;
    private readonly ISessionService _session;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAuthApi api, ISessionService session, ILogger<AuthService> logger)
    {
        _api = api;
        _session = session;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string login, string password)
    {
        string? token;
        try
        {
            token = await _api.AuthenticateAsync(login, password);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка звернення до сервера автентифікації");
            return AuthResult.Fail("Сервер автентифікації недоступний. Перевірте, чи запущено тестовий сервер.");
        }

        if (token is null)
        {
            _logger.LogWarning("Невдала спроба входу: логін {Login}", login);
            return AuthResult.Fail("Невірний логін або пароль.");
        }

        if (!JwtParser.TryParse(token, out var claims))
        {
            _logger.LogError("Сервер повернув токен у невідомому форматі");
            return AuthResult.Fail("Сервер повернув некоректний токен.");
        }

        try
        {
            await SecureStorage.Default.SetAsync(TokenKey, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти токен у SecureStorage");
            return AuthResult.Fail("Не вдалося безпечно зберегти токен доступу.");
        }

        _session.SignIn(claims.Role, claims.UserName);
        _logger.LogInformation("Успішний вхід: {User}, роль {Role}", claims.UserName, claims.Role);
        return AuthResult.Ok();
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(TokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати токен із SecureStorage");
            return null;
        }
    }

    public void Logout()
    {
        SecureStorage.Default.Remove(TokenKey);
        _logger.LogInformation("Вихід користувача {User}", _session.UserName);
        _session.SignOut();
    }
}
