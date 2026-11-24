namespace StudentCabinetMVVM.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    public string Header => $"{Timestamp:HH:mm:ss.fff} [{Level}] {Message}";
    public bool HasDetails => !string.IsNullOrEmpty(Details);
}
