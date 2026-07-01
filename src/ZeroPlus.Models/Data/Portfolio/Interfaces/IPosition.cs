using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Portfolio.Interfaces
{
    public interface IPosition
    {
        int ParentPortfolioId { get; set; }
        int ParentPositionId { get; set; }
        int Id { get; set; }
        string Name { get; set; }
        string Symbol { get; set; }
        string Underlying { get; set; }
        Side? LastTradeSide { get; set; }
        DateTime LastTradeTime { get; set; }
        DateTime PositionDate { get; set; }
        PositionType PositionType { get; set; }
        int TotalSubmissions { get; set; }
        int TotalSingleLegSubmissions { get; set; }
        int TotalSpreadSubmissions { get; set; }
        int TotalSingleFills { get; set; }
        int TotalSpreadFills { get; set; }
        int UniqueSubmissions { get; set; }
        int TotalFills { get; set; }
        int UniqueFills { get; set; }
        int TotalContracts { get; set; }
        int UniqueContracts { get; set; }
        int NetQty { get; set; }
        int RawNetQty { get; set; }
        bool FirstEdgeAcquired { get; set; }
        double FillRate { get; set; }
        double OrderFillRate { get; set; }
        double IbOrderFillRate { get; set; }
        double RealizedPnl { get; set; }
        double AdjustedPnl { get; set; }
        double SingleLegAdjustedPnl { get; set; }
        double SpreadAdjustedPnl { get; set; }
        double UnrealizedPnl { get; set; }
        double NetDelta { get; set; }
        double FirstEdge { get; set; }
        double BestSellPrice { get; set; }
        double BestSellPriceUnderMid { get; set; }
        double BestBuyPrice { get; set; }
        double BestBuyPriceUnderMid { get; set; }
        double OpenPositionAveragePrice { get; set; }
        double OpenPositionFillUnderPrice { get; set; }
        double LastEdge { get; set; }
        double LastBuyEdge { get; set; }
        double LastSellEdge { get; set; }
        double LastBuyEdgeToTheo { get; set; }
        double LastSellEdgeToTheo { get; set; }
        double LastBuyFillEdgeToTheo { get; set; }
        double LastSellFillEdgeToTheo { get; set; }
        double LastBuyAttemptEdgeToTheo { get; set; }
        double LastSellAttemptEdgeToTheo { get; set; }
        double LastPermBuyFillEdgeToTheo { get; set; }
        double LastPermSellFillEdgeToTheo { get; set; }
        double LastPermBuyAttemptEdgeToTheo { get; set; }
        double LastPermSellAttemptEdgeToTheo { get; set; }
        double BestBuyEdgeToTheo { get; set; }
        double WorstBuyEdgeToTheo { get; set; }
        double BestSellEdgeToTheo { get; set; }
        double WorstSellEdgeToTheo { get; set; }
        double OpenNotional { get; set; }
        string LastInstance { get; set; }
        string LastTrader { get; set; }
        string Account { get; set; }
        int MaxResubmitEstimate { get; set; }
        int MaxResubmitForFill { get; set; }
        int AvgResubmitEstimate { get; set; }
        int AvgResubmitForFill { get; set; }
        int TotalOutOfMarketOrders { get; set; }
        int TotalOutOfMarketFills { get; set; }
        Side? HardSide { get; set; }
        DateTime HardSideDesignationTime { get; set; }
        double HardSideBuyGiveUp { get; set; }
        double HardSideSellGiveUp { get; set; }
        int SubmissionRatePerSec { get; set; }
        int MaxOrdersPerSec { get; set; }
        int WinnerTrades { get; set; }
        int LoserTrades { get; set; }
        int SizeWinnerTrades { get; set; }
        int SizeLoserTrades { get; set; }
        int AvgCloseSubs { get; set; }
        int OpenSubsCount { get; set; }
        int SubsBetweenFillsCount { get; set; }
        double IntroducingBrokerFee { get; set; }
        double ExecutingBrokerFee { get; set; }
        double ExchangeFee { get; set; }
        double OrfFee { get; set; }
        double SecFee { get; set; }
        double TotalFees { get; set; }
        double LastBuyAttempt { get; set; }
        double LastBuyAttemptUnderlying { get; set; }
        double LastSellAttempt { get; set; }
        double LastSellAttemptUnderlying { get; set; }
        ushort LastInstanceId { get; set; }
        ushort LastTraderId { get; set; }
        ushort AccountId { get; set; }

        IPosition GetPosition(int positionId, PositionType type);
    }
}