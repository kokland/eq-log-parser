namespace EqLogParser.Parsing;
/// <summary>Returns (MobName, SpellName) or null.</summary>
public interface IResistLineParser { (string MobName, string SpellName)? TryParse(string message); }
