namespace ZeroPlus.Models.Data.TradeFees;

public class SymbolModel
{
    public int Id { get; set; }
    public string? Symbol { get; set; }
    public PennyProgram Penny { get; set; }
}