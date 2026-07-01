using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Portfolio.Interfaces
{
    public interface IPortfolio
    {
        int Id { get; set; }
        string Name { get; set; }
        PortfolioType PortfolioType { get; set; }
        DateTime PortfolioDate { get; set; }
        int TotalSubmissions { get; set; }
        int GroupSubmissionsAvg { get; set; }
        int TotalSingleLegSubmissions { get; set; }
        int TotalSpreadSubmissions { get; set; }
        int TotalSingleFills { get; set; }
        int TotalSpreadFills { get; set; }
        int UniqueSubmissions { get; set; }
        int UniqueSpreadSubmissions { get; set; }
        int TotalFills { get; set; }
        int UniqueFills { get; set; }
        int UniqueSpreadFills { get; set; }
        int StockContracts { get; set; }
        int TotalContracts { get; set; }
        int UniqueContracts { get; set; }
        int UniqueSpreadContracts { get; set; }
        int NetQty { get; set; }
        int ShortQty { get; set; }
        int LongQty { get; set; }
        double FillRate { get; set; }
        double OrderFillRate { get; set; }
        double IbOrderFillRate { get; set; }
        double GroupAvgFillRate { get; set; }
        double LowestRealizedPnl { get; set; }
        double HighestRealizedPnl { get; set; }
        double RealizedPnl { get; set; }
        double LowestAdjustedPnl { get; set; }
        double HighestAdjustedPnl { get; set; }
        double AdjustedPnl { get; set; }
        double SingleLegAdjustedPnl { get; set; }
        double SpreadAdjustedPnl { get; set; }
        double UnrealizedPnl { get; set; }
        double NetDelta { get; set; }
        double DeltaAdjustedBurn { get; set; }
        double DeltaAdjustedHelp { get; set; }
        double HighestOpenNotional { get; set; }
        double TotalOpenNotional { get; set; }
        ICollection<IPosition> Positions { get; }
        int PositionsCount { get; }
        int MaxResubmitEstimate { get; set; }
        int MaxResubmitForFill { get; set; }
        int AvgResubmitEstimate { get; set; }
        int AvgResubmitForFill { get; set; }
        int TotalOutOfMarketOrders { get; set; }
        int TotalOutOfMarketFills { get; set; }
        int SubmissionRatePerSec { get; set; }
        int MaxOrdersPerSec { get; set; }
        int WinnerTrades { get; set; }
        int LoserTrades { get; set; }
        int SizeWinnerTrades { get; set; }
        int SizeLoserTrades { get; set; }
        int AvgCloseSubs { get; set; }
        double IntroducingBrokerFee { get; set; }
        double ExecutingBrokerFee { get; set; }
        double ExchangeFee { get; set; }
        double OrfFee { get; set; }
        double SecFee { get; set; }
        double TotalFees { get; set; }
        double AvgOpenSubsCount { get; set; }
        double AvgSubsBetweenFillsCount { get; set; }

        void AddPosition(IPosition position);
        IPosition GetPosition(int positionId, PositionType type);
        bool TryGetPosition(string name, PositionType type, out IPosition position);
        void Clear();
    }
}