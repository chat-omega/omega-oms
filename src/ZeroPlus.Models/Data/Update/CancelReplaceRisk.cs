namespace ZeroPlus.Models.Data.Update;

public class CancelReplaceRisk : CancelRisk
{
    public int NewQty { get; set; }
    public double NewPrice { get; set; }
    public string? NewRoute { get; set; }
}