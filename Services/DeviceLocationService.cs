using Microsoft.Extensions.Logging;

namespace StudentCabinetMVVM.Services;

public class DeviceLocationService : IDeviceLocationService
{
    private readonly IGeolocation _geolocation;
    private readonly IPermissionService _permissions;
    private readonly ILogger<DeviceLocationService> _logger;

    public DeviceLocationService(IGeolocation geolocation, IPermissionService permissions,
        ILogger<DeviceLocationService> logger)
    {
        _geolocation = geolocation;
        _permissions = permissions;
        _logger = logger;
    }

    public async Task<LocationResult> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await _permissions.EnsureAsync<Permissions.LocationWhenInUse>())
            {
                _logger.LogWarning("Місцеперебування не визначено: користувач не надав дозвіл на геолокацію");
                return new LocationResult(LocationStatus.PermissionDenied);
            }

            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

            // свіжий запит; якщо він нічого не повернув, беремо останнє відоме місцеперебування
            var location = await _geolocation.GetLocationAsync(request, cancellationToken)
                           ?? await _geolocation.GetLastKnownLocationAsync();

            if (location is null)
            {
                _logger.LogWarning("Не вдалося отримати координати: тайм-аут або відсутні дані про місцеперебування");
                return new LocationResult(LocationStatus.Unavailable);
            }

            // самі координати до журналу не записуються (приватність, лабораторна робота 5)
            _logger.LogInformation("Місцеперебування визначено, точність {Accuracy} м", location.Accuracy);
            return new LocationResult(LocationStatus.Success, location.Latitude, location.Longitude);
        }
        catch (FeatureNotEnabledException ex)
        {
            _logger.LogWarning(ex, "Служби геолокації (GPS) вимкнено в налаштуваннях пристрою");
            return new LocationResult(LocationStatus.FeatureDisabled);
        }
        catch (FeatureNotSupportedException ex)
        {
            _logger.LogWarning(ex, "Геолокація не підтримується цим пристроєм");
            return new LocationResult(LocationStatus.NotSupported);
        }
        catch (PermissionException ex)
        {
            _logger.LogWarning(ex, "Система відмовила в доступі до геолокації");
            return new LocationResult(LocationStatus.PermissionDenied);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Визначення місцеперебування скасовано");
            return new LocationResult(LocationStatus.Unavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося визначити місцеперебування");
            return new LocationResult(LocationStatus.Failed);
        }
    }
}
