using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.ViewModels;

public partial class InspectionDetailViewModel : ObservableObject, IQueryAttributable
{
    private readonly IMap _map;
    private readonly ILogger<InspectionDetailViewModel> _logger;

    [ObservableProperty]
    private RoomInspection _inspection = new();

    public InspectionDetailViewModel(IMap map, ILogger<InspectionDetailViewModel> logger)
    {
        _map = map;
        _logger = logger;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
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
            await Shell.Current.DisplayAlert("Немає координат", "Для цього акта координати не визначались.", "OK");
            return;
        }

        try
        {
            await _map.OpenAsync(Inspection.Latitude!.Value, Inspection.Longitude!.Value, new MapLaunchOptions
            {
                Name = $"Акт огляду, аудиторія {Inspection.RoomNumber}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити застосунок карт");
            await Shell.Current.DisplayAlert("Карти недоступні", "Не вдалося відкрити застосунок карт.", "OK");
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => Shell.Current.GoToAsync("..");
}
