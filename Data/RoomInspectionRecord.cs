using SQLite;

namespace StudentCabinetMVVM.Data;

[Table("RoomInspections")]
public class RoomInspectionRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string RoomNumber { get; set; } = string.Empty;
    public DateTime InspectedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? PhotoPath { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime UpdatedAt { get; set; }
}
