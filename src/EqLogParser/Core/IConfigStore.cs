namespace EqLogParser.Core;

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
}
