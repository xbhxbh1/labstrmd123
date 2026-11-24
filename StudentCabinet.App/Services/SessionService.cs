using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

/// <summary>
/// Стан поточної сесії: хто увійшов і з якою роллю. Реєструється як singleton,
/// тому всі ViewModel бачать один і той самий стан.
/// </summary>
public class SessionService : ISessionService
{
    public UserRole? CurrentRole { get; private set; }

    public string? UserName { get; private set; }

    public bool IsAuthenticated => CurrentRole is not null;

    public event EventHandler? SessionChanged;

    public bool IsInRole(UserRole role) => CurrentRole == role;

    public void SignIn(UserRole role, string userName)
    {
        CurrentRole = role;
        UserName = userName;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SignOut()
    {
        CurrentRole = null;
        UserName = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
