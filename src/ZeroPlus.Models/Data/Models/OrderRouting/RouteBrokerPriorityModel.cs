namespace ZeroPlus.Models.Data.Models.OrderRouting;

public class RouteBrokerPriorityModel
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int BrokerId { get; set; }
    public int Priority { get; set; }
}
