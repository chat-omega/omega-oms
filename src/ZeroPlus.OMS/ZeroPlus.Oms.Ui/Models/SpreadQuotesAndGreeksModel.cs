using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models;

public class SpreadQuotesAndGreeksModel : QuotesAndGreeksModel
{
    public override event QuoteAndGreeksUpdatedHandler Updated;

    public List<SpreadQuotesAndGreeksLegModel> SpreadLegs { get; } = [];

    public SpreadQuotesAndGreeksModel(OmsCore omsCore) : base(omsCore)
    {
    }

    public override void Initialize(string symbol)
    {
        var codec = new SymbolLib.SymbolCodec(symbol);
        Symbol = symbol;
        Underlying = codec.UnderlyingSymbol();

        Task.Run(RequestModelConfigs);

        for (int i = 0; i < codec.LegCount; i++)
        {
            var leg = codec.GetLeg(i);
            SpreadQuotesAndGreeksLegModel legModel = new SpreadQuotesAndGreeksLegModel(_omsCore)
            {
                Side = leg.buySell ? Side.Buy : Side.Sell,
                Ratio = leg.buySell ? Math.Abs(leg.ratio) : -Math.Abs(leg.ratio),
            };
            legModel.Initialize(leg);
            legModel.Updated += OnLegUpdate;
            SpreadLegs.Add(legModel);
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
        catch
        {
            // ignored
        }
    }

    public override void Subscribe()
    {
        foreach (var leg in SpreadLegs)
        {
            leg.Subscribe();
        }
    }

    public override void Unsubscribe()
    {
        foreach (var leg in SpreadLegs)
        {
            leg.Unsubscribe();
        }
    }

    private void OnLegUpdate(IQuotesAndGreeks _, SubscriptionFieldType type, byte model)
    {
        switch (type)
        {
            case SubscriptionFieldType.Bid:
            case SubscriptionFieldType.Ask:
                var leg = SpreadLegs.FirstOrDefault();
                Bid = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.Ask) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.Bid);
                Ask = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.Bid) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.Ask);
                Mid = (Bid + Ask) * .5;
                UnderBid = leg?.UnderBid ?? double.NaN;
                UnderAsk = leg?.UnderAsk ?? double.NaN;
                UnderMid = (UnderBid + UnderAsk) * .5;
                break;
            case SubscriptionFieldType.DeltaAdjEma when SpreadLegs.DistinctBy(x => x.EmaServerUnder).Count() == 1:
                leg = SpreadLegs.OrderBy(x => x.EmaServerSendTime).LastOrDefault();
                DaEma = SpreadLegs.Sum(x => x.Ratio * x.DaEma);
                DaEmaUnder = leg.DaEmaUnder;
                DaEmaTime = leg.DaEmaTime;
                EmaServerUnder = leg.EmaServerUnder;
                EmaServerSendTime = leg.EmaServerSendTime;
                EmaServerCalcTime = leg.EmaServerCalcTime;
                EmaServerDistributionTime = leg.EmaServerDistributionTime;
                EmaServerExchangeTime = leg.EmaServerExchangeTime;
                EmaServerUnderSymbol = leg.EmaServerUnderSymbol;
                EmaServerErrorCode = leg.EmaServerErrorCode;
                EmaServerErrorMessage = leg.EmaServerErrorMessage;
                EmaServerIntervalMS = leg.EmaServerIntervalMS;
                break;
            case SubscriptionFieldType.SpreadEma when SpreadLegs.DistinctBy(x => x.SpreadEmaUnder).Count() == 1:
                leg = SpreadLegs.OrderBy(x => x.SpreadEmaTime).LastOrDefault();
                SpreadEma = SpreadLegs.Sum(x => x.Ratio * x.SpreadEma);
                SpreadEmaUnder = leg.SpreadEmaUnder;
                SpreadEmaTime = leg.SpreadEmaTime;
                break;
            case SubscriptionFieldType.VolaEma when SpreadLegs.DistinctBy(x => x.VolaEmaUnder).Count() == 1:
                leg = SpreadLegs.OrderBy(x => x.VolaEmaTime).LastOrDefault();
                VolaEma = SpreadLegs.Sum(x => x.Ratio * x.VolaEma);
                VolaEmaUnder = leg.VolaEmaUnder;
                VolaEmaTime = leg.VolaEmaTime;
                break;
            case SubscriptionFieldType.FullEma:
                BidEma = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.AskEma) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.BidEma);
                AskEma = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.BidEma) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.AskEma);
                MidEma = SpreadLegs.Sum(x => x.Ratio * x.MidEma);
                break;
            case SubscriptionFieldType.Greeks:
                HanweckTheo = SpreadLegs.Sum(x => x.Ratio * x.HanweckTheo);
                HanweckVol = SpreadLegs.Sum(x => x.Ratio * x.HanweckVol);
                HanweckDelta = SpreadLegs.Sum(x => x.Ratio * x.HanweckDelta);
                HanweckVega = SpreadLegs.Sum(x => x.Ratio * x.HanweckVega);
                break;
            case SubscriptionFieldType.DerivedValues:
                ImpliedBid = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.ImpliedAsk) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.ImpliedBid);
                ImpliedAsk = SpreadLegs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.ImpliedBid) +
                      SpreadLegs.Where(x => x.Ratio > 0).Sum(x => x.ImpliedAsk);
                ImpliedBidRecordPrice = SpreadLegs.Sum(x => x.Ratio * x.ImpliedBidRecordPrice);
                ImpliedAskRecordPrice = SpreadLegs.Sum(x => x.Ratio * x.ImpliedAskRecordPrice);
                ImpliedBidRecordTheo = SpreadLegs.Sum(x => x.Ratio * x.ImpliedBidRecordTheo);
                ImpliedAskRecordTheo = SpreadLegs.Sum(x => x.Ratio * x.ImpliedAskRecordTheo);
                ImpliedBidRecordTimestamp = SpreadLegs.Min(x => x.ImpliedBidRecordTimestamp);
                ImpliedAskRecordTimestamp = SpreadLegs.Min(x => x.ImpliedAskRecordTimestamp);
                ImpliedBidDeltaMovement = SpreadLegs.Sum(x => x.Ratio * x.ImpliedBidDeltaMovement);
                ImpliedAskDeltaMovement = SpreadLegs.Sum(x => x.Ratio * x.ImpliedAskDeltaMovement);
                ImpliedBidNonDeltaMovement = SpreadLegs.Sum(x => x.Ratio * x.ImpliedBidNonDeltaMovement);
                ImpliedAskNonDeltaMovement = SpreadLegs.Sum(x => x.Ratio * x.ImpliedAskNonDeltaMovement);
                break;
            case SubscriptionFieldType.DeltaAdjTheo when model == 0 && SpreadLegs.DistinctBy(x => x.VolaV0Sequence).Count() == 1:
                leg = SpreadLegs.FirstOrDefault();
                HanweckSequence = SpreadLegs.Max(x => x.HanweckSequence);
                HanweckTheoAdj = SpreadLegs.Sum(x => x.Ratio * x.HanweckTheoAdj);
                VolaV0Sequence = SpreadLegs.Max(x => x.VolaV0Sequence);
                VolaV0PriceMetric = SpreadLegs.Min(x => x.VolaV0PriceMetric);
                VolaV0Theo = SpreadLegs.Sum(x => x.Ratio * x.VolaV0Theo);
                VolaV0TheoAdj = SpreadLegs.Sum(x => x.Ratio * x.VolaV0TheoAdj);
                VolaV0Vol = SpreadLegs.Sum(x => x.Ratio * x.VolaV0Vol);
                VolaV0ChangeInPremium = SpreadLegs.Sum(x => x.Ratio * x.VolaV0ChangeInPremium);
                VolaV0Spot = leg?.VolaV0Spot ?? double.NaN;
                VolaV0Underlying = leg?.VolaV0Underlying ?? double.NaN;
                AdjDaEma = SpreadLegs.Sum(x => x.Ratio * x.AdjDaEma);
                AdjVolaEma = SpreadLegs.Sum(x => x.Ratio * x.AdjVolaEma);
                AdjUnderlying = leg?.AdjUnderlying ?? double.NaN;
                break;
            case SubscriptionFieldType.DeltaAdjTheo when model == 1 && SpreadLegs.DistinctBy(x => x.VolaV1Sequence).Count() == 1:
                leg = SpreadLegs.FirstOrDefault();
                VolaV1Sequence = SpreadLegs.Max(x => x.VolaV1Sequence);
                VolaV1PriceMetric = SpreadLegs.Min(x => x.VolaV1PriceMetric);
                VolaV1Theo = SpreadLegs.Sum(x => x.Ratio * x.VolaV1Theo);
                VolaV1TheoAdj = SpreadLegs.Sum(x => x.Ratio * x.VolaV1TheoAdj);
                VolaV1Vol = SpreadLegs.Sum(x => x.Ratio * x.VolaV1Vol);
                VolaV1ChangeInPremium = SpreadLegs.Sum(x => x.Ratio * x.VolaV1ChangeInPremium);
                VolaV1Spot = leg?.VolaV1Spot ?? double.NaN;
                VolaV1Underlying = leg?.VolaV1Underlying ?? double.NaN;
                break;
            case SubscriptionFieldType.DeltaAdjTheo when model == 2 && SpreadLegs.DistinctBy(x => x.VolaV2Sequence).Count() == 1:
                leg = SpreadLegs.FirstOrDefault();
                VolaV2Sequence = SpreadLegs.Max(x => x.VolaV2Sequence);
                VolaV2PriceMetric = SpreadLegs.Min(x => x.VolaV2PriceMetric);
                VolaV2Theo = SpreadLegs.Sum(x => x.Ratio * x.VolaV2Theo);
                VolaV2TheoAdj = SpreadLegs.Sum(x => x.Ratio * x.VolaV2TheoAdj);
                VolaV2Vol = SpreadLegs.Sum(x => x.Ratio * x.VolaV2Vol);
                VolaV2ChangeInPremium = SpreadLegs.Sum(x => x.Ratio * x.VolaV2ChangeInPremium);
                VolaV2Spot = leg?.VolaV2Spot ?? double.NaN;
                VolaV2Underlying = leg?.VolaV2Underlying ?? double.NaN;
                break;
            case SubscriptionFieldType.DeltaAdjTheo when model == 3 && SpreadLegs.DistinctBy(x => x.VolaV3Sequence).Count() == 1:
                leg = SpreadLegs.FirstOrDefault();
                VolaV3Sequence = SpreadLegs.Max(x => x.VolaV3Sequence);
                VolaV3PriceMetric = SpreadLegs.Min(x => x.VolaV3PriceMetric);
                VolaV3Theo = SpreadLegs.Sum(x => x.Ratio * x.VolaV3Theo);
                VolaV3TheoAdj = SpreadLegs.Sum(x => x.Ratio * x.VolaV3TheoAdj);
                VolaV3Vol = SpreadLegs.Sum(x => x.Ratio * x.VolaV3Vol);
                VolaV3ChangeInPremium = SpreadLegs.Sum(x => x.Ratio * x.VolaV3ChangeInPremium);
                VolaV3Spot = leg?.VolaV3Spot ?? double.NaN;
                VolaV3Underlying = leg?.VolaV3Underlying ?? double.NaN;
                break;
        }

        Updated?.Invoke(this, type, model);
    }
}