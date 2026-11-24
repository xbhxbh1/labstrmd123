namespace FuelErp.Services;

/// <summary>Реалізація навігації через Shell. Параметри передаються як одноразові (ShellNavigationQueryParameters).</summary>
public class ShellNavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        if (parameters is null)
            return Shell.Current.GoToAsync(route);

        var query = new ShellNavigationQueryParameters();
        foreach (var pair in parameters)
            query.Add(pair.Key, pair.Value);

        return Shell.Current.GoToAsync(route, query);
    }

    public Task GoBackAsync(IDictionary<string, object>? parameters = null) => GoToAsync("..", parameters);
}
