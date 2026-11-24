namespace StudentCabinetMVVM.Services;

/// <summary>Навігація між сторінками. У застосунку реалізується через Shell, у тестах підміняється mock-об'єктом.</summary>
public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);

    Task GoBackAsync(IDictionary<string, object>? parameters = null);
}
