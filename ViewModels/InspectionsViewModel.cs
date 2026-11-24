using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Data;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.ViewModels;

public partial class InspectionsViewModel : ObservableObject
{
    private readonly IInspectionRepository _repository;
    private readonly IMap _map;
    private readonly ILogger<InspectionsViewModel> _logger;

    public ObservableCollection<RoomInspection> Inspections { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    public InspectionsViewModel(IInspectionRepository repository, IMap map, ILogger<InspectionsViewModel> logger)
    {
        _repository = repository;
        _map = map;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            var items = await _repository.GetAllAsync();

            Inspections.Clear();
            foreach (var item in items)
                Inspections.Add(item);

            Status = $"Актів огляду: {Inspections.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити акти огляду");
            await Shell.Current.DisplayAlert("Помилка", "Не вдалося завантажити акти огляду.", "OK");
        }
    }

    [RelayCommand]
    private Task NewInspectionAsync() => Shell.Current.GoToAsync("inspection/new");

    [RelayCommand]
    private async Task OpenDetailsAsync(RoomInspection? inspection)
    {
        if (inspection is null)
            return;

        await Shell.Current.GoToAsync("inspection/detail", new Dictionary<string, object>
        {
            ["SelectedInspection"] = inspection
        });
    }

    [RelayCommand]
    private async Task ShowOnMapAsync(RoomInspection? inspection)
    {
        if (inspection is null)
            return;

        if (!inspection.HasLocation)
        {
            await Shell.Current.DisplayAlert("Немає координат", "Для цього акта координати не визначались.", "OK");
            return;
        }

        try
        {
            await _map.OpenAsync(inspection.Latitude!.Value, inspection.Longitude!.Value, new MapLaunchOptions
            {
                Name = $"Акт огляду, аудиторія {inspection.RoomNumber}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити застосунок карт");
            await Shell.Current.DisplayAlert("Карти недоступні", "Не вдалося відкрити застосунок карт.", "OK");
        }
    }
}
