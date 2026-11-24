using FuelErp.Models;

namespace FuelErp.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string login, string password);

    Task<string?> GetTokenAsync();

    Task<bool> HasStoredTokenAsync();

    /// <summary>Вхід без пароля: використовується збережений токен (після біометричної перевірки).</summary>
    Task<AuthResult> LoginWithStoredTokenAsync();

    void Logout();
}
