using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public interface IAuthService
{
    Task<AuthResult> LoginAsync(string login, string password);

    Task<string?> GetTokenAsync();

    void Logout();
}
