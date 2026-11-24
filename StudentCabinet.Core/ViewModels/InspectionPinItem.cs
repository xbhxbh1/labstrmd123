namespace StudentCabinetMVVM.ViewModels;

/// <summary>Елемент колекції маркерів карти (ItemsSource елемента Map прив'язується до колекції таких об'єктів).</summary>
public class InspectionPinItem
{
    public InspectionPinItem(double latitude, double longitude, string roomNumber, string description)
    {
        Latitude = latitude;
        Longitude = longitude;
        RoomNumber = roomNumber;
        Description = description;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public string RoomNumber { get; }

    public string Description { get; }
}
