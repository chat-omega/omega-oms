namespace ZeroPlus.Models.Data.TradeFees;

public class SingleListedExchangeFees
{
    public int Id { get; set; }
    public int ExecutingBrokerId { get; set; }
    public int ExchangeId { get; set; }
    public int SymbolId { get; set; }
    public Liquidity LiquidityType { get; set; }
    public LegType LegType { get; set; }
    public double Fee { get; set; }
}