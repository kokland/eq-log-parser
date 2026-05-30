namespace EqLogParser.Rendering;

public interface IDamageReportRenderer
{
    void Render(DamageReport report, TextWriter output);
}
