namespace ZeroPlus.Models.Data.Update;

public class EdgeToTheoUpdateModel
{
    public double BuyEdgeToTheo { get; set; } = double.NaN;
    public double SellEdgeToTheo { get; set; } = double.NaN;
    public string? Symbol { get; set; }
}