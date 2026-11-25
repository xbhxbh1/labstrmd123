namespace FuelErp.Models;

/// <summary>У резервуарі менше палива, ніж потрібно для заправки.</summary>
public class InsufficientFuelException : InvalidOperationException
{
    public InsufficientFuelException(string tankName, double availableLiters, double requestedLiters)
        : base($"У резервуарі «{tankName}» лише {availableLiters:0.##} л, потрібно {requestedLiters:0.##} л")
    {
        TankName = tankName;
        AvailableLiters = availableLiters;
        RequestedLiters = requestedLiters;
    }

    public string TankName { get; }

    public double AvailableLiters { get; }

    public double RequestedLiters { get; }
}

/// <summary>Транспортний засіб із таким номером уже є в базі.</summary>
public class DuplicateVehicleException : InvalidOperationException
{
    public DuplicateVehicleException(string plateNumber)
        : base($"Транспортний засіб {plateNumber} уже зареєстровано")
    {
        PlateNumber = plateNumber;
    }

    public string PlateNumber { get; }
}
