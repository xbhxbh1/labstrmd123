namespace FuelErp.Services;

/// <summary>
/// Біометрична перевірка користувача (Windows Hello: відбиток пальця, обличчя або PIN-код).
/// Вона лише підтверджує, що за пристроєм сидить власник, а сам доступ дає токен із SecureStorage.
/// </summary>
public interface IBiometricService
{
    Task<bool> IsAvailableAsync();

    Task<bool> AuthenticateAsync(string reason);
}
