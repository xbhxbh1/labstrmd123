using FuelErp.Models;

namespace FuelErp.Services;

public interface ILogFileReader
{
    Task<IReadOnlyList<LogEntry>> ReadLastAsync(int count, string minLevel);
}
