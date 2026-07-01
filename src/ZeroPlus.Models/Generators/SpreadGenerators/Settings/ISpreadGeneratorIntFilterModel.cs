namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings;

public interface ISpreadGeneratorIntFilter
{
    public bool Enabled { get; set; }
    public char Filter { get; set; }
    public int Value { get; set; }
}