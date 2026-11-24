using System.Globalization;
using System.Text.RegularExpressions;
using StudentCabinetMVVM.Models;

namespace StudentCabinetMVVM.Services;

public static class LogParser
{
    public static readonly string[] LevelNames = { "Debug", "Information", "Warning", "Error", "Fatal" };

    private static readonly Regex HeaderRegex = new(
        @"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) [+-]\d{2}:\d{2} \[(?<level>[A-Z]{3})\] ?(?<text>.*)$",
        RegexOptions.Compiled);

    public static List<LogEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<LogEntry>();
        LogEntry? current = null;

        foreach (var line in lines)
        {
            var match = HeaderRegex.Match(line);
            if (match.Success)
            {
                current = new LogEntry
                {
                    Timestamp = DateTime.ParseExact(
                        match.Groups["time"].Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                    Level = ToLevelName(match.Groups["level"].Value),
                    Message = match.Groups["text"].Value.Trim()
                };
                entries.Add(current);
            }
            else if (current is not null && line.Length > 0)
            {
                current.Details = current.Details.Length == 0
                    ? line
                    : current.Details + Environment.NewLine + line;
            }
        }

        return entries;
    }

    public static string ToLevelName(string abbreviation) => abbreviation switch
    {
        "DBG" or "VRB" => "Debug",
        "INF" => "Information",
        "WRN" => "Warning",
        "ERR" => "Error",
        "FTL" => "Fatal",
        _ => abbreviation
    };

    public static int Rank(string level)
    {
        var index = Array.IndexOf(LevelNames, level);
        return index < 0 ? 0 : index;
    }
}
