using Microsoft.Extensions.ObjectPool;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update;

public class OrderRisk : IHaveRisk, IResettable
{
    public uint UserId { get; set; }
    public uint RiskCheckId { get; set; }
    public bool RiskCheckPassed { get; set; }
    public string? RiskCheckMessage { get; set; }
    public bool IsOpening { get; set; }
    public Venue? Venue { get; set; }
    public Side? Side { get; set; }
    public Broker? Broker { get; set; }
    public Exchange? Exchange { get; set; }
    public OrderSubType? SubType { get; set; }
    public int Qty { get; set; }
    public double Price { get; set; }
    public string? Route { get; set; }
    public BaseStrategy BaseStrategy { get; set; }
    public double StrikeSpacing { get; set; }
    public string? OrderId { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public string? Description { get; set; }
    public string? Symbol { get; set; }

    public bool TryReset()
    {
        UserId = default;
        RiskCheckId = default;
        IsOpening = default;
        Venue = default;
        Side = default;
        Broker = default;
        Exchange = default;
        SubType = default;
        Qty = default;
        Price = default;
        Route = default;
        BaseStrategy = default;
        StrikeSpacing = default;
        OrderId = default;
        UnderlyingSymbol = default;
        Description = default;
        Symbol = default;
        RiskCheckPassed = default;
        RiskCheckMessage = default;
        return true;
    }
}