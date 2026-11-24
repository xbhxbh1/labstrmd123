namespace StudentCabinetMVVM.Services;

/// <summary>Дозволи пристрою, потрібні застосунку (власний перелік, щоб ядро не залежало від MAUI Permissions).</summary>
public enum DevicePermission
{
    Camera,
    Location
}

public interface IPermissionService
{
    /// <summary>
    /// Перевіряє дозвіл і, якщо його немає, пояснює користувачу навіщо він потрібен та запитує його.
    /// Повертає true, лише якщо дозвіл надано.
    /// </summary>
    Task<bool> EnsureAsync(DevicePermission permission);
}
