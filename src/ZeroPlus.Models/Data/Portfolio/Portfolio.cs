using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;

namespace ZeroPlus.Models.Data.Portfolio
{
    public class Portfolio : IPortfolio
    {
        public int Id { get; set; }
        public PortfolioType PortfolioType { get; set; }
        public string Name { get; set; }
        public DateTime PortfolioDate { get; set; }
        public int TotalSubmissions { get; set; }
        public int GroupSubmissionsAvg { get; set; }
        public int TotalSingleLegSubmissions { get; set; }
        public int TotalSpreadSubmissions { get; set; }
        public int TotalSingleFills { get; set; }
        public int TotalSpreadFills { get; set; }
        public int UniqueSubmissions { get; set; }
        public int UniqueSpreadSubmissions { get; set; }
        public int TotalFills { get; set; }
        public int UniqueFills { get; set; }
        public int UniqueSpreadFills { get; set; }
        public int StockContracts { get; set; }
        public int TotalContracts { get; set; }
        public int UniqueContracts { get; set; }
        public int UniqueSpreadContracts { get; set; }
        public int NetQty { get; set; }
        public int ShortQty { get; set; }
        public int LongQty { get; set; }
        public double FillRate { get; set; }
        public double OrderFillRate { get; set; }
        public double IbOrderFillRate { get; set; }
        public double GroupAvgFillRate { get; set; }
        public double LowestRealizedPnl { get; set; }
        public double HighestRealizedPnl { get; set; }
        public double RealizedPnl { get; set; }
        public double LowestAdjustedPnl { get; set; }
        public double HighestAdjustedPnl { get; set; }
        public double AdjustedPnl { get; set; }
        public double SingleLegAdjustedPnl { get; set; }
        public double SpreadAdjustedPnl { get; set; }
        public double UnrealizedPnl { get; set; }
        public double NetDelta { get; set; }
        public double DeltaAdjustedBurn { get; set; }
        public double DeltaAdjustedHelp { get; set; }
        public double HighestOpenNotional { get; set; }
        public double TotalOpenNotional { get; set; }
        public ICollection<IPosition> Positions { get; set; }
        public int PositionsCount { get; set; }
        public int MaxResubmitEstimate { get; set; }
        public int MaxResubmitForFill { get; set; }
        public int AvgResubmitEstimate { get; set; }
        public int AvgResubmitForFill { get; set; }
        public int TotalOutOfMarketOrders { get; set; }
        public int TotalOutOfMarketFills { get; set; }
        public int SubmissionRatePerSec { get; set; }
        public int MaxOrdersPerSec { get; set; }
        public int WinnerTrades { get; set; }
        public int LoserTrades { get; set; }
        public int SizeWinnerTrades { get; set; }
        public int SizeLoserTrades { get; set; }
        public int AvgCloseSubs { get; set; }
        public double IntroducingBrokerFee { get; set; }
        public double ExecutingBrokerFee { get; set; }
        public double ExchangeFee { get; set; }
        public double OrfFee { get; set; }
        public double SecFee { get; set; }
        public double TotalFees { get; set; }
        public double AvgOpenSubsCount { get; set; }
        public double AvgSubsBetweenFillsCount { get; set; }

        public Portfolio()
        {
            Name = string.Empty;
            Positions = new List<IPosition>();
        }

        public void AddPosition(IPosition calculator)
        {
            Positions.Add(calculator);
            PositionsCount = Positions.Count;
        }

        public IPosition GetPosition(int id, PositionType type)
        {
            return new Position()
            {
                Id = id,
                PositionType = type,
            };
        }

        public bool TryGetPosition(string name, PositionType type, out IPosition position)
        {
            position = new Position()
            {
                Name = name,
                PositionType = type,
            };
            return true;
        }

        public void Clear()
        {
            Positions.Clear();
        }
    }
}