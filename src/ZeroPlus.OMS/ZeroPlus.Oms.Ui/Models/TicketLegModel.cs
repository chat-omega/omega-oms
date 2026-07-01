using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.MessageObjects;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.ViewModels;
using Option = ZeroPlus.Oms.Data.Securities.Option;
using PositionEffect = ZeroPlus.Models.Data.Enums.PositionEffect;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Models
{
    public enum Types
    {
        CALL,
        PUT,
        STOCK,
    };

    public enum Positions
    {
        AUTO,
        OPEN,
        CLOSE,
    };

    public enum SecurityType
    {
        Stock,
        Option,
    }

    public partial class TicketLegModel : BindableBase, IOmsDataSubscriber, IOmsPositionSubscriber, IOmsOrderLeg, IComplexOrderLeg, IQuoteDisplay
    {
        private const double TOLERANCE = .01;
        private const int MIN_WEIGHTED_VEGA_UPDATE = 1000;
        private const double STRIKE_TOLERANCE = .01;

        public delegate void LegUpdatedEventHandler();
        public event LegUpdatedEventHandler LegUpdatedEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private Notifier[] _notifiers;
        private int _notifiersCount;

        #region Notifier Callbacks
        partial void CoerceType(ref string value) => value = value?.ToUpper();
        partial void CoercePosition(ref string value) => value = value?.ToUpper();
        partial void OnSymbolChanged(string value)
        {
            SecurityType = !string.IsNullOrEmpty(value) && value.StartsWith(".") ? SecurityType.Option : SecurityType.Stock;
            if (SecurityType == SecurityType.Option)
            {
                Security = OptionsHelper.GetOptionFromSymbol(Symbol);
                ((IComplexOrderLeg)this).Security = OmsCore.SecurityBook.GetSecurity(Symbol);
            }
        }
        partial void OnQuantityChanged(int value) => QtyChanged();
        partial void OnSideChanged(Side? value) =>
            ContraSide = value == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
        #endregion

        #region Notifier Properties
        [NotifyProperty(CheckEquality = false)]
        public partial string Symbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsCheapo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UnderLast { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string DerivedSymbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string LowerDerivedSymbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string HigherDerivedSymbol { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool Active { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool AddToPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int ActualQty { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Description { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<ExpirationInfoModel> ExpirationsList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<StrikeInfoModel> StrikesList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> TypesList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ObservableCollection<string> PositionsList { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? ContraSide { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int Quantity { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial int Ratio { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial ExpirationInfoModel ExpirationInfo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool StrikeVisible { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial StrikeInfoModel Strike { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Type { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial string Position { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsValid { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsExpirationValid { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool IsStrikeValid { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double GammaAdjustedDelta { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ManualAvgCost { get; set; }
        [NotifyProperty]
        public partial double Bid { get; set; }
        [NotifyProperty]
        public partial double Ask { get; set; }
        [NotifyProperty]
        public partial double Mid { get; set; }
        [NotifyProperty]
        public partial double BidInterpolated { get; set; }
        [NotifyProperty]
        public partial double AskInterpolated { get; set; }
        [NotifyProperty]
        public partial double BestBid { get; set; }
        [NotifyProperty]
        public partial double BestAsk { get; set; }
        [NotifyProperty]
        public partial double MktMkrBid { get; set; }
        [NotifyProperty]
        public partial double MktMkrAsk { get; set; }
        [NotifyProperty]
        public partial double HighestBid { get; set; }
        [NotifyProperty]
        public partial double LowestAsk { get; set; }
        [NotifyProperty]
        public partial double SkewAdjustedHighestBid { get; set; }
        [NotifyProperty]
        public partial double SkewAdjustedLowestAsk { get; set; }
        [NotifyProperty]
        public partial double HighestBidBase { get; set; }
        [NotifyProperty]
        public partial double LowestAskBase { get; set; }
        [NotifyProperty]
        public partial DateTime HighestBidTime { get; set; }
        [NotifyProperty]
        public partial DateTime LowestAskTime { get; set; }
        [NotifyProperty]
        public partial double HighestBidUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double LowestAskUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double BidDerived { get; set; }
        [NotifyProperty]
        public partial double AskDerived { get; set; }
        [NotifyProperty]
        public partial double BidDerivedInterpolated { get; set; }
        [NotifyProperty]
        public partial double AskDerivedInterpolated { get; set; }
        [NotifyProperty]
        public partial double Ema { get; set; }
        [NotifyProperty]
        public partial double AdjBidEma { get; set; }
        [NotifyProperty]
        public partial double BidEma { get; set; }
        [NotifyProperty]
        public partial double AdjEma { get; set; }
        [NotifyProperty]
        public partial double UnderEma { get; set; }
        [NotifyProperty]
        public partial double FullEma { get; set; }
        [NotifyProperty]
        public partial double AdjAskEma { get; set; }
        [NotifyProperty]
        public partial double AskEma { get; set; }
        [NotifyProperty]
        public partial double EmaSpreadBid { get; set; }
        [NotifyProperty]
        public partial double EmaSpreadAsk { get; set; }
        [NotifyProperty]
        public partial double SpreadEma { get; set; }
        [NotifyProperty]
        public partial double BidIvEma { get; set; }
        [NotifyProperty]
        public partial double AskIvEma { get; set; }
        [NotifyProperty]
        public partial double MidIvEma { get; set; }
        [NotifyProperty]
        public partial double DeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double TheoBid { get; set; }
        [NotifyProperty]
        public partial double TheoAsk { get; set; }
        [NotifyProperty]
        public partial double SmoothedDeltaAdjTheo { get; set; }
        [NotifyProperty]
        public partial double VolaTheoV0 { get; set; }
        [NotifyProperty]
        public partial double VolaTheoAdjV0 { get; set; }
        [NotifyProperty]
        public partial double VolaIv { get; set; }
        [NotifyProperty]
        public partial double TestValue { get; set; }
        [NotifyProperty]
        public partial uint DeltaAdjTheoSequence { get; set; }
        [NotifyProperty]
        public partial ulong ZpTheoSequence { get; set; }
        [NotifyProperty]
        public partial double DigBid { get; set; }
        [NotifyProperty]
        public partial double DigAsk { get; set; }
        [NotifyProperty]
        public partial uint DigBidSize { get; set; }
        [NotifyProperty]
        public partial uint DigAskSize { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial bool TheoJumpDetected { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AdjTheoUnderlying { get; set; }
        [NotifyProperty]
        public partial double LockedTheo { get; set; }
        [NotifyProperty]
        public partial double LockedTheoUnderlying { get; set; }
        [NotifyProperty]
        public partial double LockedDeltaAdjTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastBidTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double LastAskTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BidTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double AskTheoSpreadEma { get; set; }
        [NotifyProperty]
        public partial uint TestValueSequence { get; set; }
        [NotifyProperty]
        public partial ulong EmaSequence { get; set; }
        [NotifyProperty]
        public partial double DeltaAdjBidTheo { get; set; }
        [NotifyProperty]
        public partial double DeltaAdjAskTheo { get; set; }
        [NotifyProperty]
        public partial double Volume { get; set; }
        [NotifyProperty]
        public partial double OpenInterest { get; set; }
        [NotifyProperty]
        public partial double Delta { get; set; }
        [NotifyProperty]
        public partial double Gamma { get; set; }
        [NotifyProperty]
        public partial double Theta { get; set; }
        [NotifyProperty]
        public partial double Vega { get; set; }
        [NotifyProperty]
        public partial double DeltaModeled { get; set; }
        [NotifyProperty]
        public partial double GammaModeled { get; set; }
        [NotifyProperty]
        public partial double ThetaModeled { get; set; }
        [NotifyProperty]
        public partial double VegaModeled { get; set; }
        [NotifyProperty]
        public partial double NetDelta { get; set; }
        [NotifyProperty]
        public partial double NetGamma { get; set; }
        [NotifyProperty]
        public partial double NetTheta { get; set; }
        [NotifyProperty]
        public partial double WeightedVega { get; set; }
        [NotifyProperty]
        public partial double Rho { get; set; }
        [NotifyProperty]
        public partial double Implied { get; set; }
        [NotifyProperty]
        public partial double Theo { get; set; }
        [NotifyProperty]
        public partial string HanweckTime { get; set; }
        [NotifyProperty]
        public partial int InfoBits { get; set; }
        [NotifyProperty]
        public partial double AdjDaEma { get; set; }
        [NotifyProperty]
        public partial double VolaEma { get; set; }
        [NotifyProperty]
        public partial double AdjVolaEma { get; set; }
        [NotifyProperty]
        public partial double DaEma { get; set; }
        [NotifyProperty]
        public partial double UnrealizedPL { get; set; }
        [NotifyProperty]
        public partial double TradingPL { get; set; }
        [NotifyProperty]
        public partial int TradingNetQty { get; set; }
        [NotifyProperty]
        public partial double TradingAveCost { get; set; }
        [NotifyProperty]
        public partial double NotionalValue { get; set; }
        [NotifyProperty]
        public partial int NetQty { get; set; }
        [NotifyProperty]
        public partial int FirmNetQty { get; set; }
        [NotifyProperty]
        public partial bool FirmNetQtyInitialized { get; set; }
        [NotifyProperty]
        public partial double UserNetQty { get; set; }
        [NotifyProperty]
        public partial bool NetQtyInitialized { get; set; }
        [NotifyProperty]
        public partial double NetPL { get; set; }
        [NotifyProperty]
        public partial double MarketValue { get; set; }
        [NotifyProperty]
        public partial double DayPL { get; set; }
        [NotifyProperty]
        public partial int TradingSellQty { get; set; }
        [NotifyProperty]
        public partial double TradingSellAvePrice { get; set; }
        [NotifyProperty]
        public partial int TradingBuyQty { get; set; }
        [NotifyProperty]
        public partial double RealizedPL { get; set; }
        [NotifyProperty]
        public partial int OpeningQty { get; set; }
        [NotifyProperty]
        public partial double OpeningCost { get; set; }
        [NotifyProperty]
        public partial double MarkedCost { get; set; }
        [NotifyProperty]
        public partial double AveCost { get; set; }
        [NotifyProperty]
        public partial double TradingBuyAvePrice { get; set; }
        [NotifyProperty]
        public partial string Account { get; set; }
        [NotifyProperty]
        public partial double IvVegaPnl { get; set; }
        [NotifyProperty]
        public partial double GammaTheta { get; set; }
        [NotifyProperty]
        public partial double NetWeightedVega { get; set; }
        [NotifyProperty]
        public partial ZeroPlus.Models.Data.Enums.Side? BestOpeningSide { get; set; }
        [NotifyProperty]
        public partial ZeroPlus.Models.Data.Enums.Side? BestHardSide { get; set; }
        [NotifyProperty]
        public partial double BestBuyPrice { get; set; }
        [NotifyProperty]
        public partial double BestBuyPriceAdj { get; set; }
        [NotifyProperty]
        public partial double BestBuyPriceUnder { get; set; }
        [NotifyProperty]
        public partial double BestBuyPriceDelta { get; set; }
        [NotifyProperty]
        public partial double BestSellPrice { get; set; }
        [NotifyProperty]
        public partial double BestSellPriceAdj { get; set; }
        [NotifyProperty]
        public partial double BestSellPriceUnder { get; set; }
        [NotifyProperty]
        public partial double BestSellPriceDelta { get; set; }
        [NotifyProperty]
        public partial int BidSize { get; set; }
        [NotifyProperty]
        public partial int AskSize { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UnrealPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ManualUnrealPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double ManualRealPnl { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double TimeValue { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double IntrinsicValue { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double FVDivs { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UPrice { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UFwd { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double UFwdFactor { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BorrowCost { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double BorrowRate { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial Side? Side { get; set; }
        #endregion

        private string _underlying;
        private double _bid;
        private double _ask;
        private int _bidSize;
        private int _askSize;
        private double _bidInterpolated;
        private double _askInterpolated;
        private double _bestBid;
        private double _bestAsk;
        private double _mktMkrBid;
        private double _mktMkrAsk;
        private double _skewAdjustedHighestBid;
        private double _skewAdjustedLowestAsk;
        private double _highestBid;
        private double _LowestAsk;
        private double _highestBidBase;
        private double _LowestAskBase;
        private DateTime _highestBidTime;
        private DateTime _LowestAskTime;
        private double _highestBidUnderlyingMid;
        private double _LowestAskUnderlyingMid;
        private double _lowerBidDerived;
        private double _higherBidDerived;
        private double _bidDerived;
        private double _lowerAskDerived;
        private double _higherAskDerived;
        private double _askDerived;
        private double _lowerBidDerivedInterpolated;
        private double _higherBidDerivedInterpolated;
        private double _bidDerivedInterpolated;
        private double _lowerAskDerivedInterpolated;
        private double _higherAskDerivedInterpolated;
        private double _askDerivedInterpolated;
        private double _emaSpreadBid;
        private double _emaSpreadAsk;
        private double _ema;
        private double _adjBidEma;
        private double _bidEma;
        private double _adjEma;
        private double _underEma;
        private double _fullEma;
        private double _adjAskEma;
        private double _askEma;
        private double _spreadEma;
        private double _bidIvEma;
        private double _askIvEma;
        private double _midIvEma;
        private uint _deltaAdjTheoSequence;
        private ulong _zpTheoSequence;
        private uint _testValueSequence;
        private ulong _emaSequence;
        private double _testValue;
        private double _deltaAdjTheo;
        private double _theoBid;
        private double _theoAsk;
        private double _digBid;
        private double _digAsk;
        private uint _digBidSize;
        private uint _digAskSize;
        private double _smoothedDeltaAdjTheo;
        private double _volaTheoV0;
        private double _volaTheoAdjV0;
        private double _volaIv;
        private double _adjDaEma;
        private double _volaEma;
        private double _adjVolaEma;
        private double _daEma;
        private double _volaPriceMetric;
        private double _deltaAdjBidTheo;
        private double _deltaAdjAskTheo;
        private double _volume = double.NaN;
        private double _openInterest = double.NaN;
        private double _iv;
        private double _delta;
        private double _gamma;
        private double _theta;
        private double _netDelta;
        private double _netGamma;
        private double _netTheta;
        private double _vega;
        private double _weightedVega;
        private double _rho;
        private double _implied;
        private double _theo;
        private string _hanweckTime;
        private int _infoBits;
        private double _unrealizedPL;
        private double _tradingPL;
        private int _tradingNetQty;
        private double _tradingAveCost;
        private double _notionalValue;
        private int _netQty;
        private int _firmNetQty;
        private double _userNetQty;
        private bool _netQtyInitialized;
        private bool _firmNetQtyInitialized;
        private double _netPL;
        private double _marketValue;
        private double _dayPL;
        private int _tradingSellQty;
        private double _tradingSellAvePrice;
        private int _tradingBuyQty;
        private double _realizedPL;
        private int _openingQty;
        private double _openingCost;
        private double _markedCost;
        private double _aveCost;
        private double _tradingBuyAvePrice;
        private string _account;
        private double _initialIv;
        private double _mid;
        private double _ivVegaPnl;
        private double _gammaTheta;
        private double _netWeightedVega;
        private double _deltaModeled;
        private double _gammaModeled;
        private double _thetaModeled;
        private double _vegaModeled;
        private Side? _bestOpeningSide;
        private Side? _bestHardSide;
        private double _bestBuyPrice = double.NaN;
        private double _bestBuyPriceAdj = double.NaN;
        private double _bestBuyPriceUnder = double.NaN;
        private double _bestBuyPriceDelta = double.NaN;
        private double _bestSellPrice = double.NaN;
        private double _bestSellPriceAdj = double.NaN;
        private double _bestSellPriceUnder = double.NaN;
        private double _bestSellPriceDelta = double.NaN;
        private bool _isStrikeValid;
        private bool _theoJumpDetected;
        private double _adjTheoUnderlying;
        private string _topQuoteSymbol;
        private double _topQuoteMultiplier;
        private double _lockedTheo;
        private double _lockedTheoUnderlying;
        private double _lockedDeltaAdjTheo;
        private bool _isCheapo;
        private double _gammaAdjustedDelta;
        private double _manualAvgCost;
        private bool _isExpirationValid;
        private bool _isValid;
        private string _position;
        private string _type;
        private StrikeInfoModel _strike;
        private bool _strikeVisible;
        private ExpirationInfoModel _expirationInfo;
        private int _ratio;
        private int _quantity;
        private Side? _contraSide;
        private ObservableCollection<string> _positionsList;
        private ObservableCollection<string> _typesList;
        private ObservableCollection<StrikeInfoModel> _strikesList;
        private ObservableCollection<ExpirationInfoModel> _expirationsList;
        private string _description;
        private int _actualQty;
        private bool _addToPnl;
        private bool _active;
        public string _higherDerivedSymbol;
        public double _derivedPercentage;
        public string _LowerDerivedSymbol;
        public string _derivedSymbol;
        private double _underLast = double.NaN;
        public string _symbol;
        private double _manualUnrealPnl;
        private double _unrealPnl;
        private double _manualRealPnl;
        private double _timeValue;
        private double _intrinsicValue;
        private double _fVDivs;
        private double _uPrice;
        private double _uTheo;
        private double _uFwd;
        private double _uFwdFactor;
        private double _borrowCost;
        private double _borrowRate;
        private double _lastBidTheoSpread;
        private double _lastAskTheoSpread;
        private double _bidTheoSpreadEma;
        private double _askTheoSpreadEma;
        private Side? _side;

        public OmsCore OmsCore { get; }
        public PortfolioManagerModel PortfolioManager { get; }
        public BasketTraderViewModel ParentBasket { get; private set; }
        public bool IsBasket => ParentBasket != null;
        public SecurityType SecurityType { get; set; }
        public double Multiplier => SecurityType == SecurityType.Option ? 100 : 1;
        public bool IsDisposed { get; set; }
        public double TotalCommissions { get; }
        public int LastQuantity { get; set; }
        public double AveragePrice { get; set; }
        public Option Security { get; set; }
        public DerivedValueConfigModel DerivedModel { get; private set; }
        public DateTime DeltaAdjTheoTime { get; set; }
        public string Underlying
        {
            get => _underlying;
            set => _underlying = OptionsHelper.IsIndex(value) ? "$" + value : value;
        }
        Security IComplexOrderLegMin.Security { get; }


        public void Clone(IComplexOrderLeg other)
        {
            ExchangeFee2 = other.ExchangeFee2;
            ExchangeFee1 = other.ExchangeFee1;
            Fee2 = other.Fee2;
            Fee1 = other.Fee1;
            Delta = other.Delta;
            TV = other.TV;
            Ask = other.Ask;
            Bid = other.Bid;
            AveragePrice = other.AveragePrice;
            CumulativeQuantity = other.CumulativeQuantity;
            LastQuantity = other.LastQuantity;
            LeavesQuantity = other.LeavesQuantity;
            LastPrice = other.LastPrice;
            OrderStatus = other.OrderStatus;
            UTheo = other.UTheo;
            Side = other.Side;
            Quantity = other.Quantity;
            Ratio = other.Ratio;
            PositionEffect = other.PositionEffect;
            LegID = other.LegID;
            ExecutionID = other.ExecutionID;
            OrderID = other.OrderID;
            PermID = other.PermID;
            LastExchange = other.LastExchange;
            TransactionID = other.TransactionID;
            Timestamp = other.Timestamp;
            LastUpdateTime = other.LastUpdateTime;
            BrokerFee1 = other.BrokerFee1;
            BrokerFee2 = other.BrokerFee2;
            HanweckTV = other.HanweckTV;
            HanweckGamma = other.HanweckGamma;
            HanweckVega = other.HanweckVega;
            HanweckTheta = other.HanweckTheta;
            HanweckRho = other.HanweckRho;
            HanweckIV = other.HanweckIV;
            HanweckUnder = other.HanweckUnder;
            HanweckUnderBid = other.HanweckUnderBid;
            HanweckUnderAsk = other.HanweckUnderAsk;
            HanweckBid = other.HanweckBid;
            HanweckAsk = other.HanweckAsk;
            VolaTheo = other.VolaTheo;
            VolaTheoAdj = other.VolaTheoAdj;
            DeltaAdjustedTheo = other.DeltaAdjustedTheo;
            Ema = other.Ema;
            BidSize = other.BidSize;
            AskSize = other.AskSize;
            MinimumTickStyle = other.MinimumTickStyle;
            HanweckBidTime = other.HanweckBidTime;
            HanweckAskTime = other.HanweckAskTime;
            HanweckTimestamp = other.HanweckTimestamp;
            TimeValue = other.TimeValue;
            IntrinsicValue = other.IntrinsicValue;
            FVDivs = other.FVDivs;
            UFwd = other.UFwd;
            UFwdFactor = other.UFwdFactor;
            BorrowCost = other.BorrowCost;
            BorrowRate = other.BorrowRate;
            UPrice = other.UPrice;
        }


        public double VolaTheoAdj { get; set; }

        public double VolaPriceMetricV0
        {
            get => _volaPriceMetric;
            set => SetValue(ref _volaPriceMetric, value);
        }
        [Bindable]
        public partial uint VolaTheoSequenceV1 { get; set; }
        [Bindable]
        public partial double VolaTheoV1 { get; set; }
        [Bindable]
        public partial double VolaTheoAdjV1 { get; set; }
        [Bindable]
        public partial double VolaPriceMetricV1 { get; set; }
        [Bindable]
        public partial uint VolaTheoSequenceV2 { get; set; }
        [Bindable]
        public partial double VolaTheoV2 { get; set; }
        [Bindable]
        public partial double VolaTheoAdjV2 { get; set; }
        [Bindable]
        public partial double VolaPriceMetricV2 { get; set; }
        [Bindable]
        public partial uint VolaTheoSequenceV3 { get; set; }
        [Bindable]
        public partial double VolaTheoV3 { get; set; }
        [Bindable]
        public partial double VolaTheoAdjV3 { get; set; }
        [Bindable]
        public partial double VolaPriceMetricV3 { get; set; }

        // Position values

        public MinimumTickStyle MinimumTickStyle { get; set; }


        public double RealPnl => RealizedPL;
        public DateTime PutOnTime { get; internal set; }
        public DoubleUpdateModel LastDoubleUpdateModel { get; set; }
        public int TransactionID { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public double ExchangeFee2 { get; set; }
        public double ExchangeFee1 { get; set; }
        public double Fee2 { get; set; }
        public double Fee1 { get; set; }
        public double TV { get; set; }
        public double LastPrice { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public double HanweckTV { get; set; }
        public double HanweckGamma { get; set; }
        public double HanweckVega { get; set; }
        public double HanweckTheta { get; set; }
        public double HanweckRho { get; set; }
        public double HanweckIV { get; set; }
        public double HanweckUnder { get; set; }
        public double HanweckUnderBid { get; set; }
        public double HanweckUnderAsk { get; set; }
        public double HanweckBid { get; set; }
        public double HanweckAsk { get; set; }
        public double VolaTheo { get; set; }
        public string LegID { get; set; }
        public string ExecutionID { get; set; }
        public string OrderID { get; set; }
        public string PermID { get; set; }
        public string LastExchange { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime HanweckBidTime { get; set; }
        public DateTime HanweckAskTime { get; set; }
        public DateTime HanweckTimestamp { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public PositionEffect PositionEffect
        {
            get => GetPositionEffect();
            set => _ = value;
        }
        public ISecurityBook SecurityBook { get; }
        public double DeltaAdjustedTheo
        {
            get => DeltaAdjTheo;
            set => DeltaAdjTheo = value;
        }
        ZeroPlus.Models.Data.Securities.Security IComplexOrderLeg.Security { get; set; }
        public IList<ContraCapacity> ContraCapacities { get; set; }
        public IList<ContraBrokerName> ContraBrokerNames { get; set; }
        public IList<ContraCmta> ContraCmtas { get; set; }
        public IList<ContraTrader> ContraTraders { get; set; }
        public List<Side> Sides { get; } = [ZeroPlus.Models.Data.Enums.Side.Buy, ZeroPlus.Models.Data.Enums.Side.Sell];

        public TicketLegModel(OmsCore omsCore, string underlying, string account, BasketTraderViewModel basketTraderViewModel, PortfolioManagerModel portfolioManagerModel)
        {
            OmsCore = omsCore;
            PortfolioManager = portfolioManagerModel;

            SetupNotifiers();

            Underlying = underlying;
            Account = account;
            ParentBasket = basketTraderViewModel;
            TypesList = new ObservableCollection<string>(Enum.GetNames(typeof(Types)));
            PositionsList = new ObservableCollection<string>(Enum.GetNames(typeof(Positions)));
            ExpirationsList = new ObservableCollection<ExpirationInfoModel>();
            StrikesList = new ObservableCollection<StrikeInfoModel>();

            NetQtyInitialized = false;
            FirmNetQtyInitialized = false;
            LastDoubleUpdateModel = null;
            BidSize = 0;
            AskSize = 0;
            UnderLast = Double.NaN;
            MktMkrBid = double.NaN;
            MktMkrAsk = double.NaN;
            BestBid = double.NaN;
            BestAsk = double.NaN;
            TestValue = double.NaN;
            Bid = double.NaN;
            Ask = double.NaN;
            BidInterpolated = double.NaN;
            AskInterpolated = double.NaN;
            BidDerived = double.NaN;
            AskDerived = double.NaN;
            SkewAdjustedHighestBid = double.NaN;
            SkewAdjustedLowestAsk = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            HighestBidBase = double.NaN;
            LowestAskBase = double.NaN;
            HighestBidUnderlyingMid = double.NaN;
            LowestAskUnderlyingMid = double.NaN;
            BidDerivedInterpolated = double.NaN;
            AskDerivedInterpolated = double.NaN;
            Volume = double.NaN;
            Delta = double.NaN;
            Gamma = double.NaN;
            Vega = double.NaN;
            WeightedVega = double.NaN;
            Theta = double.NaN;
            NetDelta = double.NaN;
            SpreadEma = double.NaN;
            NetTheta = double.NaN;
            NetGamma = double.NaN;
            Rho = double.NaN;
            Implied = double.NaN;
            Theo = double.NaN;
            UnrealizedPL = double.NaN;
            TradingPL = double.NaN;
            TradingAveCost = double.NaN;
            NotionalValue = double.NaN;
            NetPL = double.NaN;
            MarketValue = double.NaN;
            DayPL = double.NaN;
            TradingSellAvePrice = double.NaN;
            RealizedPL = double.NaN;
            OpeningCost = double.NaN;
            MarkedCost = double.NaN;
            AveCost = double.NaN;
            TradingBuyAvePrice = double.NaN;
            Ema = double.NaN;
            DeltaAdjTheo = double.NaN;
            TheoBid = double.NaN;
            TheoAsk = double.NaN;
            SmoothedDeltaAdjTheo = double.NaN;
            VolaTheoV0 = double.NaN;
            VolaPriceMetricV0 = double.NaN;
            VolaPriceMetricV1 = double.NaN;
            VolaPriceMetricV2 = double.NaN;
            VolaPriceMetricV3 = double.NaN;
            VolaTheoAdjV0 = double.NaN;
            VolaIv = double.NaN;
            AdjDaEma = double.NaN;
            VolaEma = double.NaN;
            AdjVolaEma = double.NaN;
            DaEma = double.NaN;
            VolaTheoSequenceV1 = 0;
            VolaTheoV1 = double.NaN;
            VolaTheoAdjV1 = double.NaN;
            VolaTheoSequenceV2 = 0;
            VolaTheoV2 = double.NaN;
            VolaTheoAdjV2 = double.NaN;
            VolaTheoSequenceV3 = 0;
            VolaTheoV3 = double.NaN;
            VolaTheoAdjV3 = double.NaN;
            AdjTheoUnderlying = double.NaN;
            LockedTheo = double.NaN;
            LockedTheoUnderlying = double.NaN;
            LockedDeltaAdjTheo = double.NaN;
            DeltaAdjBidTheo = double.NaN;
            GammaAdjustedDelta = double.NaN;
            DeltaAdjAskTheo = double.NaN;
            LastBidTheoSpread = double.NaN;
            LastAskTheoSpread = double.NaN;
            BidTheoSpreadEma = double.NaN;
            AskTheoSpreadEma = double.NaN;
            UserNetQty = double.NaN;
            BidIvEma = double.NaN;
            AskIvEma = double.NaN;
            MidIvEma = double.NaN;
            BidEma = double.NaN;
            AdjBidEma = double.NaN;
            FullEma = double.NaN;
            AdjEma = double.NaN;
            AskEma = double.NaN;
            UnderEma = double.NaN;
            AdjAskEma = double.NaN;
            Ema = double.NaN;
            SpreadEma = double.NaN;
            EmaSpreadBid = double.NaN;
            EmaSpreadAsk = double.NaN;
            BidIvEma = double.NaN;
            AskIvEma = double.NaN;
            MidIvEma = double.NaN;
            TimeValue = double.NaN;
            IntrinsicValue = double.NaN;
            FVDivs = double.NaN;
            UPrice = double.NaN;
            UTheo = double.NaN;
            UFwd = double.NaN;
            UFwdFactor = double.NaN;
            BorrowCost = double.NaN;
            BorrowRate = double.NaN;
            TestValueSequence = DeltaAdjTheoSequence = 0;
            ZpTheoSequence = 0;
            DeltaAdjTheoTime = DateTime.MinValue;
            TradingNetQty = NetQty = FirmNetQty = TradingSellQty = TradingBuyQty = OpeningQty = 0;
            HanweckTime = "";
            InfoBits = 0;
            _lowerAskDerived = _lowerBidDerived = double.NaN;
            _lowerAskDerivedInterpolated = double.NaN;
            _lowerBidDerivedInterpolated = double.NaN;
            _higherAskDerived = double.NaN;
            _higherBidDerived = double.NaN;
            _higherAskDerivedInterpolated = double.NaN;
            _higherBidDerivedInterpolated = double.NaN;
            Active = true;
            AddToPnl = true;

            IsExpirationValid = IsStrikeValid = IsValid = false;
            Side = ZeroPlus.Models.Data.Enums.Side.Buy;
            ContraSide = ZeroPlus.Models.Data.Enums.Side.Sell;
            Type = Types.CALL.ToString();
            UpdateStrikeVisibility();
            Position = Positions.AUTO.ToString();
            Quantity = 1;
            Ratio = 1;
        }

        private void SetupNotifiers()
        {
            _notifiers = GetGeneratedNotifiers();
            _notifiersCount = _notifiers.Length;
        }

        private PositionEffect GetPositionEffect()
        {
            if (Enum.TryParse(Position, true, out Positions posEffect))
            {
                switch (posEffect)
                {
                    case Positions.AUTO:
                        return PositionEffect.AUTO;
                    case Positions.OPEN:
                        return PositionEffect.Open;
                    case Positions.CLOSE:
                        return PositionEffect.Close;
                    default:
                        return PositionEffect.AUTO;
                }
            }

            return PositionEffect.AUTO;
        }

        public async Task LoadExpirationsListAsync()
        {
            List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);

            if (options.Count > 0)
            {
                HashSet<Tuple<DateTime, string>> alreadyLoadedExpiration = new();
                foreach (Option option in options.OrderBy(x => x.Expiration))
                {
                    string rootSymbol = option.RootSymbol;
                    Tuple<DateTime, string> key = Tuple.Create(option.Expiration, rootSymbol);
                    if (alreadyLoadedExpiration.Add(key))
                    {
                        ExpirationsList.Add(new ExpirationInfoModel(option.Expiration, rootSymbol));
                    }
                }
            }
        }

        internal void Dispose()
        {
            try
            {
                IsDisposed = true;
                UnsubscribeFromDataFeed(final: true);
                ParentBasket = null;
                _notifiers = null;
                DisposeGeneratedNotifiers();
                Symbol = null;
                Security = null;
                Description = null;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        public void ExpirationChanged()
        {
            try
            {
                if (Type == Types.STOCK.ToString())
                {
                    return;
                }

                UnsubscribeFromDataFeed();
                UpdateStrikeVisibility();
                UpdateStrikesList();
                ValidateLegAsync();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(ExpirationChanged)} -> Unknown exception.");
            }
        }

        public async void UpdateStrikesList()
        {
            try
            {
                List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                UpdateStrikesList(options);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UpdateStrikesList)} -> Unknown exception.");
            }
        }

        public void UpdateStrikesList(List<Option> options)
        {
            try
            {
                if (ExpirationInfo != null)
                {
                    List<double> strikes = OmsCore.QuoteClient.OptionsLookup.GetOptionsWithExpiration(Underlying, ExpirationInfo.Expiration).Select(x => x.Strike).Distinct().OrderBy(x => x).ToList();
                    if (strikes.Count > 0)
                    {
                        StrikesList.Clear();
                        foreach (double strike in strikes)
                        {
                            bool isUnique = options.Count(x => Math.Abs(x.Strike - strike) < STRIKE_TOLERANCE) <= 2;
                            StrikeInfoModel strikeInfo = new(isUnique, strike);
                            StrikesList.Add(strikeInfo);
                        }
                    }
                }
                else
                {
                    if (options.Count > 0)
                    {
                        StrikesList.Clear();
                        foreach (double strike in options.Select(x => x.Strike).Distinct().OrderBy(x => x))
                        {
                            bool isUnique = options.Count(x => Math.Abs(x.Strike - strike) < STRIKE_TOLERANCE) <= 2;
                            StrikeInfoModel strikeInfo = new(isUnique, strike);
                            StrikesList.Add(strikeInfo);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UpdateStrikesList)} -> Unknown exception.");
            }
        }

        internal async Task OnTypeChange()
        {
            UnsubscribeFromDataFeed();
            if (Type == Types.STOCK.ToString())
            {
                ExpirationInfo = null;
                ExpirationsList.Clear();
                StrikeVisible = false;
                StrikesList.Clear();
            }
            else
            {
                UpdateStrikeVisibility();
                List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);

                if (options.Count > 0)
                {
                    HashSet<Tuple<DateTime, string>> alreadyLoadedExpiration = new();
                    foreach (Option option in options.OrderBy(x => x.Expiration))
                    {
                        string rootSymbol = option.RootSymbol;
                        Tuple<DateTime, string> key = Tuple.Create(option.Expiration, rootSymbol);
                        if (alreadyLoadedExpiration.Add(key))
                        {
                            ExpirationsList.Add(new ExpirationInfoModel(option.Expiration, rootSymbol));
                        }
                    }

                    UpdateStrikesList();
                }
            }

            ValidateLegAsync();
        }

        internal void UpdateStrikeVisibility()
        {
            StrikeVisible = Strike != 0;
        }

        internal void OnStrikeChange()
        {
            UnsubscribeFromDataFeed();
            UpdateStrikeVisibility();
            ValidateLegAsync();
        }

        internal void Reverse()
        {
            var side = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
            var contraSide = side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
            Side = side;
            ContraSide = contraSide;
        }

        internal async Task FlipCPAsync()
        {
            Type = Type == Types.CALL.ToString() ? Types.PUT.ToString() : Type == Types.PUT.ToString() ? Types.CALL.ToString() : Type;
            await OnTypeChange();
        }

        internal void Flat(int qty)
        {
            Quantity = qty;
        }

        internal void Flat()
        {
            if (OpenedPosition())
            {
                Quantity = Ratio;
            }
            else
            {
                if (OmsCore.Config.UseFirmNetQtyForFlat)
                {
                    Quantity = Math.Abs(FirmNetQty);
                }
                else
                {
                    Quantity = Math.Abs(NetQty);
                }
            }
        }

        internal bool OpenedPosition()
        {
            var netQty = NetQty;

            if (OmsCore.Config.UseFirmNetQtyForFlat)
            {
                netQty = FirmNetQty;
            }

            return (Side == ZeroPlus.Models.Data.Enums.Side.Buy && netQty >= 0) ||
                   (Side == ZeroPlus.Models.Data.Enums.Side.Sell && netQty <= 0);
        }

        internal async Task OppCP(HashSet<double> strikes = null)
        {
            if (IsBasket)
            {
                if (OmsCore.Config.UseMoneynessForOppCpInBasketsV2)
                {
                    await OppCPMoneyNess(strikes);
                    return;
                }
            }
            else
            {
                if (OmsCore.Config.UseMoneynessForOppCpInTicketsV2)
                {
                    await OppCPMoneyNess(strikes);
                    return;
                }
            }

            if (Type == Types.STOCK.ToString())
            {
                return;
            }

            OptionType type = Type == Types.CALL.ToString() ? OptionType.PUT : OptionType.CALL;
            List<Option> fullSymbols = await OmsCore.QuoteClient.GetSymbols(Underlying);
            List<Option> expSymbols = fullSymbols.Where(x => x.Type == type && x.Expiration == ExpirationInfo.Expiration).ToList();

            if (strikes != null && strikes.Count > 1)
            {
                List<Option> matching = expSymbols.Where(x => strikes.Contains(x.Strike)).ToList();
                if (matching.Count > 1)
                {
                    expSymbols = matching;
                }
            }

            Option selected = null;
            DataStore deltaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
            deltaStore.GetHanweckDataFor(expSymbols, SubscriptionFieldType.Delta);

            double minChange = double.MaxValue;
            foreach (Option symbol in expSymbols)
            {
                double delta = await deltaStore.GetDataAsync(symbol.OptionSymbol);
                double currentChange = Math.Abs(delta + Delta);
                if (currentChange < minChange)
                {
                    selected = symbol;
                    minChange = currentChange;
                    if (currentChange == 0)
                    {
                        break;
                    }
                }
            }

            deltaStore.Dispose();

            if (selected == null)
            {
                throw new SlimException("Opposite strike search failed.");
            }

            UnsubscribeFromDataFeed();

            Type = selected.Type.ToString();
            bool isUnique = fullSymbols.Count(x => Math.Abs(x.Strike - selected.Strike) < TOLERANCE) <= 2;
            Strike = new StrikeInfoModel(isUnique, selected.Strike);

            if (!StrikesList.Contains(Strike))
            {
                StrikesList.Add(Strike);
            }

            OnStrikeChange();
        }

        internal async Task OppCPMoneyNess(HashSet<double> strikes = null)
        {
            try
            {
                if (Type == Types.STOCK.ToString())
                {
                    return;
                }

                OptionType type = Type == Types.CALL.ToString() ? OptionType.PUT : OptionType.CALL;
                List<Option> fullSymbols = await OmsCore.QuoteClient.GetSymbols(Underlying);
                List<Option> expSymbols = fullSymbols.Where(x => x.Type == type && x.Expiration == ExpirationInfo.Expiration).ToList();

                if (strikes != null && strikes.Count > 1)
                {
                    List<Option> matching = expSymbols.Where(x => strikes.Contains(x.Strike)).ToList();
                    if (matching.Count > 1)
                    {
                        expSymbols = matching;
                    }
                }

                if (double.IsNaN(UnderLast))
                {
                    throw new SlimException("Opposite strike search failed.");
                }

                double moneyNess = (UnderLast - Strike.Strike) / Strike.Strike;
                double oppStrike = UnderLast / (1 - moneyNess);
                Option selected = expSymbols.MinBy(opt => Math.Abs(oppStrike - opt.Strike));

                if (selected == null)
                {
                    throw new SlimException("Opposite strike search failed.");
                }

                UnsubscribeFromDataFeed();

                Type = selected.Type.ToString();
                bool isUnique = fullSymbols.Count(x => Math.Abs(x.Strike - selected.Strike) < TOLERANCE) <= 2;
                Strike = new StrikeInfoModel(isUnique, selected.Strike);

                if (!StrikesList.Contains(Strike))
                {
                    StrikesList.Add(Strike);
                }

                OnStrikeChange();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OppCPMoneyNess));
            }
        }

        internal async Task ValidateLegAsync(bool ignoreOptionChainLookup = false)
        {
            try
            {
                await ValidateExpiration();
                ValidateStrike();

                if (string.IsNullOrWhiteSpace(Underlying) || !IsExpirationValid || !IsStrikeValid)
                {
                    IsValid = false;
                }
                else
                {
                    if (Type == Types.STOCK.ToString())
                    {
                        IsValid = true;
                        SecurityType = SecurityType.Stock;
                        Symbol = Description = Underlying;
                    }
                    else
                    {
                        SecurityType = SecurityType.Option;
                        if (ignoreOptionChainLookup)
                        {
                            IsValid = true;
                        }
                        else
                        {
                            if (!OmsCore.QuoteClient.OptionsLookup.Contains(Underlying))
                            {
                                await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
                            }
                            IsValid = OmsCore.QuoteClient.OptionsLookup
                                .GetOptionsWithExpiration(Underlying, ExpirationInfo.Expiration).Select(x => x.Strike)
                                .Distinct().Contains(Strike.Strike);
                        }

                        if (IsValid)
                        {
                            Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                            Description = Underlying + " " + ExpirationInfo.Expiration.ToString("MMM-dd-yy") + " " + Strike + " " + Type;
                            await LoadDerivedSymbol();
                        }
                    }
                }

                if (IsValid)
                {
                    SubscribeToDataFeedAsync();
                }
                else
                {
                    Symbol = null;
                    Security = null;
                    Description = null;
                }

                if (!ignoreOptionChainLookup)
                {
                    LegUpdatedEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                IsValid = false;
                _log.Error(ex, $"{nameof(ValidateLegAsync)} -> Not a valid leg.");
            }
        }

        private async Task LoadDerivedSymbol()
        {
            try
            {
                if (OmsCore.Config.DerivedValueConfigModelLookup.TryGetValue(Underlying, out DerivedValueConfigModel derivedModel))
                {
                    if (derivedModel.LoadDerivatives)
                    {
                        if (derivedModel.Multiplier != 0)
                        {
                            await OmsCore.QuoteClient.GetSymbolsAsync(derivedModel.DerivedSymbol).ContinueWith(t =>
                            {
                                double derivedStrike = Strike.Strike / derivedModel.Multiplier;
                                List<Option> options = OmsCore.QuoteClient.OptionsLookup.GetOptionsWithExpiration(derivedModel.DerivedSymbol, ExpirationInfo.Expiration);
                                if (options.Count > 0)
                                {
                                    Option selected = options.FirstOrDefault(x => x.Strike == derivedStrike);
                                    if (selected != null)
                                    {
                                        DerivedSymbol = OptionsHelper.GetSymbolFromComponents(selected.RootSymbol, selected.Expiration, Type, selected.Strike);
                                    }
                                    else
                                    {
                                        Option lower = options.Where(x => x.Strike < derivedStrike).OrderByDescending(x => x.Strike).FirstOrDefault();
                                        Option higher = options.Where(x => x.Strike > derivedStrike).OrderBy(x => x.Strike).FirstOrDefault();
                                        if (lower != null && higher != null)
                                        {
                                            LowerDerivedSymbol = OptionsHelper.GetSymbolFromComponents(lower.RootSymbol, ExpirationInfo.Expiration, Type, lower.Strike);
                                            HigherDerivedSymbol = OptionsHelper.GetSymbolFromComponents(higher.RootSymbol, ExpirationInfo.Expiration, Type, higher.Strike);
                                            DerivedSymbol = OptionsHelper.GetSymbolFromComponents(higher.RootSymbol, ExpirationInfo.Expiration, Type, Math.Round(derivedStrike, 2));
                                            _derivedPercentage = (derivedStrike - lower.Strike) / (higher.Strike - lower.Strike);
                                            DerivedModel = derivedModel;
                                        }
                                        else
                                        {
                                            _log.Warn(nameof(LoadDerivedSymbol) + "No derivitive found. Symbol: " + Symbol);
                                        }
                                    }
                                }
                            });
                        }
                        else
                        {
                            _log.Warn(nameof(LoadDerivedSymbol) + " invalid multiplier. Symbol: " + Underlying);
                        }
                    }
                    else
                    {
                        _log.Warn(nameof(LoadDerivedSymbol) + " disabled by model. Symbol: " + Underlying);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadDerivedSymbol));
            }
        }

        private async Task ValidateExpiration()
        {
            try
            {
                if (ExpirationInfo != null && ExpirationInfo.Expiration.Date < DateTime.Today.Date)
                {
                    var validExpirations = ExpirationsList.Where(x => x.Expiration.Date >= DateTime.Today.Date);
                    if (!validExpirations.Any())
                    {
                        await LoadExpirationsListAsync();
                    }
                    ExpirationInfo = ExpirationsList.Where(x => x.Expiration.Date >= DateTime.Today.Date).OrderBy(x => x.Expiration).FirstOrDefault();

                }
                IsExpirationValid = Type == Types.STOCK.ToString() || ExpirationInfo != null;
            }
            catch (Exception ex)
            {
                IsExpirationValid = false;
                _log.Error(ex, $"{nameof(ValidateExpiration)} -> Not a valid expiration.");
            }
        }

        private void ValidateStrike()
        {
            try
            {
                IsStrikeValid = Type == Types.STOCK.ToString() || StrikesList.Contains(Strike);
            }
            catch (Exception ex)
            {
                IsStrikeValid = false;
                _log.Error(ex, $"{nameof(ValidateStrike)} -> Not a valid strike.");
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (IsDisposed)
                {
                    return;
                }

                bool notifyTicket = false;
                string symbol = key.Symbol;
                SubscriptionFieldType fieldType = key.Type;
                if (symbol == Underlying)
                {
                    switch (fieldType)
                    {
                        case SubscriptionFieldType.MidPoint:
                            if (value is double midPoint)
                            {
                                BestBuyPriceAdj = ((midPoint - BestBuyPriceUnder) * Delta) + BestBuyPrice;
                                BestSellPriceAdj = ((midPoint - BestSellPriceUnder) * Delta) + BestSellPrice;
                            }
                            break;
                        case SubscriptionFieldType.LastPrice:
                            if (value is double last)
                            {
                                UnderLast = last;
                            }
                            break;
                    }
                }
                if (Symbol == symbol)
                {
                    switch (fieldType)
                    {
                        case SubscriptionFieldType.FirmSymbolPosition when value is IPosition position:
                            FirmNetQty = position.NetQty;
                            FirmNetQtyInitialized = true;
                            break;
                        case SubscriptionFieldType.IbQuote:
                            if (value is IbQuoteUpdateModel ibQuoteUpdate)
                            {
                                Bid = ibQuoteUpdate.Bid;
                                Ask = ibQuoteUpdate.Ask;
                                BidSize = ibQuoteUpdate.BidSize;
                                AskSize = ibQuoteUpdate.AskSize;
                                Implied = ibQuoteUpdate.ImpliedVolatility;
                                Delta = ibQuoteUpdate.Delta;
                                Theo = ibQuoteUpdate.OptPrice;
                                DeltaAdjTheo = ibQuoteUpdate.OptPrice;
                                DeltaAdjTheoSequence = 1;
                                Gamma = ibQuoteUpdate.Gamma;
                                Vega = ibQuoteUpdate.Vega;
                                Theta = ibQuoteUpdate.Theta;
                                UnderLast = ibQuoteUpdate.UndPrice;
                                notifyTicket = true;
                            }
                            break;
                        case SubscriptionFieldType.TopQuote:
                            if (value is DoubleUpdateModel doubleUpdateModel)
                            {
                                LastDoubleUpdateModel = doubleUpdateModel;
                                if (Bid != doubleUpdateModel.Bid ||
                                    Ask != doubleUpdateModel.Ask)
                                {
                                    notifyTicket = true;
                                    double bidUpdate = doubleUpdateModel.Bid;
                                    double askUpdate = doubleUpdateModel.Ask;

                                    if (bidUpdate > askUpdate && SecurityType != SecurityType.Stock)
                                    {
                                        bidUpdate = double.NaN;
                                        askUpdate = double.NaN;
                                    }

                                    Bid = bidUpdate;
                                    Ask = askUpdate;

                                    Mid = (Bid + Ask) / 2;
                                }
                            }
                            break;
                        case SubscriptionFieldType.Bid:
                            if (value is double bid)
                            {
                                notifyTicket = true;
                                if (bid > Ask && SecurityType != SecurityType.Stock)
                                {
                                    bid = double.NaN;
                                }
                                Bid = bid;
                                Mid = (Bid + Ask) / 2;
                            }
                            break;
                        case SubscriptionFieldType.Ask:
                            if (value is double ask)
                            {
                                notifyTicket = true;

                                if (Bid > ask && SecurityType != SecurityType.Stock)
                                {
                                    ask = double.NaN;
                                }
                                else if (ask == 0.0)
                                {
                                    ask = double.NaN;
                                }
                                Ask = ask;
                                Mid = (Bid + Ask) / 2;
                            }
                            break;
                        case SubscriptionFieldType.BidSize:
                            if (value is double bidSize)
                            {
                                notifyTicket = true;
                                BidSize = double.IsNaN(bidSize) ? 0 : Convert.ToInt32(bidSize);
                            }
                            break;
                        case SubscriptionFieldType.AskSize:
                            if (value is double askSize)
                            {
                                notifyTicket = true;
                                AskSize = double.IsNaN(askSize) ? 0 : Convert.ToInt32(askSize);
                            }
                            break;
                        case SubscriptionFieldType.DeltaAdjTheo:
                            if (value is double deltaAdjTheo)
                            {
                                notifyTicket = true;
                                DeltaAdjTheo = deltaAdjTheo;
                            }
                            else if (value is DeltaAdjTheo adjTheo)
                            {
                                notifyTicket = true;
                                switch (adjTheo.ModelId)
                                {
                                    case 0:
                                        DeltaAdjTheo = adjTheo.DeltaAdjustedTheo;
                                        DeltaAdjTheoSequence = adjTheo.UpdateSequence;
                                        SmoothedDeltaAdjTheo = adjTheo.SmoothedDeltaAdjustedTheo;
                                        TheoJumpDetected = adjTheo.JumpDetected;
                                        AdjTheoUnderlying = adjTheo.Underlying;
                                        VolaTheoV0 = adjTheo.SecondaryTheo;
                                        VolaTheoAdjV0 = adjTheo.SecondaryTheoAdj;
                                        VolaIv = adjTheo.SecondaryVol;
                                        VolaPriceMetricV0 = adjTheo.PriceMetric;
                                        AdjDaEma = adjTheo.AdjDaEma;
                                        AdjVolaEma = adjTheo.AdjVolaEma;
                                        break;
                                    case 1:
                                        VolaTheoSequenceV1 = adjTheo.UpdateSequence;
                                        VolaTheoV1 = adjTheo.SecondaryTheo;
                                        VolaTheoAdjV1 = adjTheo.SecondaryTheoAdj;
                                        VolaPriceMetricV1 = adjTheo.PriceMetric;
                                        break;
                                    case 2:
                                        VolaTheoSequenceV2 = adjTheo.UpdateSequence;
                                        VolaTheoV2 = adjTheo.SecondaryTheo;
                                        VolaTheoAdjV2 = adjTheo.SecondaryTheoAdj;
                                        VolaPriceMetricV2 = adjTheo.PriceMetric;
                                        break;
                                    case 3:
                                        VolaTheoSequenceV3 = adjTheo.UpdateSequence;
                                        VolaTheoV3 = adjTheo.SecondaryTheo;
                                        VolaTheoAdjV3 = adjTheo.SecondaryTheoAdj;
                                        VolaPriceMetricV3 = adjTheo.PriceMetric;
                                        break;
                                }
                            }
                            break;
                        case SubscriptionFieldType.WeightedVega:
                            if (value is double weightedVega)
                            {
                                notifyTicket = true;
                                WeightedVega = weightedVega;
                            }
                            break;
                        case SubscriptionFieldType.ZpTheo:
                            if (value is DoubleUpdateModel zpTheoUpdate)
                            {
                                notifyTicket = true;
                                TheoBid = zpTheoUpdate.Bid;
                                TheoAsk = zpTheoUpdate.Ask;
                                ZpTheoSequence++;
                            }
                            break;
                        case SubscriptionFieldType.Dig:
                            if (value is DoubleUpdateModel digUpdate)
                            {
                                notifyTicket = true;
                                DigBid = digUpdate.Bid;
                                DigAsk = digUpdate.Ask;
                                DigBidSize = (uint)digUpdate.BidSize;
                                DigAskSize = (uint)digUpdate.AskSize;
                            }
                            break;
                        case SubscriptionFieldType.TheoToMarketSpread when value is TheoToMarketSpread theoToMarketSpread:
                            notifyTicket = true;
                            LastBidTheoSpread = theoToMarketSpread.LastBidTheoSpread;
                            LastAskTheoSpread = theoToMarketSpread.LastAskTheoSpread;
                            BidTheoSpreadEma = theoToMarketSpread.BidTheoSpreadEma;
                            AskTheoSpreadEma = theoToMarketSpread.AskTheoSpreadEma;
                            break;
                        case SubscriptionFieldType.DebugValue:
                            if (value is DeltaAdjTheo testTheo)
                            {
                                notifyTicket = true;
                                TestValue = testTheo.DeltaAdjustedTheo;
                                TestValueSequence = testTheo.UpdateSequence;
                            }
                            break;
                        case SubscriptionFieldType.DeltaAdjBidTheo:
                            if (value is double deltaAdjBidTheo)
                            {
                                notifyTicket = true;
                                DeltaAdjBidTheo = deltaAdjBidTheo;
                            }
                            break;
                        case SubscriptionFieldType.DeltaAdjAskTheo:
                            if (value is double deltaAdjAskTheo)
                            {
                                notifyTicket = true;
                                DeltaAdjAskTheo = deltaAdjAskTheo;
                            }
                            break;
                        case SubscriptionFieldType.DerivedValues:
                            if (value is DerivedValueUpdateModel updateModel)
                            {
                                notifyTicket = true;
                                BidInterpolated = updateModel.InterpolatedBidUpdate;
                                AskInterpolated = updateModel.InterpolatedAskUpdate;
                                BestBid = updateModel.BestBidUpdate;
                                BestAsk = updateModel.BestAskUpdate;
                                MktMkrBid = updateModel.CustTradeBid;
                                MktMkrAsk = updateModel.CustTradeAsk;
                                if (updateModel.HighestBidLowestAskResult != null)
                                {
                                    SkewAdjustedHighestBid = updateModel.HighestBidLowestAskResult.SkewAdjustedHighestBid;
                                    SkewAdjustedLowestAsk = updateModel.HighestBidLowestAskResult.SkewAdjustedLowestAsk;
                                    HighestBid = updateModel.HighestBidLowestAskResult.HighestBid;
                                    LowestAsk = updateModel.HighestBidLowestAskResult.LowestAsk;
                                    HighestBidBase = updateModel.HighestBidLowestAskResult.HighestBidBase;
                                    LowestAskBase = updateModel.HighestBidLowestAskResult.LowestAskBase;
                                    HighestBidUnderlyingMid = updateModel.HighestBidLowestAskResult.HighestBidUnderlyingMid;
                                    LowestAskUnderlyingMid = updateModel.HighestBidLowestAskResult.LowestAskUnderlyingMid;
                                    HighestBidTime = new DateTime((long)updateModel.HighestBidLowestAskResult.HighestBidTime);
                                    LowestAskTime = new DateTime((long)updateModel.HighestBidLowestAskResult.LowestAskTime);
                                }
                            }
                            break;
                        case SubscriptionFieldType.Ema:
                            if (value is double ema)
                            {
                                notifyTicket = true;
                                Ema = ema;
                                double spread = SpreadEma / 2;
                                EmaSpreadBid = ema - spread;
                                EmaSpreadAsk = ema + spread;
                            }
                            break;
                        case SubscriptionFieldType.FullEma:
                            if (value is EmaUpdateModel emaUpdateModel)
                            {
                                notifyTicket = true;
                                EmaSequence = emaUpdateModel.Sequence;
                                UnderEma = emaUpdateModel.MidPeriodEmaUnderlying;
                                switch (OmsCore.Config.LiveEmaPeriod)
                                {
                                    case LiveEmaPeriod.Low:
                                        AdjEma = emaUpdateModel.LowPeriodEmaAdj;
                                        FullEma = emaUpdateModel.LowPeriodEma;
                                        break;
                                    case LiveEmaPeriod.Mid:
                                        BidEma = emaUpdateModel.MidPeriodBidEma;
                                        AdjBidEma = emaUpdateModel.MidPeriodBidEmaAdj;
                                        AdjEma = emaUpdateModel.MidPeriodEmaAdj;
                                        FullEma = emaUpdateModel.MidPeriodEma;
                                        AskEma = emaUpdateModel.MidPeriodAskEma;
                                        AdjAskEma = emaUpdateModel.MidPeriodAskEmaAdj;
                                        break;
                                    case LiveEmaPeriod.High:
                                        AdjEma = emaUpdateModel.HighPeriodEmaAdj;
                                        FullEma = emaUpdateModel.HighPeriodEma;
                                        break;
                                }
                            }
                            break;
                        case SubscriptionFieldType.SpreadEma:
                            if (value is double spreadEma)
                            {
                                notifyTicket = true;
                                SpreadEma = spreadEma;
                                double spread = spreadEma / 2;
                                ema = Ema;
                                EmaSpreadBid = ema - spread;
                                EmaSpreadAsk = ema + spread;
                            }
                            break;
                        case SubscriptionFieldType.VolaEma when value is APIEMAData volaEmaData 
                            && volaEmaData.Type is captureType.deltaadjvolatheo:
                            {
                                notifyTicket = true;
                                VolaEma = volaEmaData.EMA;
                            }
                            break;
                        case SubscriptionFieldType.DeltaAdjEma when value is APIEMAData daEmaData 
                            && daEmaData.Type is captureType.deltaadjoption:
                            {
                                notifyTicket = true;
                                DaEma = daEmaData.EMA;
                            }
                            break;
                        case SubscriptionFieldType.Volume:
                            if (value is double volume)
                            {
                                notifyTicket = true;
                                Volume = volume;
                            }
                            break;
                        case SubscriptionFieldType.OpenInterest:
                            if (value is double openInterest)
                            {
                                notifyTicket = false;
                                OpenInterest = openInterest;
                            }
                            break;
                        case SubscriptionFieldType.BidEma:
                        case SubscriptionFieldType.BidIvEma:
                        case SubscriptionFieldType.DerivedBidEma:
                            if (value is OptionPricingModel pricingModel)
                            {
                                notifyTicket = true;
                                BidIvEma = pricingModel.OptionPrice;
                            }
                            else if (value is double bidIvEma)
                            {
                                notifyTicket = true;
                                BidIvEma = bidIvEma;
                            }
                            break;
                        case SubscriptionFieldType.AskEma:
                        case SubscriptionFieldType.AskIvEma:
                        case SubscriptionFieldType.DerivedAskEma:
                            if (value is OptionPricingModel askPricingModel)
                            {
                                notifyTicket = true;
                                AskIvEma = askPricingModel.OptionPrice;
                            }
                            else if (value is double askIvEma)
                            {
                                notifyTicket = true;
                                AskIvEma = askIvEma;
                            }
                            break;
                        case SubscriptionFieldType.Greeks:
                            if (value is GreekUpdate greekUpdate)
                            {
                                if (Delta != greekUpdate.Delta)
                                {
                                    notifyTicket = true;
                                    Delta = greekUpdate.Delta;
                                }

                                if (Gamma != greekUpdate.Gamma)
                                {
                                    notifyTicket = true;
                                    Gamma = greekUpdate.Gamma;
                                }

                                if (Theta != greekUpdate.Theta)
                                {
                                    notifyTicket = true;
                                    Theta = greekUpdate.Theta;
                                }

                                if (Vega != greekUpdate.Vega)
                                {
                                    notifyTicket = true;
                                    Vega = greekUpdate.Vega;
                                }

                                if (Rho != greekUpdate.Rho)
                                {
                                    notifyTicket = true;
                                    Rho = greekUpdate.Rho;
                                }

                                if (Implied != greekUpdate.Implied)
                                {
                                    notifyTicket = true;
                                    Implied = greekUpdate.Implied;
                                }

                                if (Theo != greekUpdate.Theo)
                                {
                                    notifyTicket = true;
                                    Theo = greekUpdate.Theo;
                                }

                                if (HanweckTime != greekUpdate.HanweckTime)
                                {
                                    notifyTicket = true;
                                    HanweckTime = greekUpdate.HanweckTime;
                                    InfoBits = greekUpdate.InfoBits;
                                }
                                HanweckTimestamp = greekUpdate.HanweckTimeRaw;
                                TimeValue = greekUpdate.TimeValue;
                                IntrinsicValue = greekUpdate.IntrinsicValue;
                                FVDivs = greekUpdate.FVDivs;
                                UPrice = greekUpdate.UPrice;
                                UTheo = greekUpdate.UTheo;
                                UFwd = greekUpdate.UFwd;
                                UFwdFactor = greekUpdate.UFwdFactor;
                                BorrowCost = greekUpdate.BorrowCost;
                                BorrowRate = greekUpdate.BorrowRate;
                            }
                            break;
                        default:
                            return;
                    }
                }
                else if (_topQuoteSymbol == symbol && fieldType == SubscriptionFieldType.TopQuote && value is DoubleUpdateModel doubleUpdateModel)
                {
                    var mid = doubleUpdateModel.Mid * _topQuoteMultiplier;
                    LockedDeltaAdjTheo = (mid - LockedTheoUnderlying) * Delta + LockedTheo;
                }
                else if (DerivedModel != null)
                {
                    if (symbol == DerivedSymbol)
                    {
                        switch (key.Type)
                        {
                            case SubscriptionFieldType.Bid:
                                if (value is double bid)
                                {
                                    notifyTicket = true;
                                    BidDerived = bid * DerivedModel.Multiplier;
                                }
                                break;
                            case SubscriptionFieldType.Ask:
                                if (value is double ask)
                                {
                                    notifyTicket = true;
                                    AskDerived = ask * DerivedModel.Multiplier;
                                }
                                break;
                            case SubscriptionFieldType.BidInterpolated:
                                if (value is double bidInterpolated)
                                {
                                    notifyTicket = true;
                                    BidDerivedInterpolated = bidInterpolated * DerivedModel.Multiplier;
                                }
                                break;
                            case SubscriptionFieldType.AskInterpolated:
                                if (value is double askInterpolated)
                                {
                                    notifyTicket = true;
                                    AskDerivedInterpolated = askInterpolated * DerivedModel.Multiplier;
                                }
                                break;
                        }
                    }
                    else if (symbol == LowerDerivedSymbol)
                    {
                        switch (key.Type)
                        {
                            case SubscriptionFieldType.Bid:
                                if (value is double bid)
                                {
                                    notifyTicket = true;
                                    _lowerBidDerived = bid * DerivedModel.Multiplier;
                                    BidDerived = (_derivedPercentage * (_higherBidDerived - _lowerBidDerived)) + _lowerBidDerived;
                                }
                                break;
                            case SubscriptionFieldType.Ask:
                                if (value is double ask)
                                {
                                    notifyTicket = true;
                                    _lowerAskDerived = ask * DerivedModel.Multiplier;
                                    AskDerived = (_derivedPercentage * (_higherAskDerived - _lowerAskDerived)) + _lowerAskDerived;
                                }
                                break;
                            case SubscriptionFieldType.BidInterpolated:
                                if (value is double bidInterpolated)
                                {
                                    notifyTicket = true;
                                    _lowerBidDerivedInterpolated = bidInterpolated * DerivedModel.Multiplier;
                                    BidDerivedInterpolated = (_derivedPercentage * (_higherBidDerivedInterpolated - _lowerBidDerivedInterpolated)) + _lowerBidDerivedInterpolated;
                                }
                                break;
                            case SubscriptionFieldType.AskInterpolated:
                                if (value is double askInterpolated)
                                {
                                    notifyTicket = true;
                                    _lowerAskDerivedInterpolated = askInterpolated * DerivedModel.Multiplier;
                                    AskDerivedInterpolated = (_derivedPercentage * (_higherAskDerivedInterpolated - _lowerAskDerivedInterpolated)) + _lowerAskDerivedInterpolated;
                                }
                                break;
                        }
                    }
                    else if (symbol == HigherDerivedSymbol)
                    {
                        switch (key.Type)
                        {
                            case SubscriptionFieldType.Bid:
                                if (value is double bid)
                                {
                                    notifyTicket = true;
                                    _higherBidDerived = bid * DerivedModel.Multiplier;
                                    BidDerived = (_derivedPercentage * (_higherBidDerived - _lowerBidDerived)) + _lowerBidDerived;
                                }
                                break;
                            case SubscriptionFieldType.Ask:
                                if (value is double ask)
                                {
                                    notifyTicket = true;
                                    _higherAskDerived = ask * DerivedModel.Multiplier;
                                    AskDerived = (_derivedPercentage * (_higherAskDerived - _lowerAskDerived)) + _lowerAskDerived;
                                }
                                break;
                            case SubscriptionFieldType.BidInterpolated:
                                if (value is double bidInterpolated)
                                {
                                    notifyTicket = true;
                                    _higherBidDerivedInterpolated = bidInterpolated * DerivedModel.Multiplier;
                                    BidDerivedInterpolated = (_derivedPercentage * (_higherBidDerivedInterpolated - _lowerBidDerivedInterpolated)) + _lowerBidDerivedInterpolated;
                                }
                                break;
                            case SubscriptionFieldType.AskInterpolated:
                                if (value is double askInterpolated)
                                {
                                    notifyTicket = true;
                                    _higherAskDerivedInterpolated = askInterpolated * DerivedModel.Multiplier;
                                    AskDerivedInterpolated = (_derivedPercentage * (_higherAskDerivedInterpolated - _lowerAskDerivedInterpolated)) + _lowerAskDerivedInterpolated;
                                }
                                break;
                        }
                    }
                }

                if (notifyTicket)
                {
                    LegUpdatedEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue) + ". Symbol: " + key.Symbol + ", Type: " + key.Type);
            }
        }

        private void QtyChanged()
        {
            LegUpdatedEvent?.Invoke();
        }

        public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
        {
            try
            {
                string symbol = key.Item1;
                bool notifyTicket = false;
                if (value == null || symbol != Symbol)
                {
                    return;
                }
                else if (key.Item2 == OmsCore.User.Username && value is double userNetQty)
                {
                    if (UserNetQty != userNetQty)
                    {
                        UserNetQty = userNetQty;
                        notifyTicket = true;
                    }
                }
                else if (value is OMSSendPosition position)
                {
                    UnrealizedPL = position.UnrealizedPL;
                    TradingPL = position.TradingPL;
                    TradingNetQty = position.TradingNetQty;
                    TradingAveCost = position.TradingAveCost;
                    NotionalValue = position.NotionalValue;
                    NetQtyInitialized = true;
                    NetPL = position.NetPL;
                    MarketValue = position.MarketValue;
                    DayPL = position.DayPL;
                    TradingSellQty = position.TradingSellQty;
                    TradingSellAvePrice = position.TradingSellAvePrice;
                    TradingBuyQty = position.TradingBuyQty;
                    RealizedPL = position.RealizedPL;
                    OpeningQty = position.OpeningQty;
                    OpeningCost = position.OpeningCost;
                    MarkedCost = position.MarkedCost;
                    AveCost = position.AveCost;
                    TradingBuyAvePrice = position.TradingBuyAvePrice;
                    if (NetQty != position.NetQty)
                    {
                        NetQty = position.NetQty;
                        notifyTicket = true;
                    }
                }

                if (notifyTicket)
                {
                    LegUpdatedEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscibedPositionUpdateValue)}");
            }
        }

        internal async Task<TicketLegModel> LoadFromTemplateAsync(TicketLegModel legModel)
        {
            Type = legModel.Type;
            await OnTypeChange();
            Position = Positions.AUTO.ToString();
            Quantity = legModel.Quantity;
            Ratio = legModel.Ratio;
            Side = legModel.Side;
            ContraSide = Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
            await LoadExpirationsListAsync();
            ExpirationInfo = ExpirationsList.FirstOrDefault(x => x.Equals(legModel.ExpirationInfo));
            UpdateStrikesList();
            Strike = StrikesList.FirstOrDefault(x => x == legModel.Strike);
            OnStrikeChange();
            return legModel;
        }

        internal void UpdateRatio(int ratio)
        {
            try
            {
                Ratio = ratio;
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UpdateRatio)}");
            }
        }

        internal async Task<string> ExpirationUp(bool subscribeToData = true)
        {
            string message = "";
            try
            {
                if (SecurityType != SecurityType.Option)
                {
                    return message;
                }
                Option option = await OmsCore.QuoteClient.GetNextExpirationOption(Security, PermutationDirection.Up);
                if (option == null)
                {
                    throw new SlimException($"Expiration up failed for option: {Symbol}");
                }

                ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                if (!ExpirationsList.Contains(ExpirationInfo))
                {
                    ExpirationsList.Add(ExpirationInfo);
                }
                if (Strike != option.Strike)
                {
                    message = $"strike updated from {Strike} to {option.Strike}";
                    bool isUnique = false;
                    if (!IsBasket && subscribeToData)
                    {
                        List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                        isUnique = options.Count(x => x.Strike == option.Strike) <= 2;
                    }
                    Strike = new StrikeInfoModel(isUnique, option.Strike);
                    if (!StrikesList.Contains(Strike))
                    {
                        StrikesList.Add(Strike);
                    }
                }
                if (subscribeToData)
                {
                    ExpirationChanged();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    ExpirationChanged();
                }
                throw;
            }
            return message;
        }

        internal async Task<string> ExpirationDown(bool subscribeToData = true)
        {
            string message = "";
            try
            {
                if (SecurityType != SecurityType.Option)
                {
                    return message;
                }
                Option option = await OmsCore.QuoteClient.GetNextExpirationOption(Security, PermutationDirection.Down);
                if (option == null)
                {
                    throw new SlimException($"Expiration down failed for option: {Symbol}");
                }

                ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                if (!ExpirationsList.Contains(ExpirationInfo))
                {
                    ExpirationsList.Add(ExpirationInfo);
                }
                if (option != null && Strike != option.Strike)
                {
                    message = $"strike updated from {Strike} to {option.Strike}";
                    bool isUnique = false;
                    if (!IsBasket && subscribeToData)
                    {
                        List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                        isUnique = options.Count(x => x.Strike == option.Strike) <= 2;
                    }
                    Strike = new StrikeInfoModel(isUnique, option.Strike);
                    if (!StrikesList.Contains(Strike))
                    {
                        StrikesList.Add(Strike);
                    }
                }
                if (subscribeToData)
                {
                    ExpirationChanged();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    ExpirationChanged();
                }
                throw;
            }
            return message;
        }

        internal async Task StrikeUp(bool subscribeToData = true)
        {
            try
            {
                if (SecurityType != SecurityType.Option)
                {
                    return;
                }
                Option option = await OmsCore.QuoteClient.GetNextStrikeOption(Security, PermutationDirection.Up);
                if (option == null)
                {
                    throw new SlimException($"Strike up failed for option: {Symbol}");
                }

                if (ExpirationInfo == null || ExpirationInfo.Expiration != option.Expiration || ExpirationInfo.RootSymbol != option.RootSymbol)
                {
                    ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                    if (!ExpirationsList.Contains(ExpirationInfo))
                    {
                        ExpirationsList.Add(ExpirationInfo);
                    }
                }
                bool isUnique = false;
                if (!IsBasket && subscribeToData)
                {
                    List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                    isUnique = options.Count(x => x.Strike == option.Strike) <= 2;
                }
                Strike = new StrikeInfoModel(isUnique, option.Strike);
                if (!StrikesList.Contains(Strike))
                {
                    StrikesList.Add(Strike);
                }
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                throw;
            }
        }

        internal async Task StrikeDown(bool subscribeToData = true)
        {
            try
            {
                if (SecurityType != SecurityType.Option)
                {
                    return;
                }
                Option option = await OmsCore.QuoteClient.GetNextStrikeOption(Security, PermutationDirection.Down);
                if (option == null)
                {
                    throw new SlimException($"Strike down failed for option: {Symbol}");
                }

                if (ExpirationInfo == null || ExpirationInfo.Expiration != option.Expiration || ExpirationInfo.RootSymbol != option.RootSymbol)
                {
                    ExpirationInfo = new ExpirationInfoModel(option.Expiration, option.RootSymbol);
                    if (!ExpirationsList.Contains(ExpirationInfo))
                    {
                        ExpirationsList.Add(ExpirationInfo);
                    }
                }
                bool isUnique = false;
                if (!IsBasket && subscribeToData)
                {
                    List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                    isUnique = options.Count(x => x.Strike == option.Strike) <= 2;
                }
                Strike = new StrikeInfoModel(isUnique, option.Strike);
                if (!StrikesList.Contains(Strike))
                {
                    StrikesList.Add(Strike);
                }
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                throw;
            }
        }

        internal async Task UpdateStrike(double strike, bool subscribeToData = true)
        {
            try
            {
                bool isUnique = false;
                if (!IsBasket && subscribeToData)
                {
                    List<Option> options = await OmsCore.QuoteClient.GetSymbols(Underlying);
                    isUnique = options.Count(x => x.Strike == strike) <= 2;
                }
                Strike = new StrikeInfoModel(isUnique, strike);
                if (!StrikesList.Contains(Strike))
                {
                    StrikesList.Add(Strike);
                }
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                throw;
            }
        }

        internal void UpdateExpiration(DateTime expiration, bool subscribeToData = true)
        {
            try
            {
                ExpirationInfo = new ExpirationInfoModel(expiration, Security.RootSymbol);
                if (!ExpirationsList.Contains(ExpirationInfo))
                {
                    ExpirationsList.Add(ExpirationInfo);
                }
                if (subscribeToData)
                {
                    ExpirationChanged();
                }
                else
                {
                    Symbol = OptionsHelper.GetSymbolFromComponents(ExpirationInfo.RootSymbol, ExpirationInfo.Expiration, Type, Strike.Strike);
                }
            }
            catch (SlimException)
            {
                if (subscribeToData)
                {
                    OnStrikeChange();
                }
                throw;
            }
        }

        private void SubscribeToDataFeedAsync()
        {
            Task.Run(() =>
            {
                SubscribeToDataFeed();
                SubscribeToPositions();
            });
        }

        public void SubscribeToDataFeed(string source = "")
        {
            if (IsDisposed)
            {
                return;
            }

            string symbol = Symbol;
            string derivedSymbol = DerivedSymbol;
            string lowerDerivedSymbol = LowerDerivedSymbol;
            string higherDerivedSymbol = HigherDerivedSymbol;
            bool all = string.IsNullOrWhiteSpace(source);
            if (!all)
            {
                source = source.ToUpper();
            }

            if (!string.IsNullOrWhiteSpace(symbol))
            {
                if (Underlying != null && Underlying.Contains('\\'))
                {
                    return;
                }
                if (all || source == "MARKET DATA")
                {
                    SubscribeToMarketData(symbol);
                }
                if (all || source == "DERIVED")
                {
                    SubscribeToDerivedValues(derivedSymbol, lowerDerivedSymbol, higherDerivedSymbol);
                }
                if (all || source == "HANWECK")
                {
                    SubscribeToHanweck(symbol);
                }
                if (all || source == "DERIVATIVES")
                {
                    SubscribeToDerivatives(symbol);
                }
                if (all || source == "INTERPOLATED")
                {
                    SubscribeToInterpolated(symbol, derivedSymbol, lowerDerivedSymbol, higherDerivedSymbol);
                }
                if (all || source == "EMA")
                {
                    SubscribeEma(symbol);
                }
                if (all || source == "BESTEDGE")
                {
                    RequestBestEdgeMap(symbol);
                }
            }
            if (IsDisposed)
            {
                UnsubscribeFromDataFeed();
            }
        }

        internal void SubscribeToIbData(string key)
        {
            if (IsValid)
            {
                OmsCore.UpdateManager.Subscribe(Symbol + key, SubscriptionFieldType.IbQuote, this);
            }
        }

        private void SubscribeToMarketData(string symbol)
        {
            bool isBasket = ParentBasket != null;
            bool subscribe = !isBasket || ParentBasket.BasketSettings.SubscribeToMarketData;
            if (subscribe)
            {
                if (isBasket && ParentBasket.BasketSettings.DataType == DataType.Live && Type == "STOCK")
                {
                    OmsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.TopQuote, this);
                }
                else
                {
                    OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Ask, this);
                    OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.BidSize, this);
                    OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.AskSize, this);
                }
                SubscribeToTopQuote();
                OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.Volume, this);
                OmsCore.QuoteClient.Subscribe(symbol, SubscriptionFieldType.OpenInterest, this);
            }
        }

        internal void SubscribeToTopQuote()
        {
            bool isBasket = ParentBasket != null;
            if (isBasket && ParentBasket.BasketSettings.LockTheos)
            {
                if (OmsCore.Config.SymbolsLookup.TryGetValue(Underlying, out var tuple))
                {
                    _topQuoteSymbol = tuple.Item2;
                    _topQuoteMultiplier = tuple.Item3;
                }
                else
                {
                    _topQuoteSymbol = Underlying;
                    _topQuoteMultiplier = 1;
                }
                OmsCore.UpdateManager.Subscribe(_topQuoteSymbol, SubscriptionFieldType.TopQuote, this);
            }
        }

        private void SubscribeToDerivedValues(string derivedSymbol, string lowerDerivedSymbol, string higherDerivedSymbol)
        {
            bool subscribe = ParentBasket?.BasketSettings?.SubscribeToDerivedValues ?? true;
            if (subscribe)
            {
                if (!string.IsNullOrWhiteSpace(derivedSymbol))
                {
                    OmsCore.QuoteClient.Subscribe(derivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(derivedSymbol, SubscriptionFieldType.Ask, this);
                }
                if (!string.IsNullOrWhiteSpace(lowerDerivedSymbol))
                {
                    OmsCore.QuoteClient.Subscribe(lowerDerivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(lowerDerivedSymbol, SubscriptionFieldType.Ask, this);
                }
                if (!string.IsNullOrWhiteSpace(higherDerivedSymbol))
                {
                    OmsCore.QuoteClient.Subscribe(higherDerivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Subscribe(higherDerivedSymbol, SubscriptionFieldType.Ask, this);
                }
            }
        }

        private void SubscribeToHanweck(string symbol)
        {
            bool subscribe = ParentBasket?.BasketSettings?.SubscribeToHanweck ?? true;
            if (subscribe)
            {
                OmsCore.GreekClient.Subscribe(symbol, SubscriptionFieldType.Greeks, this);
            }
        }

        private void SubscribeToDerivatives(string symbol)
        {
            bool subscribe = ParentBasket?.BasketSettings?.SubscribeToDerivatives ?? true;
            if (subscribe)
            {
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DeltaAdjTheo, this);
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.WeightedVega, this);
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.ZpTheo, this);
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.Dig, this);
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.TheoToMarketSpread, this);
            }
        }

        private void SubscribeToInterpolated(string symbol, string derivedSymbol, string lowerDerivedSymbol, string higherDerivedSymbol)
        {
            bool subscribe = ParentBasket?.BasketSettings?.SubscribeToInterpolated ?? true;
            if (subscribe)
            {
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DerivedValues, this);
                if (!string.IsNullOrWhiteSpace(derivedSymbol))
                {
                    OmsCore.UpdateManager.Subscribe(derivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }
                if (!string.IsNullOrWhiteSpace(lowerDerivedSymbol))
                {
                    OmsCore.UpdateManager.Subscribe(lowerDerivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }
                if (!string.IsNullOrWhiteSpace(higherDerivedSymbol))
                {
                    OmsCore.UpdateManager.Subscribe(higherDerivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }
            }
        }

        public void SubscribeEma(string symbol)
        {
            if (ParentBasket == null || ParentBasket.BasketSettings.SubscribeToEma)
            {
                OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.FullEma, this);
                OmsCore.EmaServerClientModel.Subscribe(symbol, SubscriptionFieldType.DeltaAdjEma, this);
                OmsCore.EmaServerClientModel.Subscribe(symbol, SubscriptionFieldType.VolaEma, this);
            }
        }

        private async void RequestBestEdgeMap(string symbol)
        {
            try
            {
                if (IsBasket && (ParentBasket?.BasketSettings?.RequestBestEdge ?? false))
                {
                    if (!string.IsNullOrWhiteSpace(symbol) && ParentBasket != null)
                    {
                        int days = ParentBasket.BasketSettings.RequestBestEdgeDays;
                        if (days > 0)
                        {
                            IEnumerable<ZeroPlus.Models.Data.Edge.SymbolEdgeMap> edges = await OmsCore.HerculesClient.RequestSymbolEdgeMapAsync(symbol, DateTime.Today - TimeSpan.FromDays(days));
                            if (edges != null && edges.Any())
                            {
                                double sampleUnder = edges.OrderByDescending(x => x.Date).FirstOrDefault().BestBuyPriceUnderlying;

                                foreach (ZeroPlus.Models.Data.Edge.SymbolEdgeMap edge in edges)
                                {
                                    double adjBuy = ((sampleUnder - edge.BestBuyPriceUnderlying) * edge.BestBuyPriceDelta) + edge.BestBuyPrice;
                                    double adjSell = ((sampleUnder - edge.BestSellPriceUnderlying) * edge.BestSellPriceDelta) + edge.BestSellPrice;

                                    if ((double.IsNaN(BestBuyPrice) || adjBuy < BestBuyPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                                    {
                                        BestBuyPrice = edge.BestBuyPrice;
                                        BestBuyPriceUnder = edge.BestBuyPriceUnderlying;
                                        BestBuyPriceDelta = edge.BestBuyPriceDelta;
                                    }
                                    if ((double.IsNaN(BestSellPrice) || adjSell > BestSellPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                                    {
                                        BestSellPrice = edge.BestSellPrice;
                                        BestSellPriceUnder = edge.BestSellPriceUnderlying;
                                        BestSellPriceDelta = edge.BestSellPriceDelta;
                                    }
                                }
                                if (edges.Select(x => x.OpeningSide).Distinct().Count() == 1)
                                {
                                    BestOpeningSide = edges.FirstOrDefault()?.OpeningSide;
                                }
                                if (edges.Select(x => x.HardSide).Distinct().Count() == 1)
                                {
                                    BestHardSide = edges.FirstOrDefault()?.HardSide;
                                }
                                if (!double.IsNaN(BestBuyPrice) ||
                                    !double.IsNaN(BestSellPrice))
                                {
                                    OmsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.MidPoint, this);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestBestEdgeMap));
            }
        }

        public void SubscribeToPositions()
        {
            bool subscribe = ParentBasket == null && !IsDisposed;
            if (subscribe)
            {
                if (string.IsNullOrWhiteSpace(Account) && !string.IsNullOrWhiteSpace(OmsCore.Config.DefaultAccount))
                {
                    Account = OmsCore.Config.DefaultAccount;
                }

                OmsCore.OrderClient.UnsubscribeAllPosition(this);
                OmsCore.OrderClient.SubscribePosition(Symbol, Account, this);
                PortfolioManager?.Subscribe(Symbol, SubscriptionFieldType.FirmSymbolPosition, this);
            }
        }

        public void UnsubscribeFromDataFeed(bool final = false)
        {
            if (final)
            {
                OmsCore.UpdateManager.UnsubscribeAll(this);
                OmsCore.QuoteClient.UnsubscribeAll(this);
                OmsCore.GreekClient.UnsubscribeAll(this);
                OmsCore.OrderClient.UnsubscribeAllPosition(this);
                PortfolioManager?.UnsubscribeAll(this);
            }
            else
            {

                UnsubscribeFromDataSource("");
                UnsubscribePosition();
                Symbol = "";
                DerivedSymbol = "";
                LowerDerivedSymbol = "";
                HigherDerivedSymbol = "";
                DerivedModel = null;
                ResetLegValues();
            }
        }

        public void UnsubscribeFromDataSource(string source = "")
        {
            bool all = string.IsNullOrWhiteSpace(source);
            if (!all)
            {
                source = source.ToUpper();
            }
            string symbol = Symbol;

            if (Underlying != null && Underlying.Contains('\\'))
            {
                UnsubscribeIbData(symbol);
                return;
            }
            if (all || source == "MARKET DATA")
            {
                UnsubscribeMarketData(symbol, Underlying);
            }
            if (all || source == "DERIVED")
            {
                UnsubscribeDerived(symbol);
            }
            if (all || source == "HANWECK")
            {
                UnsubscribeHanweck(symbol);
            }
            if (all || source == "DERIVATIVES")
            {
                UnsubscribeDerivatives(symbol);
            }
            if (all || source == "INTERPOLATED")
            {
                UnsubscribeInterpolated(symbol);
            }
            if (all || source == "EMA")
            {
                UnsubscribeEma(symbol);
            }
            if (source == "BESTEDGE")
            {
                ResetBestEdgeMap();
            }
            LegUpdatedEvent?.Invoke();
        }

        private void ResetBestEdgeMap()
        {
            BestOpeningSide = null;
            BestHardSide = null;
            BestBuyPrice = double.NaN;
            BestBuyPriceAdj = double.NaN;
            BestBuyPriceUnder = double.NaN;
            BestBuyPriceDelta = double.NaN;
            BestSellPrice = double.NaN;
            BestSellPriceAdj = double.NaN;
            BestSellPriceUnder = double.NaN;
            BestSellPriceDelta = double.NaN;
            if (!string.IsNullOrWhiteSpace(Underlying))
            {
                OmsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.MidPoint, this);
            }
        }

        private void UnsubscribeMarketData(string symbol, string underlying)
        {
            try
            {
                if (ParentBasket != null &&
                        ParentBasket.BasketSettings.DataType == DataType.Live &&
                        Type == "STOCK")
                {
                    OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.TopQuote, this);
                }
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.Volume, this);
                OmsCore.QuoteClient.Unsubscribe(symbol, SubscriptionFieldType.OpenInterest, this);
                OmsCore.QuoteClient.Unsubscribe(underlying, SubscriptionFieldType.LastPrice, this);
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.IbQuote, this);
                UnsubscribeTopQuote();
                LastDoubleUpdateModel = null;
                Bid = double.NaN;
                Ask = double.NaN;
                UnderLast = double.NaN;
                BidSize = 0;
                AskSize = 0;
                Volume = double.NaN;
                OpenInterest = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeMarketData));
            }
        }

        internal void UnsubscribeTopQuote()
        {
            if (_topQuoteSymbol != null)
            {
                OmsCore.UpdateManager.Unsubscribe(_topQuoteSymbol, SubscriptionFieldType.TopQuote, this);
                _topQuoteSymbol = null;
            }
        }

        private void UnsubscribeDerived(string symbol)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(DerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(DerivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Unsubscribe(DerivedSymbol, SubscriptionFieldType.Ask, this);
                }

                if (!string.IsNullOrWhiteSpace(LowerDerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(LowerDerivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Unsubscribe(LowerDerivedSymbol, SubscriptionFieldType.Ask, this);
                }

                if (!string.IsNullOrWhiteSpace(HigherDerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(HigherDerivedSymbol, SubscriptionFieldType.Bid, this);
                    OmsCore.QuoteClient.Unsubscribe(HigherDerivedSymbol, SubscriptionFieldType.Ask, this);
                }

                BidDerived = AskDerived = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeDerived));
            }
        }

        private void UnsubscribeDerivatives(string symbol)
        {
            try
            {
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.DeltaAdjTheo, this);
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.WeightedVega, this);
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.ZpTheo, this);
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.Dig, this);
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.TheoToMarketSpread, this);
                DeltaAdjTheo = double.NaN;
                WeightedVega = double.NaN;
                TheoBid = double.NaN;
                TheoAsk = double.NaN;
                SmoothedDeltaAdjTheo = double.NaN;
                VolaTheoV0 = double.NaN;
                VolaTheoAdjV0 = double.NaN;
                VolaIv = double.NaN;
                VolaPriceMetricV0 = double.NaN;
                VolaPriceMetricV1 = double.NaN;
                VolaPriceMetricV2 = double.NaN;
                VolaPriceMetricV3 = double.NaN;
                VolaTheoSequenceV1 = 0;
                VolaTheoV1 = double.NaN;
                VolaTheoAdjV1 = double.NaN;
                VolaTheoSequenceV2 = 0;
                VolaTheoV2 = double.NaN;
                VolaTheoAdjV2 = double.NaN;
                VolaTheoSequenceV3 = 0;
                VolaTheoV3 = double.NaN;
                VolaTheoAdjV3 = double.NaN;
                AdjTheoUnderlying = double.NaN;
                LockedTheo = double.NaN;
                LockedTheoUnderlying = double.NaN;
                LockedDeltaAdjTheo = double.NaN;
                DeltaAdjBidTheo = double.NaN;
                DeltaAdjAskTheo = double.NaN;
                LastBidTheoSpread = double.NaN;
                LastAskTheoSpread = double.NaN;
                BidTheoSpreadEma = double.NaN;
                AskTheoSpreadEma = double.NaN;
                GammaAdjustedDelta = double.NaN;
                AdjDaEma = double.NaN;
                VolaEma = double.NaN;
                AdjVolaEma = double.NaN;
                DaEma = double.NaN;
                DeltaAdjTheoSequence = 0;
                ZpTheoSequence = 0;
                DigBid = double.NaN;
                DigAsk = double.NaN;
                DigBidSize = 0;
                DigAskSize = 0;
                DeltaAdjTheoTime = DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeDerivatives));
            }
        }

        private void UnsubscribeIbData(string symbol)
        {
            try
            {
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.IbQuote, this);
                Bid = double.NaN;
                Ask = double.NaN;
                BidSize = 0;
                AskSize = 0;
                Implied = double.NaN;
                Delta = double.NaN;
                Theo = double.NaN;
                DeltaAdjTheo = double.NaN;
                TheoBid = double.NaN;
                TheoAsk = double.NaN;
                SmoothedDeltaAdjTheo = double.NaN;
                VolaPriceMetricV0 = double.NaN;
                VolaPriceMetricV1 = double.NaN;
                VolaPriceMetricV2 = double.NaN;
                VolaPriceMetricV3 = double.NaN;
                VolaTheoV0 = double.NaN;
                VolaTheoAdjV0 = double.NaN;
                VolaIv = double.NaN;
                VolaTheoSequenceV1 = 0;
                VolaTheoV1 = double.NaN;
                VolaTheoAdjV1 = double.NaN;
                VolaTheoSequenceV2 = 0;
                VolaTheoV2 = double.NaN;
                VolaTheoAdjV2 = double.NaN;
                VolaTheoSequenceV3 = 0;
                VolaTheoV3 = double.NaN;
                VolaTheoAdjV3 = double.NaN;
                DeltaAdjTheoSequence = 0;
                ZpTheoSequence = 0;
                DigBid = double.NaN;
                DigAsk = double.NaN;
                DigBidSize = 0;
                DigAskSize = 0;
                AdjTheoUnderlying = double.NaN;
                LockedTheo = double.NaN;
                LockedTheoUnderlying = double.NaN;
                LockedDeltaAdjTheo = double.NaN;
                Gamma = double.NaN;
                Vega = double.NaN;
                Theta = double.NaN;
                UnderLast = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeIbData));
            }
        }

        private void UnsubscribeInterpolated(string symbol)
        {
            try
            {
                OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.DerivedValues, this);

                if (!string.IsNullOrWhiteSpace(DerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(DerivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }

                if (!string.IsNullOrWhiteSpace(LowerDerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(LowerDerivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }

                if (!string.IsNullOrWhiteSpace(HigherDerivedSymbol))
                {
                    OmsCore.QuoteClient.Unsubscribe(HigherDerivedSymbol, SubscriptionFieldType.InterpolatedQuote, this);
                }

                _lowerAskDerivedInterpolated = _lowerBidDerivedInterpolated = double.NaN;
                _higherAskDerivedInterpolated = _higherBidDerivedInterpolated = double.NaN;
                BidDerivedInterpolated = AskDerivedInterpolated = double.NaN;
                BidInterpolated = AskInterpolated = double.NaN;
                MktMkrBid = MktMkrAsk = BestBid = BestAsk = double.NaN;
                SkewAdjustedHighestBid = double.NaN;
                SkewAdjustedLowestAsk = double.NaN;
                HighestBid = double.NaN;
                LowestAsk = double.NaN;
                HighestBidBase = double.NaN;
                LowestAskBase = double.NaN;
                HighestBidTime = default;
                LowestAskTime = default;
                HighestBidUnderlyingMid = double.NaN;
                LowestAskUnderlyingMid = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeInterpolated));
            }
        }

        private void UnsubscribeHanweck(string symbol)
        {
            try
            {
                OmsCore.GreekClient.Unsubscribe(symbol, SubscriptionFieldType.Greeks, this);
                NetDelta = NetTheta = NetGamma = Delta = Gamma = Vega = Theta = Rho = Implied = Theo = double.NaN;
                HanweckTime = "";
                InfoBits = 0;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeHanweck));
            }
        }

        private void UnsubscribePosition()
        {
            try
            {
                OmsCore.OrderClient.UnsubscribePosition(Symbol, Account, this);
                OmsCore.OrderClient.UnsubscribePosition(Symbol, OmsCore.User.Username, this);
                PortfolioManager?.Unsubscribe(Symbol, SubscriptionFieldType.FirmSymbolPosition, this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribePosition));
            }
        }

        private void UnsubscribeEma(string symbol)
        {
            try
            {
                if (ParentBasket == null || ParentBasket.BasketSettings.SubscribeToEma)
                {
                    OmsCore.UpdateManager.Unsubscribe(symbol, SubscriptionFieldType.FullEma, this);
                    OmsCore.EmaServerClientModel.Unsubscribe(symbol, SubscriptionFieldType.DeltaAdjEma, this);
                    OmsCore.EmaServerClientModel.Unsubscribe(symbol, SubscriptionFieldType.VolaEma, this);
                }

                BidEma = AdjBidEma = FullEma = AdjEma = UnderEma = AskEma = AdjAskEma = Ema = SpreadEma = EmaSpreadBid = EmaSpreadAsk = BidIvEma = AskIvEma = MidIvEma = double.NaN;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeEma));
            }
        }

        internal void ResetLegValues()
        {
            _lowerAskDerived = _lowerBidDerived = double.NaN;
            _lowerAskDerivedInterpolated = _lowerBidDerivedInterpolated = double.NaN;
            _higherAskDerived = _higherBidDerived = double.NaN;
            _higherAskDerivedInterpolated = _higherBidDerivedInterpolated = double.NaN;
            NetQtyInitialized = false;
            FirmNetQtyInitialized = false;
            LastDoubleUpdateModel = null;
            BidEma = double.NaN;
            AdjBidEma = double.NaN;
            FullEma = double.NaN;
            AdjEma = double.NaN;
            UnderEma = double.NaN;
            AskEma = double.NaN;
            AdjAskEma = double.NaN;
            SpreadEma = double.NaN;
            NetDelta = double.NaN;
            NetTheta = double.NaN;
            NetGamma = double.NaN;
            Bid = double.NaN;
            Ask = double.NaN;
            UnderLast = double.NaN;
            BidSize = 0;
            AskSize = 0;
            BidInterpolated = double.NaN;
            AskInterpolated = double.NaN;
            MktMkrBid = double.NaN;
            MktMkrAsk = double.NaN;
            BestBid = double.NaN;
            BestAsk = double.NaN;
            BidDerived = double.NaN;
            AskDerived = double.NaN;
            SkewAdjustedHighestBid = double.NaN;
            SkewAdjustedLowestAsk = double.NaN;
            HighestBid = double.NaN;
            LowestAsk = double.NaN;
            HighestBidBase = double.NaN;
            LowestAskBase = double.NaN;
            HighestBidTime = default;
            LowestAskTime = default;
            HighestBidUnderlyingMid = double.NaN;
            LowestAskUnderlyingMid = double.NaN;
            BidDerivedInterpolated = double.NaN;
            AskDerivedInterpolated = double.NaN;
            Volume = double.NaN;
            OpenInterest = double.NaN;
            Delta = double.NaN;
            Gamma = double.NaN;
            Vega = double.NaN;
            WeightedVega = double.NaN;
            Theta = double.NaN;
            Rho = double.NaN;
            Implied = double.NaN;
            Theo = double.NaN;
            TimeValue = double.NaN;
            IntrinsicValue = double.NaN;
            FVDivs = double.NaN;
            UPrice = double.NaN;
            UTheo = double.NaN;
            UFwd = double.NaN;
            UFwdFactor = double.NaN;
            BorrowCost = double.NaN;
            BorrowRate = double.NaN;
            UnrealizedPL = double.NaN;
            TradingPL = double.NaN;
            TradingAveCost = double.NaN;
            NotionalValue = double.NaN;
            NetPL = double.NaN;
            MarketValue = double.NaN;
            DayPL = double.NaN;
            TradingSellAvePrice = double.NaN;
            RealizedPL = double.NaN;
            OpeningCost = double.NaN;
            MarkedCost = double.NaN;
            AveCost = double.NaN;
            TradingBuyAvePrice = double.NaN;
            Ema = double.NaN;
            DeltaAdjTheo = double.NaN;
            TheoBid = double.NaN;
            TheoAsk = double.NaN;
            SmoothedDeltaAdjTheo = double.NaN;
            VolaPriceMetricV0 = double.NaN;
            VolaPriceMetricV1 = double.NaN;
            VolaPriceMetricV2 = double.NaN;
            VolaPriceMetricV3 = double.NaN;
            VolaTheoV0 = double.NaN;
            VolaTheoAdjV0 = double.NaN;
            VolaIv = double.NaN;
            VolaTheoSequenceV1 = 0;
            VolaTheoV1 = double.NaN;
            VolaTheoAdjV1 = double.NaN;
            VolaTheoSequenceV2 = 0;
            VolaTheoV2 = double.NaN;
            VolaTheoAdjV2 = double.NaN;
            VolaTheoSequenceV3 = 0;
            VolaTheoV3 = double.NaN;
            VolaTheoAdjV3 = double.NaN;
            AdjTheoUnderlying = double.NaN;
            LockedTheo = double.NaN;
            LockedTheoUnderlying = double.NaN;
            LockedDeltaAdjTheo = double.NaN;
            DeltaAdjBidTheo = double.NaN;
            DeltaAdjAskTheo = double.NaN;
            GammaAdjustedDelta = double.NaN;
            LastBidTheoSpread = double.NaN;
            LastAskTheoSpread = double.NaN;
            BidTheoSpreadEma = double.NaN;
            AskTheoSpreadEma = double.NaN;
            UserNetQty = double.NaN;
            BidIvEma = double.NaN;
            AskIvEma = double.NaN;
            MidIvEma = double.NaN;
            AdjDaEma = double.NaN;
            VolaEma = double.NaN;
            AdjVolaEma = double.NaN;
            DaEma = double.NaN;
            DeltaAdjTheoSequence = 0;
            ZpTheoSequence = 0;
            DeltaAdjTheoTime = DateTime.MinValue;
            HanweckTime = "";
            InfoBits = 0;
            TradingNetQty = NetQty = FirmNetQty = TradingSellQty = TradingBuyQty = OpeningQty = 0;
        }

        internal void UpdateUiProperties()
        {
            try
            {
                UpdateUnrealPnl();
                if (_notifiers != null)
                {
                    for (var index = 0; index < _notifiersCount; index++)
                    {
                        var notifier = _notifiers[index];
                        if (notifier.IsUpdated)
                        {
                            notifier.IsUpdated = false;
                            RaisePropertyChanged(notifier.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Debug(ex, nameof(UpdateUiProperties));
            }
        }

        private void UpdateUnrealPnl()
        {
            switch (ActualQty)
            {
                case > 0:
                    UnrealPnl = (Mid - AveCost) * ActualQty * Multiplier;
                    ManualUnrealPnl = (Mid - ManualAvgCost) * ActualQty * Multiplier;
                    break;
                case < 0:
                    UnrealPnl = (AveCost - Mid) * Math.Abs(ActualQty) * Multiplier;
                    ManualUnrealPnl = (ManualAvgCost - Mid) * Math.Abs(ActualQty) * Multiplier;
                    break;
                default:
                    UnrealPnl = 0.0;
                    ManualUnrealPnl = 0.0;
                    break;
            }
        }

        internal Greeks UpdateGreeks(Comms.Models.Data.MarketData.MDUnderlying underlyingDetails, double mid)
        {
            Greeks greeks = new();

            if (!IsValid || ExpirationInfo == null)
            {
                greeks = new Greeks()
                {
                    Delta = double.NaN,
                    Gamma = double.NaN,
                    Theta = double.NaN,
                };
                return greeks;
            }

            var timeNowCt = (DateTime.Now.ToEastern() - TimeSpan.FromHours(1));
            double totalDays = (ExpirationInfo.Expiration - timeNowCt).TotalDays;
            if (totalDays < 0.0)
            {
                totalDays = 0;
            }
            else
            {
                totalDays += 1;
            }

            PricingParameters pricingParameters = new()
            {
                Volatility = 0.0,
                PutCall = Type == "PUT" ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                Strike = Strike.Strike,
                DaysToExpiration = totalDays,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = mid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = _underlying.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, mid, underlyingDetails.Dividends, DateTime.Now);
            _iv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, _mid, greeks);
            DeltaModeled = greeks.Delta;
            GammaModeled = greeks.Gamma;
            ThetaModeled = greeks.Theta;
            VegaModeled = greeks.Vega;
            NetWeightedVega = WeightedVega * ActualQty * 100;
            if ((double.IsNaN(_initialIv) || _initialIv == 0) && ActualQty != 0)
            {
                _initialIv = _iv;
            }
            IvVegaPnl = (_iv - _initialIv) * Multiplier * _vega * ActualQty * Multiplier;
            NetDelta = DeltaModeled * ActualQty * Multiplier;
            NetGamma = GammaModeled * ActualQty * Multiplier;
            NetTheta = ThetaModeled * ActualQty * Multiplier;
            GammaTheta = NetTheta != 0 ? NetGamma / NetTheta : 0;
            return greeks;
        }

        internal Greeks GetGreeks(Comms.Models.Data.MarketData.MDUnderlying underlyingDetails, double mid)
        {
            Greeks greeks = new();
            if (!IsValid || ExpirationInfo == null)
            {
                return greeks;
            }

            double totalDays = (ExpirationInfo.Expiration - DateTime.Now).TotalDays;
            if (totalDays < 0.0)
            {
                totalDays = 0;
            }
            else
            {
                totalDays += 1;
            }
            PricingParameters pricingParameters = new()
            {
                Volatility = 0.0,
                PutCall = Type == "PUT" ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                Strike = Strike.Strike,
                DaysToExpiration = totalDays,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = mid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = _underlying.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, mid, underlyingDetails.Dividends, DateTime.Now);
            OptionModel.Binomial.ImpliedVolatility(pricingParameters, _mid, greeks);
            return greeks;
        }
    }
}
