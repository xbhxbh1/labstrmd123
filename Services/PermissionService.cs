using Microsoft.Extensions.Logging;

namespace StudentCabinetMVVM.Services;

public class PermissionService : IPermissionService
{
    private readonly ILogger<PermissionService> _logger;

    public PermissionService(ILogger<PermissionService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> EnsureAsync<TPermission>() where TPermission : Permissions.BasePermission, new()
    {
        var name = typeof(TPermission).Name;

        var status = await Permissions.CheckStatusAsync<TPermission>();
        if (status == PermissionStatus.Granted)
            return true;

        // rationale: пояснення, навіщо потрібен дозвіл, якщо користувач уже відмовляв раніше (Android)
        if (Permissions.ShouldShowRationale<TPermission>())
        {
            _logger.LogInformation("Показано пояснення (rationale) для дозволу {Permission}", name);
            await Shell.Current.DisplayAlert("Потрібен дозвіл",
                "Дозвіл потрібен для фіксації акта огляду аудиторії: фото дефекту та місця огляду.", "OK");
        }

        status = await Permissions.RequestAsync<TPermission>();

        if (status == PermissionStatus.Granted)
            _logger.LogInformation("Дозвіл {Permission} надано", name);
        else
            _logger.LogWarning("Дозвіл {Permission} не надано (стан {Status})", name, status);

        return status == PermissionStatus.Granted;
    }
}
