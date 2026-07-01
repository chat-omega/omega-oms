namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings.Generic;

public class GenericSpreadGeneratorIntFilter : ISpreadGeneratorIntFilter
{
    public bool Enabled { get; set; }
    public char Filter { get; set; }
    public int Value { get; set; }
}