using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StudentCabinetMVVM.Data;
using StudentCabinetMVVM.Models;
using StudentCabinetMVVM.Services;

namespace StudentCabinetMVVM.ViewModels;

/// <summary>Створення акта огляду: фото дефекту, координати місця огляду, збереження в SQLite.</summary>
public partial class InspectionEditViewModel : ObservableObject, IQueryReceiver
{
    private readonly IInspectionRepository _repository;
    private readonly IPhotoService _photos;
    private readonly IDeviceLocationService _location;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<InspectionEditViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _roomNumber = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string? _photoPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    [NotifyPropertyChangedFor(nameof(LocationText))]
    private double? _latitude;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocation))]
    [NotifyPropertyChangedFor(nameof(LocationText))]
    private double? _longitude;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(TakePhotoCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickPhotoCommand))]
    [NotifyCanExecuteChangedFor(nameof(GetLocationCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = "Новий акт огляду";

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath);

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    public string LocationText => HasLocation
        ? $"Широта: {Latitude:F5}, довгота: {Longitude:F5}"
        : "Координати не визначено";

    public InspectionEditViewModel(
        IInspectionRepository repository,
        IPhotoService photos,
        IDeviceLocationService location,
        INavigationService navigation,
        IDialogService dialogs,
        ILogger<InspectionEditViewModel> logger)
    {
        _repository = repository;
        _photos = photos;
        _location = location;
        _navigation = navigation;
        _dialogs = dialogs;
        _logger = logger;
    }

    // Сторінку відкрито за струшуванням: це швидке повідомлення про інцидент
    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("IsIncident", out var value) && value is true)
        {
            Title = "Повідомлення про інцидент";
            if (string.IsNullOrEmpty(Description))
                Description = "ІНЦИДЕНТ: ";
        }
    }

    private bool CanUseDevice() => !IsBusy;

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(RoomNumber);

    [RelayCommand(CanExecute = nameof(CanUseDevice))]
    private async Task TakePhotoAsync()
    {
        await RunBusyAsync(async () => await ApplyPhotoAsync(await _photos.TakePhotoAsync(RoomNumber.Trim())));
    }

    [RelayCommand(CanExecute = nameof(CanUseDevice))]
    private async Task PickPhotoAsync()
    {
        await RunBusyAsync(async () => await ApplyPhotoAsync(await _photos.PickPhotoAsync(RoomNumber.Trim())));
    }

    [RelayCommand(CanExecute = nameof(CanUseDevice))]
    private async Task GetLocationAsync()
    {
        await RunBusyAsync(async () =>
        {
            var result = await _location.GetCurrentAsync();

            switch (result.Status)
            {
                case LocationStatus.Success:
                    Latitude = result.Latitude;
                    Longitude = result.Longitude;
                    break;
                case LocationStatus.PermissionDenied:
                    await _dialogs.AlertAsync("Немає дозволу",
                        "Без дозволу на геолокацію місце огляду визначити неможливо. " +
                        "Дозвольте доступ до місцеперебування в налаштуваннях системи.");
                    break;
                case LocationStatus.FeatureDisabled:
                    await _dialogs.AlertAsync("Геолокацію вимкнено",
                        "Служби геолокації (GPS) вимкнено. Увімкніть їх у налаштуваннях пристрою й повторіть спробу.");
                    break;
                case LocationStatus.NotSupported:
                    await _dialogs.AlertAsync("Геолокація недоступна",
                        "Цей пристрій не підтримує визначення місцеперебування.");
                    break;
                case LocationStatus.Unavailable:
                    await _dialogs.AlertAsync("Координати не отримано",
                        "Не вдалося визначити місцеперебування. Спробуйте ще раз у місці з кращим сигналом.");
                    break;
                default:
                    await _dialogs.AlertAsync("Помилка", "Не вдалося визначити місцеперебування.");
                    break;
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var inspection = new RoomInspection
        {
            RoomNumber = RoomNumber.Trim(),
            InspectedAt = DateTime.Now,
            Description = Description.Trim(),
            PhotoPath = PhotoPath,
            Latitude = Latitude,
            Longitude = Longitude
        };

        await RunBusyAsync(async () =>
        {
            try
            {
                await _repository.SaveAsync(inspection);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не вдалося зберегти акт огляду");
                await _dialogs.AlertAsync("Помилка", "Не вдалося зберегти акт огляду.");
                return;
            }

            await _navigation.GoBackAsync();
        });
    }

    [RelayCommand]
    private Task CancelAsync() => _navigation.GoBackAsync();

    private async Task ApplyPhotoAsync(PhotoResult result)
    {
        switch (result.Status)
        {
            case PhotoStatus.Success:
                PhotoPath = result.Path;
                break;
            case PhotoStatus.Cancelled:
                break; // користувач сам скасував зйомку: повідомляти нічого не потрібно
            case PhotoStatus.PermissionDenied:
                await _dialogs.AlertAsync("Немає дозволу",
                    "Без дозволу на камеру сфотографувати дефект неможливо. " +
                    "Дозвольте доступ до камери в налаштуваннях системи або оберіть фото з галереї.");
                break;
            case PhotoStatus.NotSupported:
                await _dialogs.AlertAsync("Камера недоступна",
                    "На цьому пристрої немає камери або вона недоступна. Можна обрати наявне фото з галереї.");
                break;
            default:
                await _dialogs.AlertAsync("Помилка", "Не вдалося отримати фото. Спробуйте ще раз.");
                break;
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
