using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Trading;
using OrderType = ZeroPlus.Models.Data.Enums.OrderType;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class OmsOrderModel : ViewModelBase, IOmsDataSubscriber, IComplexOrder
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<int, IComplexOrderLeg> _legIdToLegMap = new();
        private static readonly ConcurrentDictionary<HardSideKey, HardSideResult> _spreadKeyToHardSideMapping = new();

        private static readonly string[] _hardSidePropertyNames =
        [
            nameof(HardSide),
            nameof(HardSideDesignationTime),
            nameof(HardSideBuyGiveUp),
            nameof(HardSideSellGiveUp)
        ];
        private static readonly string[] _positionPropertyNames =
        [
            nameof(Position),
            nameof(RawPosition),
            nameof(RealizedPnL),
            nameof(AdjustedPnl),
            nameof(UnrealizedPnl),
            nameof(NetPnl),
            nameof(BestBuyEdgeToTheo),
            nameof(WorstBuyEdgeToTheo),
            nameof(BestSellEdgeToTheo),
            nameof(WorstSellEdgeToTheo),
            nameof(BestSellPrice),
            nameof(BestSellPriceUnderMid),
            nameof(BestBuyPrice),
            nameof(BestBuyPriceUnderMid),
            nameof(OpenSubsCount),
            nameof(SubsBetweenFillsCount)
        ];
        private static readonly string[] _deltaAdjustedPropertyNames =
        [
            nameof(DeltaAdjBestBuyPrice),
            nameof(DeltaAdjBestSellPrice),
            nameof(AdjAveragePrice)
        ];
        private static readonly string[] _indicatorPropertyNames =
        [
            nameof(IsFirstFill),
            nameof(IsCitadel),
            nameof(FirstEdgeAcquired),
            nameof(FirstEdge),
            nameof(CitadelSide),
            nameof(SpreadPositionEffect),
            nameof(LastEdge),
            nameof(DeltaAdjLastEdge),
            nameof(DeltaAdjLastEdgeNotional),
            nameof(EdgeScanFeedDeltaAdjPrice),
            nameof(DeltaAdjChange),
            nameof(DeltaAdjChangeNotional),
            nameof(LoopInitLatency),
            nameof(IsTagged),
            nameof(Tagger),
            nameof(TaggedMessage),
            nameof(EdgeGiveUp),
            nameof(CloseSubs)
        ];
        private static readonly string[] _tagPropertyNames =
        [
            nameof(DigBid),
            nameof(DigAsk),
            nameof(DigBidSize),
            nameof(DigAskSize),
            nameof(WeightedVega),
            nameof(TagEdge),
            nameof(TagMid),
            nameof(TagBid),
            nameof(TagAsk),
            nameof(TagUnderBid),
            nameof(TagUnderAsk),
            nameof(TagTheo),
            nameof(TagVolaV0),
            nameof(TagVolaV1),
            nameof(TagVolaV2),
            nameof(TagEma),
            nameof(VolaIv),
            nameof(TheoBid),
            nameof(TheoAsk),
            nameof(TheoPercentBid),
            nameof(FillTheoPercentBid),
            nameof(SharedId),
            nameof(Sequence),
            nameof(TypeId),
            nameof(SubTypeId),
            nameof(SubTypeSequence),
            nameof(SubType),
            nameof(EdgeScanFeedDeltaAdjPrice),
            nameof(EdgeScanFeedEdge),
            nameof(EdgeScanFeedTimespan),
            nameof(EdgeScanFeedBuyPrice),
            nameof(EdgeScanFeedSellPrice),
            nameof(EdgeScanFeedBuyQty),
            nameof(EdgeScanFeedSellQty),
            nameof(EdgeScanFeedBuyTime),
            nameof(EdgeScanFeedSellTime),
            nameof(EdgeScanFeedRespondLatency),
            nameof(EdgeScanFeedConditionCode),
            nameof(TradeToNewTime),
            nameof(OmsBidPercentOfFillPrice),
            nameof(OrderSource),
            nameof(EdgeToTheo),
            nameof(TagEdgeToTheo),
            nameof(TagEdgeToEma),
            nameof(TagEdgeToVolaV0),
            nameof(TagEdgeToVolaV1),
            nameof(TagEdgeToVolaV2),
            nameof(OrderEdgeToTheo),
            nameof(InitialEdge),
            nameof(OpenEdge),
            nameof(CloseEdge),
            nameof(CostOfHedging),
            nameof(Comment),
            nameof(Reason),
            nameof(AutomationType),
            nameof(Tag),
            nameof(Trader),
            nameof(EdgeType),
            nameof(EdgeGiveUp),
        ];
        private static readonly string[] _updatePropertyNames =
        [
            nameof(PartiallyFilled),
            nameof(LastQuantity),
            nameof(FilledQty),
            nameof(LeavesQuantity),
            nameof(CumulativeQuantity),
            nameof(TransactionID),
            nameof(SpreadAvgPrice),
            nameof(AveragePrice),
            nameof(FillTheoPercentBid),
            nameof(Price),
            nameof(TheoPercentBid),
            nameof(LastPrice),
            nameof(MinPrice),
            nameof(MaxPrice),
            nameof(Quantity),
            nameof(Fee1),
            nameof(Fee2),
            nameof(Bid),
            nameof(Ask),
            nameof(Width),
            nameof(UnderBid),
            nameof(UnderAsk),
            nameof(UnderMid),
            nameof(TV),
            nameof(Delta),
            nameof(ExchangeFee1),
            nameof(ExchangeFee2),
            nameof(BrokerFee1),
            nameof(BrokerFee2),
            nameof(TotalContracts),
            nameof(FillTime),
            nameof(TradeToNewTime),
            nameof(SubmitToNewTime),
            nameof(NewToCancelTime),
            nameof(BidPercentOfFillPrice),
            nameof(CloseBidPercentOfFillPrice),
            nameof(OmsBidPercentOfFillPrice),
            nameof(OmsBestBidPercent),
            nameof(TotalDelta),
            nameof(HanweckTotalTheo),
            nameof(HanweckTotalGamma),
            nameof(HanweckTotalVega),
            nameof(HanweckTotalTheta),
            nameof(HanweckTotalRho),
            nameof(HanweckTotalIV),
            nameof(HanweckTotalUnder),
            nameof(HanweckTotalUBid),
            nameof(HanweckTotalUAsk),
            nameof(HanweckTotalBid),
            nameof(HanweckTotalAsk),
            nameof(TimeValue),
            nameof(IntrinsicValue),
            nameof(FVDivs),
            nameof(UFwd),
            nameof(UFwdFactor),
            nameof(BorrowCost),
            nameof(BorrowRate),
            nameof(UPrice),
            nameof(UTheo),
            nameof(CloseTV),
            nameof(CloseDelta),
            nameof(CloseTotalDelta),
            nameof(CloseHanweckTotalTheo),
            nameof(CloseHanweckTotalGamma),
            nameof(CloseHanweckTotalVega),
            nameof(CloseHanweckTotalTheta),
            nameof(CloseHanweckTotalRho),
            nameof(CloseHanweckTotalIV),
            nameof(CloseBid),
            nameof(CloseAsk),
            nameof(CloseWidth),
            nameof(CloseUnderBid),
            nameof(CloseUnderAsk),
            nameof(UnderWidth),
            nameof(CloseHanweckTotalUnder),
            nameof(CloseHanweckTotalUBid),
            nameof(CloseHanweckTotalUAsk),
            nameof(CloseHanweckTotalBid),
            nameof(CloseHanweckTotalAsk),
            nameof(OrderStatus),
            nameof(LastUpdateTime),
            nameof(NewStatusTimeStamp),
            nameof(EdgeToTheo),
            nameof(TagEdgeToTheo),
            nameof(TagEdgeToEma),
            nameof(TagEdgeToVolaV0),
            nameof(TagEdgeToVolaV1),
            nameof(TagEdgeToVolaV2),
            nameof(InitialEdge),
            nameof(OpenEdge),
            nameof(CloseEdge),
            nameof(IsFirstFill),
            nameof(Exchanges),
            nameof(LastExchange),
            nameof(Reason),
            nameof(DeltaAdjustedTheo),
            nameof(CloseDeltaAdjustedTheo),
            nameof(BidSize),
            nameof(AskSize),
            nameof(CloseBidSize),
            nameof(CloseAskSize),
            nameof(UnderlyingBidSize),
            nameof(UnderlyingAskSize),
            nameof(CloseUnderlyingBidSize),
            nameof(CloseUnderlyingAskSize),
            nameof(LastEdge),
            nameof(DeltaAdjLastEdge),
            nameof(DeltaAdjLastEdgeNotional),
            nameof(EdgeScanFeedDeltaAdjPrice),
            nameof(DeltaAdjChange),
            nameof(DeltaAdjChangeNotional),
            nameof(LoopInitLatency),
            nameof(IsTagged),
            nameof(Tagger),
            nameof(TaggedMessage),
            nameof(HardSideAtTrade),
            nameof(HardSideAtTradeDesignationTime),
            nameof(HardSideAtTradeBuyGiveUp),
            nameof(HardSideAtTradeSellGiveUp),
            nameof(EdgeGiveUp),
            nameof(CloseSubs),
            nameof(CostOfHedging),
            nameof(CostOfHedgingInEdge),
            nameof(ContraCapacitiesDisplay),
            nameof(ContraBrokerNamesDisplay),
            nameof(ContraCmtasDisplay),
            nameof(ContraTradersDisplay),
        ];

        private readonly ISecurityBook _securityBook;
        private readonly PortfolioManagerModel _portfolioManager;

        public static OmsCore OmsCore { get; private set; }

        public Dictionary<string, string> UnboundDataColumnToValueMap = new();
        private string _underlyingSymbol;
        private char _edgeScanFeedConditionCode = '\0';
        private OrderStatus _orderStatus;
        private bool _subscribedToHardSide;

        [Bindable]
        public partial bool Hide { get; set; }

        public string UnderlyingSymbol
        {
            get => _underlyingSymbol;
            set => _underlyingSymbol = value ?? _underlyingSymbol;
        }
        public OrderStatus OrderStatus
        {
            get => _orderStatus;
            set
            {
                _orderStatus = PartiallyFilled && value == OrderStatus.Canceled ? OrderStatus.PartiallyFilled : value;
                Done = value.IsClosed();
            }
        }
        public bool IsGTH { get; set; }
        public OrderTagModel OrderTag { get; set; }
        public OrderType OrderType { get; set; }
        public string Currency { get; set; }
        public string Guid { get; set; }
        public bool Done { get; set; }
        public bool PartiallyFilled { get; set; }
        public string Description { get; set; }
        public string SpreadId { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime Timestamp { get; set; }
        public int TransactionID { get; set; }
        public string Source { get; set; }
        public string Tag { get; set; }
        public string SmartRoute { get; set; }
        public string FullTag { get; set; }
        public string Trader { get; set; }
        public double EdgeGiveUp { get; set; }
        public double CloseSubs { get; set; }
        public double TagEdge { get; set; }
        public bool SkipNewPriceEvaluation { get; set; }
        public double TagMid { get; set; }
        public double TagBid { get; set; }
        public double TagAsk { get; set; }
        public double TagWidth => TagAsk - TagBid;
        public double TagTheo { get; set; }
        public double TagVolaV0 { get; set; }
        public double TagVolaV1 { get; set; }
        public double TagVolaV2 { get; set; }
        public OrderSubType? SubType { get; set; }
        public double TagEma { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; }
        public SubType SubTypeId { get; set; }
        public Venue? Venue { get; set; }
        public ushort SubTypeSequence { get; set; }
        public string SubTypeSummary { get; set; }
        public string Comment { get; set; }
        public string RouteOverride { get; set; }
        public string PrimaryExchange { get; set; }
        public string ExchangeOrderID { get; set; }
        public string ExecutingBroker { get; set; }
        public string ExecutionID { get; set; }
        public string ExecutionReferenceID { get; set; }
        public string LastExchange { get; set; }
        public string Reason { get; set; }
        public double Fee1 { get; set; }
        public double Fee2 { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double Width => Ask - Bid;
        public double VolaTheoAdj { get; set; } = double.NaN;
        public double UnderBid { get; set; } = double.NaN;
        public double UnderAsk { get; set; } = double.NaN;
        public double UnderMid
        {
            get => (UnderBid + UnderAsk) / 2;
            set => _ = value;
        }
        public double TV { get; set; }
        public double Delta { get; set; }
        public double ExchangeFee1 { get; set; }
        public double ExchangeFee2 { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public DateTime SubmitTime { get; set; }
        public double FillTime { get; set; }
        public double TradeToNewTime { get; set; }
        public double SubmitToNewTime { get; set; }
        public double NewToCancelTime { get; set; }
        public long MsgSequence { get; set; }
        public int LastQuantity { get; set; }
        public int FilledQty { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public string PermID { get; set; }
        public string OrderID { get; set; }
        public string OriginalOrderID { get; set; }
        public string Username { get; set; }
        public string AccountAcronym { get; set; }
        public string Symbol { get; set; }
        public double OrderEdgeToTheo { get; set; }
        public string RequestAccountAcronym { get; set; }
        public string Route { get; set; }
        public string RequestSymbol { get; set; }
        public Side? Side { get; set; }
        public double Price { get; set; } = double.NaN;
        public double LastPrice { get; set; } = double.NaN;
        public double MinPrice { get; set; } = double.NaN;
        public double MaxPrice { get; set; } = double.NaN;
        public int Quantity { get; set; }
        public string Destination { get; set; }
        public uint DestinationSequence { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public string RoutingSession { get; set; }
        public string ClearingFirm { get; set; }
        public string ClearingID { get; set; }
        public int AccountID { get; set; }
        public double AveragePrice { get; set; }
        public double SpreadAvgPrice { get; set; }
        public double BidPercentOfFillPrice { get; set; }
        public double CloseBidPercentOfFillPrice { get; set; }
        public double OmsBidPercentOfFillPrice { get; set; }
        public double OmsBestBidPercent { get; set; }

        public bool ShowDetailsButton => IsComplexOrder || ContrapartyReportModel != null;

        private bool _isComplexOrder;
        public bool IsComplexOrder
        {
            get => _isComplexOrder;
            set
            {
                _isComplexOrder = value;
                RaisePropertyChanged(nameof(ShowDetailsButton));
            }
        }
        public HashSet<IComplexOrderLeg> Legs { get; set; }
        public double TotalContracts { get; set; }
        public double TotalDelta { get; set; } = double.NaN;
        public double HanweckTotalTheo { get; set; } = double.NaN;
        public double HanweckTotalGamma { get; set; } = double.NaN;
        public double HanweckTotalVega { get; set; } = double.NaN;
        public double HanweckTotalTheta { get; set; } = double.NaN;
        public double HanweckTotalRho { get; set; } = double.NaN;
        public double HanweckTotalIV { get; set; } = double.NaN;
        public double HanweckTotalUnder { get; set; } = double.NaN;
        public double HanweckTotalUBid { get; set; } = double.NaN;
        public double HanweckTotalUAsk { get; set; } = double.NaN;
        public double HanweckTotalBid { get; set; } = double.NaN;
        public double HanweckTotalAsk { get; set; } = double.NaN;
        public double TimeValue { get; set; } = double.NaN;
        public double IntrinsicValue { get; set; } = double.NaN;
        public double FVDivs { get; set; } = double.NaN;
        public double UFwd { get; set; } = double.NaN;
        public double UFwdFactor { get; set; } = double.NaN;
        public double BorrowCost { get; set; } = double.NaN;
        public double BorrowRate { get; set; } = double.NaN;
        public double UPrice { get; set; } = double.NaN;
        public double UTheo { get; set; } = double.NaN;
        public double CloseTV { get; set; } = double.NaN;
        public double CloseDelta { get; set; } = double.NaN;
        public double CloseTotalDelta { get; set; } = double.NaN;
        public double CloseHanweckTotalTheo { get; set; } = double.NaN;
        public double CloseHanweckTotalGamma { get; set; } = double.NaN;
        public double CloseHanweckTotalVega { get; set; } = double.NaN;
        public double CloseHanweckTotalTheta { get; set; } = double.NaN;
        public double CloseHanweckTotalRho { get; set; } = double.NaN;
        public double CloseHanweckTotalIV { get; set; } = double.NaN;
        public double CloseBid { get; set; } = double.NaN;
        public double CloseAsk { get; set; } = double.NaN;
        public double CloseWidth => CloseAsk - CloseBid;
        public double CloseUnderBid { get; set; } = double.NaN;
        public double CloseUnderAsk { get; set; } = double.NaN;
        public double UnderWidth => CloseUnderAsk - CloseUnderBid;
        public double CloseHanweckTotalUnder { get; set; } = double.NaN;
        public double CloseHanweckTotalUBid { get; set; } = double.NaN;
        public double CloseHanweckTotalUAsk { get; set; } = double.NaN;
        public double CloseHanweckTotalBid { get; set; } = double.NaN;
        public double CloseHanweckTotalAsk { get; set; } = double.NaN;
        public double DeltaAdjustedTheo { get; set; } = double.NaN;
        public double VolaTheo { get; set; } = double.NaN;
        public double VolaIv { get; set; } = double.NaN;
        public double TheoBid { get; set; } = double.NaN;
        public double TheoAsk { get; set; } = double.NaN;
        public double TheoPercentBid => GetPercentBidInTheoRange(Price);
        public double FillTheoPercentBid => GetPercentBidInTheoRange(AveragePrice);
        public double CloseDeltaAdjustedTheo { get; set; } = double.NaN;
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public int CloseBidSize { get; set; }
        public int CloseAskSize { get; set; }
        public int UnderlyingBidSize { get; set; }
        public int UnderlyingAskSize { get; set; }
        public int CloseUnderlyingBidSize { get; set; }
        public int CloseUnderlyingAskSize { get; set; }
        public double EdgeOverride { get; set; } = double.NaN;
        public double AdjustedEdgeOverride { get; set; } = double.NaN;
        public double EdgeCurveAdjustment { get; set; } = double.NaN;
        public double TagEdgeToTheo { get; set; } = double.NaN;
        public double TagEdgeToEma { get; set; } = double.NaN;
        public double TagEdgeToVolaV0 { get; set; } = double.NaN;
        public double TagEdgeToVolaV1 { get; set; } = double.NaN;
        public double TagEdgeToVolaV2 { get; set; } = double.NaN;
        public double EdgeToTheo { get; set; } = double.NaN;
        public double InitialEdge { get; set; } = double.NaN;
        public double OpenEdge { get; set; } = double.NaN;
        public double CloseEdge { get; set; } = double.NaN;
        public double LiveUnderMid { get; set; } = double.NaN;
        public bool IsDisposed { get; set; }
        public DateTime NewStatusTimeStamp { get; set; }
        public Security Security { get; set; }
        public ISecurityBook SecurityBook { get; }
        public bool IsFirstFill { get; set; }
        public bool FirstEdgeAcquired { get; set; }
        public double FirstEdge { get; set; }
        public bool IsCitadel { get; set; }
        public bool Subscribed { get; internal set; }
        public ExecutionType ExecutionType { get; set; }
        public int TagVolume { get; set; }
        public int TagFirmVolume { get; set; }
        public double TagBestBid { get; set; } = double.NaN;
        public double TagBestAsk { get; set; } = double.NaN;
        public double TagMktMkrBid { get; set; } = double.NaN;
        public double TagMktMkrAsk { get; set; } = double.NaN;
        public string Type { get; set; }
        public double EdgeScanFeedEdge { get; set; } = double.NaN;
        public double EdgeScanFeedTimespan { get; set; } = double.NaN;
        public double EdgeScanFeedBuyPrice { get; set; } = double.NaN;
        public int EdgeScanFeedBuyQty { get; set; }
        public double EdgeScanFeedSellPrice { get; set; } = double.NaN;
        public int EdgeScanFeedSellQty { get; set; }
        public DateTime EdgeScanFeedBuyTime { get; set; }
        public DateTime EdgeScanFeedSellTime { get; set; }
        public double EdgeScanFeedRespondLatency { get; set; } = double.NaN;
        public char EdgeScanFeedConditionCode
        {
            get => _edgeScanFeedConditionCode;
            set
            {
                _edgeScanFeedConditionCode = value;
                EdgeScanFeedCondition = GetConditionCode(value);
            }
        }
        public int ResubmitCount { get; set; }
        public int TotalEstimatedResubmit { get; set; }
        public double DeltaAdjChange { get; set; } = double.NaN;
        public double DeltaAdjChangeNotional { get; set; } = double.NaN;
        public string EdgeScanFeedCondition { get; set; }
        public double LastEdge { get; set; } = double.NaN;
        public double DeltaAdjLastEdge { get; set; } = double.NaN;
        public double DeltaAdjLastEdgeNotional { get; set; } = double.NaN;
        public double EdgeScanFeedDeltaAdjPrice { get; set; } = double.NaN;
        public Side? HardSide { get; set; }
        public DateTime HardSideDesignationTime { get; set; }
        public double HardSideBuyGiveUp { get; set; } = double.NaN;
        public double HardSideSellGiveUp { get; set; } = double.NaN;
        public Side? HardSideAtTrade { get; set; }
        public DateTime HardSideAtTradeDesignationTime { get; set; }
        public double HardSideAtTradeBuyGiveUp { get; set; } = double.NaN;
        public double HardSideAtTradeSellGiveUp { get; set; } = double.NaN;
        public double CostOfHedging { get; set; } = double.NaN;
        public double CostOfHedgingInEdge => IsFill ? CostOfHedging / (FilledQty * 100) : double.NaN;
        public Side? CitadelSide { get; set; }
        public PositionEffect? SpreadPositionEffect { get; set; }
        public bool IsFill => FilledQty > 0;
        public string Exchanges { get; set; }
        public bool IsAutomation { get; set; }
        public string AutomationType { get; set; }
        public string SpreadHash { get; set; }
        public double Multiplier { get; set; }
        public double Last { get; set; } = double.NaN;
        public double AdjAveragePrice { get; set; } = double.NaN;
        public double AveragePriceDiff { get; set; } = double.NaN;
        public double TheoToMid => TagTheo - TagMid;
        public double EmaToMid => TagEma - TagMid;
        public int Position { get; set; }
        public int RawPosition { get; set; }
        public double RealizedPnL { get; set; }
        public double AdjustedPnl { get; set; }
        public double UnrealizedPnl { get; set; }
        public double NetPnl { get; set; }
        public double BestBuyEdgeToTheo { get; set; }
        public double WorstBuyEdgeToTheo { get; set; }
        public double BestSellEdgeToTheo { get; set; }
        public double WorstSellEdgeToTheo { get; set; }
        public int OpenSubsCount { get; set; }
        public int SubsBetweenFillsCount { get; set; }
        public double BestSellPrice { get; set; } = double.NaN;
        public double BestSellPriceUnderMid { get; set; } = double.NaN;
        public double BestBuyPrice { get; set; } = double.NaN;
        public double BestBuyPriceUnderMid { get; set; } = double.NaN;
        public double DeltaAdjBestSellPrice { get; set; } = double.NaN;
        public double DeltaAdjBestBuyPrice { get; set; } = double.NaN;
        public double HanweckDeltaLeg1 => Legs?.ElementAtOrDefault(0)?.Delta ?? double.NaN;
        public double HanweckDeltaLeg2 => Legs?.ElementAtOrDefault(1)?.Delta ?? double.NaN;
        public double HanweckDeltaLeg3 => Legs?.ElementAtOrDefault(2)?.Delta ?? double.NaN;
        public double HanweckDeltaLeg4 => Legs?.ElementAtOrDefault(3)?.Delta ?? double.NaN;
        public int LegsCount => Legs?.Count ?? 1;
        public string SpreadType => BaseStrategy.ToString().Replace("_", " ");
        public double PriceImprovement => Price - AveragePrice;
        public DateTime LutTimeOnly => LastUpdateTime;
        public DateTime DateAdded => Timestamp.Date;
        public double Mid
        {
            get => (Bid + Ask) / 2;
            set => _ = value;
        }
        public MinimumTickStyle MinimumTickStyle { get; set; }
        public string LocalID { get; set; }
        public OrderSource OrderSource { get; set; }
        public EdgeType EdgeType { get; set; } = EdgeType.None;
        public double Edge { get; set; } = double.NaN;
        public bool IsDeltaAdjusted { get; set; }
        public double LoopInitLatency { get; set; } = double.NaN;
        public double TagUnderBid { get; set; } = double.NaN;
        public double TagUnderAsk { get; set; } = double.NaN;
        public double Ema { get; set; } = double.NaN;
        public bool IsTagged { get; set; }
        public string Tagger { get; set; }
        public string TaggedMessage { get; set; }
        [Bindable]
        public partial bool LoadingDetails { get; set; }
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string RiskCheckMessage { get; set; }
        public double DigBid { get; set; }
        public double DigAsk { get; set; }
        public uint DigBidSize { get; set; }
        public uint DigAskSize { get; set; }
        public double WeightedVega { get; set; }
        public double CloseEdgeOverride { get; set; } = double.NaN;
        public StockHedgeOrderModel StockHedgeOrderModel { get; set; }

        [Bindable]
        public partial ObservableCollection<KeyValuePair<string, string>> Details { get; set; }

        [Bindable]
        public partial bool AddToTodaysOrderbook { get; set; }

        [Bindable]
        public partial bool RemoveFromTodaysOrderbook { get; set; }

        #region Live Contra Collections
        public IList<ContraCapacity> ContraCapacities { get; set; }
        public IList<ContraBrokerName> ContraBrokerNames { get; set; }
        public IList<ContraCmta> ContraCmtas { get; set; }
        public IList<ContraTrader> ContraTraders { get; set; }

        public string ContraCapacitiesDisplay => JoinContraValues(ContraCapacities);
        public string ContraBrokerNamesDisplay => JoinContraValues(ContraBrokerNames);
        public string ContraCmtasDisplay => JoinContraValues(ContraCmtas);
        public string ContraTradersDisplay => JoinContraValues(ContraTraders);

        // The Contra* enums use a leading underscore only because their values start with a digit
        // (C# identifier rule). The [Description] attribute on each member just strips that underscore,
        // so we can produce the same display text without any reflection by trimming it ourselves.
        private static string JoinContraValues<T>(IList<T> values) where T : struct, Enum
        {
            if (values is not { Count: > 0 }) return string.Empty;
            return string.Join(", ", values.Select(v => v.ToString().TrimStart('_')).Distinct());
        }
        #endregion

        #region ContraPartyReport Data
        private HashSet<(string, string, string)> _seenContraReports;
        public bool HasContraPartyReport => (ContrapartyReportModel?.Count ?? 0) > 0;
        private ObservableCollection<ContraPartyReportModel> _contrapartyReportModel;
        public ObservableCollection<ContraPartyReportModel> ContrapartyReportModel
        {
            get => _contrapartyReportModel;
            set
            {
                SetValue(ref _contrapartyReportModel, value);
                RaisePropertyChanged(nameof(HasContraPartyReport));
                RaisePropertyChanged(nameof(ShowDetailsButton));
            }
        }

        [Bindable]
        public partial string CRContraClearingFirm { get; set; }
        [Bindable]
        public partial string CRContraAccountType { get; set; }
        [Bindable]
        public partial string CRContraOpenClose { get; set; }
        [Bindable]
        public partial string CRMarketMakerSubAccountCode { get; set; }
        [Bindable]
        public partial string CRTheirExtraText { get; set; }
        [Bindable]
        public partial string CRTheirClientOrderID { get; set; }
        [Bindable]
        public partial string CRTheirBrokerID { get; set; }
        [Bindable]
        public partial string CRLiquidityIndicator { get; set; }
        public ulong IoiId { get; set; }

        public void AddContraPartyReport(ContraPartyReportModel report)
        {
            if (ContrapartyReportModel == null)
                ContrapartyReportModel = [report];
            else
            {
                _seenContraReports ??= [];
                if (_seenContraReports.Add((report.ClOrdID, report.OCCID, report.TheirClientOrderID)))
                {
                    ContrapartyReportModel.Add(report);
                }
            }
            CRContraClearingFirm = string.Join(", ", ContrapartyReportModel.Select(r => r.ContraClearingFirm).Where(r => r != null).Distinct());
            CRContraAccountType = string.Join(", ", ContrapartyReportModel.Select(r => r.ContraAccountType).Where(r => r != null).Distinct());
            CRContraOpenClose = string.Join(", ", ContrapartyReportModel.Select(r => r.ContraOpenClose).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
            CRMarketMakerSubAccountCode = string.Join(", ", ContrapartyReportModel.Select(r => r.MarketMakerSubAccountCode).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
            CRTheirExtraText = string.Join(", ", ContrapartyReportModel.Select(r => r.TheirExtraText).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
            CRTheirClientOrderID = string.Join(", ", ContrapartyReportModel.Select(r => r.TheirClientOrderID).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
            CRTheirBrokerID = string.Join(", ", ContrapartyReportModel.Select(r => r.TheirBrokerID).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
            CRLiquidityIndicator = string.Join(", ", ContrapartyReportModel.Select(r => r.LiquidityIndicator).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct());
        }

        #endregion

        public OmsOrderModel()
        {
            FillTime = double.NaN;
            TradeToNewTime = double.NaN;
            SubmitToNewTime = double.NaN;
            NewToCancelTime = double.NaN;
        }

        public OmsOrderModel(PortfolioManagerModel portfolioManager, ISecurityBook securityBook, OmsCore omsCore)
        {
            OmsCore = omsCore;
            _securityBook = securityBook;
            _portfolioManager = portfolioManager;
            FillTime = double.NaN;
            TradeToNewTime = double.NaN;
            SubmitToNewTime = double.NaN;
            NewToCancelTime = double.NaN;
        }

        public IComplexOrderLeg GetLeg(string legId)
        {
            if (string.IsNullOrWhiteSpace(legId))
            {
                legId = "";
            }
            int key = legId.GetHashCode() + GetHashCode();
            if (!_legIdToLegMap.TryGetValue(key, out IComplexOrderLeg orderLeg))
            {
                orderLeg = new OmsOrderLegModel(_securityBook);
                _legIdToLegMap[key] = orderLeg;
                Legs ??= [];
                Legs.Add(orderLeg);
            }
            return orderLeg;
        }

        public void DeltaAdjustPrices(double underlying)
        {
            bool updated = false;

            double buyAdjValue = Math.Round(((underlying - BestBuyPriceUnderMid) * CloseTotalDelta) + BestBuyPrice, 2);
            if (DeltaAdjBestBuyPrice != buyAdjValue && !double.IsNaN(buyAdjValue))
            {
                DeltaAdjBestBuyPrice = buyAdjValue;
                updated = true;
            }

            double sellAdjValue = Math.Round(((underlying - BestSellPriceUnderMid) * CloseTotalDelta) + BestSellPrice, 2);
            if (DeltaAdjBestSellPrice != sellAdjValue && !double.IsNaN(sellAdjValue))
            {
                DeltaAdjBestSellPrice = sellAdjValue;
                updated = true;
            }

            if (CumulativeQuantity > 0)
            {
                double avgPxAdj = Math.Round(((underlying - ((CloseUnderBid + CloseUnderAsk) / 2)) * CloseTotalDelta) + AveragePrice, 2);
                if (AdjAveragePrice != avgPxAdj && !double.IsNaN(avgPxAdj))
                {
                    AdjAveragePrice = avgPxAdj;
                    updated = true;
                }
            }

            if (updated)
            {
                NotifyOfDeltaAdjValuesUpdate();
            }
        }

        public void ResetDeltaAdjustPrices()
        {
            bool updated = false;

            if (!double.IsNaN(DeltaAdjBestBuyPrice))
            {
                DeltaAdjBestBuyPrice = double.NaN;
                updated = true;
            }

            if (!double.IsNaN(DeltaAdjBestSellPrice))
            {
                DeltaAdjBestSellPrice = double.NaN;
                updated = true;
            }

            if (CumulativeQuantity > 0)
            {
                if (!double.IsNaN(AdjAveragePrice))
                {
                    AdjAveragePrice = double.NaN;
                    updated = true;
                }
            }

            if (updated)
            {
                NotifyOfDeltaAdjValuesUpdate();
            }
        }

        public OmsOrder ToOrder()
        {
            OmsOrder order = new()
            {
                Guid = Guid,
                EdgeOverride = Math.Round(EdgeToTheo, 2),
                UserData = UnboundDataColumnToValueMap,
                OrderStatus = OrderStatus,
                LastUpdateTime = LastUpdateTime,
                Timestamp = Timestamp,
                TransactionID = TransactionID,
                Source = Source,
                Tag = Tag,
                Trader = Trader,
                TagEdge = TagEdge,
                TagMid = TagMid,
                TagBid = TagBid,
                TagAsk = TagAsk,
                TagTheo = TagTheo,
                Type = Type,
                Subtype = SubType?.ToString().FromCamelCase(),
                TagEma = TagEma,
                Comment = Comment,
                ExchangeOrderID = ExchangeOrderID,
                ExecutingBroker = ExecutingBroker,
                ExecutionID = ExecutionID,
                ExecutionReferenceID = ExecutionReferenceID,
                LastExchange = LastExchange,
                Exchanges = Exchanges,
                Fee1 = Fee1,
                Fee2 = Fee2,
                Bid = Bid,
                Ask = Ask,
                UnderBid = UnderBid,
                UnderAsk = UnderAsk,
                TV = TV,
                Delta = Delta,
                ExchangeFee1 = ExchangeFee1,
                ExchangeFee2 = ExchangeFee2,
                BrokerFee1 = BrokerFee1,
                BrokerFee2 = BrokerFee2,
                SubmitTime = SubmitTime,
                LastQuantity = LastQuantity,
                FilledQty = CumulativeQuantity,
                LeavesQuantity = LeavesQuantity,
                CumulativeQuantity = CumulativeQuantity,
                PermID = PermID,
                OrderID = OrderID,
                OriginalOrderID = OriginalOrderID,
                Username = Username,
                AccountAcronym = AccountAcronym,
                Symbol = Symbol,
                UnderlyingSymbol = UnderlyingSymbol,
                RequestAccountAcronym = RequestAccountAcronym,
                Route = Route,
                RequestSymbol = RequestSymbol,
                SideString = Side.ToString(),
                Side = Side,
                Price = Price,
                Quantity = Quantity,
                Destination = Destination,
                PositionEffect = PositionEffect.ToString(),
                RoutingSession = RoutingSession,
                ClearingFirm = ClearingFirm,
                ClearingID = ClearingID,
                AccountID = AccountID,
                AveragePrice = AveragePrice,
                MultiLeg = IsComplexOrder,
                Description = Description,
                SpreadId = SpreadId,
                LegsCount = LegsCount,
                TotalContracts = TotalContracts,
                TimeInForce = TimeInForce,
            };

            if (Legs != null)
            {
                foreach (IComplexOrderLeg leg in Legs)
                {
                    OmsOrderLeg omsOrderLeg = new()
                    {
                        ExchangeFee2 = leg.ExchangeFee2,
                        ExchangeFee1 = leg.ExchangeFee1,
                        Fee2 = leg.Fee2,
                        Fee1 = leg.Fee1,
                        Delta = leg.Delta,
                        TV = leg.TV,
                        Ask = leg.Ask,
                        Bid = leg.Bid,
                        AveragePrice = leg.AveragePrice,
                        CumulativeQuantity = leg.CumulativeQuantity,
                        LastQuantity = leg.LastQuantity,
                        LeavesQuantity = leg.LeavesQuantity,
                        LastPrice = leg.LastPrice,
                        OrderStatus = leg.OrderStatus.ToString(),
                        Side = leg.Side,
                        Quantity = leg.Quantity,
                        Ratio = leg.Ratio,
                        PositionEffect = leg.PositionEffect.ToString(),
                        Symbol = leg.Symbol,
                        LegID = leg.LegID,
                        ExecutionID = leg.ExecutionID,
                        OrderID = leg.OrderID,
                        PermID = leg.PermID,
                        TransactionID = leg.TransactionID,
                        Timestamp = leg.Timestamp,
                        LastUpdateTime = leg.LastUpdateTime,
                        BrokerFee1 = leg.BrokerFee1,
                        BrokerFee2 = leg.BrokerFee2
                    };
                    order.Legs.Add(omsOrderLeg);
                }
            }

            return order;
        }

        internal void ResetTransactionSpecificProperties()
        {
            Guid = System.Guid.NewGuid().ToString();
            FillTime = double.NaN;
            TradeToNewTime = double.NaN;
            SubmitToNewTime = double.NaN;
            NewToCancelTime = double.NaN;
            OrderStatus = OrderStatus.DoneForDay;
            Timestamp = DateTime.Now;
            TransactionID = 0;
            Source = String.Empty;
            ExchangeOrderID = String.Empty;
            ExecutingBroker = String.Empty;
            ExecutionID = String.Empty;
            ExecutionReferenceID = String.Empty;
            LastExchange = String.Empty;
            Exchanges = String.Empty;
            Fee1 = 0.0;
            Fee2 = 0.0;
            Bid = 0.0;
            Ask = 0.0;
            UnderBid = 0.0;
            UnderAsk = 0.0;
            TV = 0.0;
            Delta = 0.0;
            ExchangeFee1 = 0.0;
            ExchangeFee2 = 0.0;
            BrokerFee1 = 0.0;
            BrokerFee2 = 0.0;
            SubmitTime = DateTime.Now;
            LastQuantity = 0;
            FilledQty = 0;
            LeavesQuantity = 0;
            CumulativeQuantity = 0;
            PermID = String.Empty;
            OrderID = String.Empty;
            OriginalOrderID = String.Empty;
            Position = 0;
            RawPosition = 0;
            RealizedPnL = 0.0;
            AdjustedPnl = 0.0;
            UnrealizedPnl = 0.0;
            NetPnl = 0.0;
            BestBuyEdgeToTheo = double.NaN;
            WorstBuyEdgeToTheo = double.NaN;
            BestSellEdgeToTheo = double.NaN;
            WorstSellEdgeToTheo = double.NaN;
            Destination = String.Empty;
            RoutingSession = String.Empty;
            ClearingFirm = String.Empty;
            ClearingID = String.Empty;
            AveragePrice = 0.0;
            BidPercentOfFillPrice = 0.0;
            CloseBidPercentOfFillPrice = 0.0;
            OmsBidPercentOfFillPrice = 0.0;
            OmsBestBidPercent = 0.0;
        }

        internal void Subscribe()
        {
            try
            {
                Subscribed = true;
                if (!string.IsNullOrWhiteSpace(SpreadId))
                {
                    _portfolioManager?.Subscribe(SpreadId, SubscriptionFieldType.FirmSpreadPosition, this);
                    SubscribeToHardSide();
                }
                else
                {
                    _log.Warn("Spread ID not set.");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Subscribe));
            }
        }

        private void SubscribeToHardSide()
        {
            if (OmsCore.Config.SubscribeToHardSideIdentification)
            {
                if (!_subscribedToHardSide)
                {
                    _subscribedToHardSide = true;
                    HardSideKey? hardSideKey = this.GetHardSideKey();
                    if (hardSideKey != null)
                    {
                        var symbol = hardSideKey.ToString();
                        _portfolioManager?.Subscribe(symbol, SubscriptionFieldType.HardSide, this);
                    }
                }
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                switch (key.Type)
                {
                    case SubscriptionFieldType.FirmSpreadPosition:
                        if (value is IPosition positionUpdate && positionUpdate.Name == SpreadId)
                        {
                            UpdateSpreadPositionUi(positionUpdate);
                        }
                        break;
                    case SubscriptionFieldType.HardSide:
                        HandleHardSideUpdate(value as HardSideResult);
                        break;
                    case SubscriptionFieldType.LastPrice:
                        if (value is double last)
                        {
                            Last = last;
                            PropertyChangedSafe(nameof(Last));
                        }
                        break;
                    case SubscriptionFieldType.MidPoint:
                        if (value is double mid)
                        {
                            LiveUnderMid = mid;
                            string[] propertyNames = [nameof(LiveUnderMid), nameof(AdjAveragePrice), nameof(AveragePriceDiff)];
                            PropertiesChangedSafe(propertyNames);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        public void UpdateSpreadPositionUi(IPosition spreadPosition)
        {
            try
            {
                UpdateSpreadPositionFields(spreadPosition);
                if (spreadPosition.TotalFills > 0)
                {
                    SubscribeToHardSide();
                }
                if (spreadPosition.HardSide.HasValue &&
                    (OmsCore.Config.SubscribeToHardSideIdentification ||
                     OmsCore.Config.SubscribeToHardSideIdentificationOnTickets))
                {
                    Task.Run(() => ProcessHardSideUpdate(spreadPosition));
                }
            }
            catch (InvalidOperationException)
            {
                UpdateSpreadPositionSafe(spreadPosition);
                _log.Info($"{nameof(UpdateSpreadPositionUi)} -> Update values failed using dispatcher instead.");
            }
        }

        private void HandleHardSideUpdate(HardSideResult hardSideResult)
        {
            HardSideKey key = hardSideResult.HardSideKey;
            bool isValid = !IsComplexOrder
                           || (key.BaseStrategy is BaseStrategy.CALL_CALENDAR or BaseStrategy.PUT_CALENDAR or BaseStrategy.CALL_DIAGONAL or BaseStrategy.PUT_DIAGONAL)
                           || (Legs != null && Legs.Where(x => x.Security != null && x.Security.SecurityType == ZeroPlus.Models.Data.Enums.SecurityType.Option).Select(x => (x.Security as Option)!.Strike).OrderBy(x => x).ToList().ValidateHardSideStrikes(hardSideResult.Strikes));
            if (isValid)
            {
                HardSide = hardSideResult.HardSide;
                HardSideDesignationTime = hardSideResult.DesignationTime;
                HardSideBuyGiveUp = hardSideResult.HardSideBuyGiveUp;
                HardSideSellGiveUp = hardSideResult.HardSideSellGiveUp;

                RaisePropertiesChanged(_hardSidePropertyNames);
            }
        }

        private void ProcessHardSideUpdate(IPosition spreadPosition)
        {
            try
            {
                var key = this.GetHardSideKey();
                if (key.HasValue)
                {
                    if (!_spreadKeyToHardSideMapping.TryGetValue(key.Value, out var model) || model == null)
                    {
                        model = new HardSideResult
                        {
                            HardSideKey = key.Value
                        };
                        _spreadKeyToHardSideMapping[key.Value] = model;
                        model.Strikes = IsComplexOrder ? Legs.Where(x => x.Security is { SecurityType: ZeroPlus.Models.Data.Enums.SecurityType.Option }).Select(x => ((Option)x.Security).Strike).OrderBy(x => x).ToList() : null;
                    }

                    model.HardSide = spreadPosition.HardSide!.Value;
                    model.HardSideBuyGiveUp = spreadPosition.HardSideBuyGiveUp;
                    model.HardSideSellGiveUp = spreadPosition.HardSideSellGiveUp;
                    model.DesignationTime = spreadPosition.HardSideDesignationTime;

                    _portfolioManager.HardSideUpdated(model);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessHardSideUpdate));
            }
        }

        private void UpdateSpreadPositionSafe(IPosition spreadPosition)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    UpdateSpreadPositionFields(spreadPosition);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(UpdateSpreadPositionSafe));
                }
            }, DispatcherPriority.Render);
        }

        private void UpdateSpreadPositionFields(IPosition spreadPosition)
        {
            bool updated = false;
            if (Position != spreadPosition.NetQty)
            {
                Position = spreadPosition.NetQty;
                RawPosition = spreadPosition.RawNetQty;
                updated = true;
            }
            if (!FirstEdgeAcquired && spreadPosition.FirstEdgeAcquired)
            {
                FirstEdge = spreadPosition.FirstEdge;
                FirstEdgeAcquired = spreadPosition.FirstEdgeAcquired;
                updated = true;
            }
            if (BestSellPrice != spreadPosition.BestSellPrice)
            {
                BestSellPrice = spreadPosition.BestSellPrice;
                updated = true;
            }
            if (BestSellPriceUnderMid != spreadPosition.BestSellPriceUnderMid)
            {
                BestSellPriceUnderMid = spreadPosition.BestSellPriceUnderMid;
                updated = true;
            }
            if (BestBuyPrice != spreadPosition.BestBuyPrice)
            {
                BestBuyPrice = spreadPosition.BestBuyPrice;
                updated = true;
            }
            if (BestBuyPriceUnderMid != spreadPosition.BestBuyPriceUnderMid)
            {
                BestBuyPriceUnderMid = spreadPosition.BestBuyPriceUnderMid;
                updated = true;
            }
            if (RealizedPnL != spreadPosition.RealizedPnl)
            {
                RealizedPnL = spreadPosition.RealizedPnl;
                updated = true;
            }
            if (AdjustedPnl != spreadPosition.AdjustedPnl)
            {
                AdjustedPnl = spreadPosition.AdjustedPnl;
                updated = true;
            }
            if (UnrealizedPnl != spreadPosition.UnrealizedPnl)
            {
                UnrealizedPnl = spreadPosition.UnrealizedPnl;
                updated = true;
            }
            NetPnl = RealizedPnL + AdjustedPnl;
            if (BestBuyEdgeToTheo != spreadPosition.BestBuyEdgeToTheo)
            {
                BestBuyEdgeToTheo = spreadPosition.BestBuyEdgeToTheo;
                updated = true;
            }
            if (WorstBuyEdgeToTheo != spreadPosition.WorstBuyEdgeToTheo)
            {
                WorstBuyEdgeToTheo = spreadPosition.WorstBuyEdgeToTheo;
                updated = true;
            }
            if (BestSellEdgeToTheo != spreadPosition.BestSellEdgeToTheo)
            {
                BestSellEdgeToTheo = spreadPosition.BestSellEdgeToTheo;
                updated = true;
            }
            if (WorstSellEdgeToTheo != spreadPosition.WorstSellEdgeToTheo)
            {
                WorstSellEdgeToTheo = spreadPosition.WorstSellEdgeToTheo;
                updated = true;
            }
            if (OpenSubsCount != spreadPosition.OpenSubsCount)
            {
                OpenSubsCount = spreadPosition.OpenSubsCount;
                updated = true;
            }
            if (SubsBetweenFillsCount != spreadPosition.SubsBetweenFillsCount)
            {
                SubsBetweenFillsCount = spreadPosition.SubsBetweenFillsCount;
                updated = true;
            }
            if (updated)
            {
                RaisePropertiesChanged(_positionPropertyNames);
            }
        }

        private void PropertiesChangedSafe(string[] propertyNames)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    RaisePropertiesChanged(propertyNames);
                }, DispatcherPriority.ContextIdle);
            }
            catch (Exception)
            {
            }
        }

        private void PropertyChangedSafe(string propertyNames)
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    RaisePropertyChanged(propertyNames);
                }, DispatcherPriority.ContextIdle);
            }
            catch
            {
                // ignored
            }
        }

        internal void NotifyOfUpdate()
        {
            try
            {
                RaisePropertiesChanged(_updatePropertyNames);
            }
            catch (Exception)
            {
                NotifyUpdatesSafe();
            }
        }

        private void NotifyUpdatesSafe()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                RaisePropertiesChanged(_updatePropertyNames);
            }, DispatcherPriority.ContextIdle);
        }

        internal void NotifyOfIndicatorUpdate()
        {
            try
            {
                RaisePropertiesChanged(_indicatorPropertyNames);
            }
            catch (Exception)
            {
                NotifyOfIndicatorUpdateSafe();
            }
        }

        internal void NotifyOfTagUpdate()
        {
            try
            {
                RaisePropertiesChanged(_tagPropertyNames);
            }
            catch
            {
                // ignored
            }
        }

        private void NotifyOfIndicatorUpdateSafe()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                RaisePropertiesChanged(_indicatorPropertyNames);
            }, DispatcherPriority.ContextIdle);
        }

        internal void NotifyOfDeltaAdjValuesUpdate()
        {
            try
            {
                RaisePropertiesChanged(_deltaAdjustedPropertyNames);
            }
            catch
            {
                // ignored
            }
        }

        internal void Update(OmsOrder order)
        {
            if (order.Legs?.Count > 1)
            {
                IsComplexOrder = true;
            }

            OrderStatus = order.OrderStatus;

            FillTime = order.FillTime;
            TradeToNewTime = order.TradeToNewTime;
            SubmitToNewTime = order.SubmitToNewTime;
            NewToCancelTime = order.NewToCancelTime;
            UnboundDataColumnToValueMap = order.UserData;
            Guid = order.Guid;
            LastUpdateTime = order.LastUpdateTime;
            Timestamp = order.Timestamp;
            TransactionID = order.TransactionID;
            Source = order.Source;
            Tag = order.Tag;
            FullTag = order.FullTag;
            Trader = order.Trader;
            TagEdge = order.TagEdge;
            TagMid = order.TagMid;
            TagBid = order.TagBid;
            TagAsk = order.TagAsk;
            TagTheo = order.TagTheo;
            Type = order.Type;
            SubType = order.Subtype?.TryGetSubType();
            TagEma = order.TagEma;
            Comment = order.Comment;
            ExchangeOrderID = order.ExchangeOrderID;
            ExecutingBroker = order.ExecutingBroker;
            ExecutionID = order.ExecutionID;
            ExecutionReferenceID = order.ExecutionReferenceID;
            LastExchange = order.LastExchange;
            Exchanges = order.Exchanges;
            Fee1 = order.Fee1;
            Fee2 = order.Fee2;
            ExchangeFee1 = order.ExchangeFee1;
            ExchangeFee2 = order.ExchangeFee2;
            BrokerFee1 = order.BrokerFee1;
            BrokerFee2 = order.BrokerFee2;
            SubmitTime = order.SubmitTime;
            LastQuantity = order.LastQuantity;
            FilledQty = order.FilledQty;
            LeavesQuantity = order.LeavesQuantity;
            CumulativeQuantity = order.CumulativeQuantity;
            PermID = order.PermID;
            OrderID = order.OrderID;
            OriginalOrderID = order.OriginalOrderID;
            Username = order.Username;
            AccountAcronym = order.AccountAcronym;
            Symbol = order.Symbol;
            UnderlyingSymbol = order.UnderlyingSymbol;
            RequestAccountAcronym = order.RequestAccountAcronym;
            Route = order.Route;
            RequestSymbol = order.RequestSymbol;
            Price = order.Price;
            Quantity = order.Quantity;
            Position = order.Position;
            RealizedPnL = order.RealizedPnL;
            AdjustedPnl = order.AdjustedPnl;
            Destination = order.Destination;
            TimeInForce = order.TimeInForce;
            RoutingSession = order.RoutingSession;
            ClearingFirm = order.ClearingFirm;
            ClearingID = order.ClearingID;
            AccountID = order.AccountID;
            AveragePrice = order.AveragePrice;
            BidPercentOfFillPrice = order.BidPercentOfFillPrice;
            CloseBidPercentOfFillPrice = order.CloseBidPercentOfFillPrice;
            OmsBidPercentOfFillPrice = order.OmsBidPercentOfFillPrice;
            Bid = Math.Round(order.Bid, 2, MidpointRounding.AwayFromZero);
            Ask = Math.Round(order.Ask, 2, MidpointRounding.AwayFromZero);
            UnderBid = Math.Round(order.UnderBid, 2, MidpointRounding.AwayFromZero);
            UnderAsk = Math.Round(order.UnderAsk, 2, MidpointRounding.AwayFromZero);
            TV = Math.Round(order.TV, 2, MidpointRounding.AwayFromZero);
            Delta = Math.Round(order.Delta, 2, MidpointRounding.AwayFromZero);
            EdgeOverride = order.EdgeOverride;
            AdjustedEdgeOverride = order.AdjustedEdgeOverride;
            EdgeCurveAdjustment = order.EdgeCurveAdjustment;
            if (Enum.TryParse(order.SideString, ignoreCase: true, out Side side))
            {
                Side = side;
            }

            var legsCount = order.Legs?.Count ?? 0;
            int legContracts = (legsCount) == 0 ? 1 : 0;

            for (int i = 0; i < legsCount; i++)
            {
                OmsOrderLeg leg = order.Legs[i];
                legContracts += leg.Ratio;
                if (leg.LegID is null or "")
                {
                    leg.LegID = $"leg{i}";
                }
                UpdateLeg(leg);
            }

            HanweckTotalTheo = order.HanweckTotalTheo;
            HanweckTotalGamma = order.HanweckTotalGamma;
            HanweckTotalVega = order.HanweckTotalVega;
            HanweckTotalTheta = order.HanweckTotalTheta;
            HanweckTotalRho = order.HanweckTotalRho;
            HanweckTotalIV = order.HanweckTotalIV;
            HanweckTotalUnder = order.HanweckTotalUnder;
            HanweckTotalUBid = order.HanweckTotalUBid;
            HanweckTotalUAsk = order.HanweckTotalUAsk;
            HanweckTotalBid = order.HanweckTotalBid;
            HanweckTotalAsk = order.HanweckTotalAsk;
            CloseTV = order.CloseTV;
            CloseDelta = order.CloseDelta;
            CloseTotalDelta = order.CloseTotalDelta;
            CloseHanweckTotalTheo = order.CloseHanweckTotalTheo;
            CloseHanweckTotalGamma = order.CloseHanweckTotalGamma;
            CloseHanweckTotalVega = order.CloseHanweckTotalVega;
            CloseHanweckTotalTheta = order.CloseHanweckTotalTheta;
            CloseHanweckTotalRho = order.CloseHanweckTotalRho;
            CloseHanweckTotalIV = order.CloseHanweckTotalIV;
            CloseBid = order.CloseBid;
            CloseAsk = order.CloseAsk;
            CloseUnderBid = order.CloseUnderBid;
            CloseUnderAsk = order.CloseUnderAsk;
            CloseHanweckTotalUnder = order.CloseHanweckTotalUnder;
            CloseHanweckTotalUBid = order.CloseHanweckTotalUBid;
            CloseHanweckTotalUAsk = order.CloseHanweckTotalUAsk;
            DeltaAdjustedTheo = order.DeltaAdjustedTheo;
            CloseDeltaAdjustedTheo = order.CloseDeltaAdjustedTheo;
            BidSize = order.BidSize;
            AskSize = order.AskSize;
            CloseBidSize = order.CloseBidSize;
            CloseAskSize = order.CloseAskSize;
            UnderlyingBidSize = order.UnderlyingBidSize;
            UnderlyingAskSize = order.UnderlyingAskSize;
            CloseUnderlyingBidSize = order.CloseUnderlyingBidSize;
            CloseUnderlyingAskSize = order.CloseUnderlyingAskSize;
            CloseHanweckTotalBid = order.CloseHanweckTotalBid;
            CloseHanweckTotalAsk = order.CloseHanweckTotalAsk;
            int totalContracts = legContracts * order.FilledQty;
            TotalContracts = totalContracts;
            if (AveragePrice != 0)
            {
                double tagTheo = TagTheo;
                if (HanweckTotalTheo < 0 && AveragePrice < 0 && TagTheo > 0)
                {
                    tagTheo *= -1;
                }
                double edgeToTheo = tagTheo - AveragePrice;

                EdgeToTheo = edgeToTheo;
            }
            CheckUnderlying();

            OptionStrategy.EvaluateOrder(this, out var baseType, out var spreadType, out var description);
            BaseStrategy = OptionStrategy.ConvertFromString(baseType);
            Description = description;
            SpreadId = spreadType;
        }

        private void UpdateLeg(OmsOrderLeg leg)
        {
            try
            {
                OmsOrderLegModel omsOrderLeg = (OmsOrderLegModel)GetLeg(leg.LegID);
                Legs ??= [];
                Legs.Add(omsOrderLeg);
                omsOrderLeg.Update(leg);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UpdateLeg)} -> Exception updating leg.");
            }
        }

        internal void CheckUnderlying()
        {
            try
            {
                if (string.IsNullOrEmpty(UnderlyingSymbol) &&
                    !string.IsNullOrEmpty(Symbol))
                {
                    SymbolLib.SymbolCodec spread = new(Symbol);
                    UnderlyingSymbol = spread.UnderlyingSymbol();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckUnderlying));
            }
        }

        private static string GetConditionCode(char condition)
        {
            return condition switch
            {
                'a' => "SingLegAuct",
                'b' => "SingLegAuctISO",
                'c' => "SingLegCross",
                'd' => "SingLegCrossISO",
                'e' => "SingLegFloor",
                'f' => "MultLegAutoEx",
                'g' => "MultLegAuct",
                'h' => "MultLegCross",
                'i' => "MultLegFlr",
                'j' => "MultAutoSingLeg",
                'k' => "MultStkOptAuct",
                'l' => "MultLegAuctSingLeg",
                'm' => "MultLegFlrSingLeg",
                'n' => "MultStkOptAutoEx",
                'o' => "MultStkOptCrossAutoEx",
                'p' => "MultStkOptFlr",
                'q' => "MultStkOptAutoSingLeg",
                'r' => "MultStkOptAuctSingLeg",
                's' => "MultStkOptFlrSingLeg",
                't' => "MultLegFlrPropProd",
                'u' => "MultComprPropProd",
                'v' => "ExtendedHrs",
                'A' => "Canceled",
                'B' => "LateOutOfSeq",
                'C' => "CanceledLast",
                'D' => "Late",
                'E' => "CanceledOpen",
                'F' => "OpenLate",
                'G' => "CanceledOnly",
                'H' => "OpenLast",
                'I' => "Auto",
                'J' => "Reopen",
                'S' => "ISOI",
                _ => " ",
            };
        }

        private double GetPercentBidInTheoRange(double value)
        {
            if (double.IsNaN(value) || double.IsNaN(TheoBid) || double.IsNaN(TheoAsk))
            {
                return double.NaN;
            }

            var theoRange = TheoAsk - TheoBid;
            if (Math.Abs(theoRange) <= double.Epsilon)
            {
                return double.NaN;
            }

            return (value - TheoBid) / theoRange;
        }
    }
}