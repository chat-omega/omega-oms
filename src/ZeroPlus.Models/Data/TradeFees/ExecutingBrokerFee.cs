namespace ZeroPlus.Models.Data.TradeFees;

public class ExecutingBrokerFee
{
    public int Id { get; set; }
    public int ExecutingBrokerId { get; set; }
    public double DmaFee { get; set; }
    public double SorFee { get; set; }
}