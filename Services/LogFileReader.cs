using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public class LogFileReader : ILogFileReader
{
    private readonly string? _directory;

    public LogFileReader()
    {
    }

    public LogFileReader(string directory)
    {
        _directory = directory;
    }

    private string LogsDirectory => _directory ?? AppPaths.LogsDirectory;

    public async Task<IReadOnlyList<LogEntry>> ReadLastAsync(int count, string minLevel)
    {
        if (!Directory.Exists(LogsDirectory))
            return Array.Empty<LogEntry>();

        var minRank = LogParser.Rank(minLevel);
        var files = Directory.GetFiles(LogsDirectory, "app-*.log")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .ToList();

        var result = new List<LogEntry>();
        foreach (var file in files)
        {
            var lines = await ReadAllLinesSharedAsync(file);
            var entries = LogParser.Parse(lines)
                .Where(e => LogParser.Rank(e.Level) >= minRank)
                .ToList();

            result.InsertRange(0, entries);

            if (result.Count >= count)
                break;
        }

        return result.Skip(Math.Max(0, result.Count - count)).ToList();
    }

    private static async Task<List<string>> ReadAllLinesSharedAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);

        var lines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
            lines.Add(line);

        return lines;
    }
}
