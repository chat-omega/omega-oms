using DevExpress.Mvvm;
using SymbolLib;
using System;
using System.Threading.Tasks;
using ZeroPlus.MessageObjects;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Updates;

namespace ZeroPlus.Oms.Ui.Models;

public class QuotesAndGreeksModel : BindableBase, IQuotesAndGreeks, IOmsDataSubscriber
{
    public virtual event QuoteAndGreeksUpdatedHandler Updated;

    protected readonly OmsCore _omsCore;

    private string _underlying;
    private string _symbol;
    private double _bid = double.NaN;
    private double _mid = double.NaN;
    private double _ask = double.NaN;
    private double _underBid = double.NaN;
    private double _underMid = double.NaN;
    private double _underAsk = double.NaN;
    private double _bidEma = double.NaN;
    private double _midEma = double.NaN;
    private double _askEma = double.NaN;
    private uint _hanweckSequence;
    private double _hanweckTheo = double.NaN;
    private double _hanweckTheoAdj = double.NaN;
    private double _hanweckVol = double.NaN;
    private double _hanweckDelta = double.NaN;
    private double _hanweckVega = double.NaN;
    private uint _volaV0Sequence;
    private double _volaV0PriceMetric = double.NaN;
    private double _volaV0Theo = double.NaN;
    private double _volaV0Vol = double.NaN;
    private double _volaV0ChangeInPremium = double.NaN;
    private double _volaV0Spot = double.NaN;
    private double _volaV0Underlying = double.NaN;
    private string _volaV0Config = string.Empty;
    private double _volaV0TheoAdj = double.NaN;
    private uint _volaV1Sequence;
    private double _volaV1PriceMetric = double.NaN;
    private double _volaV1Theo = double.NaN;
    private double _volaV1Vol = double.NaN;
    private double _volaV1ChangeInPremium = double.NaN;
    private double _volaV1TheoAdj = double.NaN;
    private double _volaV1Spot = double.NaN;
    private double _volaV1Underlying = double.NaN;
    private string _volaV1Config = string.Empty;
    private uint _volaV2Sequence;
    private double _volaV2PriceMetric = double.NaN;
    private double _volaV2Theo = double.NaN;
    private double _volaV2Vol = double.NaN;
    private double _volaV2ChangeInPremium = double.NaN;
    private double _volaV2TheoAdj = double.NaN;
    private double _volaV2Spot = double.NaN;
    private double _volaV2Underlying = double.NaN;
    private string _volaV2Config = string.Empty;
    private uint _volaV3Sequence;
    private double _volaV3PriceMetric = double.NaN;
    private double _volaV3Theo = double.NaN;
    private double _volaV3TheoAdj = double.NaN;
    private double _volaV3Vol = double.NaN;
    private double _volaV3ChangeInPremium = double.NaN;
    private double _volaV3Spot = double.NaN;
    private double _volaV3Underlying = double.NaN;
    private string _volaV3Config = string.Empty;
    private double _impliedBid = double.NaN;
    private double _impliedAsk = double.NaN;
    private double _impliedBidRecordPrice = double.NaN;
    private double _impliedAskRecordPrice = double.NaN;
    private double _impliedBidRecordTheo = double.NaN;
    private double _impliedAskRecordTheo = double.NaN;
    private DateTime _impliedBidRecordTimestamp;
    private DateTime _impliedAskRecordTimestamp;
    private double _impliedBidDeltaMovement = double.NaN;
    private double _impliedAskDeltaMovement = double.NaN;
    private double _impliedBidNonDeltaMovement = double.NaN;
    private double _impliedAskNonDeltaMovement = double.NaN;
    private double _deltaAdjEma = double.NaN;
    private double _adjDeltaAdjEma = double.NaN;
    private double _spreadEma = double.NaN;
    private double _spreadEmaUnder = double.NaN;
    private DateTime _spreadEmaTime;
    private double _daEmaUnder = double.NaN;
    private DateTime _daEmaTime;
    private double _volaEmaUnder = double.NaN;
    private DateTime _volaEmaTime;
    private DateTime _emaServerSendTime;
    private DateTime _emaServerDistributionTime;
    private DateTime _emaServerExchangeTime;
    private DateTime _emaServerCalcTime;
    private string _emaServerUnderSymbol = string.Empty;
    private int _emaServerErrorCode;
    private string _emaServerErrorMessage = string.Empty;
    private int _emaServerIntervalMS;
    private double _deltaAdjEmaUnder;
    private double _volaEma;
    private double _adjVolaEma;
    private double _adjUnderlying;
    private DateTime _bidUpdateTime;
    private DateTime _askUpdateTime;
    private DateTime _underBidUpdateTime;
    private DateTime _underAskUpdateTime;
    private DateTime _fullEmaUpdateTime;
    private DateTime _hanweckUpdateTime;
    private DateTime _volaV0UpdateTime;
    private DateTime _volaV1UpdateTime;
    private DateTime _volaV2UpdateTime;
    private DateTime _volaV3UpdateTime;
    private DateTime _raptorUpdateTime;
    private DateTime _derivedValuesUpdateTime;

    public string Underlying { get => _underlying; set => SetValue(ref _underlying, value); }
    public string Symbol { get => _symbol; set => SetValue(ref _symbol, value); }
    public DateTime StartTime => DateTime.Now;
    public uint HanweckSequence { get => _hanweckSequence; set => SetValue(ref _hanweckSequence, value); }
    public double Bid { get => _bid; set => SetValue(ref _bid, value); }
    public double Mid { get => _mid; set => SetValue(ref _mid, value); }
    public double Ask { get => _ask; set => SetValue(ref _ask, value); }
    public double UnderBid { get => _underBid; set => SetValue(ref _underBid, value); }
    public double UnderMid { get => _underMid; set => SetValue(ref _underMid, value); }
    public double UnderAsk { get => _underAsk; set => SetValue(ref _underAsk, value); }
    public double BidEma { get => _bidEma; set => SetValue(ref _bidEma, value); }
    public double MidEma { get => _midEma; set => SetValue(ref _midEma, value); }
    public double AskEma { get => _askEma; set => SetValue(ref _askEma, value); }
    public double HanweckTheo { get => _hanweckTheo; set => SetValue(ref _hanweckTheo, value); }
    public double HanweckTheoAdj { get => _hanweckTheoAdj; set => SetValue(ref _hanweckTheoAdj, value); }
    public double HanweckVol { get => _hanweckVol; set => SetValue(ref _hanweckVol, value); }
    public double HanweckDelta { get => _hanweckDelta; set => SetValue(ref _hanweckDelta, value); }
    public double HanweckVega { get => _hanweckVega; set => SetValue(ref _hanweckVega, value); }
    public uint VolaV0Sequence { get => _volaV0Sequence; set => SetValue(ref _volaV0Sequence, value); }
    public double VolaV0PriceMetric { get => _volaV0PriceMetric; set => SetValue(ref _volaV0PriceMetric, value); }
    public double VolaV0Theo { get => _volaV0Theo; set => SetValue(ref _volaV0Theo, value); }
    public double VolaV0TheoAdj { get => _volaV0TheoAdj; set => SetValue(ref _volaV0TheoAdj, value); }
    public double VolaV0Vol { get => _volaV0Vol; set => SetValue(ref _volaV0Vol, value); }
    public double VolaV0ChangeInPremium { get => _volaV0ChangeInPremium; set => SetValue(ref _volaV0ChangeInPremium, value); }
    public double VolaV0Spot { get => _volaV0Spot; set => SetValue(ref _volaV0Spot, value); }
    public double VolaV0Underlying { get => _volaV0Underlying; set => SetValue(ref _volaV0Underlying, value); }
    public string VolaV0Config { get => _volaV0Config; set => SetValue(ref _volaV0Config, value); }
    public uint VolaV1Sequence { get => _volaV1Sequence; set => SetValue(ref _volaV1Sequence, value); }
    public double VolaV1PriceMetric { get => _volaV1PriceMetric; set => SetValue(ref _volaV1PriceMetric, value); }
    public double VolaV1Theo { get => _volaV1Theo; set => SetValue(ref _volaV1Theo, value); }
    public double VolaV1TheoAdj { get => _volaV1TheoAdj; set => SetValue(ref _volaV1TheoAdj, value); }
    public double VolaV1Vol { get => _volaV1Vol; set => SetValue(ref _volaV1Vol, value); }
    public double VolaV1ChangeInPremium { get => _volaV1ChangeInPremium; set => SetValue(ref _volaV1ChangeInPremium, value); }
    public double VolaV1Spot { get => _volaV1Spot; set => SetValue(ref _volaV1Spot, value); }
    public double VolaV1Underlying { get => _volaV1Underlying; set => SetValue(ref _volaV1Underlying, value); }
    public string VolaV1Config { get => _volaV1Config; set => SetValue(ref _volaV1Config, value); }
    public uint VolaV2Sequence { get => _volaV2Sequence; set => SetValue(ref _volaV2Sequence, value); }
    public double VolaV2PriceMetric { get => _volaV2PriceMetric; set => SetValue(ref _volaV2PriceMetric, value); }
    public double VolaV2Theo { get => _volaV2Theo; set => SetValue(ref _volaV2Theo, value); }
    public double VolaV2TheoAdj { get => _volaV2TheoAdj; set => SetValue(ref _volaV2TheoAdj, value); }
    public double VolaV2Vol { get => _volaV2Vol; set => SetValue(ref _volaV2Vol, value); }
    public double VolaV2ChangeInPremium { get => _volaV2ChangeInPremium; set => SetValue(ref _volaV2ChangeInPremium, value); }
    public double VolaV2Spot { get => _volaV2Spot; set => SetValue(ref _volaV2Spot, value); }
    public double VolaV2Underlying { get => _volaV2Underlying; set => SetValue(ref _volaV2Underlying, value); }
    public string VolaV2Config { get => _volaV2Config; set => SetValue(ref _volaV2Config, value); }
    public uint VolaV3Sequence { get => _volaV3Sequence; set => SetValue(ref _volaV3Sequence, value); }
    public double VolaV3PriceMetric { get => _volaV3PriceMetric; set => SetValue(ref _volaV3PriceMetric, value); }
    public double VolaV3Theo { get => _volaV3Theo; set => SetValue(ref _volaV3Theo, value); }
    public double VolaV3TheoAdj { get => _volaV3TheoAdj; set => SetValue(ref _volaV3TheoAdj, value); }
    public double VolaV3Vol { get => _volaV3Vol; set => SetValue(ref _volaV3Vol, value); }
    public double VolaV3ChangeInPremium { get => _volaV3ChangeInPremium; set => SetValue(ref _volaV3ChangeInPremium, value); }
    public double VolaV3Spot { get => _volaV3Spot; set => SetValue(ref _volaV3Spot, value); }
    public double VolaV3Underlying { get => _volaV3Underlying; set => SetValue(ref _volaV3Underlying, value); }
    public string VolaV3Config { get => _volaV3Config; set => SetValue(ref _volaV3Config, value); }
    public double ImpliedBid { get => _impliedBid; set => SetValue(ref _impliedBid, value); }
    public double ImpliedAsk { get => _impliedAsk; set => SetValue(ref _impliedAsk, value); }
    public double ImpliedBidRecordPrice { get => _impliedBidRecordPrice; set => SetValue(ref _impliedBidRecordPrice, value); }
    public double ImpliedAskRecordPrice { get => _impliedAskRecordPrice; set => SetValue(ref _impliedAskRecordPrice, value); }
    public double ImpliedBidRecordTheo { get => _impliedBidRecordTheo; set => SetValue(ref _impliedBidRecordTheo, value); }
    public double ImpliedAskRecordTheo { get => _impliedAskRecordTheo; set => SetValue(ref _impliedAskRecordTheo, value); }
    public DateTime ImpliedBidRecordTimestamp { get => _impliedBidRecordTimestamp; set => SetValue(ref _impliedBidRecordTimestamp, value); }
    public DateTime ImpliedAskRecordTimestamp { get => _impliedAskRecordTimestamp; set => SetValue(ref _impliedAskRecordTimestamp, value); }
    public double ImpliedBidDeltaMovement { get => _impliedBidDeltaMovement; set => SetValue(ref _impliedBidDeltaMovement, value); }
    public double ImpliedAskDeltaMovement { get => _impliedAskDeltaMovement; set => SetValue(ref _impliedAskDeltaMovement, value); }
    public double ImpliedBidNonDeltaMovement { get => _impliedBidNonDeltaMovement; set => SetValue(ref _impliedBidNonDeltaMovement, value); }
    public double ImpliedAskNonDeltaMovement { get => _impliedAskNonDeltaMovement; set => SetValue(ref _impliedAskNonDeltaMovement, value); }
    public double AdjDaEma { get => _adjDeltaAdjEma; set => SetValue(ref _adjDeltaAdjEma, value); }
    public double DaEma { get => _deltaAdjEma; set => SetValue(ref _deltaAdjEma, value); }
    public double DaEmaUnder { get => _daEmaUnder; set => SetValue(ref _daEmaUnder, value); }
    public DateTime DaEmaTime { get => _daEmaTime; set => SetValue(ref _daEmaTime, value); }
    public double SpreadEma { get => _spreadEma; set => SetValue(ref _spreadEma, value); }
    public double SpreadEmaUnder { get => _spreadEmaUnder; set => SetValue(ref _spreadEmaUnder, value); }
    public DateTime SpreadEmaTime { get => _spreadEmaTime; set => SetValue(ref _spreadEmaTime, value); }
    public double EmaServerUnder { get => _deltaAdjEmaUnder; set => SetValue(ref _deltaAdjEmaUnder, value); }
    public DateTime EmaServerSendTime { get => _emaServerSendTime; set => SetValue(ref _emaServerSendTime, value); }
    public DateTime EmaServerExchangeTime { get => _emaServerExchangeTime; set => SetValue(ref _emaServerExchangeTime, value); }
    public DateTime EmaServerCalcTime { get => _emaServerCalcTime; set => SetValue(ref _emaServerCalcTime, value); }
    public DateTime EmaServerDistributionTime { get => _emaServerDistributionTime; set => SetValue(ref _emaServerDistributionTime, value); }
    public string EmaServerUnderSymbol { get => _emaServerUnderSymbol; set => SetValue(ref _emaServerUnderSymbol, value); }
    public int EmaServerErrorCode { get => _emaServerErrorCode; set => SetValue(ref _emaServerErrorCode, value); }
    public string EmaServerErrorMessage { get => _emaServerErrorMessage; set => SetValue(ref _emaServerErrorMessage, value); }
    public int EmaServerIntervalMS { get => _emaServerIntervalMS; set => SetValue(ref _emaServerIntervalMS, value); }
    public double VolaEma { get => _volaEma; set => SetValue(ref _volaEma, value); }
    public double VolaEmaUnder { get => _volaEmaUnder; set => SetValue(ref _volaEmaUnder, value); }
    public DateTime VolaEmaTime { get => _volaEmaTime; set => SetValue(ref _volaEmaTime, value); }
    public double AdjVolaEma { get => _adjVolaEma; set => SetValue(ref _adjVolaEma, value); }
    public double AdjUnderlying { get => _adjUnderlying; set => SetValue(ref _adjUnderlying, value); }
    public DateTime BidUpdateTime { get => _bidUpdateTime; set => SetValue(ref _bidUpdateTime, value); }
    public DateTime AskUpdateTime { get => _askUpdateTime; set => SetValue(ref _askUpdateTime, value); }
    public DateTime UnderBidUpdateTime { get => _underBidUpdateTime; set => SetValue(ref _underBidUpdateTime, value); }
    public DateTime UnderAskUpdateTime { get => _underAskUpdateTime; set => SetValue(ref _underAskUpdateTime, value); }
    public DateTime FullEmaUpdateTime { get => _fullEmaUpdateTime; set => SetValue(ref _fullEmaUpdateTime, value); }
    public DateTime HanweckUpdateTime { get => _hanweckUpdateTime; set => SetValue(ref _hanweckUpdateTime, value); }
    public DateTime VolaV0UpdateTime { get => _volaV0UpdateTime; set => SetValue(ref _volaV0UpdateTime, value); }
    public DateTime VolaV1UpdateTime { get => _volaV1UpdateTime; set => SetValue(ref _volaV1UpdateTime, value); }
    public DateTime VolaV2UpdateTime { get => _volaV2UpdateTime; set => SetValue(ref _volaV2UpdateTime, value); }
    public DateTime VolaV3UpdateTime { get => _volaV3UpdateTime; set => SetValue(ref _volaV3UpdateTime, value); }
    public DateTime RaptorUpdateTime { get => _raptorUpdateTime; set => SetValue(ref _raptorUpdateTime, value); }
    public DateTime DerivedValuesUpdateTime { get => _derivedValuesUpdateTime; set => SetValue(ref _derivedValuesUpdateTime, value); }

    public bool IsDisposed { get; set; }

    public QuotesAndGreeksModel(OmsCore omsCore)
    {
        _omsCore = omsCore;
    }

    public virtual void Initialize(string viewModelSymbol)
    {
        var security = new Instrument(viewModelSymbol.Replace("+", "").Replace("-", ""));
        Initialize(security);
    }

    public void Initialize(Instrument security)
    {
        if (security.valid)
        {
            Symbol = security.symbol;
            Underlying = security.underlyingSymbol;

            Task.Run(RequestModelConfigs);
        }
    }

    private void RequestModelConfigs()
    {
        try
        {
            VolaV0Config = _omsCore.UpdateManager.GetTheoConfig(TheoModel.VolaV0, Underlying);
            VolaV1Config = _omsCore.UpdateManager.GetTheoConfig(TheoModel.VolaV1, Underlying);
            VolaV2Config = _omsCore.UpdateManager.GetTheoConfig(TheoModel.VolaV2, Underlying);
            VolaV3Config = _omsCore.UpdateManager.GetTheoConfig(TheoModel.VolaV3, Underlying);
        }
        catch (Exception)
        {
        }
    }

    public string ToCsv()
    {
        return
            Underlying + "," +
            Symbol + "," +
            StartTime.ToHHMMSSfff() + "," +
            Format(Bid) + "," +
            BidUpdateTime.ToHHMMSSfff() + "," +
            Format(Mid) + "," +
            Format(Ask) + "," +
            AskUpdateTime.ToHHMMSSfff() + "," +
            Format(UnderBid) + "," +
            UnderBidUpdateTime.ToHHMMSSfff() + "," +
            Format(UnderMid) + "," +
            Format(UnderAsk) + "," +
            UnderAskUpdateTime.ToHHMMSSfff() + "," +
            Format(BidEma) + "," +
            Format(MidEma) + "," +
            Format(AskEma) + "," +
            FullEmaUpdateTime.ToHHMMSSfff() + "," +
            Format(HanweckSequence) + "," +
            HanweckUpdateTime.ToHHMMSSfff() + "," +
            Format(HanweckTheo) + "," +
            Format(HanweckTheoAdj) + "," +
            Format(HanweckVol) + "," +
            Format(HanweckDelta) + "," +
            Format(HanweckVega) + "," +
            Format(VolaV0Sequence) + "," +
            VolaV0UpdateTime.ToHHMMSSfff() + "," +
            RaptorUpdateTime.ToHHMMSSfff() + "," +
            Format(VolaV0PriceMetric) + "," +
            Format(VolaV0Theo) + "," +
            Format(VolaV0TheoAdj) + "," +
            Format(VolaV0Vol) + "," +
            Format(VolaV0ChangeInPremium) + "," +
            Format(VolaV0Spot) + "," +
            Format(VolaV0Underlying) + "," +
            VolaV0Config + "," +
            Format(VolaV1Sequence) + "," +
            VolaV1UpdateTime.ToHHMMSSfff() + "," +
            Format(VolaV1PriceMetric) + "," +
            Format(VolaV1Theo) + "," +
            Format(VolaV1TheoAdj) + "," +
            Format(VolaV1Vol) + "," +
            Format(VolaV1ChangeInPremium) + "," +
            Format(VolaV1Spot) + "," +
            Format(VolaV1Underlying) + "," +
            VolaV1Config + "," +
            Format(VolaV2Sequence) + "," +
            VolaV2UpdateTime.ToHHMMSSfff() + "," +
            Format(VolaV2PriceMetric) + "," +
            Format(VolaV2Theo) + "," +
            Format(VolaV2TheoAdj) + "," +
            Format(VolaV2Vol) + "," +
            Format(VolaV2ChangeInPremium) + "," +
            Format(VolaV2Spot) + "," +
            Format(VolaV2Underlying) + "," +
            VolaV2Config + "," +
            Format(VolaV3Sequence) + "," +
            VolaV3UpdateTime.ToHHMMSSfff() + "," +
            Format(VolaV3PriceMetric) + "," +
            Format(VolaV3Theo) + "," +
            Format(VolaV3TheoAdj) + "," +
            Format(VolaV3Vol) + "," +
            Format(VolaV3ChangeInPremium) + "," +
            Format(VolaV3Spot) + "," +
            Format(VolaV3Underlying) + "," +
            VolaV3Config + "," +
            Format(ImpliedBid) + "," +
            DerivedValuesUpdateTime.ToHHMMSSfff() + "," +
            Format(ImpliedAsk) + "," +
            Format(ImpliedBidRecordPrice) + "," +
            Format(ImpliedAskRecordPrice) + "," +
            Format(ImpliedBidRecordTheo) + "," +
            Format(ImpliedAskRecordTheo) + "," +
            ImpliedBidRecordTimestamp.ToHHMMSSfff() + "," +
            ImpliedAskRecordTimestamp.ToHHMMSSfff() + "," +
            Format(ImpliedBidDeltaMovement) + "," +
            Format(ImpliedAskDeltaMovement) + "," +
            Format(ImpliedBidNonDeltaMovement) + "," +
            Format(ImpliedAskNonDeltaMovement) + "," +
            Format(SpreadEma) + ',' +
            Format(SpreadEmaUnder) + ',' +
            SpreadEmaTime.ToHHMMSSfff() + ',' +
            Format(DaEma) + ',' +
            Format(DaEmaUnder) + ',' +
            DaEmaTime.ToHHMMSSfff() + ',' +
            Format(VolaEma) + ',' +
            Format(VolaEmaUnder) + ',' +
            VolaEmaTime.ToHHMMSSfff() + ',' +
            Format(EmaServerUnder) + ',' +
            Format(AdjDaEma) + ',' +
            Format(AdjVolaEma) + ',' +
            Format(AdjUnderlying) + ',' +
            EmaServerSendTime.ToHHMMSSfff() + ',' +
            EmaServerExchangeTime.ToHHMMSSfff() + ',' +
            EmaServerCalcTime.ToHHMMSSfff() + ',' +
            EmaServerDistributionTime.ToHHMMSSfff() + ',' +
            EmaServerUnderSymbol + ',' +
            EmaServerErrorCode + ',' +
            EmaServerErrorMessage + ',' +
            EmaServerIntervalMS;
    }

    private static string Format(double value)
    {
        return value.ToString("#.#######");
    }

    public virtual void Subscribe()
    {
        _omsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.DeltaAdjTheo, this);
        _omsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.DerivedValues, this);
        _omsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.FullEma, this);
        _omsCore.UpdateManager.Subscribe(Symbol, SubscriptionFieldType.DeltaAdjEma, this);
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
        _omsCore.GreekClient.Subscribe(Symbol, SubscriptionFieldType.Greeks, this);
        _omsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.Ask, this);
        _omsCore.EmaServerClientModel.Subscribe(Symbol, SubscriptionFieldType.DeltaAdjEma, this);
        _omsCore.EmaServerClientModel.Subscribe(Symbol, SubscriptionFieldType.SpreadEma, this);
        _omsCore.EmaServerClientModel.Subscribe(Symbol, SubscriptionFieldType.VolaEma, this);
    }

    public virtual void Unsubscribe()
    {
        _omsCore.UpdateManager.Unsubscribe(Symbol, SubscriptionFieldType.DeltaAdjTheo, this);
        _omsCore.UpdateManager.Unsubscribe(Symbol, SubscriptionFieldType.DerivedValues, this);
        _omsCore.UpdateManager.Unsubscribe(Symbol, SubscriptionFieldType.FullEma, this);
        _omsCore.UpdateManager.Unsubscribe(Symbol, SubscriptionFieldType.DeltaAdjEma, this);
        _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
        _omsCore.GreekClient.Unsubscribe(Symbol, SubscriptionFieldType.Greeks, this);
        _omsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Bid, this);
        _omsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.Ask, this);
        _omsCore.EmaServerClientModel.Unsubscribe(Symbol, SubscriptionFieldType.DeltaAdjEma, this);
        _omsCore.EmaServerClientModel.Unsubscribe(Symbol, SubscriptionFieldType.SpreadEma, this);
        _omsCore.EmaServerClientModel.Unsubscribe(Symbol, SubscriptionFieldType.VolaEma, this);
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
    {
        switch (key.Type)
        {
            case SubscriptionFieldType.Bid when value is double bid:
                if (key.Symbol == Symbol)
                {
                    Bid = bid;
                    BidUpdateTime = DateTime.Now;
                    Mid = (Bid + Ask) * .5;
                }
                else if (key.Symbol == Underlying)
                {
                    UnderBid = bid;
                    UnderBidUpdateTime = DateTime.Now;
                    UnderMid = (UnderBid + UnderAsk) * .5;
                }
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.Ask when value is double ask:
                if (key.Symbol == Symbol)
                {
                    Ask = ask;
                    AskUpdateTime = DateTime.Now;
                    Mid = (Bid + Ask) * .5;
                }
                else if (key.Symbol == Underlying)
                {
                    UnderAsk = ask;
                    UnderAskUpdateTime = DateTime.Now;
                    UnderMid = (UnderBid + UnderAsk) * .5;
                }
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.FullEma when value is EmaUpdateModel emaUpdate:
                BidEma = emaUpdate.MidPeriodBidEma;
                MidEma = emaUpdate.MidPeriodEma;
                AskEma = emaUpdate.MidPeriodAskEma;
                FullEmaUpdateTime = DateTime.Now;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.VolaEma when value is APIEMAData emaData
            && emaData.Type is captureType.deltaadjvolatheo:
                VolaEma = emaData.EMA;
                VolaEmaUnder = emaData.UnderMidpoint;
                VolaEmaTime = emaData.SendTime;
                EmaServerUnder = emaData.UnderMidpoint;
                EmaServerUnderSymbol = emaData.UnderSymbol;
                EmaServerErrorCode = emaData.ErrorCode;
                EmaServerErrorMessage = emaData.ErrorMessage;
                EmaServerIntervalMS = emaData.IntervalMS;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.SpreadEma when value is APIEMAData emaData
            && emaData.Type is captureType.spread:
                SpreadEma = emaData.EMA;
                SpreadEmaUnder = emaData.UnderMidpoint;
                SpreadEmaTime = emaData.SendTime;
                EmaServerUnderSymbol = emaData.UnderSymbol;
                EmaServerErrorCode = emaData.ErrorCode;
                EmaServerErrorMessage = emaData.ErrorMessage;
                EmaServerIntervalMS = emaData.IntervalMS;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.DeltaAdjEma when value is APIEMAData emaData
            && emaData.Type is captureType.deltaadjoption:
                DaEma = emaData.EMA;
                DaEmaUnder = emaData.UnderMidpoint;
                DaEmaTime = emaData.SendTime;
                EmaServerUnder = emaData.UnderMidpoint;
                EmaServerSendTime = emaData.SendTime;
                EmaServerCalcTime = emaData.CalcTime;
                EmaServerDistributionTime = emaData.DistributionTime;
                EmaServerExchangeTime = emaData.Timestamp;
                EmaServerUnderSymbol = emaData.UnderSymbol;
                EmaServerErrorCode = emaData.ErrorCode;
                EmaServerErrorMessage = emaData.ErrorMessage;
                EmaServerIntervalMS = emaData.IntervalMS;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.Greeks when value is GreekUpdate greekUpdate:
                HanweckTheo = greekUpdate.Theo;
                HanweckVol = greekUpdate.Implied;
                HanweckDelta = greekUpdate.Delta;
                HanweckVega = greekUpdate.Vega;
                HanweckUpdateTime = DateTime.Now;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.DerivedValues when value is DerivedValueUpdateModel derivedValueUpdateModel:
                ImpliedBid = derivedValueUpdateModel.ImpliedBid;
                ImpliedAsk = derivedValueUpdateModel.ImpliedAsk;
                ImpliedBidRecordPrice = derivedValueUpdateModel.ImpliedBidRecord;
                ImpliedAskRecordPrice = derivedValueUpdateModel.ImpliedAskRecord;
                ImpliedBidRecordTheo = derivedValueUpdateModel.ImpliedBidRecordTheo;
                ImpliedAskRecordTheo = derivedValueUpdateModel.ImpliedAskRecordTheo;
                ImpliedBidRecordTimestamp = derivedValueUpdateModel.ImpliedBidRecordTimestamp;
                ImpliedAskRecordTimestamp = derivedValueUpdateModel.ImpliedAskRecordTimestamp;
                ImpliedBidDeltaMovement = derivedValueUpdateModel.ImpliedBidRecordTheoMovement;
                ImpliedAskDeltaMovement = derivedValueUpdateModel.ImpliedAskRecordTheoMovement;
                ImpliedBidNonDeltaMovement = derivedValueUpdateModel.ImpliedBidRecordNonDeltaMovement;
                ImpliedAskNonDeltaMovement = derivedValueUpdateModel.ImpliedAskRecordNonDeltaMovement;
                DerivedValuesUpdateTime = DateTime.Now;
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.DeltaAdjEma when value is double daEma:
                Updated?.Invoke(this, key.Type, default);
                break;
            case SubscriptionFieldType.DeltaAdjTheo when value is DeltaAdjTheo deltaAdjTheo:
                switch (deltaAdjTheo.ModelId)
                {
                    case 0:
                        HanweckSequence = deltaAdjTheo.UpdateSequence;
                        HanweckTheoAdj = deltaAdjTheo.DeltaAdjustedTheo;
                        VolaV0PriceMetric = deltaAdjTheo.PriceMetric;
                        VolaV0Sequence = deltaAdjTheo.UpdateSequence;
                        VolaV0Theo = deltaAdjTheo.SecondaryTheo;
                        VolaV0TheoAdj = deltaAdjTheo.SecondaryTheoAdj;
                        VolaV0Vol = deltaAdjTheo.SecondaryVol;
                        VolaV0ChangeInPremium = deltaAdjTheo.ChangeInPremium;
                        VolaV0Spot = deltaAdjTheo.SecondarySpot;
                        VolaV0Underlying = deltaAdjTheo.Underlying;
                        AdjDaEma = deltaAdjTheo.AdjDaEma;
                        AdjVolaEma = deltaAdjTheo.AdjVolaEma;
                        AdjUnderlying = deltaAdjTheo.Underlying;
                        VolaV0UpdateTime = DateTime.Now;
                        RaptorUpdateTime = DateTime.Now;
                        break;
                    case 1:
                        VolaV1PriceMetric = deltaAdjTheo.PriceMetric;
                        VolaV1Sequence = deltaAdjTheo.UpdateSequence;
                        VolaV1Theo = deltaAdjTheo.SecondaryTheo;
                        VolaV1TheoAdj = deltaAdjTheo.SecondaryTheoAdj;
                        VolaV1Vol = deltaAdjTheo.SecondaryVol;
                        VolaV1ChangeInPremium = deltaAdjTheo.ChangeInPremium;
                        VolaV1Spot = deltaAdjTheo.SecondarySpot;
                        VolaV1Underlying = deltaAdjTheo.Underlying;
                        VolaV1UpdateTime = DateTime.Now;
                        break;
                    case 2:
                        VolaV2PriceMetric = deltaAdjTheo.PriceMetric;
                        VolaV2Sequence = deltaAdjTheo.UpdateSequence;
                        VolaV2Theo = deltaAdjTheo.SecondaryTheo;
                        VolaV2TheoAdj = deltaAdjTheo.SecondaryTheoAdj;
                        VolaV2Vol = deltaAdjTheo.SecondaryVol;
                        VolaV2ChangeInPremium = deltaAdjTheo.ChangeInPremium;
                        VolaV2Spot = deltaAdjTheo.SecondarySpot;
                        VolaV2Underlying = deltaAdjTheo.Underlying;
                        VolaV2UpdateTime = DateTime.Now;
                        break;
                    case 3:
                        VolaV3PriceMetric = deltaAdjTheo.PriceMetric;
                        VolaV3Sequence = deltaAdjTheo.UpdateSequence;
                        VolaV3Theo = deltaAdjTheo.SecondaryTheo;
                        VolaV3TheoAdj = deltaAdjTheo.SecondaryTheoAdj;
                        VolaV3Vol = deltaAdjTheo.SecondaryVol;
                        VolaV3ChangeInPremium = deltaAdjTheo.ChangeInPremium;
                        VolaV3Spot = deltaAdjTheo.SecondarySpot;
                        VolaV3Underlying = deltaAdjTheo.Underlying;
                        VolaV3UpdateTime = DateTime.Now;
                        break;
                }
                Updated?.Invoke(this, key.Type, deltaAdjTheo.ModelId);
                break;
        }
    }
}