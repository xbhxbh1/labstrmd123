namespace FuelErp.Models;

/// <summary>Заправка: видача палива або мастила з резервуара транспортному засобу (Заправка).</summary>
public class Refueling
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    public int TankId { get; set; }

    /// <summary>ПІБ водія: персональні дані, у базі зберігаються лише в зашифрованому вигляді.</summary>
    public string DriverName { get; set; } = string.Empty;

    public double Liters { get; set; }

    public double PricePerLiter { get; set; }

    public DateTime RefueledAt { get; set; }

    /// <summary>Шлях до фото чека або лічильника в каталозі застосунку (самі байти в базі не зберігаються).</summary>
    public string? ReceiptPhotoPath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string Notes { get; set; } = string.Empty;

    public string OperatorName { get; set; } = string.Empty;

    // Поля для відображення, які заповнює репозиторій із пов'язаних таблиць
    public string VehiclePlate { get; set; } = string.Empty;

    public string TankName { get; set; } = string.Empty;

    public FuelType FuelType { get; set; }

    public double Total => Math.Round(Liters * PricePerLiter, 2);

    public bool HasPhoto => !string.IsNullOrEmpty(ReceiptPhotoPath);

    public bool HasLocation => Latitude.HasValue && Longitude.HasValue;

    public string LocationText => HasLocation
        ? $"Широта: {Latitude:F5}, довгота: {Longitude:F5}"
        : "Координати не визначено";

    public string Title => $"{VehiclePlate} · {Liters:0.##} л";

    public string Subtitle => $"{RefueledAt:dd.MM.yyyy HH:mm} · {FuelType.ToDisplayName()} · {Total:N2} грн";
}
