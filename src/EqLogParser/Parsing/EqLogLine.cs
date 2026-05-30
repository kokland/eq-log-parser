using System.Globalization;

namespace EqLogParser.Parsing;

public sealed record EqLogLine(int Number, string Timestamp, string Message, DateTime? ParsedTimestamp)
{
    // Two formats covering 2-digit and space-padded 1-digit day fields.
    private static readonly string[] TimestampFormats =
    [
        "ddd MMM dd HH:mm:ss yyyy",
        "ddd MMM  d HH:mm:ss yyyy"
    ];

    public static EqLogLine FromText(int number, string text)
    {
        var ts = GetTimestamp(text);
        return new EqLogLine(number, ts, GetMessage(text), ParseTimestamp(ts));
    }

    private static string GetMessage(string line)
    {
        var messageStart = line.IndexOf("] ", StringComparison.Ordinal);
        return messageStart >= 0 ? line[(messageStart + 2)..] : line;
    }

    private static string GetTimestamp(string line)
    {
        if (line.Length == 0 || line[0] != '[')
            return string.Empty;

        var end = line.IndexOf(']', StringComparison.Ordinal);
        return end > 1 ? line[1..end] : string.Empty;
    }

    private static DateTime? ParseTimestamp(string raw)
    {
        if (raw.Length == 0) return null;
        return DateTime.TryParseExact(
            raw, TimestampFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var result)
            ? result : null;
    }
}
