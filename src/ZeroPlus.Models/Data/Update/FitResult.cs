namespace ZeroPlus.Models.Data.Update;

public class FitResult
{
    public uint Index { get; set; }
    public double Theo { get; set; } = double.NaN;

    public double Delta { get; set; } = double.NaN;
    public double Gamma { get; set; } = double.NaN;
    public double Vega { get; set; } = double.NaN;
    public double Iv { get; set; } = double.NaN;
}