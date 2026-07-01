using DevExpress.Mvvm;
using NLog;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Oms.Ui.Collections;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class PositionSModel : BindableBase, IPosition
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<int, IPosition> _idToPositionMap;
        private bool _underlyingSet;

        private double _AdjustedPnl;
        private double _UnrealizedPnl;

        public FastObservableCollection<IPosition> ExpirationPositions { get; }
        public int ParentPositionId { get; set; }
        public int Id { get; set; }
        public PositionType PositionType { get; set; }
        public string Name { get; set; }
        public double NetDelta { get; set; }
        public bool FirstEdgeAcquired { get; set; }
        public double FirstEdge { get; set; }
        public double BestSellPrice { get; set; }
        public double BestSellPriceUnderMid { get; set; }
        public double BestBuyPrice { get; set; }
        public double BestBuyPriceUnderMid { get; set; }
        public double OpenPositionAveragePrice { get; set; }
        public double OpenPositionFillUnderPrice { get; set; }
        public string Underlying { get; set; }
        public string Symbol { get; set; }
        public Side? LastTradeSide { get; set; }
        public DateTime LastTradeTime { get; set; }
        public double LastBuyAttempt { get; set; }
        public double LastBuyAttemptUnderlying { get; set; }
        public double LastSellAttempt { get; set; }
        public double LastSellAttemptUnderlying { get; set; }
        public ushort LastInstanceId { get; set; }
        public ushort LastTraderId { get; set; }
        public ushort AccountId { get; set; }
        public int QtyOffSet { get; set; }
        public int ActualQty { get; set; }
        public int ParentPortfolioId { get; set; }
        public DateTime PositionDate { get; set; }

        [Bindable]
        public partial int RawNetQty { get; set; }
        [Bindable]
        public partial int NetQty { get; set; }

        partial void OnNetQtyChanged(int value) => UpdateActualQty();
        [Bindable]
        public partial double RealizedPnl { get; set; }
        public double AdjustedPnl
        {
            get => _AdjustedPnl;
            set
            {
                SetValue(ref _AdjustedPnl, value);
                NetPnl = AdjustedPnl + UnrealizedPnl;
            }
        }
        public double UnrealizedPnl
        {
            get => _UnrealizedPnl;
            set
            {
                SetValue(ref _UnrealizedPnl, value);
                NetPnl = AdjustedPnl + UnrealizedPnl;
            }
        }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial int TotalSubmissions { get; set; }
        [Bindable]
        public partial int UniqueSubmissions { get; set; }
        [Bindable]
        public partial int TotalFills { get; set; }
        [Bindable]
        public partial int UniqueFills { get; set; }
        [Bindable]
        public partial int TotalContracts { get; set; }
        [Bindable]
        public partial int UniqueContracts { get; set; }
        [Bindable]
        public partial double FillRate { get; set; }
        [Bindable]
        public partial double OrderFillRate { get; set; }
        [Bindable]
        public partial double IbOrderFillRate { get; set; }
        [Bindable]
        public partial double LastEdge { get; set; }
        [Bindable]
        public partial double LastBuyEdge { get; set; }
        [Bindable]
        public partial double LastSellEdge { get; set; }
        [Bindable]
        public partial double LastBuyEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastSellEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastBuyFillEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastSellFillEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastBuyAttemptEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastSellAttemptEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastPermBuyFillEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastPermSellFillEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastPermBuyAttemptEdgeToTheo { get; set; }
        [Bindable]
        public partial double LastPermSellAttemptEdgeToTheo { get; set; }
        [Bindable]
        public partial double BestBuyEdgeToTheo { get; set; }
        [Bindable]
        public partial double WorstBuyEdgeToTheo { get; set; }
        [Bindable]
        public partial double BestSellEdgeToTheo { get; set; }
        [Bindable]
        public partial double WorstSellEdgeToTheo { get; set; }
        [Bindable]
        public partial string LastInstance { get; set; }
        [Bindable]
        public partial string LastTrader { get; set; }
        [Bindable]
        public partial string Account { get; set; }

        [Bindable]
        public partial int MaxResubmitEstimate { get; set; }
        [Bindable]
        public partial int MaxResubmitForFill { get; set; }
        [Bindable]
        public partial int AvgResubmitEstimate { get; set; }
        [Bindable]
        public partial int AvgResubmitForFill { get; set; }
        [Bindable]
        public partial double OpenNotional { get; set; }
        [Bindable]
        public partial int TotalOutOfMarketOrders { get; set; }
        [Bindable]
        public partial int TotalOutOfMarketFills { get; set; }
        [Bindable]
        public partial int TotalSingleLegSubmissions { get; set; }
        [Bindable]
        public partial int TotalSpreadSubmissions { get; set; }
        [Bindable]
        public partial int TotalSingleFills { get; set; }
        [Bindable]
        public partial int TotalSpreadFills { get; set; }
        [Bindable]
        public partial Side? HardSide { get; set; }
        [Bindable]
        public partial DateTime HardSideDesignationTime { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double HardSideBuyGiveUp { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double HardSideSellGiveUp { get; set; }
        [Bindable]
        public partial int SubmissionRatePerSec { get; set; }
        [Bindable]
        public partial int MaxOrdersPerSec { get; set; }
        [Bindable]
        public partial int WinnerTrades { get; set; }
        [Bindable]
        public partial int LoserTrades { get; set; }
        [Bindable]
        public partial int SizeWinnerTrades { get; set; }
        [Bindable]
        public partial int SizeLoserTrades { get; set; }
        [Bindable]
        public partial int AvgCloseSubs { get; set; }
        [Bindable]
        public partial int OpenSubsCount { get; set; }
        [Bindable]
        public partial int SubsBetweenFillsCount { get; set; }
        [Bindable]
        public partial double IntroducingBrokerFee { get; set; }
        [Bindable]
        public partial double ExecutingBrokerFee { get; set; }
        [Bindable]
        public partial double ExchangeFee { get; set; }
        [Bindable]
        public partial double OrfFee { get; set; }
        [Bindable]
        public partial double SecFee { get; set; }
        [Bindable]
        public partial double TotalFees { get; set; }
        [Bindable]
        public partial double SingleLegAdjustedPnl { get; set; }
        [Bindable]
        public partial double SpreadAdjustedPnl { get; set; }

        public PositionSModel()
        {
            _idToPositionMap = new ConcurrentDictionary<int, IPosition>();
            LastInstance = "";
            LastTrader = "";
            Account = "";
            BestSellPrice = double.NaN;
            BestSellPriceUnderMid = double.NaN;
            BestBuyPrice = double.NaN;
            BestBuyPriceUnderMid = double.NaN;
            LastEdge = double.NaN;
            LastBuyEdge = double.NaN;
            LastSellEdge = double.NaN;
            LastBuyFillEdgeToTheo = double.NaN;
            LastSellFillEdgeToTheo = double.NaN;
            LastBuyAttemptEdgeToTheo = double.NaN;
            LastSellAttemptEdgeToTheo = double.NaN;
            LastPermBuyFillEdgeToTheo = double.NaN;
            LastPermSellFillEdgeToTheo = double.NaN;
            LastPermBuyAttemptEdgeToTheo = double.NaN;
            LastPermSellAttemptEdgeToTheo = double.NaN;
            LastBuyEdgeToTheo = double.NaN;
            LastSellEdgeToTheo = double.NaN;
            BestBuyEdgeToTheo = double.NaN;
            WorstBuyEdgeToTheo = double.NaN;
            BestSellEdgeToTheo = double.NaN;
            WorstSellEdgeToTheo = double.NaN;
            ExpirationPositions = new FastObservableCollection<IPosition>();
        }

        public void SetUnderlying()
        {
            try
            {
                if (PositionType == PositionType.Spread && !_underlyingSet && !string.IsNullOrWhiteSpace(Symbol))
                {
                    _underlyingSet = true;
                    SymbolCodec codec = new(Symbol);
                    Underlying = codec.UnderlyingSymbol();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetUnderlying));
            }
        }

        public IPosition GetPosition(int positionId, PositionType type)
        {
            if (!_idToPositionMap.TryGetValue(positionId, out IPosition position))
            {
                position = new PositionSModel()
                {
                    Id = positionId,
                    PositionType = type,
                };
                _idToPositionMap[positionId] = position;
                ExpirationPositions.Add(position);
            }
            return position;
        }

        private void UpdateActualQty()
        {
            ActualQty = NetQty + QtyOffSet;
        }
    }
}