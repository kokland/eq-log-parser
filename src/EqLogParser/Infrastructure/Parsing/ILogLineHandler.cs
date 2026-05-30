namespace EqLogParser.Infrastructure.Parsing;

/// <summary>
/// A single line-type concern. Handle returns true if the line was consumed
/// (stops further processing). Reset clears all accumulated state.
/// </summary>
public interface ILogLineHandler
{
    bool Handle(EqLogLine line);
    void Reset();
}
