using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Services;

namespace FuelErp.ViewModels;

public partial class InspectionsMapViewModel : ObservableObject
{
    private readonly IInspectionRepository _repository;
    private readonly IPermissionService _permissions;
    private readonly INavigationService _navigation;
    private readonly ILogger<InspectionsMapViewModel> _logger;

    public ObservableCollection<InspectionPinItem> Pins { get; } = new();

    [ObservableProperty]
    private string _status = string.Empty;

    public InspectionsMapViewModel(
        IInspectionRepository repository,
        IPermissionService permissions,
        INavigationService navigation,
        ILogger<InspectionsMapViewModel> logger)
    {
        _repository = repository;
        _permissions = permissions;
        _navigation = navigation;
        _logger = logger;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        try
        {
            // дозвіл потрібен лише для позначки «моє місцезнаходження» (IsShowingUser); без нього карта працює
            await _permissions.EnsureAsync(DevicePermission.Location);

            var inspections = await _repository.GetAllAsync();

            Pins.Clear();
            foreach (var inspection in inspections.Where(i => i.HasLocation))
            {
                Pins.Add(new InspectionPinItem(
                    inspection.Latitude!.Value,
                    inspection.Longitude!.Value,
                    inspection.RoomNumber,
                    inspection.Description));
            }

            Status = Pins.Count == 0
                ? "Немає актів огляду з координатами"
                : $"Актів огляду на карті: {Pins.Count}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити маркери карти");
            Status = "Не вдалося завантажити маркери карти";
        }
    }

    [RelayCommand]
    private Task GoBackAsync() => _navigation.GoBackAsync();
}
