using SQLite;

namespace FuelErp.Data;

[Table("Vehicles")]
public class VehicleRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string PlateNumber { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
    public int FuelType { get; set; }
    public DateTime UpdatedAt { get; set; }
}
