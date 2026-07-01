using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading;

public class ModifySmartRequest : IHaveRisk
{
    public string? LocalId { get; set; }
    public string? PermId { get; set; }
    public string? OrderId { get; set; }
    public string? Account { get; set; }
    public double Price { get; set; }
    public int Quantity { get; set; }
    public Venue? Venue { get; set; }
    public uint UserId { get; set; }
    public uint RiskCheckId { get; set; }
    public bool RiskCheckPassed { get; set; }
    public string? RiskCheckMessage { get; set; }
    public ScrapeStrategyData ScrapeStrategyData { get; } = new();

    public override string ToString() => string.Format("Local: {0}, Perm: {1}, Order: {2}, Price: {}, Quantity: {}", LocalId, PermId, OrderId, Price, Quantity);
}