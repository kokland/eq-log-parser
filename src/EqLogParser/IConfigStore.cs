namespace EqLogParser;

public interface IConfigStore
{
    AppConfig Load();
    void Save(AppConfig config);
}
