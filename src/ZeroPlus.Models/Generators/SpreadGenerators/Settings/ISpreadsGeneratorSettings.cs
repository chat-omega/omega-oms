namespace ZeroPlus.Models.Generators.SpreadGenerators.Settings;

public interface ISpreadsGeneratorSettings
{
    public bool WidthSortingEnabled { get; set; }

    bool DataRequested();
}