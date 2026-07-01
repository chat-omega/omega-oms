using System;
using SymbolLib;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Oms.Ui.Models;

public delegate void QuoteAndGreeksUpdatedHandler(IQuotesAndGreeks model, SubscriptionFieldType type, byte modelId);
public interface IQuotesAndGreeks
{
    event QuoteAndGreeksUpdatedHandler Updated;

    public string Underlying { get; set; }
    public string Symbol { get; set; }
    public DateTime StartTime { get; }
    public uint HanweckSequence { get; set; }
    public double Bid { get; set; }
    public double Mid { get; set; }
    public double Ask { get; set; }
    public double UnderBid { get; set; }
    public double UnderMid { get; set; }
    public double UnderAsk { get; set; }
    public double BidEma { get; set; }
    public double MidEma { get; set; }
    public double AskEma { get; set; }
    public double HanweckTheo { get; set; }
    public double HanweckTheoAdj { get; set; }
    public double HanweckVol { get; set; }
    public double HanweckDelta { get; set; }
    public double HanweckVega { get; set; }
    public uint VolaV0Sequence { get; set; }
    public double VolaV0PriceMetric { get; set; }
    public double VolaV0Theo { get; set; }
    public double VolaV0TheoAdj { get; set; }
    public double VolaV0Vol { get; set; }
    public double VolaV0ChangeInPremium { get; set; }
    public double VolaV0Spot { get; set; }
    public double VolaV0Underlying { get; set; }
    public string VolaV0Config { get; set; }
    public uint VolaV1Sequence { get; set; }
    public double VolaV1PriceMetric { get; set; }
    public double VolaV1Theo { get; set; }
    public double VolaV1TheoAdj { get; set; }
    public double VolaV1Vol { get; set; }
    public double VolaV1ChangeInPremium { get; set; }
    public double VolaV1Spot { get; set; }
    public double VolaV1Underlying { get; set; }
    public string VolaV1Config { get; set; }
    public uint VolaV2Sequence { get; set; }
    public double VolaV2PriceMetric { get; set; }
    public double VolaV2Theo { get; set; }
    public double VolaV2TheoAdj { get; set; }
    public double VolaV2Vol { get; set; }
    public double VolaV2ChangeInPremium { get; set; }
    public double VolaV2Spot { get; set; }
    public double VolaV2Underlying { get; set; }
    public string VolaV2Config { get; set; }
    public uint VolaV3Sequence { get; set; }
    public double VolaV3PriceMetric { get; set; }
    public double VolaV3Theo { get; set; }
    public double VolaV3TheoAdj { get; set; }
    public double VolaV3Vol { get; set; }
    public double VolaV3ChangeInPremium { get; set; }
    public double VolaV3Spot { get; set; }
    public double VolaV3Underlying { get; set; }
    public string VolaV3Config { get; set; }
    public DateTime BidUpdateTime { get; set; }
    public DateTime AskUpdateTime { get; set; }
    public DateTime UnderBidUpdateTime { get; set; }
    public DateTime UnderAskUpdateTime { get; set; }
    public DateTime FullEmaUpdateTime { get; set; }
    public DateTime HanweckUpdateTime { get; set; }
    public DateTime VolaV0UpdateTime { get; set; }
    public DateTime VolaV1UpdateTime { get; set; }
    public DateTime VolaV2UpdateTime { get; set; }
    public DateTime VolaV3UpdateTime { get; set; }
    public DateTime RaptorUpdateTime { get; set; }
    public DateTime DerivedValuesUpdateTime { get; set; }
    public double ImpliedBid { get; set; }
    public double ImpliedAsk { get; set; }
    public double ImpliedBidRecordPrice { get; set; }
    public double ImpliedAskRecordPrice { get; set; }
    public double ImpliedBidRecordTheo { get; set; }
    public double ImpliedAskRecordTheo { get; set; }
    public DateTime ImpliedBidRecordTimestamp { get; set; }
    public DateTime ImpliedAskRecordTimestamp { get; set; }
    public double ImpliedBidDeltaMovement { get; set; }
    public double ImpliedAskDeltaMovement { get; set; }
    public double ImpliedBidNonDeltaMovement { get; set; }
    public double ImpliedAskNonDeltaMovement { get; set; }
    public double SpreadEma { get; set; }
    public double SpreadEmaUnder { get; set; }
    public DateTime SpreadEmaTime { get; set; }
    public double DaEma { get; set; }
    public double DaEmaUnder { get; set; }
    public DateTime DaEmaTime { get; set; }
    public double EmaServerUnder { get; set; }
    public DateTime EmaServerSendTime { get; set; }
    public DateTime EmaServerExchangeTime { get; set; }
    public DateTime EmaServerCalcTime { get; set; }
    public DateTime EmaServerDistributionTime { get; set; }
    public string EmaServerUnderSymbol { get; set; }
    public int EmaServerErrorCode { get; set; }
    public string EmaServerErrorMessage { get; set; }
    public int EmaServerIntervalMS { get; set; }
    public double AdjDaEma { get; set; }
    public double VolaEma { get; set; }
    public double VolaEmaUnder { get; set; }
    public DateTime VolaEmaTime { get; set; }
    public double AdjVolaEma { get; set; }
    public double AdjUnderlying { get; set; }
    void Initialize(string viewModelSymbol);
    void Initialize(Instrument viewModelSymbol);
    void Subscribe();
    void Unsubscribe();
    string ToCsv();
    static string ToCsvHeader()
    {
        return
            nameof(Underlying).FromCamelCase() + "," +
            nameof(Symbol).FromCamelCase() + "," +
            nameof(StartTime).FromCamelCase() + "," +
            nameof(Bid).FromCamelCase() + "," +
            nameof(BidUpdateTime).FromCamelCase() + "," +
            nameof(Mid).FromCamelCase() + "," +
            nameof(Ask).FromCamelCase() + "," +
            nameof(AskUpdateTime).FromCamelCase() + "," +
            nameof(UnderBid).FromCamelCase() + "," +
            nameof(UnderBidUpdateTime).FromCamelCase() + "," +
            nameof(UnderMid).FromCamelCase() + "," +
            nameof(UnderAsk).FromCamelCase() + "," +
            nameof(UnderAskUpdateTime).FromCamelCase() + "," +
            nameof(BidEma).FromCamelCase() + "," +
            nameof(MidEma).FromCamelCase() + "," +
            nameof(AskEma).FromCamelCase() + "," +
            nameof(FullEmaUpdateTime).FromCamelCase() + "," +
            nameof(HanweckSequence).FromCamelCase() + "," +
            nameof(HanweckUpdateTime).FromCamelCase() + "," +
            nameof(HanweckTheo).FromCamelCase() + "," +
            nameof(HanweckTheoAdj).FromCamelCase() + "," +
            nameof(HanweckVol).FromCamelCase() + "," +
            nameof(HanweckDelta).FromCamelCase() + "," +
            nameof(HanweckVega).FromCamelCase() + "," +
            nameof(VolaV0Sequence).FromCamelCase() + "," +
            nameof(VolaV0UpdateTime).FromCamelCase() + "," +
            nameof(RaptorUpdateTime).FromCamelCase() + "," +
            nameof(VolaV0PriceMetric).FromCamelCase() + "," +
            nameof(VolaV0Theo).FromCamelCase() + "," +
            nameof(VolaV0TheoAdj).FromCamelCase() + "," +
            nameof(VolaV0Vol).FromCamelCase() + "," +
            nameof(VolaV0ChangeInPremium).FromCamelCase() + "," +
            nameof(VolaV0Spot).FromCamelCase() + "," +
            nameof(VolaV0Underlying).FromCamelCase() + "," +
            nameof(VolaV0Config).FromCamelCase() + "," +
            nameof(VolaV1Sequence).FromCamelCase() + "," +
            nameof(VolaV1UpdateTime).FromCamelCase() + "," +
            nameof(VolaV1PriceMetric).FromCamelCase() + "," +
            nameof(VolaV1Theo).FromCamelCase() + "," +
            nameof(VolaV1TheoAdj).FromCamelCase() + "," +
            nameof(VolaV1Vol).FromCamelCase() + "," +
            nameof(VolaV1ChangeInPremium).FromCamelCase() + "," +
            nameof(VolaV1Spot).FromCamelCase() + "," +
            nameof(VolaV1Underlying).FromCamelCase() + "," +
            nameof(VolaV1Config).FromCamelCase() + "," +
            nameof(VolaV2Sequence).FromCamelCase() + "," +
            nameof(VolaV2UpdateTime).FromCamelCase() + "," +
            nameof(VolaV2PriceMetric).FromCamelCase() + "," +
            nameof(VolaV2Theo).FromCamelCase() + "," +
            nameof(VolaV2TheoAdj).FromCamelCase() + "," +
            nameof(VolaV2Vol).FromCamelCase() + "," +
            nameof(VolaV2ChangeInPremium).FromCamelCase() + "," +
            nameof(VolaV2Spot).FromCamelCase() + "," +
            nameof(VolaV2Underlying).FromCamelCase() + "," +
            nameof(VolaV2Config).FromCamelCase() + "," +
            nameof(VolaV3Sequence).FromCamelCase() + "," +
            nameof(VolaV3UpdateTime).FromCamelCase() + "," +
            nameof(VolaV3PriceMetric).FromCamelCase() + "," +
            nameof(VolaV3Theo).FromCamelCase() + "," +
            nameof(VolaV3TheoAdj).FromCamelCase() + "," +
            nameof(VolaV3Vol).FromCamelCase() + "," +
            nameof(VolaV3ChangeInPremium).FromCamelCase() + "," +
            nameof(VolaV3Spot).FromCamelCase() + "," +
            nameof(VolaV3Underlying).FromCamelCase() + "," +
            nameof(VolaV3Config).FromCamelCase() + "," +
            nameof(ImpliedBid).FromCamelCase() + "," +
            nameof(DerivedValuesUpdateTime).FromCamelCase() + "," +
            nameof(ImpliedAsk).FromCamelCase() + "," +
            nameof(ImpliedBidRecordPrice).FromCamelCase() + "," +
            nameof(ImpliedAskRecordPrice).FromCamelCase() + "," +
            nameof(ImpliedBidRecordTheo).FromCamelCase() + "," +
            nameof(ImpliedAskRecordTheo).FromCamelCase() + "," +
            nameof(ImpliedBidRecordTimestamp).FromCamelCase() + "," +
            nameof(ImpliedAskRecordTimestamp).FromCamelCase() + "," +
            nameof(ImpliedBidDeltaMovement).FromCamelCase() + "," +
            nameof(ImpliedAskDeltaMovement).FromCamelCase() + "," +
            nameof(ImpliedBidNonDeltaMovement).FromCamelCase() + "," +
            nameof(ImpliedAskNonDeltaMovement).FromCamelCase() + "," +
            nameof(SpreadEma).FromCamelCase() + ',' +
            nameof(SpreadEmaUnder).FromCamelCase() + ',' +
            nameof(SpreadEmaTime).FromCamelCase() + ',' +
            nameof(DaEma).FromCamelCase() + ',' +
            nameof(DaEmaUnder).FromCamelCase() + ',' +
            nameof(DaEmaTime).FromCamelCase() + ',' +
            nameof(VolaEma).FromCamelCase() + ',' +
            nameof(VolaEmaUnder).FromCamelCase() + ',' +
            nameof(VolaEmaTime).FromCamelCase() + ',' +
            nameof(EmaServerUnder).FromCamelCase() + ',' +
            nameof(AdjDaEma).FromCamelCase() + ',' +
            nameof(AdjVolaEma).FromCamelCase() + ',' +
            nameof(AdjUnderlying).FromCamelCase() + ',' +
            nameof(EmaServerSendTime).FromCamelCase() + ',' +
            nameof(EmaServerExchangeTime).FromCamelCase() + ',' +
            nameof(EmaServerCalcTime).FromCamelCase() + ',' +
            nameof(EmaServerDistributionTime).FromCamelCase() + ',' +
            nameof(EmaServerUnderSymbol).FromCamelCase() + ',' +
            nameof(EmaServerErrorCode).FromCamelCase() + ',' +
            nameof(EmaServerErrorMessage).FromCamelCase() + ',' +
            nameof(EmaServerIntervalMS).FromCamelCase();
    }
}
