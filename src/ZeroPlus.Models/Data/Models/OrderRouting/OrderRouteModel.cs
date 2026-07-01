namespace ZeroPlus.Models.Data.Models.OrderRouting;

public class OrderRouteModel
{
    public int Id { get; set; }
    public int RouteId { get; set; }
    public int BrokerId { get; set; }
    public int OrderType { get; set; }
    public string VenueExpectedName { get; set; } = string.Empty;
    public string? FixExpectedName { get; set; }
}
