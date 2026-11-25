namespace FuelErp.Models;

/// <summary>Незбережена форма нової заправки; зберігається при згортанні застосунку й відновлюється після повернення.</summary>
public class RefuelingDraft
{
    public int? VehicleId { get; set; }

    public int? TankId { get; set; }

    public string DriverName { get; set; } = string.Empty;

    public string LitersText { get; set; } = string.Empty;

    public string PriceText { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string? ReceiptPhotoPath { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool IsEmpty =>
        VehicleId is null && TankId is null
        && string.IsNullOrWhiteSpace(DriverName) && string.IsNullOrWhiteSpace(LitersText)
        && string.IsNullOrWhiteSpace(PriceText) && string.IsNullOrWhiteSpace(Notes)
        && string.IsNullOrEmpty(ReceiptPhotoPath) && Latitude is null && Longitude is null;
}
