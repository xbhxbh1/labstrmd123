using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

/// <summary>Довідник транспортних засобів: перегляд і додавання з перевіркою дубля номера.</summary>
public partial class VehiclesViewModel : ObservableObject
{
    private readonly IVehicleRepository _repository;
    private readonly IFuelDataSeeder _seeder;
    private readonly INavigationService _navigation;
    private readonly ILogger<VehiclesViewModel> _logger;

    public ObservableCollection<Vehicle> Vehicles { get; } = new();

    public IReadOnlyList<FuelTypeOption> FuelTypes => FuelTypeOption.All;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddVehicleCommand))]
    private string _newPlate = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddVehicleCommand))]
    private string _newModel = string.Empty;

    [ObservableProperty]
    private FuelTypeOption _newFuelType = FuelTypeOption.All[0];

    [ObservableProperty]
    private string _status = string.Empty;

    public VehiclesViewModel(IVehicleRepository repository, IFuelDataSeeder seeder, INavigationService navigation,
        ILogger<VehiclesViewModel> logger)
    {
        _repository = repository;
        _seeder = seeder;
        _navigation = navigation;
        _logger = logger;
    }

    private bool CanAdd() =>
        Vehicle.NormalizePlate(NewPlate).Length >= 4 && !string.IsNullOrWhiteSpace(NewModel);

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            await _seeder.SeedAsync();
            var vehicles = await _repository.GetAllAsync();

            Vehicles.Clear();
            foreach (var vehicle in vehicles)
                Vehicles.Add(vehicle);

            Status = $"Транспортних засобів: {Vehicles.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити транспортні засоби");
            Status = "Не вдалося завантажити транспортні засоби";
        }
    }

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddVehicleAsync()
    {
        var vehicle = new Vehicle
        {
            PlateNumber = NewPlate,
            Model = NewModel,
            FuelType = NewFuelType.Type
        };

        try
        {
            await _repository.SaveAsync(vehicle);
        }
        catch (DuplicateVehicleException ex)
        {
            Status = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося додати транспортний засіб");
            Status = "Не вдалося зберегти транспортний засіб";
            return;
        }

        NewPlate = string.Empty;
        NewModel = string.Empty;
        await LoadAsync();
        Status = $"Додано: {vehicle.DisplayName}";
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
