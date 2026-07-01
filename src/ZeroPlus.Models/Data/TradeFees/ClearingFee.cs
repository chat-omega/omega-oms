namespace ZeroPlus.Models.Data.TradeFees;

public class ClearingFee
{
    public int Id { get; set; }
    public int ClearingFirmId { get; set; }
    public double Fee { get; set; }
}