using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public interface IAuthApi
{
    /// <summary>Повертає JWT або null, якщо логін чи пароль невірні.</summary>
    Task<string?> AuthenticateAsync(string login, string password, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserInfo>> GetUsersAsync(string token, CancellationToken cancellationToken = default);
}
