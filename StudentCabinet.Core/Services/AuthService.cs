using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public class AuthService : IAuthService
{
    private const string TokenKey = "auth_token";

    private readonly IAuthApi _api;
    private readonly ISecureStoreService _secureStore;
    private readonly ISessionService _session;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IAuthApi api, ISecureStoreService secureStore, ISessionService session,
        ILogger<AuthService> logger)
    {
        _api = api;
        _secureStore = secureStore;
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
        catch (Exception ex) when (IsCertificateProblem(ex))
        {
            _logger.LogError(ex, "Сертифікат сервера автентифікації не пройшов перевірку закріплення");
            return AuthResult.Fail("Не вдалося перевірити сертифікат сервера. З'єднання відхилено: можлива атака «людина посередині».");
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
            await _secureStore.SetAsync(TokenKey, token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося зберегти токен у захищеному сховищі");
            return AuthResult.Fail("Не вдалося безпечно зберегти токен доступу.");
        }

        _session.SignIn(claims.Role, claims.UserName);
        _logger.LogInformation("Успішний вхід: {User}, роль {Role}", claims.UserName, claims.Role);
        return AuthResult.Ok();
    }

    private static bool IsCertificateProblem(Exception ex) =>
        ex is HttpRequestException { HttpRequestError: HttpRequestError.SecureConnectionError }
        || ex.InnerException is System.Security.Authentication.AuthenticationException;

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _secureStore.GetAsync(TokenKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося прочитати токен із захищеного сховища");
            return null;
        }
    }

    public async Task<bool> HasStoredTokenAsync() =>
        !string.IsNullOrEmpty(await GetTokenAsync());

    public async Task<AuthResult> LoginWithStoredTokenAsync()
    {
        var token = await GetTokenAsync();
        if (string.IsNullOrEmpty(token))
            return AuthResult.Fail("Збережений токен відсутній. Увійдіть за допомогою пароля.");

        if (!JwtParser.TryParse(token, out var claims))
        {
            _logger.LogWarning("Збережений токен має некоректний формат і буде видалений");
            _secureStore.Remove(TokenKey);
            return AuthResult.Fail("Збережений токен пошкоджено. Увійдіть за допомогою пароля.");
        }

        if (claims.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _logger.LogInformation("Термін дії збереженого токена користувача {User} минув", claims.UserName);
            _secureStore.Remove(TokenKey);
            return AuthResult.Fail("Термін дії збереженого токена минув. Увійдіть за допомогою пароля.");
        }

        _session.SignIn(claims.Role, claims.UserName);
        _logger.LogInformation("Вхід за збереженим токеном після біометрії: {User}, роль {Role}",
            claims.UserName, claims.Role);
        return AuthResult.Ok();
    }

    public void Logout()
    {
        _secureStore.Remove(TokenKey);
        _logger.LogInformation("Вихід користувача {User}", _session.UserName);
        _session.SignOut();
    }
}
