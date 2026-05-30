namespace EqLogParser.Parsing;

public sealed class MobNameNormalizer : IMobNameNormalizer
{
    public string Normalize(string mobName)
    {
        mobName = mobName.Trim();
        if (mobName.Length < 2)
        {
            return mobName;
        }

        return char.ToUpperInvariant(mobName[0]) + mobName[1..];
    }
}
