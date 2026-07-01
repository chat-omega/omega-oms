namespace ZeroPlus.Models.Data.Update;

public interface IPriceChainModel : IEdgeScanFeedModel
{
    int PriceChainTotalBidDeviations { get; set; }
    int PriceChainTotalAskDeviations { get; set; }
    int PriceChainDeviationSequence { get; set; }
    double PriceChainTradePrice { get; set; }
    double PriceChainRecentBidDeviation { get; set; }
    double PriceChainRecentBidDeviationTimeDiff { get; set; }
    double PriceChainRecentBidDeviationUnderBid { get; set; }
    double PriceChainRecentBidDeviationUnderAsk { get; set; }
    double PriceChainRecentBidDeviationBid { get; set; }
    double PriceChainRecentBidDeviationAsk { get; set; }
    double PriceChainRecentAskDeviation { get; set; }
    double PriceChainRecentAskDeviationTimeDiff { get; set; }
    double PriceChainRecentAskDeviationUnderBid { get; set; }
    double PriceChainRecentAskDeviationUnderAsk { get; set; }
    double PriceChainRecentAskDeviationBid { get; set; }
    double PriceChainRecentAskDeviationAsk { get; set; }
    double PriceChainHighestBidDeviation { get; set; }
    double PriceChainHighestBidDeviationTimeDiff { get; set; }
    double PriceChainHighestBidDeviationUnderBid { get; set; }
    double PriceChainHighestBidDeviationUnderAsk { get; set; }
    double PriceChainHighestBidDeviationBid { get; set; }
    double PriceChainHighestBidDeviationAsk { get; set; }
    double PriceChainHighestAskDeviation { get; set; }
    double PriceChainHighestAskDeviationTimeDiff { get; set; }
    double PriceChainHighestAskDeviationUnderBid { get; set; }
    double PriceChainHighestAskDeviationUnderAsk { get; set; }
    double PriceChainHighestAskDeviationBid { get; set; }
    double PriceChainHighestAskDeviationAsk { get; set; }
    double PriceChainRecentBidDeviationIvOffset { get; set; }
    double PriceChainHighestBidDeviationIvOffset { get; set; }
    double PriceChainRecentAskDeviationIvOffset { get; set; }
    double PriceChainHighestAskDeviationIvOffset { get; set; }
}