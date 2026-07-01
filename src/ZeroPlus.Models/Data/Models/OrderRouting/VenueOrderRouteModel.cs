namespace ZeroPlus.Models.Data.Models.OrderRouting;

public class VenueOrderRouteModel
{
    public int Id { get; set; }
    public int VenueId { get; set; }
    public int OrderRouteId { get; set; }
    public bool Active { get; set; }
}
