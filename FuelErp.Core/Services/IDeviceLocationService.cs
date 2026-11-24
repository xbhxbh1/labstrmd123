namespace FuelErp.Services;

public enum LocationStatus
{
    Success,
    PermissionDenied,
    FeatureDisabled,
    NotSupported,
    Unavailable,
    Failed
}

public record LocationResult(LocationStatus Status, double Latitude = 0, double Longitude = 0);

public interface IDeviceLocationService
{
    Task<LocationResult> GetCurrentAsync(CancellationToken cancellationToken = default);
}
