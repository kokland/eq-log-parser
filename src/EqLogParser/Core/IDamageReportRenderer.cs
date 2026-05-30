using EqLogParser.Core.Domain;
namespace EqLogParser.Core;

public interface IDamageReportRenderer
{
    void Render(DamageReport report);

    void RenderWatch(
        DamageReport          initialReport,
        Func<DamageReport>    refresh,
        TimeSpan              interval,
        CancellationToken     cancellationToken = default);
}
