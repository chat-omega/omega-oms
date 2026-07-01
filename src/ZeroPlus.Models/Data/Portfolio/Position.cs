using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;

namespace ZeroPlus.Models.Data.Portfolio
{
    public class Position : IPosition
    {
        public int ParentPortfolioId { get; set; }
        public int ParentPositionId { get; set; }
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Underlying { get; set; } = string.Empty;
        public Side? LastTradeSide { get; set; }
        public DateTime LastTradeTime { get; set; }
        public DateTime PositionDate { get; set; }
        public PositionType PositionType { get; set; }
        public int NetQty { get; set; }
        public int RawNetQty { get; set; }
        public double RealizedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public double SingleLegAdjustedPnl { get; set; }
        public double SpreadAdjustedPnl { get; set; }
        public double UnrealizedPnl { get; set; }
        public int TotalSubmissions { get; set; }
        public int TotalSingleLegSubmissions { get; set; }
        public int TotalSpreadSubmissions { get; set; }
        public int TotalSingleFills { get; set; }
        public int TotalSpreadFills { get; set; }
        public int UniqueSubmissions { get; set; }
        public int TotalFills { get; set; }
        public int UniqueFills { get; set; }
        public int TotalContracts { get; set; }
        public int UniqueContracts { get; set; }
        public double FillRate { get; set; }
        public double OrderFillRate { get; set; }
        public double IbOrderFillRate { get; set; }
        public double NetDelta { get; set; }
        public bool FirstEdgeAcquired { get; set; }
        public double FirstEdge { get; set; }
        public double BestSellPrice { get; set; }
        public double BestSellPriceUnderMid { get; set; }
        public double BestBuyPrice { get; set; }
        public double BestBuyPriceUnderMid { get; set; }
        public double OpenPositionAveragePrice { get; set; }
        public double OpenPositionFillUnderPrice { get; set; }
        public double LastEdge { get; set; }
        public double LastBuyEdge { get; set; }
        public double LastSellEdge { get; set; }
        public double LastBuyEdgeToTheo { get; set; }
        public double LastSellEdgeToTheo { get; set; }
        public double LastBuyFillEdgeToTheo { get; set; }
        public double LastSellFillEdgeToTheo { get; set; }
        public double LastBuyAttemptEdgeToTheo { get; set; }
        public double LastSellAttemptEdgeToTheo { get; set; }
        public double LastPermBuyFillEdgeToTheo { get; set; }
        public double LastPermSellFillEdgeToTheo { get; set; }
        public double LastPermBuyAttemptEdgeToTheo { get; set; }
        public double LastPermSellAttemptEdgeToTheo { get; set; }
        public double BestBuyEdgeToTheo { get; set; }
        public double WorstBuyEdgeToTheo { get; set; }
        public double BestSellEdgeToTheo { get; set; }
        public double WorstSellEdgeToTheo { get; set; }
        public string LastInstance { get; set; } = string.Empty;
        public string LastTrader { get; set; } = string.Empty;
        public string Account { get; set; } = string.Empty;
        public int MaxResubmitEstimate { get; set; }
        public int MaxResubmitForFill { get; set; }
        public int AvgResubmitEstimate { get; set; }
        public int AvgResubmitForFill { get; set; }
        public double OpenNotional { get; set; }
        public int TotalOutOfMarketOrders { get; set; }
        public int TotalOutOfMarketFills { get; set; }
        public Side? HardSide { get; set; }
        public DateTime HardSideDesignationTime { get; set; }
        public double HardSideBuyGiveUp { get; set; }
        public double HardSideSellGiveUp { get; set; }
        public int SubmissionRatePerSec { get; set; }
        public int MaxOrdersPerSec { get; set; }
        public int WinnerTrades { get; set; }
        public int LoserTrades { get; set; }
        public int SizeWinnerTrades { get; set; }
        public int SizeLoserTrades { get; set; }
        public int AvgCloseSubs { get; set; }
        public int OpenSubsCount { get; set; }
        public int SubsBetweenFillsCount { get; set; }
        public double IntroducingBrokerFee { get; set; }
        public double ExecutingBrokerFee { get; set; }
        public double ExchangeFee { get; set; }
        public double OrfFee { get; set; }
        public double SecFee { get; set; }
        public double TotalFees { get; set; }
        public double LastBuyAttempt { get; set; }
        public double LastBuyAttemptUnderlying { get; set; }
        public double LastSellAttempt { get; set; }
        public double LastSellAttemptUnderlying { get; set; }
        public ushort LastInstanceId { get; set; }
        public ushort LastTraderId { get; set; }
        public ushort AccountId { get; set; }

        public IPosition GetPosition(int positionId, PositionType type)
        {
            return default!;
        }

        public override string ToString()
        {
            return $"{Name} - {PositionType}";
        }
    }
}
