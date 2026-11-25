using SQLite;

namespace FuelErp.Data;

[Table("FuelTanks")]
public class FuelTankRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public int FuelType { get; set; }
    public double CapacityLiters { get; set; }
    public double CurrentLiters { get; set; }
    public DateTime UpdatedAt { get; set; }
}
