using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

public partial class InspectionDetailViewModel : ObservableObject, IQueryReceiver
{
    private readonly IMapLauncher _map;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<InspectionDetailViewModel> _logger;

    [ObservableProperty]
    private RoomInspection _inspection = new();

    public InspectionDetailViewModel(IMapLauncher map, INavigationService navigation, IDialogService dialogs,
        ILogger<InspectionDetailViewModel> logger)
    {
        _map = map;
        _navigation = navigation;
        _dialogs = dialogs;
        _logger = logger;
    }

    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedInspection", out var value) && value is RoomInspection inspection)
            Inspection = inspection;
    }

    // Для Windows це альтернатива елементу Map: координати відкриваються в зовнішньому застосунку карт
    [RelayCommand]
    private async Task OpenOnMapAsync()
    {
        if (!Inspection.HasLocation)
        {
            await _dialogs.AlertAsync("Немає координат", "Для цього акта координати не визначались.");
            return;
        }

        try
        {
            await _map.OpenAsync(Inspection.Latitude!.Value, Inspection.Longitude!.Value,
                $"Акт огляду, аудиторія {Inspection.RoomNumber}");
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
