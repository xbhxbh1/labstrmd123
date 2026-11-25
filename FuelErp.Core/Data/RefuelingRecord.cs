using SQLite;

namespace FuelErp.Data;

[Table("Refuelings")]
public class RefuelingRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int VehicleId { get; set; }

    [Indexed]
    public int TankId { get; set; }

    /// <summary>ПІБ водія в зашифрованому вигляді (AES, Base64).</summary>
    public string DriverNameEncrypted { get; set; } = string.Empty;

    public double Liters { get; set; }
    public double PricePerLiter { get; set; }
    public DateTime RefueledAt { get; set; }
    public string? ReceiptPhotoPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

    /// <summary>Додано міграцією схеми v1 -> v2.</summary>
    public string Notes { get; set; } = string.Empty;
}
