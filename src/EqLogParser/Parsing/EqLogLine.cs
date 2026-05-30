namespace EqLogParser.Parsing;

public sealed record EqLogLine(int Number, string Timestamp, string Message)
{
    public static EqLogLine FromText(int number, string text)
    {
        return new EqLogLine(
            number,
            GetTimestamp(text),
            GetMessage(text));
    }

    private static string GetMessage(string line)
    {
        var messageStart = line.IndexOf("] ", StringComparison.Ordinal);
        return messageStart >= 0 ? line[(messageStart + 2)..] : line;
    }

    private static string GetTimestamp(string line)
    {
        if (line.Length == 0 || line[0] != '[')
        {
            return string.Empty;
        }

        var timestampEnd = line.IndexOf(']', StringComparison.Ordinal);
        return timestampEnd > 1 ? line[1..timestampEnd] : string.Empty;
    }
}
