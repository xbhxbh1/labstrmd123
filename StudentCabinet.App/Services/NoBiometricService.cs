namespace StudentCabinetMVVM.Services;

/// <summary>Заглушка для платформ, де біометрію ще не реалізовано.</summary>
public class NoBiometricService : IBiometricService
{
    public Task<bool> IsAvailableAsync() => Task.FromResult(false);

    public Task<bool> AuthenticateAsync(string reason) => Task.FromResult(false);
}
