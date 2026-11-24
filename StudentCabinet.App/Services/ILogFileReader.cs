using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public interface ILogFileReader
{
    Task<IReadOnlyList<LogEntry>> ReadLastAsync(int count, string minLevel);
}
