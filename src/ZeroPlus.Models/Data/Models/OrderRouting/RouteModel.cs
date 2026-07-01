namespace ZeroPlus.Models.Data.Models.OrderRouting;

public class RouteModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RouteType { get; set; }
    public bool Active { get; set; }
}
