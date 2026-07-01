namespace ZeroPlus.Models.Data.Models
{
    public class OrderRoutingInfoModel
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int VenueId { get; set; }
        public int OrderRouteId { get; set; }
        public int OrderTypeId { get; set; }
        public int BrokerId { get; set; }
        public int RouteTypeId { get; set; }
        public int RouteId { get; set; }
        public bool Active { get; set; }
        public string? Acronym { get; set; }
        public string? Venue { get; set; }
        public string? OrderType { get; set; }
        public string? Broker { get; set; }
        public string? RouteType { get; set; }
        public string? Route { get; set; }
        public string? ExpectedName { get; set; }
        public string? FixExpectedName { get; set; }
    }
}
