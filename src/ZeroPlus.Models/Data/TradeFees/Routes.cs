namespace ZeroPlus.Models.Data.TradeFees;

public class Routes
{
    public int Id { get; set; }
    public int ExecutingBrokerId { get; set; }
    public RouteType Type { get; set; }
    public string? Name { get; set; }
}