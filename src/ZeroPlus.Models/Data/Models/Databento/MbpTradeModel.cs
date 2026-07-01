namespace ZeroPlus.Models.Data.Models.Databento;

public class MbpTradeModel
{
    public DbPublisher Publisher { get; set; }
    public uint InstrumentId { get; set; }
    public ulong TsEvent { get; set; }
    public ulong TsRecv { get; set; }
    public double Price { get; set; }
    public uint Size { get; set; }
    public DbAction Action { get; set; }
    public DbSide Side { get; set; }
    public DbFlagSet Flags { get; set; }
    public byte Depth { get; set; }
    public uint Sequence { get; set; }
    public string? Symbol { get; set; }
}