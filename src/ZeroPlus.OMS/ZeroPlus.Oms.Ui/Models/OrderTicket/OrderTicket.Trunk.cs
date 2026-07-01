using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Editors;
using DevExpress.Xpf.Editors.Native;
using DevExpress.Xpf.Editors.Popups;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Managers;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Automation;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using ZeroPlus.TagCodecLib;
using ZeroPlus.Telemetry.Client.Helpers;
using BaseStrategy = ZeroPlus.Models.Data.Enums.BaseStrategy;
using ComplexOrderLeg = ZeroPlus.Models.Data.Trading.ComplexOrderLeg;
using IOrder = ZeroPlus.Models.Data.Trading.Interfaces.IOrder;
using OrderRoutingOrderType = ZeroPlus.Models.Data.Models.OrderRouting.OrderType;
using OrderStatus = ZeroPlus.Models.Data.Enums.OrderStatus;
using OrderType = ZeroPlus.Models.Data.Enums.OrderType;
using PositionEffect = ZeroPlus.Models.Data.Enums.PositionEffect;
using Side = ZeroPlus.Models.Data.Enums.Side;
using SpreadLeg = ZeroPlus.Models.Data.Matrix.SpreadLeg;
using TimeInForce = ZeroPlus.Models.Data.Enums.TimeInForce;
using Timer = System.Timers.Timer;
using Venue = ZeroPlus.Models.Data.Enums.Venue;

namespace ZeroPlus.Oms.Ui.Models
{
    internal readonly record struct GetTheoResult(double NetTheo, double NetDeltaAdjTheo);

    public abstract partial class OrderTicket : CustomizableTableViewModelBase, IOmsDataSubscriber, IOmsOrderUpdateSubscriber, IOrder, IComplexOrder, ILoopSettings, IDisposable
    {
        public TimeSpan _pmCurbSessionStartEastern = new TimeSpan(16, 15, 0);
        public TimeSpan _pmCurbSessionEndEastern = new TimeSpan(17, 0, 0);

        #region Properties
        public delegate void OrderStatusUpdatedEventHandler(OrderTicket order, OrderStatus orderStatus);
        public delegate void IOrderStatusUpdatedEventHandler(IOmsOrder order, OrderStatus orderStatus, OrderTicket ticket);
        public delegate void TradeEventHandler(OrderTicket order, IOmsOrder trade);
        public delegate void SpreadPnlUpdatedEventHandler();
        public delegate void LcdPositionUpdatedEventHandler();
        public delegate void ActivateWindowEventHandler();
        public delegate bool ValidationHandler();
        public delegate void LoopCommandEventHandler(bool start);

        public event LoopCommandEventHandler LoopCommandEvent;
        public event OrderStatusUpdatedEventHandler OrderFilledUpdatedEvent;
        public event IOrderStatusUpdatedEventHandler OrderClosedUpdateEvent;
        public event TradeEventHandler TradeEvent;
        public event ActivateWindowEventHandler ActivateWindow;

        public const int SINGLE_LEG_AUCTION = 25;
        public const int SPREAD_AUCTION = 125;

        public const int SPX_AUCTION = 100;
        public const int SPX_SPREAD_AUCTION = 125;

        private const int BASE_TIMEOUT = 999;

        internal static int DataLoadTimeout = BASE_TIMEOUT;
        internal static int EmaLoadTimeout = BASE_TIMEOUT;
        internal static int WeightedVegaLoadTimeout = BASE_TIMEOUT;

        private volatile TaskCompletionSource<bool> _dataLoadNotification = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected static readonly ConcurrentDictionary<Tuple<string, Side?>, double> _spreadIdAndSideToLastAvgFillPxMap = new();

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly DateTime _epochDate = new DateTime(1970, 1, 1).Date;
        private static readonly object _hedgeMasterLock = new();
        private static readonly ConcurrentDictionary<string, OrderTicket> _spreadIdToHedgingInstanceMap = new();
        private static readonly ConcurrentDictionary<string, object> _spreadIdToHedgeLockMap = new();
        private static readonly ConcurrentDictionary<string, Side?> _openingOrderToSideMap = new();
        private static readonly PriceCacheManager _priceCacheManager = new();

        protected readonly NotificationManager _notificationManager;
        protected readonly PortfolioManagerModel _portfolioManagerModel;
        protected readonly TransactionConsumerModel _transactionConsumer;
        protected readonly Stopwatch _latencyTimer = new();
        protected readonly object _legUpdateLock = new();
        private long _omsOrderInitiatedNanos;
        private string _lastReloadedAccount;

        private readonly IAbstractFactory<ComplexOrderTicketViewModel> _ticketFactory;
        private readonly IAbstractFactory<ThreeWayCloser> _threeWayCloserFactory;
        private readonly Timer _fishTimer;
        private readonly Timer _smartRouteOverwatchTimer = new();
        private readonly ConcurrentDictionary<string, string> _orderDetailsContainer = new();
        private readonly Timer _contraSmartRouteOverwatchTimer = new();
        private readonly object _stopLossLock = new();
        private readonly object _orderLock = new();

        private Notifier[] _notifiers;
        private int _notifiersCount;
        private bool _subscribedToHardSide;
        private DateTime _mainNewTimestamp;
        private DateTime _contraNewTimestamp;
        private string _lastContraRoute;
        protected bool _cancelRequestSent = false;
        private bool _cancelContraRequestSent = false;

        private IPosition _lastTraderPositionUpdate;
        private IPosition _lastFirmPositionUpdate;

        private double _referenceTradeOriginalPrice;
        private FishRoute _routeFish;
        private FishRoute _manualRouteFish;
        private BasketSettings _basketSettings;
        protected UnderlyingRiskModel _riskModel = UnderlyingRiskModel.Default;
        private uint _deltaAdjTheoSequence = 0;
        private ValueCompare _theoToMid;
        private DateTime _highestTheoChangeUpdate;
        private decimal _defaultIncrement = .01M;

        #region Notifier Callbacks
        partial void CoerceUnderlying(ref string value) => value = OptionsHelper.IsIndex(value) ? "$" + value : value;
        partial void CoerceHedgeUnderlying(ref string value) => value = OptionsHelper.IsIndex(value) ? "$" + value : value;
        partial void CoerceDescription(ref string value) => value ??= "";
        partial void OnSpeedTraderClosingTypeChanged(SpeedTraderClosingType value) => UpdateTicketClosingMode();
        partial void OnLoopFreeLookModeChanged(bool? value) => SetFreeLookMode();
        partial void OnLcdChanged(int value) => UpdateQty(value);
        partial void OnTotalDeltaChanged(double value) { Delta = value; UpdateStockPositions(); }
        partial void OnEdgeOverrideChanged(double value) { if (!double.IsNaN(value)) SetEdgeAferEdgeOverideChange(); }
        partial void OnAdjustedEdgeOverrideChanged(double value) { if (!double.IsNaN(value)) SetEdgeAferEdgeOverideChange(); }
        partial void OnEdgeCurveAdjustmentChanged(double value) { if (!double.IsNaN(value)) SetEdgeAferEdgeOverideChange(); }
        #endregion

        #region Notifier Properties
        [NotifyProperty(CheckEquality = false)]
        public partial bool SingleOrderTicketStopLossEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SingleOrderTicketStopLossUsePercentage { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketStopLossValue { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketStopLoss { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketStopLossPercentage { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SingleOrderTicketTrailingStopEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SingleOrderTicketTrailingStopUsePercentage { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketTrailingStopValue { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketTrailingStop { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SingleOrderTicketTrailingStopPercentage { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial SpeedTraderClosingType SpeedTraderClosingType { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LoopInterval { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LoopIntervalMax { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ContraFishEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CloseEdgeOveride { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int SizeOveride { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LoopMinEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeGiveUp { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CloseSubs { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double OrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastEdgeTightenPercent { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ResubmitCount { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TotalEstimatedResubmit { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TotalResubmitCount { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ResubmitAfterLastLoopCount { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int SpeedTraderMaxLoopCount { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LoopMaxLoss { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ContraFishPriceIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LoopResubmit { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ContraFishInterval { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ContraFishIntervalMax { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AutomationRequiredPartialFillPercentage { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int AutomationPartialResubmitCount { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LoopAutoSizeup { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LoopSizeupQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LoopCountBeforeSizeup { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LoopFreeLook { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LoopFreeLookOnAll { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool FreeLookRequireMinFillTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FreeLookMinFillTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool FreeLookWhenGettingCloseEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LoopFreeLookOnAllUsingTicks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FreeLookOnAllIncrementTicks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FreeLookOnAllIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool? LoopFreeLookMode { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial LoopPricingMode LoopPricingMode { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AutoCloseToggled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AutoCloseArmed { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial AutoCloseConfigViewModel AutoCloseConfigModel { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<AutoCloseConfigViewModel> AutoCloseConfigModels { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsSingleLegSell { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool TrackerToggled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool DerivativeLoaded { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockLowPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockMidPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockHighPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockContraLowPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockContraMidPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockContraHighPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TicketLegModel Leg1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TicketLegModel Leg2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TicketLegModel Leg3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TicketLegModel Leg4 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double NotionalLastEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastBuyEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastSellEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BestEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastOrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastContraOrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastFilledOrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastFilledClosingOrderEdgeToTheo { get; set; }
        [NotifyProperty]
        public partial int Lcd { get; set; }
        [NotifyProperty]
        public partial double MktMkrBid { get; set; }
        [NotifyProperty]
        public partial double MktMkrAsk { get; set; }
        [NotifyProperty]
        public partial double HighestBid { get; set; }
        [NotifyProperty]
        public partial double LowestAsk { get; set; }
        [NotifyProperty]
        public partial double BestEdgeBid { get; set; }
        [NotifyProperty]
        public partial double BestEdgeMid { get; set; }
        [NotifyProperty]
        public partial double BestEdgeAsk { get; set; }
        [NotifyProperty]
        public partial double Low { get; set; }
        [NotifyProperty]
        public partial double BestLow { get; set; }
        [NotifyProperty]
        public partial double BestBidInt { get; set; }
        [NotifyProperty]
        public partial double BestMidInt { get; set; }
        [NotifyProperty]
        public partial double BestAskInt { get; set; }
        [NotifyProperty]
        public partial double LowInt { get; set; }
        [NotifyProperty]
        public partial double LowDerived { get; set; }
        [NotifyProperty]
        public partial double LowIntDerived { get; set; }
        [NotifyProperty]
        public partial bool LowIntEdge { get; set; }
        [NotifyProperty]
        public partial double BestHigh { get; set; }
        [NotifyProperty]
        public partial double High { get; set; }
        [NotifyProperty]
        public partial int BidSize { get; set; }
        [NotifyProperty]
        public partial int AskSize { get; set; }
        [NotifyProperty]
        public partial bool IsCheapo { get; set; }
        [NotifyProperty]
        public partial double HighInt { get; set; }
        [NotifyProperty]
        public partial double HighDerived { get; set; }
        [NotifyProperty]
        public partial double HighIntDerived { get; set; }
        [NotifyProperty]
        public partial bool HighIntEdge { get; set; }
        [NotifyProperty]
        public partial double Mid { get; set; }
        [NotifyProperty]
        public partial double MidDerived { get; set; }
        [NotifyProperty]
        public partial double MidInt { get; set; }
        [NotifyProperty]
        public partial double MidIntDerived { get; set; }
        [NotifyProperty]
        public partial double BidEma { get; set; }
        [NotifyProperty]
        public partial double BidEmaAdj { get; set; }
        [NotifyProperty]
        public partial double AskEma { get; set; }
        [NotifyProperty]
        public partial double AskEmaAdj { get; set; }
        [NotifyProperty]
        public partial double AdjEma { get; set; }
        [NotifyProperty]
        public partial double FullEma { get; set; }
        [NotifyProperty]
        public partial double UnderEma { get; set; }
        [NotifyProperty]
        public partial double Ema { get; set; }
        [NotifyProperty]
        public partial double BidIvEma { get; set; }
        [NotifyProperty]
        public partial double AskIvEma { get; set; }
        [NotifyProperty]
        public partial double Width { get; set; }
        [NotifyProperty]
        public partial bool ShowWidthNotification { get; set; }
        [NotifyProperty]
        public partial bool NetTheoSynched { get; set; }
        [NotifyProperty]
        public partial bool DeltaAdjTheoSynched { get; set; }
        [NotifyProperty]
        public partial uint DeltaAdjTheoSequence { get; set; }
        [NotifyProperty]
        public partial double NetTheo { get; set; }
        [NotifyProperty]
        public partial double NetDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double TheoBid { get; set; }
        [NotifyProperty]
        public partial double TheoAsk { get; set; }
        [NotifyProperty]
        public partial double DigBid { get; set; }
        [NotifyProperty]
        public partial double DigAsk { get; set; }
        [NotifyProperty]
        public partial uint DigBidSize { get; set; }
        [NotifyProperty]
        public partial uint DigAskSize { get; set; }
        [NotifyProperty]
        public partial ValueCompare TheoToMid { get; set; }
        [NotifyProperty]
        public partial double AdjTheoToMid { get; set; }
        [NotifyProperty]
        public partial double NetTestValue { get; set; }
        [NotifyProperty]
        public partial double TheoDiff { get; set; }
        [NotifyProperty]
        public partial bool TheoJumpDetected { get; set; }
        [NotifyProperty]
        public partial double HighestTheoChange { get; set; }
        [NotifyProperty]
        public partial double NetDelta { get; set; }
        [NotifyProperty]
        public partial double NetGamma { get; set; }
        [NotifyProperty]
        public partial double NetTheta { get; set; }
        [NotifyProperty]
        public partial double TotalDelta { get; set; }
        [NotifyProperty]
        public partial double TotalDeltaDirection { get; set; }
        [NotifyProperty]
        public partial double TotalVolume { get; set; }
        [NotifyProperty]
        public partial double OpenInterest { get; set; }
        [NotifyProperty]
        public partial double FirmTotalVolume { get; set; }
        [NotifyProperty]
        public partial double TotalGamma { get; set; }
        [NotifyProperty]
        public partial double TotalVega { get; set; }
        [NotifyProperty]
        public partial double WeightedVega { get; set; }
        [NotifyProperty]
        public partial double TotalTheta { get; set; }
        [NotifyProperty]
        public partial double TotalRho { get; set; }
        [NotifyProperty]
        public partial double TotalImplied { get; set; }
        [NotifyProperty]
        public partial double TotalTheo { get; set; }
        [NotifyProperty]
        public partial double TotalDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double SmoothedDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double VolaTheoV0 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoAdjV0 { get; set; }
        [NotifyProperty]
        public partial double VolaIv { get; set; }
        [NotifyProperty]
        public partial double AdjDaEma { get; set; }
        [NotifyProperty]
        public partial double VolaEma { get; set; }
        [NotifyProperty]
        public partial double AdjVolaEma { get; set; }
        [NotifyProperty]
        public partial double DaEma { get; set; }
        [NotifyProperty]
        public partial double VolaPriceMetricV0 { get; set; }
        [NotifyProperty]
        public partial double VolaPriceMetricV1 { get; set; }
        [NotifyProperty]
        public partial double VolaPriceMetricV2 { get; set; }
        [NotifyProperty]
        public partial double VolaPriceMetricV3 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoV1 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoAdjV1 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoV2 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoAdjV2 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoV3 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoAdjV3 { get; set; }
        [NotifyProperty]
        public partial double LockedTheo { get; set; }
        [NotifyProperty]
        public partial double LockedDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double NetPrice { get; set; }
        [NotifyProperty]
        public partial double NetContraPrice { get; set; }
        [NotifyProperty]
        public partial double EdgeToTheo { get; set; }
        [NotifyProperty]
        public partial double ContraEdgeToTheo { get; set; }
        [NotifyProperty]
        public partial double EdgeToDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double EdgeToDeltaAdjTheoV0 { get; set; }
        [NotifyProperty]
        public partial double ContraEdgeToDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double EdgeToMid { get; set; }
        [NotifyProperty]
        public partial double EdgeToMidDerived { get; set; }
        [NotifyProperty]
        public partial double ContraEdgeToMid { get; set; }
        [NotifyProperty]
        public partial double PercentBid { get; set; }
        [NotifyProperty]
        public partial double ContraPercentBid { get; set; }
        [NotifyProperty]
        public partial double PriceDiff { get; set; }
        [NotifyProperty]
        public partial double Last { get; set; }
        [NotifyProperty]
        public partial double UnderMid { get; set; }
        [NotifyProperty]
        public partial double HedgeBid { get; set; }
        [NotifyProperty]
        public partial double HedgeAsk { get; set; }
        [NotifyProperty]
        public partial double NetChange { get; set; }
        [NotifyProperty]
        public partial double PercentChange { get; set; }
        [NotifyProperty]
        public partial double LowestBid { get; set; }
        [NotifyProperty]
        public partial double HighestOffer { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastBidTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastAskTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BidTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AskTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TradeUpdateModel? LastTradeUpdate { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjLastTradeUpdate { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjContraPx { get; set; }
        [NotifyProperty]
        public partial bool UsingFirmPosition { get; set; }
        [NotifyProperty]
        public partial double BestDeltaAdjPx { get; set; }
        [NotifyProperty]
        public partial double BestDeltaAdjContraPx { get; set; }
        [NotifyProperty]
        public partial double PermAdjPx { get; set; }
        [NotifyProperty]
        public partial double PermAdjContraPx { get; set; }
        [NotifyProperty]
        public partial double AdjEdgeSummaryBid { get; set; }
        [NotifyProperty]
        public partial double AdjEdgeSummaryAsk { get; set; }
        [NotifyProperty]
        public partial double ImpliedEma { get; set; }
        [NotifyProperty]
        public partial double ImpliedChange { get; set; }
        [NotifyProperty]
        public partial double NotionalImpliedChange { get; set; }
        [NotifyProperty]
        public partial double MidIvEma { get; set; }
        [NotifyProperty]
        public partial int SpreadPosition { get; set; }
        [NotifyProperty]
        public partial int SpreadRawPosition { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool Active { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial AutoCancelMode AutoCancelMode { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsLowLatencyHangManager { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int AutoCancelIntervalMin { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int AutoCancelIntervalMax { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ResubmitAfterCancel { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool RiskCheckEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> AccountsList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsLooping { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsFreeLooking { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> RoutesList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> DmaRoutesList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> SorRoutesList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Underlying { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string HedgeUnderlying { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HedgeMultiplier { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Symbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ShowIbData { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string SpreadSymbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string ContraSpreadSymbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Description { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string SubmitText { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string ContraSubmitText { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsSellOrder { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsIbTicket { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsContraSellOrder { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string SpreadId { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string SpreadPermId { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string DualDescription { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial PutCall PutCall { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string SpreadType { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StrikeSpacing { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool CanThreeWay { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ThreeWayOtmOverride { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TraderSpreadPosition { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TraderAdjustedPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FeesEstimate { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string LastExchange { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Exchanges { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string LastContraExchange { get; set; }
        public IList<ContraCapacity> ContraCapacities { get; set; }
        public IList<ContraBrokerName> ContraBrokerNames { get; set; }
        public IList<ContraCmta> ContraCmtas { get; set; }
        public IList<ContraTrader> ContraTraders { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool TraderSpreadPositionInitialized { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LcdPosition { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TotalStocks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int HedgedStocks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int RequiredStocks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int SubmittedStocks { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool CanHedge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double VolumeAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ChangeInVolume { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ManualStrikeOffset { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ManualMaxStrikeOffset { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockHedgePercent { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int StockHedgeQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string StockHedgeRoute { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string StockHedgeStatus { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial StatusMode StockHedgeStatusMode { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockHedgeAdjPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockHedgeUnrealizedPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial HedgeSuggestion HedgeSuggestion { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockPriceAtHedge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AdjustedPriceAtHedge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockHedgeAdjTradePx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PositionNetDelta { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HedgeNetDelta { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PositionNetWeightedVega { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AdjustedPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UnrealizedPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AvgCost { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double OpenPositionAveragePrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string FirmLastTrader { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastBuyEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastSellEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? HardSide { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime HardSideDesignationTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HardSideBuyGiveUp { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HardSideSellGiveUp { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? HardSideAtTrade { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime HardSideAtTradeDesignationTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HardSideAtTradeBuyGiveUp { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double HardSideAtTradeSellGiveUp { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastBuyOrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastSellOrderEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastFillBuyEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastFillSellEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastBuyAttemptEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastSellAttemptEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double GlobalMarketBuyEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyLastAttemptPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyLastAttemptUnderPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime BuyLastAttemptTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyLastFillPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyLastFillUnderPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime BuyLastFillTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyLowestAttemptedEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BuyHighestFilledEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellLastAttemptPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellLastAttemptUnderPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime SellLastAttemptTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellLastFillPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellLastFillUnderPx { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime SellLastFillTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellLowestAttemptedEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double SellHighestFilledEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double GlobalMarketSellEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? FirmLastTradeSide { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastBuyAttempt { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastBuyAttemptUnderlying { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastSellAttempt { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastSellAttemptUnderlying { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastPermBuyFillEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastPermSellFillEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastPermBuyAttemptEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastPermSellAttemptEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FirmLastTradeTimeAgo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime? FirmLastTradeTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BestBuyEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double WorstBuyEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BestSellEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double WorstSellEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StockHedgeOpenPositionAveragePrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SpreadPositionInitialized { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string StockPos { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockDeltaAdjPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockContraDeltaAdjPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockBestDeltaAdjPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool LockContraBestDeltaAdjPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FishPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double Price { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double MinPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double MaxPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeOverride { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AdjustedEdgeOverride { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeCurveAdjustment { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastMainUnderPriceAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastContraUnderPriceAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastMainTotalVolumeAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastContraTotalVolumeAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastMainUnderMidAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastContraUnderMidAtFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastMainUnderMidAtBestFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AveragePrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BestAveragePrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsHigh { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsLow { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsOpen { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsClose { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsLast { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TwsBidSize { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TwsAskSize { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool TwsBidLive { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool TwsAskLive { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TwsLastSize { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int TwsVolume { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string TwsBidExch { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string TwsAskExch { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string TwsLastExch { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsContraPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TwsVol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int LastHedgeQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastHedgePrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastOptionPnlOnFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastHedgePnlOnFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastTotalPnlOnFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastEdgeToMarketOnFill { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LiveLastTradeOptionPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LiveLastTradeHedgePnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LiveLastTradeTotalPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LiveLastTradeEdgeToMarket { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastTransactionPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastContraTransactionPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial decimal PriceIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial decimal TicketPriceIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial decimal ContraTicketPriceIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Account { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AccountLocked { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial RouteSelectionViewModel RouteSelection { get; set; }
        public virtual string BrokerOverride
        {
            get => _brokerOverride;
            set => SetValue(ref _brokerOverride, value);
        }
        [NotifyProperty(CheckEquality = false)]
        public partial RouteSelectionViewModel ContraRouteSelection { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Tag { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool RatioLocked { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool DisableDuplicateSubmissions { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsCloseEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsAccountValid { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime LastUpdateTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ShowEdgeIndicators { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AcquiredEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ProjectedEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ThreeWayAdjustedPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SuggestTradingMain { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SuggestTradingContra { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int Qty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ContraQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? Side { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial OrderStatus OrderStatus { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double Offset { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StrikeOffset { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<TronTradeModel> TronTrades { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ShowTimeAndSales { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TronTradeModel LatestTrade { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial OrderSubType? SubType { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SubmitWithDelayResetQtyEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool SubmitWithDelayEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string AskSizeCaption { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string BidSizeCaption { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial long SubmitLatency { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial long PendingNewLatency { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsGammaScalpTicket { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool GammaScalpOrderResubmitOnCancel { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial PositionEntryType ScalpPricingType { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ScalpPnlTarget { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ScalpEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ScalpCancelWithMidEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ScalpCancelWithUnderEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool ShowRiskRunPanel { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double MarketAskEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double MarketBidEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int SingleOrderTicketPosition { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int SingleOrderTicketWorkingPosition { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int BelowEdgeResubmitCounter { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjLastEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjLastEdgeNotional { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedDeltaAdjPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjChange { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double DeltaAdjChangeNotional { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side StopLossSide { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int StopLossQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StopLossTriggerPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StopLossCloseTriggerPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StopLossStartBidPercent { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double StopLossIncrement { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int StopLossInterval { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool MaintainLastEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AutoHedgeOnClose { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AutoHedgeOnCloseSizeOnly { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double MinHedgeHouseEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double Delta { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ZeroPlus.Models.Data.Enums.TimeInForce TimeInForce { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedEdge { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedTimespan { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedUnderlying { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedBuyPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedBuyPriceDeltaAdj { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedSellPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double EdgeScanFeedSellPriceDeltaAdj { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AutoHedgeOpenTicket { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool NagEnabled { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial DateTime NextNagTime { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CurrentNagInterval { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial TimeSpan NagCountdown { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsDeltaAdjusted { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LoopInitLatency { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TagUnderBid { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TagUnderAsk { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Reason { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TagEdgeToTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TagEdgeToEma { get; set; }
        #endregion

        #region Private Backing Fields
        private string _reason = string.Empty;
        private string _Underlying;
        private string _HedgeUnderlying;
        private string _Symbol;
        private string _SpreadSymbol;
        private string _ContraSpreadSymbol;
        private string _Description = "";
        private string _SubmitText;
        private string _ContraSubmitText;
        private string _SpreadId;
        private string _SpreadPermId;
        private string _DualDescription;
        private string _SpreadType;
        private string _LastExchange = "";
        private string _Exchanges = "";
        private string _LastContraExchange = "";
        private string _StockHedgeRoute;
        private string _StockHedgeStatus = "";
        private string _StockPos;
        private string _TwsBidExch = "";
        private string _TwsAskExch = "";
        private string _TwsLastExch = "";
        private string _Account;
        private bool _accountLocked = true;
        private string _brokerOverride;
        private RouteSelectionViewModel _routeSelection;
        private string _route;
        private RouteSelectionViewModel _contraRouteSelection;
        private string _contraRoute;
        private string _Tag;
        private OrderSubType? _subType;
        private string _AskSizeCaption;
        private string _BidSizeCaption;
        private long _SubmitLatency;
        private ObservableCollection<AutoCloseConfigViewModel> _autoCloseConfigModels = new();
        private AutoCloseConfigViewModel _autoCloseConfigModel;
        private DateTime _lastTheoUpdateTime;
        bool _autoHedgeOpenTicket;
        private DateTime _nextNagTime;
        private TimeSpan _nagCountdown;
        private List<TicketLegModel> _calcLegs;
        private PositionEntryType _scalpPricingType;
        private bool _isLowLatencyHangManager;
        private double _edgeGiveUp;
        private double _closeSubs;
        private double _orderEdgeToTheo;
        protected bool _resubmitWhenReceivingCancelStatus;
        private bool _usingSmartRoute = false;
        private bool _contraUsingSmartRoute = false;
        private bool _manualClose;
        private bool _lastHedgedMain;
        private bool _isDeltaAdjusted = false;
        private bool _showWidthNotification;
        private bool _netTheoSynched = false;
        private bool _deltaAdjTheoSynched = false;
        private bool _usingFirmPosition;
        private bool _autoCloseToggled;
        private bool _autoCloseArmed;
        protected bool _canAutoCancel;
        private bool _adjEdgeSummaryLoaded;
        private bool _nagEnabled;
        private bool _SingleOrderTicketStopLossEnabled;
        private bool _SingleOrderTicketStopLossUsePercentage;
        private bool _SingleOrderTicketTrailingStopEnabled;
        private bool _SingleOrderTicketTrailingStopUsePercentage;
        private bool _LoopAutoSizeup;
        private bool _LoopFreeLook;
        private bool _LoopFreeLookOnAll;
        private bool _IsSingleLegSell;
        private InstanceMode _instanceMode = OmsCore.Config.InstanceModeV3;
        private bool _TrackerToggled;
        private bool _DerivativeLoaded;
        private bool _LockLowPrice;
        private bool _LockMidPrice;
        private bool _LockHighPrice;
        private bool _LockContraLowPrice;
        private bool _LockContraMidPrice;
        private bool _LockContraHighPrice;
        private bool _Active;
        private AutoCancelMode _AutoCancelMode = AutoCancelMode.AUTO;
        private bool _ResubmitAfterCancel;
        private bool _RiskCheckEnabled = true;
        private bool _IsLooping;
        private bool _IsFreeLooking;
        private bool _ShowIbData;
        private bool _IsSellOrder;
        private bool _isIbTicket;
        private bool _IsContraSellOrder;
        private bool _CanThreeWay;
        private bool _ThreeWayOtmOverride = OmsCore.Config.ThreeWayPreference == ThreeWayPreference.OTM;
        private bool _TraderSpreadPositionInitialized;
        private bool _CanHedge = false;
        private bool _SpreadPositionInitialized;
        private bool _LockDeltaAdjPrice = false;
        private bool _LockContraDeltaAdjPrice = false;
        private bool _LockBestDeltaAdjPrice = false;
        private bool _LockContraBestDeltaAdjPrice = false;
        private bool _RatioLocked;
        private bool _DisableDuplicateSubmissions;
        private bool _IsCloseEnabled;
        private bool _IsAccountValid;
        private bool _ShowEdgeIndicators = false;
        private bool _SuggestTradingMain;
        private bool _SuggestTradingContra;
        private bool _ShowTimeAndSales;
        private bool _SubmitWithDelayResetQtyEnabled;
        private bool _SubmitWithDelayEnabled;
        private bool _IsGammaScalpTicket;
        private bool _GammaScalpOrderResubmitOnCancel;
        private bool _ShowRiskRunPanel;
        private bool _lowIntEdge;
        private bool _highIntEdge;
        private double _HedgeMultiplier;
        private double _StrikeSpacing;
        private double _TraderAdjustedPnl = double.NaN;
        private double _FeesEstimate = 0;
        private double _VolumeAtFill = double.NaN;
        private double _ChangeInVolume = double.NaN;
        private double _ManualStrikeOffset;
        private double _ManualMaxStrikeOffset;
        private double _StockHedgePercent = OmsCore.Config.MaxAutoHedgePercentV2;
        private double _StockHedgeAdjPnl = double.NaN;
        private double _StockHedgeUnrealizedPnl = double.NaN;
        private double _EstHedgeCost = double.NaN;
        private double _StockPriceAtHedge = double.NaN;
        private double _AdjustedPriceAtHedge = double.NaN;
        private double _StockHedgeAdjTradePx = double.NaN;
        private double _PositionNetDelta;
        private double _HedgeNetDelta;
        private double _PositionNetWeightedVega;
        private double _AdjustedPnl;
        private double _UnrealizedPnl;
        private double _OpenPositionAveragePrice;
        private double _FirmLastEdge;
        private double _FirmLastBuyEdge;
        private double _FirmLastSellEdge;
        private double _hardSideBuyGiveUp = double.NaN;
        private double _hardSideSellGiveUp = double.NaN;
        private double _hardSideAtTradeBuyGiveUp = double.NaN;
        private double _hardSideAtTradeSellGiveUp = double.NaN;
        private double _firmLastBuyOrderEdgeToTheo = double.NaN;
        private double _firmLastSellOrderEdgeToTheo = double.NaN;
        private double _firmLastFillBuyEdgeToTheo = double.NaN;
        private double _firmLastFillSellEdgeToTheo = double.NaN;
        private double _firmLastBuyAttemptEdgeToTheo = double.NaN;
        private double _firmLastSellAttemptEdgeToTheo = double.NaN;
        private double _globalMarketBuyEdgeToTheo = double.NaN;
        private double _globalMarketSellEdgeToTheo = double.NaN;
        private Side? _firmLastTradeSide;
        private DateTime? _firmLastTradeTime = null;
        private double _firmLastTradeTimeAgo = double.NaN;
        private double _BestBuyEdgeToTheo;
        private double _WorstBuyEdgeToTheo;
        private double _BestSellEdgeToTheo;
        private double _WorstSellEdgeToTheo;
        private double _StockHedgeOpenPositionAveragePrice;
        private double _FishPrice;
        private double _Price = double.NaN;
        private double _LastPrice;
        private double _MinPrice;
        private double _MaxPrice;
        private double _EdgeOverride = double.NaN;
        private double _AdjustedEdgeOverride = double.NaN;
        private double _EdgeCurveAdjustment = double.NaN;
        private double _LastMainUnderPriceAtFill = double.NaN;
        private double _LastContraUnderPriceAtFill = double.NaN;
        private double _LastMainTotalVolumeAtFill = double.NaN;
        private double _LastContraTotalVolumeAtFill = double.NaN;
        private double _LastMainUnderMidAtFill = double.NaN;
        private double _LastContraUnderMidAtFill = double.NaN;
        private double _LastMainUnderMidAtBestFill = double.NaN;
        private double _AveragePrice = double.NaN;
        private double _BestAveragePrice = double.NaN;
        private double _TwsHigh = double.NaN;
        private double _TwsLow = double.NaN;
        private double _TwsOpen = double.NaN;
        private double _TwsClose = double.NaN;
        private double _TwsLast = double.NaN;
        private double _TwsPrice = double.NaN;
        private double _TwsContraPrice = double.NaN;
        private double _TwsVol;
        private double _LastHedgePrice = double.NaN;
        private double _LastOptionPnlOnFill = double.NaN;
        private double _LastHedgePnlOnFill = double.NaN;
        private double _LastTotalPnlOnFill = double.NaN;
        private double _LastEdgeToMarketOnFill = double.NaN;
        private double _LiveLastTradeOptionPnl = double.NaN;
        private double _LiveLastTradeHedgePnl = double.NaN;
        private double _LiveLastTradeTotalPnl = double.NaN;
        private double _LiveLastTradeEdgeToMarket = double.NaN;
        private double _LastTransactionPrice = double.NaN;
        private double _LastContraTransactionPrice = double.NaN;
        private double _AcquiredEdge = double.NaN;
        private double _ProjectedEdge = double.NaN;
        private double _DeltaAdjEdge = double.NaN;
        private double _ThreeWayAdjustedPnl = double.NaN;
        private double _Offset;
        private double _StrikeOffset;
        private double _ScalpPnlTarget;
        private double _ScalpEdge;
        private double _ScalpCancelWithMidEdge;
        private double _ScalpCancelWithUnderEdge;
        private double _marketAskEma = double.NaN;
        private double _marketBidEma = double.NaN;
        private double _DeltaAdjLastEdge = double.NaN;
        private double _DeltaAdjLastEdgeNotional = double.NaN;
        private double _EdgeScanFeedDeltaAdjPrice = double.NaN;
        private double _DeltaAdjChange = double.NaN;
        private double _DeltaAdjChangeNotional = double.NaN;
        private bool _ContraQtyLocked = true;
        private bool _StopLossEnabled = false;
        private Side _StopLossSide;
        private int _StopLossQty = 1;
        private double _StopLossTriggerPrice = double.NaN;
        private double _StopLossCloseTriggerPrice = double.NaN;
        private double _StopLossStartBidPercent = .5;
        private double _StopLossIncrement = .01;
        private int _StopLossInterval = 100;
        private bool _MaintainLastEdge;
        private bool _AutoHedgeOnClose;
        private bool _AutoHedgeOnCloseSizeOnly;
        private double _MinHedgeHouseEdge;
        private double _EdgeScanFeedEdge = double.NaN;
        private double _EdgeScanFeedTimespan = double.NaN;
        private double _EdgeScanFeedUnderlying = double.NaN;
        private double _EdgeScanFeedBuyPrice = double.NaN;
        private double _EdgeScanFeedBuyPriceDeltaAdj = double.NaN;
        private double _EdgeScanFeedSellPrice = double.NaN;
        private double _EdgeScanFeedSellPriceDeltaAdj = double.NaN;
        protected double _theoToWatchFor = double.NaN;
        protected double _adjTheoToWatchFor = double.NaN;
        protected double _midToWatchFor = double.NaN;
        protected double _underToWatchFor = double.NaN;
        protected double _lastDeltaToWatchFor = double.NaN;
        private double _increment;
        private double _fishInterval;
        private double _bestEdgeBid = double.NaN;
        private double _bestEdgeMid = double.NaN;
        private double _bestEdgeAsk = double.NaN;
        private double _low = double.NaN;
        private double _high = double.NaN;
        private double _bestLow = double.NaN;
        private double _bestHigh = double.NaN;
        private double _mid = double.NaN;
        private double _lowInt = double.NaN;
        private double _highInt = double.NaN;
        private double _midInt = double.NaN;
        private double _mktMkrBid = double.NaN;
        private double _mktMkrAsk = double.NaN;
        private double _highestBid = double.NaN;
        private double _lowestAsk = double.NaN;
        private double _bestBidInt = double.NaN;
        private double _bestAskInt = double.NaN;
        private double _bestMidInt = double.NaN;
        private double _lowDerived = double.NaN;
        private double _highDerived = double.NaN;
        private double _midDerived = double.NaN;
        private double _lowIntDerived = double.NaN;
        private double _highIntDerived = double.NaN;
        private double _midIntDerived = double.NaN;
        private double _adjEma = double.NaN;
        private double _loopInitLatency = double.NaN;
        private double _tagUnderBid = double.NaN;
        private double _tagUnderAsk = double.NaN;
        private double _askEmaAdj;
        private double _askEma;
        private double _bidEmaAdj;
        private double _bidEma;
        private double _fullEma = double.NaN;
        private double _underEma = double.NaN;
        private double _ema = double.NaN;
        private double _bidIvEma = double.NaN;
        private double _askIvEma = double.NaN;
        private double _width = double.NaN;
        private double _netTheo = double.NaN;
        private double _netDeltaAdjTheo = double.NaN;
        private double _theoBid = double.NaN;
        private double _theoAsk = double.NaN;
        private double _digBid = double.NaN;
        private double _digAsk = double.NaN;
        private uint _digBidSize;
        private uint _digAskSize;
        private double _adjTheoToMid = double.NaN;
        private double _netTestValue = double.NaN;
        private double _theoDiff = double.NaN;
        private double _highestTheoChange = double.NaN;
        private double _netDelta = double.NaN;
        private double _netGamma = double.NaN;
        private double _netTheta = double.NaN;
        private double _totalDelta = double.NaN;
        private double _totalDeltaDirection = double.NaN;
        private double _totalVolume = double.NaN;
        private double _firmTotalVolume = double.NaN;
        private double _openInterest = double.NaN;
        private double _totalGamma = double.NaN;
        private double _totalVega = double.NaN;
        private double _weightedVega = double.NaN;
        private double _totalTheta = double.NaN;
        private double _totalRho = double.NaN;
        private double _totalImplied = double.NaN;
        private double _totalTheo = double.NaN;
        private double _totalDeltaAdjTheo = double.NaN;
        private double _smoothedDeltaAdjTheo = double.NaN;
        private double _volaTheoV0 = double.NaN;
        private double _volaTheoAdjV0 = double.NaN;
        private double _volaIv = double.NaN;
        private double _adjDaEma = double.NaN;
        private double _volaEma = double.NaN;
        private double _adjVolaEma = double.NaN;
        private double _daEma = double.NaN;
        private double _volaTheoV1 = double.NaN;
        private double _volaPriceMetricV0 = double.NaN;
        private double _volaPriceMetricV1 = double.NaN;
        private double _volaPriceMetricV2 = double.NaN;
        private double _volaPriceMetricV3 = double.NaN;
        private double _volaTheoAdjV1 = double.NaN;
        private double _volaTheoV2 = double.NaN;
        private double _volaTheoAdjV2 = double.NaN;
        private double _volaTheoV3 = double.NaN;
        private double _volaTheoAdjV3 = double.NaN;
        private double _lockedTheo = double.NaN;
        private double _lockedDeltaAdjTheo = double.NaN;
        private double _netPrice = double.NaN;
        private double _netContraPrice = double.NaN;
        private double _edgeToTheo = double.NaN;
        private double _contraEdgeToTheo = double.NaN;
        private double _edgeToDeltaAdjTheo = double.NaN;
        private double _edgeToDeltaAdjTheoV0 = double.NaN;
        private double _contraEdgeToDeltaAdjTheo = double.NaN;
        private double _edgeToMid = double.NaN;
        private double _edgeToMidDerived = double.NaN;
        private double _contraEdgeToMid = double.NaN;
        private double _percentBid = double.NaN;
        private double _contraPercentBid = double.NaN;
        private double _priceDiff = double.NaN;
        private double _midIvEma = double.NaN;
        private double _notionalImpliedChange = double.NaN;
        private double _impliedChange = double.NaN;
        private double _impliedEma = double.NaN;
        private double _bestDeltaAdjContraPx = double.NaN;
        private double _bestDeltaAdjPx = double.NaN;
        private double _deltaAdjContraPx = double.NaN;
        private double _deltaAdjPx = double.NaN;
        private double _permAdjPx;
        private double _permAdjContraPx;
        private double _adjEdgeSummaryBid;
        private double _adjEdgeSummaryAsk;
        private double _lowestBid = double.NaN;
        private double _highestOffer = double.NaN;
        private double _percentChange;
        private double _netChange = double.NaN;
        private double _hedgeAsk = double.NaN;
        private double _hedgeBid = double.NaN;
        private double _underMid = double.NaN;
        private double _last = double.NaN;
        private double _adjEdgeSummaryBidBase;
        private double _adjEdgeSummaryAskBase;
        private double _adjEdgeSummaryUnderMidAtLoad;
        private double _bidAtFillForSingleTickets = 0;
        private double _askAtFillForSingleTickets = 0;
        private double _attemptedEdge;
        private double _currentNagInterval;
        private double _tagEdgeToTheo = double.NaN;
        private double _tagEdgeToEma = double.NaN;
        private double _notionalLastEdge = double.NaN;
        private double _lastBuyEdge = double.NaN;
        private double _lastSellEdge = double.NaN;
        private double _bestEdge = double.NaN;
        private double _lastOrderEdgeToTheo = double.NaN;
        private double _lastContraOrderEdgeToTheo = double.NaN;
        private double _lastFilledOrderEdgeToTheo = double.NaN;
        private double _lastFilledClosingOrderEdgeToTheo = double.NaN;
        private double _DeltaAdjLastTradeUpdate = double.NaN;
        private double _SingleOrderTicketStopLossValue = 0;
        private double _SingleOrderTicketStopLoss;
        private double _SingleOrderTicketStopLossPercentage;
        private double _SingleOrderTicketTrailingStopValue = 0;
        private double _SingleOrderTicketTrailingStop;
        private double _SingleOrderTicketTrailingStopPercentage;
        private double _ContraFishEdge;
        private double _CloseEdgeOveride = double.NaN;
        private double _LoopMinEdge;
        private double _LastEdgeTightenPercent;
        private double _LoopMaxLoss;
        private double _ContraFishPriceIncrement;
        private double _AutomationRequiredPartialFillPercentage;
        private double _FreeLookOnAllIncrement;
        private double _avgCost;
        private bool _theoJumpDetected = false;
        private int _spreadRawPosition;
        private bool _FreeLookWhenGettingCloseEdge;
        private bool _freeLookRequireMinFillTime;
        private double _freeLookMinFillTime;
        private int _smartRouteIndex = -1;
        private int _smartRouteFilledQty = 0;
        private int _contraSmartRouteIndex = -1;
        private int _contraSmartRouteFilledQty = 0;
        private int _lcd;
        private int _bidSize;
        private int _askSize;
        private bool _isCheapo;
        protected int _spreadPosition;
        private int _LoopInterval;
        private int _LoopIntervalMax;
        private int _SizeOveride = 0;
        private int _ResubmitCount;
        private int _TotalEstimatedResubmit;
        private int _TotalResubmitCount;
        private int _ResubmitAfterLastLoopCount;
        private int _SpeedTraderMaxLoopCount;
        private int _LoopResubmit;
        private int _ContraFishInterval;
        private int _AutomationPartialResubmitCount;
        private int _LoopSizeupQty;
        private int _LoopCountBeforeSizeup;
        private int _AutoCancelIntervalMin;
        private int _AutoCancelIntervalMax;
        private int _TraderSpreadPosition = 0;
        private int _LcdPosition;
        private int _TotalStocks = 0;
        private int _HedgedStocks = 0;
        private int _RequiredStocks = 0;
        private int _SubmittedStocks = 0;
        private int _StockHedgeQty = 0;
        private int _TwsBidSize;
        private int _TwsAskSize;
        private bool _TwsBidLive;
        private bool _TwsAskLive;
        private int _TwsLastSize;
        private int _TwsVolume;
        private int _LastHedgeQty = 0;
        private int _Qty;
        private int _ContraQty;
        private int _SingleOrderTicketPosition = 0;
        private int _SingleOrderTicketWorkingPosition = 0;
        private int _BelowEdgeResubmitCounter = 0;
        private long _PendingNewLatency;
        private double _firmLastBuyAttempt;
        private double _firmLastBuyAttemptUnderlying;
        private double _firmLastSellAttempt;
        private double _firmLastSellAttemptUnderlying;
        private double _lastPermBuyFillEdgeToTheo;
        private double _lastPermSellFillEdgeToTheo;
        private double _lastPermBuyAttemptEdgeToTheo;
        private double _lastPermSellAttemptEdgeToTheo;
        private double _delta;
        private ObservableCollection<TronTradeModel> _TronTrades = [];
        private TronTradeModel _LatestTrade;
        private bool _loopFreeLookOnAllUsingTicks;
        private LoopPricingMode _LoopPricingMode;
        private bool? _LoopFreeLookMode;
        private double _freeLookOnAllIncrementTicks;
        private double _buyLastAttemptPx;
        private double _buyLastAttemptUnderPx;
        private DateTime _buyLastAttemptTime;
        private double _buyLastFillPx;
        private double _buyLastFillUnderPx;
        private DateTime _buyLastFillTime;
        private double _buyLowestAttemptedEdgeToTheo;
        private double _buyHighestFilledEdgeToTheo;
        private double _sellLastAttemptPx;
        private double _sellLastAttemptUnderPx;
        private DateTime _sellLastAttemptTime;
        private double _sellLastFillPx;
        private double _sellLastFillUnderPx;
        private DateTime _sellLastFillTime;
        private double _sellLowestAttemptedEdgeToTheo;
        private double _sellHighestFilledEdgeToTheo;
        private int _totalLoserFreeLook;
        private OrderStatus _OrderStatus;
        private string _firmLastTrader;
        private int _contraFishIntervalMax;
        private double _lastBidTheoSpread = double.NaN;
        private double _lastAskTheoSpread = double.NaN;
        private double _bidTheoSpreadEma = double.NaN;
        private double _askTheoSpreadEma = double.NaN;
        #endregion

        public virtual bool SubscribedToWeightedVega => true;
        public virtual bool SubscribedToNetTheo => true;
        public virtual bool SubscribedToNetHistoricBest => true;
        public virtual bool SubscribedToNetAdjTheo => true;
        public virtual bool SubscribedToLow => true;
        public virtual bool SubscribedToLowestBid => true;
        public virtual bool SubscribedToHighestOffer => true;
        public virtual bool SubscribedToHighestBidLowestAsk => true;
        public virtual bool SubscribedToHigh => true;
        public virtual bool SubscribedToSize => true;
        public virtual bool SubscribedToIbQuote => true;
        public virtual bool SubscribedToMark => true;
        public virtual bool SubscribedToBestMark => true;
        public virtual bool SubscribedToEma => true;
        public virtual bool SubscribedToAdjEma => true;
        public virtual bool SubscribedToBidEma => true;
        public virtual bool SubscribedToAskEma => true;
        public virtual bool SubscribedToDig => true;
        public virtual bool SubscribedToUnder => true;
        public virtual bool SubscribedToHedgeUnder => true;
        public virtual string DestinationUid { get; } = string.Empty;
        public virtual uint DestinationSequence { get; set; } = 0;

        #region Single Order Ticket
        #endregion

        #region Speed Trader
        private SpeedTraderClosingType _SpeedTraderClosingType;
        #endregion

        public static readonly HashSet<string> SingleLegOnlyRoutes = new HashSet<string>
        {
            "EXCH_ROLL_S",
            "EXCH_ROLL_SR",
            "TEXCH_ROLL_S",
            "TEXCH_ROLL_SR",
        };

        protected static readonly HashSet<string> _algoRoutes = new HashSet<string>()
        {
            "EXCH_ROLL",
            "EXCH_ROLL_S",
            "EXCH_ROLL_SR",
            "ZPROLL",
            "ZPSROLL",
        };

        protected static readonly HashSet<string> _nonAlgoUnders = new HashSet<string>()
        {
            "$SPX",
            "$NDX",
            "$VIX",
            "$RUT",
            "$XSP",
        };

        protected virtual bool ConformBuySide { get; }

        protected bool Closing;
        protected bool OrderIsClosed = true;
        protected double UnderlyingClosing;
        protected bool UnderlyingClosingInitialized = false;
        public string HedgeOrderId;
        protected HashSet<string> OrderIdsSet = [];
        protected HashSet<string> ContraOrderIdsSet = [];
        protected HashSet<string> HedgeOrderIdsSet = [];

        internal readonly object PositionUpdateLock = new();
        internal int PrevQty = 0;
        internal bool ResetSize;
        internal double LastPx = double.NaN;
        internal string LastRoute = string.Empty;
        internal double LastContraPx = double.NaN;
        internal int LastQty;
        internal int LastContraQty;
        internal bool ReversePrompted;
        internal int HedgeAttempt = 0;

        public string OrderId { get; set; }
        public string LocalId { get; set; }
        public string ContraOrderId { get; set; }
        public string ContraPermId { get; set; }
        public string ContraLocalId { get; set; }
        public Tracker Tracker { get; }
        public PermCloser PermCloser { get; }
        public Looper Looper { get; }
        public ICloser Closer { get; }
        public CxlReplaceCloser CxlReplaceCloser { get; }
        public SweepCloser SweepCloser { get; }
        public LegOutCloser LegOutCloser { get; }
        public AutoLegCloser AutoLegCloser { get; }
        public StopLossManager StopLossManager { get; }
        public StopOrderManager StopOrderManager { get; }
        public AutoCloseManager AutoCloseManager { get; }
        public IFisher Fisher { get; }
        public ThreeWayCloser ThreeWayCloser { get; private set; }

        internal double HedgeMid => (HedgeBid + HedgeAsk) / 2;
        internal bool OrderClosedEventIsSet { get; set; } = false;
        internal ManualResetEventSlim OrderClosedEvent { get; set; } = new ManualResetEventSlim(false);
        public bool IsBasketOrder { get; set; }

        public bool SingleLegStockRoundingDisabled => IsSingleLeg &&
                                                      IsBasketOrder &&
                                                      BasketTraderViewModel.IsEdgeScanFeedAutoTrader &&
                                                      BasketSettings.DisablePriceRounding &&
                                                      IsNotNickelOrDimePrice(_referenceTradeOriginalPrice) &&
                                                      !Underlying.StartsWith("$");

        #region Logging
        public double LogPermAdjPxUnderlyingMid { get; set; } = double.NaN;
        public double LogPermAdjPxDelta { get; set; } = double.NaN;
        public double LogPermAdjPxContraDelta { get; set; } = double.NaN;
        public double LogUnderlyingMidAtPermLoad { get; set; } = double.NaN;
        public double LogPermAdjPxBase { get; set; } = double.NaN;
        public double LogPermAdjContraPxBase { get; set; } = double.NaN;
        public double LogPermAdjPrice { get; set; } = double.NaN;
        public double LogPermAdjContraPrice { get; set; } = double.NaN;
        public double LogPermAdjPxMatchingHw { get; set; } = double.NaN;
        public double LogPermAdjPxBaseEdge { get; set; } = double.NaN;
        public double LogPermAdjPxOrig { get; set; } = double.NaN;
        public double LogPermAdjContraPxOrig { get; set; } = double.NaN;
        public double LogPermAdjDeltaAdjPxOrig { get; set; } = double.NaN;
        public double LogPermAdjDeltaAdjContraPxOrig { get; set; } = double.NaN;
        public string PermDetailsLog { get; set; } = "";
        #endregion

        public EdgeProjectorModel EdgeProjector { get; set; }
        public OrderTicketStyle TicketStyle { get; set; } = OrderTicketStyle.Complex;
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        protected virtual bool CanSubscribeToUnderlying { get; }
        protected virtual bool CanSubscribeToHedge { get; }
        protected virtual bool CanSubscribeToGlobalEdgeToTheo { get; }
        protected virtual bool CanSubscribeToFirmSummary { get; }

        internal bool IsStockTied { get; set; }
        internal TicketLegModel StockLeg { get; set; }

        public bool IsDisposed { get; set; }
        public bool isDisposing = false;
        public double Multiplier { get; set; }
        public int LastFillQty { get; set; } = 0;
        public double LastFillPx { get; set; } = double.NaN;
        public double LastContraFillPx { get; set; } = double.NaN;
        public double LastFillUnderBidPx { get; set; } = double.NaN;
        public double LastFillUnderPx { get; set; } = double.NaN;
        public double LastFillUnderAskPx { get; set; } = double.NaN;
        public double LastFillAdjTheo { get; set; } = double.NaN;
        public double LastContraFillAdjTheo { get; set; } = double.NaN;
        public string RouteOverride { get; set; }
        public bool ResubmitWithRegularRoute { get; set; }
        public string LastLoopRoute { get; set; }
        public string LastLoopContraRoute { get; set; }
        public double LastEdge { get; set; } = double.NaN;
        public double AttemptedEdge { get => _attemptedEdge; set => _attemptedEdge = Math.Round(value, 2); }
        public int StopLossAttemptCounter { get; set; }
        public int LoopIterationCounter { get; set; }
        public int LoopIterationCounterAfterSizeup { get; set; }
        public bool MainNotFilled { get; set; }
        public int ResubmitCounter { get; set; }
        public bool MainResting { get; set; }
        public bool ContraResting { get; set; }
        public bool CanReplace { get; set; }
        public bool CanReplaceContra { get; set; }
        public bool ContraNotFilled { get; set; }
        public OmsCore OmsCore { get; }
        public Dispatcher Dispatcher { get; set; }
        public string ModuleTypeSuffix { get; internal set; } = "";
        public IEnumerable<LoopPricingMode> LoopPricingModes { get; } = Enum.GetValues<LoopPricingMode>();
        public IEnumerable<AutoCancelMode> AutoCancelModes { get; set; } = Enum.GetValues<AutoCancelMode>();
        public IEnumerable<SpeedTraderClosingType> SpeedTraderClosingTypes { get; set; } = Enum.GetValues<SpeedTraderClosingType>();
        public IEnumerable<Side> Sides { get; set; } = Enum.GetValues<Side>();
        public List<TimeInForce> TifsList => Enum.GetValues<TimeInForce>().ToList();
        protected TimeSpan RiskTimeSpan { get; set; } = TimeSpan.FromMilliseconds(500);

        public ObservableCollection<RiskProfileModel> PositionsRisk { get; set; } = [];
        public bool WeightedVegaLoaded { get; private set; }
        public bool NetTheoLoaded { get; private set; }
        public bool NetHistoricBestLoaded { get; private set; }
        public bool NetAdjTheoLoaded { get; private set; }
        public bool TotalDeltaLoaded { get; private set; }
        public bool LowLoaded { get; private set; }
        public bool LowestBidLoaded { get; private set; }
        public bool HighestOfferLoaded { get; private set; }
        public bool HighestBidLowestAskLoaded { get; private set; }
        public bool HighLoaded { get; private set; }
        public bool MarkLoaded { get; private set; }
        public bool IbQuoteLoaded { get; private set; }
        public bool SizeLoaded { get; private set; }
        public bool BestMarkLoaded { get; private set; }
        public bool EmaLoaded { get; private set; }
        public bool AdjEmaLoaded { get; private set; }
        public bool BidEmaLoaded { get; private set; }
        public bool AskEmaLoaded { get; private set; }
        public bool DigLoaded { get; private set; }
        public bool UnderLoaded { get; private set; }
        public bool HedgeUnderLoaded { get; private set; }
        public double CostOfHedging { get; set; }
        public double VolaTheo { get; set; } = double.NaN;
        public double VolaTheoAdj { get; set; } = double.NaN;
        public ManualResetEventSlim HedgeClosedEvent { get; set; } = new ManualResetEventSlim(true);
        public BasketTraderViewModel BasketTraderViewModel { get; set; }
        public OrderType OrderType { get; set; }
        public bool IsGTH
        {
            get => _isGth;
            set => SetValue(ref _isGth, value);
        }
        public bool IsSingleLeg => Legs.Count == 1;
        public ObservableCollection<TicketLegModel> Legs { get; set; } = [];
        public BasketSettings BasketSettings
        {
            get => _basketSettings;
            set
            {
                _basketSettings = value;
                IsBasketOrder = _basketSettings != null;
            }
        }
        public virtual InstanceMode InstanceMode
        {
            get => _instanceMode;
            set => SetValue(ref _instanceMode, value);
        }

        private TicketLegModel _Leg1;

        private TicketLegModel _Leg2;

        private TicketLegModel _Leg3;

        private TicketLegModel _Leg4;
        public bool IsStockTicket => IsSingleLeg && Legs[0].Type == Types.STOCK.ToString();

        private TradeUpdateModel? _LastTradeUpdate = null;
        public bool MarketMakerEnabled { get; } = false;
        public int AttemptResubmit { get; } = 0;
        public bool LoopingEnabled => SpeedTraderClosingType == SpeedTraderClosingType.Loop;
        private void UpdateTicketClosingMode()
        {
            switch (SpeedTraderClosingType)
            {
                case SpeedTraderClosingType.Off:
                    LoopCommandEvent?.Invoke(start: false);
                    break;
                case SpeedTraderClosingType.Close:
                    LoopCommandEvent?.Invoke(start: false);
                    break;
                case SpeedTraderClosingType.Loop:
                    LoopCommandEvent?.Invoke(start: true);
                    break;
            }
        }
        private void SetFreeLookMode()
        {
            if (LoopFreeLookMode == null)
            {
                LoopFreeLook = false;
                LoopFreeLookOnAll = false;
            }
            else if (LoopFreeLookMode.Value)
            {
                LoopFreeLook = false;
                LoopFreeLookOnAll = true;
            }
            else
            {
                LoopFreeLook = true;
                LoopFreeLookOnAll = false;
            }
        }
        internal AutomationConfigModel GetAutomationConfig()
        {
            return BasketTraderViewModel?.GetAutomationConfig(Underlying, (double)PriceIncrement);
        }
        public bool ContraQtyLocked
        {
            get => _ContraQtyLocked;
            set
            {
                SetValue(ref _ContraQtyLocked, value);
                if (value)
                {
                    ContraQty = Lcd;
                }
            }
        }

        private ObservableCollection<string> _AccountsList;
        public bool AutomationRunning { get; set; }
        internal bool IsActive => IsLooping || IsFreeLooking || AutomationRunning;
        private ObservableCollection<string> _RoutesList;
        private ObservableCollection<string> _DmaRoutesList;
        private ObservableCollection<string> _SorRoutesList;

        private PutCall _PutCall;


        private StatusMode _StockHedgeStatusMode = StatusMode.Reset;
        public double EstHedgeCost
        {
            get => _EstHedgeCost;
            set
            {
                if (_EstHedgeCost != value)
                {
                    SetValue(ref _EstHedgeCost, value);
                }
            }
        }

        private HedgeSuggestion _HedgeSuggestion = HedgeSuggestion.None;
        private Side? _HardSide;

        private DateTime _HardSideDesignationTime;

        private Side? _HardSideAtTrade;

        private DateTime _HardSideAtTradeDesignationTime;

        private decimal _PriceIncrement;

        private decimal _TicketPriceIncrement;

        private decimal _ContraTicketPriceIncrement;
        public string SmartRoute { get; set; }
        public string LocalID { get; set; }

        [Bindable]
        public partial string Route { get; set; }

        [Bindable]
        public partial string ContraRoute { get; set; }
        [Bindable]
        public partial bool IsSubmitEnabled { get; set; }
        [Bindable]
        public partial bool IsModifyEnabled { get; set; }
        [Bindable]
        public partial bool IsCancelEnabled { get; set; }
        [Bindable]
        public partial string Filled { get; set; }
        [Bindable]
        public partial int LastQuantity { get; set; }
        [Bindable]
        public partial int ContraLastQuantity { get; set; }
        [Bindable]
        public partial string Status { get; set; }

        [Bindable]
        public partial OrderStatus? MainOrderStatus { get; set; }

        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ContraAveragePrice { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double ContraPrice { get; set; }
        [Bindable]
        public partial string ContraFilled { get; set; }
        [Bindable]
        public partial string ContraStatus { get; set; }

        [Bindable]
        public partial OrderStatus? ContraOrderStatus { get; set; }

        [Bindable]
        public partial StatusMode ContraStatusMode { get; set; }
        [Bindable]
        public partial bool IsContraSubmitEnabled { get; set; }
        [Bindable]
        public partial bool IsContraModifyEnabled { get; set; }
        [Bindable]
        public partial bool IsContraCancelEnabled { get; set; }

        private DateTime _LastUpdateTime;

        private Side? _Side;
        private bool _isGth;
        private TimeInForce _timeInForce;


        [Bindable]
        public partial bool ShowAutoHedge { get; set; }
        [Bindable]
        public partial bool ShowOrderInstructions { get; set; }
        public bool StopLossEnabled
        {
            get => _StopLossEnabled;
            set
            {
                if (value &&
                    ((High < StopLossTriggerPrice && StopLossSide == ZeroPlus.Models.Data.Enums.Side.Sell) ||
                     (Low > StopLossTriggerPrice && StopLossSide == ZeroPlus.Models.Data.Enums.Side.Buy)))
                {
                    var proceed = GetVerification("Stop order you set will trigger immediately.\nAre you sure you want to proceed?", SpreadId);
                    if (!proceed)
                    {
                        return;
                    }
                }
                SetValue(ref _StopLossEnabled, value);
            }
        }
        public bool ScratchOnLowDeltaSize { get; set; }
        public double ScratchOnLowDeltaMax { get; set; }
        public double ScratchOnLowDeltaMaxLoss { get; set; }
        public int ScratchOnLowDeltaMinSize { get; set; } = 1;
        public bool ThreeWayStarted { get; protected set; }
        public bool ThreeWayComplete { get; protected set; }
        public bool LastTradedContra { get; protected set; }
        public double Bid { get => Low; set => Low = value; }
        public double Ask { get => High; set => High = value; }
        public double TotalCommissions { get; }
        public bool WasPartiallyFilled { get; set; }
        public bool PartiallyFilled { get; set; }
        public bool ContraPartiallyFilled { get; set; }
        public int CumulativeQty { get; set; }
        public int LeavesQty { get; set; }
        public int ContraLeavesQty { get; set; }
        public int ContraCumulativeQty { get; set; }
        public bool IsComplexOrder => !IsSingleLeg;
        public int FilledQty { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public int TotalFills { get; set; }
        public int Quantity { get; set; }
        public int TransactionID { get; set; }
        public int AccountID { get; set; }
        public double SpreadAvgPrice { get; set; }
        public double TagEdge { get; set; }
        public double TagMid { get; set; }
        public double TagBid { get; set; }
        public double TagAsk { get; set; }
        public double TagTheo { get; set; }
        public double TagVolaV0 { get; set; } = double.NaN;
        public double TagVolaV1 { get; set; } = double.NaN;
        public double TagVolaV2 { get; set; } = double.NaN;
        public double TagEdgeToVolaV0 { get; set; } = double.NaN;
        public double TagEdgeToVolaV1 { get; set; } = double.NaN;
        public double TagEdgeToVolaV2 { get; set; } = double.NaN;
        public double TagEma { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; }
        public SubType SubTypeId { get; set; }
        public ushort SubTypeSequence { get; set; }
        public double Fee1 { get; set; }
        public double Fee2 { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
        public double TV { get; set; }
        public long MsgSequence { get; set; }
        public bool SkipNewPriceEvaluation { get; set; }
        public double ExchangeFee1 { get; set; }
        public double ExchangeFee2 { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public double TotalContracts { get; set; }
        public double FillTime { get; set; }
        public double TradeToNewTime { get; set; }
        public double SubmitToNewTime { get; set; }
        public double NewToCancelTime { get; set; }
        public double BidPercentOfFillPrice { get; set; }
        public double CloseBidPercentOfFillPrice { get; set; }
        public double OmsBidPercentOfFillPrice { get; set; }
        public double OmsBestBidPercent { get; set; }
        public double HanweckTotalTheo { get; set; }
        public double HanweckTotalGamma { get; set; }
        public double HanweckTotalVega { get; set; }
        public double HanweckTotalTheta { get; set; }
        public double HanweckTotalRho { get; set; }
        public double HanweckTotalIV { get; set; }
        public double HanweckTotalUnder { get; set; }
        public double HanweckTotalUBid { get; set; }
        public double HanweckTotalUAsk { get; set; }
        public double HanweckTotalBid { get; set; }
        public double HanweckTotalAsk { get; set; }
        public double TimeValue { get; set; }
        public double IntrinsicValue { get; set; }
        public double FVDivs { get; set; }
        public double UFwd { get; set; }
        public double UFwdFactor { get; set; }
        public double BorrowCost { get; set; }
        public double BorrowRate { get; set; }
        public double UPrice { get; set; }
        public double UTheo { get; set; }
        public double CloseTV { get; set; }
        public double CloseDelta { get; set; }
        public double CloseTotalDelta { get; set; }
        public double CloseHanweckTotalTheo { get; set; }
        public double CloseHanweckTotalGamma { get; set; }
        public double CloseHanweckTotalVega { get; set; }
        public double CloseHanweckTotalTheta { get; set; }
        public double CloseHanweckTotalRho { get; set; }
        public double CloseHanweckTotalIV { get; set; }
        public double DeltaAdjustedTheo { get; set; }
        public double CloseDeltaAdjustedTheo { get; set; }
        public int CloseBidSize { get; set; }
        public int CloseAskSize { get; set; }
        public int UnderlyingBidSize { get; set; }
        public int UnderlyingAskSize { get; set; }
        public int CloseUnderlyingBidSize { get; set; }
        public int CloseUnderlyingAskSize { get; set; }
        public double CloseBid { get; set; }
        public double CloseAsk { get; set; }
        public double CloseUnderBid { get; set; }
        public double CloseUnderAsk { get; set; }
        public double CloseHanweckTotalUnder { get; set; }
        public double CloseHanweckTotalUBid { get; set; }
        public double CloseHanweckTotalUAsk { get; set; }
        public double CloseHanweckTotalBid { get; set; }
        public double CloseHanweckTotalAsk { get; set; }
        public string Guid { get; set; }
        public string Username { get; set; }
        public string AccountAcronym { get; set; }
        public string UnderlyingSymbol { get; set; }
        public string Currency { get; set; }
        public string Source { get; set; }
        public string Trader { get; set; }
        public string Type { get; set; }
        public string Comment { get; set; }
        public string FullTag { get; set; }
        public string ExchangeOrderID { get; set; }
        public string ExecutingBroker { get; set; }
        public string ExecutionID { get; set; }
        public string ExecutionReferenceID { get; set; }
        public string PermID { get; set; }
        public string OrderID { get; set; }
        public string OriginalOrderID { get; set; }
        public string RequestAccountAcronym { get; set; }
        public string RequestSymbol { get; set; }
        public string Destination { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public string RoutingSession { get; set; }
        public string ClearingFirm { get; set; }
        public string ClearingID { get; set; }
        public DateTime SubmitTime { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime NewStatusTimeStamp { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public ZeroPlus.Models.Data.Securities.Security Security { get; set; }
        public ISecurityBook SecurityBook { get; }
        public bool IsFirstFill { get; set; }
        public bool FirstEdgeAcquired { get; set; }
        public double FirstEdge { get; set; }
        public bool IsCitadel { get; set; }
        public Side? CitadelSide { get; set; }
        public PositionEffect? SpreadPositionEffect { get; set; }
        public bool IsFill { get; }
        public double InitialEdge { get; set; } = double.NaN;
        public double OpenEdge { get; set; } = double.NaN;
        public double CloseEdge { get; set; } = double.NaN;
        private double ReversePnl { get; set; }
        private int ReverseSpreadPosition { get; set; }
        private double HedgeReversePnl { get; set; }
        private int HedgeReversePosition { get; set; }
        public bool AutoCancelRunning { get; private set; }
        public ExecutionType ExecutionType { get; set; }
        public char EdgeScanFeedConditionCode { get; set; } = '\0';
        internal CloseStyle? CloseStyle { get; set; }
        internal bool OrderSent { get; private set; }
        public Venue? Venue
        {
            get
            {
                switch (GetInstanceMode())
                {
                    case InstanceMode.OPS_SILEXX:
                        return ZeroPlus.Models.Data.Enums.Venue.Silexx;
                    case InstanceMode.OPS_TB:
                        return ZeroPlus.Models.Data.Enums.Venue.TB;
                    case InstanceMode.OPS_ZPFIX:
                        return ZeroPlus.Models.Data.Enums.Venue.ZpFix;
                    case InstanceMode.AT_TB:
                        return ZeroPlus.Models.Data.Enums.Venue.TB;
                    case InstanceMode.AT_SILEXX:
                        return ZeroPlus.Models.Data.Enums.Venue.Silexx;
                    case InstanceMode.AT_ZPFIX:
                        return ZeroPlus.Models.Data.Enums.Venue.ZpFix;
                }
                return null;
            }
            set => _ = value;
        }
        public int EdgeScanFeedBuyQty { get; set; }
        public int EdgeScanFeedSellQty { get; set; }
        public DateTime EdgeScanFeedBuyTime { get; set; }
        public DateTime EdgeScanFeedSellTime { get; set; }
        public double EdgeScanFeedRespondLatency { get; set; } = double.NaN;
        public bool AdjustClosingPriceToMarket { get; set; }
        public bool AdjustClosingPriceToMarketWinnersOnly { get; set; }
        public ZeroPlus.Models.Data.Enums.MinimumTickStyle MinimumTickStyle { get; set; } = ZeroPlus.Models.Data.Enums.MinimumTickStyle.AllPenny;
        public bool PermAdjPxLoaded { get; set; }
        public double PermAdjPxBase { get; set; }
        public double PermAdjContraPxBase { get; set; }
        public int PermGen { get; internal set; }
        public bool IsAutomation { get; set; }
        public string AutomationType { get; set; }
        public bool CanNotHedge { get; set; }
        public double TagBestBid { get; set; }
        public double TagBestAsk { get; set; }
        public double TagMktMkrBid { get; set; }
        public double TagMktMkrAsk { get; set; }
        public int TagVolume { get; set; }
        public int TagFirmVolume { get; set; }
        public ValidationHandler PreSizeCheck { get; set; }
        public double UnderMidAtPermLoad { get; set; }
        public bool AutoHedgeOnFailure { get; set; }
        public bool AutoHedgePartial { get; set; }
        public int DaysToExpiration { get; private set; }
        public int Contracts { get; private set; }
        [Bindable]
        public partial double MinStrike { get; set; }
        public string SpreadHash { get; set; }
        public OrderSource OrderSource { get; set; } = OrderSource.OMS;
        public OrderSource ContraOrderSource { get; set; } = OrderSource.OMS;
        public EdgeType EdgeType { get; set; }
        public double Edge { get; set; }
        public string EdgeFormula { get; set; }
        public bool IsTagged { get; set; }
        public string Tagger { get; set; }
        public string TaggedMessage { get; set; }
        public bool LogUpdates { get; set; }
        public int LegsCount { get; set; } = 1;
        #endregion
        HashSet<IComplexOrderLeg> IComplexOrder.Legs
        {
            get => Legs.Select(x => (IComplexOrderLeg)x).ToHashSet();
            set => _ = value;
        }
        HashSet<IComplexOrderLeg> IComplexOrderSlim.Legs
        {
            get => Legs.Select(x => (IComplexOrderLeg)x).ToHashSet();
            set => _ = value;
        }
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string RiskCheckMessage { get; set; }
        public string PrimaryExchange { get; set; }
        public StockHedgeOrderModel StockHedgeOrderModel { get; set; }
        public string EffectiveBroker => string.IsNullOrWhiteSpace(BrokerOverride) ? OmsCore.Config.DefaultBroker : BrokerOverride;

        // Single source of truth for "this ticket's outgoing orders go through AutoTrader."
        // Drives both the broker-prefix wire format (ApplyBrokerPrefix) and the route list
        // shown in the dropdown (ReloadAccountsAndRoutesList) so they can never disagree.
        public bool RoutesGoThroughAutoTrader =>
            InstanceMode.IsAutoTraderInstance() || OmsCore.Config.RouteOpsOrdersToAutoTraderDirect;

        public double CloseEdgeOverride { get; set; } = double.NaN;
        public ulong IoiId { get; set; }

        static OrderTicket()
        {
            try
            {
                DataLoadTimeout = BASE_TIMEOUT;
                EmaLoadTimeout = BASE_TIMEOUT;
                WeightedVegaLoadTimeout = BASE_TIMEOUT;
                Ping pingClass = new();
                PingReply pingReply = pingClass.Send(OmsCore.Config.AuthServer);
                _log.Info($"Ping. Server: {OmsCore.Config.AuthServer}. Round Trip: {pingReply.RoundtripTime}");
                if (pingReply.RoundtripTime > 10)
                {
                    DataLoadTimeout = BASE_TIMEOUT * 2;
                    EmaLoadTimeout = BASE_TIMEOUT * 2;
                    WeightedVegaLoadTimeout = BASE_TIMEOUT * 2;
                }
            }
            catch (Exception ex)
            {
                _log.Info(ex, $"Ping Failed. Server: {OmsCore.Config.AuthServer}");
            }
        }

        public OrderTicket(IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory,
            IAbstractFactory<ThreeWayCloser> threeWayCloserFactory,
            IAbstractFactory<RouteSelectionViewModel> routeSelectionFactory,
            TransactionConsumerModel transactionConsumer,
            NotificationManager notificationManager,
            PortfolioManagerModel portfolioManagerModel,
            OmsCore omsCore)
        {
            _ticketFactory = ticketFactory;
            _threeWayCloserFactory = threeWayCloserFactory;
            _notificationManager = notificationManager;
            _transactionConsumer = transactionConsumer;
            _portfolioManagerModel = portfolioManagerModel;

            OmsCore = omsCore;
            OmsCore.AutoTraderClient.AccountsAndRoutesLoaded += () => Task.Run(ReloadAccountsAndRoutesList);

            _smartRouteOverwatchTimer.AutoReset = false;
            _smartRouteOverwatchTimer.Interval = OmsCore.Config.SmartRouteOverwatchTimer;
            _smartRouteOverwatchTimer.Elapsed += SmartRouteOverwatchTimer_Ellapsed;

            _contraSmartRouteOverwatchTimer.AutoReset = false;
            _contraSmartRouteOverwatchTimer.Interval = OmsCore.Config.SmartRouteOverwatchTimer;
            _contraSmartRouteOverwatchTimer.Elapsed += ContraSmartRouteOverwatchTimer_Ellapsed;

            _fishTimer = new()
            {
                AutoReset = false,
            };
            _fishTimer.Elapsed += FishTimer_Ellapsed;

            _notifiers = GetGeneratedNotifiers();
            _notifiersCount = _notifiers.Length;
            RouteSelection = routeSelectionFactory.Create();
            ContraRouteSelection = routeSelectionFactory.Create();
            AccountsList =
            [
                OmsCore.Config.DefaultAccount,
            ];
            RoutesList = new List<string>()
            {
                OmsCore.Config.DefaultRouteSpxRutXsp(InstanceMode),
                OmsCore.Config.DefaultRouteNdx(InstanceMode),
                OmsCore.Config.DefaultRoute(InstanceMode),
                OmsCore.Config.DefaultSingleLegRoute(InstanceMode),
                OmsCore.Config.DefaultCurbSessionRoute(InstanceMode),
            }.Distinct().ToObservableCollection();
            DmaRoutesList = new ObservableCollection<string>();
            SorRoutesList = new ObservableCollection<string>();
            Route = string.Empty;

            for (int i = 0; i < 7; i++)
            {
                RiskProfileModel riskPosition = new();
                if (i == 3)
                {
                    riskPosition.IsCurrent = true;
                }
                PositionsRisk.Add(riskPosition);
            }
            ResetPermAdj();
            ResetAdjEdgeSummary();

            PermCloser = new(this);
            Looper = new(this);
            Tracker = new(this);
            Closer = new Closer(this);
            CxlReplaceCloser = new(this);
            Fisher = new Fisher(this);
            LegOutCloser = new(this);
            AutoLegCloser = new(this);
            SweepCloser = new(this);
            StopLossManager = new(this);
            StopOrderManager = new(this);
            AutoCloseManager = new(this);

            StockHedgeRoute = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
            StopLossSide = Sides.FirstOrDefault();
        }

        public virtual bool GetVerification(string message, string title) { return true; }
        public virtual Task<bool> GetVerificationAsync(string message, string title) { return Task.FromResult(true); }
        public virtual RiskWarningMessageResponse GetRiskVerification(string message, string title) { return RiskWarningMessageResponse.CancelAll; }
        public virtual void ShowMessage(string message, string title, bool canBeSilended = true) { }
        protected virtual void UpdateSummary() { }
        protected virtual void UnderMidUpdated(double prevValue, double newValue) { }
        protected virtual void TheoUpdated(double prevValue, double newValue) { }
        protected virtual void DeltaAdjTheoUpdated(double prevValue, double newValue) { }
        protected virtual void DeltaAdjTheoLoaded() { }
        protected virtual void MidUpdated(double prevValue, double newValue) { }
        protected virtual void MarketUpdated(double prevValue, double newValue) { }
        private void OnConfigChange(OmsConfig config, bool requiresRestart)
        {
            {
                LoadAutoCloseConfigModels();
            }
        }

        [Command]
        public void SearchTimeAndSalesCommand()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Trades))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        TradesView window = new();
                        TradesViewModel viewModel = (TradesViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        viewModel.Ready += (IModuleViewModel module) =>
                        {
                            viewModel.Symbol = Symbol;
                            viewModel.SelectedTime = "Today";
                            viewModel.LegTypes = IsSingleLeg ? LegTypes.Single : LegTypes.MLeg;

                            viewModel.Refresh();
                        };
                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchTimeAndSalesCommand));
            }
        }

        [Command]
        public void AddNewAutoCloseTierCommand()
        {
            AutoCloseTierConfigView view = new();
            if (view.DataContext is AutoCloseConfigViewModel viewModel)
            {
                viewModel.Parent = this;
            }
            view.Show();
        }

        [Command]
        public void EditAutoCloseTierCommand(AutoCloseConfigViewModel model)
        {
            AutoCloseTierConfigView view = new();
            view.DataContext = model;
            if (view.DataContext is AutoCloseConfigViewModel viewModel)
            {
                viewModel.Parent = this;
            }
            view.Show();
        }

        [Command]
        public void LoadAutoCloseTierCommand(AutoCloseConfigViewModel model)
        {
            foreach (AutoCloseConfigViewModel config in AutoCloseConfigModels)
            {
                config.Selected = false;
            }
            AutoCloseConfigModel = model;
            model.Selected = true;
        }

        [Command]
        public void SubscribeToIbCommand()
        {
            try
            {
                var key = SubscribeToIbCob();

                foreach (var leg in Legs)
                {
                    leg.SubscribeToIbData(key);
                }

                ShowIbData = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeToIbCommand));
            }
        }

        private string SubscribeToIbCob()
        {
            var key = "";
            if (Underlying.Contains('\\'))
            {
                var input = Underlying.ToUpper();
                var symbolRoute = input.Split("\\");
                if (symbolRoute.Length > 3)
                {
                    var underlyingSymbol = symbolRoute[0];
                    var secType = symbolRoute[1];
                    var exchange = symbolRoute[2];
                    var currency = symbolRoute[3];
                    Currency = currency;
                    UnderlyingSymbol = underlyingSymbol;
                    Dispatcher?.BeginInvoke(() =>
                    {
                        if (!RoutesList.Contains(exchange))
                        {
                            RoutesList.Add(exchange);
                        }
                        Route = exchange;
                    });
                    key = '\\' + secType + '\\' + exchange + '\\' + currency;
                }
            }
            Symbol = GetTosFromLegs(Legs.ToList());
            if (!string.IsNullOrWhiteSpace(Symbol) && Legs.All(x => x.IsValid))
            {
                OmsCore.UpdateManager.Subscribe(Symbol + key, SubscriptionFieldType.IbQuote, this);
            }
            return key;
        }

        [Command]
        public void UnsubscribeIbDataCommand()
        {
            try
            {
                OmsCore.UpdateManager.UnsubscribeAll(SubscriptionFieldType.IbQuote, this);

                foreach (var leg in Legs)
                {
                    OmsCore.UpdateManager.UnsubscribeAll(SubscriptionFieldType.IbQuote, leg);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeIbDataCommand));
            }
        }

        [Command]
        public void AccountOrRouteChangedCommand()
        {
            try
            {
                if (IsDisposed || isDisposing)
                {
                    return;
                }
                bool accountChanged = false;
                foreach (TicketLegModel leg in Legs)
                {
                    if (leg.Account != Account)
                    {
                        accountChanged = true;
                        leg.Account = Account;
                        leg.ResetLegValues();
                        leg.ValidateLegAsync();
                    }
                }

                if (accountChanged)
                {
                    _ = ReloadAccountsAndRoutesList();
                }

                ValidateTicket();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AccountOrRouteChangedCommand));
            }
        }

        partial void OnAccountChanged(string value)
        {
            if (IsDisposed || isDisposing)
            {
                return;
            }

            if (string.Equals(_lastReloadedAccount, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _lastReloadedAccount = value;
            _ = ReloadAccountsAndRoutesList();
        }

        [Command]
        public void AddLeg()
        {
            try
            {
                if (Legs.Count >= 12)
                {
                    return;
                }
                PreUpdate();
                TicketLegModel leg = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel);
                _ = leg.LoadExpirationsListAsync();
                leg.UpdateStrikesList();
                Legs.Add(leg);
                _ = UpdateAccountsAndRoutes();
                PostUpdate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddLeg));
            }
        }

        [Command]
        public void Clear()
        {
            try
            {
                ClearTicket();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clear));
            }
        }

        [Command]
        public void AutoCloseCommand()
        {
            try
            {
                if (ThreeWayCloser == null)
                {
                    ThreeWayCloser = _threeWayCloserFactory.Create();
                    ThreeWayCloser.Initialize(this);
                }
                ThreeWayCloser.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AutoCloseCommand));
            }
        }

        [Command]
        public async void DuplicateLeg(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is TicketLegModel legModel)
                {
                    TicketLegModel leg = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel);
                    await leg.LoadFromTemplateAsync(legModel);
                    leg.LegUpdatedEvent -= UpdateTicketValues;
                    leg.LegUpdatedEvent += UpdateTicketValues;
                    Legs.Add(leg);
                    _ = UpdateAccountsAndRoutes();
                    ResetTicket();
                    UpdateDescription();
                    QuantityChanged(leg);
                    ValidateTicket();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DuplicateLeg));
            }
        }

        [Command]
        public void ExpirationChanged(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                PreUpdate();
                if (parameter is TicketLegModel legModel)
                {
                    legModel.ExpirationChanged();

                    IEnumerable<TicketLegModel> emptyLegs = Legs.Where(x => x != legModel && x.ExpirationInfo == null);
                    foreach (TicketLegModel leg in emptyLegs)
                    {
                        leg.ExpirationInfo = leg.ExpirationsList.FirstOrDefault(x => x.Equals(legModel.ExpirationInfo));
                        ExpirationChanged(leg);
                    }
                }

                PostUpdate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ExpirationChanged));
            }
        }

        [Command]
        public async void FlipCP()
        {
            try
            {
                if (IsActive)
                {
                    return;
                }
                PreUpdate();
                List<Task> flipLegTasks = new();
                foreach (TicketLegModel leg in Legs)
                {
                    flipLegTasks.Add(leg.FlipCPAsync());
                }
                await Task.WhenAll(flipLegTasks);
                PostUpdate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FlipCP));
            }
        }

        [Command]
        public async Task OppCP()
        {
            try
            {
                if (IsActive)
                {
                    return;
                }
                await Task.Run(async () =>
                {
                    PreUpdate();
                    Dictionary<int, double> indexToSpacingMap = new();
                    for (int i = 1; i < Legs.Count; i++)
                    {
                        TicketLegModel prevLeg = Legs[i - 1];
                        TicketLegModel leg = Legs[i];
                        double spacing = leg.Strike.Strike - prevLeg.Strike.Strike;
                        indexToSpacingMap[i] = spacing;
                    }
                    for (int i = 0; i < Legs.Count; i++)
                    {
                        TicketLegModel leg = Legs[i];
                        if (indexToSpacingMap.TryGetValue(i, out double spacing))
                        {
                            TicketLegModel prevLeg = Legs[i - 1];
                            try
                            {
                                HashSet<double> strikes = new()
                                {
                                    prevLeg.Strike.Strike + spacing,
                                    prevLeg.Strike.Strike - spacing
                                };
                                await leg.OppCP(strikes);
                            }
                            catch (Exception)
                            {
                                await leg.OppCP();
                            }
                        }
                        else
                        {
                            await leg.OppCP();
                        }
                    }
                    PostUpdate();
                });
            }
            catch (SlimException ex)
            {
                ShowMessage($"{ex.Message}", "Opposite Search Failed");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OppCP));
            }
        }

        [Command]
        public void LcdChanged()
        {
            try
            {
                UpdateQty(Lcd);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LcdChanged));
            }
        }

        [Command]
        public void LockRatio()
        {
            try
            {
                RatioLocked = !RatioLocked;
                UpdateLCD();
                UpdateDescription();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LockRatio));
            }
        }

        [Command]
        public void QuantityChanged(object parameter)
        {
            try
            {
                if (parameter is not null and TicketLegModel focustedLeg)
                {
                    if (focustedLeg.SecurityType == SecurityType.Stock && RatioLocked)
                    {
                        RatioLocked = false;
                    }
                    focustedLeg.Quantity = Math.Abs(focustedLeg.Quantity);
                    if (RatioLocked)
                    {
                        foreach (TicketLegModel leg in Legs)
                        {
                            if (focustedLeg != leg && focustedLeg.Ratio != 0 && leg.Symbol != null && leg.SecurityType == SecurityType.Option)
                            {
                                leg.Quantity = focustedLeg.Quantity / focustedLeg.Ratio * leg.Ratio;
                            }
                        }
                    }
                    UpdateLCD();
                    UpdateDescription();
                    UpdateTicketValues();
                    ValidateTicket();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(QuantityChanged));
            }
        }

        [Command]
        public void RemoveLegCommand(object parameter)
        {
            try
            {
                if (IsActive)
                {
                    return;
                }
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                if (parameter is TicketLegModel legModel)
                {
                    PreUpdate();
                    RemoveLegFromOrder(legModel);
                    PostUpdate();

                    legModel.Dispose();
                }

                if (Legs.Count == 0)
                {
                    Clear();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveLegCommand));
            }
        }

        protected void RemoveLegFromOrder(TicketLegModel legModel)
        {
            if (Legs.Contains(legModel))
            {
                legModel.LegUpdatedEvent -= UpdateTicketValues;
                Legs.Remove(legModel);
                _ = UpdateAccountsAndRoutes();
            }
        }

        [Command]
        public bool Reverse()
        {
            try
            {
                if (IsActive)
                {
                    return false;
                }
                PreUpdate();
                foreach (TicketLegModel leg in Legs)
                {
                    leg.Reverse();
                }
                PostUpdate();
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Reverse));
                return false;
            }
        }

        [Command]
        public async Task SearchUnderlying()
        {
            try
            {
                ClearTicket();
                IsIbTicket = Underlying != null && Underlying.Contains('\\');
                string queryString = Underlying;
                if (string.IsNullOrWhiteSpace(queryString))
                {
                    return;
                }
                else if (queryString.Contains('.') && queryString.Length > 6)
                {
                    await LoadLegsFromTosAsync(queryString);
                    await UpdateAccountsAndRoutes();
                }
                else
                {
                    Task<List<Option>> getOptionsTask = OmsCore.QuoteClient.GetSymbolsAsync(Underlying);

                    SubscribeDataAsync();

                    List<Option> options = await getOptionsTask;
                    if (options != null && options.Count > 0)
                    {
                        AddMultipleLegs(TicketStyle is OrderTicketStyle.Single or OrderTicketStyle.Dual ? 1 : TicketStyle is OrderTicketStyle.GammaScalp ? 0 : 2);
                        await UpdateAccountsAndRoutes();
                        await SetPriceIncrementAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchUnderlying));
            }
        }

        [Command]
        public void SideChanged(TicketLegModel parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                parameter.ContraSide = parameter.Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                QuantityChanged(parameter);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SideChanged));
            }
        }

        [Command]
        public void ContraSideChanged(TicketLegModel parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                parameter.Side = parameter.ContraSide == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                QuantityChanged(parameter);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ContraSideChanged));
            }
        }

        [Command]
        public void StrikePopupOpenedCommand(RoutedEventArgs args)
        {
            try
            {
                if (args.Source is ComboBoxEdit comboBox && comboBox.EditValue == null)
                {
                    var frameworkElement = LookUpEditHelper.GetVisualClient(comboBox).InnerEditor;
                    if (frameworkElement is PopupListBox popupListBox)
                    {
                        var comboBoxItemsSource = comboBox.ItemsSource as ObservableCollection<StrikeInfoModel>;
                        var selectedStrike = comboBoxItemsSource.MinBy(x => Math.Abs(UnderMid - x.Strike));
                        popupListBox.ScrollIntoView(selectedStrike);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikePopupOpenedCommand));
            }
        }

        [Command]
        public void StrikeChanged(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                PreUpdate();
                if (parameter is TicketLegModel legModel)
                {
                    legModel.OnStrikeChange();
                }

                QuantityChanged(parameter);
                PostUpdate();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StrikeChanged));
            }
        }

        [Command]
        public async Task FlattenStockHedgeAsyncCommand()
        {
            try
            {
                await Task.Run(() =>
                {
                    if (HedgedStocks != 0)
                    {
                        HedgeWithStockAsync(-HedgedStocks);
                    }
                });
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(FlattenStockHedgeAsyncCommand));
                ShowMessage(ex.Message, "Order Hedge Failed.");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(FlattenStockHedgeAsyncCommand));
                ShowMessage(ex.Message, "Order Hedge Failed.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(FlattenStockHedgeAsyncCommand));
            }
        }

        [Command]
        public async Task SubmitStockHedgeAsyncCommand()
        {
            try
            {
                bool valid = IsValidForHedging();

                if (!valid)
                {
                    ShowMessage("Underlying Width is above risk limit.", "Hedge - " + SpreadId, canBeSilended: false);
                    return;
                }

                if (TryGetLcdPosition(out int lcdPosition))
                {
                    if (Math.Abs(lcdPosition) < Math.Abs(TraderSpreadPosition))
                    {
                        bool proceed = await GetVerificationAsync($"Invalid user position detected.\n" +
                                                       $"User Position: {TraderSpreadPosition}\n" +
                                                       $"Open Position: {lcdPosition}\n" +
                                                       $"Are you sure you want to proceed with this hedge?", "Hedge - " + SpreadId);
                        if (!proceed)
                        {
                            return;
                        }
                    }
                }

                await Task.Run(() =>
                {
                    if (RequiredStocks != 0 && StockHedgeQty != 0)
                    {
                        HedgeWithStockAsync(StockHedgeQty);
                        DeltaAdjPrice();
                        if ((_spreadPosition > 0 && Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (_spreadPosition < 0 && Side == ZeroPlus.Models.Data.Enums.Side.Sell))
                        {
                            AdjustedPriceAtHedge = DeltaAdjPx;
                        }
                        else
                        {
                            AdjustedPriceAtHedge = DeltaAdjContraPx;
                        }
                        DeltaAdjHedgePrice();
                    }
                    else
                    {
                        throw new SlimException("[Invalid] No position to hedge.");
                    }
                });
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitStockHedgeAsyncCommand));
                ShowMessage(ex.Message, "Order Hedge Failed");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitStockHedgeAsyncCommand));
                ShowMessage(ex.Message, "Order Hedge Failed");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitStockHedgeAsyncCommand));
            }
        }

        public async Task<double> SetupStockTieAsync(double totalDelta = double.NaN)
        {
            if (double.IsNaN(totalDelta))
            {
                if (await WaitForTheoLoadAsync())
                {
                    totalDelta = TotalDelta;
                }
            }

            if (!double.IsNaN(totalDelta))
            {
                if (IsSingleLegSell)
                {
                    totalDelta *= -1;
                }

                PreUpdate();
                List<TicketLegModel> stockLegs = Legs.Where(x => x.SecurityType == SecurityType.Stock).ToList();
                foreach (var leg in stockLegs)
                {
                    Legs.Remove(leg);
                    leg.Dispose();
                }

                int qty = !IsBasketOrder || BasketSettings.StockTiedDeltaNeutral ? (int)Math.Abs(totalDelta * Multiplier) : 1;
                Side side = totalDelta > 0
                    ? ZeroPlus.Models.Data.Enums.Side.Sell
                    : ZeroPlus.Models.Data.Enums.Side.Buy;

                if (qty > 0)
                {
                    TicketLegModel stockLeg = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                    {
                        Side = side,
                        Ratio = qty,
                        Quantity = qty,
                        Type = Types.STOCK.ToString(),
                        Position = Positions.AUTO.ToString(),
                        Symbol = Underlying,
                    };
                    await stockLeg.ValidateLegAsync(true);
                    Legs.Add(stockLeg);
                }

                PostUpdate();
                SetBestRoute(true);

                return qty * (side == ZeroPlus.Models.Data.Enums.Side.Sell ? -1 : 1);
            }

            return double.NaN;
        }

        public void RemoveStockTieAsync()
        {
            PreUpdate();
            var stockLegs = Legs.Where(x => x.SecurityType == SecurityType.Stock).ToList();
            foreach (var leg in stockLegs)
            {
                Legs.Remove(leg);
                leg.Dispose();
            }
            PostUpdate();
            SetBestRoute(true);
        }

        public bool IsValidForHedging()
        {
            if (!UnderLoaded)
            {
                _log.Warn(nameof(IsValidForHedging) + "Underlying Width load failed." + GetStats());
                return false;
            }
            else
            {
                double width = Math.Round(UnderAsk - UnderBid, 2);

                if (width > OmsCore.Config.MaxHedgeWidthV2)
                {
                    _log.Warn(nameof(IsValidForHedging) + "Underlying Width is above risk limit. Width: " + width + GetStats());
                    return false;
                }
            }

            return true;
        }

        public string HedgeWithStockAsync(int stockHedgeQty)
        {
            try
            {
                CanHedge = false;
                ClearHedgeIds(clearHistory: false);
                var orderInfo = BuildStockHedgeOrderAsync(stockHedgeQty);

                if (OmsCore.Config.MaxAutoHedgeNetCashEnabled)
                {
                    if (double.IsNaN(orderInfo.Price))
                    {
                        throw new SlimException("[Risk] Hedge price could not be determined.");
                    }
                    if (orderInfo.Price * orderInfo.Qty > OmsCore.Config.MaxAutoHedgeNetCash)
                    {
                        throw new SlimException("[Risk] Hedge price above risk limit.");
                    }
                }

                if (OmsCore.Config.MaxAutoHedgePositionEnabled)
                {
                    if (orderInfo.Qty > OmsCore.Config.MaxAutoHedgePosition)
                    {
                        throw new SlimException("[Risk] Hedge qty above risk limit.");
                    }
                }

                orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();

                SetHedgingInstance();
                object hedgeLock = AcquireSpreadHedgeLock();
                lock (hedgeLock)
                {
                    HedgeClosedEvent.Reset();
                    SubmittedStocks += stockHedgeQty;
                    StockPriceAtHedge = UnderMid;
                    HedgeOrderId = OmsCore.OrderClient.SendOrder(orderInfo, GetInstanceMode(), this, false, 1);
                    HedgeOrderIdsSet.Add(HedgeOrderId);
                    UpdateStockPositions();
                }

                return orderInfo.LocalID;
            }
            catch (Exception)
            {
                CanHedge = true;
                throw;
            }
        }

        private bool CheckHedgingInstance()
        {
            lock (_hedgeMasterLock)
            {
                if (_spreadIdToHedgingInstanceMap.TryGetValue(SpreadId, out OrderTicket instance))
                {
                    return instance == null || instance == this;
                }
                else
                {
                    return false;
                }
            }
        }

        public void SetHedgingInstance()
        {
            lock (_hedgeMasterLock)
            {
                _spreadIdToHedgingInstanceMap[SpreadId] = this;
            }
        }

        public void SetHedgingInstanceFreeForAll()
        {
            lock (_hedgeMasterLock)
            {
                _spreadIdToHedgingInstanceMap[SpreadId] = null;
            }
        }

        public void ReleaseHedgingInstance()
        {
            lock (_hedgeMasterLock)
            {
                if (_spreadIdToHedgingInstanceMap.TryGetValue(SpreadId, out OrderTicket ticket))
                {
                    if (ticket == this)
                    {
                        _spreadIdToHedgingInstanceMap[SpreadId] = null;
                    }
                }
            }
        }

        private object AcquireSpreadHedgeLock()
        {
            object hedgeLock = null;

            if (SpreadId != null)
            {
                lock (_hedgeMasterLock)
                {
                    if (!_spreadIdToHedgeLockMap.TryGetValue(SpreadId, out hedgeLock))
                    {
                        hedgeLock = new object();
                        _spreadIdToHedgeLockMap[SpreadId] = hedgeLock;
                    }
                }
            }

            return hedgeLock;
        }

        [Command]
        public virtual async Task<bool> SubmitAsync(string args = null)
        {
            if (TicketStyle == OrderTicketStyle.Combined)
            {
                CancelRestingOrders(isContra: false);
            }

            return await SubmitOrder(!string.IsNullOrWhiteSpace(args));
        }

        public async Task<bool> SubmitOrder(bool resting = false, bool skipAdjPxBeforeSubmit = false, int totalResubmitCount = 0, bool markForRemoval = true, bool doNotTradeThroughFillPrice = false, OrderSubType? subType = null, double restOverride = double.NaN, ReferenceTradeModel referenceTradeModel = null, bool clearDetailsContainer = true, double referenceTradeOriginalPrice = double.NaN, int payUpTicks = 0)
        {
            try
            {
                if (IsActive)
                {
                    return false;
                }

                if (clearDetailsContainer)
                {
                    ClearOrderDetails();
                }

                if (IsBasketOrder)
                {
                    bool isValidRoute = BasketTraderViewModel.IsValidRoute(underlying: Underlying, checkSingle: IsSingleLeg, checkSpread: !IsSingleLeg);
                    if (!isValidRoute)
                    {
                        throw new SlimException(message: "Invalid Open/Close Route Set!");
                    }
                }

                DateTime time = DateTime.Now;
                _latencyTimer.Restart();
                _omsOrderInitiatedNanos = EpochNanosTimer.Now();
                SubmitLatency = 0;
                PendingNewLatency = 0;
                Reason = string.Empty;

                if (TrySendToMatrix())
                {
                    OrderSource = OrderSource.AutoTrader;
                    return true;
                }

                if (InstanceMode.IsAutoTraderInstance() && IsBasketOrder && BasketTraderViewModel.BasketType == BasketType.LockTrader)
                {
                    throw new SlimException("Order Not Supported In Auto Trader!");
                }

                if (IsGammaScalpTicket)
                {
                    await UpdateGammaScalpTicketPrice();
                }

                bool done = TrySendToAutoTraderOrIb(edge: null, isContra: false, resting: resting, skipAdjPxBeforeSubmit: skipAdjPxBeforeSubmit);
                if (done)
                {
                    OrderSource = OrderSource.AutoTrader;
                    return true;
                }

                OrderSource = OrderSource.OMS;
                Venue = ZeroPlus.Models.Data.Enums.Venue.OPS;
                long deltaCheckTime = 0;
                long posCheckTime = 0;
                long setEdgeTime = 0;
                long riskCheckTime = 0;
                long pxCrossCheck = 0;
                long marketCrossCheck = 0;
                long riskCheckTime2 = 0;
                long submitLatency = 0;

                if (IsBasketOrder)
                {
                    if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(value: NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
                    {
                        if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition < 0))
                        {
                            throw new SlimException(message: "Basket Delta Limit Reached.");
                        }
                    }
                    deltaCheckTime = _latencyTimer.ElapsedMilliseconds;

                    if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        throw new SlimException(message: "Basket Long Position Limit Reached.");

                    }

                    if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        throw new SlimException(message: "Basket Short Position Limit Reached.");
                    }
                    posCheckTime = _latencyTimer.ElapsedMilliseconds - deltaCheckTime;

                    _referenceTradeOriginalPrice = referenceTradeOriginalPrice;
                    if (BasketSettings.AdjustPriceBeforeSubmit && !skipAdjPxBeforeSubmit)
                    {
                        _log.Info(message: "Adjust px before submit enabled." + GetStats());
                        time = await SetEdgeAsync(ignoreAdjTheoRiskCheck: false);
                        _log.Info(message: "Set price complete." + GetStats());
                    }
                    else
                    {
                        _log.Info(message: "Adjust px before submit disabled." + GetStats());
                    }
                    setEdgeTime = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime);

                    if (double.IsNaN(d: Price))
                    {
                        throw new SlimException("Invalid Price");
                    }

                    if (BasketTraderViewModel != null && BasketTraderViewModel.IsEdgeScanFeedAutoTrader && doNotTradeThroughFillPrice)
                    {
                        _log.Info(message: "Check for fill price crossing on edge scan feed." + GetStats());
                        if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            if (Price > EdgeScanFeedBuyPrice)
                            {
                                throw new SlimException("Trigger Px Crossed!");
                            }
                        }
                        else if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            if (Math.Abs(value: Price) < Math.Abs(value: EdgeScanFeedSellPrice))
                            {
                                throw new SlimException("Trigger Px Crossed!");
                            }
                        }
                    }
                    riskCheckTime = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime);

                    if (DateTime.Now - time > RiskTimeSpan)
                    {
                        throw new SlimException("Set Edge Timeout!");
                    }

                    CheckForPriceCross(referenceTradeModel: referenceTradeModel);
                    pxCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime);
                }

                if (double.IsNaN(d: Price))
                {
                    throw new SlimException("Invalid Px");
                }

                switch (TicketStyle)
                {
                    case OrderTicketStyle.Combined:
                        if (!OmsCore.Config.AllowCombinedSimultaneousOrders && IsContraCancelEnabled)
                        {
                            CancelContraAsync();
                        }
                        break;
                    case OrderTicketStyle.Dual:
                        if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            int newPos = TraderSpreadPosition + Lcd;
                            if (newPos > 0 && Math.Abs(value: newPos) > OmsCore.Config.MaxPosForDualTicket)
                            {
                                throw new SlimException("Dual Ticket Max Position Risk.");
                            }
                        }
                        else if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            int newPos = TraderSpreadPosition - Lcd;
                            if (newPos < 0 && Math.Abs(value: newPos) > OmsCore.Config.MaxPosForDualTicket)
                            {
                                throw new SlimException("Dual Ticket Max Position Risk.");
                            }
                        }
                        break;

                }

                if (SubmitWithDelayEnabled)
                {
                    SubmitWithDelayEnabled = false;
                }

                ThreeWayStarted = false;
                ThreeWayComplete = false;

                if (payUpTicks > 0)
                {
                    var prevPrice = Price;
                    if (IsSingleLegSell)
                    {
                        SetPrice(prevPrice - (payUpTicks * (double)GetPriceIncrement(prevPrice, IncrementDirection.Down)));
                    }
                    else
                    {
                        SetPrice(prevPrice + (payUpTicks * (double)GetPriceIncrement(prevPrice, IncrementDirection.Up)));
                    }

                    await CheckForPercentOfMarketCap(skipAdjPxBeforeSubmit: skipAdjPxBeforeSubmit);
                    string checkNewRiskTaskResult = await CheckRiskParametersAsync();
                    if (!string.IsNullOrEmpty(value: checkNewRiskTaskResult))
                    {
                        SetPrice(prevPrice);
                    }
                }

                await CheckForPercentOfMarketCap(skipAdjPxBeforeSubmit: skipAdjPxBeforeSubmit);

                marketCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck);
                bool success = false;
                time = DateTime.Now;
                string checkRiskTaskResult = await CheckRiskParametersAsync();
                TimeSpan span = DateTime.Now - time;
                if (!string.IsNullOrEmpty(value: checkRiskTaskResult))
                {
                    throw new SlimException("Risk. " + checkRiskTaskResult);
                }
                else if (span > RiskTimeSpan)
                {
                    throw new SlimException("Risk. Timeout checking for risk.");
                }
                else
                {
                    riskCheckTime2 = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck + marketCrossCheck);
                    TotalResubmitCount = totalResubmitCount;
                    ResetLastFillTrackers();
                    SharedId = OmsCore.OrderClient.GetNextSharedId();
                    Sequence = 0;
                    SubTypeId = ZeroPlus.Models.Data.Enums.SubType.FishOpen;

                    success = await SubmitOrderAsync(resting, subType, restOverride, span);
                    submitLatency = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck + marketCrossCheck + riskCheckTime2);

                    _log.Info(message: $"Latency Log. Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}, Risk Check: {riskCheckTime:N2}, Px Cross Check: {pxCrossCheck:N2}, Mkt Cross Check: {marketCrossCheck:N2}, Risk 2 Check: {riskCheckTime2:N2}, Submit Latency: {submitLatency:N2}" + GetStats());
                }

                return success;
            }
            catch (SlimException sle)
            {
                _log.Warn(message: sle.Message + " " + _latencyTimer.ElapsedMilliseconds + GetStats());
                _latencyTimer.Stop();
                ShowErrorMessage(sle.Message);
                NotifyOrderCloseWaitHandlers(main: true, orderStatus: null);
                markForRemoval = true;
                return false;
            }
        }

        protected virtual async Task<bool> SubmitOrderAsync(bool resting, OrderSubType? subType, double restOverride, TimeSpan span)
        {
            bool success;
            TypeId = ZeroPlus.Models.Data.Enums.ModuleType.Ticket;
            success = await Task.Run(function: () => SubmitOrderAsync(isContra: false, resting: resting, subType, cancelDelay: restOverride));
            return success;
        }

        private async Task<bool> CheckForPercentOfMarketCap(bool skipAdjPxBeforeSubmit)
        {
            DateTime time;
            if (!IsSingleLeg && !await IsWithinPercentMarketCap())
            {
                bool proceed = (!IsBasketOrder || !BasketTraderViewModel.IsEdgeScanFeedAutoTrader) && GetRiskVerification($"Your price crosses market by more than {Math.Round(_riskModel.RiskCheckMarketPercentage * 100, 2)}%.\nMkt: [{Low:F2}X{High:F2}] Px: {Price:F2}\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                if (!proceed)
                {
                    throw new SlimException("Risk. Price crosses market.");
                }

                if (IsBasketOrder && !skipAdjPxBeforeSubmit)
                {
                    time = DateTime.Now;
                    if (BasketSettings.AdjustPriceBeforeSubmit)
                    {
                        _log.Info("Adjust px before submit enabled. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        time = await SetEdgeAsync(ignoreAdjTheoRiskCheck: true);
                        _log.Info("Set price complete. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    }
                    else
                    {
                        _log.Info("Adjust px before submit disabled. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    }
                    if (DateTime.Now - time > RiskTimeSpan)
                    {
                        throw new SlimException("Set Edge Timeout!");
                    }
                }
            }

            return false;
        }

        private void CheckForPriceCross(ReferenceTradeModel referenceTradeModel)
        {
            if (!IsStockTied)
            {
                if (IsSingleLeg)
                {
                    if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        if (Price < Low)
                        {
                            _log.Info("Px Cross Detected!" + GetStats());
                            switch (BasketSettings.PxCrossOption)
                            {
                                case PxCrossOption.Drop:
                                    throw new SlimException("Px Cross Detected!");

                                case PxCrossOption.Adjust:
                                    SetPriceMinimal(Low);
                                    break;
                                case PxCrossOption.SmartAdjust:
                                case PxCrossOption.SmartAdjustDrop:
                                    double minIncrement = Convert.ToDouble(PriceIncrement);
                                    double newPrice = Low + minIncrement;

                                    if (newPrice > NetDeltaAdjTheo)
                                    {
                                        throw new SlimException("Px Cross Detected!");
                                    }

                                    if (referenceTradeModel != null)
                                    {
                                        if (referenceTradeModel.TryGetAdjustedBid(UnderMid, TotalDelta, out double refTradeBid))
                                        {
                                            if (refTradeBid < Low)
                                            {
                                                if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjust)
                                                {
                                                    newPrice = Low;
                                                }
                                                else if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjustDrop)
                                                {
                                                    throw new SlimException("Px Cross Detected!");
                                                }
                                            }
                                        }

                                        if (referenceTradeModel.TryGetAdjustedTradePrice(UnderMid, TotalDelta, out double refTradePrice))
                                        {
                                            if (refTradePrice > NetDeltaAdjTheo)
                                            {
                                                throw new SlimException("Px Cross Detected!");
                                            }
                                        }
                                    }

                                    double minEdge = GetLoopMinEdge();
                                    double increment = (double)GetPriceIncrement();
                                    double check = Math.Max(minEdge, increment);
                                    if (newPrice >= High - check)
                                    {
                                        throw new SlimException("Min Edge From Mkt Cross!");
                                    }

                                    SetPriceMinimal(newPrice);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        if (Price > High)
                        {
                            _log.Info("Px Cross Detected!" + GetStats());
                            switch (BasketSettings.PxCrossOption)
                            {
                                case PxCrossOption.Drop:
                                    throw new SlimException("Px Cross Detected!");
                                case PxCrossOption.Adjust:
                                    SetPriceMinimal(High);
                                    break;
                                case PxCrossOption.SmartAdjust:
                                case PxCrossOption.SmartAdjustDrop:
                                    double minIncrement = Convert.ToDouble(PriceIncrement);
                                    double newPrice = High - minIncrement;

                                    if (newPrice < NetDeltaAdjTheo)
                                    {
                                        throw new SlimException("Px Cross Detected!");
                                    }

                                    if (referenceTradeModel != null)
                                    {
                                        if (referenceTradeModel.TryGetAdjustedAsk(UnderMid, TotalDelta, out double refTradeAsk))
                                        {
                                            if (refTradeAsk > High)
                                            {
                                                if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjust)
                                                {
                                                    newPrice = High;
                                                }
                                                else if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjustDrop)
                                                {
                                                    throw new SlimException("Px Cross Detected!");
                                                }
                                            }
                                        }

                                        if (referenceTradeModel.TryGetAdjustedTradePrice(UnderMid, TotalDelta, out double refTradePrice))
                                        {
                                            if (refTradePrice < NetDeltaAdjTheo)
                                            {
                                                throw new SlimException("Px Cross Detected!");
                                            }
                                        }
                                    }

                                    double minEdge = GetLoopMinEdge();
                                    double increment = (double)GetPriceIncrement();
                                    double check = Math.Max(minEdge, increment);
                                    if (newPrice <= Low + check)
                                    {
                                        throw new SlimException("Min Edge From Mkt Cross!");
                                    }

                                    SetPriceMinimal(newPrice);
                                    break;
                            }
                        }
                    }
                }
                else
                {
                    if (Price < Low)
                    {
                        _log.Info("Px Cross Detected!" + GetStats());
                        switch (BasketSettings.PxCrossOption)
                        {
                            case PxCrossOption.Drop:
                                throw new SlimException("Px Cross Detected!");
                            case PxCrossOption.Adjust:
                                SetPriceMinimal(Low);
                                break;
                            case PxCrossOption.SmartAdjust:
                            case PxCrossOption.SmartAdjustDrop:
                                double minIncrement = Convert.ToDouble(PriceIncrement);
                                double newPrice = Low + minIncrement;

                                if (newPrice > NetDeltaAdjTheo)
                                {
                                    throw new SlimException("Px Cross Detected!");
                                }

                                if (referenceTradeModel != null)
                                {
                                    if (referenceTradeModel.TryGetAdjustedBid(UnderMid, TotalDelta, out double refTradeBid))
                                    {
                                        if (refTradeBid < Low)
                                        {
                                            if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjust)
                                            {
                                                newPrice = Low;
                                            }
                                            else if (BasketSettings.PxCrossOption == PxCrossOption.SmartAdjustDrop)
                                            {
                                                throw new SlimException("Px Cross Detected!");
                                            }
                                        }
                                    }

                                    if (referenceTradeModel.TryGetAdjustedTradePrice(UnderMid, TotalDelta, out double refTradePrice))
                                    {
                                        if (refTradePrice > NetDeltaAdjTheo)
                                        {
                                            throw new SlimException("Px Cross Detected!");
                                        }
                                    }
                                }

                                double minEdge = GetLoopMinEdge();
                                double increment = (double)GetPriceIncrement();
                                double check = Math.Max(minEdge, increment);
                                if (newPrice >= High - check)
                                {
                                    throw new SlimException("Min Edge From Mkt Cross!");
                                }

                                SetPriceMinimal(newPrice);
                                break;
                        }
                    }
                }
            }
        }

        private bool TrySendToAutoTraderOrIb(double? edge = null, bool isContra = false, bool resting = false, bool skipAdjPxBeforeSubmit = false)
        {
            bool done = false;

            if (IsIbTicket)
            {
                OrderTicket outboundOrder = CreateOutboundMainOrder(
                    destination: Destination,
                    tag: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                    comment: EdgeProjector != null && EdgeProjector.IsThreeWay && SuggestTradingMain
                        ? EdgeProjector.GetComment()
                        : "",
                    resting: resting);
                OmsCore.IbGatewayClient.SendOrder(outboundOrder, this);
                return true;
            }

            var isValid = InstanceMode.IsAutoTraderInstance();

            if (isValid)
            {
                if (IsBasketOrder)
                {
                    if (!isContra)
                    {
                        done = SendBasketOrderToAutoTrader(edge: edge, resting: resting, skipAdjPxBeforeSubmit: skipAdjPxBeforeSubmit);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    bool isClosing = IsClosing(isContra);
                    CheckForPosEffect(isContra, isClosing);

                    if (!isContra)
                    {
                        done = SendTicketOrderToAutoTrader(resting: resting);
                    }
                    else
                    {
                        done = SendTicketContraOrderToAutoTrader(resting: resting);
                    }
                }
            }
            else if (IsLowLatencyHangManager && !IsBasketOrder && !isContra)
            {
                done = SendTicketOrderToAutoTrader(resting: resting);
            }

            return done;
        }

        protected virtual bool TrySendToMatrix()
        {
            return false;
        }

        private bool SendBasketOrderToAutoTrader(double? edge, bool resting = false, bool skipAdjPxBeforeSubmit = false)
        {
            OrderTicket outboundOrder = CreateOutboundMainOrder(
                destination: DestinationUid,
                tag: OmsCore.User.Username,
                comment: BasketSettings.Uid,
                resting: resting,
                skipAdjPxBeforeSubmit: skipAdjPxBeforeSubmit,
                useBasketCancelDelay: true);

            if (edge != null)
            {
                EdgeOverride = edge.Value;
                outboundOrder.EdgeOverride = edge.Value;
            }

            AutomationRunning = true;
            IsLooping = true;

            OmsCore.OrderLifecycleService.RecordSentToAutoTrader(outboundOrder.LocalID, _omsOrderInitiatedNanos, EpochNanosTimer.Now());
            OmsCore.AutoTraderClient.SendOrder(outboundOrder, this);

            _log.Info("Sending basket order to auto trader. Destination: {}, Spread: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}, Time: {}", outboundOrder.Destination, outboundOrder.SpreadId, outboundOrder.SubType, outboundOrder.Quantity, outboundOrder.Price, outboundOrder.EdgeType, outboundOrder.Edge, outboundOrder.MinimumTickStyle, DateTime.Now);

            return true;
        }

        private bool SendTicketOrderToAutoTrader(bool resting = false)
        {
            Venue ??= ZeroPlus.Models.Data.Enums.Venue.TB;
            OrderTicket outboundOrder = CreateOutboundMainOrder(
                destination: "Direct",
                tag: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                comment: EdgeProjector != null && EdgeProjector.IsThreeWay && SuggestTradingMain
                    ? EdgeProjector.GetComment()
                    : "",
                resting: resting);
            MainResting = true;

            OmsCore.OrderLifecycleService.RecordSentToAutoTrader(outboundOrder.LocalID, _omsOrderInitiatedNanos, EpochNanosTimer.Now());
            OmsCore.AutoTraderClient.SendOrder(outboundOrder, this);

            return true;
        }

        protected bool CheckForMatrixRoute()
        {
            SmartStrategyData strategyData = null;
            var routeLookup = OmsCore.OrderClient?.RouteLookup;
            if (TrySelectRoute(false, true, out var route, out var delay) &&
                routeLookup?.IsMatrixSmartRoute(route, out strategyData) == true)
            {
                if (strategyData is ScrapeStrategyData scrapeStrategyData)
                {
                    SendMatrixScrape(scrapeStrategyData);
                    return true;
                }
                if (strategyData is SeekerStrategyData seekerStrategyData)
                {
                    SendMatrixSeeker(seekerStrategyData);
                    return true;
                }
                if (strategyData is SeekerSpreadStrategyData seekerSpreadStrategyData)
                {
                    SendMatrixSeekerSpread(seekerSpreadStrategyData);
                    return true;
                }
                if (strategyData is SyntheticSpreadStrategyData syntheticSpreadStrategyData)
                {
                    SendMatrixSyntheticSpread(syntheticSpreadStrategyData);
                    return true;
                }
            }

            return false;
        }

        protected void SendMatrixSyntheticSpread(SyntheticSpreadStrategyData strategyConfig)
        {
            var count = Legs.Count;
            if (count < 2 || count > 4)
            {
                ShowErrorMessage("Invalid leg count for synthetic spread!");
                return;
            }

            if (strategyConfig == null)
            {
                ShowErrorMessage("Synthetic spread config not found!");
                return;
            }

            SyntheticSpread order = BuildSyntheticSpread(strategyConfig);
            if (order == null)
            {
                _log.Info(
                    "Sending synthetic order failed. Spread: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}, Time: {}",
                    SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle, DateTime.Now);
                return;
            }

            MainResting = true;
            Venue = ZeroPlus.Models.Data.Enums.Venue.Matrix;
            OmsCore.AutoTraderClient.SendOrder(order, this);
            _log.Info(
                "Sending synthetic order. Spread: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}, Time: {}",
                SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle, DateTime.Now);
        }

        protected void SendMatrixScrape(ScrapeStrategyData configModel)
        {
            var count = Legs.Count;
            if (count != 1)
            {
                ShowErrorMessage("Invalid leg count for scrape!");
                return;
            }

            if (configModel == null)
            {
                ShowErrorMessage("Scrape config not found!");
                return;
            }

            Scrape order = BuildScrapeOrder(configModel);
            if (order == null)
            {
                _log.Info(
                    "Sending order failed. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}, Time: {}",
                    SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle, DateTime.Now);
                return;
            }

            MainResting = true;
            Venue = ZeroPlus.Models.Data.Enums.Venue.Matrix;
            OmsCore.AutoTraderClient.SendOrder(order, this);
            _log.Info(
                "Sending order. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}, Time: {}",
                SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle, DateTime.Now);
        }

        protected void SendMatrixSeeker(SeekerStrategyData configModel)
        {
            var count = Legs.Count;
            if (count != 1)
            {
                ShowErrorMessage("Invalid leg count for Seeker!");
                return;
            }

            if (configModel == null)
            {
                ShowErrorMessage("Seeker config not found!");
                return;
            }

            Seeker order = BuildSeekerOrder(configModel);
            if (order == null)
            {
                _log.Info(
                    "Sending order failed. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}",
                    SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle);
                return;
            }

            MainResting = true;
            Venue = ZeroPlus.Models.Data.Enums.Venue.Matrix;
            OmsCore.AutoTraderClient.SendOrder(order, this);
            _log.Info(
                "Sending order. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}",
                SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle);
        }

        protected void SendMatrixSeekerSpread(SeekerSpreadStrategyData configModel)
        {
            var count = Legs.Count;
            if (count < 2 || count > 4)
            {
                ShowErrorMessage("Invalid leg count for SeekerSpread!");
                return;
            }

            if (configModel == null)
            {
                ShowErrorMessage("SeekerSpread config not found!");
                return;
            }

            SeekerSpread order = BuildSeekerSpreadOrder(configModel);
            if (order == null)
            {
                _log.Info(
                    "Sending order failed. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}",
                    SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle);
                return;
            }

            MainResting = true;
            Venue = ZeroPlus.Models.Data.Enums.Venue.Matrix;
            OmsCore.AutoTraderClient.SendOrder(order, this);
            _log.Info(
                "Sending order. Desc: {}, Subtype: {}, Qty: {}, Price: {}, Type: {}, Edge: {}, Min Tick: {}",
                SpreadId, SubType, Quantity, Price, EdgeType, Edge, MinimumTickStyle);
        }

        private SyntheticSpread BuildSyntheticSpread(SyntheticSpreadStrategyData strategyData)
        {
            SyntheticSpread order = new();
            order.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
            order.Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            order.Price = Price;
            order.OrderQuantity = Qty;
            order.Memo = GetTag(false, "Synt", false, "").Replace("NaN", "").Replace("80000000", "");
            order.Source = "OMS";
            order.Tif = TimeInForce == TimeInForce.IOC
                ? ZeroPlus.Models.Data.Enums.Matrix.Tif.IOC
                : ZeroPlus.Models.Data.Enums.Matrix.Tif.DAY;
            order.Destination = DestinationUid;
            if (TryGetAutoCancel(false, order.Exchange, out var cancelDelay))
            {
                order.CancelDelay = (int)cancelDelay;
            }

            foreach (var leg in Legs)
            {
                if (!leg.IsValid)
                {
                    return null;
                }
                var spreadLeg = new SpreadLeg();
                spreadLeg.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
                spreadLeg.InstrumentType = leg.Type == Types.STOCK.ToString()
                    ? InstrumentType.EQUITY
                    : InstrumentType.EQUITYOPTION;
                spreadLeg.LegRatio = leg.Ratio;
                spreadLeg.Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                    ? ZeroPlus.Models.Data.Enums.Matrix.Side.Buy
                    : ZeroPlus.Models.Data.Enums.Matrix.Side.Sell;

                switch (leg.Position)
                {
                    case "OPEN":
                        spreadLeg.OpenClose = OpenClose.Open;
                        break;
                    case "CLOSE":
                        spreadLeg.OpenClose = OpenClose.Close;
                        break;
                }

                var inst = new SymbolLib.Instrument(leg.Symbol);
                var legSymbol = inst.ToOSIStrict();
                spreadLeg.Symbol = legSymbol;
                order.Legs.Add(spreadLeg);
            }

            order.StrategyData.CopyFrom(strategyData);

            return order;
        }

        private Scrape BuildScrapeOrder(ScrapeStrategyData strategyData)
        {
            Scrape order = new();
            order.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
            order.Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            order.Price = Price;
            order.OrderQuantity = Qty;
            order.Memo = GetTag(false, "Scrape", false, "").Replace("NaN", "").Replace("80000000", "");
            order.Source = "OMS";
            order.Tif = ZeroPlus.Models.Data.Enums.Matrix.Tif.DAY;
            order.Destination = DestinationUid;
            order.MinimumTickStyle = MinimumTickStyle;

            if (TryGetAutoCancel(false, order.Exchange, out var cancelDelay))
            {
                order.CancelDelay = (int)cancelDelay;
            }

            var leg = Legs.FirstOrDefault();
            if (leg != null)
            {
                var inst = new SymbolLib.Instrument(leg.Symbol);
                var legSymbol = inst.ToOSIStrict();
                order.Symbol = legSymbol;
                switch (leg.Position)
                {
                    case "OPEN":
                        order.OpenClose = OpenClose.Open;
                        break;
                    case "CLOSE":
                        order.OpenClose = OpenClose.Close;
                        break;
                }
                order.Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                    ? ZeroPlus.Models.Data.Enums.Matrix.Side.Buy
                    : ZeroPlus.Models.Data.Enums.Matrix.Side.Sell;
            }

            order.StrategyData.CopyFrom(strategyData);

            return order;
        }

        private Seeker BuildSeekerOrder(SeekerStrategyData strategyData)
        {
            Seeker order = new();
            order.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
            order.Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            order.Price = Price;
            order.OrderQuantity = Qty;
            order.Memo = GetTag(false, "Seeker", false, "").Replace("NaN", "").Replace("80000000", "");
            order.Source = "OMS";
            order.Tif = ZeroPlus.Models.Data.Enums.Matrix.Tif.DAY;
            order.Destination = DestinationUid;
            order.MinimumTickStyle = MinimumTickStyle;

            if (TryGetAutoCancel(false, order.Exchange, out var cancelDelay))
            {
                order.CancelDelay = (int)cancelDelay;
            }

            var leg = Legs.FirstOrDefault();
            if (leg != null)
            {
                var inst = new SymbolLib.Instrument(leg.Symbol);
                var legSymbol = inst.ToOSIStrict();
                order.Symbol = legSymbol;
                switch (leg.Position)
                {
                    case "OPEN":
                        order.OpenClose = OpenClose.Open;
                        break;
                    case "CLOSE":
                        order.OpenClose = OpenClose.Close;
                        break;
                }
                order.Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                    ? ZeroPlus.Models.Data.Enums.Matrix.Side.Buy
                    : ZeroPlus.Models.Data.Enums.Matrix.Side.Sell;
            }

            order.StrategyData.CopyFrom(strategyData);

            return order;
        }

        private SeekerSpread BuildSeekerSpreadOrder(SeekerSpreadStrategyData strategyData)
        {
            SeekerSpread order = new();
            order.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
            order.Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            order.Price = Price;
            order.OrderQuantity = Qty;
            order.Memo = GetTag(false, "Seeker", false, "").Replace("NaN", "").Replace("80000000", "");
            order.Source = "OMS";
            order.Tif = ZeroPlus.Models.Data.Enums.Matrix.Tif.DAY;
            order.Destination = DestinationUid;

            if (TryGetAutoCancel(false, order.Exchange, out var cancelDelay))
            {
                order.CancelDelay = (int)cancelDelay;
            }

            foreach (var leg in Legs)
            {
                if (!leg.IsValid)
                {
                    return null;
                }
                var spreadLeg = new SpreadLeg();
                spreadLeg.ClientGuid = OmsCore.OrderClient.GetNextOrderId();
                spreadLeg.InstrumentType = leg.Type == Types.STOCK.ToString()
                    ? InstrumentType.EQUITY
                    : InstrumentType.EQUITYOPTION;
                spreadLeg.LegRatio = leg.Ratio;
                spreadLeg.Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                    ? ZeroPlus.Models.Data.Enums.Matrix.Side.Buy
                    : ZeroPlus.Models.Data.Enums.Matrix.Side.Sell;

                switch (leg.Position)
                {
                    case "OPEN":
                        spreadLeg.OpenClose = OpenClose.Open;
                        break;
                    case "CLOSE":
                        spreadLeg.OpenClose = OpenClose.Close;
                        break;
                }

                var inst = new SymbolLib.Instrument(leg.Symbol);
                var legSymbol = inst.ToOSIStrict();
                spreadLeg.Symbol = legSymbol;
                order.Legs.Add(spreadLeg);
            }

            order.StrategyData.CopyFrom(strategyData);

            return order;
        }

        private string GetTag(bool isContra, string module, bool stampValues, string comment)
        {
            return new TagCodec(trader: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                edge: GetTagEdge(isContra),
                type: OmsCore.OrderClient.TYPE + GetLog(isContra),
                subtype: module,
                tv: stampValues ? isContra ? -NetDeltaAdjTheo : NetDeltaAdjTheo : double.NaN,
                ema: stampValues ? isContra ? -GetEma() : GetEma() : double.NaN,
                bid: stampValues ? isContra ? -High : Low : double.NaN,
                ask: stampValues ? isContra ? -Low : High : double.NaN,
                comment: !string.IsNullOrEmpty(comment)
                    ? comment
                    : (IsBasketOrder
                        ? BasketSettings.Uid + (BasketSettings.UseCustomFunctionEdge
                            ? "fn=" + (BasketSettings.CustomFunctionEdgeFormula.Length > 55
                                ? BasketSettings.CustomFunctionEdgeFormula.Substring(0, 55)
                                : BasketSettings.CustomFunctionEdgeFormula)
                            : "")
                        : ""),
                sharedId: SharedId,
                sequence: Sequence++,
                typeId: (ushort)TypeId,
                subTypeId: (ushort)GetSubType(isContra),
                subTypeSequence: SubTypeSequence,
                v0: stampValues ? isContra ? -VolaTheoAdjV0 : VolaTheoAdjV0 : double.NaN,
                v1: stampValues ? isContra ? -VolaTheoAdjV1 : VolaTheoAdjV1 : double.NaN,
                v2: stampValues ? isContra ? -VolaTheoAdjV2 : VolaTheoAdjV2 : double.NaN).Encode();
        }

        private OrderTicket CreateOutboundMainOrder(string destination, string tag, string comment, bool resting, bool? skipAdjPxBeforeSubmit = null, bool useBasketCancelDelay = false)
        {
            string wireRoute = ApplyBrokerPrefix(Route ?? GetBestRoute());
            string smartRoute = SmartRoute;
            if (!string.IsNullOrWhiteSpace(wireRoute) && OmsCore.Config.SmartRoutes.TryGetValue(wireRoute,
                    out Dictionary<int, Tuple<string, double>> smartRouteMapping))
            {
                smartRoute = JsonConvert.SerializeObject(smartRouteMapping.Select(x => x.Value).ToList());
            }

            double newToCancelTime = NewToCancelTime;
            if (resting)
            {
                newToCancelTime = 0.0;
            }
            else if (useBasketCancelDelay)
            {
                newToCancelTime = GetBasketCancelTimer(wireRoute);
            }
            else if (TryGetAutoCancel(false, wireRoute, out var delay))
            {
                newToCancelTime = delay;
            }

            string underlyingSymbol = IsIbTicket ? UnderlyingSymbol : Underlying;
            string localId = OmsCore.OrderClient.GetNextOrderId();
            PositionEffect positionEffect = Legs.Any(x => x.PositionEffect == PositionEffect.Close)
                ? PositionEffect.Close
                : PositionEffect.AUTO;

            Tag = tag;
            Quantity = Lcd;
            AccountAcronym = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            UnderlyingSymbol = underlyingSymbol;
            CloseUnderBid = LastMainUnderMidAtFill;
            CloseUnderAsk = LastMainUnderMidAtFill;
            Comment = comment;
            PositionEffect = positionEffect;
            SmartRoute = smartRoute;
            Destination = destination;
            NewToCancelTime = newToCancelTime;
            LocalID = localId;
            if (skipAdjPxBeforeSubmit.HasValue)
            {
                SkipNewPriceEvaluation = skipAdjPxBeforeSubmit.Value;
            }

            for (int i = 0; i < Legs.Count; i++)
            {
                TicketLegModel leg = Legs[i];
                leg.LegID = LocalID + i;
            }

            OrderTicket outboundOrder = (OrderTicket)MemberwiseClone();
            outboundOrder.Tag = tag;
            outboundOrder.AccountAcronym = AccountAcronym;
            outboundOrder.UnderlyingSymbol = underlyingSymbol;
            outboundOrder.CloseUnderBid = CloseUnderBid;
            outboundOrder.CloseUnderAsk = CloseUnderAsk;
            outboundOrder.Route = wireRoute;
            outboundOrder.Comment = comment;
            outboundOrder.PositionEffect = positionEffect;
            outboundOrder.SmartRoute = smartRoute;
            outboundOrder.Destination = destination;
            outboundOrder.NewToCancelTime = newToCancelTime;
            outboundOrder.LocalID = localId;
            if (skipAdjPxBeforeSubmit.HasValue)
            {
                outboundOrder.SkipNewPriceEvaluation = skipAdjPxBeforeSubmit.Value;
            }

            return outboundOrder;
        }

        private bool SendTicketContraOrderToAutoTrader(bool resting = false)
        {
            IOrderSlim order = IsSingleLeg ?
                BuildSingleLegContraOrderSlim(resting) :
                BuildMultiLegContraOrderSlim(resting);
            ContraResting = true;

            OmsCore.OrderLifecycleService.RecordSentToAutoTrader(order.LocalID, _omsOrderInitiatedNanos, EpochNanosTimer.Now());
            OmsCore.AutoTraderClient.SendSlimOrder(order, this);

            _log.Info("Sending ticket contra order to order gateway. Destination: {}, Spread: {}, Subtype: {}, Qty: {}, Price: {}", order.Destination, order.SpreadId, order.SubType, order.Quantity, order.Price);

            return true;
        }

        private IOrderSlim BuildSingleLegContraOrderSlim(bool resting)
        {
            var route = ApplyBrokerPrefix(!string.IsNullOrWhiteSpace(ContraRoute) ? ContraRoute : GetBestRoute());
            double cancelDelay = 0;
            if (!resting && TryGetAutoCancel(true, route, out var delay))
            {
                cancelDelay = delay;
            }

            var comment = EdgeProjector != null && EdgeProjector.IsThreeWay && SuggestTradingMain
                ? EdgeProjector.GetComment()
                : "";
            var positionEffect = Legs.Any(x => x.PositionEffect == PositionEffect.Close)
                ? PositionEffect.Close
                : PositionEffect.AUTO;
            var smartRoute = !string.IsNullOrWhiteSpace(Route) && OmsCore.Config.SmartRoutes.TryGetValue(Route,
                out Dictionary<int, Tuple<string, double>> smartRouteMapping)
                ? JsonConvert.SerializeObject(smartRouteMapping.Select(x => x.Value).ToList())
                : string.Empty;
            IOrderSlim orderSlim = new OrderSlim(OmsCore.SecurityBook)
            {
                IsComplexOrder = IsComplexOrder,
                BaseStrategy = BaseStrategy,
                UnderlyingSymbol = IsIbTicket ? UnderlyingSymbol : Underlying,
                Currency = Currency,
                SpreadId = SpreadId,
                Security = Security,
                Side = ((IOrder)this).Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                MinimumTickStyle = MinimumTickStyle,
                Quantity = Lcd,
                Price = ContraPrice,
                Tag = IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                RouteOverride = RouteOverride,
                Bid = Bid,
                Mid = Mid,
                Ask = Ask,
                Ema = Ema,
                TotalDelta = TotalDelta,
                HanweckTotalTheo = HanweckTotalTheo,
                DeltaAdjustedTheo = DeltaAdjustedTheo,
                UnderBid = UnderBid,
                UnderMid = UnderMid,
                UnderAsk = UnderAsk,
                SubType = IsGammaScalpTicket ? ZeroPlus.Models.Data.Enums.OrderSubType.GammaScalp : ZeroPlus.Models.Data.Enums.OrderSubType.Ticket,
                SmartRoute = smartRoute,
                AdjustedEdgeOverride = AdjustedEdgeOverride,
                EdgeOverride = EdgeOverride,
                CloseUnderBid = LastContraUnderMidAtFill,
                CloseUnderAsk = LastContraUnderMidAtFill,
                AveragePrice = ContraAveragePrice,
                Route = route,
                LocalID = OmsCore.OrderClient.GetNextOrderId(),
                Multiplier = Multiplier,
                Destination = "DirectContra",
                Venue = Venue ?? ZeroPlus.Models.Data.Enums.Venue.TB,
                TagEdge = TagEdge,
                EdgeType = EdgeType,
                AccountAcronym = AccountLocked ? OmsCore.Config.DefaultAccount : Account,
                TimeInForce = TimeInForce,
                PositionEffect = positionEffect,
                NewToCancelTime = cancelDelay,
                Comment = comment,
                Symbol = Symbol,
                PrimaryExchange = PrimaryExchange,
            };

            return orderSlim;
        }

        private IOrderSlim BuildMultiLegContraOrderSlim(bool resting)
        {
            var route = ApplyBrokerPrefix(!string.IsNullOrWhiteSpace(ContraRoute) ? ContraRoute : GetBestRoute());
            double cancelDelay = 0;
            if (!resting && TryGetAutoCancel(true, route, out var delay))
            {
                cancelDelay = delay;
            }

            var comment = EdgeProjector != null && EdgeProjector.IsThreeWay && SuggestTradingMain
                ? EdgeProjector.GetComment()
                : "";
            var positionEffect = Legs.Any(x => x.PositionEffect == PositionEffect.Close)
                ? PositionEffect.Close
                : PositionEffect.AUTO;
            var smartRoute = !string.IsNullOrWhiteSpace(Route) && OmsCore.Config.SmartRoutes.TryGetValue(Route,
                out Dictionary<int, Tuple<string, double>> smartRouteMapping)
                ? JsonConvert.SerializeObject(smartRouteMapping.Select(x => x.Value).ToList())
                : string.Empty;
            var contraPrice = TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical ? -ContraPrice : ContraPrice;
            IComplexOrderSlim complexOrderSlim = new ComplexOrderSlim(OmsCore.SecurityBook)
            {
                IsComplexOrder = IsComplexOrder,
                BaseStrategy = BaseStrategy,
                UnderlyingSymbol = IsIbTicket ? UnderlyingSymbol : Underlying,
                Currency = Currency,
                SpreadId = SpreadId,
                Security = Security,
                Side = ((IOrder)this).Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                MinimumTickStyle = MinimumTickStyle,
                Quantity = Lcd,
                Price = contraPrice,
                Tag = IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                RouteOverride = RouteOverride,
                Bid = -Ask,
                Mid = -Mid,
                Ask = -Bid,
                Ema = -Ema,
                TotalDelta = -TotalDelta,
                HanweckTotalTheo = -HanweckTotalTheo,
                DeltaAdjustedTheo = -DeltaAdjustedTheo,
                UnderBid = UnderBid,
                UnderMid = UnderMid,
                UnderAsk = UnderAsk,
                SubType = IsGammaScalpTicket ? ZeroPlus.Models.Data.Enums.OrderSubType.GammaScalp : ZeroPlus.Models.Data.Enums.OrderSubType.Ticket,
                SmartRoute = smartRoute,
                AdjustedEdgeOverride = AdjustedEdgeOverride,
                EdgeOverride = EdgeOverride,
                CloseUnderBid = LastContraUnderMidAtFill,
                CloseUnderAsk = LastContraUnderMidAtFill,
                AveragePrice = ContraAveragePrice,
                Route = route,
                LocalID = OmsCore.OrderClient.GetNextOrderId(),
                Multiplier = Multiplier,
                Destination = "DirectContra",
                TagEdge = TagEdge,
                EdgeType = EdgeType,
                AccountAcronym = AccountLocked ? OmsCore.Config.DefaultAccount : Account,
                TimeInForce = TimeInForce,
                PositionEffect = positionEffect,
                NewToCancelTime = cancelDelay,
                Comment = comment,
                Symbol = Symbol,
                PrimaryExchange = PrimaryExchange,
            };

            for (int i = 0; i < Legs.Count; i++)
            {
                TicketLegModel leg = Legs[i];
                var complexOrderLeg = new ComplexOrderLeg(OmsCore.SecurityBook)
                {
                    Ratio = leg.Ratio,
                    Quantity = leg.Quantity,
                    Delta = leg.Delta,
                    TV = leg.TV,
                    Ask = leg.Ask,
                    Bid = leg.Bid,
                    AveragePrice = leg.AveragePrice,
                    HanweckTV = leg.HanweckTV,
                    HanweckGamma = leg.HanweckGamma,
                    HanweckVega = leg.HanweckVega,
                    HanweckTheta = leg.HanweckTheta,
                    HanweckRho = leg.HanweckRho,
                    HanweckIV = leg.HanweckIV,
                    HanweckUnder = leg.HanweckUnder,
                    HanweckUnderBid = leg.HanweckUnderBid,
                    HanweckUnderAsk = leg.HanweckUnderAsk,
                    HanweckBid = leg.HanweckBid,
                    HanweckAsk = leg.HanweckAsk,
                    Ema = leg.Ema,
                    LegID = complexOrderSlim.LocalID + i,
                    HanweckBidTime = leg.HanweckBidTime,
                    HanweckAskTime = leg.HanweckAskTime,
                    HanweckTimestamp = leg.HanweckTimestamp,
                    Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy,
                    PositionEffect = leg.PositionEffect,
                    DeltaAdjustedTheo = leg.DeltaAdjustedTheo,
                    BidSize = leg.BidSize,
                    AskSize = leg.AskSize,
                    Symbol = leg.Symbol,
                };
                complexOrderSlim.Legs.Add(complexOrderLeg);
            }

            return complexOrderSlim;
        }

        public void ClearOrderDetails()
        {
            _orderDetailsContainer.Clear();
        }

        public bool IsValidCancelDelay(double cancelDelay, out double newDelay)
        {
            double minRestPeriod = GetMinRestPeriod();
            if (cancelDelay > 0 && cancelDelay < minRestPeriod)
            {
                newDelay = minRestPeriod;
                return false;
            }

            newDelay = cancelDelay;
            return true;
        }

        public int GetMinRestPeriod()
        {
            if (Underlying == "$SPX")
            {
                if (IsSingleLeg)
                {
                    return SPX_AUCTION;
                }
                else
                {
                    return SPX_SPREAD_AUCTION;
                }
            }
            else
            {
                if (IsSingleLeg)
                {
                    return SINGLE_LEG_AUCTION;
                }
                else
                {
                    return SPREAD_AUCTION;
                }
            }
        }

        private void ResetLastFillTrackers()
        {
            BelowEdgeResubmitCounter = 0;
            LoopIterationCounter = 0;
            LoopIterationCounterAfterSizeup = 0;
            ResubmitCount = 0;
            StopLossAttemptCounter = 0;
            BestEdge = double.NaN;
            LastEdge = double.NaN;
            DeltaAdjLastEdge = double.NaN;
            LastFillUnderBidPx = double.NaN;
            LastFillUnderPx = double.NaN;
            LastFillUnderAskPx = double.NaN;
            LastFillAdjTheo = double.NaN;
            LastContraFillAdjTheo = double.NaN;
            LastLoopRoute = null;
            LastLoopContraRoute = null;
            PartiallyFilled = false;
            ContraPartiallyFilled = false;
            LeavesQty = 0;
            ContraLeavesQty = 0;
            CumulativeQty = 0;
            ContraCumulativeQty = 0;
            _resubmitWhenReceivingCancelStatus = false;
        }

        private async Task UpdateGammaScalpTicketPrice()
        {
            if (IsGammaScalpTicket)
            {
                GammaScalpOrderResubmitOnCancel = true;
                switch (ScalpPricingType)
                {
                    case PositionEntryType.PnL:
                        double pnl = ScalpPnlTarget;
                        double hedgeQty = -CalculateNetDeltaForOrder();

                        Side? side = IsSingleLeg ? Side : Mid < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        double underWidth = UnderBid - UnderAsk;
                        double hedgePx = underWidth * Math.Abs(hedgeQty);

                        double mid = GetAvgCostOrMid();

                        if (side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            double price = mid + ((-pnl - hedgePx) / (Qty * Multiplier));
                            Price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            double price = mid - ((pnl - hedgePx) / (Qty * Multiplier));
                            Price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                        }
                        break;
                    case PositionEntryType.MID:
                        await UseEdgeToMid(ScalpEdge);
                        break;
                }
            }
        }

        [Command]
        public async Task SubmitBuyAsync()
        {
            if (IsActive || MainResting || ContraResting)
            {
                return;
            }

            if (IsSingleLeg)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell && !ContraResting)
                {
                    Reverse();
                }
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy && !MainResting)
                {
                    await SubmitMainAsync();
                }
            }
            else if (Legs.Count > 1)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell && Price < 0 && !ContraResting)
                {
                    Reverse();
                }
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy && Price > 0 && !MainResting)
                {
                    await SubmitMainAsync();
                }
            }
        }

        [Command]
        public async Task SubmitSellAsync()
        {
            if (IsActive || MainResting || ContraResting)
            {
                return;
            }

            if (IsSingleLeg)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy && !ContraResting)
                {
                    Reverse();
                }
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell && !MainResting)
                {
                    await SubmitMainAsync();
                }
            }
            else if (Legs.Count > 1)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy && Price > 0 && !ContraResting)
                {
                    Reverse();
                }
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell && Price < 0 && !MainResting)
                {
                    await SubmitMainAsync();
                }
            }
        }

        internal async Task<bool> SubmitCustomEdgeAsync(Side side)
        {
            if (IsActive || MainResting || ContraResting)
            {
                return false;
            }

            ResetLastFillTrackers();
            if (IsSingleLeg)
            {
                if (Side != side && !ContraResting)
                {
                    Reverse();
                }
                if (Side == side && !MainResting)
                {
                    return await SubmitMainAsync(BasketSettings?.SellEdge);
                }
            }
            else if (Legs.Count > 1)
            {
                await SetEdgeAsync();
                if (Side != side && Price > 0 && !ContraResting)
                {
                    Reverse();
                }
                if (Side == side && Price < 0 && !MainResting)
                {
                    return await SubmitMainAsync(BasketSettings?.SellEdge);
                }
            }

            return false;
        }

        [Command]
        public async Task SubmitMainAsync()
        {
            if (IsActive)
            {
                return;
            }

            if (!await CanSubmit())
            {
                return;
            }

            if (TicketStyle == OrderTicketStyle.Combined &&
            !OmsCore.Config.AllowCombinedSimultaneousOrders)
            {
                if (IsCancelEnabled)
                {
                    CancelContraAsync();
                }
            }

            if (await IsValidOrder())
            {
                _ = Task.Run(() => SubmitOrderAsync(isContra: false));
            }
        }

        protected virtual Task<bool> CanSubmit()
        {
            return Task.FromResult(true);
        }

        public virtual Task<bool> IsValidOrder()
        {
            return Task.FromResult(true);
        }

        public async Task<bool> SubmitMainAsync(double? edge)
        {
            if (IsActive)
            {
                return false;
            }

            if (!await CanSubmit())
            {
                return false;
            }

            DateTime time = DateTime.Now;
            if (BasketSettings.AdjustPriceBeforeSubmit)
            {
                _log.Info("Adjust px before submit enabled. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                time = await SetEdgeAsync(ignoreAdjTheoRiskCheck: false, edge);
                _log.Info("Set price complete. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }
            else
            {
                _log.Info("Adjust px before submit disabled. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }

            if (DateTime.Now - time > RiskTimeSpan)
            {
                ShowMessage("Set Edge Timeout! " + SpreadId, "Set Edge Timeout!");
                return false;
            }

            if (!await IsWithinPercentMarketCap())
            {
                bool proceed = GetRiskVerification($"Your price crosses market by more than {Math.Round(_riskModel.RiskCheckMarketPercentage * 100, 2)}%.\nMkt: [{Low:F2}X{High:F2}] Px: {Price:F2}\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                if (!proceed)
                {
                    ShowErrorMessage("Risk");
                    return false;
                }

                if (IsBasketOrder)
                {
                    time = DateTime.Now;
                    if (BasketSettings.AdjustPriceBeforeSubmit)
                    {
                        _log.Info("Adjust px before submit enabled. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        time = await SetEdgeAsync(ignoreAdjTheoRiskCheck: true, edge);
                        _log.Info("Set price complete. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    }
                    else
                    {
                        _log.Info("Adjust px before submit disabled. Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    }
                    if (DateTime.Now - time > RiskTimeSpan)
                    {
                        ShowErrorMessage("Set Edge Timeout!");
                        _log.Info("Set price timedout! Attempt 2. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        return false;
                    }
                }
            }

            if (TicketStyle == OrderTicketStyle.Combined &&
            !OmsCore.Config.AllowCombinedSimultaneousOrders)
            {
                if (IsCancelEnabled)
                {
                    CancelContraAsync();
                }
            }

            if (await IsValidOrder())
            {
                return await Task.Run(() => SubmitOrderAsync(isContra: false));
            }

            return false;
        }

        [Command]
        public async Task SubmitContraAsync(string args = null)
        {
            var resting = !string.IsNullOrWhiteSpace(args);

            if (IsActive)
            {
                return;
            }

            if (IsBasketOrder && InstanceMode.IsAutoTraderInstance())
            {
                ShowMessage("Not Supported In Auto Trader Mode.",
                             "ZeroPlus OMS");
                return;
            }

            if (TicketStyle == OrderTicketStyle.Combined)
            {
                CancelRestingOrders(isContra: true);
            }

            if (SubmitWithDelayEnabled)
            {
                SubmitWithDelayEnabled = false;
            }

            if (!await CanSubmit())
            {
                return;
            }

            if (IsBasketOrder && BasketSettings.AdjustPriceBeforeSubmit)
            {
                DateTime time = DateTime.Now;
                await SetEdgeAsync(ignoreAdjTheoRiskCheck: false);
                if (DateTime.Now - time > RiskTimeSpan)
                {
                    ShowMessage("Set Edge Timeout! " + SpreadId, "Set Edge Timeout!");
                    return;
                }
            }

            if (TicketStyle == OrderTicketStyle.Combined &&
                !OmsCore.Config.AllowCombinedSimultaneousOrders)
            {
                if (IsCancelEnabled)
                {
                    CancelAsync();
                    _log.Info("Auto cancel triggered by combined ticket check." +
                          ", Spread: " + SpreadId +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                }
            }
            else if (TicketStyle == OrderTicketStyle.Dual)
            {
                int qty = !ContraQtyLocked && ContraQty > 0 ? ContraQty : Lcd;
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    int newPos = TraderSpreadPosition + qty;
                    if (newPos > 0 && Math.Abs(newPos) > OmsCore.Config.MaxPosForDualTicket)
                    {
                        ContraStatus = "Dual Ticket Max Position Risk.";
                        ContraStatusMode = StatusMode.NewSell;
                        _latencyTimer.Stop();
                        NotifyOrderCloseWaitHandlers(main: false, null);
                        return;
                    }
                }
                else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    int newPos = TraderSpreadPosition - qty;
                    if (newPos < 0 && Math.Abs(newPos) > OmsCore.Config.MaxPosForDualTicket)
                    {
                        ContraStatus = "Dual Ticket Max Position Risk.";
                        ContraStatusMode = StatusMode.NewSell;
                        _latencyTimer.Stop();
                        NotifyOrderCloseWaitHandlers(main: false, null);
                        return;
                    }
                }
            }

            if (IsGammaScalpTicket)
            {
                GammaScalpOrderResubmitOnCancel = true;

                switch (ScalpPricingType)
                {
                    case PositionEntryType.PnL:
                        double pnl = ScalpPnlTarget;
                        double hedgeQty = -CalculateNetDeltaForOrder(reverse: true);

                        Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy : Mid < 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                        Side hedgeSide = hedgeQty < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        double underWidth = UnderBid - UnderAsk;
                        double hedgePx = underWidth * Math.Abs(hedgeQty);
                        double mid = GetAvgCostOrMid();
                        mid = IsSingleLeg ? mid : -mid;

                        if (side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            double price = mid + ((-pnl - hedgePx) / (Qty * Multiplier));
                            ContraPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            double price = mid - ((pnl - hedgePx) / (Qty * Multiplier));
                            ContraPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                        }
                        break;
                    case PositionEntryType.MID:
                        await UseEdgeToMid(ScalpEdge);
                        break;
                }
            }

            _omsOrderInitiatedNanos = EpochNanosTimer.Now();
            bool done = TrySendToAutoTraderOrIb(edge: null, isContra: true, resting: resting);
            if (done)
            {
                ContraOrderSource = OrderSource.AutoTrader;
                return;
            }

            ContraOrderSource = OrderSource.OMS;

            if (await IsValidOrder())
            {
                _ = Task.Run(() =>
                {
                    return SubmitOrderAsync(isContra: true, resting);
                });
            }
        }

        public void CancelRestingOrders(bool isContra)
        {
            if (OmsCore.Config.CancelRestingOrdersOnCombinedTickets)
            {
                _transactionConsumer.CancelRestingOrders(OmsCore.User.Username, SpreadId, (this as IOrder).Side);
            }
        }

        private double GetAvgCostOrMid()
        {
            double totalMid = 0.0;
            for (int i = 0; i < Legs.Count; i++)
            {
                TicketLegModel leg = Legs[i];

                double qtyAbs = Math.Abs(leg.Quantity);
                int legSide = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy || IsSingleLeg ? 1 : -1;
                double aveCost = leg.AveCost;
                if (aveCost <= 0 || double.IsNaN(aveCost))
                {
                    aveCost = leg.Mid;
                }
                totalMid += legSide * (qtyAbs * aveCost * leg.Multiplier);
            }

            totalMid = FastRound(totalMid / Multiplier / Lcd);

            double mid = totalMid != 0 && !double.IsNaN(totalMid) ? totalMid : Mid;
            return mid;
        }

        private double CalculateNetDeltaForOrder(bool reverse = false)
        {
            double netDelta = 0.0;
            for (int j = 0; j < Legs.Count; j++)
            {
                TicketLegModel position = Legs[j];

                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(Underlying);
                if (position.Active)
                {
                    Greeks greek = position.GetGreeks(underlyingDetails, UnderMid);
                    int side = position.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? 1 : -1;
                    if (reverse)
                    {
                        side *= -1;
                    }
                    netDelta += side * greek.Delta * position.Quantity * position.Multiplier * HedgeMultiplier;
                }
            }
            return netDelta;
        }

        [Command]
        public async Task<bool> ModifyAsync()
        {
            if (!await IsWithinPercentMarketCap())
            {
                bool proceed = GetRiskVerification($"Your price crosses market by more than {Math.Round(_riskModel.RiskCheckMarketPercentage * 100, 2)}%.\nMkt: [{Low:F2}X{High:F2}] Px: {Price:F2}\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                if (!proceed)
                {
                    ShowErrorMessage("Risk");
                    CancelAsync();
                    _log.Info("Auto cancel triggered by modify risk check." +
                          ", Spread: " + SpreadId +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    return false;
                }
            }

            DateTime time = DateTime.Now;
            if ((IsBasketOrder && !BasketSettings.RiskCheckEnabled) ||
                (!IsBasketOrder && !RiskCheckEnabled))
            {
                return await Proceed();
            }
            else
            {
                string checkRiskTaskResult = await CheckRiskParametersAsync();
                if (!string.IsNullOrWhiteSpace(checkRiskTaskResult))
                {
                    ShowErrorMessage("Risk. " + checkRiskTaskResult);
                    CancelAsync();
                    _log.Info("Auto cancel triggered by modify risk check." +
                          ", Spread: " + SpreadId +
                          ", Result: " + checkRiskTaskResult +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    return false;
                }
                else if (DateTime.Now - time > RiskTimeSpan)
                {
                    ShowErrorMessage("Risk. Timeout checking for risk.");
                    CancelAsync();
                    _log.Info("Auto cancel triggered by modify risk check timeout." +
                          ", Spread: " + SpreadId +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    return false;
                }
                else
                {
                    return await Proceed();
                }
            }

            async Task<bool> Proceed()
            {
                var result = await Task.Run(Modify);
                return result;
            }
        }

        [Command]
        public async Task ModifyContraAsync()
        {
            await Task.Run(() => ModifyContra());
        }

        [Command]
        public async Task CloseByPermCommand()
        {
            if (PermCloser != null)
            {
                await Task.Run(async () =>
                {
                    var generated = await PermCloser.GeneratePermsAsync(true);
                    if (generated)
                    {
                        double increment = (double)GetPriceIncrement();
                        await PermCloser.StartAsync(Lcd, AveragePrice, OmsCore.Config.DefaultContraEdge, 0, increment, increment, 5);
                    }
                });
            }
        }

        [Command]
        public void CloseByLegOutCommand()
        {
            _ = Task.Run(() => LegOutCloser?.ClosePosition(Lcd));
        }

        [Command]
        public void CloseByAutoLegCommand()
        {
            _ = Task.Run(() => AutoLegCloser?.ClosePosition(Lcd));
        }

        [Command]
        public void CancelAnyCommand()
        {
            GammaScalpOrderResubmitOnCancel = false;
            CancelAsync();
            _log.Info("Cancel triggered by cancel any.");
            CancelContraAsync();
        }

        [Command]
        public void CancelAsync()
        {
            ResetSmartRoutes();
            CancelResting();
        }

        private void CancelResting()
        {
            _resubmitWhenReceivingCancelStatus = false;
            CanReplace = false;
            SubmitWithDelayEnabled = false;
            if ((MainResting || !IsBasketOrder) && !string.IsNullOrWhiteSpace(OrderId))
            {
                Task.Run(() => CancelMain());
            }
        }

        internal void InvokeLoopCommand(bool start)
        {
            LoopCommandEvent?.Invoke(start);
        }

        [Command]
        public void CancelSpeedTrader()
        {
            try
            {
                SubmitWithDelayEnabled = false;
                PermCloser?.Stop();
                Looper?.Stop();
                Tracker?.Stop();
                Closer?.Stop();
                CxlReplaceCloser?.Stop();
                Fisher?.Stop();
                SweepCloser?.Stop();
                LegOutCloser?.Stop();
                AutoLegCloser?.Stop();
                ThreeWayCloser?.Stop();
                StopLossManager?.Stop();
                StopOrderManager?.Stop();
                AutoCloseManager?.Stop();

                _log.Info("Cancel speed trader." +
                      ", Spread: " + SpreadId +
                      ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelSpeedTrader));
            }
        }

        [Command]
        public async Task CloseAsyncCommand()
        {
            try
            {
                if (IsCloseEnabled && !IsActive)
                {
                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition < 0))
                    {
                        Closer.StartCloser(lastFillPx: LastFillPx,
                                           qty: LastFillQty,
                                           closingEdge: .10,
                                           closeMaxLoss: .00,
                                           priceIncrement: .05,
                                           closeInterval: GetAutomationConfig().ContraFishInterval,
                                           manualClose: true);
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseAsyncCommand));
                ShowMessage(ex.Message, nameof(CloseAsyncCommand));
            }
        }

        [Command]
        public async Task CloseAllPositionsAsyncCommand()
        {
            try
            {
                if (!double.IsNaN(Mid) && !IsActive)
                {
                    int spreadPosition = _spreadPosition;

                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition < 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition > 0))
                    {
                        Fisher.StartFisher(basePrice: Mid,
                                           underlyingAtBase: UnderMid,
                                           qty: Math.Abs(spreadPosition),
                                           fishEdge: OmsCore.Config.CloseButtonEdge,
                                           fishMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseButtonPxIncrement,
                                           interval: OmsCore.Config.CloseButtonInterval,
                                           manual: true);
                    }
                    else if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition < 0))
                    {
                        Closer.StartCloser(lastFillPx: Mid,
                                           qty: Math.Abs(spreadPosition),
                                           closingEdge: OmsCore.Config.CloseButtonEdge,
                                           closeMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseButtonPxIncrement,
                                           closeInterval: OmsCore.Config.CloseButtonInterval,
                                           manualClose: true);
                    }
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseAsyncCommand));
                ShowMessage(ex.Message, "Close Positions");
            }
        }

        [Command]
        public async Task CloseAllPositionsFastAsyncCommand()
        {
            try
            {
                double stopPx = double.NaN;
                double edge = OmsCore.Config.CloseFastButtonEdge;
                double bestBid = Math.Max(BidIvEma, Bid);
                double bestAsk = Math.Min(AskIvEma, Ask);
                switch (OmsCore.Config.CloseFastEdgeType)
                {
                    case CloseFastEdgeType.Percent:
                        if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            double startPx = bestAsk - (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMin);
                            startPx = Math.Round(startPx, 2, MidpointRounding.AwayFromZero);
                            startPx = PriceNeedsPadding(startPx) ? PadForNickelOrDime(startPx, floor: false) : startPx;

                            stopPx = bestAsk - (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMax);
                            stopPx = Math.Round(stopPx, 2, MidpointRounding.AwayFromZero);
                            stopPx = PriceNeedsPadding(stopPx) ? PadForNickelOrDime(stopPx, floor: false) : stopPx;

                            edge = startPx - stopPx;
                        }
                        else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            double startPx = bestBid + (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMin);
                            startPx = Math.Round(startPx, 2, MidpointRounding.AwayFromZero);
                            startPx = PriceNeedsPadding(startPx) ? PadForNickelOrDime(startPx, floor: true) : startPx;

                            stopPx = bestBid + (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMax);
                            stopPx = Math.Round(stopPx, 2, MidpointRounding.AwayFromZero);
                            stopPx = PriceNeedsPadding(stopPx) ? PadForNickelOrDime(stopPx, floor: true) : stopPx;

                            edge = stopPx - startPx;
                        }
                        break;
                    case CloseFastEdgeType.Best:
                        double edgeStopPx = (bestBid + bestAsk) / 2;
                        double edgeToMid = OmsCore.Config.CloseFastButtonEdge;

                        double percentageStopPx = double.NaN;
                        double percentageEdge = double.NaN;
                        if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            double startPx = bestAsk - (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMin);
                            startPx = Math.Round(startPx, 2, MidpointRounding.AwayFromZero);
                            startPx = PriceNeedsPadding(startPx) ? PadForNickelOrDime(startPx, floor: false) : startPx;

                            percentageStopPx = bestAsk - (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMax);
                            percentageStopPx = Math.Round(percentageStopPx, 2, MidpointRounding.AwayFromZero);
                            percentageStopPx = PriceNeedsPadding(percentageStopPx) ? PadForNickelOrDime(percentageStopPx, floor: false) : percentageStopPx;

                            percentageEdge = startPx - percentageStopPx;

                            if (percentageStopPx < edgeStopPx)
                            {
                                stopPx = percentageStopPx;
                                edge = percentageEdge;
                            }
                            else if (percentageStopPx > edgeStopPx)
                            {
                                stopPx = edgeStopPx;
                                edge = edgeToMid;
                            }
                        }
                        else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            double startPx = bestBid + (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMin);
                            startPx = Math.Round(startPx, 2, MidpointRounding.AwayFromZero);
                            startPx = PriceNeedsPadding(startPx) ? PadForNickelOrDime(startPx, floor: true) : startPx;

                            percentageStopPx = bestBid + (Math.Abs(bestBid - bestAsk) * OmsCore.Config.CloseFastButtonEdgePercentageMax);
                            percentageStopPx = Math.Round(percentageStopPx, 2, MidpointRounding.AwayFromZero);
                            percentageStopPx = PriceNeedsPadding(percentageStopPx) ? PadForNickelOrDime(percentageStopPx, floor: true) : percentageStopPx;

                            percentageEdge = percentageStopPx - startPx;

                            if (percentageStopPx > edgeStopPx)
                            {
                                stopPx = percentageStopPx;
                                edge = percentageEdge;
                            }
                            else if (percentageStopPx < edgeStopPx)
                            {
                                stopPx = edgeStopPx;
                                edge = edgeToMid;
                            }
                        }
                        break;
                    case CloseFastEdgeType.Edge:
                        stopPx = (bestBid + bestAsk) / 2;
                        edge = OmsCore.Config.CloseFastButtonEdge;
                        break;
                }

                if (!double.IsNaN(stopPx) && edge >= 0 && !IsActive && _spreadPosition != 0)
                {
                    int spreadPosition = _spreadPosition;

                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition < 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition > 0))
                    {
                        Fisher.StartFisher(basePrice: stopPx,
                                           underlyingAtBase: UnderMid,
                                           qty: Math.Abs(spreadPosition),
                                           fishEdge: Math.Round(edge, 2, MidpointRounding.AwayFromZero),
                                           fishMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseFastButtonPxIncrement,
                                           interval: OmsCore.Config.CloseFastButtonInterval,
                                           manual: true);
                    }
                    else if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && spreadPosition < 0))
                    {
                        Closer.StartCloser(lastFillPx: stopPx,
                                           qty: Math.Abs(spreadPosition),
                                           closingEdge: Math.Round(edge, 2, MidpointRounding.AwayFromZero),
                                           closeMaxLoss: .00,
                                           priceIncrement: OmsCore.Config.CloseFastButtonPxIncrement,
                                           closeInterval: OmsCore.Config.CloseFastButtonInterval,
                                           manualClose: true);
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseAllPositionsFastAsyncCommand));
                ShowMessage(ex.Message, "Close Positions Fast");
            }
        }

        [Command]
        public void CancelContraAsync()
        {
            _contraUsingSmartRoute = false;
            _contraSmartRouteOverwatchTimer.Stop();
            _resubmitWhenReceivingCancelStatus = false;
            CanReplace = false;
            Task.Run(() => CancelContra());
        }

        [Command]
        public void CancelOrderFromDepthTableCommand(DepthItemModel depthItemModel)
        {
            try
            {
                if (depthItemModel != null)
                {
                    IOrder order = depthItemModel.Order;
                    if (order != null)
                    {
                        OmsCore.OrderClient.CancelOrder(new CancelRequest
                        {
                            OrderId = order.PermID,
                            Venue = order.Venue,
                            LocalId = LocalId,
                            PermId = order.PermID,
                            Account = Account,
                            UserId = order.UserId,
                            RiskCheckId = order.RiskCheckId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelOrderFromDepthTableCommand));
            }
        }

        [Command]
        public async void TypeChanged(object parameter)
        {
            try
            {
                if (parameter is null)
                {
                    throw new ArgumentNullException(nameof(parameter));
                }
                PreUpdate();
                if (parameter is TicketLegModel legModel)
                {
                    await legModel.OnTypeChange();
                }
                QuantityChanged(parameter);
                PostUpdate();

                if (_lastLoadedRoutingOrderType != GetOrderType())
                {
                    _ = UpdateAccountsAndRoutes();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TypeChanged));
            }
        }

        [Command]
        public async void PriceBoxIncrement(SpinEventArgs e)
        {
            try
            {
                ResetPriceLock();
                decimal increment = PriceIncrement;
                await SetPriceIncrementAsync(loadSymbol: false);
                if (IsSingleLeg)
                {
                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && !e.IsSpinUp && Price == 3) ||
                        (Side == ZeroPlus.Models.Data.Enums.Side.Sell && e.IsSpinUp && Price == 3))
                    {
                        Price -= (double)_defaultIncrement;
                        e.Handled = true;
                    }
                }
                UpdateTicketSide();
                UpdateTicketValues();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PriceBoxIncrement));
            }
        }

        [Command]
        public async void ContraPriceBoxIncrement(SpinEventArgs e)
        {
            try
            {
                ResetContraPriceLock();
                decimal increment = ContraTicketPriceIncrement;
                await SetPriceIncrementAsync(loadSymbol: false);
                if (IsSingleLeg)
                {
                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Sell && !e.IsSpinUp && ContraPrice == 3) ||
                        (Side == ZeroPlus.Models.Data.Enums.Side.Buy && e.IsSpinUp && ContraPrice == 3))
                    {
                        ContraPrice -= (double)_defaultIncrement;
                        e.Handled = true;
                    }
                }
                UpdateTicketSide();
                UpdateTicketValues();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ContraPriceBoxIncrement));
            }
        }

        protected void ResetPriceLock()
        {
            LockLowPrice = false;
            LockMidPrice = false;
            LockHighPrice = false;
        }

        protected void ResetContraPriceLock()
        {
            LockContraLowPrice = false;
            LockContraMidPrice = false;
            LockContraHighPrice = false;
        }

        [Command]
        public async void UpdatePrice()
        {
            try
            {
                await SetPriceIncrementAsync(loadSymbol: false);
                UpdateTicketSide();
                UpdateTicketValues();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdatePrice));
            }
        }

        [Command]
        public void ShowFeeCalculatorCommand()
        {
            FeesEstimateView view = new()
            {
                DataContext = this
            };
            view.ShowDialog();
        }

        [Command]
        public void OpenInGammaScalperCommand()
        {
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    GammaScalpingModuleView view = new();
                    if (view.DataContext is GammaScalpingModuleViewModel viewModel)
                    {
                        UnderlyingPositionModel underlyingModel = viewModel.AddSymbol(Underlying, HedgeUnderlying, HedgeMultiplier);
                        viewModel.Account = OmsCore.Config.DefaultAccount;
                        _ = viewModel.OrderTicket.LoadFromTicketAsync(this);
                        view.Show();
                    }
                }));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInGammaScalperCommand));
            }
        }

        [Command]
        public void OpenInLiveChartCommand()
        {
            try
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    LiveChartView window = new();
                    LiveChartViewModel viewModel = (LiveChartViewModel)window.DataContext;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                    viewModel.Ready += LoadChart;

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OpenInLiveChartCommand));
            }
        }

        private void LoadChart(IModuleViewModel module)
        {
            module.Ready -= LoadChart;
            if (module is LiveChartViewModel viewModel)
            {
                viewModel.Symbol = GetTosFromLegs(Legs.ToList());
                viewModel.SearchCommand();
            }
        }

        [Command]
        public void UpdateFeesEstimate()
        {
            FeesEstimate = GetTotalFeesForTicket("", LastExchange);
        }

        public static Side EvaluateSide(string spreadType, List<TicketLegModel> legs)
        {
            if (legs.Count == 1)
            {
                Side side = legs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                return side;
            }
            else
            {
                switch (spreadType)
                {
                    case "CALL 1X2":
                    case "CALL 1X3":
                    case "CALL 2X3":
                    case "CALL VERTICAL":
                    case "CALL 1X3X3X1":
                    case "CALL CONDOR":
                    case "PUT CONDOR":
                    case "STRADDLE":
                    case "STRANGLE":
                    case "PUT SPREAD VS CALL":
                    case "COVERED STRADDLE":
                        return legs.OrderBy(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    case "BOX SPREAD":
                    case "IRON BUTTERFLY":
                    case "IRON CONDOR":
                    case "CALL SPREAD VS PUT":
                    case "MARRIED STRADDLE":
                        return legs.OrderBy(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    case "PUT 1X2":
                    case "PUT 1X3":
                    case "PUT 2X3":
                    case "PUT VERTICAL":
                    case "PUT 1X3X3X1":
                        return legs.OrderByDescending(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    case "CALL BUTTERFLY":
                    case "PUT BUTTERFLY":
                    case "CALL SKEWED BUTTERFLY":
                    case "PUT SKEWED BUTTERFLY":
                    case "CALL ONE THREE TWO":
                    case "PUT ONE THREE TWO":
                    case "CALL TWO THREE ONE":
                    case "PUT TWO THREE ONE":
                        return legs.OrderBy(x => x.Ratio).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    case "CALL CALENDAR":
                    case "PUT CALENDAR":
                    case "CALL DIAGONAL":
                    case "PUT DIAGONAL":
                    case "CALL TRIAGONAL":
                    case "PUT TRIAGONAL":
                        return legs.OrderBy(x => x.ExpirationInfo.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    case "CALL CALENDAR FLY":
                    case "PUT CALENDAR FLY":
                    case "CALL SKEWED CALENDAR FLY":
                    case "PUT SKEWED CALENDAR FLY":
                        return legs.OrderBy(x => x.ExpirationInfo.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? ZeroPlus.Models.Data.Enums.Side.Buy
                            : ZeroPlus.Models.Data.Enums.Side.Sell;
                    case "REVERSAL":
                    case "CONVERSION":
                        return legs.Where(x => x.Type == Types.PUT.ToString()).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy;
                    default:
                        return false ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                }
            }
        }

        private bool UpdateTicketSide()
        {
            bool reversed = false;
            if ((TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical) ||
                (TicketStyle == OrderTicketStyle.Combined && double.IsNaN(Price)))
            {
                bool isValid = false;
                if (IsSingleLeg)
                {
                    SubmitText = "Send";
                    ContraSubmitText = "Send";
                    isValid = true;
                    IsSellOrder = Legs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                }
                else if (Legs.Count > 1)
                {
                    switch (SpreadType)
                    {
                        case "CALL 1X2":
                        case "CALL 1X3":
                        case "CALL 2X3":
                        case "CALL VERTICAL":
                        case "CALL 1X3X3X1":
                        case "CALL CONDOR":
                        case "PUT CONDOR":
                        case "STRADDLE":
                        case "STRANGLE":
                            isValid = true;
                            IsSellOrder = Legs.OrderBy(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                            break;
                        case "IRON BUTTERFLY":
                        case "IRON CONDOR":
                            isValid = true;
                            IsSellOrder = Legs.OrderBy(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                            break;
                        case "PUT 1X2":
                        case "PUT 1X3":
                        case "PUT 2X3":
                        case "PUT VERTICAL":
                        case "PUT 1X3X3X1":
                            isValid = true;
                            IsSellOrder = Legs.OrderByDescending(x => x.Strike).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                            break;
                        case "CALL BUTTERFLY":
                        case "PUT BUTTERFLY":
                        case "CALL SKEWED BUTTERFLY":
                        case "PUT SKEWED BUTTERFLY":
                        case "CALL ONE THREE TWO":
                        case "PUT ONE THREE TWO":
                        case "CALL TWO THREE ONE":
                        case "PUT TWO THREE ONE":
                            isValid = true;
                            IsSellOrder = Legs.OrderBy(x => x.Ratio).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                            break;
                        case "CALL CALENDAR":
                        case "PUT CALENDAR":
                        case "CALL DIAGONAL":
                        case "PUT DIAGONAL":
                        case "CALL TRIAGONAL":
                        case "PUT TRIAGONAL":
                            isValid = true;
                            IsSellOrder = Legs.OrderBy(x => x.ExpirationInfo.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                            break;
                        case "CALL CALENDAR FLY":
                        case "PUT CALENDAR FLY":
                            isValid = true;
                            IsSellOrder = Legs.OrderBy(x => x.ExpirationInfo.Expiration).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                            break;
                        case "REVERSAL":
                        case "CONVERSION":
                            IsSellOrder = Legs.Where(x => x.Type == Types.PUT.ToString()).FirstOrDefault()?.Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                            break;
                        case "INVALID":
                        case "CUSTOM":
                            isValid = true;
                            IsSellOrder = false;
                            break;
                        case "CALL COVERED":
                        case "PUT COVERED":
                        case "PUT PROTECTIVE":
                            break;
                    }

                    SubmitText = double.IsNaN(Price) ? "" : "Submit for " + Price;
                    ContraSubmitText = double.IsNaN(ContraPrice) ? "" : "Submit for " + ContraPrice;
                }

                if (!isValid)
                {
                    IsSellOrder = false;
                }
                else if (IsSellOrder)
                {
                    if (OmsCore.Config.AutoReverseOrdersToBuySide && EdgeProjector == null)
                    {
                        Reverse();
                        reversed = true;
                    }
                    else if (!ReversePrompted)
                    {
                        bool ok = GetVerification("You have a reversed ticket. Would you like to reverse sides?", "Spread Reversed");
                        if (ok)
                        {
                            ReversePrompted = false;
                            Reverse();
                            reversed = true;
                        }
                        else
                        {
                            ReversePrompted = true;
                        }
                    }
                }

                IsContraSellOrder = !IsSellOrder;
            }
            else
            {
                string submitText = "";
                bool isSellOrder = false;
                if (IsSingleLeg)
                {
                    submitText = "Send";
                    isSellOrder = Legs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                }
                else if (Legs.Count > 1)
                {
                    if (double.IsNaN(Price))
                    {
                        submitText = "";
                    }
                    else if (Price == 0)
                    {
                        submitText = "Submit for " + Price + " EVEN";
                        isSellOrder = false;
                    }
                    else if (Price < 0)
                    {
                        submitText = "Submit @ " + Price + " CR";
                        isSellOrder = true;
                    }
                    else if (Price > 0)
                    {
                        submitText = "Submit for " + Price + " DR";
                        isSellOrder = false;
                    }
                }

                if (SubmitText != submitText)
                {
                    SubmitText = submitText;
                }

                if (IsSellOrder != isSellOrder)
                {
                    IsSellOrder = isSellOrder;
                }

                submitText = "";
                if (IsSingleLeg)
                {
                    submitText = "Send";
                }
                else if (Legs.Count > 1)
                {
                    if (double.IsNaN(ContraPrice))
                    {
                        submitText = "";
                    }
                    else if (ContraPrice == 0)
                    {
                        submitText = "Submit for " + ContraPrice + " EVEN";
                    }
                    else if (ContraPrice < 0)
                    {
                        submitText = "Submit @ " + ContraPrice + " CR";
                    }
                    else if (ContraPrice > 0)
                    {
                        submitText = "Submit for " + ContraPrice + " DR";
                    }
                }

                if (ContraSubmitText != submitText)
                {
                    ContraSubmitText = submitText;
                }

                if (IsContraSellOrder != !isSellOrder)
                {
                    IsContraSellOrder = !isSellOrder;
                }
            }
            return reversed;
        }

        [Command]
        public async Task ThreeWay(object parameter)
        {
            try
            {
                if (!OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                {
                    return;
                }
                Task<List<Option>> getOptionsTask = OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                List<Option> options = await getOptionsTask;
                if (options.Count <= 0 || !CanCreateThreeWay())
                {
                    return;
                }

                TicketLegModel[] legs = Legs.OrderBy(x => x.Ratio).ToArray();

                TicketLegModel swappedLeg = legs[0];

                string type = Legs.First().Type;
                if (Legs.Select(x => x.Ratio).Distinct().Count() == 1)
                {
                    ThreeWayPreference threeWayPreference = ThreeWayOtmOverride ? ThreeWayPreference.OTM : ThreeWayPreference.ITM;
                    switch (threeWayPreference)
                    {
                        case ThreeWayPreference.ITM when type == Types.CALL.ToString():
                        case ThreeWayPreference.OTM when type == Types.PUT.ToString():
                            swappedLeg = legs.OrderBy(x => x.Strike).First();
                            break;

                        case ThreeWayPreference.OTM when type == Types.CALL.ToString():
                        case ThreeWayPreference.ITM when type == Types.PUT.ToString():
                            swappedLeg = legs.OrderByDescending(x => x.Strike).First();
                            break;
                    }
                }

                Option swapOption = options.Where(x => x.Type.ToString() == swappedLeg.Type)
                                        .Where(x => x.Expiration == swappedLeg.ExpirationInfo.Expiration)
                                        .Where(x => !Legs.Any(leg => x.Strike == leg.Strike))
                                        .OrderBy(x => Math.Abs(x.Strike - swappedLeg.Strike.Strike))
                                        .FirstOrDefault();

                if (swapOption == null)
                {
                    return;
                }

                List<TicketLegModel> secondTicketLegs = new();

                foreach (TicketLegModel leg in Legs)
                {
                    if (leg.Symbol == swappedLeg.Symbol)
                    {
                        secondTicketLegs.Add(new TicketLegModel(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                        {
                            Symbol = swapOption.OptionSymbol,
                            Quantity = swappedLeg.Ratio,
                            Ratio = swappedLeg.Ratio,
                            Type = swappedLeg.Type.ToString(),
                            Side = LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                    }
                    else
                    {
                        secondTicketLegs.Add(new TicketLegModel(OmsCore, leg.Underlying, leg.Account, null, _portfolioManagerModel)
                        {
                            Symbol = leg.Symbol,
                            Quantity = leg.Ratio,
                            Ratio = leg.Ratio,
                            Type = leg.Type.ToString(),
                            Side = LastTradedContra ? leg.Side : leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                    }
                }
                ;

                List<TicketLegModel> thirdTicketLegs = new()
                {
                    new TicketLegModel(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                    {
                        Symbol = swappedLeg.Symbol,
                        Quantity = swappedLeg.Ratio,
                        Ratio = swappedLeg.Ratio,
                        Type = swappedLeg.Type.ToString(),
                        Side = LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    },
                    new TicketLegModel(OmsCore, legs[1].Underlying, legs[1].Account, null, _portfolioManagerModel)
                    {
                        Symbol = swapOption.OptionSymbol,
                        Quantity = swappedLeg.Ratio,
                        Ratio = swappedLeg.Ratio,
                        Type = swappedLeg.Type.ToString(),
                        Side = !LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    }
                };

                ManualResetEventSlim secondTicketReadyEvent = new(false);
                ManualResetEventSlim thirdTicketReadyEvent = new(false);
                ComplexOrderTicketViewModel secondTicketViewModel = null;
                EdgeProjectorModel edgeProjector = new(_portfolioManagerModel);
                edgeProjector.AddTicket(this, Ticket.First, reverse: LastTradedContra);
                SetEdgeProjector(edgeProjector);
                SuggestTradingMain = !LastTradedContra;
                SuggestTradingContra = LastTradedContra;

                Thread secondTicket = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                                    new DispatcherSynchronizationContext(
                                        Dispatcher.CurrentDispatcher));
                    Window window = null;
                    try
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                        }

                        secondTicketViewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        secondTicketViewModel.InstanceMode = InstanceMode;
                        secondTicketViewModel.BrokerOverride = BrokerOverride;
                        secondTicketViewModel.SetDispatcher(window.Dispatcher);
                        secondTicketViewModel.SetEdgeProjector(edgeProjector);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        secondTicketViewModel.ReversePrompted = true;
                        _ = secondTicketViewModel.LoadFromLegsAsync(secondTicketLegs).ContinueWith(x =>
                        {
                            if (window is CombinedOrderTicketView && secondTicketViewModel.IsSellOrder)
                            {
                                secondTicketViewModel.Reverse();
                                edgeProjector.AddTicket(secondTicketViewModel, Ticket.Second, reverse: true);
                                secondTicketViewModel.SuggestTradingMain = false;
                                secondTicketViewModel.SuggestTradingContra = true;
                            }
                            else
                            {
                                edgeProjector.AddTicket(secondTicketViewModel, Ticket.Second);
                                secondTicketViewModel.SuggestTradingMain = true;
                                secondTicketViewModel.SuggestTradingContra = false;
                            }
                            secondTicketReadyEvent.Set();
                        });
                        if (parameter is not null and object[] values)
                        {
                            try
                            {
                                double width = (double)values[0];
                                double height = (double)values[1];
                                double left = (double)values[2];
                                double top = (double)values[3];
                                window.Loaded += (s, e) =>
                                {
                                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                                    window.Width = width;
                                    window.Height = height;
                                    window.Top = top;
                                    window.Left = left + width;
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(ThreeWay));
                            }
                        }

                        window.Show();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ThreeWay));
                        window?.Close();
                        ShowMessage("Error occured when creating a 3 way ticket\nCreate a new one.", "ZeroPlus OMS");
                    }
                });
                secondTicket.SetApartmentState(ApartmentState.STA);
                Thread thirdTicket = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    try
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                        }

                        ComplexOrderTicketViewModel thirdTicketViewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        thirdTicketViewModel.InstanceMode = InstanceMode;
                        thirdTicketViewModel.BrokerOverride = BrokerOverride;
                        thirdTicketViewModel.SetDispatcher(window.Dispatcher);
                        thirdTicketViewModel.SetEdgeProjector(edgeProjector);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        thirdTicketViewModel.ReversePrompted = true;
                        _ = thirdTicketViewModel.LoadFromLegsAsync(thirdTicketLegs).ContinueWith(x =>
                        {
                            if (window is CombinedOrderTicketView && thirdTicketViewModel.IsSellOrder)
                            {
                                thirdTicketViewModel.Reverse();
                                edgeProjector.AddTicket(thirdTicketViewModel, Ticket.Third, reverse: true);
                                thirdTicketViewModel.SuggestTradingMain = false;
                                thirdTicketViewModel.SuggestTradingContra = true;
                            }
                            else
                            {
                                edgeProjector.AddTicket(thirdTicketViewModel, Ticket.Third);
                                thirdTicketViewModel.SuggestTradingMain = true;
                                thirdTicketViewModel.SuggestTradingContra = false;
                            }
                            thirdTicketReadyEvent.Set();
                        });

                        Task.WhenAll(new Task[] { Task.Run(() => secondTicketReadyEvent.Wait()), Task.Run(() => thirdTicketReadyEvent.Wait()) }).ContinueWith(t =>
                        {
                            SetPriceForThreeWay(secondTicketViewModel, thirdTicketViewModel);
                        });

                        if (parameter is not null and object[] values)
                        {
                            try
                            {
                                double width = (double)values[0];
                                double height = (double)values[1];
                                double left = (double)values[2];
                                double top = (double)values[3];
                                window.Loaded += (s, e) =>
                                {
                                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                                    window.Width = width;
                                    window.Height = height;
                                    window.Top = top;
                                    window.Left = left + (2 * width);
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(ThreeWay));
                            }
                        }

                        window.Show();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ThreeWay));
                        window?.Close();
                        ShowMessage("Error occured when creating a 3 way ticket\nCreate a new one.", "ZeroPlus OMS");
                    }
                });
                thirdTicket.SetApartmentState(ApartmentState.STA);

                secondTicket.Start();
                thirdTicket.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ThreeWay));
            }
        }

        [Command]
        public void LegOutCommand(object parameter)
        {
            try
            {
                if (!OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
                {
                    return;
                }

                EdgeProjectorModel edgeProjector = new(_portfolioManagerModel)
                {
                    TicketsCount = Legs.Count + 1,
                };
                edgeProjector.AddTicket(this, Ticket.First, reverse: LastTradedContra || (IsSingleLeg && IsSellOrder));
                SetEdgeProjector(edgeProjector);
                SuggestTradingMain = !LastTradedContra;
                SuggestTradingContra = LastTradedContra;

                for (int i = 0; i < Legs.Count; i++)
                {
                    TicketLegModel leg = Legs[i];
                    List<TicketLegModel> newLegs = new()
                    {
                        new(OmsCore, leg.Underlying, leg.Account, null, _portfolioManagerModel)
                        {
                            Symbol = leg.Symbol,
                            Quantity = leg.Ratio,
                            Ratio = leg.Ratio,
                            Type = leg.Type.ToString(),
                            Side = LastTradedContra ? leg.Side : leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        }
                    };
                    int index = i;
                    Thread legTicket = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        Window window = null;
                        try
                        {
                            switch (OmsCore.Config.DefaultOrderTicketStyle)
                            {
                                case OrderTicketStyle.Complex:
                                    window = new ComplexOrderTicketView
                                    {
                                        Contra = true
                                    };
                                    break;
                                case OrderTicketStyle.Combined:
                                    window = new CombinedOrderTicketView
                                    {
                                        Clone = true
                                    };
                                    break;
                            }

                            ComplexOrderTicketViewModel legTicketViewModel = (ComplexOrderTicketViewModel)window.DataContext;
                            legTicketViewModel.InstanceMode = InstanceMode;
                            legTicketViewModel.BrokerOverride = BrokerOverride;
                            legTicketViewModel.SetDispatcher(window.Dispatcher);
                            legTicketViewModel.SetEdgeProjector(edgeProjector);

                            window.Dispatcher.UnhandledException += (s, e) =>
                            {
                                _log.Error(e.Exception, "DispatcherUnhandledException");
                                e.Handled = true;
                            };

                            window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                            legTicketViewModel.ReversePrompted = true;
                            _ = legTicketViewModel.LoadFromLegsAsync(newLegs).ContinueWith(x =>
                            {
                                if (window is CombinedOrderTicketView && legTicketViewModel.IsSellOrder)
                                {
                                    legTicketViewModel.Reverse();
                                    edgeProjector.AddTicket(legTicketViewModel, Ticket.Third, reverse: true);
                                    legTicketViewModel.SuggestTradingMain = false;
                                    legTicketViewModel.SuggestTradingContra = true;
                                }
                                else
                                {
                                    edgeProjector.AddTicket(legTicketViewModel, Ticket.Third, reverse: legTicketViewModel.IsSingleLeg && legTicketViewModel.IsSellOrder);
                                    legTicketViewModel.SuggestTradingMain = true;
                                    legTicketViewModel.SuggestTradingContra = false;
                                }
                            });

                            if (parameter is not null and object[] values)
                            {
                                try
                                {
                                    double width = (double)values[0];
                                    double height = (double)values[1];
                                    double left = (double)values[2];
                                    double top = (double)values[3];
                                    window.Loaded += (s, e) =>
                                    {
                                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                                        window.Width = width;
                                        window.Height = height;
                                        window.Top = top;
                                        window.Left = left + ((index + 1) * width);
                                    };
                                }
                                catch (Exception ex)
                                {
                                    _log.Error(ex, nameof(ThreeWay));
                                }
                            }

                            window.Show();
                            Dispatcher.Run();
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex, nameof(ThreeWay));
                            window?.Close();
                            ShowMessage("Error occured when creating a leg out ticket\nCreate a new one.", "ZeroPlus OMS");
                        }
                    });
                    legTicket.SetApartmentState(ApartmentState.STA);
                    legTicket.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ThreeWay));
            }
        }

        [Command]
        public void UpdateStockPositions()
        {
            try
            {
                if (!IsBasketOrder || BasketSettings.HedgeAutoEnabled)
                {
                    SetRequiredHedgeQty();

                    if (RequiredStocks == 0 && HedgedStocks == 0)
                    {
                        HedgeSuggestion = HedgeSuggestion.None;
                        EstHedgeCost = double.NaN;
                        AdjustedPriceAtHedge = double.NaN;
                        StockPriceAtHedge = double.NaN;
                        LastHedgePrice = double.NaN;
                        LastOptionPnlOnFill = double.NaN;
                        LastHedgePnlOnFill = double.NaN;
                        LastTotalPnlOnFill = double.NaN;
                        LastEdgeToMarketOnFill = double.NaN;
                        LiveLastTradeOptionPnl = double.NaN;
                        LiveLastTradeHedgePnl = double.NaN;
                        LiveLastTradeTotalPnl = double.NaN;
                        LiveLastTradeEdgeToMarket = double.NaN;
                    }

                    UpdateStockHedgeUnrealizedPnl();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateStockPositions));
            }
        }

        public virtual bool CheckForAutoHedge(double attemptedEdge, bool lastAttempt = false)
        {
            return false;
        }

        public double CalculateAttemptedEdgeOnClose(double newPrice)
        {
            double attemptedEdge = double.NaN;

            if (IsSingleLeg)
            {
                if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    attemptedEdge = LastFillPx - newPrice;
                }
                else
                {
                    attemptedEdge = newPrice - LastFillPx;
                }
            }
            else
            {
                if (LastFillPx < 0 && newPrice > 0)
                {
                    attemptedEdge = Math.Abs(LastFillPx) - newPrice;
                }
                else if (newPrice < 0 && LastFillPx > 0)
                {
                    attemptedEdge = Math.Abs(newPrice) - LastFillPx;
                }
                else if (newPrice < 0 && LastFillPx < 0)
                {
                    attemptedEdge = Math.Abs(newPrice + LastFillPx);
                }
            }

            SetOrderDetailTag("Attempted Edge Close Est", attemptedEdge.ToString());
            return Math.Round(attemptedEdge, 2);
        }

        private void SetRequiredHedgeQty()
        {
            object hedgeLock = AcquireSpreadHedgeLock();
            if (hedgeLock != null)
            {
                lock (hedgeLock)
                {
                    TotalStocks = CalculateRequiredHedgeQty(TraderSpreadPosition);
                    StockHedgeQty = RequiredStocks = (int)Math.Round((double)(TotalStocks * StockHedgePercent) - HedgedStocks - SubmittedStocks);
                }
                _log.Info($"Set Hedge Qty. " +
                    $"Id: {SpreadId}, " +
                    $"Pos: {TraderSpreadPosition}, " +
                    $"Total: {TotalStocks}, " +
                    $"Req: {RequiredStocks}, " +
                    $"Percent: {StockHedgePercent}, " +
                    $"Hedged: {HedgedStocks}, " +
                    $"Submitted: {SubmittedStocks}");
            }
        }

        private bool TryGetLcdPosition(out int lcdPosition)
        {
            lcdPosition = 0;
            try
            {
                if (Legs.Count == 1)
                {
                    lcdPosition = Legs.First().NetQty;
                }
                else if (Legs.Count > 1)
                {
                    if ((Legs.Count(x => (x.NetQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.NetQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == Legs.Count) ||
                        (Legs.Count(x => (x.NetQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.NetQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == Legs.Count))
                    {
                        int divisor = Legs.Min(x => Math.Abs(x.NetQty));
                        TicketLegModel sample = Legs.First();
                        if (((sample.NetQty < 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (sample.NetQty > 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) ^ Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            lcdPosition = -divisor;
                        }
                        else
                        {
                            lcdPosition = divisor;
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public int CalculateRequiredHedgeQty(int position)
        {
            double spreadDelta = -TotalDelta;
            if (Legs.Count > 1 && Side != ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                spreadDelta = TotalDelta;
            }

            double tempTotalStocks = position * spreadDelta * Multiplier;
            if (double.IsNaN(tempTotalStocks) || double.IsInfinity(tempTotalStocks))
            {
                tempTotalStocks = 0;
            }

            int requiredTotalStocks = (int)Math.Round(tempTotalStocks * HedgeMultiplier);
            return requiredTotalStocks;
        }

        private void CheckForHedgeAutoFlatten()
        {
            if (RequiredStocks != 0 && HedgedStocks != 0)
            {
                if (!IsBasketOrder && !CanNotHedge)
                {
                    if (OmsCore.Config.AutoFlattenHedgeV2 && CheckHedgingInstance())
                    {
                        if (OmsCore.Config.AutoHedgeWhenAddingPositionToHedgedSpread || Math.Abs(RequiredStocks + HedgedStocks) <= Math.Abs(HedgedStocks))
                        {
                            object hedgeLock = AcquireSpreadHedgeLock();
                            lock (hedgeLock)
                            {
                                UpdateStockPositions();
                                HedgeWithStockAsync(RequiredStocks);
                            }
                        }
                    }
                }
            }
        }

        private void UpdateHedgeSymbol()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Underlying))
                {
                    if (OmsCore.Config.BasketHedgeLookupMap.TryGetValue(Underlying, out Tuple<string, double> swap) && swap != null && swap.Item1 != null)
                    {
                        HedgeUnderlying = swap.Item1;
                        HedgeMultiplier = Math.Abs(swap.Item2);
                    }
                    else
                    {
                        HedgeUnderlying = Underlying;
                        HedgeMultiplier = 1;
                    }

                    OmsCore.QuoteClient.Subscribe(HedgeUnderlying, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(HedgeUnderlying, SubscriptionFieldType.Ask, this);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateHedgeSymbol));
                HedgeUnderlying = Underlying;
                HedgeMultiplier = 0;
            }
        }

        internal async Task ThreeWayFromRequestAsync(OpenTicketRequest openTicketRequest, object[] windowParameter, Dominator dominator)
        {
            try
            {
                if (IsSellOrder)
                {
                    Reverse();
                    ReversePrompted = false;
                }
                LastTradedContra = openTicketRequest.Side?.ToUpper() == "SELL";
                if (!LastTradedContra)
                {
                    AveragePrice = openTicketRequest.Price;
                    BestAveragePrice = openTicketRequest.Price;
                    ContraAveragePrice = openTicketRequest.ContraPrice;
                    LastMainUnderMidAtFill = openTicketRequest.UnderPrice;
                    LastMainUnderMidAtBestFill = openTicketRequest.UnderPrice;
                    LastContraUnderMidAtFill = openTicketRequest.ContraUnderPrice;
                }
                else
                {

                    AveragePrice = openTicketRequest.ContraPrice;
                    BestAveragePrice = openTicketRequest.ContraPrice;
                    ContraAveragePrice = openTicketRequest.Price;
                    LastMainUnderMidAtFill = openTicketRequest.ContraUnderPrice;
                    LastMainUnderMidAtBestFill = openTicketRequest.ContraUnderPrice;
                    LastContraUnderMidAtFill = openTicketRequest.UnderPrice;
                }

                DeltaAdjPriceAsync();

                Task<List<Option>> getOptionsTask = OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                List<Option> options = await getOptionsTask;
                if (options.Count <= 0 || !CanCreateThreeWay())
                {
                    return;
                }

                TicketLegModel[] legs = Legs.OrderBy(x => x.Ratio).ToArray();

                TicketLegModel swappedLeg = legs[0];

                string type = Legs.First().Type;
                if (Legs.Select(x => x.Ratio).Distinct().Count() == 1)
                {
                    switch (OmsCore.Config.ThreeWayPreference)
                    {
                        case ThreeWayPreference.ITM when type == Types.CALL.ToString():
                        case ThreeWayPreference.OTM when type == Types.PUT.ToString():
                            swappedLeg = legs.OrderBy(x => x.Strike).First();
                            break;

                        case ThreeWayPreference.OTM when type == Types.CALL.ToString():
                        case ThreeWayPreference.ITM when type == Types.PUT.ToString():
                            swappedLeg = legs.OrderByDescending(x => x.Strike).First();
                            break;
                    }
                }

                Option swapOption = options.Where(x => x.Type.ToString() == swappedLeg.Type)
                                        .Where(x => x.Expiration == swappedLeg.ExpirationInfo.Expiration)
                                        .Where(x => !Legs.Any(leg => x.Strike == leg.Strike))
                                        .OrderBy(x => Math.Abs(x.Strike - swappedLeg.Strike.Strike))
                                        .FirstOrDefault();

                if (swapOption == null)
                {
                    return;
                }

                List<TicketLegModel> secondTicketLegs = new();

                foreach (TicketLegModel leg in Legs)
                {
                    if (leg.Symbol == swappedLeg.Symbol)
                    {
                        secondTicketLegs.Add(new TicketLegModel(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                        {
                            Symbol = swapOption.OptionSymbol,
                            Quantity = swappedLeg.Ratio,
                            Ratio = swappedLeg.Ratio,
                            Type = swappedLeg.Type.ToString(),
                            Side = LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                    }
                    else
                    {
                        secondTicketLegs.Add(new TicketLegModel(OmsCore, leg.Underlying, leg.Account, null, _portfolioManagerModel)
                        {
                            Symbol = leg.Symbol,
                            Quantity = leg.Ratio,
                            Ratio = leg.Ratio,
                            Type = leg.Type.ToString(),
                            Side = LastTradedContra ? leg.Side : leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                        });
                    }
                }

                List<TicketLegModel> thirdTicketLegs = new()
                {
                    new TicketLegModel(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                    {
                        Symbol = swappedLeg.Symbol,
                        Quantity = swappedLeg.Ratio,
                        Ratio = swappedLeg.Ratio,
                        Type = swappedLeg.Type.ToString(),
                        Side = LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    },
                    new TicketLegModel(OmsCore, legs[1].Underlying, legs[1].Account, null, _portfolioManagerModel)
                    {
                        Symbol = swapOption.OptionSymbol,
                        Quantity = swappedLeg.Ratio,
                        Ratio = swappedLeg.Ratio,
                        Type = swappedLeg.Type.ToString(),
                        Side = !LastTradedContra ? swappedLeg.Side : swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    }
                };

                ManualResetEventSlim secondTicketReadyEvent = new(false);
                ManualResetEventSlim thirdTicketReadyEvent = new(false);
                ComplexOrderTicketViewModel secondTicketViewModel = null;
                EdgeProjectorModel edgeProjector = new(_portfolioManagerModel);
                edgeProjector.AddTicket(this, Ticket.First, reverse: LastTradedContra);
                SetEdgeProjector(edgeProjector);
                SuggestTradingMain = !LastTradedContra;
                SuggestTradingContra = LastTradedContra;

                Thread secondTicket = new(async () =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    try
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                        }

                        secondTicketViewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        secondTicketViewModel.SetDispatcher(window.Dispatcher);
                        secondTicketViewModel.SetEdgeProjector(edgeProjector);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        secondTicketViewModel.ReversePrompted = true;

                        secondTicketViewModel.OrderClosedUpdateEvent += (order, status, _) => dominator.HandleOrderUpdate(openTicketRequest.Symbol, order, status);
                        _ = secondTicketViewModel.LoadFromLegsAsync(secondTicketLegs).ContinueWith(x =>
                        {
                            if (window is CombinedOrderTicketView && secondTicketViewModel.IsSellOrder)
                            {
                                secondTicketViewModel.Reverse();
                                edgeProjector.AddTicket(secondTicketViewModel, Ticket.Second, reverse: true);
                                secondTicketViewModel.SuggestTradingMain = false;
                                secondTicketViewModel.SuggestTradingContra = true;
                            }
                            else
                            {
                                edgeProjector.AddTicket(secondTicketViewModel, Ticket.Second);
                                secondTicketViewModel.SuggestTradingMain = true;
                                secondTicketViewModel.SuggestTradingContra = false;
                            }

                            secondTicketReadyEvent.Set();
                        });
                        if (windowParameter is not null and object[] values)
                        {
                            try
                            {
                                double width = (double)values[0];
                                double height = (double)values[1];
                                double left = (double)values[2];
                                double top = (double)values[3];
                                window.Loaded += (s, e) =>
                                {
                                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                                    window.Width = width;
                                    window.Height = height;
                                    window.Top = top;
                                    window.Left = left + width;
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(ThreeWay));
                            }
                        }

                        window.Show();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ThreeWayFromRequestAsync));
                        window?.Close();
                        await ThreeWayFromRequestAsync(openTicketRequest, windowParameter, dominator);
                        return;
                    }
                });
                secondTicket.SetApartmentState(ApartmentState.STA);
                Thread thirdTicket = new(async () =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    try
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView
                                {
                                    Clone = true
                                };
                                break;
                        }

                        ComplexOrderTicketViewModel thirdTicketViewModel = (ComplexOrderTicketViewModel)window.DataContext;
                        thirdTicketViewModel.SetDispatcher(window.Dispatcher);

                        thirdTicketViewModel.SetEdgeProjector(edgeProjector);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        thirdTicketViewModel.ReversePrompted = true;
                        thirdTicketViewModel.OrderClosedUpdateEvent += (order, status, _) => dominator.HandleOrderUpdate(openTicketRequest.Symbol, order, status);
                        _ = thirdTicketViewModel.LoadFromLegsAsync(thirdTicketLegs).ContinueWith(x =>
                        {
                            if (window is CombinedOrderTicketView && thirdTicketViewModel.IsSellOrder)
                            {
                                thirdTicketViewModel.Reverse();
                                edgeProjector.AddTicket(thirdTicketViewModel, Ticket.Third, reverse: true);
                                thirdTicketViewModel.SuggestTradingMain = false;
                                thirdTicketViewModel.SuggestTradingContra = true;
                            }
                            else
                            {
                                edgeProjector.AddTicket(thirdTicketViewModel, Ticket.Third);
                                thirdTicketViewModel.SuggestTradingMain = true;
                                thirdTicketViewModel.SuggestTradingContra = false;
                            }
                            thirdTicketReadyEvent.Set();
                        });

                        if (windowParameter is not null and object[] values)
                        {
                            try
                            {
                                double width = (double)values[0];
                                double height = (double)values[1];
                                double left = (double)values[2];
                                double top = (double)values[3];
                                window.Loaded += (s, e) =>
                                {
                                    window.WindowStartupLocation = WindowStartupLocation.Manual;
                                    window.Width = width;
                                    window.Height = height;
                                    window.Top = top;
                                    window.Left = left + (2 * width);
                                };
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex, nameof(ThreeWay));
                            }
                        }

                        _ = Task.WhenAll(new Task[] { Task.Run(() => secondTicketReadyEvent.Wait()), Task.Run(() => thirdTicketReadyEvent.Wait()) }).ContinueWith(t =>
                        {
                            if (!string.IsNullOrWhiteSpace(openTicketRequest.Route))
                            {
                                if (SuggestTradingMain)
                                {
                                    Route = openTicketRequest.Route;
                                }
                                else if (SuggestTradingContra)
                                {
                                    ContraRoute = openTicketRequest.Route;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(openTicketRequest.ClosingRoute))
                            {
                                if (secondTicketViewModel.SuggestTradingMain)
                                {
                                    secondTicketViewModel.Route = openTicketRequest.ClosingRoute;
                                }
                                else if (secondTicketViewModel.SuggestTradingContra)
                                {
                                    secondTicketViewModel.ContraRoute = openTicketRequest.ClosingRoute;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(openTicketRequest.FinishingRoute))
                            {
                                if (thirdTicketViewModel.SuggestTradingMain)
                                {
                                    thirdTicketViewModel.Route = openTicketRequest.FinishingRoute;
                                }
                                else if (thirdTicketViewModel.SuggestTradingContra)
                                {
                                    thirdTicketViewModel.ContraRoute = openTicketRequest.FinishingRoute;
                                }
                            }
                            SetPriceForThreeWay(secondTicketViewModel, thirdTicketViewModel, openTicketRequest.Price, openTicketRequest.Edge, openTicketRequest.Increment, openTicketRequest.Interval, openTicketRequest.FishStartEdge);
                        });

                        window.Show();
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ThreeWayFromRequestAsync));
                        window?.Close();
                        await ThreeWayFromRequestAsync(openTicketRequest, windowParameter, dominator);
                        return;
                    }
                });
                thirdTicket.SetApartmentState(ApartmentState.STA);

                secondTicket.Start();
                thirdTicket.Start();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ThreeWayFromRequestAsync));
            }
        }

        private void SetPriceForThreeWay(OrderTicket secondTicketViewModel, OrderTicket thirdTicketViewModel, double averagePrice = double.NaN, double edge = double.NaN, double increment = 0, double interval = 0, double startingEdge = 0)
        {
            if (!double.IsNaN(averagePrice))
            {
                Task.Run(() =>
                {
                    if (double.IsNaN(edge))
                    {
                        double defaultContraEdge = GetDefaultContraEdge();
                        edge = defaultContraEdge;
                    }

                    Task<bool> theoLoadTask;
                    double verticalTheo = double.NaN;
                    if (!Underlying.StartsWith("$") && OmsCore.Config.UseDeltaAdjTheoForThreeWay)
                    {
                        theoLoadTask = thirdTicketViewModel.WaitForAdjTheoLoadAsync();
                        verticalTheo = thirdTicketViewModel.SuggestTradingContra ? -thirdTicketViewModel.NetDeltaAdjTheo : thirdTicketViewModel.NetDeltaAdjTheo;
                    }
                    else
                    {
                        theoLoadTask = thirdTicketViewModel.WaitForTheoLoadAsync();
                        verticalTheo = thirdTicketViewModel.SuggestTradingContra ? -thirdTicketViewModel.NetTheo : thirdTicketViewModel.NetTheo;
                    }

                    theoLoadTask.ContinueWith(t =>
                    {
                        if ((double.IsNaN(Price) && SuggestTradingMain) || (double.IsNaN(ContraPrice) && SuggestTradingContra))
                        {
                            if (SuggestTradingMain)
                            {
                                if (!double.IsNaN(DeltaAdjPx))
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetPrice(DeltaAdjPx)));
                                }
                                else
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetPrice(averagePrice)));
                                }
                            }
                            else if (SuggestTradingContra)
                            {
                                if (!double.IsNaN(DeltaAdjContraPx))
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetContraPrice(DeltaAdjContraPx)));
                                }
                                else
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetContraPrice(averagePrice)));
                                }
                            }
                        }
                        averagePrice = SuggestTradingContra ? -averagePrice : averagePrice;

                        double price = -(edge + (averagePrice + GetTotalFeesForTicket()) + (verticalTheo + thirdTicketViewModel.GetTotalFeesForTicket()) + secondTicketViewModel.GetTotalFeesForTicket());
                        if (secondTicketViewModel.SuggestTradingMain)
                        {
                            secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetPrice(price)));
                        }
                        else if (secondTicketViewModel.SuggestTradingContra)
                        {
                            secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetContraPrice(-price)));
                        }

                        if (thirdTicketViewModel.SuggestTradingMain)
                        {
                            thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetPrice(verticalTheo)));
                        }
                        else if (thirdTicketViewModel.SuggestTradingContra)
                        {
                            thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetContraPrice(-verticalTheo)));
                        }

                        if (increment > 0 && interval >= 250 && startingEdge >= 0)
                        {
                            secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                secondTicketViewModel?.StartRouteFish(secondTicketViewModel.SuggestTradingMain, startingEdge, increment, interval);
                            }));
                        }

                        EdgeProjector?.CalculateEdge();
                    });
                });
            }
            else
            {
                Task.Run(() =>
                {
                    if (!Underlying.StartsWith("$") && OmsCore.Config.UseDeltaAdjTheoForThreeWay)
                    {
                        if ((double.IsNaN(Price) && SuggestTradingMain) || (double.IsNaN(ContraPrice) && SuggestTradingContra))
                        {
                            WaitForAdjTheoLoadAsync().ContinueWith(t =>
                            {
                                if (SuggestTradingMain)
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetPrice(NetDeltaAdjTheo)));
                                }
                                else if (SuggestTradingContra)
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetContraPrice(NetDeltaAdjTheo)));
                                }
                                EdgeProjector?.CalculateEdge();
                            });
                        }
                        secondTicketViewModel.WaitForAdjTheoLoadAsync().ContinueWith(t =>
                        {
                            if (secondTicketViewModel.SuggestTradingMain)
                            {
                                secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetPrice(secondTicketViewModel.NetDeltaAdjTheo)));
                            }
                            else if (secondTicketViewModel.SuggestTradingContra)
                            {
                                secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetContraPrice(secondTicketViewModel.NetDeltaAdjTheo)));
                            }
                            EdgeProjector?.CalculateEdge();
                        });
                        thirdTicketViewModel.WaitForAdjTheoLoadAsync().ContinueWith(t =>
                        {
                            if (thirdTicketViewModel.SuggestTradingMain)
                            {
                                thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetPrice(thirdTicketViewModel.NetDeltaAdjTheo)));
                            }
                            else if (thirdTicketViewModel.SuggestTradingContra)
                            {
                                thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetContraPrice(thirdTicketViewModel.NetDeltaAdjTheo)));
                            }
                            EdgeProjector?.CalculateEdge();
                        });
                    }
                    else
                    {
                        if ((double.IsNaN(Price) && SuggestTradingMain) || (double.IsNaN(ContraPrice) && SuggestTradingContra))
                        {
                            WaitForTheoLoadAsync().ContinueWith(t =>
                            {
                                if (SuggestTradingMain)
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetPrice(NetTheo)));
                                }
                                else if (SuggestTradingContra)
                                {
                                    Dispatcher.BeginInvoke(new Action(() => SetContraPrice(NetTheo)));
                                }
                                EdgeProjector?.CalculateEdge();
                            });
                        }
                        secondTicketViewModel.WaitForTheoLoadAsync().ContinueWith(t =>
                        {
                            if (secondTicketViewModel.SuggestTradingMain)
                            {
                                secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetPrice(secondTicketViewModel.NetTheo)));
                            }
                            else if (secondTicketViewModel.SuggestTradingContra)
                            {
                                secondTicketViewModel.Dispatcher.BeginInvoke(new Action(() => secondTicketViewModel?.SetContraPrice(secondTicketViewModel.NetTheo)));
                            }
                            EdgeProjector?.CalculateEdge();
                        });
                        thirdTicketViewModel.WaitForTheoLoadAsync().ContinueWith(t =>
                        {
                            if (thirdTicketViewModel.SuggestTradingMain)
                            {
                                thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetPrice(thirdTicketViewModel.NetTheo)));
                            }
                            else if (thirdTicketViewModel.SuggestTradingContra)
                            {
                                thirdTicketViewModel.Dispatcher.BeginInvoke(new Action(() => thirdTicketViewModel?.SetContraPrice(thirdTicketViewModel.NetTheo)));
                            }
                            EdgeProjector?.CalculateEdge();
                        });
                    }
                });
            }
        }

        private void StartRouteFish(bool suggestTradingMain, double edge, double increment, double interval)
        {
            _manualRouteFish = new FishRoute()
            {
                Edge = edge,
                Increment = increment,
                Interval = interval,
            };
            if (suggestTradingMain)
            {
                _manualRouteFish.RoutesList.Add(Route);
                _ = SubmitAsync();
            }
            else
            {
                _manualRouteFish.RoutesList.Add(ContraRoute);
                _ = SubmitContraAsync();
            }
        }

        public async Task<int> GetMinOfBidAndAskSize()
        {
            await WaitForSizeLoadAsync();
            return Math.Min(BidSize, AskSize);
        }

        public Task<bool> WaitForWeightedVegaLoadAsync()
        {
            if (SubscribedToWeightedVega)
            {
                return WaitForDataLoad(() => WeightedVegaLoaded, nameof(WeightedVegaLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForTheoLoadAsync()
        {
            if (SubscribedToNetTheo)
            {
                return WaitForDataLoad(() => NetTheoLoaded, nameof(NetTheoLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForHistoricBestLoadAsync()
        {
            if (SubscribedToNetHistoricBest)
            {
                return WaitForDataLoad(() => NetHistoricBestLoaded, nameof(NetHistoricBestLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForAdjTheoLoadAsync()
        {
            if (SubscribedToNetAdjTheo)
            {
                return WaitForDataLoad(() => NetAdjTheoLoaded, nameof(NetAdjTheoLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForLowLoadAsync()
        {
            if (SubscribedToLow)
            {
                return WaitForDataLoad(() => LowLoaded, nameof(LowLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForLowestBidLoad()
        {
            if (SubscribedToLowestBid)
            {
                return WaitForDataLoad(() => LowestBidLoaded, nameof(LowestBidLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForHighestOfferLoad()
        {
            if (SubscribedToHighestOffer)
            {
                return WaitForDataLoad(() => HighestOfferLoaded, nameof(HighestOfferLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForHighestBidLowestAskDataLoad()
        {
            if (SubscribedToHighestBidLowestAsk)
            {
                return WaitForDataLoad(() => HighestBidLowestAskLoaded, nameof(HighestBidLowestAskLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForHighLoad()
        {
            if (SubscribedToHigh)
            {
                return WaitForDataLoad(() => HighLoaded, nameof(HighLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForSizeLoadAsync()
        {
            if (SubscribedToSize)
            {
                return WaitForDataLoad(() => SizeLoaded, nameof(SizeLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForIbQuoteLoadAsync()
        {
            if (SubscribedToIbQuote)
            {
                return WaitForDataLoad(() => IbQuoteLoaded, nameof(IbQuoteLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForMarkLoad()
        {
            if (SubscribedToMark)
            {
                return WaitForDataLoad(() => MarkLoaded, nameof(MarkLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForBestMarkLoad()
        {
            if (SubscribedToBestMark)
            {
                return WaitForDataLoad(() => BestMarkLoaded, nameof(BestMarkLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForEmaLoad()
        {
            if (SubscribedToEma)
            {
                return WaitForDataLoad(() => EmaLoaded, nameof(EmaLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForAdjEmaLoad()
        {
            if (SubscribedToAdjEma)
            {
                return WaitForDataLoad(() => AdjEmaLoaded, nameof(AdjEmaLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForBidEmaLoad()
        {
            if (SubscribedToBidEma)
            {
                return WaitForDataLoad(() => BidEmaLoaded, nameof(BidEmaLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForAskEmaLoadAsync()
        {
            if (SubscribedToAskEma)
            {
                return WaitForDataLoad(() => AskEmaLoaded, nameof(AskEmaLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForDigLoad()
        {
            if (SubscribedToDig)
            {
                return WaitForDataLoad(() => DigLoaded, nameof(DigLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForUnderMidLoadAsync()
        {
            if (SubscribedToUnder)
            {
                return WaitForDataLoad(() => UnderLoaded, nameof(UnderLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        public Task<bool> WaitForHedgeLastLoadAsync()
        {
            if (SubscribedToHedgeUnder)
            {
                return WaitForDataLoad(() => HedgeUnderLoaded, nameof(HedgeUnderLoaded));
            }
            else
            {
                return Task.FromResult(false);
            }
        }

        private async Task<bool> WaitForDataLoad(Func<bool> condition, string name = "data")
        {
            if (condition())
                return true;

            int timeoutMs = DataLoadTimeout * LegsCount;
            long deadline = Environment.TickCount64 + timeoutMs;

            while (true)
            {
                var signal = _dataLoadNotification;

                if (condition())
                    return true;

                long remaining = deadline - Environment.TickCount64;
                if (remaining <= 0)
                    break;

                try
                {
                    await signal.Task.WaitAsync(TimeSpan.FromMilliseconds(remaining)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            if (condition())
                return true;

            Reason = $"Waiting for {name} failed!";
            if (_log.IsDebugEnabled)
            {
                _log.Debug("{} {}", SpreadId, Reason);
            }
            return false;
        }

        internal double PadForNickelOrDime(double input)
        {
            return PadForNickelOrDime(input, false);
        }

        internal double PadForNickelOrDime(double input, bool floor)
        {
            return PadForNickelOrDime(input, GetPriceIncrement(input), floor);
        }

        internal double PadForNickelOrDime(double input, decimal increment, bool floor)
        {
            if (double.IsNaN(input) || double.IsInfinity(input))
            {
                return input;
            }

            decimal value = Math.Round(Convert.ToDecimal(input), 2);

            const decimal DIME_INC = .10M;

            if (increment == DIME_INC)
            {
                if (value % DIME_INC == 0)
                {
                    return Convert.ToDouble(value);
                }
                else if (floor)
                {
                    decimal rounded = Math.Round(value, 1);
                    return rounded < value ? Convert.ToDouble(rounded) : Convert.ToDouble(rounded - DIME_INC);
                }
                else
                {
                    decimal rounded = Math.Round(value, 1);
                    return rounded > value ? Convert.ToDouble(rounded) : Convert.ToDouble(rounded + DIME_INC);
                }
            }
            else
            {
                int multiplier = 100;
                int whole = (int)value;

                int fraction = (int)((Math.Abs(value) - Math.Abs(whole)) * multiplier);

                if (input == 0)
                {
                    return input;
                }

                int lastDigit = fraction % 10;
                if (lastDigit is > 0 and < 5)
                {
                    int diffW5 = 5 - lastDigit;
                    if (!floor)
                    {
                        fraction = lastDigit < diffW5 ? fraction - lastDigit : fraction + diffW5;
                    }
                    else
                    {
                        fraction = input > 0 ? fraction - lastDigit : fraction + diffW5;
                    }
                }
                else if (lastDigit is > 5 and <= 9)
                {
                    int diffW5 = lastDigit - 5;
                    int diffW10 = 10 - lastDigit;
                    if (!floor)
                    {
                        fraction = diffW5 < diffW10 ? fraction - diffW5 : fraction + diffW10;
                    }
                    else
                    {
                        fraction = input > 0 ? fraction - diffW5 : fraction + diffW10;
                    }
                }

                decimal newFraction = fraction / (decimal)multiplier;
                return input > 0
                    ? Convert.ToDouble(whole + newFraction)
                    : Convert.ToDouble(whole - newFraction);
            }
        }

        public void ClearTicket()
        {
            OrderId = ContraOrderId = "";
            LoopIterationCounter = 0;
            BelowEdgeResubmitCounter = 0;
            LoopIterationCounterAfterSizeup = 0;
            ResubmitCount = 0;
            StopLossAttemptCounter = 0;
            OrderIdsSet.Clear();
            ContraOrderIdsSet.Clear();
            HedgeOrderIdsSet.Clear();
            UnderlyingClosing = 0;
            UnderlyingClosingInitialized = false;
            UnsubscribeDataAsync();
            foreach (TicketLegModel leg in Legs)
            {
                leg.LegUpdatedEvent -= UpdateTicketValues;
                leg.Dispose();
            }
            ClearUi();
            MarketBidEma = double.NaN;
            MarketAskEma = double.NaN;
            LastTradeUpdate = null;
            DeltaAdjLastTradeUpdate = double.NaN;
            DeltaAdjPx = double.NaN;
            DeltaAdjContraPx = double.NaN;
            LockDeltaAdjPrice = false;
            LockContraDeltaAdjPrice = false;
            BestDeltaAdjPx = double.NaN;
            BestDeltaAdjContraPx = double.NaN;
            ResetPermAdj();
            ResetAdjEdgeSummary();
            LockBestDeltaAdjPrice = false;
            LockContraBestDeltaAdjPrice = false;
            LastFillPx = double.NaN;
            LastFillUnderBidPx = double.NaN;
            LastFillUnderPx = double.NaN;
            LastFillUnderAskPx = double.NaN;
            LastFillAdjTheo = double.NaN;
            LastContraFillPx = double.NaN;
            LastContraFillAdjTheo = double.NaN;
            LastLoopRoute = null;
            LastLoopContraRoute = null;
            Last = double.NaN;
            VolumeAtFill = double.NaN;
            ChangeInVolume = double.NaN;
            UnderMid = double.NaN;
            LastOptionPnlOnFill = double.NaN;
            LastHedgePnlOnFill = double.NaN;
            LastTotalPnlOnFill = double.NaN;
            LastEdgeToMarketOnFill = double.NaN;
            LiveLastTradeOptionPnl = double.NaN;
            LiveLastTradeHedgePnl = double.NaN;
            LiveLastTradeTotalPnl = double.NaN;
            LiveLastTradeEdgeToMarket = double.NaN;
            LastHedgePrice = double.NaN;
            LastHedgeQty = 0;
            AveragePrice = double.NaN;
            BestAveragePrice = double.NaN;
            ContraAveragePrice = double.NaN;
            HedgeBid = double.NaN;
            HedgeAsk = double.NaN;

            LastMainUnderPriceAtFill = double.NaN;
            LastContraUnderPriceAtFill = double.NaN;

            LastMainTotalVolumeAtFill = double.NaN;
            LastContraTotalVolumeAtFill = double.NaN;

            LastMainUnderMidAtFill = double.NaN;
            LastMainUnderMidAtBestFill = double.NaN;
            LastContraUnderMidAtFill = double.NaN;

            LastTransactionPrice = double.NaN;
            LastContraTransactionPrice = double.NaN;

            ShowIbData = false;

            TwsPrice = double.NaN;
            TwsContraPrice = double.NaN;
            TwsBidLive = false;
            TwsAskLive = false;
            TwsBidSize = 0;
            TwsAskSize = 0;
            TwsLastSize = 0;
            TwsVolume = 0;
            TwsHigh = double.NaN;
            TwsLow = double.NaN;
            TwsOpen = double.NaN;
            TwsClose = double.NaN;
            TwsLast = double.NaN;
            TwsBidExch = "";
            TwsAskExch = "";
            TwsLastExch = "";

            SubmitLatency = 0;
            PendingNewLatency = 0;
            RemoveEdgeProjector();
            SpreadSymbol = "";
            ContraSpreadSymbol = "";
            Legs.Clear();

            SingleOrderTicketStopLossValue = 0;
            SingleOrderTicketTrailingStopValue = 0;
            SingleOrderTicketPosition = 0;
            SingleOrderTicketWorkingPosition = 0;
            _bidAtFillForSingleTickets = 0;
            _askAtFillForSingleTickets = 0;

            LastExchange = "";
            Exchanges = "";
            LastContraExchange = "";
            FeesEstimate = 0;
            Description = "";
            TraderSpreadPosition = 0;
            SpreadPosition = 0;
            SpreadRawPosition = 0;
            LcdPosition = 0;
            HedgeAttempt = 0;
            TotalStocks = 0;
            HedgedStocks = 0;
            RequiredStocks = 0;
            SubmittedStocks = 0;
            CanHedge = false;
            StockHedgeQty = 0;
            StockHedgeStatus = "";
            StockHedgeStatusMode = StatusMode.Reset;
            StockHedgeAdjTradePx = double.NaN;
            AdjustedPriceAtHedge = double.NaN;
            StockPriceAtHedge = double.NaN;
            PositionNetDelta = double.NaN;
            HedgeNetDelta = double.NaN;
            PositionNetWeightedVega = double.NaN;
            ReversePnl = 0;
            ReverseSpreadPosition = 0;
            HedgeReversePnl = 0;
            HedgeReversePosition = 0;
            AdjustedPnl = double.NaN;
            UnrealizedPnl = double.NaN;
            AvgCost = double.NaN;
            FirmLastTrader = "";
            FirmLastEdge = double.NaN;
            FirmLastBuyEdge = double.NaN;
            FirmLastSellEdge = double.NaN;
            FirmLastBuyOrderEdgeToTheo = double.NaN;
            FirmLastSellOrderEdgeToTheo = double.NaN;
            FirmLastFillBuyEdgeToTheo = double.NaN;
            FirmLastFillSellEdgeToTheo = double.NaN;
            FirmLastBuyAttemptEdgeToTheo = double.NaN;
            FirmLastSellAttemptEdgeToTheo = double.NaN;
            GlobalMarketBuyEdgeToTheo = double.NaN;
            GlobalMarketSellEdgeToTheo = double.NaN;
            ResetFirmOrderAndTradeSummaryValues();
            FirmLastTradeSide = null;
            FirmLastTradeTime = default;
            FirmLastBuyAttempt = double.NaN;
            FirmLastBuyAttemptUnderlying = double.NaN;
            FirmLastSellAttempt = double.NaN;
            FirmLastSellAttemptUnderlying = double.NaN;
            LastPermBuyFillEdgeToTheo = double.NaN;
            LastPermSellFillEdgeToTheo = double.NaN;
            LastPermBuyAttemptEdgeToTheo = double.NaN;
            LastPermSellAttemptEdgeToTheo = double.NaN;
            BestBuyEdgeToTheo = double.NaN;
            WorstBuyEdgeToTheo = double.NaN;
            BestSellEdgeToTheo = double.NaN;
            WorstSellEdgeToTheo = double.NaN;
            OpenPositionAveragePrice = double.NaN;
            HardSide = null;
            StockHedgeOpenPositionAveragePrice = double.NaN;
            TraderAdjustedPnl = double.NaN;
            SpreadPositionInitialized = false;
            TraderSpreadPositionInitialized = false;
            DualDescription = "";
            SpreadId = "";
            SpreadPermId = "";
            SpreadType = "";
            Symbol = "";
            StrikeSpacing = double.NaN;
            Filled = Status = ContraFilled = ContraStatus = "";
            MainOrderStatus = ContraOrderStatus = null;
            StatusMode = ContraStatusMode = StatusMode.Reset;
            IsSubmitEnabled = false;
            IsModifyEnabled = false;
            IsCancelEnabled = false;
            IsContraSubmitEnabled = false;
            IsContraModifyEnabled = false;
            IsContraCancelEnabled = false;
            OrderIsClosed = true;
            Closing = false;
            ThreeWayStarted = false;
            ThreeWayComplete = false;
            LastTradedContra = false;
            StockPos = "";
            Route = GetBestRoute();
            TimeInForce = TimeInForce.DAY;

            FirmTotalVolume = double.NaN;
            TotalVolume = double.NaN;
            OpenInterest = double.NaN;
            TotalDelta = double.NaN;
            TotalDeltaDirection = double.NaN;
            TotalGamma = double.NaN;
            TotalVega = double.NaN;
            WeightedVega = double.NaN;
            TotalTheta = double.NaN;
            TotalRho = double.NaN;
            TotalImplied = double.NaN;
            ImpliedEma = double.NaN;
            ImpliedChange = double.NaN;
            NotionalImpliedChange = double.NaN;

            ResetEvents(all: true);

            NetTheoSynched = false;
            DeltaAdjTheoSynched = false;
            DeltaAdjTheoSequence = 0;
            UpdateLCD();
            UpdateDescription();
            ClearLoggers();

            OnClearTicket();
        }

        protected virtual void OnClearTicket()
        {
        }

        private void ClearLoggers()
        {
            LogPermAdjPxUnderlyingMid = double.NaN;
            LogPermAdjPxDelta = double.NaN;
            LogPermAdjPxContraDelta = double.NaN;
            LogUnderlyingMidAtPermLoad = double.NaN;
            LogPermAdjPxBase = double.NaN;
            LogPermAdjContraPxBase = double.NaN;
            LogPermAdjPrice = double.NaN;
            LogPermAdjContraPrice = double.NaN;
            LogPermAdjPxMatchingHw = double.NaN;
            LogPermAdjPxBaseEdge = double.NaN;
            LogPermAdjPxOrig = double.NaN;
            LogPermAdjContraPxOrig = double.NaN;
            LogPermAdjDeltaAdjPxOrig = double.NaN;
            LogPermAdjDeltaAdjContraPxOrig = double.NaN;
        }

        private void ClearUi()
        {
            TicketValues update = new()
            {
                Low = double.NaN,
                High = double.NaN,
                BestLow = double.NaN,
                BestEdgeBid = double.NaN,
                BestEdgeAsk = double.NaN,
                BestEdgeMid = double.NaN,
                BestHigh = double.NaN,
                Mid = double.NaN,
                LowInt = double.NaN,
                HighInt = double.NaN,
                MidInt = double.NaN,
                BestBidInt = double.NaN,
                BestAskInt = double.NaN,
                MktMkrBid = double.NaN,
                MktMkrAsk = double.NaN,
                HighestBid = double.NaN,
                LowestAsk = double.NaN,
                BestMidInt = double.NaN,
                LowDerived = double.NaN,
                HighDerived = double.NaN,
                MidDerived = double.NaN,
                LowIntDerived = double.NaN,
                HighIntDerived = double.NaN,
                MidIntDerived = double.NaN,
                BidEma = double.NaN,
                AskEma = double.NaN,
                BidEmaAdj = double.NaN,
                AskEmaAdj = double.NaN,
                AdjEma = double.NaN,
                UnderEma = double.NaN,
                FullEma = double.NaN,
                Ema = double.NaN,
                Width = double.NaN,
                BidIvEma = double.NaN,
                AskIvEma = double.NaN,
                NetDeltaAdjTheo = double.NaN,
                TheoBid = double.NaN,
                TheoAsk = double.NaN,
                DigBid = double.NaN,
                DigAsk = double.NaN,
                NetTheo = double.NaN,
                TheoSynched = false,
                NetDelta = double.NaN,
                NetTheta = double.NaN,
                NetGamma = double.NaN,
                TotalVolume = double.NaN,
                OpenInterest = double.NaN,
                FirmTotalVolume = double.NaN,
                TotalDelta = double.NaN,
                TotalGamma = double.NaN,
                TotalVega = double.NaN,
                WeightedVega = double.NaN,
                TotalTheta = double.NaN,
                TotalRho = double.NaN,
                TotalImplied = double.NaN,
                TotalTheo = double.NaN,
                TotalDeltaAdjTheo = double.NaN,
                SmoothedDeltaAdjTheo = double.NaN,
                VolaPriceMetricV0 = double.NaN,
                VolaPriceMetricV1 = double.NaN,
                VolaPriceMetricV2 = double.NaN,
                VolaPriceMetricV3 = double.NaN,
                VolaTheoV0 = double.NaN,
                VolaTheoAdjV0 = double.NaN,
                VolaIv = double.NaN,
                VolaTheoV1 = double.NaN,
                VolaTheoAdjV1 = double.NaN,
                VolaTheoV2 = double.NaN,
                VolaTheoAdjV2 = double.NaN,
                VolaTheoV3 = double.NaN,
                VolaTheoAdjV3 = double.NaN,
                LockedTheo = double.NaN,
                LockedDeltaAdjTheo = double.NaN,
                NetPrice = double.NaN,
                PriceDiff = double.NaN,
                NetContraPrice = double.NaN,
                EdgeToTheo = double.NaN,
                ContraEdgeToTheo = double.NaN,
                EdgeToDeltaAdjTheo = double.NaN,
                EdgeToDeltaAdjTheoV0 = double.NaN,
                ContraEdgeToDeltaAdjTheo = double.NaN,
                EdgeToMid = double.NaN,
                EdgeToMidDerived = double.NaN,
                ContraEdgeToMid = double.NaN,
                PercentBid = double.NaN,
                ContraPercentBid = double.NaN,
                LastBidTheoSpread = double.NaN,
                LastAskTheoSpread = double.NaN,
                BidTheoSpreadEma = double.NaN,
                AskTheoSpreadEma = double.NaN,
                AdjDaEma = double.NaN,
                DeltaAdjTheoSynched = true,
                VolaAdjTheoSyncV1 = true,
                VolaAdjTheoSyncV2 = true,
                VolaAdjTheoSyncV3 = true,
            };
            UpdateUi(update);
        }

        public async Task<string> ExpirationUpAsync(PermSide permSide, bool skipCheck = false, bool maintainBaseStrategy = false)
        {
            if (!skipCheck && !await IsNextPermValidAsync(PermMode.ExpirationUp, permSide, maintainBaseStrategy: maintainBaseStrategy))
            {
                return "Next perm is invalid.";
            }
            return await RunPermBumpAsync(PermMode.ExpirationUp, permSide, maintainBaseStrategy, returnSummary: true) ?? string.Empty;
        }

        public async Task<string> ExpirationDownAsync(PermSide permSide, bool skipCheck = false, bool maintainBaseStrategy = false)
        {
            if (!skipCheck && !await IsNextPermValidAsync(PermMode.ExpirationDown, permSide, maintainBaseStrategy: maintainBaseStrategy))
            {
                return "Next perm is invalid.";
            }
            return await RunPermBumpAsync(PermMode.ExpirationDown, permSide, maintainBaseStrategy, returnSummary: true) ?? string.Empty;
        }

        public async Task<bool> StrikeUpAsync(PermSide permSide, bool skipCheck = false, bool maintainBaseStrategy = false)
        {
            if (!skipCheck && !await IsNextPermValidAsync(PermMode.StrikeUp, permSide, maintainBaseStrategy))
            {
                return false;
            }
            string failure = await RunPermBumpAsync(PermMode.StrikeUp, permSide, maintainBaseStrategy, returnSummary: false);
            if (failure != null)
            {
                throw new SlimException(failure);
            }
            return true;
        }

        public async Task<bool> StrikeDownAsync(PermSide permSide, bool skipCheck = false, bool maintainBaseStrategy = false)
        {
            if (!skipCheck && !await IsNextPermValidAsync(PermMode.StrikeDown, permSide, maintainBaseStrategy))
            {
                return false;
            }
            string failure = await RunPermBumpAsync(PermMode.StrikeDown, permSide, maintainBaseStrategy, returnSummary: false);
            if (failure != null)
            {
                throw new SlimException(failure);
            }
            return true;
        }

        private async Task<string> RunPermBumpAsync(PermMode mode, PermSide permSide, bool maintainBaseStrategy, bool returnSummary)
        {
            PreUpdate();
            try
            {
                ZeroPlus.Models.Data.Models.PermSpreadResult candidate;
                if (UseServerSidePerming())
                {
                    candidate = await FetchServerPermAsync(mode, permSide, maintainBaseStrategy, skipCheck: true);
                }
                else
                {
                    candidate = await ZeroPlus.Models.Utils.PermutationOrderHelper.ComputeNextSpreadAsync(
                        this,
                        mode,
                        permSide,
                        chainFactory: (under, ct) => PermutationAdapter.BuildModelsChainAsync(OmsCore.QuoteClient, under, ct),
                        maintainBaseStrategy: maintainBaseStrategy,
                        skipCheck: true,
                        cache: PermutationAdapter.TreeCache);
                }

                List<TicketLegModel> optionLegs = Legs.Where(x => x.SecurityType == SecurityType.Option).ToList();
                int targetCount = permSide switch
                {
                    PermSide.All or PermSide.Alternate => optionLegs.Count,
                    PermSide.Low or PermSide.High => optionLegs.Count > 0 ? 1 : 0,
                    _ => optionLegs.Count,
                };

                int failedCount = 0;
                string summary = string.Empty;

                if (candidate == null)
                {
                    failedCount = targetCount;
                }
                else
                {
                    for (int i = 0; i < candidate.Legs.Count && i < optionLegs.Count; i++)
                    {
                        TicketLegModel leg = optionLegs[i];
                        ZeroPlus.Models.Data.Models.PermLegResult after = candidate.Legs[i];
                        if (string.Equals(leg.Symbol, after.Symbol, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        double afterStrike = after.Strike;
                        DateTime afterExpiration = after.Expiration;
                        if (afterStrike == 0 || afterExpiration == default)
                        {
                            ZeroPlus.Models.Data.Securities.Security parsed = ZeroPlus.Models.Utils.SymbolParser.GetSecurityFromSymbol(after.Symbol, out _);
                            if (parsed is ZeroPlus.Models.Data.Securities.Option opt)
                            {
                                if (afterStrike == 0) afterStrike = opt.Strike;
                                if (afterExpiration == default) afterExpiration = opt.Expiration;
                            }
                        }

                        try
                        {
                            if ((leg.ExpirationInfo?.Expiration.Date ?? DateTime.MinValue) != afterExpiration.Date)
                            {
                                leg.UpdateExpiration(afterExpiration);
                            }
                            if (leg.Strike.Strike != afterStrike)
                            {
                                await leg.UpdateStrike(afterStrike);
                            }
                        }
                        catch (Exception ex)
                        {
                            failedCount++;
                            summary += $"Leg {Legs.IndexOf(leg) + 1}: {ex.Message}\n";
                        }
                    }
                }

                if (permSide != PermSide.All)
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        if (mode == PermMode.StrikeUp || mode == PermMode.StrikeDown)
                        {
                            leg.OnStrikeChange();
                        }
                        else
                        {
                            leg.ExpirationChanged();
                        }
                    }
                }

                if (returnSummary)
                {
                    if (candidate == null)
                    {
                        summary += "Next perm not available\n";
                    }
                    return summary;
                }

                return (candidate == null || failedCount >= targetCount)
                    ? (mode == PermMode.StrikeUp ? "Strike up failed for all legs." : "Strike down failed for all legs.")
                    : null;
            }
            finally
            {
                PostUpdate();
            }
        }

        public async Task<bool> IsNextPermValidAsync(PermMode mode, PermSide permSide, bool maintainBaseStrategy)
        {
            if (UseServerSidePerming())
            {
                ZeroPlus.Models.Data.Models.PermSpreadResult candidate = await FetchServerPermAsync(mode, permSide, maintainBaseStrategy, skipCheck: false);
                return candidate != null && candidate.Legs != null && candidate.Legs.Count > 0;
            }

            return await ZeroPlus.Models.Utils.PermutationOrderHelper.IsNextPermValidAsync(
                this,
                mode,
                permSide,
                chainFactory: (under, ct) => PermutationAdapter.BuildModelsChainAsync(OmsCore.QuoteClient, under, ct),
                maintainBaseStrategy: maintainBaseStrategy,
                cache: PermutationAdapter.TreeCache);
        }

        private bool UseServerSidePerming()
        {
            if (!OmsCore.Config.UseServerSidePerming)
            {
                return false;
            }
            ZeroPlus.SymbolMap.Client.Interfaces.ISymbolMapClient candidate = OmsCore.SymbolMapClient?.Client;
            if (candidate == null || !(OmsCore.SymbolMapClient?.IsConnected ?? false))
            {
                return false;
            }
            return true;
        }

        private async Task<ZeroPlus.Models.Data.Models.PermSpreadResult> FetchServerPermAsync(
            PermMode mode,
            PermSide permSide,
            bool maintainBaseStrategy,
            bool skipCheck)
        {
            ZeroPlus.SymbolMap.Client.Interfaces.ISymbolMapClient client = OmsCore.SymbolMapClient.Client;
            List<ZeroPlus.Models.Data.Models.PermLegRequest> legRequests = new();
            foreach (TicketLegModel leg in Legs)
            {
                if (leg.SecurityType != SecurityType.Option || string.IsNullOrEmpty(leg.Symbol)) continue;
                legRequests.Add(new ZeroPlus.Models.Data.Models.PermLegRequest
                {
                    Symbol = leg.Symbol,
                    Side = leg.Side ?? ZeroPlus.Models.Data.Enums.Side.Buy,
                    Ratio = leg.Ratio,
                });
            }
            if (legRequests.Count == 0)
            {
                return null;
            }

            List<ZeroPlus.Models.Data.Models.PermSpreadResult> results = await client.NextSpreadPermsAsync(
                legRequests,
                ZeroPlus.Models.Utils.PermutationEngine.GetDirection(mode),
                mode,
                permSide,
                count: 1,
                baseStrategy: BaseStrategy,
                maintainBaseStrategy: maintainBaseStrategy,
                maintainBaseStrategyFlyException: OmsCore.Config.MaintainBaseStrategyExceptionForFlyEnabled,
                skipCheck: skipCheck);

            return results != null && results.Count > 0 ? results[0] : null;
        }

        public async Task CheckForAutoCancel()
        {
            try
            {
                if (Closing ||
                    OrderIsClosed ||
                    AutoCancelRunning ||
                    _cancelRequestSent ||
                    string.IsNullOrEmpty(OrderId))
                {
                    return;
                }

                AutoCancelRunning = true;

                if (IsGammaScalpTicket)
                {
                    CheckForGammaScalperAutoCancel();
                }
                else
                {
                    await CheckAutoCancel();
                }
            }
            finally
            {
                AutoCancelRunning = false;
            }
        }

        protected virtual async Task CheckAutoCancel()
        {
            if (!RiskCheckEnabled)
            {
                return;
            }

            if (!await CheckEdgeRiskParametersAsync(preSubmit: false))
            {
                if (!_cancelRequestSent)
                {
                    _cancelRequestSent = true;
                    CancelMain();
                    _log.Info("Auto cancel triggered by edge check." +
                          ", Spread: " + SpreadId +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                }
                else
                {
                    _log.Info("Auto cancel triggered by edge check. Cancel request already sent." +
                          ", Spread: " + SpreadId +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                }
            }
        }

        private void CheckForGammaScalperAutoCancel()
        {
            if (IsGammaScalpTicket)
            {
                if (ScalpCancelWithMidEdge > 0 && MarkLoaded)
                {
                    double delta = _midToWatchFor - Mid;

                    if (_midToWatchFor < 0)
                    {
                        delta *= -1;
                    }

                    if (delta >= ScalpCancelWithMidEdge)
                    {
                        string message = "Auto cancel triggered by mid change" +
                                         ", Spread: " + SpreadId +
                                         ", Change: " + delta +
                                         ", Threshold: " + ScalpCancelWithMidEdge;
                        if (CancelFromGammaScalp(message))
                        {
                            return;
                        }
                    }
                }

                if (ScalpCancelWithUnderEdge > 0 && UnderLoaded && NetTheoLoaded)
                {
                    double priceDelta = _underToWatchFor - UnderMid;

                    double price = Price;

                    if ((price >= 0 && TotalDelta > 0) || (price < 0 && TotalDelta < 0))
                    {
                        priceDelta *= -1;
                    }

                    if (priceDelta >= ScalpCancelWithUnderEdge)
                    {
                        string message = "Auto cancel triggered by underlying change" +
                                         ", Spread: " + SpreadId +
                                         ", Change: " + priceDelta +
                                         ", Threshold: " + BasketSettings.CancelWithUnderlyingPx +
                                         ", Server Creep: " + BasketTraderViewModel?.ServerCreep;
                        if (CancelFromGammaScalp(message))
                        {
                            return;
                        }
                    }
                }
            }
        }

        protected bool CancelFromGammaScalp(string message)
        {
            if (!_canAutoCancel)
            {
                return true;
            }

            bool retVal = false;

            if (!_cancelRequestSent)
            {
                _cancelRequestSent = true;
                if (BasketSettings.ResubmitAfterCancel)
                {
                    _resubmitWhenReceivingCancelStatus = true;
                }
                CancelMain();
                _log.Info(message);
                retVal = true;
            }
            if (!_cancelContraRequestSent)
            {
                _cancelContraRequestSent = true;
                CancelContra();
            }
            return retVal;
        }

        public async Task<bool> CheckEdgeRiskParametersAsync(bool preSubmit)
        {
            if (_riskModel.OverrideEdgeCheck)
            {
                return true;
            }

            return await Task.Run(async () =>
            {
                bool priceBelowMidCheckTask = await IsPriceBelowMid(preSubmit);
                if (!priceBelowMidCheckTask) { return false; }
                if (_riskModel.DontTradeThroughBidPercent)
                {
                    bool priceBelowBidPercentCheckTask = await IsPriceBelowBidPercent(_riskModel.DontTradeThroughBidPercentValue);
                    if (!priceBelowBidPercentCheckTask) { return false; }
                }

                if (!await CheckForEdgeRisk(preSubmit))
                {
                    return false;
                }

                return true;
            });
        }

        protected virtual Task<bool> CheckForEdgeRisk(bool preSubmit)
        {
            return Task.FromResult(true);
        }

        internal double GetCurrentEdge()
        {
            if (!double.IsNaN(AdjustedEdgeOverride))
            {
                return AdjustedEdgeOverride;
            }
            else if (!double.IsNaN(EdgeOverride))
            {
                return EdgeOverride;
            }
            else if (BasketSettings.UseEdgeToTheo)
            {
                return BasketSettings.EdgeToTheo;
            }
            else if (BasketSettings.UseEdgeToHistoricBest)
            {
                return BasketSettings.EdgeToHistoricBest;
            }
            else if (BasketSettings.UseEdgeToAdjTheo)
            {
                return BasketSettings.EdgeToAdjTheo;
            }
            else if (BasketSettings.UseTheoToMarketSpreadPx)
            {
                return BasketSettings.EdgeToTheoToMarketSpread;
            }
            else if (BasketSettings.UseLastFillAdjPx)
            {
                return BasketSettings.LastFillAdjEdge;
            }
            else if (BasketSettings.UseEdgeToMid)
            {
                return BasketSettings.EdgeToMid;
            }
            else if (BasketSettings.UseEdgeToEma)
            {
                return BasketSettings.EdgeToEma;
            }
            else if (BasketSettings.UseEdgeToTheoAndMid)
            {
                return BasketSettings.EdgeToTheoAndMid;
            }
            else if (BasketSettings.UseEdgeToTheoStopMid)
            {
                return BasketSettings.EdgeToTheoStopMid;
            }
            else if (BasketSettings.UseEdgeToEmaStopMid)
            {
                return BasketSettings.EdgeToEmaStopMid;
            }
            else if (BasketSettings.UseEdgeToMidStopEma)
            {
                return BasketSettings.EdgeToMidStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEma)
            {
                return BasketSettings.EdgeToBidPercentStopEma;
            }
            else if (BasketSettings.UseEdgeToBidPercentStopEmaStopTheo)
            {
                return BasketSettings.EdgeToBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToEmaBidPercentStopEmaStopTheo)
            {
                return BasketSettings.EdgeToEmaBidPercentStopEmaStopTheo;
            }
            else if (BasketSettings.UseEdgeToDerivedBidPercentStopEmaStopMid)
            {
                return BasketSettings.EdgeToDerivedBidPercentStopEmaStopMid;
            }
            else if (BasketSettings.UseBidPercent)
            {
                return BasketSettings.BidPercent;
            }
            else if (BasketSettings.UseEdgeToEmaBid)
            {
                return BasketSettings.EdgeToEmaBid;
            }
            else if (BasketSettings.UseEdgeToBid)
            {
                return BasketSettings.EdgeToBid;
            }
            else if (BasketSettings.UsePermAdjPx)
            {
                return BasketSettings.PermAdjEdge;
            }
            else if (BasketSettings.UseEdgeToAdjTheoWithOverride)
            {
                return EdgeOverride;
            }
            return double.NaN;
        }

        internal async Task UseEdgeToTheoAsync(double edge)
        {
            try
            {
                var results = await GetTheoAsync(null, true, OmsCore.Config.PerformanceModeEnabled);

                if (await WaitForTheoLoadAsync())
                {
                    SetEdgeToTheoPrice(results.NetTheo, edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToTheoAsync));
            }
            finally
            {
                _log.Info("Set price using edge to theo. Theo: " + NetTheo + ", Edge: " + edge + GetStats());
            }
        }

        internal async Task<DateTime> UseEdgeToAdjTheoAsync(double edge, bool ignoreRiskCheck = true, bool checkForOverride = true)
        {
            var result = await GetTheoAsync(null, true, OmsCore.Config.PerformanceModeEnabled);
            var netTheo = result.NetTheo;
            var netDeltaAdjTheo = result.NetDeltaAdjTheo;
            try
            {
                bool loaded = await WaitForAdjTheoLoadAsync();
                if (!loaded)
                {
                    ResetPriceAndContraPrice();
                    _log.Warn($"Checking adjusted theo risk failed! {(double.IsNaN(netDeltaAdjTheo) ? "Adj Theo not loaded. " : "")}. {GetStats()}");
                    return DateTime.Now;
                }

                if (!IsSingleLeg)
                {
                    if (!ignoreRiskCheck)
                    {
                        bool theoLoaded = await WaitForTheoLoadAsync();

                        if (!theoLoaded)
                        {
                            ResetPriceAndContraPrice();
                            _log.Warn($"Checking adjusted theo risk failed! {(double.IsNaN(netTheo) ? "HW theo not loaded. " : "")}. {GetStats()}");
                            return DateTime.Now;
                        }

                        if (Math.Abs(netDeltaAdjTheo - netTheo) > _riskModel.MaxTheoToAdjTheoOffset)
                        {
                            bool proceed = GetRiskVerification($"Adjusted theos are off when compared to hanweck theos.\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                            if (!proceed)
                            {
                                ResetPriceAndContraPrice();
                                ShowErrorMessage("Risk With Adj Theo");
                                return DateTime.Now;
                            }
                        }
                    }
                }

                if (!ignoreRiskCheck && _riskModel.StaleTheoRiskThreshold > 0 && _calcLegs != null && _calcLegs.Any())
                {
                    uint oldestSeq = _calcLegs.Min(x => x.DeltaAdjTheoSequence);
                    uint freshSeq = _calcLegs.Max(x => x.DeltaAdjTheoSequence);
                    uint diff = freshSeq - oldestSeq;
                    if (oldestSeq > 0 && diff > _riskModel.StaleTheoRiskThreshold)
                    {
                        bool proceed = GetRiskVerification($"Stale Adjusted theos detected.\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                        _log.Info($"Stale Adj Theo. Time: {oldestSeq}, Local: {freshSeq}, Adj Theo: {netDeltaAdjTheo} Edge: {edge}" + GetStats());
                        if (!proceed)
                        {
                            ResetPriceAndContraPrice();
                            ShowErrorMessage("Stale Adj Theo");
                            return DateTime.Now;
                        }
                    }
                }

                DateTime time = DateTime.Now;

                SetEdgeToAdjTheoPrice(netDeltaAdjTheo, edge, checkForOverride);

                return time;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToAdjTheoAsync));
                return default;
            }
            finally
            {
                _log.Info("Set price using edge to adjusted theo. Adj Theo: " + netDeltaAdjTheo + ", Edge: " + edge + GetStats());
            }
        }

        internal virtual double GetEma(bool fetch = false, ZeroPlus.Models.Data.Enums.EmaModel? emaModel = null)
        {
            double ema;
            emaModel ??= BasketSettings?.EmaModel;
            emaModel ??= OmsCore.Config.EmaMode;

            switch (emaModel)
            {
                case ZeroPlus.Models.Data.Enums.EmaModel.AdjDaEma:
                    ema = AdjDaEma;
                    break;
                case ZeroPlus.Models.Data.Enums.EmaModel.AdjEma:
                    ema = AdjEma;
                    break;
                case ZeroPlus.Models.Data.Enums.EmaModel.AdjVolaEma:
                    ema = AdjVolaEma;
                    break;
                case ZeroPlus.Models.Data.Enums.EmaModel.DaEma:
                    ema = DaEma;
                    break;
                case ZeroPlus.Models.Data.Enums.EmaModel.Ema:
                    ema = Ema;
                    break;
                case ZeroPlus.Models.Data.Enums.EmaModel.VolaEma:
                    ema = VolaEma;
                    break;
                default:
                    ema = double.NaN;
                    break;
            }

            return ema;
        }

        internal virtual async Task<GetTheoResult> GetTheoAsync(TheoModel? theoModel = null, bool checkForLockedTheo = false, bool fetchNewUpdate = false)
        {
            await Task.CompletedTask;
            return new GetTheoResult(NetTheo, NetDeltaAdjTheo);
        }

        internal async Task UseEdgeToTheoToMarketSpread(double edge)
        {
            try
            {
                if (await WaitForAdjTheoLoadAsync())
                {
                    SetEdgeToTheoToMarketSpread(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToTheoToMarketSpread));
            }
            finally
            {
                _log.Info("Set price using edge to theo to market spread. Theo: " + NetDeltaAdjTheo + ", Last Spread: [" + LastBidTheoSpread + "X" + LastAskTheoSpread + "], Spread EMA: [" + BidTheoSpreadEma + "X" + AskTheoSpreadEma + "], Edge: " + edge + GetStats());
            }
        }

        internal async Task<bool> UseEdgeToLastFillAdjPx(double edge)
        {
            try
            {
                if (await WaitForTheoLoadAsync() &&
                    await WaitForUnderMidLoadAsync())
                {
                    SetEdgeToLastFillAdjPx(edge);
                    return true;
                }
                else
                {
                    ResetPriceAndContraPrice();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToLastFillAdjPx));
                return false;
            }
            finally
            {
                _log.Info("Set price using edge to adjusted last fill px. Edge: " + edge + GetStats());
            }
        }

        internal async Task UseEdgeToMid(double edge)
        {
            try
            {
                if (await WaitForMarkLoad())
                {
                    SetEdgeToMidPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToMid));
            }
        }

        internal async Task UseEdgeToEma(double edge)
        {
            try
            {
                if (await WaitForEmaLoad())
                {
                    SetEdgeToEmaPrice(edge);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseEdgeToEma));
            }
        }

        internal async Task UseBidPercent(double bidPercent)
        {
            try
            {
                if (await WaitForLowLoadAsync() && await WaitForHighLoad())
                {
                    SetBidPercentPrice(bidPercent);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseBidPercent));
            }
        }

        internal async Task UseTheoBidPercent(double bidPercent)
        {
            try
            {
                if (await WaitForDataLoad(() => !double.IsNaN(TheoBid) && !double.IsNaN(TheoAsk), nameof(SubscriptionFieldType.ZpTheo)))
                {
                    SetEdgeToTheoBidPercent(bidPercent);
                }
                else
                {
                    ResetPriceAndContraPrice();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UseTheoBidPercent));
            }
        }

        protected void SetPermAdjPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculatePermAdjPrice(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToMidPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToMid(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        protected void SetEdgeToEmaPrice(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToEma(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetEdgeToTheoPrice(double netTheo, double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToTheo(netTheo, edge);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetEdgeToAdjTheoPrice(double adjTheo, double edge, bool checkForOverride)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToAdjTheo(adjTheo, edge, checkForOverride);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetEdgeToTheoToMarketSpread(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToTheoToMarketSpread(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetEdgeToLastFillAdjPx(double edge)
        {
            EdgePriceCalculationResult edgeResult = CalculateEdgeToLastFillAdjPx(edge);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetBidPercentPrice(double bidPercent)
        {
            EdgePriceCalculationResult edgeResult = CalculateBidPercent(bidPercent);
            SetPriceAndContraPrice(edgeResult);
        }

        private void SetEdgeToTheoBidPercent(double bidPercent)
        {
            EdgePriceCalculationResult edgeResult = CalculateTheoBidPercent(bidPercent);
            SetPriceAndContraPrice(edgeResult);
        }

        public void ResetPriceAndContraPrice()
        {
            Price = double.NaN;
            ContraPrice = double.NaN;
        }

        internal void SetPriceAndContraPrice(EdgePriceCalculationResult edgeResult, bool checkForSelfTrade = false)
        {
            if ((checkForSelfTrade && !CheckForSelfTrade(edgeResult)) || Legs.Count == 0)
            {
                Price = double.NaN;
                ContraPrice = double.NaN;
            }
            else
            {
                Price = edgeResult.Price;
                ContraPrice = !IsBasketOrder && TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical
                    ? -edgeResult.ContraPrice
                    : edgeResult.ContraPrice;
            }
        }

        public void SetEdgeOverride(double overrideEdge, bool silent)
        {
            if (silent)
            {
                _EdgeOverride = overrideEdge;
            }
            else
            {
                EdgeOverride = overrideEdge;
            }
        }

        public void ResetEdgeOverride(bool silent)
        {
            if (silent)
            {
                _EdgeOverride = double.NaN;
                _AdjustedEdgeOverride = double.NaN;
            }
            else
            {
                EdgeOverride = double.NaN;
                AdjustedEdgeOverride = double.NaN;
            }
        }

        public void SetEdgeAferEdgeOverideChange()
        {
            if (IsBasketOrder)
            {
                _ = SetEdgeAsync();
            }
        }

        private bool CheckForSelfTrade(EdgePriceCalculationResult edgeResult)
        {
            if (Legs.Count == 0)
            {
                return false;
            }
            else if (IsSingleLeg)
            {
                TicketLegModel leg = Legs[0];
                if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    return edgeResult.ContraPrice > edgeResult.Price;
                }
                else
                {
                    return edgeResult.Price > edgeResult.ContraPrice;
                }
            }
            else
            {
                if (edgeResult.Price > 0 && edgeResult.ContraPrice < 0)
                {
                    return Math.Abs(edgeResult.ContraPrice) > edgeResult.Price;
                }
                else if (edgeResult.Price < 0 && edgeResult.ContraPrice > 0)
                {
                    return Math.Abs(edgeResult.Price) > edgeResult.ContraPrice;
                }
                else
                {
                    if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        return Math.Abs(edgeResult.ContraPrice) > Math.Abs(edgeResult.Price);
                    }
                    else
                    {
                        return Math.Abs(edgeResult.Price) > Math.Abs(edgeResult.ContraPrice);
                    }
                }
            }
        }

        public async Task<bool> IsPriceBelowMid(bool preSubmit)
        {
            if ((_riskModel.DontTradeThroughMid && preSubmit) ||
                (_riskModel.AutoCancelWhenThroughMid && !preSubmit))
            {
                if (!MarkLoaded)
                {
                    await WaitForMarkLoad();
                }

                if (Legs.Count < 1)
                {
                    return false;
                }
                else if (IsSingleLeg)
                {
                    bool isSellOrder = Legs[0].Side == ZeroPlus.Models.Data.Enums.Side.Sell;
                    if (isSellOrder)
                    {
                        return Price >= Mid;
                    }
                    else
                    {
                        return Price <= Mid;
                    }
                }
                else
                {
                    return Price <= Mid;
                }
            }

            return true;
        }

        public async Task<bool> IsWithinPercentMarketCap()
        {
            if (!IsSingleLeg)
            {
                if (!(HighLoaded && LowLoaded))
                {
                    await Task.Run(async () =>
                    {
                        if (await WaitForHighLoad())
                        {
                            await WaitForLowLoadAsync();
                        }
                    });
                }

                if (!(HighLoaded && LowLoaded))
                {
                    return false;
                }

                double max = High - (Math.Abs(Low - High) * _riskModel.RiskCheckMarketPercentage);

                return Price <= Math.Round(max, 2);
            }
            else
            {
                return true;
            }
        }

        public async Task<bool> IsWithinMarketCap()
        {
            if (!_riskModel.OverrideEdgeCheck && _riskModel.DontTradeThroughMarketCap)
            {
                if (!(HighLoaded && LowLoaded))
                {
                    if (await WaitForHighLoad())
                    {
                        await WaitForLowLoadAsync();
                    }
                }

                if (Legs.Count < 1)
                {
                    return false;
                }
                else if (IsSingleLegSell)
                {
                    return Price >= Low;
                }
                else
                {
                    return Price <= High;
                }
            }
            return true;
        }

        public async Task<bool> IsPriceBelowEmaEdge(double edge, double price)
        {
            if (IsBasketOrder)
            {
                if (!EmaLoaded)
                {
                    await WaitForEmaLoad();
                }

                double evalPrice = CalculateEdgeToEma(edge, overrideEdge: false).Price;
                bool valid = IsSingleLegSell ? price >= evalPrice : price <= evalPrice;
                return valid;
            }
            return true;
        }

        public async Task<bool> IsPriceAboveBidPercent(double edge, bool useBestQuote = false)
        {
            if (IsBasketOrder)
            {
                double targetPrice;
                if (useBestQuote)
                {
                    if (!BestMarkLoaded)
                    {
                        await WaitForBestMarkLoad();
                    }
                    targetPrice = CalculateBestBidPercent(edge, overrideEdge: false).Price;
                }
                else
                {
                    if (!MarkLoaded)
                    {
                        await WaitForMarkLoad();
                    }
                    targetPrice = CalculateBidPercent(edge, overrideEdge: false).Price;
                }

                if (!double.IsNaN(targetPrice))
                {
                    return IsSingleLegSell ? Price <= targetPrice : Price >= targetPrice;
                }
            }
            return true;
        }

        public async Task<bool> IsPriceBelowBidPercent(double edge, bool useBestQuote = false)
        {
            if (IsBasketOrder)
            {
                double targetPrice;
                if (useBestQuote)
                {
                    if (!BestMarkLoaded)
                    {
                        await WaitForBestMarkLoad();
                    }
                    targetPrice = CalculateBestBidPercent(edge, overrideEdge: false).Price;
                }
                else
                {
                    if (!MarkLoaded)
                    {
                        await WaitForMarkLoad();
                    }
                    targetPrice = CalculateBidPercent(edge, overrideEdge: false).Price;
                }

                if (!double.IsNaN(targetPrice))
                {
                    return IsSingleLegSell ? Price >= targetPrice : Price <= targetPrice;
                }
            }
            return true;
        }

        public async Task<bool> IsPriceBelowDigBidPercent(double edge)
        {
            if (IsBasketOrder)
            {
                if (!DigLoaded) { await WaitForDigLoad(); }
                double targetPrice = CalculateDigBidPercent(edge, overrideEdge: false).Price;
                if (!double.IsNaN(targetPrice))
                {
                    return IsSingleLegSell ? Price >= targetPrice : Price <= targetPrice;
                }
            }
            return true;
        }

        public async Task<bool> IsPriceBelowEmaBidPercentEdge(double edge, double price)
        {
            if (IsBasketOrder)
            {
                if (!(LowLoaded && HighLoaded && BidEmaLoaded && AskEmaLoaded))
                {
                    if (await WaitForMarkLoad())
                    {
                        await WaitForEmaLoad();
                    }
                }

                double evalPrice = CalculateEdgeToBestOfEmaAndMktPercent(edge, overrideEdge: false).Price;
                bool valid = IsSingleLegSell ? price >= evalPrice : price <= evalPrice;
                return valid;
            }
            return true;
        }

        public async Task<bool> IsPriceBelowMinEmaWidthPercentEdgeToTheo(double percentage, double price)
        {
            if (IsBasketOrder)
            {
                if (!NetAdjTheoLoaded)
                {
                    await WaitForAdjTheoLoadAsync();
                }
                if (!AdjEmaLoaded)
                {
                    await WaitForAdjEmaLoad();
                }

                double edge = Math.Round((AskEmaAdj - BidEmaAdj) * percentage, 2);
                double evalPrice = CalculateEdgeToAdjTheo(NetDeltaAdjTheo, edge, overrideEdge: false).Price;
                bool valid = IsSingleLegSell ? price >= evalPrice : price <= evalPrice;
                return valid;
            }
            return true;
        }

        public async Task<bool> IsPriceBelowEmaBidAskEdge(double edgeToBid, double edgeToAsk)
        {
            if (IsBasketOrder)
            {
                if (!(LowLoaded && HighLoaded))
                {
                    if (await WaitForLowLoadAsync())
                    {
                        if (await WaitForHighLoad())
                        {
                            if (await WaitForBidEmaLoad())
                            {
                                await WaitForAskEmaLoadAsync();
                            }
                        }
                    }
                }
                EdgePriceCalculationResult edgePriceCalculationResult = CalculateEdgeToBestOfEmaAndMkt(edgeToBid, edgeToAsk, overrideEdge: false);
                bool valid = Price <= edgePriceCalculationResult.Price;
                bool reverseValid = ContraPrice >= edgePriceCalculationResult.ContraPrice;
                return valid && reverseValid;
            }
            return true;
        }

        protected virtual double CheckForEdgeOverride(double edge, bool overrideEdge)
        {
            if (overrideEdge)
            {
                if (!double.IsNaN(AdjustedEdgeOverride))
                {
                    edge = AdjustedEdgeOverride;
                }
                else if (!double.IsNaN(EdgeOverride))
                {
                    edge = EdgeOverride;
                }
                else if (!double.IsNaN(EdgeCurveAdjustment))
                {
                    edge += EdgeCurveAdjustment;
                }
            }

            return edge;
        }

        protected EdgePriceCalculationResult CalculateEdgeToTheo(double theo, double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            return GetCorrectEdgeForOrderType(theo, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToHistoricBest(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            double baseValue = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? BestEdgeBid : BestEdgeAsk;
            return GetCorrectEdgeForOrderType(baseValue, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToAdjTheo(double adjTheo, double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            return GetCorrectEdgeForOrderType(adjTheo, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToTheoToMarketSpread(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                edge += BidTheoSpreadEma;
            }
            else
            {
                edge += AskTheoSpreadEma;
            }

            return GetCorrectEdgeForOrderType(NetDeltaAdjTheo, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToLastFillAdjPx(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            double deltaAdjPx = double.NaN;

            double underMid = UnderMid;
            double totalDelta = TotalDelta;

            if (underMid != 0 && !double.IsNaN(underMid) &&
                totalDelta != 0 && !double.IsNaN(totalDelta) &&
                AveragePrice != 0 && !double.IsNaN(AveragePrice) &&
                LastMainUnderMidAtFill != 0 && !double.IsNaN(LastMainUnderMidAtFill))
            {
                deltaAdjPx = ((underMid - LastMainUnderMidAtFill) * totalDelta) + AveragePrice;
            }

            DeltaAdjPx = deltaAdjPx;

            _log.Info($"Delta Adj Last Fill. Under Mid: {UnderMid:N2}, Fill Under Mid: {LastMainUnderMidAtFill:N2}, Delta: {TotalDelta:N4}, Fill Px: {AveragePrice:N2} {GetStats()}");
            return GetCorrectEdgeForOrderType(deltaAdjPx, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToBestLastFillAdjPx(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            double deltaAdjBestPx = double.NaN;

            double underMid = UnderMid;
            double totalDelta = TotalDelta;

            if (underMid != 0 && !double.IsNaN(underMid) &&
                totalDelta != 0 && !double.IsNaN(totalDelta) &&
                BestAveragePrice != 0 && !double.IsNaN(BestAveragePrice) &&
                LastMainUnderMidAtBestFill != 0 && !double.IsNaN(LastMainUnderMidAtBestFill))
            {
                deltaAdjBestPx = ((underMid - LastMainUnderMidAtBestFill) * totalDelta) + BestAveragePrice;
            }

            _log.Info($"Delta Adj Last Fill. Under Mid: {underMid:N2}, Best Fill Under Mid: {LastMainUnderMidAtBestFill:N2}, Delta: {totalDelta:N4}, Best Fill Px: {BestAveragePrice:N2} {GetStats()}");
            return GetCorrectEdgeForOrderType(deltaAdjBestPx, edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToMid(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (!update.HasError)
            {
                return GetCorrectEdgeForOrderType(update.Mid, edge);
            }
            else
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }
        }

        protected EdgePriceCalculationResult CalculateEdgeToEma(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            return GetCorrectEdgeForOrderType(GetEma(OmsCore.Config.PerformanceModeEnabled), edge);
        }

        protected EdgePriceCalculationResult CalculateEdgeToBestOfEmaAndMkt(double edgeToBid, double edgeToAsk, bool overrideEdge = true)
        {
            edgeToBid = CheckForEdgeOverride(edgeToBid, overrideEdge);
            edgeToAsk = CheckForEdgeOverride(edgeToAsk, overrideEdge);

            double bidEma = BidIvEma;
            double askEma = AskIvEma;

            AutomationConfigModel automationConfigModel = GetAutomationConfig();

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var bid = update.Low;
            var ask = update.High;

            double minBid = Math.Min(bidEma, bid);
            double maxAsk = Math.Max(askEma, ask);

            Offset = 0.0;
            StrikeOffset = 0.0;

            double price = minBid - edgeToBid - Offset;
            double contraPrice = maxAsk + edgeToAsk - Offset;

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Info("Calculate Edge using Market and EMA. Spread: " + SpreadId +
                      ", Bid:" + bid +
                      ", Bid EMA:" + BidIvEma +
                      ", Ask:" + ask +
                      ", Ask EMA:" + AskIvEma +
                      ", Net Pos:" + BasketSettings.NetPos +
                      ", Position:" + LcdPosition +
                      ", Offset:" + Offset +
                      ", Strike offset:" + StrikeOffset +
                      ", Bid Px:" + edgePriceCalculationResult.Price +
                      ", Ask Px:" + edgePriceCalculationResult.ContraPrice +
                      ", Server Creep: " + BasketTraderViewModel?.ServerCreep);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToBestOfEmaAndMktPercent(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            AutomationConfigModel automationConfigModel = GetAutomationConfig();

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var bid = update.Low;
            var ask = update.High;

            double selectedBid = Math.Max(BidEmaAdj, bid);
            double selectedAsk = Math.Min(AskEmaAdj, ask);

            double price = selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            double contraPrice = selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Debug("Calculate Ema Bid Percent. Quote: [{0}X{1}], Target: {2}, Result: [{3}X{4}]",
                selectedBid.ToString("N2"), selectedAsk.ToString("N2"), bidPercent.ToString("P0"),
                edgePriceCalculationResult.Price.ToString("C2"), edgePriceCalculationResult.ContraPrice.ToString("C2"));

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToBid(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (!update.HasError)
            {
                return GetCorrectEdgeForOrderType(update.Low, edge);
            }
            else
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }
        }

        protected EdgePriceCalculationResult CalculatePermAdjPrice(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            var price = PermAdjPx;
            var contraPrice = IsSingleLeg ? PermAdjContraPx : -PermAdjContraPx;
            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, edge);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateCustomEdgePrice()
        {
            EdgePriceCalculationResult edgePriceCalculationResult = new()
            {
                Price = double.NaN,
                ContraPrice = double.NaN
            };

            try
            {
                string basketSettingsCustomFunctionEdgeFormula = BasketSettings.CustomFunctionEdgeFormula?.Replace("Power(", "Pow(")?.Replace("iif(", "if(");
                NCalc.Expression expression = new(basketSettingsCustomFunctionEdgeFormula);

                expression.Parameters["Delta"] = TotalDelta;
                expression.Parameters["Gamma"] = TotalGamma;
                expression.Parameters["Vega"] = TotalVega;
                expression.Parameters["Rho"] = TotalRho;
                expression.Parameters["IV"] = TotalImplied;
                expression.Parameters["HwTheo"] = NetTheo;
                expression.Parameters["AdjTheo"] = NetDeltaAdjTheo;
                expression.Parameters["BidEma"] = BidEmaAdj;
                expression.Parameters["MidEma"] = FullEma;
                expression.Parameters["AskEma"] = AskEmaAdj;
                expression.Parameters["Bid"] = Low;
                expression.Parameters["Mid"] = Mid;
                expression.Parameters["Ask"] = High;
                expression.Parameters["UnderBid"] = UnderBid;
                expression.Parameters["UnderMid"] = UnderMid;
                expression.Parameters["UnderAsk"] = UnderAsk;
                expression.Parameters["DTE"] = DaysToExpiration;
                expression.Parameters["CallPut"] = Leg1.Type;
                expression.Parameters["Strike"] = Leg1.Strike.Strike;

                var log = string.Join(", ", expression.Parameters.Select(x => x.Key + ":" + x.Value));
                _log.Info(nameof(CalculateCustomEdgePrice) + ". Exp: " + basketSettingsCustomFunctionEdgeFormula + ", Par: " + log);

                object calcPx = expression.Evaluate();
                bool valid = false;
                double price = double.NaN;

                if (calcPx is double doublePx)
                {
                    price = doublePx;
                    valid = true;
                }
                else if (calcPx is int intPx)
                {
                    price = intPx;
                    valid = true;
                }

                if (!valid)
                {
                    Reason = $"[RISK] EVAL. Failed: {calcPx}, Mkt: [{Low}X{High}]";
                }
                else
                {
                    if (IsSingleLegSell)
                    {
                        if (price >= Mid)
                        {
                            if (price < High)
                            {
                                edgePriceCalculationResult.Price = price;
                                Reason = $"[DONE] EVAL. Failed: {price}, Mkt: [{Low}X{High}]";
                            }
                            else
                            {
                                edgePriceCalculationResult.Price = High;
                                Reason = $"[RISK] EVAL. Px: {price}, Mkt: [{Low}X{High}]";
                            }
                        }
                        else
                        {
                            edgePriceCalculationResult.Price = Mid;
                            Reason = $"[RISK] EVAL. Px: {price}, Mkt: [{Low}X{High}]";
                        }
                    }
                    else
                    {
                        if (price <= Mid)
                        {
                            if (price > Low)
                            {
                                edgePriceCalculationResult.Price = price;
                                Reason = $"[DONE] EVAL. Failed: {price}, Mkt: [{Low}X{High}]";
                            }
                            else
                            {
                                edgePriceCalculationResult.Price = Low;
                                Reason = $"[RISK] EVAL. Px: {price}, Mkt: [{Low}X{High}]";
                            }
                        }
                        else
                        {
                            edgePriceCalculationResult.Price = Mid;
                            Reason = $"[RISK] EVAL. Px: {price}, Mkt: [{Low}X{High}]";
                        }
                    }
                }

                _log.Info(nameof(CalculateCustomEdgePrice) + ". Exp: " + basketSettingsCustomFunctionEdgeFormula + ", Res: " + calcPx + ", Par: " + log);
            }
            catch (Exception ex)
            {
                _log.Info(ex, nameof(CalculateCustomEdgePrice));
            }

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToTheoAndMid(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var mid = update.Mid;

            double price = Math.Min(NetTheo, mid);
            double contraPrice = Math.Max(NetTheo, mid);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToTheoStopMid(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var mid = update.Mid;

            double price = NetTheo - edge;
            if (price > mid)
            {
                price = mid;
            }

            double contraPrice = NetTheo + edge;
            if (contraPrice < mid)
            {
                contraPrice = mid;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToEmaStopMid(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);

            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var mid = update.Mid;

            double price = ema - edge;
            if (price > mid)
            {
                price = mid;
            }

            double contraPrice = ema + edge;
            if (contraPrice < mid)
            {
                contraPrice = mid;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEdgeToMidStopEma(double edge, bool overrideEdge = true)
        {
            edge = CheckForEdgeOverride(edge, overrideEdge);
            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var mid = update.Mid;

            double price = mid - edge;
            if (price > ema)
            {
                price = ema;
            }

            double contraPrice = mid + edge;
            if (contraPrice < ema)
            {
                contraPrice = ema;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateBidPercentStopEma(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var selectedBid = update.Low;
            var selectedAsk = update.High;

            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            double price = selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            if (price > ema)
            {
                price = ema;
            }

            double contraPrice = selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            if (contraPrice < ema)
            {
                contraPrice = ema;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateBidPercentStopEmaStopTheo(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var selectedBid = update.Low;
            var selectedAsk = update.High;

            double price = selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            if (price > ema)
            {
                price = ema;
            }
            if (price > NetDeltaAdjTheo)
            {
                price = NetDeltaAdjTheo;
            }

            double contraPrice = selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            if (contraPrice < ema)
            {
                contraPrice = ema;
            }
            if (contraPrice < NetDeltaAdjTheo)
            {
                contraPrice = NetDeltaAdjTheo;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateEmaBidPercentStopEmaStopTheo(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            double price = MarketBidEma + (Math.Abs(MarketBidEma - MarketAskEma) * bidPercent);
            if (price > ema)
            {
                price = ema;
            }
            if (price > NetDeltaAdjTheo)
            {
                price = NetDeltaAdjTheo;
            }

            double contraPrice = MarketAskEma - (Math.Abs(MarketBidEma - MarketAskEma) * bidPercent);
            if (contraPrice < ema)
            {
                contraPrice = ema;
            }
            if (contraPrice < NetDeltaAdjTheo)
            {
                contraPrice = NetDeltaAdjTheo;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult CalculateDerivedBidPercentStopEmaStopMid(double derivedBidPercent, bool overrideEdge = true)
        {
            derivedBidPercent = CheckForEdgeOverride(derivedBidPercent, overrideEdge);

            double ema = GetEma(OmsCore.Config.PerformanceModeEnabled);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var mid = update.Mid;

            double price = LowDerived + (Math.Abs(LowDerived - HighDerived) * derivedBidPercent);
            if (price > ema)
            {
                price = ema;
            }
            if (price > mid)
            {
                price = mid;
            }

            double contraPrice = HighDerived - (Math.Abs(LowDerived - HighDerived) * derivedBidPercent);
            if (contraPrice < ema)
            {
                contraPrice = ema;
            }
            if (contraPrice < mid)
            {
                contraPrice = mid;
            }

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            return edgePriceCalculationResult;
        }

        internal EdgePriceCalculationResult CalculateBidPercent(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var selectedBid = update.Low;
            var selectedAsk = update.High;

            if (IsBasketOrder)
            {
                if (IsSingleLeg && BasketTraderViewModel.BasketSettings.DataType == DataType.Live && Legs[0].LastDoubleUpdateModel != null && Legs[0].Type == "STOCK")
                {
                    DoubleUpdateModel doubleUpdateModel = Legs[0].LastDoubleUpdateModel;
                    selectedBid = doubleUpdateModel.Bid;
                    selectedAsk = doubleUpdateModel.Ask;
                }
            }

            double price = Math.Round(selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);
            double contraPrice = Math.Round(selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Debug("Calculate Bid Percent. Quote: [{0}X{1}], Target: {2}, Result: [{3}X{4}]",
                selectedBid.ToString("N2"), selectedAsk.ToString("N2"), bidPercent.ToString("P0"),
                edgePriceCalculationResult.Price.ToString("C2"), edgePriceCalculationResult.ContraPrice.ToString("C2"));

            return edgePriceCalculationResult;
        }

        internal EdgePriceCalculationResult CalculateDigBidPercent(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            TicketValues update = new();
            CalculateUpdates(update, _calcLegs, false);

            if (update.HasError)
            {
                return new EdgePriceCalculationResult()
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            var selectedBid = update.DigBid;
            var selectedAsk = update.DigAsk;

            double price = Math.Round(selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);
            double contraPrice = Math.Round(selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Debug("Calculate Dig Bid Percent. Quote: [{0}X{1}], Target: {2}, Result: [{3}X{4}]",
                selectedBid.ToString("N2"), selectedAsk.ToString("N2"), bidPercent.ToString("P0"),
                edgePriceCalculationResult.Price.ToString("C2"), edgePriceCalculationResult.ContraPrice.ToString("C2"));

            return edgePriceCalculationResult;
        }

        internal EdgePriceCalculationResult CalculateTheoBidPercent(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            var selectedBid = TheoBid;
            var selectedAsk = TheoAsk;

            if (double.IsNaN(selectedBid) || double.IsNaN(selectedAsk))
            {
                return new EdgePriceCalculationResult
                {
                    Price = double.NaN,
                    ContraPrice = double.NaN
                };
            }

            double price = Math.Round(selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);
            double contraPrice = Math.Round(selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent), 2, MidpointRounding.AwayFromZero);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Debug("Calculate Theo Bid Percent. Theo: [{0}X{1}], Target: {2}, Result: [{3}X{4}]",
                selectedBid.ToString("N2"), selectedAsk.ToString("N2"), bidPercent.ToString("P0"),
                edgePriceCalculationResult.Price.ToString("C2"), edgePriceCalculationResult.ContraPrice.ToString("C2"));

            return edgePriceCalculationResult;
        }

        internal EdgePriceCalculationResult CalculateBestBidPercent(double bidPercent, bool overrideEdge = true)
        {
            bidPercent = CheckForEdgeOverride(bidPercent, overrideEdge);

            double selectedBid = BestBidInt;
            double selectedAsk = BestAskInt;

            double price = selectedBid + (Math.Abs(selectedBid - selectedAsk) * bidPercent);
            double contraPrice = selectedAsk - (Math.Abs(selectedBid - selectedAsk) * bidPercent);

            EdgePriceCalculationResult edgePriceCalculationResult = GetCorrectEdgeForOrderType(price, contraPrice, 0);

            _log.Debug("Calculate Best Bid Percent. Quote: [{0}X{1}], Target: {2}, Result: [{3}X{4}]",
                selectedBid.ToString("N2"), selectedAsk.ToString("N2"), bidPercent.ToString("P0"),
                edgePriceCalculationResult.Price.ToString("C2"), edgePriceCalculationResult.ContraPrice.ToString("C2"));

            return edgePriceCalculationResult;
        }

        protected EdgePriceCalculationResult GetCorrectEdgeForOrderType(double edgeBase, double edge, bool round = true)
        {
            EdgePriceCalculationResult edgePriceCalculationResult = new();
            if (IsSingleLeg)
            {
                double price = edgeBase - edge;
                double reversePrice = edgeBase + edge;
                bool buySide = Side == ZeroPlus.Models.Data.Enums.Side.Buy;

                if (round)
                {
                    price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: buySide) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                    reversePrice = PriceNeedsPadding(reversePrice) ? PadForNickelOrDime(reversePrice, floor: !buySide) : Math.Round(reversePrice, 2, MidpointRounding.AwayFromZero);
                }

                if (buySide)
                {
                    edgePriceCalculationResult.Price = price;
                    edgePriceCalculationResult.ContraPrice = reversePrice;
                }
                else
                {
                    edgePriceCalculationResult.Price = reversePrice;
                    edgePriceCalculationResult.ContraPrice = price;
                }
            }
            else if (Legs.Count > 1)
            {
                double price = edgeBase - edge;
                double reversePrice = -(edgeBase + edge);

                if (IsStockTied)
                {
                    if (StockLeg != null)
                    {
                        switch (StockLeg.Side)
                        {
                            case ZeroPlus.Models.Data.Enums.Side.Buy:
                                price += Math.Round(StockLeg.Bid * StockLeg.Ratio / Multiplier, 2);
                                reversePrice -= Math.Round(StockLeg.Ask * StockLeg.Ratio / Multiplier, 2);
                                break;
                            case ZeroPlus.Models.Data.Enums.Side.Sell:
                                price -= Math.Round(StockLeg.Ask * StockLeg.Ratio / Multiplier, 2);
                                reversePrice += Math.Round(StockLeg.Bid * StockLeg.Ratio / Multiplier, 2);
                                break;
                            default:
                                price = double.NaN;
                                reversePrice = double.NaN;
                                break;
                        }
                    }
                    else
                    {
                        price = double.NaN;
                        reversePrice = double.NaN;
                    }
                }

                if (round)
                {
                    price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                    reversePrice = PriceNeedsPadding(reversePrice) ? PadForNickelOrDime(reversePrice, floor: true) : Math.Round(reversePrice, 2, MidpointRounding.AwayFromZero);
                }

                edgePriceCalculationResult.Price = price;
                edgePriceCalculationResult.ContraPrice = reversePrice;
            }
            else
            {
                edgePriceCalculationResult.Price = double.NaN;
                edgePriceCalculationResult.ContraPrice = double.NaN;
            }

            return edgePriceCalculationResult;
        }

        private EdgePriceCalculationResult GetCorrectEdgeForOrderType(double edgeBase, double contraEdgeBase, double edge)
        {
            EdgePriceCalculationResult edgePriceCalculationResult = new();
            if (IsSingleLeg)
            {
                double price = edgeBase - edge;
                double reversePrice = contraEdgeBase + edge;

                bool buySide = Side == ZeroPlus.Models.Data.Enums.Side.Buy;

                price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: buySide) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                reversePrice = PriceNeedsPadding(reversePrice) ? PadForNickelOrDime(reversePrice, floor: !buySide) : Math.Round(reversePrice, 2, MidpointRounding.AwayFromZero);

                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    edgePriceCalculationResult.Price = price;
                    edgePriceCalculationResult.ContraPrice = reversePrice;
                }
                else
                {
                    edgePriceCalculationResult.Price = reversePrice;
                    edgePriceCalculationResult.ContraPrice = price;
                }
            }
            else if (Legs.Count > 1)
            {
                double price = edgeBase - edge;
                double reversePrice = -(contraEdgeBase + edge);

                if (IsStockTied)
                {
                    if (StockLeg != null)
                    {
                        switch (StockLeg.Side)
                        {
                            case ZeroPlus.Models.Data.Enums.Side.Buy:
                                price += Math.Round(StockLeg.Bid * StockLeg.Ratio / Multiplier, 2);
                                reversePrice -= Math.Round(StockLeg.Ask * StockLeg.Ratio / Multiplier, 2);
                                break;
                            case ZeroPlus.Models.Data.Enums.Side.Sell:
                                price -= Math.Round(StockLeg.Ask * StockLeg.Ratio / Multiplier, 2);
                                reversePrice += Math.Round(StockLeg.Bid * StockLeg.Ratio / Multiplier, 2);
                                break;
                            default:
                                price = double.NaN;
                                reversePrice = double.NaN;
                                break;
                        }
                    }
                    else
                    {
                        price = double.NaN;
                        reversePrice = double.NaN;
                    }
                }

                price = PriceNeedsPadding(price) ? PadForNickelOrDime(price, floor: true) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                reversePrice = PriceNeedsPadding(reversePrice) ? PadForNickelOrDime(reversePrice, floor: true) : Math.Round(reversePrice, 2, MidpointRounding.AwayFromZero);

                edgePriceCalculationResult.Price = price;
                edgePriceCalculationResult.ContraPrice = reversePrice;
            }
            else
            {
                edgePriceCalculationResult.Price = double.NaN;
                edgePriceCalculationResult.ContraPrice = double.NaN;
            }

            return edgePriceCalculationResult;
        }

        public void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            if (IsMainOrder(orderCancelReject))
            {
                CanReplace = false;
            }
            else if (IsContraOrder(orderCancelReject))
            {
                CanReplaceContra = false;
            }
            Reason = orderCancelReject.Comment;
            ShowMessage($"Order Cancel Rejected: {orderCancelReject.Comment}", "Order Cancel Rejected");
            _log.Info("Order Cancel Rejected: " + orderCancelReject.Comment +
                      ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
        }

        protected async Task CheckForLoopAsync(DateTime receiveTime, double openingFee, double closingFee, double loopMinEdge, string lastExchange)
        {
            if (IsBasketOrder)
            {
                if (Looper.IcebergRunning)
                {
                    if (Looper.IcebergTotalQty > 0)
                    {
                        Looper.StartClosingLoop(receiveTime);
                    }
                    return;
                }
            }

            double fees = openingFee + closingFee;

            double lastRealEdgeBeforeFees = LastEdge;
            double lastRealEdgeAfterFees = lastRealEdgeBeforeFees - fees;

            double lastAdjEdgeBeforeFees = DeltaAdjLastEdge;
            double lastAdjEdgeAfterFees = lastAdjEdgeBeforeFees - fees;

            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (automationConfigModel.LoopPricingMode == LoopPricingMode.PriceIncrement)
            {
                if (lastRealEdgeAfterFees >= loopMinEdge)
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Edge acquired starting looper. " +
                        $"Id: {SpreadId}, " +
                        $"Best Edge: {BestEdge:f2}, " +
                        $"Last Edge: {LastEdge:f2}, " +
                        $"Adj Last Edge: {DeltaAdjLastEdge:f2}, " +
                        $"Opening Fee: {openingFee:f2}, " +
                        $"Closing Fee: {closingFee:f2}, " +
                        $"Loop Min Edge: {loopMinEdge:f2}, " +
                        $"Last Fill Px: {LastFillPx:f2}, " +
                        $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");

                    if (automationConfigModel.LoopingEnabled &&
                        automationConfigModel.LooperDynamicRouting &&
                        automationConfigModel.ExchToRouteMap != null &&
                        automationConfigModel.ExchToRouteMap.TryGetValue(lastExchange, out var routeMap))
                    {
                        LastLoopContraRoute = routeMap;
                    }
                    else
                    {
                        LastLoopContraRoute = null;
                    }

                    if (automationConfigModel.RequireAdjEdgeForSizeUp && lastAdjEdgeAfterFees < loopMinEdge)
                    {
                        ResetSize = false;
                        UpdateQty(1);
                    }
                    else if (!Looper.SizeUpLocked)
                    {
                        Looper.LoopResubmitWithPrevSize = await CheckLoopSizeUpAsync(lastRealEdgeBeforeFees, savePrevSize: true, allowReverse: true);
                    }
                    Looper.StartLoop(receiveTime);
                }
                else if ((lastRealEdgeAfterFees > 0 || (automationConfigModel.FreeLookOnLosers && ++_totalLoserFreeLook <= automationConfigModel.FreeLookOnLosersMax)) && automationConfigModel.LoopFreeLook)
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Edge below min setting, but freelook range. " +
                        $"Id: {SpreadId}, " +
                        $"Best Edge: {BestEdge:f2}, " +
                        $"Last Edge: {LastEdge:f2}, " +
                        $"Adj Last Edge: {DeltaAdjLastEdge:f2}, " +
                        $"Opening Fee: {openingFee:f2}, " +
                        $"Closing Fee: {closingFee:f2}, " +
                        $"Loop Min Edge: {loopMinEdge:f2}, " +
                        $"Last Fill Px: {LastFillPx:f2}, " +
                        $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Loser Free Look Count: {_totalLoserFreeLook}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");
                    if (automationConfigModel.RequireAdjEdgeForSizeUp && lastAdjEdgeAfterFees < loopMinEdge)
                    {
                        ResetSize = false;
                        UpdateQty(1);
                    }
                    else if (!Looper.SizeUpLocked)
                    {
                        Looper.LoopResubmitWithPrevSize = await CheckLoopSizeUpAsync(lastRealEdgeBeforeFees, savePrevSize: true, allowReverse: true);
                    }
                    Looper.StartLoop(receiveTime, isRecon: true, skipFreeLookAll: lastRealEdgeAfterFees <= 0);
                }
                else
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Edge below min setting. " +
                                            $"Id: {SpreadId}, " +
                                            $"Best Edge: {BestEdge:f2}, " +
                                            $"Last Edge: {lastRealEdgeBeforeFees}, " +
                                            $"Adj Last Edge: {lastAdjEdgeBeforeFees}, " +
                                            $"Last Edge W/ Fees: {lastRealEdgeAfterFees}, " +
                                            $"Adj Last Edge W/ Fees: {lastAdjEdgeAfterFees}, " +
                                            $"Total Fee: {fees}, " +
                                            $"Opening Fee: {openingFee}, " +
                                            $"Closing Fee: {closingFee}, " +
                                            $"Loop Min Edge: {loopMinEdge}, " +
                                            $"Free Look: {automationConfigModel.LoopFreeLook}, " +
                                            $"Mode: {automationConfigModel.LoopPricingMode}, " +
                                            $"Last Fill Px: {LastFillPx:f2}, " +
                                            $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                                            $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                                            $"Last Exch: {LastExchange}, " +
                                            $"Exchanges: {Exchanges}, " +
                                            $"Last Contra Exch: {LastContraExchange}");
                    StopLooper(openingFee, closingFee);
                }
            }
            else
            {
                if (lastAdjEdgeAfterFees >= loopMinEdge)
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Adj Edge satisfies min loop edge. " +
                        $"Id: {SpreadId}, " +
                        $"Best Edge: {BestEdge:f2}, " +
                        $"Last Edge: {LastEdge:f2}, " +
                        $"Adj Last Edge: {DeltaAdjLastEdge:f2}, " +
                        $"Opening Fee: {openingFee:f2}, " +
                        $"Closing Fee: {closingFee:f2}, " +
                        $"Loop Min Edge: {loopMinEdge:f2}, " +
                        $"Last Fill Px: {LastFillPx:f2}, " +
                        $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");

                    if (lastRealEdgeAfterFees >= loopMinEdge)
                    {
                        BelowEdgeResubmitCounter = 0;

                        if (automationConfigModel.LoopingEnabled &&
                            automationConfigModel.LooperDynamicRouting &&
                            automationConfigModel.ExchToRouteMap != null &&
                            automationConfigModel.ExchToRouteMap.TryGetValue(lastExchange, out var routeMap))
                        {
                            LastLoopContraRoute = routeMap;
                        }
                        else
                        {
                            LastLoopContraRoute = null;
                        }

                        if (!Looper.SizeUpLocked)
                        {
                            Looper.LoopResubmitWithPrevSize = await CheckLoopSizeUpAsync(lastAdjEdgeBeforeFees, savePrevSize: true, allowReverse: true);
                        }
                        Looper.StartLoop(receiveTime);
                    }
                    else
                    {
                        if (BelowEdgeResubmitCounter++ < automationConfigModel.MaxBelowEdgeResubmit)
                        {
                            if (automationConfigModel.LoopingEnabled &&
                                automationConfigModel.LooperDynamicRouting &&
                                automationConfigModel.ExchToRouteMap != null &&
                                automationConfigModel.ExchToRouteMap.TryGetValue(lastExchange, out var routeMap))
                            {
                                LastLoopContraRoute = routeMap;
                            }
                            else
                            {
                                LastLoopContraRoute = null;
                            }

                            ResetSize = false;
                            UpdateQty(1);
                            Looper.StartLoop(receiveTime);
                        }
                        else
                        {
                            BelowEdgeResubmitCounter = 0;
                            StopLooper(openingFee, closingFee);
                        }
                    }
                }
                else if ((lastAdjEdgeAfterFees > 0 || (automationConfigModel.FreeLookOnLosers && ++_totalLoserFreeLook <= automationConfigModel.FreeLookOnLosersMax)) && automationConfigModel.LoopFreeLook)
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Adj Edge below min setting, but freelook range. " +
                        $"Id: {SpreadId}, " +
                        $"Best Edge: {BestEdge:f2}, " +
                        $"Last Edge: {LastEdge:f2}, " +
                        $"Adj Last Edge: {DeltaAdjLastEdge:f2}, " +
                        $"Opening Fee: {openingFee:f2}, " +
                        $"Closing Fee: {closingFee:f2}, " +
                        $"Loop Min Edge: {loopMinEdge:f2}, " +
                        $"Last Fill Px: {LastFillPx:f2}, " +
                        $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Loser Free Look Count: {_totalLoserFreeLook}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");

                    if (lastRealEdgeAfterFees >= 0 ||
                        BelowEdgeResubmitCounter++ < automationConfigModel.MaxBelowEdgeResubmit)
                    {
                        ResetSize = false;
                        UpdateQty(1);
                        Looper.LoopResubmitWithPrevSize = ResubmitSizeOption.Off;
                        Looper.StartLoop(receiveTime, isRecon: true, skipFreeLookAll: lastRealEdgeAfterFees <= 0);
                    }
                    else
                    {
                        BelowEdgeResubmitCounter = 0;
                        StopLooper(openingFee, closingFee);
                    }
                }
                else
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Edge below min setting. " +
                                            $"Id: {SpreadId}, " +
                                            $"Best Edge: {BestEdge:f2}, " +
                                            $"Last Edge: {lastRealEdgeBeforeFees}, " +
                                            $"Adj Last Edge: {lastAdjEdgeBeforeFees}, " +
                                            $"Last Edge W/ Fees: {lastRealEdgeAfterFees}, " +
                                            $"Adj Last Edge W/ Fees: {lastAdjEdgeAfterFees}, " +
                                            $"Total Fee: {fees}, " +
                                            $"Opening Fee: {openingFee}, " +
                                            $"Closing Fee: {closingFee}, " +
                                            $"Loop Min Edge: {loopMinEdge}, " +
                                            $"Free Look: {automationConfigModel.LoopFreeLook}, " +
                                            $"Mode: {automationConfigModel.LoopPricingMode}, " +
                                            $"Last Fill Px: {LastFillPx:f2}, " +
                                            $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                                            $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                                            $"Last Exch: {LastExchange}, " +
                                            $"Exchanges: {Exchanges}, " +
                                            $"Last Contra Exch: {LastContraExchange}");
                    StopLooper(openingFee, closingFee);
                }
            }

            if (BasketSettings.LoggingEnabled && !IsSingleLeg && lastRealEdgeAfterFees > BasketSettings.MinEdgeForLogging)
            {
                SetupCobLogger();
            }
        }

        private void SetupCobLogger()
        {
            LogUpdates = true;
            SubscribeToIbCommand();
            Task.Delay(BasketSettings.LoggingTimespan)
                .ContinueWith(t =>
                {
                    UnsubscribeIbDataCommand();
                    LogUpdates = false;
                });
        }

        private void StopLooper(double openingFee, double closingFee)
        {
            _log.Info($"{nameof(HandleExecutionReport)} Edge below min setting. " +
                                    $"Id: {SpreadId}, " +
                                    $"Best Edge: {BestEdge:f2}, " +
                                    $"Last Edge: {LastEdge:f2}, " +
                                    $"Adj Last Edge: {DeltaAdjLastEdge:f2}, " +
                                    $"Opening Fee: {openingFee:f2}, " +
                                    $"Closing Fee: {closingFee:f2}, " +
                                    $"Loop Min Edge: {GetLoopMinEdge():f2}, " +
                                    $"Last Fill Px: {LastFillPx:f2}, " +
                                    $"Last Contra Fill Px: {LastContraFillPx:f2}, " +
                                    $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                                    $"Last Exch: {LastExchange}, " +
                                    $"Exchanges: {Exchanges}, " +
                                    $"Last Contra Exch: {LastContraExchange}");
            double fees = openingFee + closingFee;
            int size = Lcd;
            ResetLoopSize();
            LastEdge = double.NaN;
            DeltaAdjLastEdge = double.NaN;

            CheckToEnableNag(size);
            NotifyOrderCloseWaitHandlers(true, null);

            IsLooping = false;
            Looper.IcebergRunning = false;
            Looper.RemoveFromLoopInstances();

            if (LastEdge - fees < 0)
            {
                OnLoss();
            }
        }

        protected virtual void OnLoss()
        {
        }

        private void UpdateRoutes(out string route, out string contraRoute)
        {
            if (!TrySelectRoute(isContra: false, lookupOnly: true, out route, out _))
            {
                route = Route;
            }
            if (!TrySelectRoute(isContra: true, lookupOnly: true, out contraRoute, out _))
            {
                contraRoute = string.IsNullOrWhiteSpace(ContraRoute) ? Route : ContraRoute;
            }
        }

        protected void SetLastEdge()
        {
            double underMid = UnderMid;
            double totalDelta = TotalDelta;
            double contraDelta = IsSingleLeg ? TotalDelta : -TotalDelta;

            double changeInBuy = underMid > 0 && LastMainUnderMidAtFill > 0 ? (underMid - LastMainUnderMidAtFill) * totalDelta : double.NaN;
            double adjLastFillPx = LastFillPx + changeInBuy;

            double changeInSell = underMid > 0 && LastContraUnderMidAtFill > 0 ? (underMid - LastContraUnderMidAtFill) * contraDelta : double.NaN;
            double adjLastContraFillPx = LastContraFillPx + changeInSell;

            double lastFillPx = LastFillPx;
            double lastContraFillPx = LastContraFillPx;

            SetLastEdge(lastFillPx, lastContraFillPx, adjLastFillPx, adjLastContraFillPx);
        }

        protected void SetLastEdge(double lastFillPx, double lastContraFillPx, double adjLastFillPx, double adjLastContraFillPx)
        {
            if (lastContraFillPx > 0 && lastFillPx > 0 && IsSingleLeg)
            {
                if (!Side.HasValue)
                {
                    _log.Info($"{nameof(HandleExecutionReport)} Invalid edge. " +
                        $"Id: {SpreadId}, " +
                        $"Last Fill Px: {lastFillPx:f2}, " +
                        $"Last Contra Fill Px: {lastContraFillPx:f2}, " +
                        $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                        $"Last Exch: {LastExchange}, " +
                        $"Exchanges: {Exchanges}, " +
                        $"Last Contra Exch: {LastContraExchange}");
                    LastEdge = double.NaN;
                    DeltaAdjLastEdge = double.NaN;
                    IsLooping = false;
                    Looper.RemoveFromLoopInstances();
                }
                else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    LastEdge = lastContraFillPx - lastFillPx;
                    DeltaAdjLastEdge = adjLastContraFillPx - adjLastFillPx;
                }
                else
                {
                    LastEdge = lastFillPx - lastContraFillPx;
                    DeltaAdjLastEdge = adjLastFillPx - adjLastContraFillPx;
                }
            }
            else if (lastContraFillPx < 0 && lastFillPx > 0)
            {
                LastEdge = Math.Abs(lastContraFillPx) - lastFillPx;
                DeltaAdjLastEdge = Math.Abs(adjLastContraFillPx) - adjLastFillPx;
            }
            else if (lastFillPx < 0 && lastContraFillPx > 0)
            {
                LastEdge = Math.Abs(lastFillPx) - lastContraFillPx;
                DeltaAdjLastEdge = Math.Abs(adjLastFillPx) - adjLastContraFillPx;
            }
            else if (lastFillPx < 0 && lastContraFillPx < 0)
            {
                LastEdge = Math.Abs(lastFillPx + lastContraFillPx);
                DeltaAdjLastEdge = Math.Abs(adjLastFillPx + adjLastContraFillPx);
            }
            else
            {
                _log.Info($"{nameof(HandleExecutionReport)} Invalid edge. " +
                    $"Id: {SpreadId}, " +
                    $"Last Fill Px: {lastFillPx:f2}, " +
                    $"Last Contra Fill Px: {lastContraFillPx:f2}, " +
                    $"Latency Timer: {_latencyTimer.ElapsedMilliseconds}, " +
                    $"Last Exch: {LastExchange}, " +
                    $"Exchanges: {Exchanges}, " +
                    $"Last Contra Exch: {LastContraExchange}");
                ResetLoopSize();
                LastEdge = double.NaN;
                DeltaAdjLastEdge = double.NaN;
                IsLooping = false;
                Looper.RemoveFromLoopInstances();
            }
            UpdateNotionalEdge();
        }

        private void UpdateNotionalEdge()
        {
            if (Legs.Count > 0)
            {
                double multiplier = 1;
                string symbol = Legs[0].Symbol;
                if (symbol != null && Legs[0].Symbol.Length > 0 && Legs[0].Symbol[0] == '.')
                {
                    multiplier = 100;
                }
                DeltaAdjLastEdgeNotional = DeltaAdjLastEdge * Lcd * multiplier;
            }
            else
            {
                DeltaAdjLastEdgeNotional = double.NaN;
            }

            if (!double.IsNaN(LastEdge))
            {
                NotionalLastEdge = LastEdge;
                if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    LastBuyEdge = LastEdge;
                }
                else
                {
                    LastSellEdge = LastEdge;
                }
            }

            if ((LastEdge > 0 && double.IsNaN(BestEdge)) ||
                 LastEdge > BestEdge)
            {
                BestEdge = LastEdge;
                BestAveragePrice = AveragePrice;
                LastMainUnderMidAtBestFill = LastMainUnderMidAtFill;
            }
        }

        protected void CheckToEnableNag(int size)
        {
            if (!NagEnabled)
            {
                if ((BasketSettings.NagbotMinEdgeForSizeEnabled && size > 1 && BestEdge > BasketSettings.NagbotMinEdgeForSize) ||
                    (BasketSettings.NagbotMinEdgeEnabled && BestEdge > BasketSettings.NagbotMinEdge))
                {
                    NextNagTime = default;
                    CurrentNagInterval = 0;
                    NagEnabled = true;
                }
            }
        }

        internal bool TryGetPriceCache(out PriceCache priceCache)
        {
            return _priceCacheManager.TryGetValue(SpreadId, false, out priceCache);
        }

        internal bool TryGetGenericAttemptCache(out PriceCache priceCache)
        {
            return _priceCacheManager.TryGetGenericValue(SpreadPermId, false, out priceCache);
        }

        private void CalculateEdgeToMarket(bool logOnly = false, bool liveUpdate = false)
        {
            if (!IsBasketOrder || !BasketSettings.HedgeAutoEnabled)
            {
                return;
            }

            const int LogDelaySeconds = 10;
            double optionMid = Mid;
            double underRef = LastHedgeQty < 0 ? HedgeAsk : HedgeBid;
            double hedgePnL = (underRef - LastHedgePrice) * LastHedgeQty;

            double optionPnl = 0.0;
            if (_lastHedgedMain)
            {
                if (!Side.HasValue)
                {
                    optionPnl = double.NaN;
                }
                else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    optionPnl = (optionMid - AveragePrice) * Multiplier;
                }
                else
                {
                    optionPnl = (AveragePrice - optionMid) * Multiplier;
                }
            }
            else
            {
                if (!Side.HasValue)
                {
                    optionPnl = double.NaN;
                }
                else if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    optionPnl = (ContraAveragePrice - optionMid) * Multiplier;
                }
                else
                {
                    optionPnl = (optionMid - ContraAveragePrice) * Multiplier;
                }
            }

            if (!logOnly)
            {
                LastOptionPnlOnFill = optionPnl;
                LastHedgePnlOnFill = hedgePnL;
                LastTotalPnlOnFill = optionPnl + hedgePnL;
                LastEdgeToMarketOnFill = (optionPnl + hedgePnL) / Multiplier;

                Timer timer = new(LogDelaySeconds * 1000)
                {
                    AutoReset = false
                };
                timer.Elapsed += (s, e) => CalculateEdgeToMarket(logOnly: true);
                timer.Start();
            }

            if (liveUpdate)
            {
                LiveLastTradeOptionPnl = optionPnl;
                LiveLastTradeHedgePnl = hedgePnL;
                LiveLastTradeTotalPnl = optionPnl + hedgePnL;
                LiveLastTradeEdgeToMarket = (optionPnl + hedgePnL) / Multiplier;
            }
            else
            {
                _log.Info("Edge To Market" +
                          (logOnly ? $" ({LogDelaySeconds} second after hedge)." : "") +
                          ", Spread Id: " + SpreadId +
                          ", Option Mid: " + optionMid +
                          ", Under Ref: " + underRef +
                          ", Fill Px: " + (_lastHedgedMain ? AveragePrice : ContraAveragePrice) +
                          ", Hedge Px: " + LastHedgePrice +
                          ", Hedge Qty: " + LastHedgeQty +
                          ", Option PnL: " + optionPnl +
                          ", Hedge PnL: " + hedgePnL +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }
        }

        internal async Task OpenUnderlyingTicket(double left, double top, double width, double height)
        {
            await Task.Run(() =>
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;

                    if (OmsCore.Config.UseOrderTicketForSingleLegOrders)
                    {
                        window = new OrderTicketView();
                    }
                    else
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView();
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView();
                                break;
                        }
                    }

                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                    viewModel.InstanceMode = InstanceMode;
                    viewModel.BrokerOverride = BrokerOverride;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                    window.Loaded += (s, e) => viewModel.LoadFromHedgeAsync(Underlying, ZeroPlus.Models.Data.Enums.Side.Buy, 1).ContinueWith(x =>
                    {
                        viewModel.Route = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
                        viewModel.ShowDepthBook = true;
                        window.Dispatcher.BeginInvoke(() =>
                        {
                            window.Top = top;
                            window.Left = left - window.Width;
                            window.Height = height;
                        });
                    });

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            });
        }

        public async Task OpenHedgeTicketAsync(Side side, int qty)
        {
            await Task.Run(() =>
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;

                    if (OmsCore.Config.UseOrderTicketForSingleLegOrders)
                    {
                        window = new OrderTicketView();
                    }
                    else
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView();
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView();
                                break;
                        }
                    }

                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                    viewModel.InstanceMode = InstanceMode;
                    viewModel.BrokerOverride = BrokerOverride;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };

                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();

                    window.Loaded += (s, e) => viewModel.LoadFromHedgeAsync(HedgeUnderlying, side, qty).ContinueWith(x =>
                    {
                        viewModel.Tag = GeHedgeIdentifier();
                        viewModel.Route = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
                        viewModel.WaitForMarkLoad().ContinueWith(t => viewModel.SetPriceToMid());
                        viewModel.RiskCheckEnabled = false;
                    });

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            });
        }

        internal virtual Task<ResubmitSizeOption> CheckLoopSizeUpAsync(double edge, bool savePrevSize, bool allowReverse)
        {
            return Task.FromResult(ResubmitSizeOption.Off);
        }

        protected void SetSides()
        {
            var side = IsStockTied ? EvaluateStockTiedSide() : EvaluateSide(SpreadType, Legs.ToList());
            Side = side;
            IsSingleLegSell = IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell;
        }

        public Side EvaluateStockTiedSide()
        {
            OptionStrategy.EvaluateLegs(Legs.Where(x =>
                Security.SecurityType != ZeroPlus.Models.Data.Enums.SecurityType.Stock), out var baseType, out _, out _);
            return EvaluateSide(baseType, _calcLegs);
        }

        internal void ResetLoopSize()
        {
            ResubmitAfterLastLoopCount = 0;
            if (IsBasketOrder)
            {
                if ((GetAutomationConfig().LoopSizeupType != LoopSizeupType.Off && PrevQty > 0) ||
                    (ResetSize && PrevQty > 0))
                {
                    ResetSize = false;
                    UpdateQty(PrevQty);

                    _log.Info("Reset Loop Size" +
                              ". Loop Iteration Counter: " + LoopIterationCounter +
                              ", Loop Iteration Counter Since Last Size-Up: " + LoopIterationCounterAfterSizeup +
                              ", Lcd Before Size-Up: " + PrevQty +
                              ", Lcd: " + Lcd +
                              ", Id: " + SpreadId);
                }
            }
        }

        private bool IsMainOrder(OrderUpdateValues orderUpdateValues)
        {
            return orderUpdateValues.IsMainOrder
                || (!string.IsNullOrEmpty(orderUpdateValues.LocalOrderId) && OrderIdsSet.Contains(orderUpdateValues.LocalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OriginalOrderId) && OrderIdsSet.Contains(orderUpdateValues.OriginalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrderId) && OrderIdsSet.Contains(orderUpdateValues.OrderId));
        }

        private bool IsContraOrder(OrderUpdateValues orderUpdateValues)
        {
            return orderUpdateValues.IsContraOrder
                || (!string.IsNullOrEmpty(orderUpdateValues.LocalOrderId) && ContraOrderIdsSet.Contains(orderUpdateValues.LocalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OriginalOrderId) && ContraOrderIdsSet.Contains(orderUpdateValues.OriginalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrderId) && ContraOrderIdsSet.Contains(orderUpdateValues.OrderId));
        }

        private bool IsHedgeOrder(OrderUpdateValues orderUpdateValues)
        {
            return orderUpdateValues.IsHedgeOrder
                || (!string.IsNullOrEmpty(orderUpdateValues.LocalOrderId) && HedgeOrderIdsSet.Contains(orderUpdateValues.LocalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OriginalOrderId) && HedgeOrderIdsSet.Contains(orderUpdateValues.OriginalOrderId))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrderId) && HedgeOrderIdsSet.Contains(orderUpdateValues.OrderId));
        }

        private bool IsMainOrder(OMSOrderCancelReject orderUpdateValues)
        {
            return (!string.IsNullOrEmpty(orderUpdateValues.LocalOrderID) && OrderIdsSet.Contains(orderUpdateValues.LocalOrderID))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrigOrderID) && OrderIdsSet.Contains(orderUpdateValues.OrigOrderID))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrderID) && OrderIdsSet.Contains(orderUpdateValues.OrderID));
        }

        private bool IsContraOrder(OMSOrderCancelReject orderUpdateValues)
        {
            return (!string.IsNullOrEmpty(orderUpdateValues.LocalOrderID) && ContraOrderIdsSet.Contains(orderUpdateValues.LocalOrderID))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrigOrderID) && ContraOrderIdsSet.Contains(orderUpdateValues.OrigOrderID))
                || (!string.IsNullOrEmpty(orderUpdateValues.OrderID) && ContraOrderIdsSet.Contains(orderUpdateValues.OrderID));
        }

        private void ResetSmartRoutes()
        {
            _usingSmartRoute = false;
            _smartRouteOverwatchTimer.Stop();
        }

        public OmsOrder UpdateUiStatus(OrderUpdateValues orderUpdateValues)
        {
            OmsOrder order = null;
            try
            {
                order = UpdateOrderUi(orderUpdateValues);
            }
            catch (InvalidOperationException)
            {
                Dispatcher?.BeginInvoke(new Action(() =>
                {
                    order = UpdateOrderUi(orderUpdateValues);
                }));
                _log.Info($"{nameof(HandleExecutionReport)} -> Update values failed using dispatcher instead." +
                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }

            return order;
        }

        protected void SetEdgeProjector(EdgeProjectorModel edgeProjector)
        {
            RemoveEdgeProjector();
            EdgeProjector = edgeProjector;
            ProjectedEdgeChanged(true);
            EdgeProjector.EdgeChanged += ProjectedEdgeChanged;
        }

        protected void RemoveEdgeProjector()
        {
            if (EdgeProjector != null)
            {
                EdgeProjector.EdgeChanged -= ProjectedEdgeChanged;
                EdgeProjector.RemoveTicket(this);
                ProjectedEdgeChanged(false);
            }
        }

        internal async Task CalculatePermAdjPxUsingEdgeToTheoAsync(double edgeToTheo)
        {
            if (!double.IsNaN(edgeToTheo) && await WaitForAdjTheoLoadAsync())
            {
                EdgePriceCalculationResult result = GetCorrectEdgeForOrderType(NetDeltaAdjTheo, edgeToTheo, round: false);
                PermAdjPxBase = result.Price;
                PermAdjContraPxBase = IsSingleLeg ? result.Price : result.ContraPrice;
                UnderMidAtPermLoad = UnderMid;
                PermAdjPxLoaded = !double.IsNaN(PermAdjPxBase);
                LastMainUnderMidAtFill = UnderMidAtPermLoad;
                AveragePrice = PermAdjPxBase;
                PermAdjPxAsync();

                _log.Info($"Edge To Theo: {edgeToTheo}, " +
                          $"Perm Adj Px Base: {PermAdjPxBase}, " +
                          $"Perm Adj Contra Px Base: {PermAdjContraPxBase}, " +
                          $"Perm Adj Load Under Px: {UnderMidAtPermLoad}, " +
                          $"Adj Theo: {NetDeltaAdjTheo}, " +
                          $"Under Mid: {LogPermAdjPxUnderlyingMid}, " +
                          $"Fill Under Mid: {LogUnderlyingMidAtPermLoad}, " +
                          $"Delta: {LogPermAdjPxDelta}, " +
                          $"Fill Px: {LogPermAdjPxBase}, " +
                          $"Result: {LogPermAdjPrice}, " +
                          $"AvgPx: {AveragePrice}, " +
                          $"LastFillUnder: {LastMainUnderMidAtFill}, " +
                          $"Spread: {GetStats()}");
            }
        }

        internal async Task CalculatePermAdjPxUsingMatchingHwAsync(OrderTicket orderModel)
        {
            Stopwatch timer = Stopwatch.StartNew();
            try
            {
                _log.Info($"{nameof(CalculatePermAdjPxUsingMatchingHwAsync)} starting perm adj price calculation Spread Id: {GetStats()}, Orig Spread Id: {orderModel.SpreadId}");
                List<string> modelSymbols = await orderModel.GetLegSymbolsSafeAsync();
                List<string> symbols = GetLegSymbols();
                List<string> allSymbols = symbols.Union(modelSymbols).Distinct().ToList();
                string symbolsList = string.Join(", ", allSymbols);
                _log.Info($"{nameof(CalculatePermAdjPxUsingMatchingHwAsync)} requesting matching hw updates for [{symbolsList}], Spread Id: {GetStats()}, Orig Spread Id: {orderModel.SpreadId}");
                HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse = await OmsCore.UpdateManager.RequestHanweckUpdatesWithMatchingTimestampsAsync(allSymbols);

                if (!matchingUpdatesResponse.UpdateFound)
                {
                    _log.Info(nameof(CalculatePermAdjPxUsingMatchingHwAsync) + " matching hw updates not found for [" + symbolsList +
                              "], Time: " + timer.ElapsedMilliseconds);
                    return;
                }

                if (double.IsNaN(UnderMid))
                {
                    await WaitForUnderMidLoadAsync();
                }

                UnderMidAtPermLoad = UnderMid;
                bool runInDispatcher = !IsBasketOrder || !BasketTraderViewModel.IsEdgeScanFeedAutoTrader;
                (double edge, double contraEdge, double permAdjPxOrig, double permAdjContraPxOrig,
                        double deltaAdjPxOrig, double deltaAdjContraPxOrig) =
                    await orderModel.GetPermAdjEdgeSafe(matchingUpdatesResponse, runInDispatcher);

                double origEdge = edge;
                double deltaDiff = double.NaN;
                double deltaAdjChange = double.NaN;

                bool[] greeksLoaded = await Task.WhenAll(WaitForTheoLoadAsync(), orderModel.WaitForTheoLoadAsync());

                if (greeksLoaded.All(x => x))
                {
                    if (!IsSingleLeg && Side != orderModel.Side)
                    {
                        deltaDiff = TotalDelta + orderModel.TotalDelta;
                    }
                    else
                    {
                        deltaDiff = TotalDelta - orderModel.TotalDelta;
                    }

                    deltaAdjChange = (UnderMid - matchingUpdatesResponse.Price) * deltaDiff;

                    if (!double.IsNaN(deltaAdjChange))
                    {
                        edge += deltaAdjChange;
                        contraEdge += deltaAdjChange;
                    }
                }

                if (Side != orderModel.Side)
                {
                    edge *= -1;
                    contraEdge *= -1;
                }

                double permAdjPx = GetPermAdjPx(matchingUpdatesResponse);
                LogPermAdjPxMatchingHw = permAdjPx;
                LogPermAdjPxBaseEdge = edge;
                LogPermAdjPxOrig = permAdjPxOrig;
                LogPermAdjContraPxOrig = permAdjContraPxOrig;
                LogPermAdjDeltaAdjPxOrig = deltaAdjPxOrig;
                LogPermAdjDeltaAdjContraPxOrig = deltaAdjContraPxOrig;
                if (!double.IsNaN(permAdjPx))
                {
                    if (!double.IsNaN(edge))
                    {
                        EdgePriceCalculationResult result = GetCorrectEdgeForOrderType(permAdjPx, edge, round: false);
                        PermAdjPxBase = result.Price;
                    }
                    else
                    {
                        PermAdjPxBase = permAdjPx;
                    }
                }

                PermDetailsLog = $"Ref: {orderModel.SpreadId}, " +
                                 $"Per: {SpreadId}, " +
                                 $"Ref Und: {matchingUpdatesResponse.Price:F2}, " +
                                 $"Now Und: {UnderMid}, " +
                                 $"Ref Px: {permAdjPxOrig:F2}, " +
                                 $"Per Px: {permAdjPx:F2}, " +
                                 $"Diff ^: {deltaDiff}, " +
                                 $"Orig Chg: {origEdge:F2}, " +
                                 $"Adj Chg: {edge:F2}, " +
                                 $"Result: {PermAdjPxBase:F2}";

                double permAdjContraPx = GetPermAdjContraPx(matchingUpdatesResponse);
                if (!double.IsNaN(permAdjContraPx))
                {
                    if (!double.IsNaN(contraEdge))
                    {
                        EdgePriceCalculationResult result =
                            GetCorrectEdgeForOrderType(permAdjContraPx, contraEdge, round: false);
                        PermAdjContraPxBase = !IsSingleLeg ? result.Price : result.ContraPrice;
                    }
                    else
                    {
                        PermAdjContraPxBase = permAdjContraPx;
                    }
                }

                _log.Info($"{nameof(CalculatePermAdjPxUsingMatchingHwAsync)} matching hw updates found. " +
                          $"Elapsed: {timer.ElapsedMilliseconds}, " +
                          $"Timestamp: {matchingUpdatesResponse.Timestamp}, " +
                          $"Price: {matchingUpdatesResponse.Price}, " +
                          $"Spread Id: {GetStats()}, " +
                          $"Orig Spread Id: {orderModel.SpreadId}, " +
                          $"Orig Side: {orderModel.Side}, " +
                          $"Side: {Side}, " +
                          $"ETT: {edge}, " +
                          $"CETT: {contraEdge}, " +
                          $"Delta Diff: {deltaDiff}, " +
                          $"Delta Diff Chg: {deltaAdjChange}, " +
                          $"Base: {PermAdjPxBase}, " +
                          $"Contra Base: {PermAdjContraPxBase}");

                PermAdjPxLoaded = true;
                LastMainUnderMidAtFill = UnderMidAtPermLoad;
                AveragePrice = PermAdjPxBase;
                PermAdjPxAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CalculatePermAdjPxUsingMatchingHwAsync));
            }
            finally
            {
                timer.Stop();
            }
        }

        private async Task<(double edge, double contraEdge, double permAdjPx, double permAdjContraPx, double deltaAdjPx, double deltaAdjContraPx)> GetPermAdjEdgeSafe(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse, bool runSafeMode)
        {
            double permAdjPx = runSafeMode ? await GetPermAdjPxSafe(matchingUpdatesResponse) : GetPermAdjPx(matchingUpdatesResponse);
            double permAdjContraPx = runSafeMode ? await GetPermAdjContraPxSafe(matchingUpdatesResponse) : GetPermAdjContraPx(matchingUpdatesResponse);
            double edgeToTheo = 0;
            double edgeToContraTheo = 0;
            DeltaAdjPrice();
            if (!double.IsNaN(permAdjPx))
            {
                if (IsSingleLeg)
                {
                    if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        edgeToTheo = permAdjPx - DeltaAdjPx;
                        edgeToContraTheo = DeltaAdjContraPx - permAdjContraPx;
                    }
                    else
                    {
                        edgeToTheo = DeltaAdjPx - permAdjPx;
                        edgeToContraTheo = permAdjContraPx - DeltaAdjContraPx;
                    }
                }
                else
                {
                    edgeToTheo = permAdjPx - DeltaAdjPx;
                    edgeToContraTheo = permAdjContraPx - DeltaAdjContraPx;
                }
            }

            return (edgeToTheo, edgeToContraTheo, permAdjPx, permAdjContraPx, DeltaAdjPx, DeltaAdjContraPx);
        }

        private async Task<double> GetPermAdjPxSafe(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse)
        {
            double permAdjPx = double.NaN;
            await Dispatcher?.BeginInvoke(() => permAdjPx = GetPermAdjPx(matchingUpdatesResponse));
            return permAdjPx;
        }

        private async Task<double> GetPermAdjContraPxSafe(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse)
        {
            double permAdjPx = double.NaN;
            await Dispatcher?.BeginInvoke(() => permAdjPx = GetPermAdjContraPx(matchingUpdatesResponse));
            return permAdjPx;
        }

        public double GetPermAdjPx(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse)
        {
            ObservableCollection<TicketLegModel> legs = Legs;
            double price = GetPermAdjPx(matchingUpdatesResponse, legs);
            return price;
        }

        public static double GetPermAdjPx(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse, IList<TicketLegModel> legs)
        {
            double price = 0.0;
            bool isSingleLeg = legs.Count == 1;
            foreach (TicketLegModel leg in legs)
            {
                if (!leg.IsValid ||
                    !matchingUpdatesResponse.SymbolToTheoMap.TryGetValue(leg.Symbol, out double legTheo))
                {
                    price = double.NaN;
                    break;
                }
                else
                {
                    bool isStock = leg.SecurityType == SecurityType.Stock;
                    double ratio = leg.Ratio;
                    int side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy || isSingleLeg ? 1 : -1;
                    price += side * legTheo * ratio;
                }
            }
            return price;
        }

        private double GetPermAdjContraPx(HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse)
        {
            double price = 0.0;
            bool isSingleLeg = IsSingleLeg;
            foreach (TicketLegModel leg in Legs)
            {
                if (!leg.IsValid ||
                    !matchingUpdatesResponse.SymbolToTheoMap.TryGetValue(leg.Symbol, out double legTheo))
                {
                    price = double.NaN;
                    break;
                }
                else
                {
                    bool isStock = leg.SecurityType == SecurityType.Stock;
                    double ratio = isStock ? leg.Ratio / Multiplier : leg.Ratio;
                    int side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Sell || isSingleLeg ? 1 : -1;
                    price += side * legTheo * ratio;
                }
            }
            return price;
        }

        internal bool PermAdjPxAsync(double edgeOverride = double.NaN)
        {
            try
            {
                if (PermAdjPxLoaded)
                {
                    LogPermAdjPxUnderlyingMid = _underMid;
                    LogPermAdjPxDelta = TotalDelta;
                    LogPermAdjPxContraDelta = IsSingleLeg ? TotalDelta : -TotalDelta;
                    LogUnderlyingMidAtPermLoad = UnderMidAtPermLoad;
                    LogPermAdjPxBase = PermAdjPxBase;
                    LogPermAdjContraPxBase = PermAdjContraPxBase;
                    LogPermAdjPrice = ((LogPermAdjPxUnderlyingMid - LogUnderlyingMidAtPermLoad) * LogPermAdjPxDelta) + LogPermAdjPxBase;
                    LogPermAdjContraPrice = ((LogPermAdjPxUnderlyingMid - LogUnderlyingMidAtPermLoad) * LogPermAdjPxContraDelta) + LogPermAdjContraPxBase;

                    PermAdjPx = LogPermAdjPrice;
                    PermAdjContraPx = LogPermAdjContraPrice;

                    if (IsBasketOrder)
                    {
                        if (!double.IsNaN(edgeOverride))
                        {
                            _log.Info($"Perm Adj Px. Under Mid: {LogPermAdjPxUnderlyingMid}, Fill Under Mid: {LogUnderlyingMidAtPermLoad}, Delta: {LogPermAdjPxDelta}, Fill Px: {LogPermAdjPxBase}, Result: {LogPermAdjPrice}, Edge: {edgeOverride} Spread: {GetStats()}");

                            SetPermAdjPrice(edgeOverride);
                            return true;
                        }
                        else if (BasketSettings.UsePermAdjPx)
                        {
                            SetPermAdjPrice(BasketSettings.PermAdjEdge);
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PermAdjPxAsync));
                return false;
            }
        }

        private async Task<List<string>> GetLegSymbolsSafeAsync()
        {
            List<string> symbols = new();
            await Dispatcher?.BeginInvoke(() => symbols = GetLegSymbols());
            return symbols;
        }

        private List<string> GetLegSymbols()
        {
            return Legs.Select(x => x.Symbol).ToList();
        }

        private void ProjectedEdgeChanged(bool showEdgeIndicators, double acqEdge = double.NaN, double projEdge = double.NaN, double deltaAdjEdge = double.NaN, double realizedPnl = double.NaN, double adjustedPnl = double.NaN)
        {
            Dispatcher?.BeginInvoke(new Action(() =>
            {
                ShowEdgeIndicators = showEdgeIndicators;
                AcquiredEdge = acqEdge;
                ProjectedEdge = projEdge;
                DeltaAdjEdge = deltaAdjEdge;
                ThreeWayAdjustedPnl = adjustedPnl;
            }));
        }

        public virtual Task<DateTime> SetEdgeAsync(bool ignoreAdjTheoRiskCheck = true, double? edgeOverride = null)
        {
            EdgeType = EdgeType.None;
            Edge = double.NaN;
            EdgeFormula = "";
            ResetPriceAndContraPrice();
            _log.Info("Set price failed not supported.");
            return Task.FromResult(DateTime.Now);
        }

        private OmsOrder UpdateOrderUi(OrderUpdateValues orderUpdateValues)
        {
            OmsOrder order = new()
            {
                LastQuantity = orderUpdateValues.LastQuantity,
                CumulativeQuantity = orderUpdateValues.CumQuantity,
                AveragePrice = orderUpdateValues.AveragePrice,
                LastUpdateTime = orderUpdateValues.LastUpdateTime,
                LeavesQuantity = orderUpdateValues.LeavesQuantity,
                SpreadId = SpreadId,
                SpreadType = SpreadType,
            };
            OmsOrderLeg leg = new()
            {
                Symbol = Legs.FirstOrDefault().Symbol,
                LastQuantity = orderUpdateValues.LastQuantity,
                CumulativeQuantity = orderUpdateValues.CumQuantity,
                AveragePrice = orderUpdateValues.AveragePrice,
            };
            order.TradedLegs.Add(leg);
            if (orderUpdateValues.IsMainOrder)
            {
                leg.Side = ZeroPlus.Models.Data.Enums.Side.Buy;
                if (!orderUpdateValues.ClearOrderIdSet)
                {
                    OrderIdsSet.Add(orderUpdateValues.LocalOrderId);
                    OrderIdsSet.Add(orderUpdateValues.OriginalOrderId);
                    OrderIdsSet.Add(orderUpdateValues.OrderId);
                    OrderId = orderUpdateValues.OrderId;
                    PermID = orderUpdateValues.OriginalOrderId;
                    LocalId = orderUpdateValues.LocalOrderId;
                }
                if (orderUpdateValues.OrderStatus.IsFilled())
                {
                    AveragePrice = orderUpdateValues.AveragePrice;
                }
                MainOrderStatus = orderUpdateValues.OrderStatus;
                Status = orderUpdateValues.Status;
                StatusMode = orderUpdateValues.StatusMode;
                Filled = orderUpdateValues.Filled >= 0 ? orderUpdateValues.Filled.ToString() : "";
                LastQuantity = orderUpdateValues.LastQuantity;
                CumulativeQuantity = orderUpdateValues.CumQuantity;
                IsSubmitEnabled = orderUpdateValues.IsSubmitEnabled;
                IsCancelEnabled = orderUpdateValues.IsCancelEnabled;
                IsModifyEnabled = orderUpdateValues.IsModifyEnabled;
                FilledQty = orderUpdateValues.Filled;
                TotalFills += Math.Abs(orderUpdateValues.LastQuantity);
            }
            else if (orderUpdateValues.IsContraOrder)
            {
                leg.Side = ZeroPlus.Models.Data.Enums.Side.Sell;
                if (!IsSingleLeg)
                {
                    leg.AveragePrice *= -1;
                }
                if (!orderUpdateValues.ClearOrderIdSet)
                {
                    ContraOrderIdsSet.Add(orderUpdateValues.LocalOrderId);
                    ContraOrderIdsSet.Add(orderUpdateValues.OriginalOrderId);
                    ContraOrderIdsSet.Add(orderUpdateValues.OrderId);
                    ContraOrderId = orderUpdateValues.OrderId;
                    ContraPermId = orderUpdateValues.OriginalOrderId;
                    ContraLocalId = orderUpdateValues.LocalOrderId;
                }
                if (orderUpdateValues.OrderStatus.IsFilled())
                {
                    ContraAveragePrice = orderUpdateValues.AveragePrice;
                }
                ContraOrderStatus = orderUpdateValues.OrderStatus;
                ContraStatus = orderUpdateValues.Status;
                ContraStatusMode = orderUpdateValues.StatusMode;
                ContraFilled = orderUpdateValues.Filled >= 0 ? orderUpdateValues.Filled.ToString() : "";
                ContraLastQuantity = orderUpdateValues.LastQuantity;
                ContraCumulativeQty = orderUpdateValues.CumQuantity;
                IsContraSubmitEnabled = orderUpdateValues.IsSubmitEnabled;
                IsContraCancelEnabled = orderUpdateValues.IsCancelEnabled;
                IsContraModifyEnabled = orderUpdateValues.IsModifyEnabled;
                TotalFills += Math.Abs(orderUpdateValues.LastQuantity);
            }
            else if (orderUpdateValues.IsHedgeOrder)
            {
                if (!orderUpdateValues.ClearOrderIdSet)
                {
                    HedgeOrderIdsSet.Add(orderUpdateValues.LocalOrderId);
                    HedgeOrderIdsSet.Add(orderUpdateValues.OriginalOrderId);
                    HedgeOrderIdsSet.Add(orderUpdateValues.OrderId);
                }
                StockHedgeStatus = orderUpdateValues.Status;
                StockHedgeStatusMode = orderUpdateValues.StatusMode;
            }

            _log.Info($"Order status update. Spread: {SpreadId}, " +
                      $"OrderId:{orderUpdateValues.OrderId}/{orderUpdateValues.OriginalOrderId}/{orderUpdateValues.LocalOrderId}, " +
                      $"Status:{orderUpdateValues.OrderStatus}, " +
                      $"Current px:{Price}, " +
                      $"Contra px:{ContraPrice}, " +
                      $"Main: {orderUpdateValues.IsMainOrder}, " +
                      $"Contra:{orderUpdateValues.IsContraOrder}, " +
                      $"Hedge:{orderUpdateValues.IsHedgeOrder}, " +
                      $"Latency Timer:{_latencyTimer.ElapsedMilliseconds}, " +
                      $"Server Creep: {BasketTraderViewModel?.ServerCreep}");

            return order;
        }

        internal OrderUpdateValues ParseOrderUpdate(OmsOrderModel execReport, bool? parseAsSingle = null)
        {
            OrderStatus orderStatus = execReport.OrderStatus;

            OrderUpdateValues orderUpdateValues = new()
            {
                OrderStatus = orderStatus,
                LastUpdateTime = execReport.LastUpdateTime,
                OrderId = execReport.OrderID,
                LastPrice = execReport.LastPrice == 0 ? double.NaN : execReport.LastPrice,
                AveragePrice = double.NaN,
                AveragePriceAfterFees = double.NaN,
                UnderlyingMidPrice = double.NaN,
                LocalOrderId = execReport.LocalID,
                OriginalOrderId = execReport.OriginalOrderID,
                IsCancelEnabled = true,
                IsModifyEnabled = true,
                IsSubmitEnabled = true,
            };

            orderUpdateValues.IsMainOrder = IsMainOrder(orderUpdateValues);
            if (!orderUpdateValues.IsMainOrder)
            {
                orderUpdateValues.IsContraOrder = IsContraOrder(orderUpdateValues);
            }
            if (!orderUpdateValues.IsMainOrder && !orderUpdateValues.IsContraOrder)
            {
                orderUpdateValues.IsHedgeOrder = IsHedgeOrder(orderUpdateValues);
            }

            bool isSingleLeg = IsHedgeOrder(orderUpdateValues) || (parseAsSingle ?? IsSingleLeg);
            orderUpdateValues.Filled = execReport.CumulativeQuantity;
            orderUpdateValues.CumQuantity = execReport.CumulativeQuantity;
            orderUpdateValues.LastQuantity = execReport.LastQuantity;

            int inverter = 1;

            if (TicketStyle == OrderTicketStyle.Combined &&
               OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
               IsContraOrder(orderUpdateValues) &&
               !isSingleLeg)
            {
                inverter = -1;
            }

            bool isBuySide = isSingleLeg ? execReport.Side?.ToString().ToUpper() == "BUY" : execReport.Price > 0.0;
            int filledQty = execReport.CumulativeQuantity;
            int leavesQty = execReport.LeavesQuantity;
            double displayPx = execReport.Price * inverter;
            double fillPx = Math.Round(execReport.AveragePrice * inverter, 2);
            string lastExch = !string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "";
            int cumQty = execReport.CumulativeQuantity;
            switch (orderStatus)
            {
                case OrderStatus.New:
                    orderUpdateValues.Status = $"Order Placed. {execReport.Quantity:n0} @ {displayPx:n2}";
                    orderUpdateValues.StatusMode = StatusMode.Reset;
                    orderUpdateValues.IsCancelEnabled = true;
                    orderUpdateValues.IsModifyEnabled = true;
                    orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                    break;
                case OrderStatus.PendingNew:
                    orderUpdateValues.Status = $"Placing Order. {execReport.Quantity:n0} @ {displayPx:n2}";
                    orderUpdateValues.StatusMode = StatusMode.Pending;
                    orderUpdateValues.IsCancelEnabled = false;
                    orderUpdateValues.IsModifyEnabled = false;
                    orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                    break;
                case OrderStatus.PendingCancel when filledQty > 0:
                case OrderStatus.PendingReplace when filledQty > 0:
                case OrderStatus.PartiallyFilled:
                    double ordPx = Math.Round(displayPx, 2);
                    string suffix = "";
                    if (ordPx != fillPx)
                    {
                        suffix = "AUC Frm " + ordPx.ToString("#,###.00####") + " ";
                    }

                    orderUpdateValues.Status = $"Partially Filled. {filledQty} " +
                                               $"@ {fillPx:#,###.00####} {suffix}- " +
                                               $"Rem: {leavesQty}" +
                                               $"{lastExch}";
                    orderUpdateValues.StatusMode = isBuySide ? StatusMode.NewBuy : StatusMode.NewSell;
                    orderUpdateValues.AveragePrice = execReport.AveragePrice;
                    orderUpdateValues.AveragePriceAfterFees = execReport.AveragePrice + GetTotalFeesForTicket(execReport.Route, execReport.LastExchange);
                    orderUpdateValues.UnderlyingMidPrice = UnderMid;
                    orderUpdateValues.IsCancelEnabled = true;
                    orderUpdateValues.IsModifyEnabled = true;
                    orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                    break;
                case OrderStatus.Filled:
                    OrderIsClosed = true;
                    ordPx = Math.Round(displayPx, 2);
                    suffix = "";
                    if (ordPx != fillPx)
                    {
                        suffix = "AUC Frm " + ordPx.ToString("#,###.00####") + " ";
                    }
                    orderUpdateValues.Status = $"Filled. {filledQty} " +
                                               $"@ {fillPx:#,###.00####} {suffix}" +
                                               $"{lastExch}";
                    orderUpdateValues.StatusMode = isBuySide ? StatusMode.FilledBuy : StatusMode.FilledSell;
                    orderUpdateValues.IsCancelEnabled = false;
                    orderUpdateValues.IsModifyEnabled = false;
                    orderUpdateValues.AveragePrice = execReport.AveragePrice;
                    orderUpdateValues.AveragePriceAfterFees = execReport.AveragePrice + GetTotalFeesForTicket(execReport.Route, execReport.LastExchange);
                    orderUpdateValues.UnderlyingMidPrice = UnderMid;
                    orderUpdateValues.ClearOrderIdSet = true;
                    orderUpdateValues.IsSubmitEnabled = true;
                    break;
                case OrderStatus.Canceled:
                    OrderIsClosed = true;
                    orderUpdateValues.Status = execReport.CumulativeQuantity == 0
                                             ? $"Canceled. {execReport.Quantity:n0} @ {displayPx:n2}"
                                             : $"Canceled. Partially Filled {cumQty} " +
                                               $"@ {fillPx:#,###.00####}";
                    orderUpdateValues.AveragePrice = execReport.CumulativeQuantity == 0 ? double.NaN : execReport.AveragePrice;
                    orderUpdateValues.AveragePriceAfterFees = execReport.CumulativeQuantity == 0 ? double.NaN : execReport.AveragePrice;
                    orderUpdateValues.UnderlyingMidPrice = execReport.CumulativeQuantity == 0 ? double.NaN : UnderMid;
                    orderUpdateValues.StatusMode = isBuySide ? StatusMode.CancelledBuy : StatusMode.CancelledSell;
                    orderUpdateValues.IsCancelEnabled = false;
                    orderUpdateValues.IsModifyEnabled = false;
                    orderUpdateValues.ClearOrderIdSet = true;
                    orderUpdateValues.IsSubmitEnabled = true;
                    break;
                case OrderStatus.Rejected:
                    OrderIsClosed = true;
                    orderUpdateValues.Status = $"Rejected {execReport.Comment}. {execReport.Quantity:n0} @ {displayPx:n2}";
                    orderUpdateValues.StatusMode = isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell;
                    orderUpdateValues.IsCancelEnabled = false;
                    orderUpdateValues.IsModifyEnabled = false;
                    orderUpdateValues.ClearOrderIdSet = true;
                    orderUpdateValues.IsSubmitEnabled = true;
                    if (OmsCore.Config.ShowPopupOnRejectedOrder)
                    {
                        ShowMessage($"Order Rejected: {execReport.Comment}", "Order Rejected");
                    }
                    break;
                case OrderStatus.Replaced:
                    orderUpdateValues.Status = $"Replaced. {execReport.Quantity:n0} @ {displayPx:n2}";
                    orderUpdateValues.StatusMode = StatusMode.Reset;
                    orderUpdateValues.IsCancelEnabled = true;
                    orderUpdateValues.IsModifyEnabled = true;
                    orderUpdateValues.IsSubmitEnabled = !DisableDuplicateSubmissions;
                    break;
            }

            return orderUpdateValues;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (!IsDisposed)
                {
                    try
                    {
                        UpdateTicketSubscription(key, value, isFromCache);
                    }
                    catch (InvalidOperationException)
                    {
                        Dispatcher?.Invoke(() => UpdateTicketSubscription(key, value, isFromCache), DispatcherPriority.Background);
                        _log.Info($"{nameof(SubscribedDataUpdateValue)} -> Update values failed using dispatcher instead. Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        protected virtual void HandleUnknownUpdate(SubscriptionKey key, object value)
        {

        }

        private void UpdateTicketSubscription(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                string symbol = key.Symbol;
                switch (key.Type)
                {
                    case SubscriptionFieldType.TradeUpdate when value is TradeUpdateModel trade:
                        HandleTradeUpdate(ref trade);
                        break;
                    case SubscriptionFieldType.PermEdgeToTheo when value is List<EdgeToTheoTrackerModel> edgeToTheoModels:
                        var strike = Legs.FirstOrDefault()?.Strike;
                        var update = edgeToTheoModels.Where(x => x.StrikeStart <= strike && x.StrikeEnd >= strike).OrderBy(x => Math.Abs((x.StrikeStart + x.StrikeEnd) / 2)).FirstOrDefault();
                        if (update != null)
                        {
                            LastPermBuyFillEdgeToTheo = update.BuyFillEdgeToTheo;
                            LastPermSellFillEdgeToTheo = update.SellFillEdgeToTheo;
                            LastPermBuyAttemptEdgeToTheo = update.BuyAttemptEdgeToTheo;
                            LastPermSellAttemptEdgeToTheo = update.SellAttemptEdgeToTheo;
                        }
                        break;
                    case SubscriptionFieldType.FirmSpreadPosition:
                        HandleFirmSpreadPositionUpdate(value as IPosition);
                        break;
                    case SubscriptionFieldType.HardSide:
                        HandleHardSideUpdate(value as HardSideResult);
                        break;
                    case SubscriptionFieldType.UserSpreadPosition:
                        HandleTraderSpreadPositionUpdate(value as IPosition, isFromCache);
                        break;
                    case SubscriptionFieldType.UserInstancePosition:
                        string spreadTitle = key.Symbol;
                        if (spreadTitle == ("HEDGE - " + OmsCore.User.Username.ToUpper() + " - " + SpreadId).ToUpper())
                        {
                            HandleHedgePositionUpdate(value as IPosition);
                        }
                        break;
                    case SubscriptionFieldType.Bid:
                        if (value is double bid)
                        {
                            HandleBidUpdate(symbol, bid);
                        }
                        break;
                    case SubscriptionFieldType.Ask:
                        if (value is double ask)
                        {
                            HandleAskUpdate(symbol, ask);
                        }
                        break;
                    case SubscriptionFieldType.LastPrice:
                        if (value is double last)
                        {
                            if (Underlying == symbol)
                            {
                                Last = last;
                                SetNetAndPercentChange(last);
                            }
                            _ = CheckForAutoCancel();
                        }
                        break;
                    case SubscriptionFieldType.Trade:
                        if (value is OMSTransaction omsTransaction)
                        {
                            HandleTransactionUpdate(omsTransaction);
                        }
                        else if (value is OMSSendTransaction omsTransaction2)
                        {
                            HandleTransactionUpdate(omsTransaction2);
                        }
                        break;
                    case SubscriptionFieldType.TronTrade:
                        if (ShowTimeAndSales && value is MDSendDmitryTrade tronTrade)
                        {
                            HandleTradeUpdate(tronTrade);
                        }
                        break;
                    case SubscriptionFieldType.IbQuote:
                        if (value is IbQuoteUpdateModel ibUpdate)
                        {
                            HandleIbUpdate(ibUpdate);
                        }
                        break;
                    case SubscriptionFieldType.TradeEdgeToTheo:
                        if (value is EdgeToTheoUpdateModel edgeToTheoUpdate)
                        {
                            HandleEdgeToTheoUpdate(edgeToTheoUpdate);
                        }
                        break;
                    case SubscriptionFieldType.IbHistoricalData:
                        if (value is string vol)
                        {
                            ShowMessage(vol, "IB Historical Data");
                        }
                        break;
                    case SubscriptionFieldType.FirmOrderAndTradeSummary when value is FirmOrderAndTradeSummary summary:
                        HandleFirmOrderAndTradeSummary(summary);
                        break;
                    default:
                        HandleUnknownUpdate(key, value);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateTicketSubscription));
            }
        }

        private void HandleFirmOrderAndTradeSummary(FirmOrderAndTradeSummary summary)
        {
            if (summary.BuySummary != null)
            {
                BuyLastAttemptPx = summary.BuySummary.LastAttemptPx;
                BuyLastAttemptUnderPx = summary.BuySummary.LastAttemptUnderPx;
                BuyLastAttemptTime = summary.BuySummary.LastAttemptTime;
                BuyLastFillPx = summary.BuySummary.LastFillPx;
                BuyLastFillUnderPx = summary.BuySummary.LastFillUnderPx;
                BuyLastFillTime = summary.BuySummary.LastFillTime;
                BuyLowestAttemptedEdgeToTheo = summary.BuySummary.LowestAttemptedEdgeToTheo;
                BuyHighestFilledEdgeToTheo = summary.BuySummary.HighestFilledEdgeToTheo;
            }

            if (summary.SellSummary != null)
            {
                SellLastAttemptPx = summary.SellSummary.LastAttemptPx;
                SellLastAttemptUnderPx = summary.SellSummary.LastAttemptUnderPx;
                SellLastAttemptTime = summary.SellSummary.LastAttemptTime;
                SellLastFillPx = summary.SellSummary.LastFillPx;
                SellLastFillUnderPx = summary.SellSummary.LastFillUnderPx;
                SellLastFillTime = summary.SellSummary.LastFillTime;
                SellLowestAttemptedEdgeToTheo = summary.SellSummary.LowestAttemptedEdgeToTheo;
                SellHighestFilledEdgeToTheo = summary.SellSummary.HighestFilledEdgeToTheo;
            }
        }

        private void HandleEdgeToTheoUpdate(EdgeToTheoUpdateModel update)
        {
            GlobalMarketBuyEdgeToTheo = update.BuyEdgeToTheo;
            GlobalMarketSellEdgeToTheo = update.SellEdgeToTheo;
        }

        private void HandleBidUpdate(string symbol, double bid)
        {
            if (HedgeUnderlying == symbol)
            {
                HedgeBid = bid;
                if (!double.IsNaN(HedgeMid) && !HedgeUnderLoaded)
                {
                    HedgeUnderLoaded = true;
                    SignalDataLoadWaiters();
                }
                UpdateStockHedgeUnrealizedPnl();
            }
            if (Underlying == symbol)
            {
                UnderBid = bid;
                UpdateUnderMid();
            }
        }

        private void HandleAskUpdate(string symbol, double ask)
        {
            if (HedgeUnderlying == symbol)
            {
                HedgeAsk = ask;
                if (!double.IsNaN(HedgeMid) && !HedgeUnderLoaded)
                {
                    HedgeUnderLoaded = true;
                    SignalDataLoadWaiters();
                }
                UpdateStockHedgeUnrealizedPnl();
            }
            if (Underlying == symbol)
            {
                UnderAsk = ask;
                UpdateUnderMid();
            }
        }

        private void HandleTradeUpdate(MDSendDmitryTrade tronTrade)
        {
            TronTradeModel tradeModel = new(tronTrade);
            Dispatcher.BeginInvoke(() =>
            {
                TronTrades.Add(tradeModel);
                LatestTrade = tradeModel;
            }, DispatcherPriority.Background, null);
        }

        private void HandleTransactionUpdate(OMSTransaction omsTransaction)
        {
            if (omsTransaction.RequestSymbol == SpreadSymbol)
            {
                LastTransactionPrice = omsTransaction.AveragePrice;
            }
            else if (omsTransaction.RequestSymbol == ContraSpreadSymbol)
            {
                if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                {
                    LastContraTransactionPrice = -omsTransaction.AveragePrice;
                }
                else
                {
                    LastContraTransactionPrice = omsTransaction.AveragePrice;
                }
            }
        }

        private void HandleTransactionUpdate(OMSSendTransaction omsTransaction2)
        {
            if (omsTransaction2.RequestSymbol == SpreadSymbol)
            {
                LastTransactionPrice = omsTransaction2.AveragePrice;
            }
            else if (omsTransaction2.RequestSymbol == ContraSpreadSymbol)
            {
                if (TicketStyle == OrderTicketStyle.Combined && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                {
                    LastContraTransactionPrice = -omsTransaction2.AveragePrice;
                }
                else
                {
                    LastContraTransactionPrice = omsTransaction2.AveragePrice;
                }
            }
        }

        private void HandleIbUpdate(IbQuoteUpdateModel ibUpdate)
        {
            if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                TwsPrice = ibUpdate.Bid;
                TwsContraPrice = ibUpdate.Ask;
                TwsBidSize = ibUpdate.BidSize;
                TwsAskSize = ibUpdate.AskSize;
                TwsBidExch = ibUpdate.BidExch;
                TwsAskExch = ibUpdate.AskExch;
            }
            else
            {
                TwsPrice = ibUpdate.Ask;
                TwsContraPrice = ibUpdate.Bid;
                TwsBidSize = ibUpdate.AskSize;
                TwsAskSize = ibUpdate.BidSize;
                TwsBidExch = ibUpdate.AskExch;
                TwsAskExch = ibUpdate.BidExch;
            }

            UpdateTwsTriggers();
            TwsHigh = ibUpdate.High;
            TwsLow = ibUpdate.Low;
            TwsOpen = ibUpdate.Open;
            TwsClose = ibUpdate.Close;
            TwsLast = ibUpdate.Last;
            TwsLastExch = ibUpdate.LastExch;
            TwsLastSize = ibUpdate.LastSize;
            TwsVolume = ibUpdate.Volume;

            if (!IbQuoteLoaded)
            {
                IbQuoteLoaded = true;
            }

            if (LogUpdates)
            {
                _log.Info("COB Log. Spread: {}, TickerId: {}, BidSize: {}, AskSize: {}, LastSize: {}, Volume: {}, Bid: {}, Ask: {}, Last: {}, High: {}, Low: {}, Open: {}, Close: {}, BidExch: {}, AskExch: {}, LastExch: {}, Symbol: {}.",
                          SpreadId,
                          ibUpdate.TickerId,
                          ibUpdate.BidSize,
                          ibUpdate.AskSize,
                          ibUpdate.LastSize,
                          ibUpdate.Volume,
                          ibUpdate.Bid,
                          ibUpdate.Ask,
                          ibUpdate.Last,
                          ibUpdate.High,
                          ibUpdate.Low,
                          ibUpdate.Open,
                          ibUpdate.Close,
                          ibUpdate.BidExch,
                          ibUpdate.AskExch,
                          ibUpdate.LastExch,
                          ibUpdate.Symbol);
            }
        }

        private void UpdateTwsTriggers()
        {
            TwsBidLive = TwsPrice > Low;
            TwsAskLive = TwsContraPrice < High;
        }

        private void UpdateUnderMid()
        {
            if (UnderAsk == 0 || UnderBid == 0 || double.IsNaN(UnderAsk) || double.IsNaN(UnderBid))
            {
                return;
            }

            double mid = (UnderAsk + UnderBid) / 2;
            double prevMid = UnderMid;
            UnderMid = mid;
            UnderMidUpdated(prevMid, mid);

            if (!UnderLoaded)
            {
                UnderLoaded = true;
                SignalDataLoadWaiters();
            }

            DeltaAdjPriceAsync();
        }

        private void DeltaAdjPriceAsync()
        {
            if (!IsBasketOrder)
            {
                Task.Run(() => DeltaAdjPrice());
            }
            else if (OmsCore.Config.BasketDeltaAdjLastFillPx)
            {
                DeltaAdjPrice();
            }
        }

        public void DeltaAdjPrice()
        {
            try
            {
                if (IsBasketOrder)
                {
                    PermAdjPxAsync();
                }
                DeltaAdjHedgePrice();
                DeltaAdjEdgeSummary();
                double deltaAdjPx = double.NaN;

                double underMid = UnderMid;
                double totalDelta = TotalDelta;
                double contraDelta = IsSingleLeg ? TotalDelta : -TotalDelta;

                if (underMid != 0 && !double.IsNaN(underMid) &&
                    totalDelta != 0 && !double.IsNaN(totalDelta) &&
                    AveragePrice != 0 && !double.IsNaN(AveragePrice) &&
                    LastMainUnderMidAtFill != 0 && !double.IsNaN(LastMainUnderMidAtFill))
                {
                    deltaAdjPx = ((underMid - LastMainUnderMidAtFill) * totalDelta) + AveragePrice;
                }

                DeltaAdjPx = deltaAdjPx;

                if (IsBasketOrder && BasketTraderViewModel != null && BasketTraderViewModel.IsEdgeScanFeedAutoTrader)
                {
                    double deltaAdjLastTradePx = double.NaN;
                    if (underMid != 0 && !double.IsNaN(underMid) &&
                        totalDelta != 0 && !double.IsNaN(totalDelta) &&
                        LastTradeUpdate.HasValue &&
                        LastTradeUpdate.Value.Price != 0 && !double.IsNaN(LastTradeUpdate.Value.Price))
                    {
                        double lastTradeUnderMid = (LastTradeUpdate.Value.UnderBid + LastTradeUpdate.Value.UnderAsk) / 2;
                        if (lastTradeUnderMid != 0 && !double.IsNaN(lastTradeUnderMid))
                        {
                            deltaAdjLastTradePx = ((underMid - lastTradeUnderMid) * totalDelta) + LastTradeUpdate.Value.Price;
                        }
                    }
                    DeltaAdjLastTradeUpdate = deltaAdjLastTradePx;

                    double deltaAdjEdgeScanTriggerBuyPrice = ((underMid - EdgeScanFeedUnderlying) * totalDelta) + EdgeScanFeedBuyPrice;
                    EdgeScanFeedBuyPriceDeltaAdj = deltaAdjEdgeScanTriggerBuyPrice;

                    double deltaAdjEdgeScanTriggerSellPrice = ((underMid - EdgeScanFeedUnderlying) * totalDelta) + EdgeScanFeedSellPrice;
                    EdgeScanFeedSellPriceDeltaAdj = deltaAdjEdgeScanTriggerSellPrice;
                }

                if (LockDeltaAdjPrice && !double.IsNaN(DeltaAdjPx))
                {
                    SetPrice(DeltaAdjPx);
                    switch (TicketStyle)
                    {
                        case OrderTicketStyle.Combined when OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg:
                            double deltaAdjContraPx = -(((underMid - LastContraUnderMidAtFill) * contraDelta) + ContraAveragePrice);
                            DeltaAdjContraPx = deltaAdjContraPx;
                            if (LockContraDeltaAdjPrice && !double.IsNaN(DeltaAdjContraPx))
                            {
                                SetContraPrice(DeltaAdjContraPx);
                            }
                            break;
                        default:
                            deltaAdjContraPx = ((underMid - LastContraUnderMidAtFill) * contraDelta) + ContraAveragePrice;
                            DeltaAdjContraPx = deltaAdjContraPx;
                            if (LockContraDeltaAdjPrice && !double.IsNaN(DeltaAdjContraPx))
                            {
                                SetContraPrice(DeltaAdjContraPx);
                            }
                            break;
                    }
                }

                IPosition lastTraderPositionUpdate = _lastTraderPositionUpdate;
                if (lastTraderPositionUpdate == null)
                {
                    lastTraderPositionUpdate = _lastFirmPositionUpdate;
                    UsingFirmPosition = true;
                }
                else if (UsingFirmPosition)
                {
                    UsingFirmPosition = false;
                }

                if (lastTraderPositionUpdate != null)
                {
                    double bestDeltaAdjPx = double.NaN;
                    double deltaAdjContraPx;
                    if (Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        bestDeltaAdjPx = ((underMid - lastTraderPositionUpdate.BestSellPriceUnderMid) * totalDelta) + lastTraderPositionUpdate.BestSellPrice;
                        deltaAdjContraPx = TicketStyle switch
                        {
                            OrderTicketStyle.Combined when OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg => -(((underMid - lastTraderPositionUpdate.BestBuyPriceUnderMid) * contraDelta) + lastTraderPositionUpdate.BestBuyPrice),
                            _ => ((underMid - lastTraderPositionUpdate.BestBuyPriceUnderMid) * contraDelta) + lastTraderPositionUpdate.BestBuyPrice,
                        };
                    }
                    else
                    {
                        bestDeltaAdjPx = ((underMid - lastTraderPositionUpdate.BestBuyPriceUnderMid) * totalDelta) + lastTraderPositionUpdate.BestBuyPrice;
                        deltaAdjContraPx = TicketStyle switch
                        {
                            OrderTicketStyle.Combined when OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical && !IsSingleLeg => -(((underMid - lastTraderPositionUpdate.BestSellPriceUnderMid) * contraDelta) + lastTraderPositionUpdate.BestSellPrice),
                            _ => ((underMid - lastTraderPositionUpdate.BestSellPriceUnderMid) * contraDelta) + lastTraderPositionUpdate.BestSellPrice,
                        };
                    }

                    BestDeltaAdjPx = bestDeltaAdjPx;
                    BestDeltaAdjContraPx = deltaAdjContraPx;
                    if (!IsBasketOrder)
                    {
                        if (LockBestDeltaAdjPrice && !double.IsNaN(BestDeltaAdjPx))
                        {
                            SetPrice(BestDeltaAdjPx);
                        }

                        if (LockContraBestDeltaAdjPrice && !double.IsNaN(BestDeltaAdjContraPx))
                        {
                            SetContraPrice(BestDeltaAdjContraPx);
                        }
                    }
                }

                if (!IsBasketOrder)
                {
                    EdgeProjector?.CalculateEdge();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeltaAdjPrice));
            }
        }

        private void DeltaAdjEdgeSummary()
        {
            try
            {
                if (_adjEdgeSummaryLoaded)
                {
                    double underMid = _underMid;
                    double totalDelta = TotalDelta;

                    double changeInUnder = (underMid - _adjEdgeSummaryUnderMidAtLoad) * totalDelta;
                    AdjEdgeSummaryBid = changeInUnder + _adjEdgeSummaryBidBase;
                    AdjEdgeSummaryAsk = changeInUnder + _adjEdgeSummaryAskBase;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeltaAdjEdgeSummary));
            }
        }

        private void DeltaAdjHedgePrice()
        {
            double spreadDelta = Side == ZeroPlus.Models.Data.Enums.Side.Buy || IsSingleLeg ? TotalDelta : -TotalDelta;
            StockHedgeAdjTradePx = ((UnderMid - StockPriceAtHedge) * spreadDelta) + AdjustedPriceAtHedge;
        }

        private void HandleTradeUpdate(ref TradeUpdateModel trade)
        {
            LastTradeUpdate = trade;
        }

        private void HandleHedgePositionUpdate(IPosition spreadPosition)
        {
            try
            {
                UpdateHedgeSpreadPositionUi(spreadPosition);
            }
            finally
            {
                CalculateUnrealizedPnlAndPosNetDelta();
            }
        }

        private void HandleTraderSpreadPositionUpdate(IPosition spreadPosition, bool isFromCache)
        {
            try
            {
                _lastTraderPositionUpdate = spreadPosition;
                UpdateTraderSpreadPositionUi(spreadPosition, checkForHedge: !isFromCache);
                if (spreadPosition.NetQty == 0)
                {
                    Side? buy = ZeroPlus.Models.Data.Enums.Side.Buy;
                    Side? sell = ZeroPlus.Models.Data.Enums.Side.Sell;
                    _spreadIdAndSideToLastAvgFillPxMap.TryRemove(Tuple.Create(SpreadId, buy), out _);
                    _spreadIdAndSideToLastAvgFillPxMap.TryRemove(Tuple.Create(SpreadId, sell), out _);
                }
                DeltaAdjPriceAsync();
            }
            finally
            {
                CalculateUnrealizedPnlAndPosNetDelta();
            }
        }

        private void HandleFirmSpreadPositionUpdate(IPosition spreadPosition)
        {
            try
            {
                _lastFirmPositionUpdate = spreadPosition;
                UpdateSpreadPositionUi(spreadPosition);
            }
            finally
            {
                UpdateStockPositions();
                CalculateUnrealizedPnlAndPosNetDelta();
            }
        }

        private void UpdateSpreadPositionUi(IPosition spreadPosition)
        {
            if (spreadPosition.TotalSubmissions > 0)
            {
                FirmLastBuyAttemptEdgeToTheo = spreadPosition.LastBuyAttemptEdgeToTheo;
                FirmLastSellAttemptEdgeToTheo = spreadPosition.LastSellAttemptEdgeToTheo;

                FirmLastBuyAttempt = spreadPosition.LastBuyAttempt;
                FirmLastBuyAttemptUnderlying = spreadPosition.LastBuyAttemptUnderlying;

                FirmLastSellAttempt = spreadPosition.LastSellAttempt;
                FirmLastSellAttemptUnderlying = spreadPosition.LastSellAttemptUnderlying;

                if (spreadPosition.TotalFills > 0)
                {
                    SpreadPosition = spreadPosition.NetQty;
                    SpreadRawPosition = spreadPosition.RawNetQty;
                    AdjustedPnl = spreadPosition.AdjustedPnl - ReversePnl;
                    OpenPositionAveragePrice = spreadPosition.OpenPositionAveragePrice;
                    SpreadPositionInitialized = true;

                    FirmLastEdge = spreadPosition.LastEdge;
                    FirmLastTrader = spreadPosition.LastTrader;

                    FirmLastTradeSide = spreadPosition.LastTradeSide;
                    FirmLastTradeTime = spreadPosition.LastTradeTime;

                    FirmLastBuyOrderEdgeToTheo = spreadPosition.LastBuyEdgeToTheo;
                    FirmLastSellOrderEdgeToTheo = spreadPosition.LastSellEdgeToTheo;

                    FirmLastFillBuyEdgeToTheo = spreadPosition.LastBuyFillEdgeToTheo;
                    FirmLastFillSellEdgeToTheo = spreadPosition.LastSellFillEdgeToTheo;

                    FirmLastBuyEdge = spreadPosition.LastBuyEdge;
                    FirmLastSellEdge = spreadPosition.LastSellEdge;

                    BestBuyEdgeToTheo = spreadPosition.BestBuyEdgeToTheo;
                    BestSellEdgeToTheo = spreadPosition.BestSellEdgeToTheo;

                    WorstBuyEdgeToTheo = spreadPosition.WorstBuyEdgeToTheo;
                    WorstSellEdgeToTheo = spreadPosition.WorstSellEdgeToTheo;
                }
            }
        }

        private void HandleHardSideUpdate(HardSideResult hardSideResult)
        {
            var key = this.GetHardSideKey();
            if (key.HasValue)
            {
                HardSideKey hardSideKey = hardSideResult.HardSideKey;
                if (KeyEquals(key.Value, hardSideKey))
                {
                    bool isValid = IsSingleLeg
                                   || (key.Value.BaseStrategy is BaseStrategy.CALL_CALENDAR or BaseStrategy.PUT_CALENDAR or BaseStrategy.CALL_DIAGONAL or BaseStrategy.PUT_DIAGONAL)
                                   || Legs.Select(x => x.Strike.Strike).OrderBy(x => x).ToList().ValidateHardSideStrikes(hardSideResult.Strikes);
                    if (isValid)
                    {
                        HardSide = hardSideResult.HardSide;
                        HardSideDesignationTime = hardSideResult.DesignationTime;
                        HardSideBuyGiveUp = hardSideResult.HardSideBuyGiveUp;
                        HardSideSellGiveUp = hardSideResult.HardSideSellGiveUp;
                    }
                    else
                    {
                        _log.Warn("Msg: Hard Side update not valid, Spread: {}, CurrentKey: {}, CurrentHardSide: {}, UpdateKey: {}, HardSide: {}, DesignationTime: {}", SpreadId, hardSideKey, HardSide, hardSideResult.HardSideKey, hardSideResult.HardSide, hardSideResult.DesignationTime);
                    }
                }
                else
                {
                    _log.Warn("Msg: Hard Side update key match failed, Spread: {}, CurrentKey: {}, CurrentHardSide: {}, UpdateKey: {}, HardSide: {}, DesignationTime: {}", SpreadId, hardSideKey, HardSide, hardSideResult.HardSideKey, hardSideResult.HardSide, hardSideResult.DesignationTime);
                }
            }
            else
            {
                _log.Warn("Msg: Hard Side update key lookup failed, Spread: {}, CurrentKey: {}, UpdateKey: {}, HardSide: {}, DesignationTime: {}", SpreadId, HardSide, hardSideResult.HardSideKey, hardSideResult.HardSide, hardSideResult.DesignationTime);
            }
        }

        private static bool KeyEquals(HardSideKey key, HardSideKey other)
        {
            return key.Underlying == other.Underlying && key.ExpirationKey == other.ExpirationKey && key.BaseStrategy == other.BaseStrategy;
        }

        private void CalculateUnrealizedPnlAndPosNetDelta()
        {
            try
            {
                double spreadMid = Math.Abs(Mid);
                int pos = _spreadPosition;
                int realSpreadPosition = pos - ReverseSpreadPosition;
                int spreadPosition = Math.Abs(realSpreadPosition);
                double openPositionAveragePrice = Math.Abs(OpenPositionAveragePrice);

                bool isInverted = IsComplexOrder && (Side == ZeroPlus.Models.Data.Enums.Side.Buy && Math.Abs(Low) > Math.Abs(High) || Side == ZeroPlus.Models.Data.Enums.Side.Sell && Math.Abs(Low) < Math.Abs(High));
                if (isInverted)
                {
                    pos *= -1;
                    realSpreadPosition *= -1;
                }

                if (pos > 0 && realSpreadPosition > 0)
                {
                    UnrealizedPnl = (spreadMid - openPositionAveragePrice) * spreadPosition * Multiplier;
                    AvgCost = -OpenPositionAveragePrice;
                }
                else if (pos < 0 && realSpreadPosition < 0)
                {
                    UnrealizedPnl = (openPositionAveragePrice - spreadMid) * spreadPosition * Multiplier;
                    AvgCost = -OpenPositionAveragePrice;
                }
                else
                {
                    UnrealizedPnl = double.NaN;
                    AvgCost = double.NaN;
                }

                double spreadDelta = IsSingleLeg || Side == ZeroPlus.Models.Data.Enums.Side.Buy ? TotalDelta : -TotalDelta;
                HedgeNetDelta = HedgedStocks / HedgeMultiplier;
                PositionNetDelta = realSpreadPosition * spreadDelta * Multiplier;
                PositionNetWeightedVega = realSpreadPosition * WeightedVega;
                FirmLastTradeTimeAgo = FirmLastTradeTime == default || FirmLastTradeTime.Value.Date == _epochDate ? double.NaN : (DateTime.Now - FirmLastTradeTime.Value).TotalMinutes;
                CalculateEdgeToMarket(logOnly: true, liveUpdate: true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CalculateUnrealizedPnlAndPosNetDelta));
            }
        }

        private void UpdateHedgeSpreadPositionUi(IPosition hedgePosition)
        {
            try
            {
                HedgedStocks = hedgePosition.NetQty;
                StockHedgeAdjPnl = hedgePosition.AdjustedPnl - HedgeReversePnl;
                StockHedgeOpenPositionAveragePrice = hedgePosition.OpenPositionAveragePrice;
                UpdateStockPositions();
                CanHedge = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateHedgeSpreadPositionUi));
            }
        }

        private void UpdateStockHedgeUnrealizedPnl()
        {
            if (!IsBasketOrder || BasketSettings.HedgeAutoEnabled)
            {
                double hedgeAvgPx = Math.Abs(StockHedgeOpenPositionAveragePrice);
                double hedgeMid = Math.Abs(HedgeMid);

                int realHedgedPos = HedgedStocks - HedgeReversePosition;
                if (HedgedStocks > 0 && realHedgedPos > 0)
                {
                    StockHedgeUnrealizedPnl = (hedgeMid - hedgeAvgPx) * realHedgedPos;
                }
                else if (HedgedStocks < 0 && realHedgedPos < 0)
                {
                    StockHedgeUnrealizedPnl = (hedgeAvgPx - hedgeMid) * -realHedgedPos;
                }
                else
                {
                    StockHedgeUnrealizedPnl = double.NaN;
                }

                if (RequiredStocks != 0 || HedgedStocks != 0)
                {
                    WaitForUnderMidLoadAsync().ContinueWith(t =>
                    {
                        if (t.Result)
                        {
                            double underWidth = UnderAsk - UnderBid;
                            EstHedgeCost = -Math.Abs(Math.Round((Math.Abs(RequiredStocks) + Math.Abs(HedgedStocks)) * underWidth, 2));

                            if (RequiredStocks != 0 && _lastTraderPositionUpdate != null && _lastTraderPositionUpdate.OpenPositionFillUnderPrice != 0 && !double.IsNaN(_lastTraderPositionUpdate.OpenPositionFillUnderPrice))
                            {
                                int actRequired = RequiredStocks + HedgedStocks;
                                if (actRequired < 0 && RequiredStocks < 0)
                                {
                                    HedgeSuggestion = underWidth < OmsCore.Config.MaxHedgeWidthV2 && UnderMid >= _lastTraderPositionUpdate.OpenPositionFillUnderPrice ? HedgeSuggestion.SuggestHedge : HedgeSuggestion.DoNotSuggestHedge;
                                }
                                else if (actRequired > 0 && RequiredStocks > 0)
                                {
                                    HedgeSuggestion = underWidth < OmsCore.Config.MaxHedgeWidthV2 && UnderMid <= _lastTraderPositionUpdate.OpenPositionFillUnderPrice ? HedgeSuggestion.SuggestHedge : HedgeSuggestion.DoNotSuggestHedge;
                                }
                                else
                                {
                                    HedgeSuggestion = HedgeSuggestion.None;
                                }
                            }
                            else
                            {
                                HedgeSuggestion = HedgeSuggestion.None;
                            }
                        }
                        else
                        {
                            EstHedgeCost = double.NaN;
                        }
                    });
                }
                CalculateEdgeToMarket(logOnly: true, liveUpdate: true);
            }
        }

        private void UpdateTraderSpreadPositionUi(IPosition spreadPosition, bool checkForHedge = false)
        {
            object hedgeLock = AcquireSpreadHedgeLock();
            lock (hedgeLock)
            {
                SingleOrderTicketPosition = spreadPosition.NetQty;
                TraderSpreadPosition = spreadPosition.NetQty;
                TraderAdjustedPnl = spreadPosition.AdjustedPnl;
                TraderSpreadPositionInitialized = true;
                AutoCloseArmed = true;
                UpdateStockPositions();
                if (checkForHedge)
                {
                    CheckForHedgeAutoFlatten();
                }
            }
        }

        private void SetNetAndPercentChange(double last)
        {
            if (IsBasketOrder)
            {
                return;
            }
            if (UnderlyingClosingInitialized && last != 0.0)
            {
                double netChange = last - UnderlyingClosing;
                NetChange = netChange;
                PercentChange = netChange / UnderlyingClosing * 100;
            }
        }

        public virtual void Dispose()
        {
            try
            {
                _log.Info("Dispose started for order model for " + SpreadId + " " + IsDisposed);
                DisposeNoCancel();

                _log.Info($"Disposing PermCloser for order {SpreadId} {PermCloser == null}");
                PermCloser?.Dispose();

                _log.Info($"Disposing Looper for order {SpreadId} {Looper == null}");
                Looper?.Dispose();

                _log.Info($"Disposing Tracker for order {SpreadId} {Tracker == null}");
                Tracker?.Dispose();

                _log.Info($"Disposing Closer for order {SpreadId} {Closer == null}");
                Closer?.Dispose();

                _log.Info($"Disposing CxlReplaceCloser for order {SpreadId} {CxlReplaceCloser == null}");
                CxlReplaceCloser?.Dispose();

                _log.Info($"Disposing Fisher for order {SpreadId} {Fisher == null}");
                Fisher?.Dispose();

                _log.Info($"Disposing LegOutCloser for order {SpreadId} {LegOutCloser == null}");
                LegOutCloser?.Dispose();

                _log.Info($"Disposing AutoLegCloser for order {SpreadId} {AutoLegCloser == null}");
                AutoLegCloser?.Dispose();

                _log.Info($"Disposing StopLossManager for order {SpreadId} {StopLossManager == null}");
                StopLossManager?.Dispose();

                _log.Info($"Disposing StopOrderManager for order {SpreadId} {StopOrderManager == null}");
                StopOrderManager?.Dispose();

                _log.Info($"Disposing AutoCloseManager for order {SpreadId} {AutoCloseManager == null}");
                AutoCloseManager?.Dispose();

                _log.Info($"Disposing ThreeWayCloser for order {SpreadId} {ThreeWayCloser == null}");
                ThreeWayCloser?.Dispose();

                _log.Info($"Disposing SweepCloser for order {SpreadId} {SweepCloser == null}");
                SweepCloser?.Dispose();
            }
            catch (Exception ex)
            {
                try
                {
                    _log.Error(ex, nameof(Dispose) + " " + SpreadId);
                }
                catch (Exception ex2)
                {
                    _log.Error(ex, nameof(Dispose));
                    _log.Error(ex2, nameof(Dispose));
                }
            }
        }

        public void DisposeNoCancel()
        {
            try
            {
                isDisposing = true;
                IsDisposed = true;
                DisposeLegs();
                _lastTraderPositionUpdate = null;
                _lastFirmPositionUpdate = null;
                _portfolioManagerModel?.UnsubscribeAll(this);
                OmsCore.QuoteClient.UnsubscribeAll(this);
                OmsCore.GreekClient.UnsubscribeAll(this);
                OmsCore.UpdateManager.UnsubscribeAll(this);
                OmsCore.HerculesClientWrapper.UnsubscribeAll(this);
                RemoveEdgeProjector();
                Legs?.Clear();
                _notifiers = null;
                DisposeGeneratedNotifiers();
                Leg1 = null;
                Leg2 = null;
                Leg3 = null;
                Leg4 = null;
                BasketSettings = null;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisposeNoCancel));
            }
        }

        private void DisposeLegs()
        {
            try
            {
                foreach (TicketLegModel leg in Legs)
                {
                    leg.LegUpdatedEvent -= UpdateTicketValues;
                    leg.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisposeLegs));
            }
        }

        internal void Close()
        {
            CurrentWindowService?.Close();
        }

        internal void SetPrice(double price)
        {
            SetPriceMinimal(price);
            UpdatePrice();
            UpdateTicketValues();
            ValidateTicket();
        }

        protected void SetPriceMinimal(double price)
        {
            if (!double.IsNaN(price))
            {
                Price = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
            }
        }

        protected void SetContraPriceMinimal(double price)
        {
            if (!double.IsNaN(price))
            {
                price = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : Math.Round(price, 2, MidpointRounding.AwayFromZero);

                ContraPrice = OmsCore.Config.PriceEvaluationStyle switch
                {
                    PriceEvaluationStyle.Reversed when !IsSingleLeg => -price,
                    _ => price,
                };
            }
        }

        internal void SetContraPrice(double price)
        {
            price = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : Math.Round(price, 2, MidpointRounding.AwayFromZero);

            ContraPrice = OmsCore.Config.PriceEvaluationStyle switch
            {
                PriceEvaluationStyle.Reversed when !IsSingleLeg => -price,
                _ => price,
            };
            UpdatePrice();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal async Task<OrderTicket> LoadMorphFromOrderAsync(OrderTicket orderTicketViewModel, List<Option> options, DataStore _deltaStore)
        {
            if (options.Count <= 0)
            {
                _log.Info(nameof(LoadMorphFromOrderAsync) + " Disposing order model for " + SpreadId);
                Dispose();
                return null;
            }
            else
            {
                Underlying = options.FirstOrDefault().UnderlyingSymbol.ToUpper();
                LoadDefaultAccount();
                SubscribeDataAsync();

                List<Option> listOfOptions = new();
                List<Task<bool>> morphTasks = new();

                for (int i = 0; i < orderTicketViewModel.Legs.Count; i++)
                {
                    TicketLegModel leg = orderTicketViewModel.Legs[i];
                    try
                    {
                        double expDateDiff = 0;
                        List<Option> filteredByType = options.Where(x => x.Type.ToString().ToUpper() == leg.Type.ToUpper()).ToList();
                        List<Option> selectedExpirations = filteredByType.Where(x => Math.Abs((x.Expiration - leg.ExpirationInfo.Expiration).TotalDays) <= 33).ToList();
                        if (i > 0)
                        {
                            expDateDiff = (leg.ExpirationInfo.Expiration - orderTicketViewModel.Legs[i - 1].ExpirationInfo.Expiration).TotalDays;
                            selectedExpirations = selectedExpirations.Where(x => (x.Expiration - Legs[i - 1].ExpirationInfo.Expiration).TotalDays >= expDateDiff).ToList();
                        }
                        if (selectedExpirations.Count > 0)
                        {
                            listOfOptions = selectedExpirations.GroupBy(x => x.Expiration.Date)
                                                               .OrderBy(x => Math.Abs((x.Key - leg.ExpirationInfo.Expiration).TotalDays))
                                                               .FirstOrDefault()
                                                               .Select(x => x)
                                                               .ToList();
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(LoadMorphFromOrderAsync) + " encountered an exception disposing order model. Base spread: " + orderTicketViewModel.SpreadId);
                        Dispose();
                        return null;
                    }

                    if (listOfOptions == null || listOfOptions.Count == 0)
                    {
                        _log.Info(nameof(LoadMorphFromOrderAsync) + " no list found disposing order model. Base spread: " + orderTicketViewModel.SpreadId);
                        Dispose();
                        return null;
                    }

                    Task<bool> legMorphTask = orderTicketViewModel.WaitForUnderMidLoadAsync().ContinueWith(t =>
                    {
                        double coefficient = orderTicketViewModel.Last / leg.Strike.Strike;
                        bool result = WaitForUnderMidLoadAsync().ContinueWith(async t2 =>
                                                {
                                                    double nearestStrike = coefficient * Last;
                                                    IOrderedEnumerable<Option> orderedByStrike = listOfOptions.OrderBy(x => Math.Abs(x.Strike - nearestStrike));

                                                    Option selectedOption = null;
                                                    double smallestChange = double.MaxValue;
                                                    foreach (Option option in orderedByStrike)
                                                    {
                                                        double delta = await _deltaStore.GetDataAsync(option.OptionSymbol);
                                                        double deltaChange = Math.Abs(delta - leg.Delta);
                                                        if (deltaChange < smallestChange)
                                                        {
                                                            smallestChange = deltaChange;
                                                            selectedOption = option;
                                                        }
                                                        if (deltaChange == 0)
                                                        {
                                                            break;
                                                        }
                                                    }

                                                    if (selectedOption == null)
                                                    {
                                                        _log.Info(nameof(LoadMorphFromOrderAsync) + " no option selected disposing order model. Base spread: " + orderTicketViewModel.SpreadId);
                                                        Dispose();
                                                        return false;
                                                    }

                                                    TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                                                    {
                                                        Side = leg.Side,
                                                        Ratio = leg.Ratio,
                                                        Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                                                        Type = leg.Type,
                                                        Position = Positions.AUTO.ToString(),
                                                        Symbol = selectedOption.OptionSymbol,
                                                    };
                                                    Legs.Add(legClone);

                                                    ExpirationInfoModel expirationInfoModel = new(selectedOption.Expiration, selectedOption.RootSymbol);
                                                    legClone.ExpirationsList.Add(expirationInfoModel);
                                                    legClone.ExpirationInfo = expirationInfoModel;

                                                    bool isUnique = options.Count(x => x.Strike == selectedOption.Strike) <= 2;
                                                    StrikeInfoModel strike = new(isUnique, selectedOption.Strike);
                                                    if (!legClone.StrikesList.Contains(strike))
                                                    {
                                                        legClone.StrikesList.Add(strike);
                                                    }
                                                    legClone.Strike = strike;

                                                    await legClone.ValidateLegAsync();
                                                    return true;
                                                }).Result.Result;
                        return result;
                    });
                    await legMorphTask;
                    morphTasks.Add(legMorphTask);
                }

                await Task.WhenAll(morphTasks);
                try
                {
                    foreach (Task<bool> morphTask in morphTasks)
                    {
                        if (!morphTask.Result || Legs.Any(x => x == null))
                        {
                            _log.Info(nameof(LoadMorphFromOrderAsync) + " 4 Disposing order model for " + SpreadId);
                            Dispose();
                            return null;
                        }
                    }
                }
                catch (Exception)
                {
                    _log.Info(nameof(LoadMorphFromOrderAsync) + " 5 Disposing order model for " + SpreadId);
                    Dispose();
                    return null;
                }
                Route = GetBestRoute();
                TimeInForce = orderTicketViewModel.TimeInForce;
                RatioLocked = true;
                PostUpdate();
                return this;
            }
        }

        public void LoadDefaultAccount()
        {
            string defaultAccount = OmsCore.Config.DefaultAccount;

            if (IsLowLatencyHangManager)
            {
                defaultAccount = OmsCore.Config.LowLatencyAccounts.FirstOrDefault();
                if (!AccountsList.Contains(defaultAccount))
                {
                    Dispatcher.BeginInvoke(() => AccountsList.Add(defaultAccount));
                }
            }

            if (!string.IsNullOrWhiteSpace(defaultAccount) && OmsCore.User.Accounts.Any(x => x.Equals(defaultAccount, StringComparison.OrdinalIgnoreCase)))
            {

                Account = defaultAccount;
            }
            else
            {
                Account = AccountsList.FirstOrDefault();
            }
        }

        internal async Task LoadFromHedgeAsync(string underlying, Side side, int qty)
        {
            Underlying = underlying;
            LoadDefaultAccount();

            TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
            {
                Side = side,
                Ratio = 1,
                Quantity = qty,
                Type = Types.STOCK.ToString(),
                Position = Positions.AUTO.ToString(),
            };
            await legClone.LoadExpirationsListAsync();
            legClone.UpdateStrikesList();
            legClone.UpdateStrikeVisibility();
            Legs.Add(legClone);
            await legClone.ValidateLegAsync();

            _ = UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            Route = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal virtual async Task LoadFromTicketAsync(OrderTicket orderTicket, bool flipCP = false, bool copyPosEffect = false, bool forContra = false)
        {
            _log.Info($"[Trace-1] Load From Ticket Start. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");
            Underlying = orderTicket.Underlying;
            LoadDefaultAccount();
            TimeInForce = orderTicket.TimeInForce;
            _log.Info($"[Trace-1] Default Account Loaded. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");

            foreach (TicketLegModel leg in orderTicket.Legs)
            {
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                    Type = !flipCP ? leg.Type : leg.Type == Types.CALL.ToString() ? Types.PUT.ToString() : leg.Type == Types.PUT.ToString() ? Types.CALL.ToString() : leg.Type,
                    Position = copyPosEffect ? leg.Position : Positions.AUTO.ToString()
                };
                await legClone.LoadExpirationsListAsync();

                if (legClone.Type != Types.STOCK.ToString())
                {
                    legClone.ExpirationInfo = legClone.ExpirationsList.Where(x => x.Equals(leg.ExpirationInfo)).FirstOrDefault();

                    if (legClone.ExpirationInfo == null && leg.ExpirationInfo != null)
                    {
                        legClone.ExpirationInfo =
                            new ExpirationInfoModel(leg.ExpirationInfo.Expiration, leg.ExpirationInfo.RootSymbol);
                        legClone.ExpirationsList.Add(legClone.ExpirationInfo);
                    }

                    legClone.UpdateStrikesList();

                    legClone.Strike = legClone.StrikesList.Where(x => x == leg.Strike).FirstOrDefault();
                }

                legClone.UpdateStrikeVisibility();
                Legs.Add(legClone);
                await legClone.ValidateLegAsync();
            }
            _log.Info($"[Trace-1] Legs Loaded. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");

            await UpdateAccountsAndRoutes();
            _log.Info($"[Trace-1] Accounts and Routes Loaded. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");

            SubscribeToLegUpdates();
            RatioLocked = true;
            _log.Info($"[Trace-1] Subscribed to leg updates. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");

            SubscribeDataAsync();
            _log.Info($"[Trace-1] Subscribed to data. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");
            bool reversed = UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
            _log.Info($"[Trace-1] Ticket validation complete. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");
            LoadOrderStateFromTicket(orderTicket, reversed, forContra);
            _log.Info($"[Trace-1] Loading order state complete. Spread: {orderTicket.SpreadId}, LastId: {orderTicket.OrderId}/{orderTicket.ContraOrderId}, State: {orderTicket.OrderStatus}/{orderTicket.ContraOrderStatus}");
        }

        protected void LoadOrderStateFromTicket(OrderTicket orderTicket, bool reversed, bool forContra = false)
        {
            if (!reversed || TicketStyle != OrderTicketStyle.Combined)
            {
                AveragePrice = orderTicket.AveragePrice;
                BestAveragePrice = orderTicket.AveragePrice;
                ContraAveragePrice = orderTicket.ContraAveragePrice;
                LastMainUnderMidAtFill = orderTicket.LastMainUnderMidAtFill;
                LastMainUnderMidAtBestFill = orderTicket.LastMainUnderMidAtFill;
                LastMainTotalVolumeAtFill = orderTicket.LastMainTotalVolumeAtFill;
                LastContraTotalVolumeAtFill = orderTicket.LastContraTotalVolumeAtFill;
                LastContraUnderMidAtFill = orderTicket.LastContraUnderMidAtFill;

                double price = !forContra && orderTicket.AveragePrice != 0 && !double.IsNaN(orderTicket.AveragePrice)
                    ? orderTicket.AveragePrice
                    : orderTicket.Price;
                SetPrice(price);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    double defaultContraEdge = GetDefaultContraEdge();
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetContraPrice(price - defaultContraEdge);
                    }
                    else
                    {
                        SetContraPrice(price + defaultContraEdge);
                    }
                }
            }
            else
            {
                AveragePrice = orderTicket.ContraAveragePrice;
                BestAveragePrice = orderTicket.ContraAveragePrice;

                ContraAveragePrice = orderTicket.AveragePrice;
                LastContraUnderMidAtFill = orderTicket.LastMainUnderMidAtFill;
                LastContraTotalVolumeAtFill = orderTicket.LastMainTotalVolumeAtFill;

                LastMainUnderMidAtBestFill = orderTicket.LastContraUnderMidAtFill;
                LastMainTotalVolumeAtFill = orderTicket.LastContraTotalVolumeAtFill;
                LastMainUnderMidAtFill = orderTicket.LastContraUnderMidAtFill;

                double price = !forContra && orderTicket.AveragePrice != 0 && !double.IsNaN(orderTicket.AveragePrice)
                    ? orderTicket.AveragePrice
                    : orderTicket.Price;
                price = OmsCore.Config.PriceEvaluationStyle switch
                {
                    PriceEvaluationStyle.Identical when !IsSingleLeg => -price,
                    _ => price,
                };
                SetContraPrice(price);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    double defaultContraEdge = GetDefaultContraEdge();
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetPrice(price + defaultContraEdge);
                    }
                    else
                    {
                        SetPrice(price - defaultContraEdge);
                    }
                }
            }
        }

        internal void LoadSingleLeg(string underlying, string type, DateTime expiration, double strike, List<Option> options)
        {
            Underlying = underlying;
            LoadDefaultAccount();

            List<double> strikes = OmsCore.QuoteClient.OptionsLookup.GetOptionsWithExpiration(Underlying, expiration).Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();

            TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
            {
                Side = ZeroPlus.Models.Data.Enums.Side.Buy,
                Ratio = 1,
                Quantity = 1, // Set quantity to ratio to set the min value possible
                Type = type,
                Position = Positions.AUTO.ToString(),
            };

            Option leg = options.FirstOrDefault(x => x.Expiration.Date == expiration.Date);
            if (leg != null)
            {
                ExpirationInfoModel expModel = new(leg.Expiration, leg.RootSymbol);
                legClone.ExpirationInfo = expModel;
                legClone.ExpirationsList.Add(expModel);
                legClone.Strike = new StrikeInfoModel(false, strikes.MinBy(x => Math.Abs(x - strike)));
                legClone.StrikesList.Add(legClone.Strike);

                legClone.UpdateStrikeVisibility();
            }

            Legs.Add(legClone);
            legClone.ValidateLegAsync();

            _ = UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal async void LoadMinimalFromTicket(OrderTicket orderTicketViewModel)
        {
            ClearTicket();
            Underlying = orderTicketViewModel.Underlying;
            LoadDefaultAccount();
            TimeInForce = orderTicketViewModel.TimeInForce;
            InstanceMode = orderTicketViewModel.InstanceMode;
            BrokerOverride = orderTicketViewModel.BrokerOverride;
            await SetPriceIncrementAsync();
            foreach (TicketLegModel leg in orderTicketViewModel.Legs)
            {
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value possible
                    Type = leg.Type,
                    Position = Positions.AUTO.ToString(),
                    Symbol = leg.Symbol,
                };

                ExpirationInfoModel expirationInfoModel = leg.ExpirationInfo?.Clone();
                if (expirationInfoModel != null)
                {
                    legClone.ExpirationsList.Add(expirationInfoModel);
                }
                legClone.ExpirationInfo = expirationInfoModel;
                legClone.StrikesList.Add(leg.Strike);
                legClone.Strike = leg.Strike;
                Legs.Add(legClone);
            }

            AveragePrice = orderTicketViewModel.AveragePrice;
            BestAveragePrice = orderTicketViewModel.AveragePrice;
            ContraAveragePrice = orderTicketViewModel.ContraAveragePrice;
            LastMainUnderMidAtFill = orderTicketViewModel.LastMainUnderMidAtFill;
            LastMainUnderMidAtBestFill = orderTicketViewModel.LastMainUnderMidAtFill;
            LastContraUnderMidAtFill = orderTicketViewModel.LastContraUnderMidAtFill;
            LastMainTotalVolumeAtFill = orderTicketViewModel.LastMainTotalVolumeAtFill;
            LastContraTotalVolumeAtFill = orderTicketViewModel.LastContraTotalVolumeAtFill;
            UpdateDescription();
            RatioLocked = true;
            SubscribeData();
        }

        internal async Task LoadFromTradeAsync(OpraDatabaseTradeModel trade, bool withStockLeg = false)
        {
            Underlying = trade.UnderSymbol;

            foreach (TicketLegModel leg in ParseFromTos(trade.Symbol))
            {
                Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                string type = leg.Symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value 
                    Type = type,
                };
                await legClone.LoadExpirationsListAsync();
                legClone.UpdateStrikesList();
                legClone.Strike = legClone.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                legClone.UpdateStrikeVisibility();
                ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);
                legClone.ExpirationInfo = expirationInfoModel;
                Legs.Add(legClone);
                await legClone.ValidateLegAsync();
            }

            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
            await UpdateAccountsAndRoutes();
            bool isBuy = trade.DeltaAdjTheo > trade.Price || double.IsNaN(trade.Price);
            var delta = trade.TradeDelta;

            if (!isBuy)
            {
                Reverse();
                if (!IsSingleLeg)
                {
                    delta *= -1;
                }
            }

            double price = !isBuy && !IsSingleLeg ? -trade.Price : trade.Price;
            if (withStockLeg)
            {
                var added = await SetupStockTieAsync(delta);
                price += added * added > 0 ? trade.UnderAsk : trade.UnderBid;
            }

            SetPrice(price);
            if (TicketStyle == OrderTicketStyle.Combined)
            {
                double defaultContraEdge = GetDefaultContraEdge();
                if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                {
                    SetContraPrice(price - defaultContraEdge);
                }
                else
                {
                    SetContraPrice(price + defaultContraEdge);
                }
            }
        }

        internal async Task LoadFromOpenTicketRequestAsync(OpenTicketRequest openTicketRequest, object[] windowParameter, Dominator dominator)
        {
            ReversePrompted = true;
            foreach (TicketLegModel leg in ParseFromTos(openTicketRequest.Symbol))
            {
                Underlying = leg.Underlying;

                Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                string type = leg.Symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = leg.Side,
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio, // Set quantity to ratio to set the min value 
                    Type = type,
                };
                await legClone.LoadExpirationsListAsync();
                ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);
                legClone.ExpirationInfo = legClone.ExpirationsList.Where(x => x.Equals(expirationInfoModel)).FirstOrDefault();
                legClone.UpdateStrikesList();
                legClone.Strike = legClone.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                legClone.UpdateStrikeVisibility();
                Legs.Add(legClone);
                await legClone.ValidateLegAsync();
            }

            _ = UpdateAccountsAndRoutes().ContinueWith(t =>
            {
                string defaultRoute = Route;
                Route = !string.IsNullOrWhiteSpace(openTicketRequest.Route) ? openTicketRequest.Route : defaultRoute;
                ContraRoute = !string.IsNullOrWhiteSpace(openTicketRequest.ClosingRoute) ? openTicketRequest.ClosingRoute : defaultRoute;
            });

            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();

            double defaultContraEdge = GetDefaultContraEdge();
            AveragePrice = openTicketRequest.Price == 0 ? double.NaN : openTicketRequest.Price;
            BestAveragePrice = openTicketRequest.Price == 0 ? double.NaN : openTicketRequest.Price;
            ContraAveragePrice = openTicketRequest.ContraPrice == 0 ? double.NaN : IsSingleLeg ? openTicketRequest.ContraPrice : -openTicketRequest.ContraPrice;
            LastMainUnderMidAtBestFill = LastMainUnderMidAtFill = openTicketRequest.UnderPrice == 0 ? double.NaN : openTicketRequest.UnderPrice;
            LastContraUnderMidAtFill = openTicketRequest.ContraUnderPrice == 0 ? double.NaN : openTicketRequest.ContraUnderPrice;
            double edge = !double.IsNaN(openTicketRequest.Edge) && openTicketRequest.Edge > 0 ? openTicketRequest.Edge : defaultContraEdge;

            if (dominator != null)
            {
                OrderClosedUpdateEvent += (order, status, _) =>
                    dominator.HandleOrderUpdate(openTicketRequest.Symbol, order, status);
            }

            if (openTicketRequest.TicketType == TicketType.ThreeWay && dominator != null)
            {
                await ThreeWayFromRequestAsync(openTicketRequest, windowParameter, dominator);
            }
            else if (openTicketRequest.TicketType == TicketType.Single)
            {
                ReversePrompted = false;
                UpdateTicketSide();
                DeltaAdjPrice();
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    if (!double.IsNaN(AveragePrice))
                    {
                        SetPrice(AveragePrice);
                        if (!double.IsNaN(ContraAveragePrice))
                        {
                            double contraPrice = IsSingleLeg ? ContraAveragePrice : -ContraAveragePrice;
                            SetContraPrice(contraPrice);
                        }
                        else
                        {
                            if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                            {
                                SetContraPrice(AveragePrice - edge);
                            }
                            else
                            {
                                SetContraPrice(AveragePrice + edge);
                            }
                        }
                    }
                    else if (!double.IsNaN(ContraAveragePrice))
                    {
                        double contraPrice = IsSingleLeg ? ContraAveragePrice : -ContraAveragePrice;
                        SetContraPrice(contraPrice);
                        if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                        {
                            SetPrice(contraPrice + edge);
                        }
                        else
                        {
                            SetPrice(contraPrice - edge);
                        }
                    }
                }

                if (openTicketRequest.SubmitWithDelayEnabled &&
                    this is ComplexOrderTicketViewModel complexOrderTicketViewModel &&
                    Enum.TryParse(openTicketRequest.SubmitWithDelaySide, true, out Side side))
                {
                    complexOrderTicketViewModel.ShowSubmitWithDelayPanel = true;
                    complexOrderTicketViewModel.SubmitWithDelayPercentBidEnabled = openTicketRequest.SubmitWithDelayPercentBidEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayTheoReferenceEnabled = openTicketRequest.SubmitWithDelayTheoReferenceEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayDeltaAdjustEnabled = openTicketRequest.SubmitWithDelayDeltaAdjustEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayBidRangeEnabled = openTicketRequest.SubmitWithDelayBidRangeEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayAskRangeEnabled = openTicketRequest.SubmitWithDelayAskRangeEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayPriceRangeEnabled = openTicketRequest.SubmitWithDelayPriceRangeEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayCancelOnUserPositionChangeEnabled = openTicketRequest.SubmitWithDelayCancelOnUserPositionChangeEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayDeltaAdjCurrentPxEnabled = openTicketRequest.SubmitWithDelayDeltaAdjCurrentPxEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayCancelOnLegVolumeChangeEnabled = openTicketRequest.SubmitWithDelayCancelOnLegVolumeChangeEnabled;
                    complexOrderTicketViewModel.SubmitWithDelayPlayPreSubmitNotification = openTicketRequest.SubmitWithDelayPlayPreSubmitNotification;
                    complexOrderTicketViewModel.SubmitWithDelayCancelOnVolumeChange = openTicketRequest.SubmitWithDelayCancelOnVolumeChange;
                    complexOrderTicketViewModel.SubmitWithDelayPreSubmitNotificationSeconds = openTicketRequest.SubmitWithDelayPreSubmitNotificationSeconds;
                    complexOrderTicketViewModel.SubmitWithDelayInterval = Math.Abs(openTicketRequest.SubmitWithDelayInterval);
                    complexOrderTicketViewModel.SubmitWithDelayPercentBid = Math.Abs(openTicketRequest.SubmitWithDelayPercentBid);
                    complexOrderTicketViewModel.SubmitWithDelayEdgeToTheo = Math.Abs(openTicketRequest.SubmitWithDelayEdgeToTheo);
                    complexOrderTicketViewModel.SubmitWithDelayDeltaAdjLevel = Math.Abs(openTicketRequest.SubmitWithDelayDeltaAdjLevel);
                    complexOrderTicketViewModel.SubmitWithDelayDeltaAdjCurrentPxEdge = Math.Abs(openTicketRequest.SubmitWithDelayDeltaAdjCurrentPxEdge);
                    complexOrderTicketViewModel.SubmitWithDelayMinBid = openTicketRequest.SubmitWithDelayMinBid;
                    complexOrderTicketViewModel.SubmitWithDelayMaxBid = openTicketRequest.SubmitWithDelayMaxBid;
                    complexOrderTicketViewModel.SubmitWithDelayMinAsk = openTicketRequest.SubmitWithDelayMinAsk;
                    complexOrderTicketViewModel.SubmitWithDelayMaxAsk = openTicketRequest.SubmitWithDelayMaxAsk;
                    complexOrderTicketViewModel.SubmitWithDelayMinPrice = openTicketRequest.SubmitWithDelayMinPrice;
                    complexOrderTicketViewModel.SubmitWithDelayMaxPrice = openTicketRequest.SubmitWithDelayMaxPrice;
                    complexOrderTicketViewModel.StartStopSubmitWithDelayCommand();
                }
            }
        }

        public List<TicketLegModel> ParseFromTos(string spreadId, bool setActQty = false)
        {
            SymbolLib.SymbolCodec codec = new(spreadId);
            int legCount = codec.LegCount;
            string underlying = codec.UnderlyingSymbol();
            List<TicketLegModel> legs = new();
            for (int index = 0; index < legCount; index++)
            {
                SymbolLib.Instrument instrument = codec.GetLeg(index);
                string legSymbol = instrument.symbol;
                var side = instrument.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                int qty = Math.Abs(instrument.ratio);

                TicketLegModel leg = new(OmsCore, underlying, "", BasketTraderViewModel, _portfolioManagerModel)
                {
                    Symbol = legSymbol,
                    Quantity = qty,
                    Ratio = qty,
                    ActualQty = setActQty ? instrument.buySell ? qty : -qty : 0,
                    Side = side,
                };
                legs.Add(leg);
            }

            return legs;
        }

        internal async Task LoadFromOrderBookAsync(OmsOrderModel orderModel, bool loadExact = false)
        {
            Underlying = orderModel.UnderlyingSymbol;
            LoadDefaultAccount();
            var side = orderModel.Side;
            await LoadLegsFromTosAsync(orderModel.Symbol, side, !IsBasketOrder);
            var reversed = orderModel.Side != ((IOrder)this).Side;
            _ = UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();
            var isContra = reversed && TicketStyle == OrderTicketStyle.Combined;
            if (!isContra)
            {
                AveragePrice = orderModel.AveragePrice;
                BestAveragePrice = orderModel.AveragePrice;
                LastMainUnderMidAtFill = (orderModel.CloseUnderBid + orderModel.CloseUnderAsk) / 2;
                LastMainUnderMidAtBestFill = (orderModel.CloseUnderBid + orderModel.CloseUnderAsk) / 2;
                LastMainTotalVolumeAtFill = orderModel.TagVolume;

                double price = !loadExact && orderModel.AveragePrice != 0 && !double.IsNaN(orderModel.AveragePrice)
                    ? orderModel.AveragePrice
                    : orderModel.Price;
                SetPrice(price);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    double defaultContraEdge = GetDefaultContraEdge();
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetContraPrice(price - defaultContraEdge);
                    }
                    else
                    {
                        SetContraPrice(price + defaultContraEdge);
                    }
                }
            }
            else
            {
                ContraAveragePrice = orderModel.AveragePrice;
                LastContraUnderMidAtFill = (orderModel.CloseUnderBid + orderModel.CloseUnderAsk) / 2;
                LastContraTotalVolumeAtFill = (orderModel.CloseUnderBid + orderModel.CloseUnderAsk) / 2;

                double price = !loadExact && orderModel.AveragePrice != 0 && !double.IsNaN(orderModel.AveragePrice)
                    ? orderModel.AveragePrice
                    : orderModel.Price;
                price = OmsCore.Config.PriceEvaluationStyle switch
                {
                    PriceEvaluationStyle.Identical when !IsSingleLeg => -price,
                    _ => price,
                };
                SetContraPrice(price);
                if (TicketStyle == OrderTicketStyle.Combined)
                {
                    double defaultContraEdge = GetDefaultContraEdge();
                    if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                    {
                        SetPrice(price + defaultContraEdge);
                    }
                    else
                    {
                        SetPrice(price - defaultContraEdge);
                    }
                }
            }

            if (loadExact)
            {
                if (!isContra)
                {
                    _mainNewTimestamp = orderModel.SubmitTime;
                    _cancelRequestSent = false;
                    LastPx = orderModel.Price;
                    LastRoute = orderModel.Route;
                    LastQty = orderModel.LastQuantity;
                    OrderId = orderModel.OrderID;
                    _canAutoCancel = !OmsCore.Config.NonAutoCancelRoutes.Contains(orderModel.Route);
                    MainNotFilled = !orderModel.OrderStatus.IsClosed();
                    MainResting = !orderModel.OrderStatus.IsClosed();
                    CanReplace = orderModel.OrderStatus is OrderStatus.New or OrderStatus.Replaced;
                    OrderIdsSet.Add(orderModel.OrderID);
                    OrderIdsSet.Add(orderModel.PermID);
                    OrderIdsSet.Add(orderModel.OriginalOrderID);
                }
                else
                {
                    _contraNewTimestamp = orderModel.SubmitTime;
                    _cancelContraRequestSent = false;
                    LastContraPx = orderModel.Price;
                    LastContraQty = orderModel.LastQuantity;
                    ContraOrderId = orderModel.OrderID;
                    ContraNotFilled = !orderModel.OrderStatus.IsClosed();
                    ContraResting = !orderModel.OrderStatus.IsClosed();
                    CanReplaceContra = orderModel.OrderStatus is OrderStatus.New or OrderStatus.Replaced;
                    ContraOrderIdsSet.Add(orderModel.OrderID);
                    ContraOrderIdsSet.Add(orderModel.PermID);
                    ContraOrderIdsSet.Add(orderModel.OriginalOrderID);
                }

                Route = orderModel.Route;
                Account = orderModel.AccountAcronym;
                OmsCore.OrderClient.RegisterHandler(orderModel.OrderID, this);
                OrderIsClosed = false;

                var orderUpdateValues = ParseOrderUpdate(orderModel);
                UpdateUiStatus(orderUpdateValues);
            }
        }

        internal async Task LoadFromOrderAsync(OmsOrder orderModel)
        {
            Underlying = orderModel.UnderlyingSymbol;
            LoadDefaultAccount();
            if (orderModel.Legs.Count > 0)
            {
                List<TicketLegModel> legs = new();
                foreach (OmsOrderLeg leg in orderModel.Legs)
                {
                    Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                    if (string.IsNullOrEmpty(Underlying))
                    {
                        Underlying = option?.UnderlyingSymbol;
                    }
                    string type = leg.Symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
                    TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                    {
                        Side = leg.Side,
                        Ratio = leg.Ratio,
                        Quantity = leg.Ratio, // Set quantity to ratio to set the min value 
                        Type = type,
                        Position = Positions.AUTO.ToString()
                    };
                    await legClone.LoadExpirationsListAsync();
                    ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);
                    legClone.ExpirationInfo = legClone.ExpirationsList.Where(x => x.Equals(expirationInfoModel)).FirstOrDefault();
                    legClone.UpdateStrikesList();
                    legClone.Strike = legClone.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                    legClone.UpdateStrikeVisibility();
                    await legClone.ValidateLegAsync();
                    legs.Add(legClone);
                }
                foreach (TicketLegModel leg in legs.OrderBy(x => x.Strike))
                {
                    Legs.Add(leg);
                }
            }
            else
            {
                string symbol = orderModel.Legs.Count > 0
                    ? orderModel.Legs.FirstOrDefault()?.Symbol
                    : orderModel.Symbol;
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    return;
                }
                Option option = OptionsHelper.GetOptionFromSymbol(symbol);
                if (string.IsNullOrEmpty(Underlying))
                {
                    Underlying = option?.UnderlyingSymbol;
                }
                string type = symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = orderModel.SideString != null ? orderModel.SideString?.ToSide() : ZeroPlus.Models.Data.Enums.Side.Buy,
                    Ratio = 1,
                    Quantity = 1, // Set quantity to ratio to set the min value 
                    Type = type,
                    Position = Positions.AUTO.ToString()
                };
                await legClone.LoadExpirationsListAsync();
                ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);
                legClone.ExpirationInfo = legClone.ExpirationsList.Where(x => x.Equals(expirationInfoModel)).FirstOrDefault();
                legClone.UpdateStrikesList();
                legClone.Strike = legClone.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                legClone.UpdateStrikeVisibility();
                Legs.Add(legClone);
                await legClone.ValidateLegAsync();
            }

            AveragePrice = orderModel.AveragePrice;
            BestAveragePrice = orderModel.AveragePrice;
            LastMainUnderMidAtFill = (orderModel.UnderBid + orderModel.UnderAsk) / 2;
            LastMainUnderMidAtBestFill = (orderModel.UnderBid + orderModel.UnderAsk) / 2;

            double price = double.NaN;
            if (orderModel.AveragePrice != 0 && !double.IsNaN(orderModel.AveragePrice))
            {
                price = orderModel.AveragePrice;
            }
            else
            {
                price = orderModel.Price;
            }
            SetPrice(price);
            if (TicketStyle == OrderTicketStyle.Combined)
            {
                double defaultContraEdge = GetDefaultContraEdge();
                if (IsSellOrder && OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                {
                    SetContraPrice(price - defaultContraEdge);
                }
                else
                {
                    SetContraPrice(price + defaultContraEdge);
                }
            }

            _ = UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateLCD();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal async Task LoadFromPositionAsync(List<PositionModel> positions)
        {
            if (positions.Count == 0)
            {
                return;
            }

            Option option = OptionsHelper.GetOptionFromSymbol(positions[0].Symbol);
            Underlying = option.UnderlyingSymbol;
            LoadDefaultAccount();
            string account = positions.FirstOrDefault()?.Account ?? Account;
            foreach (PositionModel position in positions)
            {
                Option leg = OptionsHelper.GetOptionFromSymbol(position.Symbol);
                if (leg.UnderlyingSymbol != Underlying)
                {
                    continue;
                }
                string type = position.Symbol.StartsWith(".") ? leg.Type.ToString() : "STOCK";
                Side side = position.NetQty > 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                int qty = position.NetQty != 0 ? Math.Abs(position.NetQty) : 1;
                TicketLegModel legClone = new(OmsCore, Underlying, Account, BasketTraderViewModel, _portfolioManagerModel)
                {
                    Side = side,
                    Quantity = qty,
                    Type = type,
                    Position = position.NetQty == 0 ? Positions.OPEN.ToString() : Positions.CLOSE.ToString(),
                };
                await legClone.LoadExpirationsListAsync();
                ExpirationInfoModel expirationInfoModel = new(leg.Expiration, leg.RootSymbol);
                legClone.ExpirationInfo = legClone.ExpirationsList.Where(x => x.Equals(expirationInfoModel)).FirstOrDefault();
                legClone.UpdateStrikesList();
                legClone.Strike = legClone.StrikesList.Where(x => x == leg.Strike).FirstOrDefault();
                legClone.UpdateStrikeVisibility();
                legClone.Account = account;
                Legs.Add(legClone);
                await legClone.ValidateLegAsync();
            }

            await UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            // Using raw qtys from the selected positions so update lcd before locking ratio.
            UpdateLCD();
            RatioLocked = true;
            Account = account ?? Account;
            Route = GetBestRoute();
            AddRoute(Route);
            SubscribeDataAsync();
            UpdateDescription();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal async Task LoadFromLegsAsync(List<TicketLegModel> legs)
        {
            if (legs.Count == 0)
            {
                return;
            }

            Underlying = legs[0].Underlying;
            LoadDefaultAccount();
            foreach (TicketLegModel leg in legs)
            {
                if (leg.Underlying != Underlying)
                {
                    continue;
                }

                Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
                string type = leg.Symbol.StartsWith(".") ? leg.Type.ToString() : "STOCK";
                leg.IsExpirationValid = leg.IsStrikeValid = leg.IsValid = false;
                leg.Type = type;
                leg.Position = Positions.AUTO.ToString();
                if (leg.Quantity == 0)
                {
                    leg.Quantity = 1;
                }
                if (leg.Ratio == 0)
                {
                    leg.Ratio = 1;
                }

                await leg.LoadExpirationsListAsync();
                ExpirationInfoModel expirationInfoModel = new(option.Expiration, option.RootSymbol);
                leg.ExpirationInfo = leg.ExpirationsList.Where(x => x.Equals(expirationInfoModel)).FirstOrDefault();
                leg.UpdateStrikesList();
                leg.Strike = leg.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                leg.UpdateStrikeVisibility();
                Legs.Add(leg);
                await leg.ValidateLegAsync();
            }

            _ = UpdateAccountsAndRoutes();
            SubscribeToLegUpdates();

            // Using raw qtys from the selected positions so update lcd before locking ratio.
            UpdateLCD();
            RatioLocked = true;

            SubscribeDataAsync();
            UpdateDescription();
            UpdateTicketValues();
            ValidateTicket();
        }

        internal OmsOrder ToOrder(bool skipStockLegs = false)
        {
            OmsOrder order = new()
            {
                OrderID = OrderId,
                Tag = Tag,
                AccountAcronym = Account,
                Symbol = SpreadSymbol,
                UnderlyingSymbol = Underlying,
                Route = Route,
                Price = Price,
                Quantity = Lcd,
                MultiLeg = Legs.Count > 1,
                SpreadId = SpreadId,
                Description = Description,
                EdgeOverride = EdgeOverride,
                AdjustedEdgeOverride = AdjustedEdgeOverride,
                EdgeCurveAdjustment = EdgeCurveAdjustment,
                Bid = Low,
                Ask = High,
                Delta = TotalDelta,
                DeltaAdjustedTheo = TotalTheo,
                HanweckTotalVega = TotalVega,
                HanweckTotalIV = TotalImplied,
                HanweckTotalGamma = TotalGamma,
                HanweckTotalRho = TotalRho,
                HanweckTotalTheta = TotalTheta,
                UnderAsk = UnderAsk,
                UnderBid = UnderBid,
                AdjustedPnl = AdjustedPnl,
                Subtype = SubType?.ToString().FromCamelCase(),
                Type = Type,
                SideString = Side?.ToString() ?? "",
            };

            for (int i = 0; i < Legs.Count; i++)
            {
                TicketLegModel leg = Legs[i];
                if (skipStockLegs && leg.SecurityType == SecurityType.Stock)
                {
                    continue;
                }
                OmsOrderLeg omsOrderLeg = new()
                {
                    LegID = $"leg{i}",
                    Side = leg.Side,
                    Quantity = leg.Quantity,
                    Ratio = leg.Ratio,
                    PositionEffect = leg.Position,
                    Symbol = leg.Symbol,
                    OrderID = OrderId,
                    Bid = leg.Bid,
                    Ask = leg.Ask,
                    Delta = leg.Delta,
                    DeltaAdjustedTheo = leg.Theo,
                    HanweckVega = leg.Vega,
                    HanweckIV = leg.Implied,
                    HanweckGamma = leg.Gamma,
                    HanweckRho = leg.Rho,
                    HanweckTheta = leg.Theta,
                };

                order.Legs.Add(omsOrderLeg);
            }

            return order;
        }

        private void AddMultipleLegs(int count)
        {
            for (int i = 0; i < count; i++)
            {
                AddLeg();
            }
        }

        internal void CancelMain()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OrderId))
                {
                    _log.Info("Order Id not found. Spread: " + SpreadId);
                    return;
                }

                if (!MainResting)
                {
                    _log.Info("Order not working M. Spread: " + SpreadId);
                    return;
                }

                if (OmsCore.Config.IsAlgoRoute(route: LastRoute))
                {
                    _log.Info("Cancel not supported on current route. Spread: " + SpreadId);
                    return;
                }
                string orderId = OrderId;
                string permId = PermID;
                string localId = LocalId;

                CheckForRestTimeAndCancel(true, _mainNewTimestamp, orderId, permId, localId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelMain));
            }
        }

        internal void CancelContra()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ContraOrderId))
                {
                    _log.Info("Order Id not found C. Spread: " + SpreadId);
                    return;
                }

                if (!ContraResting)
                {
                    _log.Info("Order not working C. Spread: " + SpreadId);
                    return;
                }

                if (OmsCore.Config.IsAlgoRoute(route: _lastContraRoute))
                {
                    _log.Info("Cancel not supported on current route. Spread: " + SpreadId);
                    return;
                }
                string orderId = ContraOrderId;
                string permId = ContraPermId;
                string localId = ContraLocalId;

                CheckForRestTimeAndCancel(false, _contraNewTimestamp, orderId, permId, localId);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CancelContra));
            }
        }

        internal void SendCancelRequest(bool main, string orderId = null, string permId = null, string localId = null, bool skipQueue = false)
        {
            if (main && IsBasketOrder && !skipQueue)
            {
                if (BasketSettings.QueueCancel)
                {
                    BasketTraderViewModel.QueueForCancel(this);
                    return;
                }
            }

            if (main)
            {
                orderId ??= OrderId;
                permId ??= PermID;
                localId ??= LocalId;

                MainResting = false;
            }
            else
            {
                orderId ??= ContraOrderId;
                permId ??= ContraPermId;
                localId ??= ContraLocalId;

                ContraResting = false;
            }

            CancelRequest cancelRequest = new()
            {
                LocalId = localId ?? "",
                PermId = permId ?? "",
                OrderId = orderId ?? "",
                Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account,
                Venue = Venue,
            };
            if (IsIbTicket)
            {
                OmsCore.IbGatewayClient.CancelOrder(cancelRequest);
            }
            else if (OrderSource == OrderSource.AutoTrader)
            {
                OmsCore.AutoTraderClient.CancelOrder(cancelRequest);
            }
            else
            {
                OmsCore.OrderClient.CancelOrder(cancelRequest);
            }
        }

        internal void SetBestRoute(bool checkOverride = false)
        {
            Route = GetBestRoute(checkOverride);
        }

        internal string GetBestRoute(bool checkOverride = false)
        {
            InstanceMode instanceMode = InstanceMode;
            bool isStockTied = IsStockTied;
            bool isStockTicket = IsStockTicket;
            bool isSingleLeg = IsSingleLeg;
            bool isBasketOrder = IsBasketOrder;
            string underlying = Underlying;

            return GetBestRoute(checkOverride, instanceMode, isBasketOrder, isStockTied, isStockTicket, isSingleLeg, underlying);
        }

        internal string GetBestRoute(bool checkOverride, InstanceMode instanceMode, bool isBasketOrder, bool isStockTied, bool isStockTicket, bool isSingleLeg, string underlying)
        {
            string selectedRoute = RoutesList.FirstOrDefault();
            var defaultHedgeRoute = OmsCore.Config.DefaultHedgeRoute(instanceMode);
            TimeSpan timeOfDayEastern = DateTime.Now.ToEastern().TimeOfDay;
            bool isCurb = timeOfDayEastern >= _pmCurbSessionStartEastern && timeOfDayEastern <= _pmCurbSessionEndEastern;

            if (checkOverride)
            {
                if (isBasketOrder)
                {
                    AutomationConfigModel automationConfigModel = GetAutomationConfig();
                    if (automationConfigModel != null)
                    {
                        if (isStockTied)
                        {
                            if (!string.IsNullOrWhiteSpace(automationConfigModel.StockTiedOrderRoute))
                            {
                                return automationConfigModel.StockTiedOrderRoute;
                            }
                            else if (!string.IsNullOrWhiteSpace(defaultHedgeRoute))
                            {
                                return defaultHedgeRoute;
                            }
                        }
                        if (isCurb && !isStockTicket && !isStockTied)
                        {
                            var curbSessionRoute = OmsCore.Config.DefaultCurbSessionRoute(instanceMode);
                            if (!string.IsNullOrWhiteSpace(curbSessionRoute))
                            {
                                selectedRoute = curbSessionRoute;
                                selectedRoute = CheckForDirectRoute(selectedRoute);
                                return selectedRoute;
                            }
                        }
                        if (isSingleLeg && automationConfigModel.UseSingleLegSeparateLooperRoutes)
                        {
                            if (Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSingleLegSize) && !(_nonAlgoUnders.Contains(underlying) && _algoRoutes.Contains(automationConfigModel.LooperOpenRouteSingleLegSize)))
                            {
                                selectedRoute = automationConfigModel.LooperOpenRouteSingleLegSize;
                                selectedRoute = CheckForDirectRoute(selectedRoute);
                                return selectedRoute;
                            }
                            if (!string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSingleLeg) && !(_nonAlgoUnders.Contains(underlying) && _algoRoutes.Contains(automationConfigModel.LooperOpenRouteSingleLeg)))
                            {
                                selectedRoute = automationConfigModel.LooperOpenRouteSingleLeg;
                                selectedRoute = CheckForDirectRoute(selectedRoute);
                                return selectedRoute;
                            }
                        }
                        else
                        {
                            if (Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSize) && !(_nonAlgoUnders.Contains(underlying) && _algoRoutes.Contains(automationConfigModel.LooperOpenRouteSize)))
                            {
                                selectedRoute = automationConfigModel.LooperOpenRouteSize;
                                selectedRoute = CheckForDirectRoute(selectedRoute);
                                return selectedRoute;
                            }
                            if (!string.IsNullOrEmpty(automationConfigModel.LooperOpenRoute) && !(_nonAlgoUnders.Contains(underlying) && _algoRoutes.Contains(automationConfigModel.LooperOpenRoute)))
                            {
                                selectedRoute = automationConfigModel.LooperOpenRoute;
                                selectedRoute = CheckForDirectRoute(selectedRoute);
                                return selectedRoute;
                            }
                        }
                    }
                }
            }

            if (isCurb && !isStockTicket && !isStockTied)
            {
                var curbSessionRoute = OmsCore.Config.DefaultCurbSessionRoute(instanceMode);
                if (!string.IsNullOrWhiteSpace(curbSessionRoute))
                {
                    selectedRoute = curbSessionRoute;
                }
            }
            else if (underlying == "$SPX" || underlying == "$RUT" || underlying == "$XSP")
            {
                var defaultRouteSpxRutXsp = OmsCore.Config.DefaultRouteSpxRutXsp(instanceMode);
                if (!string.IsNullOrWhiteSpace(defaultRouteSpxRutXsp))
                {
                    selectedRoute = defaultRouteSpxRutXsp;
                }
            }
            else if (underlying == "$NDX")
            {
                var defaultRouteNdx = OmsCore.Config.DefaultRouteNdx(instanceMode);
                if (!string.IsNullOrWhiteSpace(defaultRouteNdx))
                {
                    selectedRoute = defaultRouteNdx;
                }
            }
            else if (isSingleLeg)
            {
                if (isStockTicket && !string.IsNullOrWhiteSpace(defaultHedgeRoute))
                {
                    selectedRoute = defaultHedgeRoute;
                }
                else
                {
                    var defaultSingleLegRoute = OmsCore.Config.DefaultSingleLegRoute(instanceMode);
                    if (!string.IsNullOrWhiteSpace(defaultSingleLegRoute))
                    {
                        selectedRoute = defaultSingleLegRoute;
                    }
                }
            }
            else
            {
                if (isStockTied && !string.IsNullOrWhiteSpace(defaultHedgeRoute))
                {
                    selectedRoute = defaultHedgeRoute;
                }
                else
                {
                    var defaultRoute = OmsCore.Config.DefaultRoute(instanceMode);
                    if (!string.IsNullOrWhiteSpace(defaultRoute))
                    {
                        selectedRoute = defaultRoute;
                    }
                }
            }

            selectedRoute = CheckForDirectRoute(selectedRoute);
            return selectedRoute;
        }

        private void SetMultiplier()
        {
            TicketLegModel leg = Legs.FirstOrDefault(x => x.Symbol != null && x.Symbol.StartsWith("."));
            Multiplier = leg == null ? 1 : leg.Multiplier;
        }

        protected async Task UpdateAccountsAndRoutes()
        {
            await ReloadAccountsAndRoutesList();
            SetDefaultValues();
        }

        private OrderRoutingOrderType? _lastLoadedRoutingOrderType;

        public OrderRoutingOrderType GetOrderType()
        {
            if (IsSingleLeg)
            {
                TicketLegModel ticketLegModel = Legs.FirstOrDefault();
                return ticketLegModel?.Type == "STOCK" ? OrderRoutingOrderType.Stock : OrderRoutingOrderType.Option;
            }

            return OrderRoutingOrderType.Complex;
        }

        private string GetSelectedAccountForRouting()
        {
            return AccountLocked ? OmsCore.Config.DefaultAccount : Account;
        }

        public async Task ReloadAccountsAndRoutesList()
        {
            MigrateLegacyRoutesIfNeeded();

            var currentBroker = EffectiveBroker;
            var account = GetSelectedAccountForRouting();
            var venue = Venue;

            OrderRoutingOrderType orderType = GetOrderType();
            OrderRoutingOrderType[] orderTypes = [orderType];
            var classified = !string.IsNullOrWhiteSpace(currentBroker)
                ? OmsCore.OrderClient.RouteLookup.GetClassifiedRoutesForBroker(currentBroker, orderTypes, account, venue, activeOnly: true)
                : OmsCore.OrderClient.RouteLookup.GetClassifiedRoutes(orderTypes, account, venue, activeOnly: true);

            var sortedDma = classified.Dma
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var sortedSor = classified.Sor
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            await Dispatcher?.BeginInvoke(new Action(() =>
            {
                // Sync AccountsList in place rather than Clear+Add. A Clear() momentarily empties
                // the bound Account ComboBoxEdit's ItemsSource which pushes null through the
                // two-way SelectedItem binding into the Account property, racing against
                // LoadDefaultAccount() and triggering an infinite reload loop.
                SyncCollectionInPlace(AccountsList, OmsCore.User.Accounts);

                RoutesList.Clear();
                DmaRoutesList.Clear();
                SorRoutesList.Clear();

                foreach (var route in sortedDma)
                {
                    AddRoute(route);
                    DmaRoutesList.Add(route);
                }

                foreach (var route in sortedSor)
                {
                    AddRoute(route);
                    SorRoutesList.Add(route);
                }

                RouteSelection?.Refresh(currentBroker, InstanceMode);
                ContraRouteSelection?.Refresh(currentBroker, InstanceMode);
            }));

            _lastLoadedRoutingOrderType = GetOrderType();
        }

        private static void SyncCollectionInPlace(ObservableCollection<string> target, IEnumerable<string> desired)
        {
            if (target == null)
            {
                return;
            }

            var desiredList = desired?.ToList() ?? new List<string>();
            var desiredSet = new HashSet<string>(desiredList, StringComparer.OrdinalIgnoreCase);

            for (int i = target.Count - 1; i >= 0; i--)
            {
                if (!desiredSet.Contains(target[i]))
                {
                    target.RemoveAt(i);
                }
            }

            foreach (var item in desiredList)
            {
                if (!target.Contains(item, StringComparer.OrdinalIgnoreCase))
                {
                    target.Add(item);
                }
            }
        }

        private bool IsSorRoute(string route)
        {
            return OmsCore.OrderClient?.RouteLookup?.IsSmartRoute(route) ?? false;
        }

        private static string StripBrokerPrefix(string route)
        {
            return RouteSelectionViewModel.StripBrokerPrefix(route);
        }

        internal string ApplyBrokerPrefix(string route)
        {
            return OmsCore.OrderClient?.RouteLookup?.ApplyBrokerPrefix(route, EffectiveBroker) ?? route;
        }

        internal void MigrateLegacyRoutesIfNeeded()
        {
            if (TryMigrateLegacyRoute(Route, out var routeBroker, out var routeName, GetOrderType(), GetSelectedAccountForRouting(), Venue, activeOnly: true))
            {
                if (!string.Equals(routeBroker, EffectiveBroker, StringComparison.OrdinalIgnoreCase))
                {
                    BrokerOverride = routeBroker;
                }
                Route = routeName;
            }

            if (TryMigrateLegacyRoute(ContraRoute, out var contraBroker, out var contraName, GetOrderType(), GetSelectedAccountForRouting(), Venue, activeOnly: true))
            {
                if (!string.Equals(contraBroker, EffectiveBroker, StringComparison.OrdinalIgnoreCase))
                {
                    BrokerOverride = contraBroker;
                }
                ContraRoute = contraName;
            }
        }

        internal static bool TryMigrateLegacyRoute(
            string saved,
            out string broker,
            out string routeName,
            OrderRoutingOrderType? orderType = null,
            string account = null,
            Venue? venue = null,
            bool activeOnly = false)
        {
            broker = null;
            routeName = null;
            var routeLookup = ZeroPlus.Oms.ServiceLocator.GetService<ZeroPlus.Oms.OmsCore>()?.OrderClient?.RouteLookup;
            if (routeLookup == null)
            {
                return false;
            }
            return routeLookup.TryMigrateLegacyRoute(saved, out broker, out routeName, orderType, account, venue, activeOnly);
        }

        public void ValidateRoute()
        {
            Route = CheckForDirectRoute(Route);
            ContraRoute = CheckForDirectRoute(ContraRoute);
        }

        protected async Task SetDefaultValues()
        {
            Dispatcher?.BeginInvoke(new Action(() =>
            {
                LoadDefaultAccount();
            }));

            TimeInForce = TimeInForce.DAY;
            Route = GetBestRoute();
            await SetPriceIncrementAsync();
        }

        private void AddRoute(string route)
        {
            route = CheckForDirectRoute(route);
            if (!RoutesList.Contains(route))
            {
                RoutesList.Add(route);
            }
        }

        public string CheckForDirectRoute(string route)
        {
            try
            {
                return route;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForDirectRoute));
                return route;
            }
        }

        internal async Task SetPriceIncrementAsync(bool loadSymbol = true)
        {
            if (IsSingleLeg)
            {
                decimal increment = _defaultIncrement;
                decimal contraIncrement = _defaultIncrement;
                string symbol = Legs[0].Symbol;
                if (!string.IsNullOrWhiteSpace(symbol) && symbol.StartsWith("."))
                {
                    string underlying = Underlying ?? Legs[0].Underlying;
                    if (loadSymbol)
                    {
                        List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(underlying);
                        if (options.Count > 0)
                        {
                            Option option = options.Where(x => x.OptionSymbol == symbol).FirstOrDefault();
                            if (option == null || double.IsNaN(option.MinimumTick))
                            {
                                option = options.First();
                            }
                            increment = contraIncrement = Convert.ToDecimal(option.MinimumTick);
                            MinimumTickStyle = (ZeroPlus.Models.Data.Enums.MinimumTickStyle)option.TickType;
                            _defaultIncrement = increment;
                        }
                        else
                        {
                            increment = contraIncrement = .01M;
                            MinimumTickStyle = ZeroPlus.Models.Data.Enums.MinimumTickStyle.AllPenny;
                            _defaultIncrement = .01M;
                        }
                    }

                    bool belowRange = Price < 3;
                    bool contraBelowRange = ContraPrice < 3;
                    switch (MinimumTickStyle)
                    {
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.None:
                            increment = contraIncrement = 0M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.AllPenny:
                            increment = contraIncrement = .01M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Pennies:
                            increment = belowRange ? .01M : .05M;
                            contraIncrement = contraBelowRange ? .01M : .05M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Nickels:
                            increment = belowRange ? .05M : .10M;
                            contraIncrement = contraBelowRange ? .05M : .10M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Dimes:
                            increment = contraIncrement = .10M;
                            break;
                    }
                }
                else
                {
                    increment = GetSpreadIncrement();
                }
                PriceIncrement = increment;
                TicketPriceIncrement = IsSellOrder ? -increment : increment;
                ContraTicketPriceIncrement = TicketStyle == OrderTicketStyle.Combined ? contraIncrement : IsSellOrder ? contraIncrement : -contraIncrement;
            }
            else
            {
                TicketPriceIncrement = ContraTicketPriceIncrement = PriceIncrement = Underlying == "$SPX" ? 0.05M : 0.01M;
            }
        }

        internal decimal GetSpreadIncrement()
        {
            decimal increment;
            increment = Underlying == "$SPX" ? 0.05M : 0.01M;
            return increment;
        }

        public bool PriceNeedsPadding(double price)
        {
            decimal increment = GetPriceIncrement(price);
            return increment is 0.05M or 0.10M;
        }

        public decimal GetPriceIncrement(double price = 0, IncrementDirection direction = IncrementDirection.Down)
        {
            if (IsSingleLeg)
            {
                decimal increment = _defaultIncrement;

                if (SingleLegStockRoundingDisabled)
                {
                    increment = .01M;
                }
                else
                {
                    switch (MinimumTickStyle)
                    {
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.None:
                            increment = 0M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.AllPenny:
                            increment = .01M;
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Pennies:
                            if (price < 3 || (price == 3 && direction == IncrementDirection.Down))
                            {
                                increment = .01M;
                            }
                            else if (price > 3 || (price == 3 && direction == IncrementDirection.Up))
                            {
                                increment = .05M;
                            }
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Nickels:
                            if (price < 3 || (price == 3 && direction == IncrementDirection.Down))
                            {
                                increment = .05M;
                            }
                            else if (price > 3 || (price == 3 && direction == IncrementDirection.Up))
                            {
                                increment = .10M;
                            }
                            break;
                        case ZeroPlus.Models.Data.Enums.MinimumTickStyle.Dimes:
                            increment = .10M;
                            break;
                    }
                }

                return increment;
            }
            else
            {
                return Underlying == "$SPX" ? 0.05M : 0.01M;
            }
        }

        private bool Modify()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OrderId))
                {
                    throw new SlimException("No order id.");
                }
                if (!double.IsNaN(LastPx) && LastPx == Price && LastQty == Lcd)
                {
                    _log.Warn("Modify px same as last px. Spread Id:" + SpreadId +
                                                       ", Price: " + Price +
                                                       ", Qty: " + Lcd +
                                                       ", Last Price: " + LastPx);
                    return false;
                }

                if ((!IsSingleLegSell || !(Price >= Low)) &&
                    (IsSingleLegSell || !(Price <= High)))
                {
                    bool ok = GetRiskVerification(
                        $"Net Price is outside market range.\n" +
                        $"Your net price at {Price:#,###.00} crosses current market of [{Low:#,###.00}X{High:#,###.00}].\n\n" +
                        $"Do you want to modify this order?",
                        "Price Check") == RiskWarningMessageResponse.Proceed;
                    if (!ok)
                    {
                        return false;
                    }
                }

                LastPx = Price;
                LastQty = Lcd;
                Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : Price > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;

                if (IsIbTicket)
                {
                    ModifyRequest modifyRequest = new()
                    {
                        LocalId = LocalId ?? "",
                        PermId = PermID ?? "",
                        OrderId = OrderId ?? "",
                        Price = Price,
                        Quantity = Lcd,
                    };
                    OmsCore.IbGatewayClient.ModifyOrder(modifyRequest);

                    IsModifyEnabled = false;
                }
                else if (OrderSource == OrderSource.AutoTrader)
                {
                    ModifyAutoTraderOrder();

                    IsModifyEnabled = false;
                }
                else
                {
                    OmsCore.OrderClient.CancelReplaceOrder(CreateModifyRequest(), IsSingleLeg, side, Multiplier,
                        IsStockTicket);
                }

                CanReplace = false;
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Modify));
                return false;
            }
        }

        protected virtual void ModifyAutoTraderOrder()
        {
            OmsCore.AutoTraderClient.ModifyOrder(CreateModifyRequest());
        }

        protected ModifyRequest CreateModifyRequest()
        {
            return new()
            {
                Account = AccountLocked ? OmsCore.Config.DefaultAccount : Account,
                LocalId = LocalId ?? "",
                PermId = PermID ?? "",
                OrderId = OrderId ?? "",
                Price = Price,
                Quantity = Lcd,
                Venue = Venue,
            };
        }

        private void ModifyContra()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ContraOrderId))
                {
                    throw new SlimException("No order id.");
                }

                double price;
                if (TicketStyle == OrderTicketStyle.Combined &&
                    OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
                    !IsBasketOrder &&
                    Legs.Count > 1)
                {
                    price = -ContraPrice;
                }
                else
                {
                    price = ContraPrice;
                }

                int quantity = !ContraQtyLocked && ContraQty > 0 ? ContraQty : Lcd;
                if (!double.IsNaN(LastContraPx) && LastContraPx == price && LastContraQty == quantity)
                {
                    _log.Warn("Modify contra px same as last px. Spread Id:" + SpreadId +
                                                              ", Price: " + price +
                                                              ", Qty: " + quantity +
                                                              ", Last Price: " + LastContraPx);
                    return;
                }

                if (IsSingleLeg)
                {
                    if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        if (price < Low)
                        {
                            bool ok = GetRiskVerification(
                                $"Net Price is outside market range.\nYour net price at {price:#,###.00} crosses current market price at [{Low:#,###.00}X{High:#,###.00}].\n\nDo you want to modify this order?",
                                "Price Check") == RiskWarningMessageResponse.Proceed;
                            if (!ok)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (price > High)
                        {
                            bool ok = GetRiskVerification(
                                $"Net Price is outside market range.\nYour net price at {price:#,###.00} crosses current market price at [{Low:#,###.00}X{High:#,###.00}].\n\nDo you want to modify this order?",
                                "Price Check") == RiskWarningMessageResponse.Proceed;
                            if (!ok)
                            {
                                return;
                            }
                        }
                    }
                }
                else if (Legs.Count > 1)
                {
                    if (price > -Low)
                    {
                        bool ok = false;
                        if (TicketStyle == OrderTicketStyle.Combined &&
                            OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
                            !IsBasketOrder)
                        {
                            ok = GetRiskVerification(
                                $"Net Price is outside market range.\nYour net price at {ContraPrice:#,###.00} crosses current market price at [{Low:#,###.00}X{High:#,###.00}].\n\nDo you want to modify this order?",
                                "Price Check") == RiskWarningMessageResponse.Proceed;
                        }
                        else
                        {
                            ok = GetRiskVerification(
                                $"Net Price is outside market range.\nYour net price at {price:#,###.00} crosses current market price at [{-High:#,###.00}X{-Low:#,###.00}].\n\nDo you want to modify this order?",
                                "Price Check") == RiskWarningMessageResponse.Proceed;
                        }

                        if (!ok)
                        {
                            return;
                        }
                    }
                }

                LastContraPx = price;
                LastContraQty = quantity;
                Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : price > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                ModifyRequest modifyRequest = new()
                {
                    LocalId = ContraLocalId ?? "",
                    PermId = ContraPermId ?? "",
                    OrderId = ContraOrderId ?? "",
                    Price = price,
                    Quantity = quantity,
                };
                if (IsIbTicket)
                {
                    OmsCore.IbGatewayClient.ModifyOrder(modifyRequest);

                    IsContraModifyEnabled = false;
                }
                else if (OrderSource == OrderSource.AutoTrader)
                {
                    OmsCore.AutoTraderClient.ModifyOrder(modifyRequest);

                    IsContraModifyEnabled = false;
                }
                else
                {
                    OmsCore.OrderClient.CancelReplaceOrder(modifyRequest, IsSingleLeg, side,
                        Multiplier, IsStockTicket);
                }

                CanReplaceContra = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifyContra));
            }
        }

        private void ModifyFish()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(OrderId))
                {
                    throw new SlimException("No order id.");
                }
                LastPx = FishPrice;
                LastQty = Lcd;
                Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : FishPrice > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                ModifyRequest modifyRequest = new()
                {
                    LocalId = LocalId ?? "",
                    PermId = PermID ?? "",
                    OrderId = OrderId ?? "",
                    Price = FishPrice,
                    Quantity = Lcd,
                };
                OmsCore.OrderClient.CancelReplaceOrder(modifyRequest, IsSingleLeg, side, Multiplier, IsStockTicket);
                StartFishTimer();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifyFish));
            }
        }

        private void ModifyContraFish()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ContraOrderId))
                {
                    throw new SlimException("No order id.");
                }
                LastContraPx = FishPrice;
                LastContraQty = Lcd;
                Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : FishPrice > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
                ModifyRequest modifyRequest = new()
                {
                    LocalId = ContraLocalId ?? "",
                    PermId = ContraPermId ?? "",
                    OrderId = ContraOrderId ?? "",
                    Price = FishPrice,
                    Quantity = Lcd,
                };
                OmsCore.OrderClient.CancelReplaceOrder(modifyRequest, IsSingleLeg, side, Multiplier, IsStockTicket);
                StartFishTimer();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModifyContraFish));
            }
        }

        private void StartFishTimer()
        {
            _fishTimer.Stop();

            if (!IsBasketOrder)
            {
                if (_routeFish != null)
                {
                    _fishInterval = Math.Max(OmsCore.Config.SubmitWithDelayIntervalMin, _routeFish.Interval);
                }
            }
            else if (IsBasketOrder && !_manualClose)
            {
                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                if (BasketSettings.FishModeEnabled || automationConfigModel.GoFishAutoCloseEnabled)
                {
                    _fishInterval = Math.Max(OmsCore.Config.SubmitWithDelayIntervalMin, !Closing ? automationConfigModel.FishInterval : automationConfigModel.ContraFishInterval);
                }
            }
            else if (_manualClose)
            {
                _fishInterval = OmsCore.Config.SubmitWithDelayIntervalMin;
            }

            if (_fishInterval > 0)
            {
                _fishTimer.Interval = _fishInterval;
                _fishTimer.Start();
            }
        }

        internal void PreUpdate()
        {
            try
            {
                _calcLegs = null;
                IsLooping = false;
                foreach (TicketLegModel leg in Legs)
                {
                    leg.LegUpdatedEvent -= UpdateTicketValues;
                }
                LastExchange = "";
                Exchanges = "";
                LastContraExchange = "";
                FeesEstimate = 0;
                Description = "";
                TraderSpreadPosition = 0;
                SpreadPosition = 0;
                SpreadRawPosition = 0;
                HedgeAttempt = 0;
                TotalStocks = 0;
                HedgedStocks = 0;
                RequiredStocks = 0;
                SubmittedStocks = 0;
                SingleOrderTicketStopLossValue = 0;
                SingleOrderTicketTrailingStopValue = 0;
                SingleOrderTicketPosition = 0;
                SingleOrderTicketWorkingPosition = 0;
                TotalFills = 0;
                _bidAtFillForSingleTickets = 0;
                _askAtFillForSingleTickets = 0;

                CanHedge = false;
                StockHedgeQty = 0;
                StockHedgeStatus = "";
                StockHedgeStatusMode = StatusMode.Reset;
                StockHedgeAdjTradePx = double.NaN;
                AdjustedPriceAtHedge = double.NaN;
                StockPriceAtHedge = double.NaN;
                PositionNetDelta = double.NaN;
                HedgeNetDelta = double.NaN;
                PositionNetWeightedVega = double.NaN;
                HedgeReversePnl = 0;
                HedgeReversePosition = 0;
                ReversePnl = 0;
                ReverseSpreadPosition = 0;
                AdjustedPnl = double.NaN;
                UnrealizedPnl = double.NaN;
                FirmLastTrader = "";
                AvgCost = double.NaN;
                FirmLastEdge = double.NaN;
                FirmLastBuyEdge = double.NaN;
                FirmLastSellEdge = double.NaN;
                FirmLastBuyOrderEdgeToTheo = double.NaN;
                FirmLastSellOrderEdgeToTheo = double.NaN;
                FirmLastFillBuyEdgeToTheo = double.NaN;
                FirmLastFillSellEdgeToTheo = double.NaN;
                FirmLastBuyAttemptEdgeToTheo = double.NaN;
                FirmLastSellAttemptEdgeToTheo = double.NaN;
                GlobalMarketBuyEdgeToTheo = double.NaN;
                GlobalMarketSellEdgeToTheo = double.NaN;
                ResetFirmOrderAndTradeSummaryValues();
                FirmLastBuyAttempt = double.NaN;
                FirmLastBuyAttemptUnderlying = double.NaN;
                FirmLastSellAttempt = double.NaN;
                FirmLastSellAttemptUnderlying = double.NaN;
                LastPermBuyFillEdgeToTheo = double.NaN;
                LastPermSellFillEdgeToTheo = double.NaN;
                LastPermBuyAttemptEdgeToTheo = double.NaN;
                LastPermSellAttemptEdgeToTheo = double.NaN;
                FirmLastTradeSide = null;
                FirmLastTradeTime = default;
                BestBuyEdgeToTheo = double.NaN;
                WorstBuyEdgeToTheo = double.NaN;
                BestSellEdgeToTheo = double.NaN;
                WorstSellEdgeToTheo = double.NaN;
                HardSide = null;
                OpenPositionAveragePrice = double.NaN;
                StockHedgeOpenPositionAveragePrice = double.NaN;
                TraderAdjustedPnl = double.NaN;
                SpreadPositionInitialized = false;
                TraderSpreadPositionInitialized = false;
                DualDescription = "";
                SpreadId = "";
                SpreadPermId = "";
                SpreadType = "";
                Symbol = "";
                StrikeSpacing = double.NaN;
                Filled = Status = ContraFilled = ContraStatus = "";
                StatusMode = ContraStatusMode = StatusMode.Reset;
                MainOrderStatus = ContraOrderStatus = null;
                IsSubmitEnabled = false;
                IsModifyEnabled = false;
                IsCancelEnabled = false;
                IsContraSubmitEnabled = false;
                IsContraModifyEnabled = false;
                IsContraCancelEnabled = false;
                OrderIsClosed = false;

                EdgeOverride = double.NaN;
                AdjustedEdgeOverride = double.NaN;
                EdgeCurveAdjustment = double.NaN;

                ResetTicket();

                Closing = false;
                ThreeWayStarted = false;
                ThreeWayComplete = false;
                LastTradedContra = false;
                StockPos = "";

                LastOptionPnlOnFill = double.NaN;
                LastHedgePnlOnFill = double.NaN;
                LastTotalPnlOnFill = double.NaN;
                LastEdgeToMarketOnFill = double.NaN;
                LiveLastTradeOptionPnl = double.NaN;
                LiveLastTradeHedgePnl = double.NaN;
                LiveLastTradeTotalPnl = double.NaN;
                LiveLastTradeEdgeToMarket = double.NaN;
                LastHedgePrice = double.NaN;
                LastHedgeQty = 0;
                AveragePrice = double.NaN;
                BestAveragePrice = double.NaN;
                ContraAveragePrice = double.NaN;

                LastMainTotalVolumeAtFill = double.NaN;
                LastContraTotalVolumeAtFill = double.NaN;

                LastMainUnderPriceAtFill = double.NaN;
                LastContraUnderPriceAtFill = double.NaN;

                LastMainUnderMidAtFill = double.NaN;
                LastMainUnderMidAtBestFill = double.NaN;
                LastContraUnderMidAtFill = double.NaN;

                LastTradeUpdate = null;
                DeltaAdjLastTradeUpdate = double.NaN;
                DeltaAdjPx = double.NaN;
                DeltaAdjContraPx = double.NaN;
                LockDeltaAdjPrice = false;
                LockContraDeltaAdjPrice = false;
                BestDeltaAdjPx = double.NaN;
                BestDeltaAdjContraPx = double.NaN;
                if (Looper != null)
                {
                    Looper.IcebergRunning = false;
                }
                Tracker?.Stop();
                SweepCloser?.Stop();
                LegOutCloser?.Stop();
                AutoLegCloser?.Stop();
                ThreeWayCloser?.Stop();
                StopLossManager?.Stop();
                StopOrderManager?.Stop();
                AutoCloseManager?.Stop();
                ResetPermAdj();
                ResetAdjEdgeSummary();
                LockBestDeltaAdjPrice = false;
                LockContraBestDeltaAdjPrice = false;
                LastTransactionPrice = double.NaN;
                LastContraTransactionPrice = double.NaN;
                SpreadSymbol = "";
                ContraSpreadSymbol = "";

                ResetEvents(false);

                NetTheoSynched = false;
                DeltaAdjTheoSynched = false;
                DeltaAdjTheoSequence = 0;
                RemoveEdgeProjector();
                UnsubscribeIbDataCommand();
                OnPreUpdateReset();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(PreUpdate));
            }
        }

        protected virtual void OnPreUpdateReset()
        {

        }

        private void ResetEvents(bool all)
        {
            WeightedVegaLoaded = false;
            NetTheoLoaded = false;
            NetHistoricBestLoaded = false;
            NetAdjTheoLoaded = false;
            TotalDeltaLoaded = false;
            LowLoaded = false;
            LowestBidLoaded = false;
            HighestOfferLoaded = false;
            HighestBidLowestAskLoaded = false;
            HighLoaded = false;
            MarkLoaded = false;
            BestMarkLoaded = false;
            IbQuoteLoaded = false;
            EmaLoaded = false;
            AdjEmaLoaded = false;
            BidEmaLoaded = false;
            AskEmaLoaded = false;
            SizeLoaded = false;

            if (all)
            {
                UnderLoaded = false;
                HedgeUnderLoaded = false;
            }

            _dataLoadNotification = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        internal void PostUpdate()
        {
            foreach (TicketLegModel leg in Legs)
            {
                leg.LegUpdatedEvent -= UpdateTicketValues;
                leg.LegUpdatedEvent += UpdateTicketValues;
            }
            UpdateLCD();
            ValidateTicket();
            UpdateDescription();
            UpdateTicketValues();
        }

        public async Task<string> CheckRiskParametersAsync()
        {
            return await Task.Run(CheckForRiskAsync);
        }

        protected virtual async Task<string> CheckForRiskAsync()
        {
            if (!RiskCheckEnabled)
            {
                return "";
            }

            if (!await IsWithinMarketCap())
            {
                return $"Outside Mkt. Px: {Price:n2}, Mkt: [{Low:n2}X{High:n2}]";
            }
            else if (!await CheckEdgeRiskParametersAsync(preSubmit: true))
            {
                return "Outside Edge.";
            }
            else if (CheckAgainstDoubleFillOnClose())
            {
                return $"Double submit from closing side detected. Pos: {SpreadPosition}";
            }
            else
            {
                return "";
            }
        }

        private bool CheckAgainstDoubleFillOnClose()
        {
            if (!_riskModel.OverrideEdgeCheck && !IsBasketOrder && OmsCore.Config.WarnAgainstDoubleFillOnCloseEnabled)
            {
                if (_openingOrderToSideMap.TryGetValue(SpreadId, out Side? side))
                {
                    if (side != Side && SpreadPosition == 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        internal async Task<string> JoinSweep(int resubmitCount)
        {
            if (IsActive || !IsBasketOrder)
            {
                return "Order Not Ready!";
            }

            DateTime time = DateTime.Now;
            _latencyTimer.Restart();
            SubmitLatency = 0;
            PendingNewLatency = 0;
            long deltaCheckTime = 0;
            long posCheckTime = 0;
            long setEdgeTime = 0;
            long riskCheckTime = 0;
            long pxCrossCheck = 0;
            long marketCrossCheck = 0;
            long riskCheckTime2 = 0;

            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition < 0))
                {
                    ShowMessage("Basket Delta Limit Reached.",
                                "ZeroPlus OMS");
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);

                    return "Basket Delta Limit Reached.";
                }
            }
            deltaCheckTime = _latencyTimer.ElapsedMilliseconds;

            if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                ShowMessage("Basket Long Position Limit Reached.",
                            "ZeroPlus OMS");
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return "Basket Long Position Limit Reached.";
            }

            if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                ShowMessage("Basket Short Position Limit Reached.",
                            "ZeroPlus OMS");
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return "Basket Short Position Limit Reached.";
            }
            posCheckTime = _latencyTimer.ElapsedMilliseconds - deltaCheckTime;

            _log.Info("Adjust px before submit enabled." + GetStats());
            Edge = 0;
            EdgeType = EdgeType.LastFillAdjEdge;
            await UseEdgeToLastFillAdjPx(Edge);
            setEdgeTime = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime);
            if (double.IsNaN(Price))
            {
                ShowErrorMessage("Invalid Price");
                _log.Info($"Price can not be NaN! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }

            AutomationConfigModel config = GetAutomationConfig();
            if (config == null)
            {
                ShowErrorMessage("Invalid Config");
                _log.Info($"Invalid Config! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }

            double edge = Price * config.SweepTradeEntryLimitPercentage;
            if (IsSingleLegSell)
            {
                SetPriceMinimal(Price + edge);
            }
            else
            {
                SetPriceMinimal(Price - edge);
            }

            _log.Info("Set price complete." + GetStats());

            if (Lcd != config.SweepTradeEntrySize)
            {
                UpdateQty(config.SweepTradeEntrySize);
            }

            if (DateTime.Now - time > RiskTimeSpan)
            {
                ShowErrorMessage("Set Edge Timeout!");
                _log.Info($"Set price timedout! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}, Risk Check: {riskCheckTime:N2}" + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }
            pxCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime);

            if (SubmitWithDelayEnabled)
            {
                SubmitWithDelayEnabled = false;
            }

            if (!IsSingleLeg && !await IsWithinPercentMarketCap())
            {
                bool proceed = (!IsBasketOrder || !BasketTraderViewModel.IsEdgeScanFeedAutoTrader) && GetRiskVerification($"Your price crosses market by more than {Math.Round(_riskModel.RiskCheckMarketPercentage * 100, 2)}%.\nMkt: [{Low:F2}X{High:F2}] Px: {Price:F2}\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                if (!proceed)
                {
                    ShowErrorMessage("Risk. Price crosses market.");
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    return Status;
                }
            }
            marketCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck);

            time = DateTime.Now;
            string checkRiskTaskResult = await CheckRiskParametersAsync();
            TimeSpan span = DateTime.Now - time;
            if (!string.IsNullOrEmpty(checkRiskTaskResult))
            {
                ShowErrorMessage("Risk. " + checkRiskTaskResult);
                _log.Info("Risk check failed. Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Result: " + checkRiskTaskResult + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }
            else if (span > RiskTimeSpan)
            {
                ShowErrorMessage("Risk. Timeout checking for risk.");
                _log.Info("Risk check failed. Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Span: " + span + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }
            else
            {
                riskCheckTime2 = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck + marketCrossCheck);
                TotalResubmitCount = resubmitCount;
                ResetLastFillTrackers();
                CloseStyle = Enums.CloseStyle.SweepTrade;
                OrderSent = false;
                return "Trigger Set";
            }
        }

        internal async Task<string> FishOffMarket(string exchange)
        {
            if (IsActive || !IsBasketOrder)
            {
                return "Order Not Ready!";
            }

            DateTime time = DateTime.Now;
            _latencyTimer.Restart();
            SubmitLatency = 0;
            PendingNewLatency = 0;
            long deltaCheckTime = 0;
            long posCheckTime = 0;
            long setEdgeTime = 0;
            long riskCheckTime = 0;
            long pxCrossCheck = 0;
            long marketCrossCheck = 0;
            long riskCheckTime2 = 0;
            long submitLatency = 0;

            if (OmsCore.Config.BasketDeltaLimitEnabledV2 && Math.Abs(NetDelta) >= OmsCore.Config.BasketDeltaLimitV2)
            {
                if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition < 0))
                {
                    ShowMessage("Basket Delta Limit Reached.",
                                "ZeroPlus OMS");
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);

                    return "Basket Delta Limit Reached.";
                }
            }
            deltaCheckTime = _latencyTimer.ElapsedMilliseconds;

            if (OmsCore.Config.BasketLongPositionLimitEnabled && BasketSettings.NetPos >= OmsCore.Config.BasketLongPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                ShowMessage("Basket Long Position Limit Reached.",
                            "ZeroPlus OMS");
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return "Basket Long Position Limit Reached.";
            }

            if (OmsCore.Config.BasketShortPositionLimitEnabled && BasketSettings.NetPos <= -OmsCore.Config.BasketShortPositionLimit && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                ShowMessage("Basket Short Position Limit Reached.",
                            "ZeroPlus OMS");
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return "Basket Short Position Limit Reached.";
            }
            posCheckTime = _latencyTimer.ElapsedMilliseconds - deltaCheckTime;

            _log.Info("Adjust px before submit enabled." + GetStats());
            double edge = BasketSettings.LastFillAdjEdge;
            EdgeType = EdgeType.LastFillAdjEdge;
            Edge = edge;
            await UseEdgeToLastFillAdjPx(edge);
            _log.Info("Set price complete." + GetStats());
            setEdgeTime = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime);

            if (double.IsNaN(Price))
            {
                ShowErrorMessage("Invalid Price");
                _log.Info($"Price can not be NaN! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }

            double minEdge = GetLoopMinEdge();
            UpdateRoutes(out string route, out string contraRoute);
            double fees = GetTotalFeesForTicket(route, exchange);
            double closingFees = GetTotalFeesForTicket(contraRoute, exchange: "", reverse: true, getWorst: true);
            double totalEdge = minEdge + (fees + closingFees);

            if (IsSingleLegSell)
            {
                double actEdge = Math.Abs(Price - High);
                if (Price < High + totalEdge)
                {
                    ShowErrorMessage($"Min Edge Failed - {totalEdge:F2} - {actEdge:F2}");
                    _log.Info($"{Status}! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    return $"{Status}";
                }

                CloseEdgeOveride = actEdge;
            }
            else
            {
                double actEdge = Math.Abs(Low - Price);
                if (Price > Low - totalEdge)
                {
                    ShowErrorMessage($"Min Edge Failed - {totalEdge:F2} - {actEdge:F2}");
                    _log.Info($"{Status}! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    return $"{Status}";
                }

                CloseEdgeOveride = actEdge;
            }

            for (int i = 0; i < Legs.Count; i++)
            {
                TicketLegModel leg = Legs[i];
                if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    double bidLimit = leg.Bid;
                    double percentAdjBidEma = leg.AdjBidEma + ((leg.Ask - leg.Bid) * GetAutomationConfig().LegOutMaxPercentThroughEma);
                    double dollarAdjBidEma = leg.AdjBidEma + GetAutomationConfig().LegOutMaxDollarThroughEma;
                    if (bidLimit > percentAdjBidEma && bidLimit > dollarAdjBidEma)
                    {
                        ShowErrorMessage($"Leg {i + 1} through EMA limit - {bidLimit:F2} - {leg.AdjBidEma:F2}");
                        _log.Info($"{Status}! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                        _latencyTimer.Stop();
                        NotifyOrderCloseWaitHandlers(main: true, null);
                        return $"{Status}";
                    }
                    LegOutCloser.LegToScratchPriceMap[leg] = leg.Bid;
                }
                else
                {
                    double askLimit = leg.Ask;
                    double percentAdjAskEma = leg.AdjAskEma - ((leg.Ask - leg.Bid) * GetAutomationConfig().LegOutMaxPercentThroughEma);
                    double dollarAdjAskEma = leg.AdjBidEma - GetAutomationConfig().LegOutMaxDollarThroughEma;
                    if (askLimit < percentAdjAskEma && askLimit < dollarAdjAskEma)
                    {
                        ShowErrorMessage($"Leg {i + 1} through EMA limit - {askLimit:F2} - {leg.AdjAskEma:F2}");
                        _log.Info($"{Status}! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}" + GetStats());
                        _latencyTimer.Stop();
                        NotifyOrderCloseWaitHandlers(main: true, null);
                        return $"{Status}";
                    }
                    LegOutCloser.LegToScratchPriceMap[leg] = leg.Ask;
                }
            }

            if (DateTime.Now - time > RiskTimeSpan)
            {
                ShowErrorMessage("Set Edge Timeout!");
                _log.Info($"Set price timedout! Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}, Risk Check: {riskCheckTime:N2}" + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);
                return Status;
            }
            pxCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime);

            if (SubmitWithDelayEnabled)
            {
                SubmitWithDelayEnabled = false;
            }

            if (!IsSingleLeg && !await IsWithinPercentMarketCap())
            {
                bool proceed = (!IsBasketOrder || !BasketTraderViewModel.IsEdgeScanFeedAutoTrader) && GetRiskVerification($"Your price crosses market by more than {Math.Round(_riskModel.RiskCheckMarketPercentage * 100, 2)}%.\nMkt: [{Low:F2}X{High:F2}] Px: {Price:F2}\nAre you sure you want to proceed?", SpreadId) == RiskWarningMessageResponse.Proceed;
                if (!proceed)
                {
                    ShowErrorMessage("Risk. Price crosses market.");
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);

                    return Status;
                }
            }
            marketCrossCheck = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck);

            time = DateTime.Now;
            string checkRiskTaskResult = await CheckRiskParametersAsync();
            TimeSpan span = DateTime.Now - time;
            if (!string.IsNullOrEmpty(checkRiskTaskResult))
            {
                ShowErrorMessage("Risk. " + checkRiskTaskResult);
                _latencyTimer.Stop();
                _log.Info("Risk check failed. Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Result: " + checkRiskTaskResult + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);

                return Status;
            }
            else if (span > RiskTimeSpan)
            {
                ShowErrorMessage("Risk. Timeout checking for risk.");
                _log.Info("Risk check failed. Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Span: " + span + GetStats());
                _latencyTimer.Stop();
                NotifyOrderCloseWaitHandlers(main: true, null);

                return Status;
            }
            else
            {
                riskCheckTime2 = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck + marketCrossCheck);
                TotalResubmitCount = 0;
                ResetLastFillTrackers();
                CloseStyle = Enums.CloseStyle.OutOfMarketLoop;
                _ = Task.Run(() => SubmitOrderAsync(isContra: false)).ContinueWith(t =>
                {
                    submitLatency = _latencyTimer.ElapsedMilliseconds - (deltaCheckTime + posCheckTime + setEdgeTime + riskCheckTime + pxCrossCheck + marketCrossCheck + riskCheckTime2);
                    _log.Info($"Latency Log. Delta Check: {deltaCheckTime:N2}, Pos Check: {posCheckTime:N2}, Set Edge: {setEdgeTime:N2}, Risk Check: {riskCheckTime:N2}, Px Cross Check: {pxCrossCheck:N2}, Mkt Cross Check: {marketCrossCheck:N2}, Risk 2 Check: {riskCheckTime2:N2}, Submit Latency: {submitLatency:N2}" + GetStats());
                });
                return "";
            }
        }

        internal async Task<bool> SubmitOrderAsync(bool isContra, bool resting = false, OrderSubType? subType = null, double cancelDelay = double.NaN)
        {
            long startTime = _latencyTimer.ElapsedMilliseconds;
            long orderBuildTime = 0;
            long edgeViolationCheckTime = 0;
            OpsOrderModel orderInfo = null;
            try
            {
                double netDeltaAdjTheo = NetDeltaAdjTheo;
                if (!isContra)
                {
                    LastOrderEdgeToTheo = IsSingleLegSell ? Price - netDeltaAdjTheo : netDeltaAdjTheo - Price;
                }
                else
                {
                    LastContraOrderEdgeToTheo = !IsSingleLegSell ? ContraPrice - netDeltaAdjTheo : IsSingleLeg ? netDeltaAdjTheo - ContraPrice : -netDeltaAdjTheo - ContraPrice;
                }

                bool isClosing = IsClosing(isContra);
                CheckForPosEffect(isContra, isClosing);

                ClearOrderIds(isContra);
                LastTradedContra = isContra;
                OrderIsClosed = true;
                ValidateAccount();
                ValidateBroker();

                if (Legs.Count == 0)
                {
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    throw new SlimException("No valid legs.");
                }

                if (IsBasketOrder && BasketTraderViewModel.IsEdgeScanFeedAutoTrader && subType == null)
                {
                    subType = SubType == OrderSubType.BasketAutoPerm ? SubType : OrderSubType.EdgeScanFeed;
                }

                orderInfo = BuildOrder(isContra, subType);

                if (resting)
                {
                    orderInfo.SetCancelDelay(0.0);
                }
                else if (!double.IsNaN(cancelDelay))
                {

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    orderInfo.SetCancelDelay(cancelDelay);
                }
                _routeFish = _manualRouteFish ?? (_routeFish = OmsCore.Config.FishRoutes.FirstOrDefault(x => x.Contains(orderInfo.Route)));

                if (_routeFish != null)
                {
                    if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        FishPrice = orderInfo.Price + _routeFish.Edge;
                        orderInfo.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, false) : FishPrice;
                    }
                    else
                    {
                        FishPrice = orderInfo.Price - _routeFish.Edge;
                        orderInfo.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, true) : FishPrice;
                    }
                }

                orderBuildTime = _latencyTimer.ElapsedMilliseconds - startTime;

                if (!IsValidPriceForStrategy(isContra, orderInfo))
                {
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    throw new SlimException("Invalid Px For Strategy.");
                }

                bool edgeViolated = CheckForRiskViolation(isContra, orderInfo.Qty, orderInfo.Price, orderInfo.Route);
                if (edgeViolated)
                {
                    _latencyTimer.Stop();
                    _log.Info("Edge violation check failed. Spread: " + SpreadId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Contra:" + isContra + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    return false;
                }

                edgeViolationCheckTime = _latencyTimer.ElapsedMilliseconds - (startTime + orderBuildTime);

                SubmitLatency = _latencyTimer.ElapsedMilliseconds;

                ResetOrderCloseWaitHandlers(main: !isContra);
                var isEdgeScanFeedAutoTrader = IsBasketOrder && BasketTraderViewModel.IsEdgeScanFeedAutoTrader;
                if (!isContra)
                {
                    _mainNewTimestamp = default;
                    _cancelRequestSent = true;
                    LastPx = orderInfo.Price;
                    LastRoute = orderInfo.Route;
                    LastQty = orderInfo.Qty;
                    orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                    OrderIdsSet.Add(orderInfo.LocalID);
                    TagEdgeToTheo = CalculateEdgeToTheo();
                    OrderId = await OmsCore.OrderClient.SendOrderAsync(orderInfo, GetInstanceMode(), this, isEdgeScanFeedAutoTrader, Multiplier, checkForDuplicate: !IsBasketOrder, !_orderDetailsContainer.IsEmpty ? _orderDetailsContainer.ToList() : null);
                    _canAutoCancel = !OmsCore.Config.NonAutoCancelRoutes.Contains(orderInfo.Route);
                    if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
                    {
                        int qty = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? orderInfo.Qty : -orderInfo.Qty;
                        lock (PositionUpdateLock)
                        {
                            SingleOrderTicketWorkingPosition += qty;
                        }
                    }

                    MainNotFilled = true;
                    MainResting = true;
                    CanReplace = false;
                    OrderIdsSet.Add(OrderId);
                    _log.Info($"Order submitted. OrderId: {OrderId}, Latency Timer: {_latencyTimer.ElapsedMilliseconds}, Build: {orderBuildTime}, Edge Violation: {edgeViolationCheckTime}{GetStats()}");
                }
                else
                {
                    _contraNewTimestamp = default;
                    _cancelContraRequestSent = true;
                    LastContraPx = orderInfo.Price;
                    LastContraQty = orderInfo.Qty;
                    orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                    ContraOrderIdsSet.Add(orderInfo.LocalID);
                    ContraOrderId = await OmsCore.OrderClient.SendOrderAsync(orderInfo, GetInstanceMode(), this, isEdgeScanFeedAutoTrader, Multiplier, checkForDuplicate: !IsBasketOrder, !_orderDetailsContainer.IsEmpty ? _orderDetailsContainer.ToList() : null);

                    if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
                    {
                        int qty = Side == ZeroPlus.Models.Data.Enums.Side.Sell ? orderInfo.Qty : -orderInfo.Qty;
                        lock (PositionUpdateLock)
                        {
                            SingleOrderTicketWorkingPosition += qty;
                        }
                    }

                    _lastContraRoute = orderInfo.Route;
                    ContraResting = true;
                    ContraNotFilled = true;
                    CanReplaceContra = false;
                    ContraOrderIdsSet.Add(ContraOrderId);
                    _log.Info($"Contra Order submitted. OrderId: {ContraOrderId}, Latency Timer: {_latencyTimer.ElapsedMilliseconds}, Build: {orderBuildTime}, Edge Violation: {edgeViolationCheckTime}{GetStats()}");
                }
                OrderIsClosed = false;

                if (_routeFish != null)
                {
                    Closing = isContra;
                    StartFishTimer();
                }

                SetAutoCancelTriggers();

                return true;
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: !isContra, null);
                Reason = ex.Message;
                ShowMessage(ex.Message, "Order Submission Failed.");
                return false;
            }
            catch (SlimException ex)
            {
                _log.Warn(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: !isContra, null);
                if (ex.ErrorType == ErrorType.DuplicateOrderFound && orderInfo != null)
                {
                    if (GetVerification($"Duplicate resting order found for {SpreadId}, do you want to clear it?", "Order Risk Check"))
                    {
                        OmsCore.OrderClient.ClearOrder(orderInfo.Symbol);
                    }
                }
                else
                {
                    ShowErrorMessage(ex.Message);
                    Reason = ex.Message;
                    ShowMessage(ex.Message, "Order Submission Failed.");
                }
                return false;
            }
            catch (RouteSelectionException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: !isContra, null);
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: !isContra, null);
                return false;
            }
        }

        protected bool IsValidPriceForStrategy(bool isContra, OpsOrderModel orderInfo)
        {
            switch (BaseStrategy)
            {
                case BaseStrategy.CALL_VERTICAL:
                case BaseStrategy.PUT_VERTICAL:
                    if (!CheckForLongCredit(isContra, orderInfo))
                    {
                        return false;
                    }
                    if (!CheckForVerticalSpacing(isContra, orderInfo))
                    {
                        return false;
                    }
                    break;
                case BaseStrategy.CALL_CALENDAR:
                case BaseStrategy.PUT_CALENDAR:
                case BaseStrategy.CALL_BUTTERFLY:
                case BaseStrategy.PUT_BUTTERFLY:
                    if (!CheckForLongCredit(isContra, orderInfo))
                    {
                        return false;
                    }
                    break;
            }
            return true;
        }

        private bool CheckForLongCredit(bool isContra, OpsOrderModel orderInfo)
        {
            if (!OmsCore.User.CheckForLongCredit)
            {
                return true;
            }

            if ((!isContra && Side == ZeroPlus.Models.Data.Enums.Side.Buy) ||
                (isContra && Side == ZeroPlus.Models.Data.Enums.Side.Sell))
            {
                if (orderInfo.Price < 0)
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckForVerticalSpacing(bool isContra, OpsOrderModel orderInfo)
        {
            if (!OmsCore.User.CheckForVerticalSpacing)
            {
                return true;
            }

            return Math.Abs(orderInfo.Price) < Math.Abs(Legs[0].Strike.Strike - Legs[1].Strike.Strike);
        }

        protected virtual void SetAutoCancelTriggers()
        {

        }

        public void ShowErrorMessage(string message)
        {
            if (!IsBasketOrder && TicketStyle == OrderTicketStyle.Complex)
            {
                ContraStatus = message;
                ContraStatusMode = StatusMode.CancelledSell;
            }
            else
            {
                Status = message;
                StatusMode = StatusMode.CancelledSell;
            }
        }

        public void ShowContraErrorMessage(string exMessage)
        {
            ContraStatus = exMessage;
            ContraStatusMode = StatusMode.CancelledSell;
        }

        internal double CalculateEdgeToTheo()
        {
            return IsSingleLegSell ? Price - NetDeltaAdjTheo : NetDeltaAdjTheo - Price;
        }

        public void CheckForPosEffect(bool isContra, bool isClosing)
        {
            if (InstanceMode is InstanceMode.AT_TB or InstanceMode.AT_ZPFIX or InstanceMode.OPS_TB or InstanceMode.OPS_ZPFIX)
            {
                if (isClosing)
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        leg.Position = Positions.CLOSE.ToString();
                    }
                }
                else
                {
                    foreach (TicketLegModel leg in Legs)
                    {
                        leg.Position = Positions.OPEN.ToString();
                    }
                }
            }
        }

        public OpsOrderModel BuildOrder(bool isContra, OrderSubType? subType)
        {
            OpsOrderModel orderInfo;
            if (IsSingleLeg)
            {
                orderInfo = BuildSingleLegOrder(isContra, Legs[0], subType);
            }
            else
            {
                orderInfo = BuildMultiLegOrder(isContra, Legs, subType);
            }

            return orderInfo;
        }

        public bool IsClosing(bool isContra)
        {
            bool isClosing = false;
            if (_lastTraderPositionUpdate != null)
            {
                if (!isContra)
                {
                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Buy && _lastTraderPositionUpdate.NetQty < 0) ||
                        (Side == ZeroPlus.Models.Data.Enums.Side.Sell && _lastTraderPositionUpdate.NetQty > 0))
                    {
                        isClosing = true;
                    }
                }
                else
                {
                    if ((Side == ZeroPlus.Models.Data.Enums.Side.Sell && _lastTraderPositionUpdate.NetQty < 0) ||
                        (Side == ZeroPlus.Models.Data.Enums.Side.Buy && _lastTraderPositionUpdate.NetQty > 0))
                    {
                        isClosing = true;
                    }
                }
            }

            return isClosing;
        }

        private bool CheckForRiskViolation(bool isContra, int qty, double price, string route)
        {

            if (_riskModel != null && _riskModel.LastFillCrossThresholdEnabled && !double.IsNaN(LastFillPx))
            {
                if (!isContra)
                {
                    if (IsSingleLegSell)
                    {
                        if (price < LastFillPx - _riskModel.LastFillCrossThreshold)
                        {
                            bool proceed = GetRiskVerification($"Price crosses last fill by more than {_riskModel.LastFillCrossThreshold}!\nAre you sure you want to proceed?", "Risk Violation") == RiskWarningMessageResponse.Proceed;
                            if (!proceed)
                            {
                                string summary = "Risk Last Fill Cross";
                                if (!isContra)
                                {
                                    ShowErrorMessage(summary);
                                }
                                else
                                {
                                    ShowContraErrorMessage(summary);
                                }
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (price > LastFillPx + _riskModel.LastFillCrossThreshold)
                        {
                            bool proceed = GetRiskVerification($"Price crosses last fill by more than {_riskModel.LastFillCrossThreshold}!\nAre you sure you want to proceed?", "Risk Violation") == RiskWarningMessageResponse.Proceed;
                            if (!proceed)
                            {
                                string summary = "Risk Last Fill Cross";
                                if (!isContra)
                                {
                                    ShowErrorMessage(summary);
                                }
                                else
                                {
                                    ShowContraErrorMessage(summary);
                                }
                                return true;
                            }
                        }
                    }
                }
            }

            if (!IsBasketOrder)
            {
                if (TicketStyle != OrderTicketStyle.Dual ||
                    OmsCore.Config.ShowEdgeRiskWarningOnDualTickets)
                {
                    if (Side != null && _spreadPosition != 0)
                    {
                        Side? side = isContra ? Side : Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                        Tuple<string, Side?> key = Tuple.Create(SpreadId, side);
                        if (_spreadIdAndSideToLastAvgFillPxMap.TryGetValue(key, out double lastAvgFillPx))
                        {
                            int qtyDiff = Math.Abs(qty) - Math.Abs(_spreadPosition);
                            bool qtyViolation = ((Side == ZeroPlus.Models.Data.Enums.Side.Sell && _spreadPosition > 0) || (Side == ZeroPlus.Models.Data.Enums.Side.Buy && _spreadPosition < 0)) && qtyDiff > _riskModel.QtyViolationMinLimit;
                            double edge = 0.0;
                            if (IsSingleLeg)
                            {
                                double sellPx;
                                double buyPx;
                                if (IsSingleLegSell)
                                {
                                    sellPx = price;
                                    buyPx = lastAvgFillPx;
                                }
                                else
                                {
                                    sellPx = lastAvgFillPx;
                                    buyPx = price;
                                }
                                edge = buyPx - sellPx;
                            }
                            else
                            {
                                edge = price + lastAvgFillPx;
                            }
                            if (edge > _riskModel.EdgeViolationMinLimit || qtyViolation)
                            {
                                string message = "";
                                string summary = "Risk ";

                                if (edge > _riskModel.EdgeViolationMinLimit)
                                {
                                    message += "Your price is off by $" + Math.Round(edge, 2) + " from your last fill.\n";
                                    summary += "Edge - ";
                                }
                                if (qtyViolation)
                                {
                                    message += "Your qty is off by " + qtyDiff + " from your spread position.\n";
                                    summary += "Qty - ";
                                }

                                message += "Are you sure you want to proceed?";
                                summary += "Violation";
                                _log.Warn($"{nameof(CheckForRiskViolation)} Showing edge violation warn.{GetStats()}\nMessage: {message}");
                                bool proceed = GetRiskVerification(message, summary) == RiskWarningMessageResponse.Proceed;
                                if (proceed)
                                {
                                    _spreadIdAndSideToLastAvgFillPxMap.TryRemove(key, out _);
                                }
                                else
                                {
                                    if (!isContra)
                                    {
                                        ShowErrorMessage(summary);
                                    }
                                    else
                                    {
                                        ShowContraErrorMessage(summary);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                    else if (ShowEdgeIndicators)
                    {
                        double edge = !double.IsNaN(AcquiredEdge) ? AcquiredEdge : ProjectedEdge;
                        if (edge < 0)
                        {
                            edge = Math.Abs(edge);
                            if (edge > _riskModel.EdgeViolationMinLimit)
                            {
                                string message = "Your price is off by $" + Math.Round(edge, 2) + " from your 3-way fill.\nAre you sure you want to proceed?";
                                string summary = "Risk Edge - Violation";
                                _log.Warn($"{nameof(CheckForRiskViolation)} Showing edge violation warn.{GetStats()}\nMessage: {message}");
                                bool proceed = GetRiskVerification(message, summary) == RiskWarningMessageResponse.Proceed;
                                if (!proceed)
                                {
                                    if (!isContra)
                                    {
                                        ShowErrorMessage(summary);
                                    }
                                    else
                                    {
                                        ShowContraErrorMessage(summary);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            if (!IsSingleLeg)
            {
                if (!string.IsNullOrWhiteSpace(route) && SingleLegOnlyRoutes.Contains(route.ToUpper()))
                {
                    string message = "You are about to submit spread orders to a route that does not support spreads.\nAre you sure you want to proceed?";
                    string summary = "Risk - Invalid Route For Type";
                    bool proceed = GetRiskVerification(message, summary) == RiskWarningMessageResponse.Proceed;
                    if (!proceed)
                    {
                        if (!isContra)
                        {
                            ShowErrorMessage(summary);
                        }
                        else
                        {
                            ShowContraErrorMessage(summary);
                        }
                        return true;
                    }
                }
            }

            if (_algoRoutes.Contains(route))
            {
                if (_nonAlgoUnders.Contains(Underlying))
                {
                    string message = "Route not supported for given underlying!\nAre you sure you want to proceed?";
                    string summary = "Risk - Invalid Route For Underlying";
                    bool proceed = GetRiskVerification(message, summary) == RiskWarningMessageResponse.Proceed;
                    if (!proceed)
                    {
                        if (!isContra)
                        {
                            ShowErrorMessage(summary);
                        }
                        else
                        {
                            ShowContraErrorMessage(summary);
                        }
                        return true;
                    }
                }
            }

            return false;
        }

        private void FishTimer_Ellapsed(object sender, EventArgs args)
        {
            _fishTimer.Stop();

            if (!IsBasketOrder)
            {
                if (OrderIsClosed || _routeFish == null)
                {
                    return;
                }

                _increment = Underlying == "$SPX" && _routeFish.Increment < 0.05 ? 0.05 : _routeFish.Increment;
                if (!Closing)
                {
                    if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        double price = FishPrice - _increment;
                        FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, false) : price;

                        if (FishPrice >= Price)
                        {
                            ModifyFish();
                        }
                        else
                        {
                            CancelMain();
                            _log.Info("Auto cancel triggered by fish timer." +
                                  ", Spread: " + SpreadId +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        }
                    }
                    else
                    {
                        double price = FishPrice + _increment;
                        FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, true) : price;

                        if (FishPrice <= Price)
                        {
                            ModifyFish();
                        }
                        else
                        {
                            CancelMain();
                            _log.Info("Auto cancel triggered by fish timer." +
                                  ", Spread: " + SpreadId +
                                  ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                        }
                    }
                }
                else
                {
                    double stopPrice = ContraPrice;

                    if (TicketStyle == OrderTicketStyle.Combined &&
                        OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
                        !IsBasketOrder)
                    {
                        stopPrice = -ContraPrice;
                    }

                    if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        double price = FishPrice - _increment;
                        FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : price;
                        if (FishPrice >= stopPrice)
                        {
                            ModifyContraFish();
                        }
                        else
                        {
                            CancelContra();
                        }
                    }
                    else
                    {
                        double price = FishPrice + _increment;
                        FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : price;

                        if (FishPrice <= stopPrice)
                        {
                            ModifyContraFish();
                        }
                        else
                        {
                            CancelContra();
                        }
                    }
                }
            }
            else
            {
                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                if (!Closing)
                {
                    if (!BasketSettings.FishModeEnabled)
                    {
                        CancelMain();
                    }
                    else
                    {
                        if (!OrderIsClosed)
                        {
                            _increment = Underlying == "$SPX" && automationConfigModel.FishPriceIncrement < 0.05 ? 0.05 : automationConfigModel.FishPriceIncrement;
                            if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                            {
                                double price = FishPrice - _increment;
                                FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, false) : price;

                                if (FishPrice >= Price)
                                {
                                    if (!automationConfigModel.UseResubmit)
                                    {
                                        ModifyFish();
                                    }
                                }
                                else
                                {
                                    CancelMain();
                                }
                            }
                            else
                            {
                                double price = FishPrice + _increment;
                                FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price, true) : price;

                                if (FishPrice <= Price)
                                {
                                    if (!automationConfigModel.UseResubmit)
                                    {
                                        ModifyFish();
                                    }
                                }
                                else
                                {
                                    CancelMain();
                                    _log.Info("Auto cancel triggered by fish timer." +
                                          ", Spread: " + SpreadId +
                                          ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!automationConfigModel.GoFishAutoCloseEnabled && !_manualClose)
                    {
                        CancelContra();
                    }
                    else
                    {
                        if (!OrderIsClosed)
                        {
                            if (!_manualClose)
                            {
                                _increment = Underlying == "$SPX" && automationConfigModel.ContraFishPriceIncrement < 0.05 ? 0.05 : automationConfigModel.ContraFishPriceIncrement;
                            }

                            if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                            {
                                double price = FishPrice - _increment;
                                FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : price;

                                if (FishPrice >= ContraPrice)
                                {
                                    if (!automationConfigModel.UseResubmit)
                                    {
                                        ModifyContraFish();
                                    }
                                }
                                else
                                {
                                    CancelContra();
                                }
                            }
                            else
                            {
                                double price = FishPrice + _increment;

                                FishPrice = PriceNeedsPadding(price) ? PadForNickelOrDime(price) : price;

                                if (FishPrice <= ContraPrice)
                                {
                                    if (!automationConfigModel.UseResubmit)
                                    {
                                        ModifyContraFish();
                                    }
                                }
                                else
                                {
                                    CancelContra();
                                }
                            }
                        }
                    }
                }
            }
        }

        protected void SubmitClosingOrderAsync(double averagePrice,
                                             double maxLoss,
                                             double edge,
                                             ClosingTypes closingMode,
                                             int? delay = null,
                                             double interval = -1,
                                             double increment = .01,
                                             bool manualClose = false)
        {
            if (!OrderIsClosed)
            {
                return;
            }
            if (!delay.HasValue)
            {
                delay = OmsCore.Config.LoopDelayMin >= OmsCore.Config.LoopDelayMax
                                  ? OmsCore.Config.LoopDelayMin
                                  : Random.Shared.Next(OmsCore.Config.LoopDelayMin, OmsCore.Config.LoopDelayMax);
            }
            if (delay > 0)
            {
                Task.Delay((int)delay).ContinueWith(t =>
                    Task.Run(() => SubmitClosingOrder(averagePrice, closingMode, maxLoss, edge, interval, increment, manualClose)));
            }
            else
            {
                Task.Run(() => SubmitClosingOrder(averagePrice, closingMode, maxLoss, edge, interval, increment, manualClose));
            }
        }

        private async void SubmitClosingOrder(double averagePrice, ClosingTypes closingMode, double maxLoss, double edge, double interval, double increment, bool manualClose)
        {
            try
            {
                Closing = true;
                OpsOrderModel orderInfo = null;

                switch (closingMode)
                {
                    case ClosingTypes.CxlResubmit:
                        orderInfo = BuildContraOrder(averagePrice, maxLoss, edge);
                        break;
                    case ClosingTypes.ThreeWay:
                        if (CanCreateThreeWay())
                        {
                            orderInfo = await BuildThreewayClosingOrderAsync(averagePrice);
                        }
                        else
                        {
                            orderInfo = BuildContraOrder(averagePrice, maxLoss, edge);
                        }
                        break;
                    default:
                        break;
                }

                OrderIsClosed = false;
                if (manualClose)
                {
                    orderInfo.SetCancelDelay(0);
                }
                SubmitContraOrder(orderInfo);
                _increment = increment;
                _manualClose = manualClose;
                _fishInterval = interval;
                StartFishTimer();
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
                ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
                ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
            }
        }

        private OpsOrderModel BuildContraOrder(double averagePrice, double maxLoss, double edge)
        {
            var orderInfo = BuildOrder(isContra: true, subtype: OrderSubType.AutoClose);

            if (!TrySelectRoute(isContra: false, lookupOnly: true, out string route, out _))
            {
                route = Route;
            }
            double ticketFees = GetTotalFeesForTicket(route);

            if (IsSingleLeg)
            {
                bool contraIsSell = Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                if (contraIsSell)
                {
                    double fillPx = Math.Round(averagePrice, 2, MidpointRounding.AwayFromZero);
                    ContraPrice = fillPx - maxLoss + ticketFees;
                    FishPrice = fillPx + edge;
                    orderInfo.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, false) : FishPrice;
                }
                else
                {
                    double fillPx = Math.Round(averagePrice, 2, MidpointRounding.AwayFromZero);
                    ContraPrice = fillPx + maxLoss - ticketFees;
                    FishPrice = fillPx - edge;
                    orderInfo.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, true) : FishPrice;
                }
            }
            else if (Legs.Count > 1)
            {
                double fillPx = Math.Round(averagePrice * -1.0, 2, MidpointRounding.AwayFromZero);
                ContraPrice = fillPx + maxLoss - ticketFees;
                FishPrice = fillPx - edge;
                orderInfo.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, true) : FishPrice;
            }

            return orderInfo;
        }

        private async Task<OpsOrderModel> BuildThreewayClosingOrderAsync(double averagePrice)
        {
            try
            {
                List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                if (options.Count <= 0 || !CanCreateThreeWay())
                {
                    throw new SlimException("Can't build three way closer.");
                }

                TicketLegModel swappedLeg = GetSwappedLeg();
                Option swapOption = GetSwappingOption(options, swappedLeg);

                List<TicketLegModel> secondTicketLegs = BuildThreeWaySecondLegs(swappedLeg, swapOption);
                List<TicketLegModel> thirdTicketLegs = BuildThreeWayVericalLegs(swappedLeg, swapOption);

                string thirdTicketTos = GetTosFromLegs(thirdTicketLegs);
                DataStore _deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                _deltaStore.GetHanweckDataFor(thirdTicketTos, SubscriptionFieldType.TheorethicalValue);

                double verticalTheo = await _deltaStore.GetDataAsync(thirdTicketTos);
                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                if (!ThreeWayStarted)
                {
                    string secondTicketTos = GetTosFromLegs(secondTicketLegs);
                    _deltaStore.GetHanweckDataFor(secondTicketTos, SubscriptionFieldType.TheorethicalValue);
                    ThreeWayStarted = true;
                    double secondTicketTheo = await _deltaStore.GetDataAsync(secondTicketTos);
                    OpsOrderModel threeWayCloser = BuildMultiLegOrder(isContra: false, secondTicketLegs, OrderSubType.ThreeWayCloser, false, secondTicketTheo);
                    ContraPrice = -averagePrice - (verticalTheo + GetTotalFeesForLegs(thirdTicketLegs)) + automationConfigModel.LoopMaxLoss - GetTotalFeesForLegs(secondTicketLegs);
                    FishPrice = -averagePrice - (verticalTheo + GetTotalFeesForLegs(thirdTicketLegs)) - automationConfigModel.ContraFishEdge;
                    threeWayCloser.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, true) : FishPrice;
                    return threeWayCloser;
                }
                else
                {
                    ThreeWayStarted = false;
                    ThreeWayComplete = true;
                    OpsOrderModel threeWayVertical = BuildMultiLegOrder(isContra: false, thirdTicketLegs, OrderSubType.ThreeWayCloser, false, verticalTheo);
                    ContraPrice = -averagePrice - (LastFillPx + GetTotalFeesForLegs(secondTicketLegs)) + automationConfigModel.LoopMaxLoss - GetTotalFeesForLegs(thirdTicketLegs);
                    FishPrice = verticalTheo;
                    threeWayVertical.Price = PriceNeedsPadding(FishPrice) ? PadForNickelOrDime(FishPrice, true) : FishPrice;
                    return threeWayVertical;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BuildThreewayClosingOrderAsync));
                return null;
            }
        }

        protected void GetFeesForBothSide(out double openingFee, out double closingFee)
        {
            UpdateRoutes(out string route, out string contraRoute);
            openingFee = GetTotalFeesForTicket(route);
            closingFee = GetTotalFeesForTicket(contraRoute);
        }

        internal double GetTotalFeesForTicket(string route = "", string exchange = "", bool reverse = false, bool getWorst = false)
        {
            return GetTotalFeesForLegs(Legs.ToList(), route, exchange, reverse, getWorst);
        }

        internal double GetTotalFeesForLegs(List<TicketLegModel> legs, string route = "", string exchange = "", bool reverse = false, bool getWorst = false)
        {
            double totalFees = 0.0;
            try
            {
                double execBrokerFee;
                ExecutingBrokerFeeModel feeModel;

                if (string.IsNullOrWhiteSpace(route))
                {
                    TrySelectRoute(isContra: false, lookupOnly: true, out route, out _);
                }


                if (string.IsNullOrWhiteSpace(route))
                {
                    feeModel = OmsCore.Config.GetExecutingBrokerFeeModel(Data.Models.ExecutingBroker.InteractiveBrokers);
                    execBrokerFee = feeModel.IsAlgoRoute(route) ? feeModel.AlgoExecutionFee : feeModel.ExecutionFee;
                }
                else if (OmsCore.Config.IsAlgoRoute(route, out feeModel))
                {
                    execBrokerFee = feeModel.AlgoExecutionFee;
                }
                else if (route.StartsWith("B") &&
                         route != "BATS" &&
                         route != "BOX" &&
                         route != "BX")
                {
                    feeModel = OmsCore.Config.GetExecutingBrokerFeeModel(Data.Models.ExecutingBroker.Volant);
                    execBrokerFee = feeModel.IsAlgoRoute(route) ? feeModel.AlgoExecutionFee : feeModel.ExecutionFee;
                }
                else if (route.StartsWith("D"))
                {
                    feeModel = OmsCore.Config.GetExecutingBrokerFeeModel(Data.Models.ExecutingBroker.Dash);
                    execBrokerFee = feeModel.IsAlgoRoute(route) || Underlying == "$SPX" ? feeModel.AlgoExecutionFee : feeModel.ExecutionFee;
                }
                else if (route.StartsWith("I") &&
                         route != "ISE" &&
                         route != "IB")
                {
                    feeModel = OmsCore.Config.GetExecutingBrokerFeeModel(Data.Models.ExecutingBroker.Instinet);
                    execBrokerFee = feeModel.IsAlgoRoute(route) ? feeModel.AlgoExecutionFee : feeModel.ExecutionFee;
                }
                else
                {
                    feeModel = OmsCore.Config.GetExecutingBrokerFeeModel(Data.Models.ExecutingBroker.InteractiveBrokers);
                    execBrokerFee = feeModel.IsAlgoRoute(route) ? feeModel.AlgoExecutionFee : feeModel.ExecutionFee;
                }

                double rebate = 0;
                if (Underlying != null && OmsCore.Config.UnderlyingToCommissionsMap.TryGetValue(Underlying, out Comms.Models.Data.Oms.Commission commissions))
                {
                    if (string.IsNullOrWhiteSpace(exchange))
                    {
                        rebate = IsSingleLeg
                            ? 0.30
                            : getWorst ? commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsMax : OmsCore.Config.ExecutingBrokerFeeModelsMaxNonPenny :
                                commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsAverage : OmsCore.Config.ExecutingBrokerFeeModelsAverageNonPenny;
                    }
                    else if (!commissions.Lookup.TryGetValue(exchange.ToUpper(), out rebate))
                    {
                        if (!commissions.Lookup.TryGetValue(exchange[1..].ToUpper(), out rebate))
                        {
                            if (feeModel != null)
                            {
                                rebate = feeModel.DefaultExchangeFee;
                            }
                            else if (OmsCore.RouteToExchangeLookup.TryGetValue(exchange[1..], out string[] exchanges))
                            {
                                double total = 0.0;
                                int count = 0;
                                foreach (string exch in exchanges)
                                {
                                    if (commissions.Lookup.TryGetValue(exch.ToUpper(), out double tempRebate))
                                    {
                                        total += tempRebate;
                                        count++;
                                    }
                                }
                                if (count > 0)
                                {
                                    rebate = total / count;
                                }
                                else
                                {
                                    rebate = IsSingleLeg
                                        ? 0.30
                                        : getWorst ? commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsMax : OmsCore.Config.ExecutingBrokerFeeModelsMaxNonPenny :
                                            commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsAverage : OmsCore.Config.ExecutingBrokerFeeModelsAverageNonPenny;
                                }
                            }
                            else
                            {
                                rebate = IsSingleLeg
                                    ? 0.30
                                    : getWorst ? commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsMax : OmsCore.Config.ExecutingBrokerFeeModelsMaxNonPenny :
                                        commissions.IsPenny ? OmsCore.Config.ExecutingBrokerFeeModelsAverage : OmsCore.Config.ExecutingBrokerFeeModelsAverageNonPenny;
                            }
                        }
                    }
                }

                foreach (TicketLegModel leg in legs)
                {
                    totalFees += OmsCore.Config.BrokerageFee * leg.Ratio;
                    totalFees += OmsCore.Config.OrfFee * leg.Ratio;

                    totalFees += execBrokerFee * leg.Ratio;

                    bool isSell = leg.Side.ToString().ToUpper() == "SELL";
                    if (((isSell && !reverse) ||
                        (!isSell && reverse)) &&
                        Underlying != "$SPX" &&
                        Underlying != "$NDX" &&
                        Underlying != "$RUT")
                    {
                        double secFee = OmsCore.Config.SecFee * (leg.AveragePrice != 0 ? leg.AveragePrice : (leg.Bid + leg.Ask) / 2) * 100.0 * leg.Ratio;
                        if (!double.IsNaN(secFee))
                        {
                            totalFees += secFee;
                        }
                    }

                    totalFees += rebate * leg.Ratio;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetTotalFeesForLegs));
            }
            return totalFees / 100;
        }

        private TicketLegModel GetSwappedLeg()
        {
            TicketLegModel[] legs = Legs.OrderBy(x => x.Ratio).ToArray();

            TicketLegModel swappedLeg = legs[0];

            string type = Legs.First().Type;
            if (Legs.Select(x => x.Ratio).Distinct().Count() == 1)
            {
                switch (OmsCore.Config.ThreeWayPreference)
                {
                    case ThreeWayPreference.ITM when type == Types.CALL.ToString():
                    case ThreeWayPreference.OTM when type == Types.PUT.ToString():
                        swappedLeg = legs.OrderBy(x => x.Strike).First();
                        break;

                    case ThreeWayPreference.OTM when type == Types.CALL.ToString():
                    case ThreeWayPreference.ITM when type == Types.PUT.ToString():
                        swappedLeg = legs.OrderByDescending(x => x.Strike).First();
                        break;
                }
            }

            return swappedLeg;
        }

        private Option GetSwappingOption(List<Option> options, TicketLegModel swappedLeg)
        {
            return options.Where(x => x.Type.ToString() == swappedLeg.Type)
                          .Where(x => x.Expiration == swappedLeg.ExpirationInfo.Expiration)
                          .Where(x => !Legs.Any(leg => x.Strike == leg.Strike))
                          .OrderBy(x => Math.Abs(x.Strike - swappedLeg.Strike.Strike))
                          .FirstOrDefault();
        }

        protected static string GetTosFromLegs(List<TicketLegModel> thirdTicketLegs)
        {
            try
            {
                if (thirdTicketLegs == null || thirdTicketLegs.Count == 0)
                {
                    return "";
                }
                else
                {
                    string tos = "";
                    for (int i = 0; i < thirdTicketLegs.Count; i++)
                    {
                        TicketLegModel leg = thirdTicketLegs[i];
                        if (i == 0)
                        {
                            tos += leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? "" : "-";
                        }
                        else
                        {
                            tos += leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? "+" : "-";
                        }
                        if (leg.Quantity > 1)
                        {
                            tos += leg.Quantity + "*";
                        }
                        tos += leg.Symbol;
                    }
                    return tos;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetTosFromLegs));
                return "";
            }
        }

        private List<TicketLegModel> BuildThreeWaySecondLegs(TicketLegModel swappedLeg, Option swapOption)
        {
            List<TicketLegModel> secondTicketLegs = new();

            foreach (TicketLegModel leg in Legs)
            {
                if (leg.Symbol == swappedLeg.Symbol)
                {
                    secondTicketLegs.Add(new TicketLegModel(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                    {
                        Symbol = swapOption.OptionSymbol,
                        Quantity = swappedLeg.Quantity,
                        Ratio = swappedLeg.Ratio,
                        Type = swappedLeg.Type.ToString(),
                        Side = swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                }
                else
                {
                    secondTicketLegs.Add(new TicketLegModel(OmsCore, leg.Underlying, leg.Account, null, _portfolioManagerModel)
                    {
                        Symbol = leg.Symbol,
                        Quantity = leg.Quantity,
                        Ratio = leg.Ratio,
                        Type = leg.Type.ToString(),
                        Side = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                    });
                }
            }
            ;
            return secondTicketLegs;
        }

        private List<TicketLegModel> BuildThreeWayVericalLegs(TicketLegModel swappedLeg, Option swapOption)
        {
            return new List<TicketLegModel>
            {
                new(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                {
                    Symbol = swappedLeg.Symbol,
                    Quantity = swappedLeg.Quantity,
                    Ratio = swappedLeg.Ratio,
                    Type = swappedLeg.Type.ToString(),
                    Side = swappedLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy
                },
                new(OmsCore, swappedLeg.Underlying, swappedLeg.Account, null, _portfolioManagerModel)
                {
                    Symbol = swapOption.OptionSymbol,
                    Quantity = swappedLeg.Quantity,
                    Ratio = swappedLeg.Ratio,
                    Type = swappedLeg.Type.ToString(),
                    Side = swappedLeg.Side
                }
            };
        }

        public OpsOrderModel BuildOrder(bool isContra, OrderSubType? subtype, int qty = -1)
        {
            ValidateAccount();
            ValidateBroker();

            List<TicketLegModel> validLegs = Legs.ToList().Where(leg => leg.IsValid).ToList();

            if (validLegs.Count == 0)
            {
                throw new SlimException("No valid legs.");
            }

            OpsOrderModel orderInfo;
            if (validLegs.Count == 1)
            {
                orderInfo = BuildSingleLegOrder(isContra: isContra, leg: validLegs[0], subtype, qty: qty);
            }
            else
            {
                orderInfo = BuildMultiLegOrder(isContra: isContra, validLegs: validLegs, subtype, stampValues: true, overrideTheo: double.NaN, qty: qty);
            }

            return orderInfo;
        }

        public async void SubmitMainOrder(OpsOrderModel orderInfo)
        {
            try
            {
                if (!IsValidPriceForStrategy(false, orderInfo))
                {
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    throw new SlimException("Invalid Px For Strategy.");
                }

                _latencyTimer.Restart();
                LastOrderEdgeToTheo = IsSingleLegSell ? Price - NetDeltaAdjTheo : NetDeltaAdjTheo - Price;
                ClearOrderIds(contra: false);
                MainNotFilled = true;
                MainResting = true;
                LastPx = orderInfo.Price;
                LastRoute = orderInfo.Route;
                LastQty = orderInfo.Qty;
                orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                _log.Info("Order submitted. Spread: " + SpreadId + ", OrderId:" + OrderId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Contra: False" + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                OrderIdsSet.Add(orderInfo.LocalID);
                OrderId = await OmsCore.OrderClient.SendOrderAsync(orderInfo, GetInstanceMode(), this, false, Multiplier, checkForDuplicate: !IsBasketOrder, !_orderDetailsContainer.IsEmpty ? _orderDetailsContainer.ToList() : null);
                if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
                {
                    int qty = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? orderInfo.Qty : -orderInfo.Qty;
                    lock (PositionUpdateLock)
                    {
                        SingleOrderTicketWorkingPosition += qty;
                    }
                }
                OrderIdsSet.Add(OrderId);
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: true, null);
                ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: true, null);
                if (ex.ErrorType == ErrorType.DuplicateOrderFound && orderInfo != null)
                {
                    if (GetVerification($"Duplicate resting order found for {SpreadId}, do you want to clear it?", "Order Risk Check"))
                    {
                        OmsCore.OrderClient.ClearOrder(orderInfo.Symbol);
                    }
                }
                else
                {
                    Reason = ex.Message;
                    ShowMessage(ex.Message, "Order Submission Failed.");
                }
            }
            catch (RouteSelectionException ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: true, null);
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitOrderAsync));
                NotifyOrderCloseWaitHandlers(main: true, null);
            }
        }

        public async void SubmitContraOrder(OpsOrderModel orderInfo)
        {
            try
            {
                if (!IsValidPriceForStrategy(true, orderInfo))
                {
                    _latencyTimer.Stop();
                    NotifyOrderCloseWaitHandlers(main: true, null);
                    throw new SlimException("Invalid Px For Strategy.");
                }

                _latencyTimer.Restart();
                LastContraOrderEdgeToTheo = !IsSingleLegSell ? ContraPrice - NetDeltaAdjTheo : IsSingleLeg ? NetDeltaAdjTheo - ContraPrice : -NetDeltaAdjTheo - ContraPrice;
                ClearOrderIds(contra: true);
                ContraNotFilled = true;
                ContraResting = true;
                LastContraPx = orderInfo.Price;
                LastContraQty = orderInfo.Qty;
                orderInfo.LocalID = OmsCore.OrderClient.GetNextOrderId();
                _log.Info("Order submitted. Spread: " + SpreadId + ", OrderId:" + OrderId + ", Current px:" + Price + ", Contra px:" + ContraPrice + ", Contra: True" + ", Latency Timer:" + _latencyTimer.ElapsedMilliseconds + ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                ContraOrderIdsSet.Add(orderInfo.LocalID);
                ContraOrderId = await OmsCore.OrderClient.SendOrderAsync(orderInfo, GetInstanceMode(), this, false, Multiplier, checkForDuplicate: !IsBasketOrder, !_orderDetailsContainer.IsEmpty ? _orderDetailsContainer.ToList() : null);
                _lastContraRoute = orderInfo.Route;
                if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
                {
                    int qty = Side == ZeroPlus.Models.Data.Enums.Side.Sell ? orderInfo.Qty : -orderInfo.Qty;
                    lock (PositionUpdateLock)
                    {
                        SingleOrderTicketWorkingPosition += qty;
                    }
                }
                ContraOrderIdsSet.Add(ContraOrderId);
            }
            catch (SendOrderServerException ex)
            {
                _log.Error(ex, nameof(SubmitContraOrder));
                NotifyOrderCloseWaitHandlers(main: false, null);
                ShowMessage(ex.Message, "Order Submission Failed.");
            }
            catch (SlimException ex)
            {
                _log.Error(ex, nameof(SubmitContraOrder));
                NotifyOrderCloseWaitHandlers(main: false, null);
                if (ex.ErrorType == ErrorType.DuplicateOrderFound && orderInfo != null)
                {
                    if (GetVerification($"Duplicate resting order found for {SpreadId}, do you want to clear it?", "Order Risk Check"))
                    {
                        OmsCore.OrderClient.ClearOrder(orderInfo.Symbol);
                    }
                }
                else
                {
                    Reason = ex.Message;
                    ShowMessage(ex.Message, "Order Submission Failed.");
                }
            }
            catch (RouteSelectionException ex)
            {
                _log.Error(ex, nameof(SubmitContraOrder));
                NotifyOrderCloseWaitHandlers(main: false, null);
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitContraOrder));
                NotifyOrderCloseWaitHandlers(main: false, null);
            }
        }

        internal void ModifyMainOrder(double price, int qty)
        {
            Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : price > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
            ModifyRequest modifyRequest = new()
            {
                LocalId = LocalId ?? "",
                PermId = PermID ?? "",
                OrderId = OrderId ?? "",
                Price = price,
                Quantity = qty,
            };
            OmsCore.OrderClient.CancelReplaceOrder(modifyRequest, IsSingleLeg, side, Multiplier, IsStockTicket);
        }

        internal void ModifyContraOrder(double price, int qty)
        {
            Side side = IsSingleLeg ? Side == ZeroPlus.Models.Data.Enums.Side.Sell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell : price > 0 ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;
            ModifyRequest modifyRequest = new()
            {
                LocalId = ContraLocalId ?? "",
                PermId = ContraPermId ?? "",
                OrderId = ContraOrderId ?? "",
                Price = price,
                Quantity = qty,
            };
            OmsCore.OrderClient.CancelReplaceOrder(modifyRequest, IsSingleLeg, side, Multiplier, IsStockTicket);
        }

        public void ResetOrderCloseWaitHandlers(bool main)
        {
            if (main)
            {
                OrderClosedEventIsSet = false;
                OrderClosedEvent?.Reset();
            }
        }

        public void NotifyOrderCloseWaitHandlers(bool main, OrderStatus? orderStatus)
        {
            if (main)
            {
                OrderClosedEvent?.Set();
                OrderClosedEventIsSet = true;
                MainResting = false;
            }
            else
            {
                ContraResting = false;
            }
            if (orderStatus is null or not OrderStatus.Canceled)
            {
                ResetSmartRoutes();
            }
        }

        public void ValidateAccount()
        {
            var account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            if (string.IsNullOrWhiteSpace(account))
            {
                throw new SlimException("Account can not be empty.");
            }
            if (!AccountsList.Contains(account))
            {
                throw new SlimException($"Account {account} not found within list.");
            }
        }

        public void ValidateBroker()
        {
            if (string.IsNullOrWhiteSpace(EffectiveBroker))
            {
                throw new SlimException("Broker must be selected before sending an order.");
            }
        }

        private void ClearOrderIds(bool contra)
        {
            if (!contra)
            {
                if (MainOrderStatus is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.Rejected)
                {
                    OrderIdsSet.Clear();
                }
                MainOrderStatus = null;
                OrderId = "";
                Status = "";
                Filled = "";
                StatusMode = StatusMode.Reset;
                FilledQty = 0;
            }
            else
            {
                if (ContraOrderStatus is OrderStatus.Filled or OrderStatus.Canceled or OrderStatus.Rejected)
                {
                    ContraOrderIdsSet.Clear();
                }
                ContraOrderStatus = null;
                ContraOrderId = "";
                ContraStatus = "";
                ContraFilled = "";
                ContraStatusMode = StatusMode.Reset;
            }
        }

        private void ClearHedgeIds(bool clearHistory = true)
        {
            HedgeOrderId = "";
            StockHedgeStatus = "";
            StockHedgeStatusMode = StatusMode.Reset;
            if (clearHistory)
            {
                HedgeOrderIdsSet.Clear();
            }
        }

        internal void ClearStatus()
        {
            ClearOrderIds(true);
            ClearOrderIds(false);
            ClearHedgeIds();
        }

        internal void ClearPnl()
        {
            try
            {
                ReversePnl = AdjustedPnl;
                ReverseSpreadPosition = _spreadPosition;
                AdjustedPnl = double.NaN;
                UnrealizedPnl = double.NaN;
                AvgCost = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearPnl));
            }
        }

        internal void ClearHedgePnl()
        {
            try
            {
                HedgeReversePnl = StockHedgeAdjPnl;
                HedgeReversePosition = HedgedStocks;
                StockHedgeAdjPnl = double.NaN;
                StockHedgeUnrealizedPnl = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ClearHedgePnl));
            }
        }

        public void ResetTicket()
        {
            UpdateQty(1);
            _lastTraderPositionUpdate = null;
            _lastFirmPositionUpdate = null;

            ClearStatus();

            MarketBidEma = double.NaN;
            MarketAskEma = double.NaN;
            LatestTrade = null;
            Price = double.NaN;
            ContraPrice = double.NaN;
            LastTradeUpdate = null;
            DeltaAdjLastTradeUpdate = double.NaN;
            DeltaAdjPx = double.NaN;
            DeltaAdjContraPx = double.NaN;
            LockDeltaAdjPrice = false;
            LockContraDeltaAdjPrice = false;
            BestDeltaAdjPx = double.NaN;
            BestDeltaAdjContraPx = double.NaN;
            ResetPermAdj();
            ResetAdjEdgeSummary();
            LockBestDeltaAdjPrice = false;
            LockContraBestDeltaAdjPrice = false;
            Status = "";
            ContraStatus = "";
            ReversePrompted = false;
            LastTransactionPrice = double.NaN;
            LastContraTransactionPrice = double.NaN;
            SpreadSymbol = "";
            ContraSpreadSymbol = "";

            SingleOrderTicketStopLossValue = 0;
            SingleOrderTicketTrailingStopValue = 0;
            SingleOrderTicketPosition = 0;
            SingleOrderTicketWorkingPosition = 0;
            _bidAtFillForSingleTickets = 0;
            _askAtFillForSingleTickets = 0;

            OrderIsClosed = true;
            SubmitLatency = 0;
            PendingNewLatency = 0;
            VolumeAtFill = double.NaN;
            ChangeInVolume = double.NaN;
            HedgeAttempt = 0;
            TotalStocks = 0;
            HedgedStocks = 0;
            RequiredStocks = 0;
            SubmittedStocks = 0;
            CanHedge = false;
            StockHedgeQty = 0;
            StockHedgeStatus = "";
            StockHedgeStatusMode = StatusMode.Reset;
            TraderSpreadPosition = 0;
            TraderAdjustedPnl = double.NaN;
            StockHedgeAdjPnl = double.NaN;
            HedgeSuggestion = HedgeSuggestion.None;
            EstHedgeCost = double.NaN;
            StockHedgeUnrealizedPnl = double.NaN;
            StockHedgeAdjTradePx = double.NaN;
            StockPriceAtHedge = double.NaN;
            AdjustedPriceAtHedge = double.NaN;
            StockHedgeRoute = OmsCore.Config.DefaultHedgeRoute(InstanceMode);
            Side = null;
            Offset = 0;
            StrikeOffset = 0;
            PartiallyFilled = false;
            ContraPartiallyFilled = false;
            CumulativeQty = 0;
            ContraCumulativeQty = 0;
            StopLossEnabled = false;
            StopLossTriggerPrice = double.NaN;
            StopLossCloseTriggerPrice = double.NaN;
            StopLossSide = Sides.FirstOrDefault();
            ClearUi();

            ResetEvents(false);

            OnTicketReset();

            Dispatcher?.BeginInvoke(() => TronTrades?.Clear());
        }

        protected virtual void OnTicketReset()
        {

        }

        private void ResetPermAdj()
        {
            PermAdjPxBase = double.NaN;
            PermAdjContraPxBase = double.NaN;
            PermAdjPxLoaded = false;
            UnderMidAtPermLoad = double.NaN;
            PermAdjPx = double.NaN;
            PermAdjContraPx = double.NaN;
        }

        private void ResetAdjEdgeSummary()
        {
            _adjEdgeSummaryBidBase = double.NaN;
            _adjEdgeSummaryAskBase = double.NaN;
            _adjEdgeSummaryUnderMidAtLoad = double.NaN;
            _adjEdgeSummaryLoaded = false;
            AdjEdgeSummaryBid = double.NaN;
            AdjEdgeSummaryAsk = double.NaN;
        }

        internal OpsOrderModel BuildStockHedgeOrderAsync(int qty, string comment = null, string subType = null)
        {
            Side side = qty < 0 ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
            ZeroPlus.Models.Data.Enums.OrderType orderType = OmsCore.Config.HedgeOrderType;
            double cancelDelay = !IsBasketOrder ? OmsCore.Config.HedgeInterval : BasketSettings.HedgeInterval;
            double pxDiff = 0.0;
            if (orderType == ZeroPlus.Models.Data.Enums.OrderType.Limit)
            {
                pxDiff = !IsBasketOrder ? OmsCore.Config.AutoHedgeLimitDiff : BasketSettings.HedgeLimitEdge;
                if (HedgeAttempt > 0)
                {
                    pxDiff += !IsBasketOrder ? OmsCore.Config.AutoHedgeLimitIncrement : BasketSettings.HedgeLimitIncrement;
                }
            }
            double price = side == ZeroPlus.Models.Data.Enums.Side.Buy ? HedgeAsk + pxDiff : HedgeBid - pxDiff;

            comment ??= GeHedgeIdentifier();
            string route = !string.IsNullOrWhiteSpace(StockHedgeRoute) ? StockHedgeRoute : OmsCore.Config.DefaultHedgeRoute(InstanceMode);

            var tif = GetTif();
            if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
            {
                tif = route.StartsWith("D") ?
                    TimeInForce.GTX :
                    TimeInForce.ETH;
            }

            subType ??= SubType?.ToSpacedString() + ModuleTypeSuffix;

            var account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;

            var details = OmsCore.QuoteClient.GetUnderlyingDetails(HedgeUnderlying);

            int absQty = Math.Abs(qty);

            OpsOrderModel order = new()
            {
                Symbol = HedgeUnderlying,
                Qty = absQty,
                OMSSide = side.ToString(),
                OpenClose = "Auto",
                Price = price,
                Account = account,
                TimeInForce = tif,
                Route = route,
                OMSOrderType = orderType.ToString().ToUpper(),
                Timestamp = DateTime.Now,
                UnderlyingSymbol = HedgeUnderlying,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,

                BaseStrategy = BaseStrategy.STOCK,
                Currency = Currency,
                SpreadId = HedgeUnderlying,
                Security = OmsCore.SecurityBook.GetSecurity(HedgeUnderlying),
                MinimumTickStyle = MinimumTickStyle,
                Side = side,
                Quantity = absQty,
                Bid = HedgeBid,
                Mid = (HedgeBid + HedgeAsk) / 2,
                Ask = HedgeAsk,
                Ema = 0,
                TotalDelta = 1,
                HanweckTotalTheo = 0,
                DeltaAdjustedTheo = 0,
                UnderBid = HedgeBid,
                UnderAsk = HedgeAsk,
                SubType = ZeroPlus.Models.Data.Enums.OrderSubType.Hedge,
                Multiplier = 1,
                Venue = GetVenue(InstanceMode),
                AccountAcronym = account,
                PositionEffect = PositionEffect.AUTO,
                NewToCancelTime = cancelDelay,
                Comment = comment,
                Destination = "HedgeLocal",
                PrimaryExchange = details?.PrimaryExchange,
                DigBid = DigBid,
                DigAsk = DigAsk,
                DigBidSize = DigBidSize,
                DigAskSize = DigAskSize,
                WeightedVega = WeightedVega,
                Tag = new TagCodec(trader: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                                   edge: pxDiff,
                                   type: OmsCore.OrderClient.TYPE,
                                   subtype: subType,
                                   tv: 0,
                                   ema: 0,
                                   bid: HedgeBid,
                                   ask: HedgeAsk,
                                   comment: !string.IsNullOrEmpty(comment) ? comment : (IsBasketOrder ? BasketSettings.Uid : ""),
                                   sharedId: SharedId,
                                   sequence: Sequence,
                                   typeId: (ushort)TypeId,
                                   subTypeId: (ushort)ZeroPlus.Models.Data.Enums.SubType.HedgeOpen,
                                   subTypeSequence: SubTypeSequence).Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(comment) ? comment : (IsBasketOrder ? BasketSettings.Uid : ""),
                    Bid = HedgeBid,
                    Ask = HedgeAsk,
                    Theo = 0,
                    Ema = 0,
                    Edge = pxDiff,
                    UnderBid = HedgeBid,
                    UnderAsk = HedgeAsk,
                    OrderSubType = ZeroPlus.Models.Data.Enums.OrderSubType.Hedge,
                    ModuleType = IsLowLatencyHangManager ? ZeroPlus.Models.Data.Enums.ModuleType.ScalpHunter : IsBasketOrder ? ZeroPlus.Models.Data.Enums.ModuleType.Basket : ZeroPlus.Models.Data.Enums.ModuleType.Ticket,
                    SubType = ZeroPlus.Models.Data.Enums.SubType.HedgeOpen,
                    VolaTheo = 0,
                    VolaTheoAdj = 0,
                    SharedId = SharedId,
                    Sequence = Sequence,
                    SubTypeSequence = SubTypeSequence,
                    BidSize = 0,
                    AskSize = 0,
                    DigBid = DigBid,
                    DigAsk = DigAsk,
                    DigBidSize = DigBidSize,
                    DigAskSize = DigAskSize,
                    WeightedVega = WeightedVega,
                    UnderBidSize = (uint)UnderlyingBidSize,
                    UnderAskSize = (uint)UnderlyingAskSize,
                    ResubmitCount = (uint)ResubmitCount,
                    TotalEstimatedResubmit = (uint)TotalEstimatedResubmit,
                    ParentSpreadHash = SpreadHash ?? string.Empty,
                },
            };
            order.SetCancelDelay(cancelDelay);

            return order;
        }

        private string GeHedgeIdentifier()
        {
            return "HEDGE - " + OmsCore.User.Username.ToUpper() + " - " + SpreadId.ToUpper() + ":" + Symbol;
        }

        internal OpsOrderModel BuildSingleLegOrder(bool isContra, TicketLegModel leg, OrderSubType? subType = null, int qty = -1, string comment = null)
        {
            var side = !isContra ? leg.Side : leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
            double price = !isContra ? Price : ContraPrice;

            if (EdgeProjector != null && EdgeProjector.IsThreeWay)
            {
                if ((isContra && SuggestTradingContra) || (!isContra && SuggestTradingMain))
                {
                    comment = EdgeProjector.GetComment();
                }
            }
            if (string.IsNullOrWhiteSpace(comment))
            {
                comment = Tag;
            }

            if (!TrySelectRoute(isContra, lookupOnly: false, out string route, out double cancelDelay))
            {
                throw new RouteSelectionException($"Route selection failed.");
            }

            if (RouteOverride != null)
            {
                RouteOverride = null;
            }

            int prevFills = !isContra ? _smartRouteFilledQty : _contraSmartRouteFilledQty;
            if (qty < 1)
            {
                qty = (isContra && !ContraQtyLocked && ContraQty > 0 ? ContraQty : Lcd) - prevFills;
            }

            if (qty < 1)
            {
                throw new SlimException($"Invalid quantity. Lcd: " + Lcd + ", Prev fills: " + prevFills + ", ContraQtyLocked: " + ContraQtyLocked + ", ContraQty: " + ContraQty + ", Contra: " + isContra);
            }

            subType ??= SubType;
            string module = (subType?.ToSpacedString() ?? "") + ModuleTypeSuffix;

            var tif = GetTif();
            var isStock = leg.Type == Types.STOCK.ToString();
            if (isStock)
            {
                if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
                {
                    tif = route.StartsWith("D") ?
                        TimeInForce.GTX :
                        TimeInForce.ETH;
                }
            }

            var positionEffect = Legs.Any(x => x.PositionEffect == PositionEffect.Close)
                ? PositionEffect.Close
                : PositionEffect.AUTO;

            var account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            OpsOrderModel order = new()
            {
                Symbol = leg.Symbol,
                Qty = qty,
                OMSSide = side?.ToString().ToUpper(),
                OpenClose = leg.Position,
                Price = price,
                Account = account,
                TimeInForce = tif,
                Route = route,
                OMSOrderType = "LIMIT",
                Timestamp = DateTime.Now,
                UnderlyingSymbol = Underlying,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,

                BaseStrategy = BaseStrategy,
                Currency = Currency,
                SpreadId = SpreadId,
                Security = OmsCore.SecurityBook.GetSecurity(leg.Symbol),
                Side = side,
                MinimumTickStyle = MinimumTickStyle,
                Quantity = qty,
                Bid = leg.Bid,
                Mid = leg.Mid,
                Ask = leg.Ask,
                Ema = leg.Ema,

                DigBid = leg.DigBid,
                DigAsk = leg.DigAsk,
                DigBidSize = leg.DigBidSize,
                DigAskSize = leg.DigAskSize,
                WeightedVega = leg.WeightedVega,

                TotalDelta = leg.Delta,
                HanweckTotalTheo = leg.Theo,
                DeltaAdjustedTheo = leg.DeltaAdjTheo,
                UnderBid = UnderBid,
                UnderAsk = UnderAsk,
                SubType = IsGammaScalpTicket ? OrderSubType.GammaScalp : subType ?? OrderSubType.Ticket,
                AdjustedEdgeOverride = AdjustedEdgeOverride,
                EdgeOverride = EdgeOverride,
                Multiplier = Multiplier,
                Venue = GetVenue(InstanceMode),
                TagEdge = GetTagEdge(isContra),
                EdgeType = EdgeType,
                AccountAcronym = account,
                PositionEffect = positionEffect,
                NewToCancelTime = cancelDelay,
                Comment = comment,
                Destination = !isContra ? "MainLocal" : "ContraLocal",
                PrimaryExchange = PrimaryExchange,

                IsGTH = IsGTH,

                Tag = new TagCodec(trader: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                                   edge: GetTagEdge(isContra),
                                   type: OmsCore.OrderClient.TYPE,
                                   subtype: module,
                                   tv: leg.DeltaAdjTheo,
                                   ema: leg.Ema,
                                   bid: leg.Bid,
                                   ask: leg.Ask,
                                   comment: (IsBasketOrder ? BasketSettings.Uid : ""),
                                   sharedId: 0,
                                   sequence: 0,
                                   typeId: 0,
                                   subTypeId: 0,
                                   subTypeSequence: 0,
                                   v0: leg.VolaTheoAdjV0,
                                   v1: leg.VolaTheoAdjV1,
                                   v2: leg.VolaTheoAdjV2).Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(comment) ? comment : (IsBasketOrder ? BasketSettings.Uid + (BasketSettings.UseCustomFunctionEdge ? "fn=" + (BasketSettings.CustomFunctionEdgeFormula.Length > 55 ? BasketSettings.CustomFunctionEdgeFormula.Substring(0, 55) : BasketSettings.CustomFunctionEdgeFormula) : "") : ""),
                    Bid = leg.Bid,
                    Ask = leg.Ask,
                    BidSize = (uint)leg.BidSize,
                    AskSize = (uint)leg.AskSize,
                    DigBid = leg.DigBid,
                    DigAsk = leg.DigAsk,
                    DigBidSize = leg.DigBidSize,
                    DigAskSize = leg.DigAskSize,
                    WeightedVega = leg.WeightedVega,
                    Theo = leg.DeltaAdjTheo,
                    Ema = leg.Ema,
                    UnderBid = UnderBid,
                    UnderAsk = UnderAsk,
                    UnderBidSize = (uint)UnderlyingBidSize,
                    UnderAskSize = (uint)UnderlyingAskSize,
                    Edge = GetTagEdge(isContra),
                    EdgeType = EdgeType,
                    OrderSubType = IsGammaScalpTicket ? OrderSubType.GammaScalp : subType ?? OrderSubType.Ticket,
                    ModuleType = IsLowLatencyHangManager ? ModuleType.ScalpHunter : IsBasketOrder ? ModuleType.Basket : ModuleType.Ticket,
                    VolaTheo = leg.VolaTheoV0,
                    VolaTheoAdj = leg.VolaTheoAdjV0,
                    VolaIv = leg.VolaIv,
                    TheoBid = leg.TheoBid,
                    TheoAsk = leg.TheoAsk,
                    SubType = SubTypeId,
                    SharedId = SharedId,
                    Sequence = Sequence,
                    SubTypeSequence = SubTypeSequence,
                    ResubmitCount = (uint)ResubmitCount,
                    TotalEstimatedResubmit = (uint)TotalEstimatedResubmit,
                    ParentSpreadHash = SpreadHash ?? string.Empty,
                },
            };

            if (cancelDelay > 0)
            {
                if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                order.SetCancelDelay(cancelDelay);
            }
            else
            {
                if (TryGetAutoCancel(isContra, order.Route, out var delay))
                {
                    order.SetCancelDelay(delay);
                }
            }

            return order;
        }

        public OpsOrderModel BuildMultiLegOrder(bool isContra, IList<TicketLegModel> validLegs, OrderSubType? subType = null, bool stampValues = true, double overrideTheo = double.NaN, int qty = -1, string comment = null)
        {
            double price;
            if (!isContra)
            {
                price = Price;
            }
            else
            {
                if (TicketStyle == OrderTicketStyle.Combined &&
                    OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical &&
                    !IsBasketOrder)
                {
                    price = -ContraPrice;
                }
                else
                {
                    price = ContraPrice;
                }
            }

            if (EdgeProjector != null && EdgeProjector.IsThreeWay)
            {
                if ((isContra && SuggestTradingContra) || (!isContra && SuggestTradingMain))
                {
                    comment = EdgeProjector.GetComment();
                }
            }
            if (string.IsNullOrWhiteSpace(comment))
            {
                comment = Tag;
            }

            if (!TrySelectRoute(isContra, lookupOnly: false, out string route, out double cancelDelay))
            {
                throw new RouteSelectionException($"Route selection failed.");
            }

            if (RouteOverride != null)
            {
                RouteOverride = null;
            }

            int prevFills = !isContra ? _smartRouteFilledQty : _contraSmartRouteFilledQty;
            if (qty < 1)
            {
                qty = (isContra && !ContraQtyLocked && ContraQty > 0 ? ContraQty : Lcd) - prevFills;
            }

            if (qty < 1)
            {
                throw new SlimException($"Invalid quantity. Lcd: " + Lcd + ", Prev fills: " + prevFills + ", ContraQtyLocked: " + ContraQtyLocked + ", ContraQty: " + ContraQty + ", Contra: " + isContra);
            }

            subType ??= SubType;
            string module = (subType?.ToSpacedString() ?? "") + ModuleTypeSuffix;

            var positionEffect = Legs.Any(x => x.PositionEffect == PositionEffect.Close)
                ? PositionEffect.Close
                : PositionEffect.AUTO;

            var account = AccountLocked ? OmsCore.Config.DefaultAccount : Account;
            OpsComplexOrderModel order = new()
            {
                Account = account,
                Symbol = !isContra ? SpreadSymbol : ContraSpreadSymbol,
                TimeInForce = GetTif(),
                Price = price,
                Qty = qty,
                Route = route,
                OMSOrderType = "MLEG",
                Timestamp = DateTime.Now,
                UnderlyingSymbol = Underlying,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,

                BaseStrategy = BaseStrategy,
                Currency = Currency,
                SpreadId = SpreadId,
                Security = OmsCore.SecurityBook.GetSecurity(Underlying),
                MinimumTickStyle = MinimumTickStyle,
                Quantity = qty,
                UnderBid = UnderBid,
                UnderAsk = UnderAsk,
                SubType = IsGammaScalpTicket ? OrderSubType.GammaScalp : subType ?? OrderSubType.Ticket,
                AdjustedEdgeOverride = AdjustedEdgeOverride,
                EdgeOverride = EdgeOverride,
                Multiplier = Multiplier,
                TagEdge = GetTagEdge(isContra),
                EdgeType = EdgeType,
                AccountAcronym = account,
                PositionEffect = positionEffect,
                Venue = GetVenue(InstanceMode),
                Comment = comment,
                Destination = !isContra ? "MainLocal" : "ContraLocal",
                PrimaryExchange = PrimaryExchange,

                IsGTH = IsGTH,

                Tag = new TagCodec(trader: IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                                   edge: GetTagEdge(isContra),
                                   type: OmsCore.OrderClient.TYPE,
                                   subtype: module,
                                   tv: stampValues ? isContra ? -NetDeltaAdjTheo : NetDeltaAdjTheo : overrideTheo,
                                   ema: stampValues ? isContra ? -GetEma() : GetEma() : double.NaN,
                                   bid: stampValues ? isContra ? -High : Low : double.NaN,
                                   ask: stampValues ? isContra ? -Low : High : double.NaN,
                                   comment: (IsBasketOrder ? BasketSettings.Uid : ""),
                                   sharedId: 0,
                                   sequence: 0,
                                   typeId: 0,
                                   subTypeId: 0,
                                   subTypeSequence: 0,
                                   v0: stampValues ? isContra ? -VolaTheoAdjV0 : VolaTheoAdjV0 : double.NaN,
                                   v1: stampValues ? isContra ? -VolaTheoAdjV1 : VolaTheoAdjV1 : double.NaN,
                                   v2: stampValues ? isContra ? -VolaTheoAdjV2 : VolaTheoAdjV2 : double.NaN).Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = IsLowLatencyHangManager ? Username : OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(comment) ? comment : (IsBasketOrder ? BasketSettings.Uid + (BasketSettings.UseCustomFunctionEdge ? "fn=" + (BasketSettings.CustomFunctionEdgeFormula.Length > 55 ? BasketSettings.CustomFunctionEdgeFormula.Substring(0, 55) : BasketSettings.CustomFunctionEdgeFormula) : "") : ""),
                    Bid = stampValues ? isContra ? -High : Low : double.NaN,
                    Ask = stampValues ? isContra ? -Low : High : double.NaN,

                    DigBid = stampValues ? isContra ? -DigBid : DigBid : double.NaN,
                    DigAsk = stampValues ? isContra ? -DigAsk : DigAsk : double.NaN,
                    DigBidSize = stampValues ? DigBidSize : 0,
                    DigAskSize = stampValues ? DigAskSize : 0,
                    WeightedVega = stampValues ? isContra ? -WeightedVega : WeightedVega : double.NaN,

                    BidSize = stampValues ? isContra ? (uint)AskSize : (uint)BidSize : 0,
                    AskSize = stampValues ? isContra ? (uint)BidSize : (uint)AskSize : 0,
                    Theo = stampValues ? isContra ? -NetDeltaAdjTheo : NetDeltaAdjTheo : overrideTheo,
                    Ema = stampValues ? isContra ? -GetEma() : GetEma() : double.NaN,
                    UnderBid = UnderBid,
                    UnderAsk = UnderAsk,
                    UnderBidSize = (uint)UnderlyingBidSize,
                    UnderAskSize = (uint)UnderlyingAskSize,
                    Edge = GetTagEdge(isContra),
                    EdgeType = EdgeType,
                    OrderSubType = IsGammaScalpTicket ? OrderSubType.GammaScalp : subType ?? OrderSubType.Ticket,
                    ModuleType = IsLowLatencyHangManager ? ZeroPlus.Models.Data.Enums.ModuleType.ScalpHunter : IsBasketOrder ? ZeroPlus.Models.Data.Enums.ModuleType.Basket : ZeroPlus.Models.Data.Enums.ModuleType.Ticket,
                    VolaTheo = stampValues ? isContra ? -VolaTheoV0 : VolaTheoV0 : double.NaN,
                    VolaTheoAdj = stampValues ? isContra ? -VolaTheoAdjV0 : VolaTheoAdjV0 : double.NaN,
                    VolaIv = stampValues ? isContra ? -VolaIv : VolaIv : double.NaN,
                    TheoBid = stampValues ? isContra ? -TheoBid : TheoBid : double.NaN,
                    TheoAsk = stampValues ? isContra ? -TheoAsk : TheoAsk : double.NaN,
                    SubType = SubTypeId,
                    SharedId = SharedId,
                    Sequence = Sequence,
                    SubTypeSequence = SubTypeSequence,
                    ResubmitCount = (uint)ResubmitCount,
                    TotalEstimatedResubmit = (uint)TotalEstimatedResubmit,
                    ParentSpreadHash = SpreadHash ?? string.Empty,
                },
            };

            if (!isContra)
            {
                order.Side = ((IOrder)this).Side;
                order.Bid = Bid;
                order.Mid = Mid;
                order.Ask = Ask;
                order.Ema = Ema;

                order.DigBid = DigBid;
                order.DigAsk = DigAsk;
                order.DigBidSize = DigBidSize;
                order.DigAskSize = DigAskSize;
                order.WeightedVega = WeightedVega;

                order.TotalDelta = TotalDelta;
                order.HanweckTotalTheo = HanweckTotalTheo;
                order.DeltaAdjustedTheo = DeltaAdjustedTheo;
            }
            else
            {
                order.Side = ((IOrder)this).Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                order.Bid = -Ask;
                order.Mid = -Mid;
                order.Ask = -Bid;
                order.Ema = -Ema;

                order.DigBid = -DigAsk;
                order.DigAsk = -DigBid;
                order.DigBidSize = DigBidSize;
                order.DigAskSize = DigAskSize;
                order.WeightedVega = -WeightedVega;

                order.TotalDelta = -TotalDelta;
                order.HanweckTotalTheo = -HanweckTotalTheo;
                order.DeltaAdjustedTheo = -DeltaAdjustedTheo;
            }

            for (var i = 0; i < validLegs.Count; i++)
            {
                var leg = validLegs[i];
                var side = !isContra ? leg.Side :
                    leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell :
                    ZeroPlus.Models.Data.Enums.Side.Buy;

                OMSOrderLeg omsOrderLeg = new()
                {
                    Symbol = leg.Symbol,
                    Quantity = leg.Ratio * qty,
                    Ratio = leg.Ratio,
                    PositionEffect = leg.Position,
                    BuySell = side == ZeroPlus.Models.Data.Enums.Side.Buy
                        ? Comms.Models.Data.Trading.Side.Buy
                        : Comms.Models.Data.Trading.Side.Sell,
                };
                order.AddOrderLeg(omsOrderLeg);

                var complexOrderLeg = new ComplexOrderLeg(OmsCore.SecurityBook)
                {
                    Ratio = leg.Ratio,
                    Quantity = leg.Ratio * qty,
                    Delta = leg.Delta,
                    TV = leg.TV,
                    Ask = leg.Ask,
                    Bid = leg.Bid,
                    AveragePrice = leg.AveragePrice,
                    HanweckTV = leg.HanweckTV,
                    HanweckGamma = leg.HanweckGamma,
                    HanweckVega = leg.HanweckVega,
                    HanweckTheta = leg.HanweckTheta,
                    HanweckRho = leg.HanweckRho,
                    HanweckIV = leg.HanweckIV,
                    HanweckUnder = leg.HanweckUnder,
                    HanweckUnderBid = leg.HanweckUnderBid,
                    HanweckUnderAsk = leg.HanweckUnderAsk,
                    HanweckBid = leg.HanweckBid,
                    HanweckAsk = leg.HanweckAsk,
                    Ema = leg.Ema,
                    LegID = order.LocalID + i,
                    HanweckBidTime = leg.HanweckBidTime,
                    HanweckAskTime = leg.HanweckAskTime,
                    HanweckTimestamp = leg.HanweckTimestamp,
                    Side = !isContra
                        ? leg.Side
                        : leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? ZeroPlus.Models.Data.Enums.Side.Sell
                            : ZeroPlus.Models.Data.Enums.Side.Buy,
                    PositionEffect = leg.PositionEffect,
                    DeltaAdjustedTheo = leg.DeltaAdjustedTheo,
                    BidSize = leg.BidSize,
                    AskSize = leg.AskSize,
                    Symbol = leg.Symbol,
                };
                order.Legs.Add(complexOrderLeg);
            }

            if (cancelDelay > 0)
            {
                if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                order.SetCancelDelay(cancelDelay);
            }
            else
            {
                if (TryGetAutoCancel(isContra, order.Route, out var delay))
                {
                    order.SetCancelDelay(delay);
                }
            }

            return order;
        }

        public static Venue? GetVenue(InstanceMode instanceMode)
        {
            switch (instanceMode)
            {
                case InstanceMode.AT_TB:
                case InstanceMode.OPS_TB:
                    return ZeroPlus.Models.Data.Enums.Venue.TB;
                case InstanceMode.AT_SILEXX:
                case InstanceMode.OPS_SILEXX:
                    return ZeroPlus.Models.Data.Enums.Venue.Silexx;
                case InstanceMode.AT_ZPFIX:
                case InstanceMode.OPS_ZPFIX:
                    return ZeroPlus.Models.Data.Enums.Venue.ZpFix;
            }

            return default;
        }

        private SubType GetSubType(bool isContra)
        {
            switch (SubTypeId)
            {
                case ZeroPlus.Models.Data.Enums.SubType.FishOpen when !isContra:
                    return ZeroPlus.Models.Data.Enums.SubType.FishOpen;
                case ZeroPlus.Models.Data.Enums.SubType.FishOpen when isContra:
                    return ZeroPlus.Models.Data.Enums.SubType.FishClose;
                case ZeroPlus.Models.Data.Enums.SubType.LoopOpen when !isContra:
                    return ZeroPlus.Models.Data.Enums.SubType.LoopOpen;
                case ZeroPlus.Models.Data.Enums.SubType.LoopOpen when isContra:
                    return ZeroPlus.Models.Data.Enums.SubType.LoopClose;
            }

            return ZeroPlus.Models.Data.Enums.SubType.None;
        }

        protected virtual TimeInForce GetTif()
        {
            return this.TimeInForce;
        }

        public double GetTagEdge(bool isContra)
        {
            double edge;
            if (!isContra)
            {
                if (IsBasketOrder)
                {
                    if (BasketTraderViewModel.IsEdgeScanFeedAutoTrader)
                    {
                        edge = CloseEdgeOveride;
                    }
                    else
                    {
                        edge = Edge;
                    }
                }
                else
                {
                    edge = double.NaN;
                }
            }
            else
            {
                edge = GetClosingEdge(false);
            }

            return edge;
        }

        private string GetLog(bool isContra = false)
        {
            string summary = "";

            if (OmsCore.Config.EnableEmbeddedLogging)
            {
                summary += "#" + TotalVolume + "#" + FirmTotalVolume + "#" +
                           (isContra ? -BestBidInt : BestBidInt) + "#" + (isContra ? -BestAskInt : BestAskInt) + "#" +
                           (isContra ? -MktMkrBid : MktMkrBid) + "#" + (isContra ? -MktMkrAsk : MktMkrAsk) + "#" +
                           (int)EdgeType + "#" + Edge;
                summary += "#<!>";
                summary += $"{(double.IsNaN(LogPermAdjPxUnderlyingMid) ? "" : LogPermAdjPxUnderlyingMid.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPxDelta) ? "" : LogPermAdjPxDelta.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogUnderlyingMidAtPermLoad) ? "" : LogUnderlyingMidAtPermLoad.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPxBase) ? "" : LogPermAdjPxBase.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPrice) ? "" : LogPermAdjPrice.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPxMatchingHw) ? "" : LogPermAdjPxMatchingHw.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPxBaseEdge) ? "" : LogPermAdjPxBaseEdge.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjPxOrig) ? "" : LogPermAdjPxOrig.ToString("F2"))}#";
                summary += $"{(double.IsNaN(LogPermAdjDeltaAdjPxOrig) ? "" : LogPermAdjDeltaAdjPxOrig.ToString("F2"))}#";
                summary += "<!>";
            }

            return summary;
        }

        private bool TryGetAutoCancel(bool isContra, string route, out double cancelDelay)
        {
            if (!IsBasketOrder && !isContra && (LockLowPrice || LockMidPrice || LockHighPrice))
            {
                cancelDelay = 0;
                return true;
            }
            if (!IsBasketOrder && isContra && (LockContraLowPrice || LockContraMidPrice || LockContraHighPrice))
            {
                cancelDelay = 0;
                return true;
            }
            if (IsGammaScalpTicket)
            {
                cancelDelay = new Random().Next(AutoCancelIntervalMin, AutoCancelIntervalMax);

                if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                return true;
            }
            if ((!isContra && _usingSmartRoute) || (isContra && _contraUsingSmartRoute))
            {
                cancelDelay = SetCancelTimer(route);
                return true;
            }
            else if (!IsBasketOrder && ((!IsStockTicket && OmsCore.Config.TicketCancelTimerEnabledV2) || (IsStockTicket && OmsCore.Config.StockTicketCancelTimerEnabledV2)))
            {
                switch (AutoCancelMode)
                {
                    case AutoCancelMode.AUTO:
                        cancelDelay = SetCancelTimer(route);
                        break;
                    case AutoCancelMode.MANUAL:
                        cancelDelay = new Random().Next(AutoCancelIntervalMin, AutoCancelIntervalMax);

                        if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                        {
                            cancelDelay = newDelay;
                        }
                        break;
                    case AutoCancelMode.OFF:
                    default:
                        cancelDelay = 0.0;
                        break;
                }

                return true;
            }
            else if (IsBasketOrder)
            {
                cancelDelay = GetBasketCancelTimer(route, isContra);
                return true;
            }

            cancelDelay = 0.0;
            return false;
        }

        private double GetBasketCancelTimer(string route, bool isContra = false)
        {
            AutomationConfigModel automationConfigModel = GetAutomationConfig();
            if (BasketSettings.CancelWithTimerEnabled)
            {
                if ((BasketSettings.FishModeEnabled && !isContra) ||
                    (automationConfigModel.GoFishAutoCloseEnabled && isContra && Closing))
                {
                    return 0.0;
                }
                else
                {
                    double cancelDelay = BasketSettings.CancelWithTimer;

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    return cancelDelay;
                }
            }
            else if (OmsCore.Config.BasketCancelTimerEnabledV2)
            {
                if ((BasketSettings.FishModeEnabled && !isContra) ||
                    (automationConfigModel.GoFishAutoCloseEnabled && isContra && Closing))
                {
                    return 0.0;
                }
                else
                {
                    return SetCancelTimer(route);
                }
            }
            return 0.0;
        }

        private static bool IsNotNickelOrDimePrice(double price)
        {
            if (double.IsNaN(price))
            {
                return false;
            }

            double roundedPrice = Math.Round(price, 2);
            double fac = roundedPrice * 100 % 10;
            return fac != 0 && fac != 5;
        }

        internal bool TrySelectRoute(bool isContra, bool lookupOnly, out string route, out double routeDelay)
        {
            route = default;
            routeDelay = 0;
            string selectedRoute = isContra && !string.IsNullOrWhiteSpace(ContraRoute) ? ContraRoute : Route;

            if (SingleLegStockRoundingDisabled)
            {
                selectedRoute = OmsCore.Config.DefaultSweepRoute(InstanceMode);
            }
            else if (IsBasketOrder)
            {
                AutomationConfigModel automationConfigModel = GetAutomationConfig();

                if (automationConfigModel.LoopingEnabled &&
                    automationConfigModel.LooperDynamicRouting &&
                    (IsActive))
                {
                    if (!isContra && !string.IsNullOrEmpty(LastLoopRoute) && automationConfigModel.EnableDynamicRouteForOpeningOrders)
                    {
                        route = ApplyBrokerPrefix(LastLoopRoute);
                        return true;
                    }
                    else if (isContra && !string.IsNullOrEmpty(LastLoopContraRoute) && automationConfigModel.EnableDynamicRouteForClosingOrders)
                    {
                        route = ApplyBrokerPrefix(LastLoopContraRoute);
                        return true;
                    }
                }
                else
                {
                    if (IsSingleLeg && automationConfigModel.UseSingleLegSeparateLooperRoutes)
                    {
                        if (!isContra && Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSingleLegSize))
                        {
                            selectedRoute = automationConfigModel.LooperOpenRouteSingleLegSize;
                        }
                        else if (!isContra && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSingleLeg))
                        {
                            selectedRoute = automationConfigModel.LooperOpenRouteSingleLeg;
                        }
                        else if (isContra && Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperCloseRouteSingleLegSize))
                        {
                            selectedRoute = automationConfigModel.LooperCloseRouteSingleLegSize;
                        }
                        else if (isContra && !string.IsNullOrEmpty(automationConfigModel.LooperCloseRouteSingleLeg))
                        {
                            selectedRoute = automationConfigModel.LooperCloseRouteSingleLeg;
                        }
                    }
                    else
                    {
                        if (!isContra && Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRouteSize))
                        {
                            selectedRoute = automationConfigModel.LooperOpenRouteSize;
                        }
                        else if (!isContra && !string.IsNullOrEmpty(automationConfigModel.LooperOpenRoute))
                        {
                            selectedRoute = automationConfigModel.LooperOpenRoute;
                        }
                        else if (isContra && Lcd > 1 && !string.IsNullOrEmpty(automationConfigModel.LooperCloseRouteSize))
                        {
                            selectedRoute = automationConfigModel.LooperCloseRouteSize;
                        }
                        else if (isContra && !string.IsNullOrEmpty(automationConfigModel.LooperCloseRoute))
                        {
                            selectedRoute = automationConfigModel.LooperCloseRoute;
                        }
                    }

                    if (!isContra && !string.IsNullOrEmpty(RouteOverride))
                    {
                        selectedRoute = RouteOverride;
                    }
                }
            }

            if (!lookupOnly)
            {
                if (_usingSmartRoute)
                {
                    var ok = SelectNextSmartRoute(ref route, ref routeDelay, selectedRoute, isContra);
                    route = ApplyBrokerPrefix(route);
                    return ok;
                }
                else
                {
                    if (OmsCore.Config.SmartRoutes.ContainsKey(selectedRoute))
                    {
                        if (!isContra)
                        {
                            _smartRouteOverwatchTimer.Stop();
                            _usingSmartRoute = true;
                            _smartRouteFilledQty = 0;
                            _smartRouteIndex = -1;
                            _smartRouteOverwatchTimer.Start();
                        }
                        else
                        {
                            _contraSmartRouteOverwatchTimer.Stop();
                            _contraUsingSmartRoute = true;
                            _contraSmartRouteFilledQty = 0;
                            _contraSmartRouteIndex = -1;
                            _contraSmartRouteOverwatchTimer.Start();
                        }
                        var ok = SelectNextSmartRoute(ref route, ref routeDelay, selectedRoute, isContra);
                        route = ApplyBrokerPrefix(route);
                        return ok;
                    }
                    else
                    {
                        route = ApplyBrokerPrefix(selectedRoute);
                        return true;
                    }
                }
            }
            else
            {
                route = ApplyBrokerPrefix(selectedRoute);
                return true;
            }

            bool SelectNextSmartRoute(ref string route, ref double routeDelay, string selectedRoute, bool isContra)
            {
                if (!string.IsNullOrWhiteSpace(selectedRoute) && OmsCore.Config.SmartRoutes.TryGetValue(selectedRoute, out var smartRoutes))
                {
                    int index = !isContra ? ++_smartRouteIndex : ++_contraSmartRouteIndex;
                    bool found = smartRoutes.TryGetValue(index, out Tuple<string, double> routeFound);
                    if (found)
                    {
                        route = routeFound.Item1;
                        routeDelay = routeFound.Item2;
                    }
                    return found;
                }
                else
                {
                    return false;
                }
            }
        }

        private void SmartRouteOverwatchTimer_Ellapsed(object sender, ElapsedEventArgs e)
        {
            if (MainResting)
            {
                CancelResting();

                _log.Info("Auto cancel triggered by smart route overwatch." +
                      ", Spread: " + SpreadId +
                      ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
            }
        }

        private void ContraSmartRouteOverwatchTimer_Ellapsed(object sender, ElapsedEventArgs e)
        {
            if (ContraResting)
            {
                CancelContra();
            }
        }

        protected double SetCancelTimer(string route)
        {
            string underlying = Underlying?.ToUpper();
            route = route?.ToUpper();
            Tuple<string, string, double, double> lookup;
            if (OmsCore.Config.UnderAndRouteToCancelIntervalMap.TryGetValue(Tuple.Create(underlying, route), out lookup))
            {
                double interval = !IsSingleLeg ? lookup.Item3 : lookup.Item4;
                if (interval >= 0)
                {
                    double cancelDelay = interval;

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    return cancelDelay;
                }
            }

            if (OmsCore.Config.UnderToCancelIntervalMap.TryGetValue(underlying, out lookup))
            {
                double interval = !IsSingleLeg ? lookup.Item3 : lookup.Item4;
                if (interval >= 0)
                {
                    double cancelDelay = interval;

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    return cancelDelay;
                }
            }

            if (OmsCore.Config.RouteToCancelIntervalMap.TryGetValue(route, out lookup))
            {
                double interval = !IsSingleLeg ? lookup.Item3 : lookup.Item4;
                if (interval >= 0)
                {
                    double cancelDelay = interval;

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    return cancelDelay;
                }
            }

            if ((!IsBasketOrder && ((!IsStockTicket && OmsCore.Config.TicketCancelTimerEnabledV2) || (IsStockTicket && OmsCore.Config.StockTicketCancelTimerEnabledV2))) ||
                (IsBasketOrder && OmsCore.Config.BasketCancelTimerEnabledV2))
            {
                var defaultInterval = IsSingleLeg ? OmsCore.Config.SingleLegCancelTimerDefaultIntervalV2 : OmsCore.Config.SpreadCancelTimerDefaultIntervalV2;
                if (defaultInterval >= 0)
                {
                    double cancelDelay = defaultInterval;

                    if (!IsValidCancelDelay(cancelDelay, out double newDelay))
                    {
                        cancelDelay = newDelay;
                    }

                    return cancelDelay;
                }
            }

            return 0;
        }

        protected void UnsubscribeDataAsync()
        {
            Task.Run(() => UnsubscribeData());
        }

        private void UnsubscribeData()
        {
            _lastTraderPositionUpdate = null;
            _portfolioManagerModel?.UnsubscribeAllAsync(this);
            _lastFirmPositionUpdate = null;
            OmsCore.QuoteClient.UnsubscribeAllAsync(this);
            OmsCore.UpdateManager.UnsubscribeAllAsync(this);
            OmsCore.GreekClient.UnsubscribeAllAsync(this);
        }

        protected void SubscribeDataAsync()
        {
            if (!IsBasketOrder || BasketSettings.SubscribeToUnderlying)
            {
                Task.Run(() => SubscribeData());
            }
        }

        internal void SubscribeData()
        {
            if (!IsIbTicket)
            {
                if (!string.IsNullOrWhiteSpace(Underlying))
                {
                    OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.LastPrice, this);
                    OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Ask, this);

                    if (!IsBasketOrder)
                    {
                        OmsCore.QuoteClient.GetSnapshotAsync(Underlying, SubscriptionFieldType.PreviousClose)
                            .ContinueWith(t =>
                            {
                                UnderlyingClosing = t.Result;
                                UnderlyingClosingInitialized = true;
                            });
                    }
                }
            }
        }

        protected void SetSpreadSymbol()
        {
            try
            {
                if (string.IsNullOrEmpty(SpreadSymbol))
                {
                    string tosSymbol = "";
                    for (int i = 0; i < Legs.Count; i++)
                    {
                        TicketLegModel leg = Legs[i];
                        if (leg.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            tosSymbol += "-";
                        }
                        else
                        {
                            if (i != 0)
                            {
                                tosSymbol += "+";
                            }
                        }

                        if (leg.Quantity > 1)
                        {
                            tosSymbol += leg.Quantity + "*";
                        }
                        tosSymbol += leg.Symbol;
                    }

                    SymbolLib.SymbolCodec symbolCodec = new(tosSymbol);
                    symbolCodec.Normalize();
                    SpreadSymbol = symbolCodec.ToTOS();

                    symbolCodec.Invert();
                    symbolCodec.Normalize();
                    ContraSpreadSymbol = symbolCodec.ToTOS();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetSpreadSymbol));
            }
        }

        protected void SubscribeToLegUpdates()
        {
            foreach (TicketLegModel leg in Legs)
            {
                leg.LegUpdatedEvent -= UpdateTicketValues;
                leg.LegUpdatedEvent += UpdateTicketValues;
            }
        }

        internal bool UpdateDescription()
        {
            bool reversed = false;
            List<TicketLegModel> legs = Legs.ToList();
            if (legs == null || legs.Count == 0)
            {
                return reversed;
            }

            var firstLeg = legs[0];
            if (legs.Count == 1)
            {
                Symbol = firstLeg.Symbol;
                Side = firstLeg.Side;
            }

            EvaluationResult result = EvaluateStrategy();
            CheckForReset(result);

            _riskModel = OmsCore.Config.GetRiskModel(Underlying);

            DerivativeLoaded = Underlying != null &&
                               OmsCore.Config.DerivedValueConfigModelLookup.TryGetValue(Underlying, out DerivedValueConfigModel derivedModel) &&
                               derivedModel.LoadDerivatives &&
                               derivedModel.Multiplier != 0;

            CanReplace = false;
            CanReplaceContra = false;

            if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
            {
                ResetPriceAndContraPrice();
            }
            else
            {
                ResetPriceLock();
                ResetContraPriceLock();
            }

            _ = SetPriceIncrementAsync();
            SetMultiplier();
            UpdateFeesEstimate();
            TraderSpreadPosition = 0;
            SpreadPosition = 0;
            SpreadRawPosition = 0;
            LcdPosition = 0;
            HedgeAttempt = 0;
            SubmittedStocks = 0;
            TotalStocks = 0;
            CanHedge = false;
            HedgedStocks = 0;
            RequiredStocks = 0;
            StockHedgeQty = 0;
            StockHedgeStatus = "";
            StockHedgeStatusMode = StatusMode.Reset;
            PositionNetDelta = double.NaN;
            HedgeNetDelta = double.NaN;
            PositionNetWeightedVega = double.NaN;
            ReversePnl = 0;
            ReverseSpreadPosition = 0;
            HedgeReversePnl = 0;
            HedgeReversePosition = 0;

            SingleOrderTicketStopLossValue = 0;
            SingleOrderTicketTrailingStopValue = 0;
            SingleOrderTicketPosition = 0;
            SingleOrderTicketWorkingPosition = 0;
            _bidAtFillForSingleTickets = 0;
            _askAtFillForSingleTickets = 0;

            AdjustedPnl = double.NaN;
            UnrealizedPnl = double.NaN;
            AvgCost = double.NaN;
            OpenPositionAveragePrice = double.NaN;
            HardSide = null;
            FirmLastTrader = "";
            FirmLastEdge = double.NaN;
            FirmLastBuyEdge = double.NaN;
            FirmLastSellEdge = double.NaN;
            FirmLastBuyOrderEdgeToTheo = double.NaN;
            FirmLastSellOrderEdgeToTheo = double.NaN;
            FirmLastFillBuyEdgeToTheo = double.NaN;
            FirmLastFillSellEdgeToTheo = double.NaN;
            FirmLastBuyAttemptEdgeToTheo = double.NaN;
            FirmLastSellAttemptEdgeToTheo = double.NaN;
            GlobalMarketBuyEdgeToTheo = double.NaN;
            GlobalMarketSellEdgeToTheo = double.NaN;
            ResetFirmOrderAndTradeSummaryValues();
            FirmLastBuyAttempt = double.NaN;
            FirmLastBuyAttemptUnderlying = double.NaN;
            FirmLastSellAttempt = double.NaN;
            FirmLastSellAttemptUnderlying = double.NaN;
            LastPermBuyFillEdgeToTheo = double.NaN;
            LastPermSellFillEdgeToTheo = double.NaN;
            LastPermBuyAttemptEdgeToTheo = double.NaN;
            LastPermSellAttemptEdgeToTheo = double.NaN;
            FirmLastTradeSide = null;
            FirmLastTradeTime = default;
            BestBuyEdgeToTheo = double.NaN;
            WorstBuyEdgeToTheo = double.NaN;
            BestSellEdgeToTheo = double.NaN;
            WorstSellEdgeToTheo = double.NaN;
            StockHedgeOpenPositionAveragePrice = double.NaN;
            TraderAdjustedPnl = double.NaN;
            SpreadPositionInitialized = false;
            TraderSpreadPositionInitialized = false;
            Description = result.Description;
            SpreadId = result.GeneralDescription;
            SpreadType = result.BaseType;
            BaseStrategy = SpreadType != null ?
                ZeroPlus.Models.Utils.OptionStrategy.ConvertFromString(SpreadType) :
                BaseStrategy.INVALID;
            SpreadPermId = this.GetHardSideKey()?.ToString();

            if (Legs.Count > 1 && Legs.Count(x => x.IsValid && x.SecurityType == SecurityType.Stock) == 1)
            {
                _calcLegs = Legs.Where(x => x.SecurityType == SecurityType.Option).ToList();
                StockLeg = Legs.FirstOrDefault(x => x.SecurityType == SecurityType.Stock);
                IsStockTied = true;
            }
            else
            {
                _calcLegs = Legs.ToList();
                StockLeg = null;
                IsStockTied = false;
            }

            if (TicketStyle == OrderTicketStyle.Dual)
            {
                DualDescription = result.GeneralDescription?.Replace("CALL", "")?.Replace("PUT", "")?.Trim();
            }
            SetTicketType();

            Symbol = GetTosFromLegs(legs);
            LegsCount = Math.Max(legs.Count, 1);
            SetDaysToExp();
            Contracts = legs.Sum(x => Math.Abs(x.Ratio));
            MinStrike = legs.Where(x => x.Security?.SecurityType == Data.Securities.SecurityType.Option).Select(x => x.Security.Strike).DefaultIfEmpty(0d).Min();
            SetSides();
            if (IsSingleLeg)
            {
                Security = OmsCore.SecurityBook.GetSecurity(firstLeg.Symbol);
                if (BaseStrategy == BaseStrategy.STOCK && Underlying != null && firstLeg?.Symbol != null)
                {
                    var details = OmsCore.QuoteClient.GetUnderlyingDetails(Underlying);
                    PrimaryExchange = details?.PrimaryExchange;
                }
            }
            else
            {
                Security = OmsCore.SecurityBook.GetSecurity(UnderlyingSymbol);
            }
            if (ConformBuySide)
            {
                if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    Reverse();
                    reversed = true;
                    return reversed;
                }
            }
            reversed = UpdateTicketSide();
            CanThreeWay = CanCreateThreeWay();
            UpdateHedgeSymbol();
            SetSpreadSymbol();
            _ = SetPriceIncrementAsync();
            SubscribeToPositions();
            SetStrikeSpacing();
            SubscribeToGlobalEdgeToTheo();
            SubscribeToFirmOrderAndTradeSummary();
            OnDescriptionUpdated();
            SubscribeToIbData();
            return reversed;
        }

        protected virtual void OnDescriptionUpdated()
        {
        }

        private protected void SubscribeToUnderlying()
        {
            if (CanSubscribeToUnderlying)
            {
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Ask, this);
            }
        }

        private protected void UnsubscribeUnderlying()
        {
            OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.LastPrice, this);
            OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.MidPoint, this);
            OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Bid, this);
            OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Ask, this);
            Last = double.NaN;
            UnderMid = double.NaN;
            UnderBid = double.NaN;
            UnderAsk = double.NaN;
        }

        private protected void SubscribeToHedgeUnderlying()
        {
            if (CanSubscribeToHedge)
            {
                OmsCore.QuoteClient.Subscribe(HedgeUnderlying, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(HedgeUnderlying, SubscriptionFieldType.Ask, this);
            }
        }

        private protected void UnsubscribeFromHedgeUnderlying()
        {
            OmsCore.QuoteClient.Unsubscribe(HedgeUnderlying, SubscriptionFieldType.Bid, this);
            OmsCore.QuoteClient.Unsubscribe(HedgeUnderlying, SubscriptionFieldType.Ask, this);
            HedgeBid = double.NaN;
            HedgeAsk = double.NaN;
        }

        private protected void SubscribeToGlobalEdgeToTheo()
        {
            if (CanSubscribeToGlobalEdgeToTheo)
            {
                OmsCore.UpdateManager.Subscribe(SpreadSymbol, SubscriptionFieldType.TradeEdgeToTheo, this);
                OmsCore.UpdateManager.Subscribe(ContraSpreadSymbol, SubscriptionFieldType.TradeEdgeToTheo, this);

                if (IsBasketOrder && IsSingleLeg && !IsStockTicket)
                {
                    var leg = Legs.FirstOrDefault();
                    if (leg != null)
                    {
                        var key =
                            (Underlying + leg.ExpirationInfo.Expiration.ToString("yyMMdd") + leg.Type[0]).ToUpper();
                        OmsCore.UpdateManager.Subscribe(key, SubscriptionFieldType.PermEdgeToTheo, this);
                    }
                }
            }
        }

        private protected void UnsubscribeFromGlobalEdgeToTheo()
        {
            OmsCore.UpdateManager.Unsubscribe(SpreadSymbol, SubscriptionFieldType.TradeEdgeToTheo, this);
            OmsCore.UpdateManager.Unsubscribe(ContraSpreadSymbol, SubscriptionFieldType.TradeEdgeToTheo, this);

            if (IsBasketOrder && IsSingleLeg && !IsStockTicket)
            {
                var leg = Legs.FirstOrDefault();
                if (leg != null)
                {
                    var key =
                        (Underlying + leg.ExpirationInfo.Expiration.ToString("yyMMdd") + leg.Type[0]).ToUpper();
                    OmsCore.UpdateManager.Unsubscribe(key, SubscriptionFieldType.PermEdgeToTheo, this);
                }
            }
            GlobalMarketBuyEdgeToTheo = double.NaN;
            GlobalMarketSellEdgeToTheo = double.NaN;
        }

        private protected void SubscribeToFirmOrderAndTradeSummary()
        {
            if (CanSubscribeToFirmSummary)
            {
                OmsCore.HerculesClientWrapper.Subscribe(SpreadId, SubscriptionFieldType.FirmOrderAndTradeSummary, this);
            }
        }

        private protected void UnsubscribeFirmOrderAndTradeSummary()
        {
            OmsCore.HerculesClientWrapper.Unsubscribe(SpreadId, SubscriptionFieldType.FirmOrderAndTradeSummary, this);
            ResetFirmOrderAndTradeSummaryValues();
        }

        private void ResetFirmOrderAndTradeSummaryValues()
        {
            BuyLastAttemptPx = double.NaN;
            BuyLastAttemptUnderPx = double.NaN;
            BuyLastAttemptTime = default;
            BuyLastFillPx = double.NaN;
            BuyLastFillUnderPx = double.NaN;
            BuyLastFillTime = default;
            BuyLowestAttemptedEdgeToTheo = double.NaN;
            BuyHighestFilledEdgeToTheo = double.NaN;
            SellLastAttemptPx = double.NaN;
            SellLastAttemptUnderPx = double.NaN;
            SellLastAttemptTime = default;
            SellLastFillPx = double.NaN;
            SellLastFillUnderPx = double.NaN;
            SellLastFillTime = default;
            SellLowestAttemptedEdgeToTheo = double.NaN;
            SellHighestFilledEdgeToTheo = double.NaN;
        }

        private protected void SubscribeToLegField(string source)
        {
            foreach (TicketLegModel leg in Legs)
            {
                leg.SubscribeToDataFeed(source);
            }
        }

        private protected void UnsubscribeFromLegField(string source)
        {
            foreach (TicketLegModel leg in Legs)
            {
                leg.UnsubscribeFromDataSource(source);
            }
        }

        private void CheckForReset(EvaluationResult result)
        {
            try
            {
                _portfolioManagerModel?.UnsubscribeAll(this);
                OmsCore.QuoteClient.UnsubscribeAll(SubscriptionFieldType.TronTrade, this);
                if (BasketTraderViewModel != null && BasketTraderViewModel.IsEdgeScanFeedAutoTrader)
                {
                    OmsCore.UpdateManager.Unsubscribe(SpreadId, SubscriptionFieldType.TradeUpdate, this);
                }
                if ((!string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(SpreadType) && Description != result.Description) ||
                    (!string.IsNullOrWhiteSpace(SpreadType) && SpreadType != result.BaseType) ||
                    (!string.IsNullOrWhiteSpace(SpreadId) && SpreadId != result.GeneralDescription))
                {
                    if (!(double.IsNaN(Price) && double.IsNaN(ContraPrice)))
                    {
                        ResetTicket();
                    }
                    if (IsStockTicket)
                    {
                        SetBestRoute();
                    }
                }

            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateDescription));
            }
        }

        private void SetTicketType()
        {
            try
            {
                if (string.IsNullOrEmpty(SpreadId))
                {
                    PutCall = PutCall.Unknown;
                }
                else
                {
                    string type = Legs.FirstOrDefault()?.Type;
                    PutCall = type switch
                    {
                        null => PutCall.Unknown,
                        "CALL" => PutCall.Call,
                        "PUT" => PutCall.Put,
                        _ => PutCall.Unknown,
                    };
                }

                IsIbTicket = Underlying != null && Underlying.Contains('\\');
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetTicketType));
                PutCall = PutCall.Unknown;
            }
        }

        private void SetStrikeSpacing()
        {
            try
            {
                int count = Legs.Count;
                Leg1 = null;
                Leg2 = null;
                Leg3 = null;
                Leg4 = null;
                if (IsBasketOrder)
                {
                    switch (count)
                    {
                        case > 3:
                            Leg1 = Legs[0];
                            Leg2 = Legs[1];
                            Leg3 = Legs[2];
                            Leg4 = Legs[3];
                            break;
                        case 3:
                            Leg1 = Legs[0];
                            Leg2 = Legs[1];
                            Leg3 = Legs[2];
                            break;
                        case 2:
                            Leg1 = Legs[0];
                            Leg2 = Legs[1];
                            break;
                        case 1:
                            Leg1 = Legs[0];
                            break;
                    }
                }

                double[] strikes = Legs.Where(x => x.IsStrikeValid).Select(x => x.Strike.Strike).Distinct().OrderBy(x => x).ToArray();

                if (strikes.Length == 2)
                {
                    StrikeSpacing = Math.Abs(strikes[0] - strikes[1]);
                }
                else if (strikes.Length == 3)
                {
                    StrikeSpacing = Math.Max(Math.Abs(strikes[0] - strikes[1]), Math.Abs(strikes[1] - strikes[2]));
                }
                else
                {
                    StrikeSpacing = 0;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetStrikeSpacing));
                StrikeSpacing = double.NaN;
            }
        }

        private void SetDaysToExp()
        {
            try
            {
                if (Legs != null &&
                    Legs.Count > 0 &&
                    Legs.All(x => x.IsValid &&
                    x.Type != "STOCK" &&
                    x.ExpirationInfo != null))
                {
                    DaysToExpiration = (int)(Legs.Max(x => x.ExpirationInfo.Expiration.Date) - DateTime.Today).TotalDays;
                }
                else
                {
                    DaysToExpiration = 0;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetDaysToExp));
                DaysToExpiration = 0;
            }
        }

        private void SubscribeToPositions()
        {
            if (!string.IsNullOrWhiteSpace(SpreadId))
            {

                _portfolioManagerModel?.Subscribe(SpreadId, SubscriptionFieldType.FirmSpreadPosition, this);
                _portfolioManagerModel?.Subscribe(SpreadId.ToUpper(), SubscriptionFieldType.UserSpreadPosition, this);
                _portfolioManagerModel?.Subscribe("HEDGE - " + OmsCore.User.Username.ToUpper() + " - " + SpreadId.ToUpper(), SubscriptionFieldType.UserInstancePosition, this);

                if (OmsCore.Config.SubscribeToHardSideIdentificationOnTickets)
                {
                    if (!_subscribedToHardSide)
                    {
                        _subscribedToHardSide = true;
                        var hardSideKey = this.GetHardSideKey();
                        if (hardSideKey != null)
                        {
                            _portfolioManagerModel?.Subscribe(hardSideKey.ToString(), SubscriptionFieldType.HardSide, this);
                        }
                    }
                }

                if (!IsBasketOrder && TicketStyle == OrderTicketStyle.Complex && ShowTimeAndSales)
                {
                    foreach (TicketLegModel leg in Legs.ToList())
                    {
                        if (leg.IsValid)
                        {
                            OmsCore.QuoteClient.Subscribe(leg.Symbol, SubscriptionFieldType.TronTrade, this);
                        }
                    }
                }

                if (IsBasketOrder && BasketTraderViewModel != null && BasketTraderViewModel.IsEdgeScanFeedAutoTrader)
                {
                    OmsCore.UpdateManager.Subscribe(SpreadId, SubscriptionFieldType.TradeUpdate, this);
                }
            }
            else
            {
                _log.Warn("Spread ID not set.");
            }
        }

        protected EvaluationResult EvaluateStrategy()
        {
            OptionStrategy.EvaluateOrder(this, out var baseStrategy, out var spreadType, out var description);
            EvaluationResult result = new EvaluationResult()
            {
                BaseType = baseStrategy,
                Description = description,
                GeneralDescription = spreadType,
            };
            return result;
        }

        private async void LoadAdjustedEdgeSummaryAsync()
        {
            try
            {
                if (IsBasketOrder || SpreadId == null || SpreadId == "" || SpreadId.StartsWith("INVALID"))
                {
                    return;
                }
                string symbol = SpreadId;
                double mins = OmsCore.Config.AdjustedEdgeSummaryLookback;
                double percentFromUnder = OmsCore.Config.AdjustedEdgeSummaryUnderPercentage;
                _log.Info("Start Request For Edge Summary. " +
                          "Symbol: " + symbol + ", " +
                          "Mins: " + mins + ", " +
                          "Percent: " + percentFromUnder + ".");
                Stopwatch globalStopwatch = Stopwatch.StartNew();
                List<TicketLegModel> legs = Legs.ToList();
                if (legs.Count > 0)
                {
                    MDUnderlying underlyingDetails = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(Underlying);
                    if (underlyingDetails == null)
                    {
                        _log.Error("Loading underlying info failed. Symbol: " + Underlying);
                        return;
                    }

                    await WaitForUnderMidLoadAsync();
                    await WaitForAdjTheoLoadAsync();
                    double midPrice = UnderMid;

                    _log.Info("Price Calculated For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Mins: " + mins + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    Dictionary<DateTime, DataPointModel> snapTimeToDataPointMap = new();
                    DateTime targetStartDay = DateTime.Today;
                    switch (targetStartDay.DayOfWeek)
                    {
                        case DayOfWeek.Saturday:
                            targetStartDay -= TimeSpan.FromDays(1);
                            break;
                        case DayOfWeek.Sunday:
                            targetStartDay -= TimeSpan.FromDays(2);
                            break;
                    }

                    DateTime marketClose = targetStartDay.Date + TimeSpan.FromHours(15);
                    if (Underlying.StartsWith("$"))
                    {
                        marketClose += TimeSpan.FromMinutes(15);
                    }

                    DateTime endDateTime = marketClose > DateTime.Now ? DateTime.Now : marketClose;
                    DateTime startDateTime = endDateTime - TimeSpan.FromMinutes(mins);

#if DEBUG
                    endDateTime += TimeSpan.FromDays(1);
#endif

                    _log.Info("Time Range Selected For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + midPrice + ", " +
                              "Percent: " + percentFromUnder + ".");

                    int totalSamples = 0;
                    DateTime now = DateTime.Now;
                    for (int i = 0; i < legs.Count; i++)
                    {
                        TicketLegModel leg = legs[i];

                        if (!leg.IsValid || leg.ExpirationInfo == null)
                        {
                            _log.Info("Invalid Leg For Edge Summary. " +
                                      "Symbol: " + symbol + ", " +
                                      "Leg-" + (i + 1) + ": " + leg.Symbol + ", " +
                                      "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                      "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                      "Price: " + midPrice + ", " +
                                      "Percent: " + percentFromUnder + ".");

                            continue;
                        }

                        int ratio = leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? leg.Ratio : -leg.Ratio;
                        Stopwatch stopwatch = Stopwatch.StartNew();
                        List<ZeroPlus.Models.Data.Responses.OptionSnapshot> results = await OmsCore.EdgeScannerClient.RequestOptionSnapshotsAsync(leg.Symbol, leg.ExpirationInfo.Expiration, default, startDateTime, endDateTime);
                        stopwatch.Stop();
                        _log.Info("Response Received For Edge Summary. " +
                                  "Symbol: " + symbol + ", " +
                                  "Leg-" + (i + 1) + ": " + leg.Symbol + ", " +
                                  "Snapshots Found: " + results?.Count + ", " +
                                  "Elapsed: " + stopwatch.ElapsedMilliseconds + "ms, " +
                                  "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                  "Price: " + midPrice + ", " +
                                  "Percent: " + percentFromUnder + ".");

                        if (results != null)
                        {
                            totalSamples = Math.Max(totalSamples, results.Count);

                            double theo = leg.DeltaAdjTheo;

                            for (int j = 0; j < results.Count; j++)
                            {
                                ZeroPlus.Models.Data.Responses.OptionSnapshot result = results[j];
                                _log.Debug("Result Snapshot For Edge Summary. " +
                                            "Symbol: " + symbol + ", " +
                                            "Leg-" + (i + 1) + ": " + result.Symbol + ", " +
                                            "Update: " + (j + 1) + ", " +
                                            "Bid: " + result.Bid + ", " +
                                            "Ask: " + result.Ask + ", " +
                                            "UnderBid: " + result.UnderBid + ", " +
                                            "UnderAsk: " + result.UnderAsk + ", " +
                                            "AdjTheo: " + result.AdjTheo + ", " +
                                            "Theo: " + result.Theo + ", " +
                                            "Delta: " + result.Delta + ", " +
                                            "Vega: " + result.Vega + ", " +
                                            "Iv: " + result.Iv + ", " +
                                            "QuoteTime: " + result.QuoteTime + ", " +
                                            "SnapshotTime: " + result.SnapshotTime + ", " +
                                            "HanweckCalcTime: " + result.HanweckCalcTime + ", " +
                                            "AdjTheoTime: " + result.AdjTheoTime + ", " +
                                            "UnderMid: " + result.UnderMid + ", " +
                                            "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ".");


                                double resultMid = (result.UnderAsk + result.UnderBid) / 2;
                                double percentageDifference = Math.Abs((midPrice - resultMid) / ((midPrice + resultMid) / 2));
                                if (percentageDifference > percentFromUnder)
                                {
                                    _log.Info("Skipping Result Snapshot By Price For Edge Summary. " +
                                              "Symbol: " + symbol + ", " +
                                              "Leg-" + (i + 1) + ": " + leg.Symbol + ", " +
                                              "Snapshot Mid: " + resultMid + ", " +
                                              "Snapshot Time: " + result.SnapshotTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                                              "Price: " + midPrice + ", " +
                                              "Percent: " + percentFromUnder + ".");
                                    continue;
                                }

                                if (!snapTimeToDataPointMap.TryGetValue(result.SnapshotTime, out DataPointModel dataPoint))
                                {
                                    dataPoint = new DataPointModel()
                                    {
                                        Timestamp = result.SnapshotTime,
                                    };
                                    snapTimeToDataPointMap[result.SnapshotTime] = dataPoint;
                                }
                                PricingParameters pricingParameters = new()
                                {
                                    Volatility = 0.0,
                                    PutCall = leg.Type == Types.PUT.ToString() ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                    Strike = leg.Strike.Strike,
                                    DaysToExpiration = (leg.ExpirationInfo.Expiration - result.SnapshotTime).TotalDays,
                                    RiskFreeRate = underlyingDetails.RiskFreeRate,
                                    StockRate = underlyingDetails.StockRate,
                                    UnderlyingPrice = resultMid,
                                    UnderlyingMultiplier = underlyingDetails.Multiplier,
                                    ExerciseStyle = Underlying.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                                };
                                pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapshotTime);
                                Greeks greeks = new();
                                dataPoint.UnderPx = resultMid;

                                double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                                pricingParameters.Volatility = bidIv;
                                double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                bidPrice += (midPrice - resultMid) * greeks.Delta;
                                double adjTheo = result.AdjTheo + ((midPrice - resultMid) * greeks.Delta);

                                if (theo < adjTheo)
                                {
                                    double change = adjTheo - theo;
                                    bidPrice -= change;
                                }

                                dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                                double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Theo, greeks);
                                pricingParameters.Volatility = hwIv;
                                double hwTheo = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                hwTheo += (midPrice - resultMid) * greeks.Delta;
                                dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwTheo, result);

                                double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                                pricingParameters.Volatility = askIv;
                                double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                                askPrice += (midPrice - resultMid) * greeks.Delta;

                                if (theo > adjTheo)
                                {
                                    double change = theo - adjTheo;
                                    askPrice += change;
                                }

                                dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                            }
                        }
                    }
                    List<DateTime> times = snapTimeToDataPointMap.Keys.OrderBy(x => x).ToList();
                    List<DataPointModel> dataPoints = snapTimeToDataPointMap.Values
                        .Where(chartDataPoint => chartDataPoint.TryRecalculate(legs.Count))
                        .OrderByDescending(x => x.Timestamp)
                        .ToList();

                    DataPointModel highestBid = dataPoints.MaxBy(x => x.BidIv);
                    DataPointModel lowestAsk = dataPoints.MinBy(x => x.AskIv);
                    if (highestBid != null && lowestAsk != null)
                    {
                        _adjEdgeSummaryBidBase = highestBid.BidIv;
                        _adjEdgeSummaryAskBase = lowestAsk.AskIv;
                        _adjEdgeSummaryUnderMidAtLoad = midPrice;
                        _adjEdgeSummaryLoaded = true;

                        DeltaAdjEdgeSummary();
                    }

                    globalStopwatch.Stop();
                    _log.Info("Valid Datapoints For Edge Summary. " +
                              "Symbol: " + symbol + ", " +
                              "Datapoints: " + dataPoints.Count + ", " +
                              "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                              "Start: " + startDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "End: " + endDateTime.ToString("dd-MMM-yy hh:mm:ss") + ", " +
                              "Price: " + Math.Round(midPrice, 2) + ", " +
                              "Percent: " + percentFromUnder + ".");
                }
                else
                {
                    globalStopwatch.Stop();
                    _log.Info("Request For Edge Summary Failed. " +
                              "No valid legs. " +
                              "Symbol: " + symbol + ", " +
                              "TotalElapsed: " + globalStopwatch.ElapsedMilliseconds + "ms, " +
                              "Mins: " + mins + ", " +
                              "Percent: " + percentFromUnder + ".");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadAdjustedEdgeSummaryAsync));
            }
        }

        protected bool CanCreateThreeWay()
        {
            try
            {
                return !(Legs.Count < 2 ||
                         !Legs.Any(x => x.Ratio == 1) ||
                         Legs.Any(x => x.ExpirationInfo == null) ||
                         string.IsNullOrEmpty(Description) ||
                         Description.Contains("INVALID") ||
                         Description.Contains("CUSTOM"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected void UpdateLCD()
        {
            lock (_legUpdateLock)
            {
                List<int> qtyList = Legs.Where(x => x.IsValid).ToList().Select(x => x.Quantity).ToList();
                int divisor = 1;
                if (qtyList.Count > 0)
                {
                    List<int> lcdAdjustedList = Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out divisor);
                    if (!RatioLocked)
                    {

                        for (int index = 0; index < qtyList.Count; ++index)
                        {
                            Legs[index].UpdateRatio(lcdAdjustedList[index]);
                        }
                    }
                }

                Lcd = divisor;

                Qty = Lcd;

                if (ContraQtyLocked)
                {
                    ContraQty = Qty;
                }
            }
            UpdateSummary();
        }

        protected void UpdateTicketValues()
        {
            try
            {
                if (IsDisposed || Legs.Count == 0)
                {
                    return;
                }

                TicketValues update = new();
                lock (_legUpdateLock)
                {
                    CalculateUpdates(update, _calcLegs);
                }

                if (update.HasError)
                {
                    return;
                }

                double prevMid = Mid;
                double prevNetTheo = NetTheo;
                double prevAdjTheo = NetDeltaAdjTheo;
                UpdateUiSafe(update);
                MarketUpdated(Low, High);
                MidUpdated(prevMid, update.Mid);
                TheoUpdated(prevNetTheo, update.NetTheo);
                DeltaAdjTheoUpdated(prevAdjTheo, update.NetDeltaAdjTheo);
                PostUpdateNotificationsAsync();
                UpdateRisk(_underMid);
                CheckForExits();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateTicketValues));
            }
        }

        private void CheckForExits()
        {
            if (!IsBasketOrder)
            {
                if ((TicketStyle == OrderTicketStyle.Single || TicketStyle == OrderTicketStyle.Dual) && IsSingleLeg)
                {
                    lock (PositionUpdateLock)
                    {
                        if (SingleOrderTicketTrailingStopEnabled && SingleOrderTicketTrailingStopValue != 0 && !double.IsNaN(SingleOrderTicketTrailingStopValue))
                        {
                            CheckForTrailingStop();
                        }
                        else if (SingleOrderTicketStopLossEnabled && SingleOrderTicketStopLossValue != 0 && !double.IsNaN(SingleOrderTicketStopLossValue))
                        {
                            CheckForStoploss();
                        }
                    }
                }
                else if (TicketStyle == OrderTicketStyle.Combined)
                {
                    if (StopLossEnabled)
                    {
                        int qty = Math.Abs(StopLossQty);
                        if (qty > 0)
                        {
                            lock (_stopLossLock)
                            {
                                if (StopLossEnabled)
                                {
                                    if ((High < StopLossTriggerPrice && StopLossSide == ZeroPlus.Models.Data.Enums.Side.Sell) ||
                                        (Low > StopLossTriggerPrice && StopLossSide == ZeroPlus.Models.Data.Enums.Side.Buy))
                                    {
                                        StopOrderManager.Start(StopLossSide, qty);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (TicketStyle == OrderTicketStyle.Complex)
                {
                    if (AutoCloseToggled && AutoCloseArmed && AutoCloseManager.CanSend && AutoCloseConfigModel != null)
                    {
                        if (TraderSpreadPosition != 0 && Math.Abs(LcdPosition) == Math.Abs(TraderSpreadPosition))
                        {
                            double profitPercentage = double.NaN;
                            if (Side == ZeroPlus.Models.Data.Enums.Side.Buy && TraderSpreadPosition > 0)
                            {
                                profitPercentage = (Mid - AveragePrice) / AveragePrice;
                            }
                            else if (Side == ZeroPlus.Models.Data.Enums.Side.Sell && TraderSpreadPosition < 0)
                            {
                                profitPercentage = (AveragePrice - Mid) / AveragePrice;
                            }
                            if (!double.IsNaN(profitPercentage))
                            {
                                AutoCloseConfigTierModel model = AutoCloseConfigModel.AutoCloseConfigTiers.Where(x => x.ProfitPercentage <= profitPercentage).OrderByDescending(x => x.ProfitPercentage).FirstOrDefault();
                                if (model != null)
                                {
                                    double closeEdge = Math.Abs(AveragePrice * model.ProfitPercentage);
                                    double closePrice;
                                    if (IsSingleLeg)
                                    {
                                        if (Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                                        {
                                            closePrice = AveragePrice + closeEdge;
                                        }
                                        else
                                        {
                                            closePrice = AveragePrice - closeEdge;
                                        }
                                    }
                                    else
                                    {
                                        closePrice = (AveragePrice * -1.0) - closeEdge;
                                    }
                                    int pos = Math.Abs(TraderSpreadPosition);
                                    int qty = (int)Math.Min(Math.Ceiling(pos * model.PositionPercentage), pos);
                                    lock (_stopLossLock)
                                    {
                                        if (AutoCloseManager.CanSend)
                                        {
                                            AutoCloseManager.InitiateExit(closePrice, qty);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (Tracker.IsRunning)
                    {
                        Tracker.CheckForExit();
                    }
                }
            }
            else
            {
                if (CloseStyle == Enums.CloseStyle.SweepTrade)
                {
                    SweepCloser?.CheckForExit();
                }
            }
        }

        private void CheckForTrailingStop()
        {
            if ((SingleOrderTicketPosition < 0 && SingleOrderTicketTrailingStopValue <= Ask) ||
                (SingleOrderTicketPosition > 0 && SingleOrderTicketTrailingStopValue >= Bid))
            {
                if (SingleOrderTicketWorkingPosition == 0)
                {
                    StoplossCloseAllPositions();
                }
            }
            else
            {
                if (SingleOrderTicketPosition > 0 && SingleOrderTicketWorkingPosition == 0)
                {
                    double changeInBid = Bid - _bidAtFillForSingleTickets;
                    if (Bid <= SingleOrderTicketTrailingStopValue)
                    {
                        StoplossCloseAllPositions();
                    }
                    else if (changeInBid >= 0.01)
                    {
                        _bidAtFillForSingleTickets = Bid;
                        SingleOrderTicketTrailingStopValue += changeInBid;
                    }
                }
                else if (SingleOrderTicketPosition < 0 && SingleOrderTicketWorkingPosition == 0)
                {
                    double changeInAsk = _askAtFillForSingleTickets - Ask;
                    if (Ask >= SingleOrderTicketTrailingStopValue)
                    {
                        StoplossCloseAllPositions();
                    }
                    else if (changeInAsk >= 0.01)
                    {
                        _askAtFillForSingleTickets = Ask;
                        SingleOrderTicketTrailingStopValue -= changeInAsk;
                    }
                }
            }
        }

        private void CheckForStoploss()
        {
            if ((SingleOrderTicketPosition < 0 && SingleOrderTicketStopLossValue <= Ask) ||
                (SingleOrderTicketPosition > 0 && SingleOrderTicketStopLossValue >= Bid))
            {
                if (SingleOrderTicketWorkingPosition == 0)
                {
                    StoplossCloseAllPositions();
                }
            }
        }

        public void StoplossCloseAllPositions()
        {
            try
            {
                if (!double.IsNaN(Mid) && !IsActive)
                {
                    int spreadPosition = _spreadPosition;

                    if (spreadPosition == 0)
                    {
                        return;
                    }

                    double closeButtonPxIncrement = OmsCore.Config.CloseButtonPxIncrement;
                    double closeButtonInterval = OmsCore.Config.CloseButtonInterval;
                    int maxCount = OmsCore.Config.StopLossMaxAttempt;

                    if (OmsCore.Config.TicketStopLossLookupMap.TryGetValue(Underlying, out Tuple<string, double, double, int> model))
                    {
                        closeButtonInterval = model.Item2;
                        closeButtonPxIncrement = model.Item3;
                        maxCount = model.Item4;
                    }

                    if (StopLossAttemptCounter++ >= maxCount)
                    {
                        _log.Info("Max stop loss attempt reached");
                        return;
                    }

                    StopLossManager.InitiateExit(maxCount, closeButtonInterval, closeButtonPxIncrement, bidPercent: 1);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StoplossCloseAllPositions));
                ShowMessage(ex.Message, "Close Positions");
            }
        }

        private void UpdateRisk(double mid)
        {
            if (ShowRiskRunPanel)
            {
                int count = PositionsRisk.Count;
                Dictionary<int, int> map = new()
                {
                    [0] = -3,
                    [1] = -2,
                    [2] = -1,
                    [3] = 0,
                    [4] = 1,
                    [5] = 2,
                    [6] = 3,
                };

                MDUnderlying underlyingDetails = OmsCore.QuoteClient.GetUnderlyingDetails(Underlying);
                if (underlyingDetails != null)
                {
                    for (int i = 0; i < count; i++)
                    {
                        double newMid = mid + map[i];
                        PositionsRisk[i].UnderlyingPrice = newMid;

                        double netDelta = 0.0;
                        double netGamma = 0.0;
                        double netTheta = 0.0;
                        for (int j = 0; j < Legs.Count; j++)
                        {
                            TicketLegModel position = Legs[j];
                            Greeks greek = position.UpdateGreeks(underlyingDetails, newMid);
                            int quantity = position.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? position.Quantity : -position.Quantity;
                            netDelta += greek.Delta * quantity * position.Multiplier;
                            netGamma += greek.Gamma * quantity * position.Multiplier;
                            netTheta += greek.Theta * quantity * position.Multiplier;
                        }
                        PositionsRisk[i].NetDelta = netDelta;
                        PositionsRisk[i].NetGamma = netGamma;
                        PositionsRisk[i].NetTheta = netTheta;
                    }
                }
            }
        }

        private void UpdateUiSafe(TicketValues update)
        {
            try
            {
                UpdateUi(update);
            }
            catch (InvalidOperationException)
            {
                Dispatcher?.Invoke(new Action(() =>
                {
                    UpdateUi(update);
                }));
                _log.Info($"{nameof(UpdateTicketValues)} -> Update values failed using dispatcher instead." +
                      ", Server Creep: " + BasketTraderViewModel?.ServerCreep);
                if (IsBasketOrder)
                {
                    ShowMessage("UI update delay detected. Restart this instance if you keep receiving this message.", "");
                }
            }
        }

        private void UpdateLcdPosition()
        {
            try
            {
                if (!IsBasketOrder && (TicketStyle is OrderTicketStyle.Combined or OrderTicketStyle.Complex or OrderTicketStyle.GammaScalp))
                {
                    ObservableCollection<TicketLegModel> legs = Legs;
                    if (legs.Any())
                    {
                        if (IsSingleLeg)
                        {
                            LcdPosition = legs.First().NetQty;
                        }
                        else
                        {
                            if ((legs.Count(x => (x.NetQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.NetQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == legs.Count) ||
                                (legs.Count(x => (x.NetQty > 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (x.NetQty < 0 && x.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) == legs.Count))
                            {
                                int divisor = legs.Min(x => (int)Math.Ceiling(Math.Abs((double)x.NetQty / x.Ratio)));
                                TicketLegModel sample = legs.First();
                                LcdPosition = ((sample.NetQty < 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Buy) || (sample.NetQty > 0 && sample.Side == ZeroPlus.Models.Data.Enums.Side.Sell)) ^ Side == ZeroPlus.Models.Data.Enums.Side.Sell
                                        ? -divisor
                                        : divisor;
                            }
                            else
                            {
                                LcdPosition = 0;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                LcdPosition = 0;
            }
        }

        private void PostUpdateNotificationsAsync()
        {
            NotifyEventWaiters();
            UpdateSummary();

            if (!Closing && !AutoCancelRunning && !string.IsNullOrEmpty(OrderId) && !OrderIsClosed)
            {
                Task.Run(() => CheckForAutoCancel());
            }
        }

        private void NotifyEventWaiters()
        {
            var theo = NetTheo;
            var adjTheo = NetDeltaAdjTheo;
            if (!double.IsNaN(theo) && !NetTheoLoaded)
            {
                NetTheoLoaded = true;
            }

            if ((!double.IsNaN(BestEdgeBid) || double.IsNaN(BestEdgeAsk)) && !NetHistoricBestLoaded)
            {
                NetHistoricBestLoaded = true;
            }

            if (!double.IsNaN(WeightedVega) && !WeightedVegaLoaded)
            {
                WeightedVegaLoaded = true;
            }

            if (!double.IsNaN(adjTheo) && !NetAdjTheoLoaded)
            {
                NetAdjTheoLoaded = true;
                DeltaAdjTheoLoaded();
            }

            if (!double.IsNaN(TotalDelta) && !TotalDeltaLoaded)
            {
                TotalDeltaLoaded = true;
            }

            if (!double.IsNaN(Low) && !LowLoaded)
            {
                LowLoaded = true;
            }

            if (!double.IsNaN(LowestBid) && !LowestBidLoaded)
            {
                LowestBidLoaded = true;
            }

            if (!double.IsNaN(HighestOffer) && !HighestOfferLoaded)
            {
                HighestOfferLoaded = true;
            }

            if (!double.IsNaN(HighestBid) && !double.IsNaN(LowestAsk) && !HighestBidLowestAskLoaded)
            {
                HighestBidLowestAskLoaded = true;
            }

            if (!double.IsNaN(High) && !HighLoaded)
            {
                HighLoaded = true;
            }

            if (!double.IsNaN(Mid) && !MarkLoaded)
            {
                MarkLoaded = true;
                SizeLoaded = true;
            }

            if (!double.IsNaN(BestAskInt) && !double.IsNaN(BestBidInt) && !BestMarkLoaded)
            {
                BestMarkLoaded = true;
            }

            if (!double.IsNaN(GetEma()) && !EmaLoaded)
            {
                EmaLoaded = true;
            }

            if (!double.IsNaN(BidEmaAdj) && !double.IsNaN(AskEmaAdj) && !AdjEmaLoaded)
            {
                AdjEmaLoaded = true;
            }

            if (!double.IsNaN(BidIvEma) && !BidEmaLoaded)
            {
                BidEmaLoaded = true;
            }

            if (!double.IsNaN(AskIvEma) && !AskEmaLoaded)
            {
                AskEmaLoaded = true;
            }

            if (!double.IsNaN(DigBid) && !double.IsNaN(DigAsk) && !DigLoaded)
            {
                DigLoaded = true;
            }

            SignalDataLoadWaiters();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SignalDataLoadWaiters()
        {
            var prev = Interlocked.Exchange(
                ref _dataLoadNotification,
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
            prev.TrySetResult(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TicketValues CalculateUpdates(TicketValues ticketValues, List<TicketLegModel> legs, bool addStockLeg = true)
        {
            int count = legs?.Count ?? 0;
            if (count == 0)
            {
                ticketValues.HasError = true;
                return ticketValues;
            }

            // cache config access to avoid static lookups in loop
            var config = OmsCore.Config;
            bool useSkew = config.UseSkewAdjustedHiBidLoAskForSpreadCalc;
            bool priceEvalReversed = config.PriceEvaluationStyle == PriceEvaluationStyle.Reversed;
            bool priceEvalIdentical = config.PriceEvaluationStyle == PriceEvaluationStyle.Identical;
            bool calculateEma = IsBasketOrder && BasketSettings.SubscribeToEma;

            double lcd = Lcd;
            double multiplier = Multiplier;
            double finalScaleFactor = 1.0 / (multiplier * lcd);

            // Initialize accumulators
            bool isCheapo = false;
            bool isSingleLeg = IsSingleLeg;

            // Using simple primitives (stack allocated)
            double totalBestLow = 0.0, totalBestHigh = 0.0;
            double totalBestEdgeLow = 0.0, totalBestEdgeHigh = 0.0, totalBestEdgeMid = 0.0;
            double totalLow = 0.0, totalHigh = 0.0, totalMid = 0.0;
            double totalLowInt = 0.0, totalHighInt = 0.0, totalMidInt = 0.0;
            double totalBestBidInt = 0.0, totalBestAskInt = 0.0, totalBestMidInt = 0.0;
            double totalMktMkrBid = 0.0, totalMktMkrAsk = 0.0;
            double totalTheoBid = 0.0, totalTheoAsk = 0.0;
            double totalDigBid = 0.0, totalDigAsk = 0.0;
            double totalHighestBid = 0.0, totalLowestAsk = 0.0;
            double totalLowDerived = 0.0, totalHighDerived = 0.0, totalMidDerived = 0.0;
            double totalLowIntDerived = 0.0, totalHighIntDerived = 0.0, totalMidIntDerived = 0.0;

            // EMA accumulators
            double totalBidEma = 0.0, totalBidEmaAdj = 0.0, totalAskEma = 0.0, totalAskEmaAdj = 0.0;
            double totalAdjEma = 0.0, totalFullEma = 0.0, totalEma = 0.0;
            double totalBidIvEma = 0.0, totalAskIvEma = 0.0, totalMidIvEma = 0.0;

            double underEma = double.NaN;

            // Synchronization flags
            bool adjTheoSeqSync = true;
            bool volaAdjTheoSyncV1 = true, volaAdjTheoSyncV2 = true, volaAdjTheoSyncV3 = true;
            bool testValueSync = true, emaSeqSync = true;
            bool theoJumpDetected = false;

            // Size integers
            int bidSize = int.MaxValue;
            int askSize = int.MaxValue;
            uint digBidSize = uint.MaxValue;
            uint digAskSize = uint.MaxValue;

            // First leg reference for synch checks
            var firstLeg = legs[0];

            // OPTIMIZATION: Check validity of first leg manually to avoid LINQ
            if (!firstLeg.IsValid)
            {
                ticketValues.HasError = true;
                return ticketValues;
            }

            uint deltaAdjTheoSequence = firstLeg.DeltaAdjTheoSequence;
            uint volaAdjTheoSeqV1 = firstLeg.VolaTheoSequenceV1;
            uint volaAdjTheoSeqV2 = firstLeg.VolaTheoSequenceV2;
            uint volaAdjTheoSeqV3 = firstLeg.VolaTheoSequenceV3;
            uint testSequence = firstLeg.TestValueSequence;
            ulong emaSequence = firstLeg.EmaSequence;

            string referenceHanweckTime = firstLeg.HanweckTime;
            bool theoSynched = true;

            ticketValues.VolaPriceMetricV0 = firstLeg.VolaPriceMetricV0;
            ticketValues.VolaPriceMetricV1 = firstLeg.VolaPriceMetricV1;
            ticketValues.VolaPriceMetricV2 = firstLeg.VolaPriceMetricV2;
            ticketValues.VolaPriceMetricV3 = firstLeg.VolaPriceMetricV3;

            if (count == 1)
            {
                ticketValues.LastBidTheoSpread = firstLeg.LastBidTheoSpread;
                ticketValues.LastAskTheoSpread = firstLeg.LastAskTheoSpread;
                ticketValues.BidTheoSpreadEma = firstLeg.BidTheoSpreadEma;
                ticketValues.AskTheoSpreadEma = firstLeg.AskTheoSpreadEma;
            }
            else
            {
                ticketValues.LastBidTheoSpread = double.NaN;
                ticketValues.LastAskTheoSpread = double.NaN;
                ticketValues.BidTheoSpreadEma = double.NaN;
                ticketValues.AskTheoSpreadEma = double.NaN;
            }

            double edgeToTheoBasePrice = Price;

            // Reset aggregated totals in ticketValues to 0 before accumulation
            ticketValues.OpenInterest = 0; ticketValues.TotalVolume = 0; ticketValues.FirmTotalVolume = 0;
            ticketValues.TotalDelta = 0; ticketValues.TotalGamma = 0; ticketValues.TotalTheta = 0;
            ticketValues.TotalVega = 0; ticketValues.WeightedVega = 0; ticketValues.TotalRho = 0;
            ticketValues.TotalImplied = 0; ticketValues.TotalTheo = 0; ticketValues.TotalDeltaAdjTheo = 0;
            ticketValues.NetTheo = 0; ticketValues.SmoothedDeltaAdjTheo = 0;
            ticketValues.VolaTheoV0 = 0; ticketValues.VolaTheoAdjV0 = 0; ticketValues.AdjDaEma = 0;
            ticketValues.VolaEma = 0; ticketValues.AdjVolaEma = 0; ticketValues.DaEma = 0;
            ticketValues.VolaTheoV1 = 0; ticketValues.VolaTheoAdjV1 = 0;
            ticketValues.VolaTheoV2 = 0; ticketValues.VolaTheoAdjV2 = 0;
            ticketValues.VolaTheoV3 = 0; ticketValues.VolaTheoAdjV3 = 0;
            ticketValues.VolaIv = 0;
            ticketValues.TheoBid = 0;
            ticketValues.TheoAsk = 0;
            ticketValues.DigBid = 0;
            ticketValues.DigAsk = 0;
            ticketValues.DigBidSize = 0;
            ticketValues.DigAskSize = 0;
            ticketValues.LockedTheo = 0; ticketValues.LockedDeltaAdjTheo = 0;
            ticketValues.NetDeltaAdjTheo = 0;
            ticketValues.NetTestValue = 0;
            ticketValues.NetDelta = 0; ticketValues.NetGamma = 0; ticketValues.NetTheta = 0;

            // MAIN HOT LOOP
            for (int i = 0; i < count; i++)
            {
                TicketLegModel leg = legs[i];

                // Validation check (replaced LINQ .Any)
                if (!leg.IsValid)
                {
                    ticketValues.HasError = true;
                    return ticketValues;
                }

                // Time sync check (replaced LINQ .Distinct)
                if (leg.HanweckTime != referenceHanweckTime)
                {
                    theoSynched = false;
                }

                isCheapo |= leg.IsCheapo;
                double qtyAbs = Math.Abs(leg.Quantity);
                double legMultiplier = leg.Multiplier;

                // Cache frequent accessors to registers/stack
                double legBid = leg.Bid;
                double legAsk = leg.Ask;
                int legBidSize = leg.BidSize;
                int legAskSize = leg.AskSize;
                double ratio = leg.Ratio;

                // Pre-calculate products
                double qtyMult = qtyAbs * legMultiplier;
                double bidProd = qtyAbs * legBid * legMultiplier;
                double askProd = qtyAbs * legAsk * legMultiplier;

                // Determine side
                bool legIsStock = leg.SecurityType == SecurityType.Stock;
                int side = (leg.Side == ZeroPlus.Models.Data.Enums.Side.Buy || isSingleLeg) ? 1 : -1;

                // Size Calculation
                if (isSingleLeg)
                {
                    bidSize = legBidSize;
                    askSize = legAskSize;
                    digBidSize = leg.DigBidSize;
                    digAskSize = leg.DigAskSize;
                }
                else
                {
                    double invRatio = 1.0 / ratio;

                    if (side == 1)
                    {
                        int calcBid = (int)Math.Ceiling(Math.Abs(legBidSize * invRatio));
                        int calcAsk = (int)Math.Ceiling(Math.Abs(legAskSize * invRatio));
                        if (calcBid < bidSize)
                        {
                            bidSize = calcBid;
                        }
                        if (calcAsk < askSize)
                        {
                            askSize = calcAsk;
                        }

                        uint calcDigBid = (uint)Math.Ceiling(Math.Abs(leg.DigBidSize * invRatio));
                        uint calcDigAsk = (uint)Math.Ceiling(Math.Abs(leg.DigAskSize * invRatio));
                        if (calcDigBid < digBidSize)
                        {
                            digBidSize = calcDigBid;
                        }
                        if (calcDigAsk < digAskSize)
                        {
                            digAskSize = calcDigAsk;
                        }
                    }
                    else
                    {
                        int calcBid = (int)Math.Ceiling(Math.Abs(legAskSize * invRatio));
                        int calcAsk = (int)Math.Ceiling(Math.Abs(legBidSize * invRatio));
                        if (calcBid < bidSize)
                        {
                            bidSize = calcBid;
                        }
                        if (calcAsk < askSize)
                        {
                            askSize = calcAsk;
                        }

                        uint calcDigBid = (uint)Math.Ceiling(Math.Abs(leg.DigAskSize * invRatio));
                        uint calcDigAsk = (uint)Math.Ceiling(Math.Abs(leg.DigBidSize * invRatio));
                        if (calcDigBid < digBidSize)
                        {
                            digBidSize = calcDigBid;
                        }
                        if (calcDigAsk < digAskSize)
                        {
                            digAskSize = calcDigAsk;
                        }
                    }
                }

                // --- Accumulation Block ---
                // We branch on 'side' once to cover all asymmetric additions
                if (side == 1)
                {
                    totalBestLow += qtyMult * Math.Max(legBid, leg.EmaSpreadBid);
                    totalBestHigh += qtyMult * Math.Min(legAsk, leg.EmaSpreadAsk);

                    totalLow += bidProd;
                    totalHigh += askProd;

                    totalBestEdgeLow += qtyMult * leg.BestBuyPriceAdj;
                    totalBestEdgeHigh += qtyMult * leg.BestSellPriceAdj;

                    totalBidEma += qtyMult * leg.BidEma;
                    totalAskEma += qtyMult * leg.AskEma;

                    totalBidEmaAdj += qtyMult * leg.AdjBidEma;
                    totalAskEmaAdj += qtyMult * leg.AdjAskEma;

                    totalLowInt += qtyMult * leg.BidInterpolated;
                    totalHighInt += qtyMult * leg.AskInterpolated;

                    totalBestBidInt += qtyMult * leg.BestBid;
                    totalBestAskInt += qtyMult * leg.BestAsk;

                    totalMktMkrBid += qtyMult * leg.MktMkrBid;
                    totalMktMkrAsk += qtyMult * leg.MktMkrAsk;
                    totalTheoBid += qtyMult * leg.TheoBid;
                    totalTheoAsk += qtyMult * leg.TheoAsk;
                    totalDigBid += qtyMult * leg.DigBid;
                    totalDigAsk += qtyMult * leg.DigAsk;

                    if (useSkew)
                    {
                        totalHighestBid += qtyMult * leg.SkewAdjustedHighestBid;
                        totalLowestAsk += qtyMult * leg.SkewAdjustedLowestAsk;
                    }
                    else
                    {
                        totalHighestBid += qtyMult * leg.HighestBid;
                        totalLowestAsk += qtyMult * leg.LowestAsk;
                    }

                    totalLowDerived += qtyMult * leg.BidDerived;
                    totalHighDerived += qtyMult * leg.AskDerived;

                    totalLowIntDerived += qtyMult * leg.BidDerivedInterpolated;
                    totalHighIntDerived += qtyMult * leg.AskDerivedInterpolated;
                }
                else
                {
                    totalBestLow -= qtyMult * Math.Min(legAsk, leg.EmaSpreadAsk); // side is -1, so we subtract
                    totalBestHigh -= qtyMult * Math.Max(legBid, leg.EmaSpreadBid);

                    totalLow -= askProd;
                    totalHigh -= bidProd;

                    totalBestEdgeLow -= qtyMult * leg.BestSellPriceAdj;
                    totalBestEdgeHigh -= qtyMult * leg.BestBuyPriceAdj;

                    totalBidEma -= qtyMult * leg.AskEma;
                    totalAskEma -= qtyMult * leg.BidEma;

                    totalBidEmaAdj -= qtyMult * leg.AdjAskEma;
                    totalAskEmaAdj -= qtyMult * leg.AdjBidEma;

                    totalLowInt -= qtyMult * leg.AskInterpolated;
                    totalHighInt -= qtyMult * leg.BidInterpolated;

                    totalBestBidInt -= qtyMult * leg.BestAsk;
                    totalBestAskInt -= qtyMult * leg.BestBid;

                    totalMktMkrBid -= qtyMult * leg.MktMkrAsk;
                    totalMktMkrAsk -= qtyMult * leg.MktMkrBid;
                    totalTheoBid -= qtyMult * leg.TheoAsk;
                    totalTheoAsk -= qtyMult * leg.TheoBid;
                    totalDigBid -= qtyMult * leg.DigAsk;
                    totalDigAsk -= qtyMult * leg.DigBid;

                    if (useSkew)
                    {
                        totalHighestBid -= qtyMult * leg.SkewAdjustedLowestAsk;
                        totalLowestAsk -= qtyMult * leg.SkewAdjustedHighestBid;
                    }
                    else
                    {
                        totalHighestBid -= qtyMult * leg.LowestAsk;
                        totalLowestAsk -= qtyMult * leg.HighestBid;
                    }

                    totalLowDerived -= qtyMult * leg.AskDerived;
                    totalHighDerived -= qtyMult * leg.BidDerived;

                    totalLowIntDerived -= qtyMult * leg.AskDerivedInterpolated;
                    totalHighIntDerived -= qtyMult * leg.BidDerivedInterpolated;
                }

                // Symmetric accumulations
                underEma = leg.UnderEma;
                totalMid += side * ((bidProd + askProd) * 0.5); // Derived from prods
                totalBestEdgeMid += side * qtyMult * ((leg.BestBuyPriceAdj + leg.BestSellPriceAdj) * 0.5);
                totalMidInt += side * qtyMult * ((leg.BidInterpolated + leg.AskInterpolated) * 0.5);
                totalBestMidInt += side * qtyMult * ((leg.BestBid + leg.BestAsk) * 0.5);
                totalMidDerived += side * qtyMult * ((leg.BidDerived + leg.AskDerived) * 0.5);
                totalMidIntDerived += side * qtyMult * ((leg.BidDerivedInterpolated + leg.AskDerivedInterpolated) * 0.5);

                totalAdjEma += side * qtyMult * leg.AdjEma;
                totalFullEma += side * qtyMult * leg.FullEma;
                totalEma += side * qtyMult * leg.Ema;

                if (calculateEma)
                {
                    totalBidIvEma += side * qtyMult * leg.BidIvEma;
                    totalAskIvEma += side * qtyMult * leg.AskIvEma;
                    totalMidIvEma += side * qtyMult * leg.MidIvEma;
                }

                // Greeks & Totals
                // Optimization: Check for stock multiplier outside loop or assume Leg.Ratio is correct for calc
                double greeksRatio = legIsStock ? (ratio / multiplier) : ratio;

                ticketValues.OpenInterest += leg.OpenInterest;
                ticketValues.TotalVolume += leg.Volume;
                ticketValues.FirmTotalVolume += (leg.TradingBuyQty + leg.TradingSellQty);

                // Vectorizable ops (Scalar for now)
                ticketValues.TotalDelta += side * leg.Delta * greeksRatio;
                ticketValues.TotalGamma += side * leg.Gamma * greeksRatio;
                ticketValues.TotalTheta += side * leg.Theta * greeksRatio;
                ticketValues.TotalVega += side * leg.Vega * greeksRatio;
                ticketValues.WeightedVega += side * leg.WeightedVega * greeksRatio;
                ticketValues.TotalRho += side * leg.Rho * greeksRatio;
                ticketValues.TotalImplied += side * leg.Implied * greeksRatio;
                ticketValues.TotalTheo += side * leg.Theo * greeksRatio;
                ticketValues.TotalDeltaAdjTheo += side * leg.DeltaAdjTheo * greeksRatio;

                // Net values use actual multiplier logic
                double netValMult = side * qtyMult;
                ticketValues.NetTheo += netValMult * leg.Theo;
                ticketValues.SmoothedDeltaAdjTheo += netValMult * leg.SmoothedDeltaAdjTheo;
                ticketValues.VolaTheoV0 += netValMult * leg.VolaTheoV0;
                ticketValues.VolaTheoAdjV0 += netValMult * leg.VolaTheoAdjV0;
                ticketValues.VolaIv += netValMult * leg.VolaIv;
                ticketValues.AdjDaEma += netValMult * leg.AdjDaEma;
                ticketValues.VolaEma += netValMult * leg.VolaEma;
                ticketValues.AdjVolaEma += netValMult * leg.AdjVolaEma;
                ticketValues.DaEma += netValMult * leg.DaEma;
                ticketValues.VolaTheoV1 += netValMult * leg.VolaTheoV1;
                ticketValues.VolaTheoAdjV1 += netValMult * leg.VolaTheoAdjV1;
                ticketValues.VolaTheoV2 += netValMult * leg.VolaTheoV2;
                ticketValues.VolaTheoAdjV2 += netValMult * leg.VolaTheoAdjV2;
                ticketValues.VolaTheoV3 += netValMult * leg.VolaTheoV3;
                ticketValues.VolaTheoAdjV3 += netValMult * leg.VolaTheoAdjV3;
                ticketValues.LockedTheo += netValMult * leg.LockedTheo;
                ticketValues.LockedDeltaAdjTheo += netValMult * leg.LockedDeltaAdjTheo;
                ticketValues.NetDeltaAdjTheo += netValMult * leg.DeltaAdjTheo;
                ticketValues.NetTestValue += netValMult * leg.TestValue;

                // Special case: NetDelta calc from original code: (leg.Delta * qtyAbs * multiplier)
                ticketValues.NetDelta += side * leg.Delta * qtyMult;
                ticketValues.NetGamma += leg.NetGamma * multiplier;
                ticketValues.NetTheta += leg.NetTheta * multiplier;

                // Metric Min/Max logic (Manual NaN checks are faster than double.IsNaN call if we know range, but sticking to logic)
                // Inlining the Max check:
                if (double.IsNaN(ticketValues.VolaPriceMetricV0) || ticketValues.VolaPriceMetricV0 > leg.VolaPriceMetricV0)
                {
                    ticketValues.VolaPriceMetricV0 = leg.VolaPriceMetricV0;
                }
                if (double.IsNaN(ticketValues.VolaPriceMetricV1) || ticketValues.VolaPriceMetricV1 > leg.VolaPriceMetricV1)
                {
                    ticketValues.VolaPriceMetricV1 = leg.VolaPriceMetricV1;
                }
                if (double.IsNaN(ticketValues.VolaPriceMetricV2) || ticketValues.VolaPriceMetricV2 > leg.VolaPriceMetricV2)
                {
                    ticketValues.VolaPriceMetricV2 = leg.VolaPriceMetricV2;
                }
                if (double.IsNaN(ticketValues.VolaPriceMetricV3) || ticketValues.VolaPriceMetricV3 > leg.VolaPriceMetricV3)
                {
                    ticketValues.VolaPriceMetricV3 = leg.VolaPriceMetricV3;
                }

                theoJumpDetected |= leg.TheoJumpDetected;

                // Sequence Sync Logic
                if (adjTheoSeqSync && leg.DeltaAdjTheoSequence != deltaAdjTheoSequence)
                {
                    adjTheoSeqSync = false;
                    if (leg.DeltaAdjTheoSequence > deltaAdjTheoSequence)
                    {
                        deltaAdjTheoSequence = leg.DeltaAdjTheoSequence;
                    }
                }
                if (volaAdjTheoSyncV1 && leg.VolaTheoSequenceV1 != volaAdjTheoSeqV1)
                {
                    volaAdjTheoSyncV1 = false;
                    if (leg.VolaTheoSequenceV1 > volaAdjTheoSeqV1)
                    {
                        volaAdjTheoSeqV1 = leg.VolaTheoSequenceV1;
                    }
                }
                if (volaAdjTheoSyncV2 && leg.VolaTheoSequenceV2 != volaAdjTheoSeqV2)
                {
                    volaAdjTheoSyncV2 = false;
                    if (leg.VolaTheoSequenceV2 > volaAdjTheoSeqV2)
                    {
                        volaAdjTheoSeqV2 = leg.VolaTheoSequenceV2;
                    }
                }

                if (volaAdjTheoSyncV3 && leg.VolaTheoSequenceV3 != volaAdjTheoSeqV3)
                {
                    volaAdjTheoSyncV3 = false;
                    if (leg.VolaTheoSequenceV3 > volaAdjTheoSeqV3)
                    {
                        volaAdjTheoSeqV3 = leg.VolaTheoSequenceV3;
                    }

                }
                if (emaSeqSync && leg.EmaSequence != emaSequence)
                {
                    emaSeqSync = false;
                }
            }

            // Stock Leg Logic (Simplified)
            if (IsStockTied && addStockLeg && StockLeg != null)
            {
                var sLeg = StockLeg;
                double sQtyAbs = Math.Abs(sLeg.Quantity);
                double sBid = sQtyAbs * sLeg.Bid * sLeg.Multiplier;
                double sAsk = sQtyAbs * sLeg.Ask * sLeg.Multiplier;
                bool sLegIsStock = sLeg.SecurityType == SecurityType.Stock;
                int sSide = (sLeg.Side == ZeroPlus.Models.Data.Enums.Side.Buy || isSingleLeg) ? 1 : -1;

                if (isSingleLeg)
                {
                    bidSize = sLeg.BidSize;
                    askSize = sLeg.AskSize;
                }

                if (sSide == 1)
                {
                    if (!isSingleLeg)
                    {
                        int cBid = (int)Math.Ceiling(Math.Abs((double)sLeg.BidSize / sLeg.Ratio));
                        int cAsk = (int)Math.Ceiling(Math.Abs((double)sLeg.AskSize / sLeg.Ratio));
                        if (cBid < bidSize)
                        {
                            bidSize = cBid;
                        }
                        if (cAsk < askSize)
                        {
                            askSize = cAsk;
                        }
                    }
                    totalLow += sBid; totalHigh += sAsk;
                    edgeToTheoBasePrice -= sAsk * finalScaleFactor;
                }
                else
                {
                    if (!isSingleLeg)
                    {
                        int cBid = (int)Math.Ceiling(Math.Abs((double)sLeg.AskSize / sLeg.Ratio));
                        int cAsk = (int)Math.Ceiling(Math.Abs((double)sLeg.BidSize / sLeg.Ratio));
                        if (cBid < bidSize)
                        {
                            bidSize = cBid;
                        }
                        if (cAsk < askSize)
                        {
                            askSize = cAsk;
                        }
                    }
                    totalLow += sAsk; totalHigh += sBid;
                    edgeToTheoBasePrice += sBid * finalScaleFactor;
                }
                totalMid += sSide * ((sBid + sAsk) * 0.5);
            }

            ticketValues.BidSize = bidSize != int.MaxValue ? bidSize : 0;
            ticketValues.AskSize = askSize != int.MaxValue ? askSize : 0;
            ticketValues.DigBidSize = digBidSize != uint.MaxValue ? digBidSize : 0;
            ticketValues.DigAskSize = digAskSize != uint.MaxValue ? digAskSize : 0;
            ticketValues.IsCheapo = isCheapo;
            ticketValues.TheoSynched = theoSynched;

            ticketValues.Low = FastRound(totalLow * finalScaleFactor);
            ticketValues.High = FastRound(totalHigh * finalScaleFactor);
            ticketValues.Mid = FastRound(totalMid * finalScaleFactor);
            ticketValues.BestEdgeBid = FastRound(totalBestEdgeLow * finalScaleFactor);
            ticketValues.BestEdgeAsk = FastRound(totalBestEdgeHigh * finalScaleFactor);
            ticketValues.BestEdgeMid = FastRound(totalBestEdgeMid * finalScaleFactor);

            ticketValues.BidEma = FastRound(totalBidEma * finalScaleFactor);
            ticketValues.AskEma = FastRound(totalAskEma * finalScaleFactor);
            ticketValues.BidEmaAdj = FastRound(totalBidEmaAdj * finalScaleFactor);
            ticketValues.AskEmaAdj = FastRound(totalAskEmaAdj * finalScaleFactor);

            ticketValues.BestLow = FastRound(totalBestLow * finalScaleFactor);
            ticketValues.BestHigh = FastRound(totalBestHigh * finalScaleFactor);

            ticketValues.LowInt = FastRound(totalLowInt * finalScaleFactor);
            ticketValues.HighInt = FastRound(totalHighInt * finalScaleFactor);
            ticketValues.MidInt = FastRound(totalMidInt * finalScaleFactor);

            ticketValues.LowDerived = FastRound(totalLowDerived * finalScaleFactor);
            ticketValues.HighDerived = FastRound(totalHighDerived * finalScaleFactor);
            ticketValues.MidDerived = FastRound(totalMidDerived * finalScaleFactor);

            ticketValues.BestBidInt = FastRound(totalBestBidInt * finalScaleFactor);
            ticketValues.BestAskInt = FastRound(totalBestAskInt * finalScaleFactor);
            ticketValues.BestMidInt = FastRound(totalBestMidInt * finalScaleFactor);

            ticketValues.MktMkrBid = FastRound(totalMktMkrBid * finalScaleFactor);
            ticketValues.MktMkrAsk = FastRound(totalMktMkrAsk * finalScaleFactor);

            ticketValues.HighestBid = FastRound(totalHighestBid * finalScaleFactor);
            ticketValues.LowestAsk = FastRound(totalLowestAsk * finalScaleFactor);

            ticketValues.LowIntDerived = FastRound(totalLowIntDerived * finalScaleFactor);
            ticketValues.HighIntDerived = FastRound(totalHighIntDerived * finalScaleFactor);
            ticketValues.MidIntDerived = FastRound(totalMidIntDerived * finalScaleFactor);

            ticketValues.UnderEma = underEma;
            ticketValues.EmaSync = emaSeqSync;
            ticketValues.AdjEma = FastRound(totalAdjEma * finalScaleFactor);
            ticketValues.FullEma = FastRound(totalFullEma * finalScaleFactor);
            ticketValues.Ema = FastRound(totalEma * finalScaleFactor);
            ticketValues.BidIvEma = FastRound(totalBidIvEma * finalScaleFactor);
            ticketValues.AskIvEma = FastRound(totalAskIvEma * finalScaleFactor);

            ticketValues.Width = Math.Abs(ticketValues.Low - ticketValues.High);

            ticketValues.NetTheo = FastRound(ticketValues.NetTheo * finalScaleFactor);
            ticketValues.NetDeltaAdjTheo = FastRound(ticketValues.NetDeltaAdjTheo * finalScaleFactor);
            ticketValues.NetTestValue = FastRound(ticketValues.NetTestValue * finalScaleFactor);
            ticketValues.SmoothedDeltaAdjTheo = FastRound(ticketValues.SmoothedDeltaAdjTheo * finalScaleFactor);
            ticketValues.VolaTheoV0 = FastRound(ticketValues.VolaTheoV0 * finalScaleFactor);
            ticketValues.VolaTheoAdjV0 = FastRound(ticketValues.VolaTheoAdjV0 * finalScaleFactor);
            ticketValues.VolaIv = ticketValues.VolaIv * finalScaleFactor;
            ticketValues.TheoBid = FastRound(totalTheoBid * finalScaleFactor);
            ticketValues.TheoAsk = FastRound(totalTheoAsk * finalScaleFactor);
            ticketValues.DigBid = FastRound(totalDigBid * finalScaleFactor);
            ticketValues.DigAsk = FastRound(totalDigAsk * finalScaleFactor);
            ticketValues.AdjDaEma = FastRound(ticketValues.AdjDaEma * finalScaleFactor);
            ticketValues.VolaEma = FastRound(ticketValues.VolaEma * finalScaleFactor);
            ticketValues.AdjVolaEma = FastRound(ticketValues.AdjVolaEma * finalScaleFactor);
            ticketValues.DaEma = FastRound(ticketValues.DaEma * finalScaleFactor);

            ticketValues.VolaTheoV1 = FastRound(ticketValues.VolaTheoV1 * finalScaleFactor);
            ticketValues.VolaTheoAdjV1 = FastRound(ticketValues.VolaTheoAdjV1 * finalScaleFactor);
            ticketValues.VolaTheoV2 = FastRound(ticketValues.VolaTheoV2 * finalScaleFactor);
            ticketValues.VolaTheoAdjV2 = FastRound(ticketValues.VolaTheoAdjV2 * finalScaleFactor);
            ticketValues.VolaTheoV3 = FastRound(ticketValues.VolaTheoV3 * finalScaleFactor);
            ticketValues.VolaTheoAdjV3 = FastRound(ticketValues.VolaTheoAdjV3 * finalScaleFactor);

            ticketValues.LockedTheo = FastRound(ticketValues.LockedTheo * finalScaleFactor);
            ticketValues.LockedDeltaAdjTheo = FastRound(ticketValues.LockedDeltaAdjTheo * finalScaleFactor);

            ticketValues.NetDelta = FastRound(ticketValues.NetDelta);
            ticketValues.NetGamma = FastRound(ticketValues.NetGamma);
            ticketValues.NetTheta = FastRound(ticketValues.NetTheta);

            ticketValues.TestValueSynched = testValueSync;
            ticketValues.VolaAdjTheoSyncV1 = volaAdjTheoSyncV1;
            ticketValues.VolaAdjTheoSyncV2 = volaAdjTheoSyncV2;
            ticketValues.VolaAdjTheoSyncV3 = volaAdjTheoSyncV3;
            ticketValues.DeltaAdjTheoSynched = adjTheoSeqSync;
            ticketValues.DeltaAdjTheoSequence = deltaAdjTheoSequence;
            ticketValues.TheoJumpDetected = theoJumpDetected;

            // Use reciprocal to calculate NetPrice (Lcd * Multiplier) -> (1 / finalScaleFactor)
            ticketValues.NetPrice = FastRound(Price / finalScaleFactor);

            double denom = ticketValues.Low - ticketValues.High;
            double percentBid = (Math.Abs(denom) > double.Epsilon) ? (ticketValues.Low - Price) / denom : 0.0;
            ticketValues.PercentBid = FastRound(IsSingleLegSell ? 1 - percentBid : percentBid);

            // Optimized Inline Infinity Checks
            if (double.IsInfinity(ticketValues.TotalDelta)) ticketValues.TotalDelta = 0.0;
            if (double.IsInfinity(ticketValues.TotalGamma)) ticketValues.TotalGamma = 0.0;
            if (double.IsInfinity(ticketValues.TotalVega)) ticketValues.TotalVega = 0.0;
            if (double.IsInfinity(ticketValues.WeightedVega)) ticketValues.WeightedVega = 0.0;
            if (double.IsInfinity(ticketValues.TotalTheta)) ticketValues.TotalTheta = 0.0;
            if (double.IsInfinity(ticketValues.TotalRho)) ticketValues.TotalRho = 0.0;
            if (double.IsInfinity(ticketValues.TotalImplied)) ticketValues.TotalImplied = 0.0;
            if (double.IsInfinity(ticketValues.TotalTheo)) ticketValues.TotalTheo = 0.0;
            if (double.IsInfinity(ticketValues.TotalDeltaAdjTheo)) ticketValues.TotalDeltaAdjTheo = 0.0;

            // --- Final Edge Calculations ---
            if (!IsBasketOrder)
            {
                bool useEdgeLogic = IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Buy;

                // Flattened logic to reduce nesting depth
                // If it is single leg buy OR not single leg, use normal logic
                // If single leg sell, use inverted logic
                bool invertEdge = IsSingleLeg && Side != ZeroPlus.Models.Data.Enums.Side.Buy;

                if (!invertEdge)
                {
                    ticketValues.EdgeToTheo = FastRound(ticketValues.NetTheo - edgeToTheoBasePrice);
                    ticketValues.EdgeToDeltaAdjTheo = FastRound(ticketValues.NetDeltaAdjTheo - edgeToTheoBasePrice);
                    ticketValues.EdgeToDeltaAdjTheoV0 = FastRound(ticketValues.VolaTheoAdjV0 - edgeToTheoBasePrice);
                    ticketValues.EdgeToMid = FastRound(ticketValues.Mid - Price);
                    ticketValues.EdgeToMidDerived = FastRound(ticketValues.MidDerived - Price);
                }
                else
                {
                    ticketValues.EdgeToTheo = FastRound(edgeToTheoBasePrice - ticketValues.NetTheo);
                    ticketValues.EdgeToDeltaAdjTheo = FastRound(edgeToTheoBasePrice - ticketValues.NetDeltaAdjTheo);
                    ticketValues.EdgeToDeltaAdjTheoV0 = FastRound(edgeToTheoBasePrice - ticketValues.VolaTheoAdjV0);
                    ticketValues.EdgeToMid = FastRound(Price - ticketValues.Mid);
                    ticketValues.EdgeToMidDerived = FastRound(Price - ticketValues.MidDerived);
                }

                if (TicketStyle is OrderTicketStyle.Combined or OrderTicketStyle.Dual)
                {
                    if (priceEvalReversed)
                    {
                        ticketValues.NetContraPrice = FastRound(ContraPrice / finalScaleFactor);
                        ticketValues.PriceDiff = IsSellOrder ? FastRound(-Price - ContraPrice) : FastRound(-ContraPrice - Price);

                        double highDiff = (ticketValues.Low - ticketValues.High);
                        // Inline check to prevent div by zero
                        double cpDiff = (Math.Abs(highDiff) > double.Epsilon) ? (ticketValues.High - ContraPrice) / highDiff : 0;
                        ticketValues.ContraPercentBid = IsSingleLeg ? FastRound(cpDiff) : FastRound((-ticketValues.High - ContraPrice) / highDiff);

                        ticketValues.ContraEdgeToTheo = IsContraSellOrder ? FastRound(-ContraPrice - ticketValues.NetTheo) : FastRound(-ticketValues.NetTheo - ContraPrice);
                        ticketValues.ContraEdgeToDeltaAdjTheo = IsContraSellOrder ? FastRound(-ContraPrice - ticketValues.NetDeltaAdjTheo) : FastRound(-ticketValues.NetDeltaAdjTheo - ContraPrice);
                        ticketValues.ContraEdgeToMid = IsContraSellOrder ? FastRound(-ContraPrice - ticketValues.Mid) : FastRound(-ticketValues.Mid - ContraPrice);
                    }
                    else if (priceEvalIdentical)
                    {
                        ticketValues.PriceDiff = IsSellOrder ? FastRound(Price - ContraPrice) : FastRound(ContraPrice - Price);

                        double highDiff = (ticketValues.Low - ticketValues.High);
                        double cpDiff = (Math.Abs(highDiff) > double.Epsilon) ? -(ticketValues.High - ContraPrice) / highDiff : 0;
                        ticketValues.ContraPercentBid = FastRound(cpDiff);

                        ticketValues.ContraEdgeToTheo = IsContraSellOrder ? FastRound(ContraPrice - ticketValues.NetTheo) : FastRound(ticketValues.NetTheo - ContraPrice);
                        ticketValues.ContraEdgeToDeltaAdjTheo = IsContraSellOrder ? FastRound(ContraPrice - ticketValues.NetDeltaAdjTheo) : FastRound(ticketValues.NetDeltaAdjTheo - ContraPrice);
                        ticketValues.ContraEdgeToMid = IsContraSellOrder ? FastRound(ContraPrice - ticketValues.Mid) : FastRound(ticketValues.Mid - ContraPrice);
                        ticketValues.NetContraPrice = FastRound(-ContraPrice / finalScaleFactor);
                    }
                }
            }

            // Final Integrity Check
            // Replaced implicit property comparisons with local vars where possible
            if (Math.Abs(lcd - Lcd) > double.Epsilon || Math.Abs(multiplier - Multiplier) > double.Epsilon)
            {
                ticketValues.HasError = true;
            }
            else if (!IsStockTied)
            {
                // Optimized bool logic
                bool countMismatch = count != Legs.Count;
                bool singleLegMismatch = isSingleLeg != IsSingleLeg;
                bool singleLegCountError = isSingleLeg && count != 1;
                bool multiLegCountError = !isSingleLeg && count <= 1;
                bool singleLegLcdError = isSingleLeg && Lcd != legs[0].Quantity;

                if (countMismatch || singleLegMismatch || singleLegCountError || multiLegCountError || singleLegLcdError)
                {
                    ticketValues.HasError = true;
                }
            }

            return ticketValues;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double FastRound(double val)
        {
            // Removing the 2nd argument (digits) assumes 2 decimals is standard. 
            // If digits varies, pass it in, but constant folding helps here.
            double ret = Math.Round(val, 2, MidpointRounding.AwayFromZero);
            // Optimized: Most values are NOT infinity. Branch prediction handles this well.
            return double.IsInfinity(ret) ? double.NaN : ret;
        }

        private void UpdateUi(TicketValues update)
        {
            if (IsDisposed)
            {
                return;
            }

            Low = update.Low;
            High = update.High;
            Mid = update.Mid;

            BidSize = update.BidSize;
            AskSize = update.AskSize;

            IsCheapo = update.IsCheapo;

            BestEdgeBid = update.BestEdgeBid;
            BestEdgeAsk = update.BestEdgeAsk;
            BestEdgeMid = update.BestEdgeMid;

            BestLow = update.BestLow;
            BestHigh = update.BestHigh;

            LowInt = update.LowInt;
            HighInt = update.HighInt;
            MidInt = update.MidInt;

            BestBidInt = update.BestBidInt;
            BestAskInt = update.BestAskInt;
            BestMidInt = update.BestMidInt;

            MktMkrBid = update.MktMkrBid;
            MktMkrAsk = update.MktMkrAsk;

            HighestBid = update.HighestBid;
            LowestAsk = update.LowestAsk;

            LowDerived = update.LowDerived;
            HighDerived = update.HighDerived;
            MidDerived = update.MidDerived;

            LowIntDerived = update.LowIntDerived;
            HighIntDerived = update.HighIntDerived;
            MidIntDerived = update.MidIntDerived;

            if (IsSingleLeg && Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                HighIntEdge = HighInt < High;
                LowIntEdge = LowInt > Low;
            }
            else
            {
                HighIntEdge = HighInt > High;
                LowIntEdge = LowInt < Low;
            }

            if (update.EmaSync || !OmsCore.Config.UpdateOnlySyncEma)
            {
                AdjEma = update.AdjEma;
                FullEma = update.FullEma;
                UnderEma = update.UnderEma;
            }

            BidEma = update.BidEma;
            AskEma = update.AskEma;

            BidEmaAdj = update.BidEmaAdj;
            AskEmaAdj = update.AskEmaAdj;

            Ema = OmsCore.Config.UseAdjEma ? update.AdjEma : update.FullEma;

            BidIvEma = update.BidIvEma;
            AskIvEma = update.AskIvEma;
            Width = update.Width;
            NetTheoSynched = update.TheoSynched;
            NetTheo = update.NetTheo;
            DeltaAdjTheoSynched = update.DeltaAdjTheoSynched;

            VolaTheoV0 = update.VolaTheoV0;
            VolaTheoV1 = update.VolaTheoV1;
            VolaTheoV2 = update.VolaTheoV2;
            VolaTheoV3 = update.VolaTheoV3;

            VolaIv = update.VolaIv;
            TheoBid = update.TheoBid;
            TheoAsk = update.TheoAsk;
            DigBid = update.DigBid;
            DigAsk = update.DigAsk;
            DigBidSize = update.DigBidSize;
            DigAskSize = update.DigAskSize;

            VolaPriceMetricV0 = update.VolaPriceMetricV0;
            VolaPriceMetricV1 = update.VolaPriceMetricV1;
            VolaPriceMetricV2 = update.VolaPriceMetricV2;
            VolaPriceMetricV3 = update.VolaPriceMetricV3;

            TheoBid = update.TheoBid;
            TheoAsk = update.TheoAsk;
            DigBid = update.DigBid;
            DigAsk = update.DigAsk;
            DigBidSize = update.DigBidSize;
            DigAskSize = update.DigAskSize;

            if (update.VolaAdjTheoSyncV1 || !OmsCore.Config.UpdateOnlySyncAdjTheo)
            {
                VolaTheoAdjV1 = update.VolaTheoAdjV1;
            }
            if (update.VolaAdjTheoSyncV2 || !OmsCore.Config.UpdateOnlySyncAdjTheo)
            {
                VolaTheoAdjV2 = update.VolaTheoAdjV2;
            }
            if (update.VolaAdjTheoSyncV3 || !OmsCore.Config.UpdateOnlySyncAdjTheo)
            {
                VolaTheoAdjV3 = update.VolaTheoAdjV3;
            }

            var now = DateTime.UtcNow;
            if (update.DeltaAdjTheoSynched || !OmsCore.Config.UpdateOnlySyncAdjTheo)
            {
                double theoChange = Math.Abs(NetDeltaAdjTheo - update.NetDeltaAdjTheo);
                if ((now - _highestTheoChangeUpdate).TotalSeconds > 15)
                {
                    _highestTheoChangeUpdate = now;
                    HighestTheoChange = theoChange;
                }
                else if (theoChange > _highestTheoChange || double.IsNaN(_highestTheoChange))
                {
                    HighestTheoChange = theoChange;
                }
                _lastTheoUpdateTime = now;
                NetDeltaAdjTheo = update.NetDeltaAdjTheo;
                DeltaAdjTheoSequence = update.DeltaAdjTheoSequence;
                SmoothedDeltaAdjTheo = update.SmoothedDeltaAdjTheo;
                VolaTheoAdjV0 = update.VolaTheoAdjV0;
                AdjDaEma = update.AdjDaEma;
                AdjVolaEma = update.AdjVolaEma;
            }
            else if ((now - _lastTheoUpdateTime).TotalSeconds > OmsCore.Config.TheoMisMatchResetIntervalSec)
            {
                NetDeltaAdjTheo = double.NaN;
                DeltaAdjTheoSequence = update.DeltaAdjTheoSequence;
            }
            if (update.TestValueSynched || !OmsCore.Config.UpdateOnlySyncAdjTheo)
            {
                NetTestValue = update.NetTestValue;
            }
            else
            {
                NetTestValue = double.NaN;
            }
            VolaEma = update.VolaEma;
            DaEma = update.DaEma;
            LockedTheo = update.LockedTheo;
            LockedDeltaAdjTheo = update.LockedDeltaAdjTheo;
            TheoJumpDetected = update.TheoJumpDetected;
            TheoDiff = update.NetTheo - update.NetDeltaAdjTheo;
            NetDelta = update.NetDelta;
            NetGamma = update.NetGamma;
            NetTheta = update.NetTheta;
            TotalVolume = update.TotalVolume;
            FirmTotalVolume = update.FirmTotalVolume;
            OpenInterest = update.OpenInterest;
            TotalGamma = update.TotalGamma;
            TotalVega = update.TotalVega;
            WeightedVega = update.WeightedVega;
            TotalTheta = update.TotalTheta;
            TotalRho = update.TotalRho;
            TotalImplied = update.TotalImplied;
            TotalTheo = update.TotalTheo;
            TotalDeltaAdjTheo = update.TotalDeltaAdjTheo;
            NetPrice = update.NetPrice;
            NetContraPrice = update.NetContraPrice;
            EdgeToTheo = update.EdgeToTheo;
            ContraEdgeToTheo = update.ContraEdgeToTheo;
            EdgeToDeltaAdjTheo = update.EdgeToDeltaAdjTheo;
            EdgeToDeltaAdjTheoV0 = update.EdgeToDeltaAdjTheoV0;
            ContraEdgeToDeltaAdjTheo = update.ContraEdgeToDeltaAdjTheo;
            EdgeToMid = update.EdgeToMid;
            EdgeToMidDerived = update.EdgeToMidDerived;
            ContraEdgeToMid = update.ContraEdgeToMid;
            PercentBid = update.PercentBid;
            ContraPercentBid = update.ContraPercentBid;
            PriceDiff = update.PriceDiff;
            TotalDelta = update.TotalDelta;
            TotalDeltaDirection = IsSingleLegSell ? -update.TotalDelta : update.TotalDelta;
            LastBidTheoSpread = update.LastBidTheoSpread;
            LastAskTheoSpread = update.LastAskTheoSpread;
            BidTheoSpreadEma = update.BidTheoSpreadEma;
            AskTheoSpreadEma = update.AskTheoSpreadEma;
            UpdateTwsTriggers();
            DeltaAdjPriceAsync();
            EdgeProjector?.CalculateEdge();

            if (IsBasketOrder)
            {
                CheckForTrigger(update);
            }

            if (LockLowPrice)
            {
                SetPriceMinimal(Low);
                if (!IsBasketOrder && CanReplace)
                {
                    _ = ModifyAsync();
                }
            }
            else if (LockMidPrice)
            {
                SetPriceMinimal(Mid);
                if (!IsBasketOrder && CanReplace)
                {
                    _ = ModifyAsync();
                }
            }
            else if (LockHighPrice)
            {
                SetPriceMinimal(High);
                if (!IsBasketOrder && CanReplace)
                {
                    _ = ModifyAsync();
                }
            }

            if (LockContraLowPrice)
            {
                SetContraPriceMinimal(Low);
                if (!IsBasketOrder && CanReplaceContra)
                {
                    _ = ModifyContraAsync();
                }
            }
            else if (LockContraMidPrice)
            {
                SetContraPriceMinimal(Mid);
                if (!IsBasketOrder && CanReplaceContra)
                {
                    _ = ModifyContraAsync();
                }
            }
            else if (LockContraHighPrice)
            {
                SetContraPriceMinimal(High);
                if (!IsBasketOrder && CanReplaceContra)
                {
                    _ = ModifyContraAsync();
                }
            }
        }

        private void CheckForTrigger(TicketValues update)
        {
            if (IsDisposed)
            {
                return;
            }

            bool prevNotification = ShowWidthNotification;

            if (!OrderSent && CloseStyle == Enums.CloseStyle.SweepTrade)
            {
                lock (_orderLock)
                {
                    if (!OrderSent && update.Low < Price && update.High > Price)
                    {
                        OrderSent = true;
                        _ = Task.Run(() => SubmitOrderAsync(isContra: false));
                        return;
                    }
                }
            }

            bool triggered = false;
            string triggerSource = null;
            if (BasketSettings.WidthNotificationEnabled)
            {
                triggered = BasketSettings.WidthNotificationTrigger < update.Width;
                if (triggered)
                {
                    triggerSource = "MKT Width";
                }
            }
            if (BasketSettings.NotifyOnTheoToMarketSpreadWideningFromEmaEnabled)
            {
                if (!triggered && LastBidTheoSpread > BidTheoSpreadEma && BidTheoSpreadEma != 0)
                {
                    var change = (LastBidTheoSpread - BidTheoSpreadEma) / BidTheoSpreadEma;
                    if (change > BasketSettings.MinPercentChangeOnTheoToMarketSpreadWideningFromEma)
                    {
                        triggered = true;
                        triggerSource = "BLCK Bid";
                    }
                }
                if (!triggered && LastAskTheoSpread > AskTheoSpreadEma && AskTheoSpreadEma != 0)
                {
                    var change = (LastAskTheoSpread - AskTheoSpreadEma) / AskTheoSpreadEma;
                    if (change > BasketSettings.MinPercentChangeOnTheoToMarketSpreadWideningFromEma)
                    {
                        triggered = true;
                        triggerSource = "BLCK Ask";
                    }
                }
            }
            if (!triggered && BasketSettings.MinChangeToEmaNotificationEnabled)
            {
                if (IsSingleLegSell)
                {
                    triggered = (update.Low - update.BidEmaAdj) > BasketSettings.MinChangeToEmaNotificationEnabledTrigger;
                }
                else
                {
                    triggered = (update.AskEmaAdj - update.High) > BasketSettings.MinChangeToEmaNotificationEnabledTrigger;
                }
            }
            if (!triggered && BasketSettings.PercentChangeInEmaNotificationEnabled)
            {
                if (IsSingleLegSell)
                {
                    triggered = (update.Low - update.BidEmaAdj) / ((update.Low + update.BidEmaAdj) / 2) > BasketSettings.MinChangeToEmaNotificationEnabledTrigger;
                }
                else
                {
                    triggered = (update.AskEmaAdj - update.High) / ((update.AskEmaAdj + update.High) / 2) > BasketSettings.PercentChangeInEmaNotificationTrigger;
                }
            }

            ShowWidthNotification = triggered;

            if (!prevNotification && triggered)
            {
                var validUnderlying = !BasketSettings.MaxPercentChangeInUnderlyingEmaEnabled || (Math.Abs(update.UnderEma - UnderMid) / (Math.Abs(update.Low + update.BidEmaAdj) / 2) < BasketSettings.MaxPercentChangeInUnderlyingEma);
                if (!validUnderlying)
                {
                    ShowErrorMessage("Triggered Blocked by Under Chg");
                }
                else
                {
                    if (BasketSettings.SubmitOnTriggerEnabled)
                    {
                        BasketTraderViewModel.SubmitFromTrigger(this, triggerSource);
                    }

                    if (BasketSettings.ActivateWindowOnNotificationEnabled)
                    {
                        ActivateWindow?.Invoke();
                    }

                    BasketSettings.TotalWidthTriggered++;
                }
            }

            if (BasketSettings.ShowTheoToMidIndicator)
            {
                if (double.IsNaN(update.NetDeltaAdjTheo) || double.IsNaN(update.Mid))
                {
                    TheoToMid = ValueCompare.Invalid;
                }
                else
                {
                    TheoToMid = update.NetDeltaAdjTheo == update.Mid ? ValueCompare.Equal :
                                update.NetDeltaAdjTheo > update.Mid ? ValueCompare.Above : ValueCompare.Below;
                }
            }
            AdjTheoToMid = update.NetDeltaAdjTheo - update.Mid;
        }

        protected void ValidateTicket()
        {
            bool isValid = Legs.Count > 0;

            if (string.IsNullOrWhiteSpace(Account))
            {
                LoadDefaultAccount();
            }

            if (string.IsNullOrWhiteSpace(Route))
            {
                Route = GetBestRoute();
            }

            if (string.IsNullOrWhiteSpace(Account))
            {
                isValid = false;
                IsAccountValid = false;
            }

            foreach (TicketLegModel spreadTraderItem in Legs)
            {
                if (string.IsNullOrWhiteSpace(spreadTraderItem.Symbol))
                {
                    isValid = false;
                }

                if (spreadTraderItem.Quantity <= 0)
                {
                    isValid = false;
                }
            }

            if (string.IsNullOrWhiteSpace(Route))
            {
                isValid = false;
            }

            if (!isValid)
            {
                IsSubmitEnabled = IsContraSubmitEnabled = false;
                IsModifyEnabled = IsContraModifyEnabled = false;
            }
            else
            {
                IsSubmitEnabled = IsContraSubmitEnabled = true;
            }
        }

        internal void UpdateQty(int qty)
        {
            lock (_legUpdateLock)
            {
                foreach (TicketLegModel leg in Legs)
                {
                    leg.LegUpdatedEvent -= UpdateTicketValues;
                }

                foreach (TicketLegModel leg in Legs)
                {
                    leg.Quantity = leg.Ratio * qty;
                }

                Lcd = qty;
                Qty = Lcd;
                if (ContraQtyLocked)
                {
                    ContraQty = Qty;
                }

                foreach (TicketLegModel leg in Legs)
                {
                    leg.LegUpdatedEvent -= UpdateTicketValues;
                    leg.LegUpdatedEvent += UpdateTicketValues;
                }
            }

            UpdateTicketValues();

            _log.Info("UpdateQty. " +
                      "Lcd: " + Lcd +
                      "Id: " + SpreadId);
        }

        internal async Task LoadLegsFromTosAsync(string tos, Side? side = null, bool loadOptions = false, bool setActQty = false)
        {
            await Task.Run(async () =>
            {
                List<TicketLegModel> legs = ParseFromTos(tos, setActQty);
                await AttachLegsToTicket(legs, side, loadOptions);
            });
        }

        internal async Task AttachLegsToTicket(List<TicketLegModel> legs, Side? side = null, bool loadOptions = false)
        {
            if (legs.Count == 0)
            {
                return;
            }
            Underlying = legs[0].Underlying;

            if (!IsBasketOrder)
            {
                await UpdateAccountsAndRoutes();
            }

            foreach (TicketLegModel leg in legs)
            {
                await ProcessLegForAdd(loadOptions, leg);
            }

            AddLegsSafe(legs);
            await SetDefaultValues();
            UpdateLCD();
            RatioLocked = true;
            SubscribeDataAsync();
            UpdateDescription();
            UpdateTicketValues();
            ValidateTicket();

            if (side != null)
            {
                if (Side != side)
                {
                    Dispatcher?.Invoke(() => Reverse());
                }
            }
        }

        protected async Task ProcessLegForAdd(bool loadOptions, TicketLegModel leg)
        {
            if (leg.Underlying != Underlying)
            {
                return;
            }

            Option option = OptionsHelper.GetOptionFromSymbol(leg.Symbol);
            leg.Account = Account;
            leg.Type = leg.Symbol.StartsWith(".") ? option.Type.ToString() : "STOCK";
            leg.Position = Positions.AUTO.ToString();
            if (leg.Quantity == 0)
            {
                leg.Quantity = 1;
            }
            if (leg.Ratio == 0)
            {
                leg.Ratio = 1;
            }

            if (loadOptions)
            {
                await leg.LoadExpirationsListAsync();
                var expirationInfo = leg.ExpirationsList.Where(x => x.Expiration == option.Expiration && x.RootSymbol == option.RootSymbol).FirstOrDefault();
                if (expirationInfo == null)
                {
                    expirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                    leg.ExpirationsList.Add(expirationInfo);
                }
                leg.ExpirationInfo = expirationInfo;
                leg.UpdateStrikesList();
                leg.Strike = leg.StrikesList.Where(x => x == option.Strike).FirstOrDefault();
                leg.UpdateStrikeVisibility();
            }
            else
            {
                var expirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                leg.ExpirationsList.Add(expirationInfo);
                leg.ExpirationInfo = expirationInfo;
                StrikeInfoModel strikeInfoModel = new(false, option.Strike);
                leg.StrikesList.Add(strikeInfoModel);
                leg.Strike = strikeInfoModel;
            }
            leg.LegUpdatedEvent -= UpdateTicketValues;
            leg.LegUpdatedEvent += UpdateTicketValues;

            await leg.ValidateLegAsync(true);
        }

        private void AddLegsSafe(List<TicketLegModel> legs)
        {
            try
            {
                if (IsBasketOrder)
                {
                    foreach (TicketLegModel leg in legs)
                    {
                        Legs.Add(leg);
                    }
                }
                else
                {
                    Dispatcher?.Invoke(() =>
                    {
                        foreach (TicketLegModel leg in legs)
                        {
                            Legs.Add(leg);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddLegsSafe));
                throw;
            }
        }

        internal void CheckOnSubscriptions()
        {
            if (double.IsNaN(Low) ^ double.IsNaN(High))
            {
                foreach (var leg in Legs)
                {
                    if (double.IsNaN(leg.Bid) ^ double.IsNaN(leg.Ask))
                    {
                        if (double.IsNaN(leg.Bid))
                        {
                            OmsCore.QuoteClient.Subscribe(leg.Symbol, SubscriptionFieldType.Bid, leg);
                        }
                        else
                        {
                            OmsCore.QuoteClient.Subscribe(leg.Symbol, SubscriptionFieldType.Ask, leg);
                        }
                    }
                }
            }
        }

        internal void UpdateUiProperties()
        {
            try
            {
                foreach (TicketLegModel leg in Legs)
                {
                    leg.UpdateUiProperties();
                }

                UpdateLcdPosition();

                for (var index = 0; index < _notifiersCount; index++)
                {
                    var notifier = _notifiers[index];
                    if (notifier.IsUpdated)
                    {
                        notifier.IsUpdated = false;
                        try
                        {
                            RaisePropertyChanged(notifier.Name);
                        }
                        catch (Exception ex)
                        {
                            notifier.IsUpdated = true;
                            _log.Debug(ex, "{0} -> RaisePropertyChanged({1})", nameof(UpdateUiProperties), notifier.Name);
                        }
                    }
                }

                CalculateUnrealizedPnlAndPosNetDelta();
                OnUiUpdate();
            }
            catch (Exception ex)
            {
                _log.Debug(ex, nameof(UpdateUiProperties));
            }
        }

        public virtual void OnUiUpdate()
        {
        }

        private void SubscribeToIbData()
        {
            if (IsIbTicket)
            {
                SubscribeToIbCommand();
            }
            else if (OmsCore.Config.ShowCobOnTicketsV2 && !IsBasketOrder && TicketStyle is OrderTicketStyle.Complex or OrderTicketStyle.Combined)
            {
                SubscribeToIbCob();
            }
        }

        internal void ResetAutomation()
        {
            try
            {
                PermCloser?.Stop();
                Looper?.Stop();
                Tracker?.Stop();
                CxlReplaceCloser?.Stop();
                Closer?.Stop();
                Fisher?.Stop();
                SweepCloser?.Stop();
                LegOutCloser?.Stop();
                AutoLegCloser?.Stop();
                ThreeWayCloser?.Stop();
                StopLossManager?.Stop();
                StopOrderManager?.Stop();
                AutoCloseManager?.Stop();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ResetAutomation));
            }
        }

        internal double GetLoopMaxLoss()
        {
            if (IsBasketOrder)
            {
                if (TryGetDynamicEdge(out _, out _, out double maxLoss, out _))
                {
                    return maxLoss;
                }
                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                if (automationConfigModel != null)
                {
                    return automationConfigModel.LoopMaxLoss;
                }
                else
                {
                    return LoopMaxLoss;
                }
            }
            else
            {
                return LoopMaxLoss;
            }
        }

        internal double GetLoopMinEdge()
        {
            if (IsBasketOrder)
            {
                if (TryGetDynamicEdge(out _, out double loopMinEdge, out _, out _))
                {
                    return loopMinEdge;
                }

                AutomationConfigModel automationConfigModel = GetAutomationConfig();
                if (automationConfigModel != null)
                {
                    return automationConfigModel.LoopMinEdgeUsePercentage ?
                        Math.Max(GetClosingEdge() * automationConfigModel.LoopMinEdgePercentage, 0) :
                        automationConfigModel.LoopMinEdge;
                }
                else
                {
                    return LoopMinEdge;
                }
            }
            else
            {
                return LoopMinEdge;
            }
        }

        internal bool TryGetDynamicEdge(out double suggestedEdge, out double loopMinEdge, out double loopMaxLoss, out double permMinEdge)
        {
            suggestedEdge = double.NaN;
            loopMinEdge = double.NaN;
            loopMaxLoss = double.NaN;
            permMinEdge = double.NaN;
            bool dynamicEdgeFound = false;
            if (IsBasketOrder && GetAutomationConfig() != null)
            {
                AutomationConfigModel automationConfig = GetAutomationConfig();
                if (automationConfig.LoopCloseEdgeType == LoopCloseEdgeType.Dynamic && automationConfig.DynamicEdgeModel != null)
                {
                    int dte = DaysToExpiration;
                    int contracts = Contracts;
                    double delta = TotalDelta;
                    dynamicEdgeFound = automationConfig.DynamicEdgeModel.GetEdge(fish: false,
                                                                                 BaseStrategy,
                                                                                 Underlying,
                                                                                 UnderMid,
                                                                                 StrikeSpacing,
                                                                                 dte,
                                                                                 contracts,
                                                                                 Math.Min(BidSize, AskSize),
                                                                                 delta,
                                                                                 Width,
                                                                                 (double)PriceIncrement,
                                                                                 GetWeightedVega,
                                                                                 out suggestedEdge,
                                                                                 out loopMinEdge,
                                                                                 out loopMaxLoss,
                                                                                 out double maxThroughTheo,
                                                                                 out double maxThroughVola,
                                                                                 out TheoModel volaModel,
                                                                                 out double maxPercentBid,
                                                                                 out double maxThroughEma,
                                                                                 out double maxThroughTradePx,
                                                                                 out double minMarketWidth,
                                                                                 out double minMarketCross,
                                                                                 out int qty,
                                                                                 out permMinEdge,
                                                                                 out string reason);

                    loopMaxLoss *= automationConfig.MaxLossMultiplier;
                    suggestedEdge *= automationConfig.EdgeMultiplier;
                    suggestedEdge = Math.Round(suggestedEdge * automationConfig.DynamicEdgeExpansion, 2);
                    loopMinEdge = Math.Abs(loopMinEdge);
                    _log.Info("Loop dynamic edge lookup. " +
                              "Order: " + SpreadId + ", " +
                              "DTE: " + dte + ", " +
                              "Reason: " + reason + ", " +
                              "Contracts: " + contracts + ", " +
                              "Delta: " + delta + ", " +
                              "Qty: " + qty + ", " +
                              "Suggested Edge: " + suggestedEdge + ", " +
                              "Max Through Ema: " + maxThroughTheo + ", " +
                              "Max Percent Bid: " + maxPercentBid + ", " +
                              "Max Through Theo: " + maxThroughEma + ", " +
                              "Max Through Trade Px: " + maxThroughTradePx + ", " +
                              "Min Market Width: " + minMarketWidth + ", " +
                              "Default: " + automationConfig.ContraFishEdge + ", " +
                              "Edge Exp: " + automationConfig.DynamicEdgeExpansion + ", " +
                              "Edge Mul: " + automationConfig.EdgeMultiplier + ", " +
                              "Max Loss Mul: " + automationConfig.MaxLossMultiplier + ", " +
                              "Fish Edge: " + suggestedEdge + ", " +
                              "Perm Min Edge: " + permMinEdge + ", " +
                              "Loop Min Edge: " + loopMinEdge + ", " +
                              "Loop Max Loss: " + loopMaxLoss);

                }
            }

            return dynamicEdgeFound;
        }

        internal double GetWeightedVega(bool wait = false)
        {
            if (!WeightedVegaLoaded)
            {
                if (wait)
                {
                    Task<bool> waitTask = WaitForWeightedVegaLoadAsync();
                    waitTask.Wait();
                    if (!waitTask.Result)
                    {
                        return double.NaN;
                    }
                }
                else
                {
                    return double.NaN;
                }
            }
            return WeightedVega;
        }

        protected async Task<PricingResponseModel> GetFreshPricesAsync()
        {
            return await OmsCore.PricingClient.GetFreshPrices(this);
        }

        public string GetStats()
        {
            return $"spread_id={SpreadId} " +
                   $"side={Side} " +
                   $"price={Price} " +
                   $"contra_price={ContraPrice} " +
                   $"bid={Low} " +
                   $"ask={High} " +
                   $"best_bid={BestLow} " +
                   $"best_ask={BestHigh} " +
                   $"best_bid_int={BestBidInt} " +
                   $"best_ask_int={BestAskInt} " +
                   $"mkt_mkr_bid={MktMkrBid} " +
                   $"mkt_mkr_ask={MktMkrAsk} " +
                   $"highest_bid={HighestBid} " +
                   $"lowest_ask={LowestAsk} " +
                   $"da_ema={AdjDaEma} " +
                   $"ema={Ema} " +
                   $"theo={NetTheo} " +
                   $"adj_theo={NetDeltaAdjTheo} " +
                   $"test_value={NetTestValue} " +
                   $"best_price={BestAveragePrice} " +
                   $"best_under_price={LastMainUnderMidAtBestFill} " +
                   $"fill_price={AveragePrice} " +
                   $"fill_under_price={LastMainUnderMidAtFill} " +
                   $"fill_contra_price={ContraAveragePrice} " +
                   $"fill_contra_under_price={LastContraUnderMidAtFill} " +
                   $"fill_volume={LastMainTotalVolumeAtFill} " +
                   $"fill_contra_volume={LastContraTotalVolumeAtFill} " +
                   $"esf_buy_price={EdgeScanFeedBuyPrice} " +
                   $"esf_sell_price={EdgeScanFeedSellPrice} " +
                   $"creep={BasketTraderViewModel?.ServerCreep}";
        }

        public void SetStats()
        {
            try
            {
                SetOrderDetailTag(nameof(EdgeType), EdgeType.ToString());
                SetOrderDetailTag(nameof(Edge), Edge.ToString());
                SetOrderDetailTag("Spread", SpreadId?.ToString());
                SetOrderDetailTag("Side", Side?.ToString());
                SetOrderDetailTag("Px", Price.ToString());
                SetOrderDetailTag("HardSide At Trade", HardSide?.ToString());
                SetOrderDetailTag("HardSide At Trade Time", HardSideDesignationTime.ToString());
                SetOrderDetailTag("HardSide At Buy Giveup", HardSideBuyGiveUp.ToString());
                SetOrderDetailTag("HardSide At Sell Giveup", HardSideSellGiveUp.ToString());
                SetOrderDetailTag("C.Px", ContraPrice.ToString());
                SetOrderDetailTag("Bid", Low.ToString());
                SetOrderDetailTag("Ask", High.ToString());
                SetOrderDetailTag("Best Bid", BestLow.ToString());
                SetOrderDetailTag("Best Ask", BestHigh.ToString());
                SetOrderDetailTag("Best Bid Int", BestBidInt.ToString());
                SetOrderDetailTag("Best Ask Int", BestAskInt.ToString());
                SetOrderDetailTag("Mkt Mkr Bid", MktMkrBid.ToString());
                SetOrderDetailTag("Mkt Mkr Ask", MktMkrAsk.ToString());
                SetOrderDetailTag("Highest Bid", HighestBid.ToString());
                SetOrderDetailTag("Lowest Ask", LowestAsk.ToString());
                SetOrderDetailTag("Ema", GetEma().ToString());
                SetOrderDetailTag("Theo", NetTheo.ToString());
                SetOrderDetailTag("A.Theo", NetDeltaAdjTheo.ToString());
                SetOrderDetailTag("Test Value", NetTestValue.ToString());
                SetOrderDetailTag("Best Px", BestAveragePrice.ToString());
                SetOrderDetailTag("Best Under Px", LastMainUnderMidAtBestFill.ToString());
                SetOrderDetailTag("Fill Px", AveragePrice.ToString());
                SetOrderDetailTag("Fill Under Px", LastMainUnderMidAtFill.ToString());
                SetOrderDetailTag("Fill C. Px", ContraAveragePrice.ToString());
                SetOrderDetailTag("Fill C. Under Px", LastContraUnderMidAtFill.ToString());
                SetOrderDetailTag("Fill Vol", LastMainTotalVolumeAtFill.ToString());
                SetOrderDetailTag("Fill C. Vol", LastContraTotalVolumeAtFill.ToString());
                SetOrderDetailTag("ESF B Px", EdgeScanFeedBuyPrice.ToString());
                SetOrderDetailTag("ESF S Px", EdgeScanFeedSellPrice.ToString());
                SetOrderDetailTag("Creep", BasketTraderViewModel?.ServerCreep.ToString());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SetStats));
                _orderDetailsContainer["Error"] = ex.Message;
            }
        }

        public void SetOrderDetailTag(string key, string value)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    _orderDetailsContainer[key] = value;
                }
            }
            catch (Exception ex)
            {
                _orderDetailsContainer["Error"] = ex.Message;
            }
        }

        public IComplexOrderLeg GetLeg(string legId)
        {
            TicketLegModel ticketLegModel = Legs.FirstOrDefault(x => x.LegID == legId);
            return ticketLegModel;
        }

        internal void SaveAutoCloseConfigModels()
        {
            try
            {
                string dir = OmsConfig.GetConfigDirectory();
                string autoCloseConfigSavePath = Path.Combine(dir, nameof(AutoCloseConfigModels) + ".json");
                string export = JsonConvert.SerializeObject(AutoCloseConfigModels.ToList());
                File.WriteAllText(autoCloseConfigSavePath, export);
                OmsCore.Config.OnChange(requiresRestart: false, notify: true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAutoCloseConfigModels));
            }
        }

        internal void LoadAutoCloseConfigModels()
        {
            try
            {
                if (TicketStyle is OrderTicketStyle.Complex or OrderTicketStyle.Combined)
                {
                    string dir = OmsConfig.GetConfigDirectory();
                    string autoCloseConfigSavePath = Path.Combine(dir, nameof(AutoCloseConfigModels) + ".json");
                    if (File.Exists(autoCloseConfigSavePath))
                    {
                        string content = File.ReadAllText(autoCloseConfigSavePath);
                        List<AutoCloseConfigViewModel> import = JsonConvert.DeserializeObject<List<AutoCloseConfigViewModel>>(content);
                        if (import != null)
                        {
                            Dispatcher?.BeginInvoke(() =>
                            {
                                AutoCloseConfigModels = import.ToObservableCollection();
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveAutoCloseConfigModels));
            }
        }

        protected double GetDefaultContraEdge()
        {
            if (Underlying != null && OmsCore.Config.ContraEdgeLookup.TryGetValue(Underlying, out var defaultContraEdge))
            {
                return defaultContraEdge;
            }

            defaultContraEdge = OmsCore.Config.DefaultContraEdge;
            if (Underlying == "$SPX")
            {
                defaultContraEdge = OmsCore.Config.DefaultSpxContraEdge;
            }

            return defaultContraEdge;
        }

        internal double GetClosingEdge(bool reset = true)
        {
            if (!IsBasketOrder)
            {
                return ContraFishEdge;
            }
            else
            {
                IAutomationConfig automationConfig = GetAutomationConfig();
                switch (automationConfig.LoopCloseEdgeType)
                {
                    case LoopCloseEdgeType.Static:
                        double edge = automationConfig.CloseEdgeMinValue;
                        if (!double.IsNaN(CloseEdgeOveride))
                        {
                            edge = Math.Max(edge, CloseEdgeOveride);
                            if (reset)
                            {
                                CloseEdgeOveride = double.NaN;
                            }
                            string message = "Loop close edge override. " +
                                             "Order: " + SpreadId + ", " +
                                             "Default: " + automationConfig.ContraFishEdge + ", " +
                                             "Override: " + edge;
                            _log.Info(message);
                        }

                        double selectedEdge = Math.Max(edge, automationConfig.ContraFishEdge);
                        _log.Info("Loop close edge lookup. " +
                                  "Order: " + SpreadId + ", " +
                                  "Default: " + automationConfig.ContraFishEdge + ", " +
                                  "Override: " + edge + ", " +
                                  "Selected: " + selectedEdge);
                        return selectedEdge;
                    case LoopCloseEdgeType.Dynamic when automationConfig.DynamicEdgeModel != null:
                        if (!TryGetDynamicEdge(out double suggestedEdge, out _, out _, out _))
                        {
                            edge = automationConfig.CloseEdgeMinValue;
                            if (!double.IsNaN(CloseEdgeOveride))
                            {
                                edge = Math.Max(edge, CloseEdgeOveride);
                                if (reset)
                                {
                                    CloseEdgeOveride = double.NaN;
                                }
                                string message = "Loop close edge override. " +
                                                 "Order: " + SpreadId + ", " +
                                                 "Default: " + automationConfig.ContraFishEdge + ", " +
                                                 "Override: " + edge;
                                _log.Info(message);
                            }
                            suggestedEdge = Math.Max(edge, automationConfig.ContraFishEdge);
                        }
                        return suggestedEdge;
                    default:
                        ShowMessage("Loop close edge using fall back!", SpreadId);
                        selectedEdge = Math.Max(automationConfig.CloseEdgeMinValue, automationConfig.ContraFishEdge);
                        _log.Info("Loop close edge lookup using fall back. " +
                                  "Order: " + SpreadId + ", " +
                                  "Min: " + automationConfig.CloseEdgeMinValue + ", " +
                                  "Default: " + automationConfig.ContraFishEdge + ", " +
                                  "Selected: " + selectedEdge);
                        return selectedEdge;
                }
            }
        }

        protected virtual void RegisterEvents(ITraderModel traderModel)
        {
            TradeEvent += traderModel.OnTrade;
            OrderFilledUpdatedEvent += traderModel.OnOrderFilledEvent;
            OrderClosedUpdateEvent += traderModel.OnOrderClosedEvent;
        }

        public virtual void ShowAlert()
        {
        }
        public virtual InstanceMode GetInstanceMode()
        {
            return InstanceMode;
        }

        public int GetLoopCloseInterval()
        {
            return ContraFishIntervalMax > ContraFishInterval
                ? Random.Shared.Next(ContraFishInterval, ContraFishIntervalMax + 1)
                : ContraFishInterval;
        }

        public virtual Task CheckForLegOutLoopAsync(double avgPrice, DateTime receiveTime)
        {
            return Task.CompletedTask;
        }
    }
}