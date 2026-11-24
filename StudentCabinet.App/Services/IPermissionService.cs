namespace StudentCabinetMVVM.Services;

public interface IPermissionService
{
    /// <summary>
    /// Перевіряє дозвіл і, якщо його немає, пояснює користувачу навіщо він потрібен та запитує його.
    /// Повертає true, лише якщо дозвіл надано.
    /// </summary>
    Task<bool> EnsureAsync<TPermission>() where TPermission : Permissions.BasePermission, new();
}
