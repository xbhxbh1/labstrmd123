using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using FuelErp.Data;
using FuelErp.Models;
using FuelErp.Services;

namespace FuelErp.ViewModels;

/// <summary>
/// Форма заправки (створення або редагування) з валідацією, фото чека, геолокацією точки заправки
/// і збереженням незавершеної форми в стані застосунку.
/// </summary>
public partial class RefuelingEditViewModel : ObservableObject, IQueryReceiver
{
    private readonly IRefuelingRepository _refuelings;
    private readonly IVehicleRepository _vehicles;
    private readonly ITankRepository _tanks;
    private readonly IPhotoService _photos;
    private readonly IDeviceLocationService _location;
    private readonly IAppStateService _state;
    private readonly ISessionService _session;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<RefuelingEditViewModel> _logger;
    private Refueling? _editing;
    private bool _restoring;
    private bool _isLoaded;

    public ObservableCollection<Vehicle> Vehicles { get; } = new();

    public ObservableCollection<FuelTank> Tanks { get; } = new();

    [ObservableProperty]
    private Vehicle? _selectedVehicle;

    [ObservableProperty]
    private FuelTank? _selectedTank;

    [ObservableProperty]
    private string _driverName = string.Empty;

    [ObservableProperty]
    private string _litersText = string.Empty;

    [ObservableProperty]
    private string _priceText = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto))]
    private string? _receiptPhotoPath;

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
    private string _title = "Нова заправка";

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    public bool HasPhoto => !string.IsNullOrEmpty(ReceiptPhotoPath);

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    public string LocationText => HasLocation
        ? $"Широта: {Latitude:F5}, довгота: {Longitude:F5}"
        : "Координати не визначено";

    public double Liters => TryParseNumber(LitersText, out var value) ? value : 0;

    public double PricePerLiter => TryParseNumber(PriceText, out var value) ? value : 0;

    public string TotalText => $"Сума: {Math.Round(Liters * PricePerLiter, 2):N2} грн";

    public double AvailableLiters => SelectedTank is null
        ? 0
        : SelectedTank.CurrentLiters + (_editing is not null && _editing.TankId == SelectedTank.Id ? _editing.Liters : 0);

    public string AvailableText => SelectedTank is null ? string.Empty : $"У резервуарі доступно: {AvailableLiters:N1} л";

    public RefuelingEditViewModel(
        IRefuelingRepository refuelings,
        IVehicleRepository vehicles,
        ITankRepository tanks,
        IPhotoService photos,
        IDeviceLocationService location,
        IAppStateService state,
        ISessionService session,
        INavigationService navigation,
        IDialogService dialogs,
        ILogger<RefuelingEditViewModel> logger)
    {
        _refuelings = refuelings;
        _vehicles = vehicles;
        _tanks = tanks;
        _photos = photos;
        _location = location;
        _state = state;
        _session = session;
        _navigation = navigation;
        _dialogs = dialogs;
        _logger = logger;
    }

    // Параметри навігації: SelectedRefueling означає редагування існуючої заправки
    public void ApplyQuery(IDictionary<string, object> query)
    {
        if (query.TryGetValue("SelectedRefueling", out var value) && value is Refueling refueling)
        {
            _editing = refueling;
            Title = "Редагування заправки";

            if (Vehicles.Count > 0)
                FillFromEditing();
        }
    }

    partial void OnSelectedVehicleChanged(Vehicle? value) => OnFieldChanged();

    partial void OnSelectedTankChanged(FuelTank? value)
    {
        // ціна за літр підставляється за видом палива, якщо оператор її ще не вказав
        if (!_restoring && value is not null && string.IsNullOrWhiteSpace(PriceText))
            PriceText = FuelPrices.For(value.FuelType).ToString("0.00", CultureInfo.InvariantCulture);

        OnFieldChanged();
    }

    partial void OnDriverNameChanged(string value) => OnFieldChanged();

    partial void OnLitersTextChanged(string value) => OnFieldChanged();

    partial void OnPriceTextChanged(string value) => OnFieldChanged();

    partial void OnNotesChanged(string value) => OnFieldChanged();

    partial void OnReceiptPhotoPathChanged(string? value) => OnFieldChanged();

    partial void OnLatitudeChanged(double? value) => OnFieldChanged();

    partial void OnLongitudeChanged(double? value) => OnFieldChanged();

    private void OnFieldChanged()
    {
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(AvailableText));
        Validate(out var message);
        ValidationMessage = message;
        SaveCommand.NotifyCanExecuteChanged();

        if (!_restoring)
            UpdateDraft();
    }

    private static bool TryParseNumber(string? text, out double value) =>
        double.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);

    /// <summary>Правила форми: обов'язкові поля, додатні числа, достатній залишок і відповідність виду палива.</summary>
    private bool Validate(out string message)
    {
        if (SelectedVehicle is null)
        {
            message = "Оберіть транспортний засіб";
            return false;
        }

        if (SelectedTank is null)
        {
            message = "Оберіть резервуар";
            return false;
        }

        if (string.IsNullOrWhiteSpace(DriverName))
        {
            message = "Вкажіть ПІБ водія";
            return false;
        }

        if (!TryParseNumber(LitersText, out var liters) || liters <= 0)
        {
            message = "Кількість літрів має бути більшою за нуль";
            return false;
        }

        if (!TryParseNumber(PriceText, out var price) || price <= 0)
        {
            message = "Ціна за літр має бути більшою за нуль";
            return false;
        }

        if (liters > AvailableLiters)
        {
            message = $"У резервуарі лише {AvailableLiters:N1} л";
            return false;
        }

        if (SelectedTank.FuelType != FuelType.Lubricant && SelectedVehicle.FuelType != SelectedTank.FuelType)
        {
            message = $"Транспортний засіб працює на «{SelectedVehicle.FuelType.ToDisplayName()}», " +
                      $"а в резервуарі «{SelectedTank.FuelType.ToDisplayName()}»";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool CanSave() => !IsBusy && Validate(out _);

    private bool CanUseDevice() => !IsBusy;

    /// <summary>Незавершена форма нової заправки зберігається у стані застосунку (відновлюється після згортання).</summary>
    private void UpdateDraft()
    {
        if (_editing is not null)
            return;

        _state.Draft = new RefuelingDraft
        {
            VehicleId = SelectedVehicle?.Id,
            TankId = SelectedTank?.Id,
            DriverName = DriverName,
            LitersText = LitersText,
            PriceText = PriceText,
            Notes = Notes,
            ReceiptPhotoPath = ReceiptPhotoPath,
            Latitude = Latitude,
            Longitude = Longitude
        };
    }

    private void FillFromEditing()
    {
        if (_editing is null)
            return;

        _restoring = true;
        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == _editing.VehicleId);
        SelectedTank = Tanks.FirstOrDefault(t => t.Id == _editing.TankId);
        DriverName = _editing.DriverName;
        LitersText = _editing.Liters.ToString("0.###", CultureInfo.InvariantCulture);
        PriceText = _editing.PricePerLiter.ToString("0.00", CultureInfo.InvariantCulture);
        Notes = _editing.Notes;
        ReceiptPhotoPath = _editing.ReceiptPhotoPath;
        Latitude = _editing.Latitude;
        Longitude = _editing.Longitude;
        _restoring = false;
        OnFieldChanged();
    }

    private void RestoreDraft(RefuelingDraft draft)
    {
        _restoring = true;
        SelectedVehicle = Vehicles.FirstOrDefault(v => v.Id == draft.VehicleId);
        SelectedTank = Tanks.FirstOrDefault(t => t.Id == draft.TankId);
        DriverName = draft.DriverName;
        LitersText = draft.LitersText;
        PriceText = draft.PriceText;
        Notes = draft.Notes;
        ReceiptPhotoPath = draft.ReceiptPhotoPath;
        Latitude = draft.Latitude;
        Longitude = draft.Longitude;
        _restoring = false;
        OnFieldChanged();
        _logger.LogInformation("Відновлено незбережену форму нової заправки");
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        // сторінка може з'являтися знову (наприклад, після камери): введені дані не затираються
        if (_isLoaded)
            return;

        try
        {
            var vehicles = await _vehicles.GetAllAsync();
            var tanks = await _tanks.GetAllAsync();

            Vehicles.Clear();
            foreach (var vehicle in vehicles)
                Vehicles.Add(vehicle);

            Tanks.Clear();
            foreach (var tank in tanks)
                Tanks.Add(tank);

            if (_editing is not null)
                FillFromEditing();
            else if (_state.Draft is { IsEmpty: false } draft)
                RestoreDraft(draft);

            _isLoaded = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося завантажити довідники для форми заправки");
            await _dialogs.AlertAsync("Помилка", "Не вдалося завантажити транспорт і резервуари.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        // Другий рівень захисту: роль перевіряється в команді, а не лише приховуванням кнопки
        if (!_session.IsInRole(UserRole.StationOperator))
        {
            _logger.LogWarning("Відмовлено в доступі: користувач {User} (роль {Role}) намагався зберегти заправку",
                _session.UserName, _session.CurrentRole);
            await _dialogs.AlertAsync("Доступ заборонено", "Реєструвати заправки може лише оператор АЗС.");
            return;
        }

        if (!Validate(out var message))
        {
            ValidationMessage = message;
            return;
        }

        var refueling = new Refueling
        {
            Id = _editing?.Id ?? 0,
            VehicleId = SelectedVehicle!.Id,
            TankId = SelectedTank!.Id,
            DriverName = DriverName.Trim(),
            Liters = Liters,
            PricePerLiter = PricePerLiter,
            RefueledAt = _editing?.RefueledAt ?? DateTime.Now,
            ReceiptPhotoPath = ReceiptPhotoPath,
            Latitude = Latitude,
            Longitude = Longitude,
            Notes = Notes.Trim(),
            OperatorName = _editing?.OperatorName is { Length: > 0 } existing ? existing : _session.UserName ?? string.Empty,
            VehiclePlate = SelectedVehicle.PlateNumber,
            TankName = SelectedTank.Name,
            FuelType = SelectedTank.FuelType
        };

        await RunBusyAsync(async () =>
        {
            try
            {
                await _refuelings.SaveAsync(refueling);
            }
            catch (InsufficientFuelException ex)
            {
                await _dialogs.AlertAsync("Недостатньо палива",
                    $"У резервуарі «{ex.TankName}» лише {ex.AvailableLiters:N1} л, а потрібно {ex.RequestedLiters:N1} л.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не вдалося зберегти заправку");
                await _dialogs.AlertAsync("Помилка", "Не вдалося зберегти заправку.");
                return;
            }

            _state.Draft = null;
            await _navigation.GoBackAsync(new Dictionary<string, object> { ["SavedRefueling"] = refueling });
        });
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        _state.Draft = null;
        await _navigation.GoBackAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseDevice))]
    private async Task TakePhotoAsync() =>
        await RunBusyAsync(async () => await ApplyPhotoAsync(await _photos.TakePhotoAsync(SelectedVehicle?.PlateNumber ?? "receipt")));

    [RelayCommand(CanExecute = nameof(CanUseDevice))]
    private async Task PickPhotoAsync() =>
        await RunBusyAsync(async () => await ApplyPhotoAsync(await _photos.PickPhotoAsync(SelectedVehicle?.PlateNumber ?? "receipt")));

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
                        "Без дозволу на геолокацію точку заправки визначити неможливо. " +
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

    private async Task ApplyPhotoAsync(PhotoResult result)
    {
        switch (result.Status)
        {
            case PhotoStatus.Success:
                ReceiptPhotoPath = result.Path;
                break;
            case PhotoStatus.Cancelled:
                break; // користувач сам скасував зйомку: повідомляти нічого не потрібно
            case PhotoStatus.PermissionDenied:
                await _dialogs.AlertAsync("Немає дозволу",
                    "Без дозволу на камеру сфотографувати чек або лічильник неможливо. " +
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
