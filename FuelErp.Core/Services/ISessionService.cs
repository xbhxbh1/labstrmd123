using FuelErp.Models;

namespace FuelErp.Services;

public interface ISessionService
{
    UserRole? CurrentRole { get; }

    string? UserName { get; }

    bool IsAuthenticated { get; }

    event EventHandler? SessionChanged;

    bool IsInRole(UserRole role);

    void SignIn(UserRole role, string userName);

    void SignOut();
}
