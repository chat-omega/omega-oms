using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Config
{
    public delegate double GetDouble(bool wait);

    public interface IDynamicEdgeModel : IDynamicConfigModel
    {
        DynamicEdgeConfigModel GetConfig();
        bool GetEdge(bool fish,
                     BaseStrategy strategy,
                     string underlyingSymbol,
                     double underlying,
                     double strikeSpacing,
                     int daysToExpiration,
                     int contracts,
                     int minOfBidAskSize,
                     double delta,
                     double width,
                     double minTick,
                     GetDouble getWeightedVega,
                     out double edge,
                     out double loopMinEdge,
                     out double loopMaxLoss,
                     out double maxThroughTheo,
                     out double maxThroughVola,
                     out TheoModel volaModel,
                     out double maxPercentBid,
                     out double maxThroughEma,
                     out double maxThroughTradePx,
                     out double minMarketWidth,
                     out double minMarketCross,
                     out int qty,
                     out double permMinEdge,
                     out string reason);
    }
}