namespace ZeroPlus.Models.Data.TradeFees;

public class ExchangeFee
{
    public int Id { get; set; }
    public int ExecutingBrokerId { get; set; }
    public int ExchangeId { get; set; }
    public PennyProgram Penny { get; set; }
    public Liquidity LiquidityType { get; set; }
    public LegType LegType { get; set; }
    public double Fee { get; set; }
}