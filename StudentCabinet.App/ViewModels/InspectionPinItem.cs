namespace StudentCabinetMVVM.ViewModels;

/// <summary>Елемент колекції маркерів карти (ItemsSource елемента Map прив'язується до колекції таких об'єктів).</summary>
public class InspectionPinItem
{
    public InspectionPinItem(Location location, string roomNumber, string description)
    {
        Location = location;
        RoomNumber = roomNumber;
        Description = description;
    }

    public Location Location { get; }

    public string RoomNumber { get; }

    public string Description { get; }
}
