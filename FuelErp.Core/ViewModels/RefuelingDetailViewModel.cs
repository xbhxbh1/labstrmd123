using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

/// <summary>Картка заправки: дані, фото чека, координати. Редагувати може лише оператор АЗС.</summary>
public partial class RefuelingDetailViewModel : ObservableObject, IQueryReceiver
{
    private readonly ISessionService _session;
    private readonly IMapLauncher _map;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<RefuelingDetailViewModel> _logger;

    [ObservableProperty]
    private Refueling _refueling = new();

    public bool CanEdit => _session.IsInRole(UserRole.StationOperator);

    public RefuelingDetailViewModel(ISessionService session, IMapLauncher map, INavigationService navigation,
        IDialogService dialogs, ILogger<RefuelingDetailViewModel> logger)
    {
        _session = session;
        _map = map;
        _navigation = navigation;
        _dialogs = dialogs;
        _logger = logger;
    }

    // SelectedRefueling передає список, SavedRefueling повертає форма редагування після збереження
    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SavedRefueling", out var saved) && saved is Refueling updated)
            Refueling = updated;
        else if (query.TryGetValue("SelectedRefueling", out var selected) && selected is Refueling refueling)
            Refueling = refueling;
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        if (!CanEdit)
        {
            _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався редагувати заправку Id={Id}",
                _session.UserName, _session.CurrentRole, Refueling.Id);
            await _dialogs.AlertAsync("Доступ заборонено", "Редагувати заправки може лише оператор АЗС.");
            return;
        }

        await _navigation.GoToAsync("refueling/edit", new Dictionary<string, object>
        {
            ["SelectedRefueling"] = Refueling
        });
    }

    // Для Windows це альтернатива елементу Map: координати відкриваються в зовнішньому застосунку карт
    [RelayCommand]
    private async Task OpenOnMapAsync()
    {
        if (!Refueling.HasLocation)
        {
            await _dialogs.AlertAsync("Немає координат", "Для цієї заправки координати не визначались.");
            return;
        }

        try
        {
            await _map.OpenAsync(Refueling.Latitude!.Value, Refueling.Longitude!.Value,
                $"Заправка {Refueling.VehiclePlate}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити застосунок карт");
            await _dialogs.AlertAsync("Карти недоступні", "Не вдалося відкрити застосунок карт.");
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
