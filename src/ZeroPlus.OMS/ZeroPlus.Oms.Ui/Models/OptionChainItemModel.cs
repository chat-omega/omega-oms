using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class OptionChainItemModel : BindableBase, IOmsDataSubscriber, IOmsPositionSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private int _notifiersCount;
        private Notifier[] _notifiers;

        #region Notifier Properties
        [NotifyProperty]
        public partial bool CallTheoMktCross { get; set; }
        [NotifyProperty]
        public partial bool PutTheoMktCross { get; set; }
        [NotifyProperty]
        public partial bool CallITM { get; set; }
        [NotifyProperty]
        public partial bool PutITM { get; set; }
        [NotifyProperty]
        public partial double Strike { get; set; }
        [NotifyProperty]
        public partial string CallSymbol { get; set; }
        [NotifyProperty]
        public partial double CallAsk { get; set; }
        [NotifyProperty]
        public partial double CallAskDelta { get; set; }
        [NotifyProperty]
        public partial string CallAskExch { get; set; }
        [NotifyProperty]
        public partial double CallAskSize { get; set; }
        [NotifyProperty]
        public partial double CallBASprd { get; set; }
        [NotifyProperty]
        public partial double CallBid { get; set; }
        [NotifyProperty]
        public partial double CallBidInterpolated { get; set; }
        [NotifyProperty]
        public partial double CallAskInterpolated { get; set; }
        [NotifyProperty]
        public partial bool CallBidInterpolatedEdge { get; set; }
        [NotifyProperty]
        public partial bool CallAskInterpolatedEdge { get; set; }
        [NotifyProperty]
        public partial double CallBidDelta { get; set; }
        [NotifyProperty]
        public partial string CallBidExch { get; set; }
        [NotifyProperty]
        public partial double CallBidSize { get; set; }
        [NotifyProperty]
        public partial double CallDelta { get; set; }
        [NotifyProperty]
        public partial double CallExtr { get; set; }
        [NotifyProperty]
        public partial double CallGamma { get; set; }
        [NotifyProperty]
        public partial double CallHigh { get; set; }
        [NotifyProperty]
        public partial double CallIntr { get; set; }
        [NotifyProperty]
        public partial double CallIvAsk { get; set; }
        [NotifyProperty]
        public partial double CallIvBid { get; set; }
        [NotifyProperty]
        public partial double CallImplied { get; set; }
        [NotifyProperty]
        public partial double CallLast { get; set; }
        [NotifyProperty]
        public partial double CallLastSize { get; set; }
        [NotifyProperty]
        public partial double CallLow { get; set; }
        [NotifyProperty]
        public partial double CallMark { get; set; }
        [NotifyProperty]
        public partial double CallNetChange { get; set; }
        [NotifyProperty]
        public partial double CallNetDelta { get; set; }
        [NotifyProperty]
        public partial double CallNetGamma { get; set; }
        [NotifyProperty]
        public partial double CallNetRho { get; set; }
        [NotifyProperty]
        public partial double CallNetTheta { get; set; }
        [NotifyProperty]
        public partial double CallNetVega { get; set; }
        [NotifyProperty]
        public partial double CallOpenInt { get; set; }
        [NotifyProperty]
        public partial double CallPnl { get; set; }
        [NotifyProperty]
        public partial bool CallAttempted { get; set; }
        [NotifyProperty]
        public partial double CallPosition { get; set; }
        [NotifyProperty]
        public partial double CallPrevClose { get; set; }
        [NotifyProperty]
        public partial double CallRho { get; set; }
        [NotifyProperty]
        public partial double CallSize { get; set; }
        [NotifyProperty]
        public partial double CallTheo { get; set; }
        [NotifyProperty]
        public partial double CallAdjTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallSmoothedAdjTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaTheoV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaAdjTheoV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallPriceMetricV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaTheoV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaAdjTheoV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallPriceMetricV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaTheoV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaAdjTheoV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallPriceMetricV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaTheoV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallVolaAdjTheoV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallPriceMetricV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallLastBidTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallLastAskTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallBidTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double CallAskTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutLastBidTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutLastAskTheoSpread { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutBidTheoSpreadEma { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutAskTheoSpreadEma { get; set; }
        [NotifyProperty]
        public partial double CallTheoEdge { get; set; }
        [NotifyProperty]
        public partial double CallTheta { get; set; }
        [NotifyProperty]
        public partial double CallTradeQty { get; set; }
        [NotifyProperty]
        public partial double CallVega { get; set; }
        [NotifyProperty]
        public partial double CallVol { get; set; }
        [NotifyProperty]
        public partial double CallUnrealizedPL { get; set; }
        [NotifyProperty]
        public partial double CallTradingPL { get; set; }
        [NotifyProperty]
        public partial int CallTradingNetQty { get; set; }
        [NotifyProperty]
        public partial double CallTradingAveCost { get; set; }
        [NotifyProperty]
        public partial double CallNotionalValue { get; set; }
        [NotifyProperty]
        public partial bool CallNetQtyInitialized { get; set; }
        [NotifyProperty]
        public partial double CallNetPL { get; set; }
        [NotifyProperty]
        public partial double CallMarketValue { get; set; }
        [NotifyProperty]
        public partial double CallDayPL { get; set; }
        [NotifyProperty]
        public partial int CallTradingSellQty { get; set; }
        [NotifyProperty]
        public partial double CallTradingSellAvePrice { get; set; }
        [NotifyProperty]
        public partial int CallTradingBuyQty { get; set; }
        [NotifyProperty]
        public partial double CallRealizedPL { get; set; }
        [NotifyProperty]
        public partial int CallOpeningQty { get; set; }
        [NotifyProperty]
        public partial double CallOpeningCost { get; set; }
        [NotifyProperty]
        public partial double CallMarkedCost { get; set; }
        [NotifyProperty]
        public partial double CallAveCost { get; set; }
        [NotifyProperty]
        public partial double CallTradingBuyAvePrice { get; set; }
        [NotifyProperty]
        public partial double CallUserNetQty { get; set; }
        [NotifyProperty]
        public partial string PutSymbol { get; set; }
        [NotifyProperty]
        public partial double PutAsk { get; set; }
        [NotifyProperty]
        public partial double PutAskDelta { get; set; }
        [NotifyProperty]
        public partial string PutAskExch { get; set; }
        [NotifyProperty]
        public partial double PutAskSize { get; set; }
        [NotifyProperty]
        public partial double PutBASprd { get; set; }
        [NotifyProperty]
        public partial double PutBid { get; set; }
        [NotifyProperty]
        public partial double PutBidInterpolated { get; set; }
        [NotifyProperty]
        public partial double PutAskInterpolated { get; set; }
        [NotifyProperty]
        public partial bool PutBidInterpolatedEdge { get; set; }
        [NotifyProperty]
        public partial bool PutAskInterpolatedEdge { get; set; }
        [NotifyProperty]
        public partial double PutBidDelta { get; set; }
        [NotifyProperty]
        public partial string PutBidExch { get; set; }
        [NotifyProperty]
        public partial double PutBidSize { get; set; }
        [NotifyProperty]
        public partial double PutDelta { get; set; }
        [NotifyProperty]
        public partial double PutExtr { get; set; }
        [NotifyProperty]
        public partial double PutPnl { get; set; }
        [NotifyProperty]
        public partial bool PutAttempted { get; set; }
        [NotifyProperty]
        public partial double PutGamma { get; set; }
        [NotifyProperty]
        public partial double PutHigh { get; set; }
        [NotifyProperty]
        public partial double PutIntr { get; set; }
        [NotifyProperty]
        public partial double PutIvAsk { get; set; }
        [NotifyProperty]
        public partial double PutIvBid { get; set; }
        [NotifyProperty]
        public partial double PutImplied { get; set; }
        [NotifyProperty]
        public partial double PutLast { get; set; }
        [NotifyProperty]
        public partial double PutLastSize { get; set; }
        [NotifyProperty]
        public partial double PutLow { get; set; }
        [NotifyProperty]
        public partial double PutMark { get; set; }
        [NotifyProperty]
        public partial double PutNetChange { get; set; }
        [NotifyProperty]
        public partial double PutNetDelta { get; set; }
        [NotifyProperty]
        public partial double PutNetGamma { get; set; }
        [NotifyProperty]
        public partial double PutNetRho { get; set; }
        [NotifyProperty]
        public partial double PutNetTheta { get; set; }
        [NotifyProperty]
        public partial double PutNetVega { get; set; }
        [NotifyProperty]
        public partial double PutOpenInt { get; set; }
        [NotifyProperty]
        public partial double PutPosition { get; set; }
        [NotifyProperty]
        public partial double PutPrevClose { get; set; }
        [NotifyProperty]
        public partial double PutRho { get; set; }
        [NotifyProperty]
        public partial double PutSize { get; set; }
        [NotifyProperty]
        public partial double PutTheo { get; set; }
        [NotifyProperty]
        public partial double PutAdjTheo { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaTheoV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaAdjTheoV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutPriceMetricV0 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaTheoV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaAdjTheoV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutPriceMetricV1 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaTheoV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaAdjTheoV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutPriceMetricV2 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaTheoV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutVolaAdjTheoV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutPriceMetricV3 { get; set; }
        [NotifyProperty(CheckEquality = false)]
        public partial double PutSmoothedAdjTheo { get; set; }
        [NotifyProperty]
        public partial double PutTheoEdge { get; set; }
        [NotifyProperty]
        public partial double PutTheta { get; set; }
        [NotifyProperty]
        public partial double PutTradeQty { get; set; }
        [NotifyProperty]
        public partial double PutVega { get; set; }
        [NotifyProperty]
        public partial double PutVol { get; set; }
        [NotifyProperty]
        public partial double PutUnrealizedPL { get; set; }
        [NotifyProperty]
        public partial double PutTradingPL { get; set; }
        [NotifyProperty]
        public partial int PutTradingNetQty { get; set; }
        [NotifyProperty]
        public partial double PutTradingAveCost { get; set; }
        [NotifyProperty]
        public partial double PutNotionalValue { get; set; }
        [NotifyProperty]
        public partial bool PutNetQtyInitialized { get; set; }
        [NotifyProperty]
        public partial double PutNetPL { get; set; }
        [NotifyProperty]
        public partial double PutMarketValue { get; set; }
        [NotifyProperty]
        public partial double PutDayPL { get; set; }
        [NotifyProperty]
        public partial int PutTradingSellQty { get; set; }
        [NotifyProperty]
        public partial double PutTradingSellAvePrice { get; set; }
        [NotifyProperty]
        public partial int PutTradingBuyQty { get; set; }
        [NotifyProperty]
        public partial double PutRealizedPL { get; set; }
        [NotifyProperty]
        public partial int PutOpeningQty { get; set; }
        [NotifyProperty]
        public partial double PutOpeningCost { get; set; }
        [NotifyProperty]
        public partial double PutMarkedCost { get; set; }
        [NotifyProperty]
        public partial double PutAveCost { get; set; }
        [NotifyProperty]
        public partial double PutTradingBuyAvePrice { get; set; }
        [NotifyProperty]
        public partial bool IsDisposed { get; set; }
        [NotifyProperty]
        public partial double PutUserNetQty { get; set; }
        [NotifyProperty]
        public partial double CallBidTradeInterpolated { get; set; }
        [NotifyProperty]
        public partial double CallBestBidBase { get; set; }
        [NotifyProperty]
        public partial double CallBestAskBase { get; set; }
        [NotifyProperty]
        public partial double CallBestBidUnderlying { get; set; }
        [NotifyProperty]
        public partial double CallBestAskUnderlying { get; set; }
        [NotifyProperty]
        public partial double CallBidTradeBase { get; set; }
        [NotifyProperty]
        public partial double CallAskTradeBase { get; set; }
        [NotifyProperty]
        public partial double CallBidTradeUnderlying { get; set; }
        [NotifyProperty]
        public partial double CallAskTradeUnderlying { get; set; }
        [NotifyProperty]
        public partial DateTime CallBidTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial DateTime CallAskTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial double CallAskTradeInterpolated { get; set; }
        [NotifyProperty]
        public partial bool CallTradeInterpolatedCrossed { get; set; }
        [NotifyProperty]
        public partial int CallBidTradeCount { get; set; }
        [NotifyProperty]
        public partial int CallAskTradeCount { get; set; }
        [NotifyProperty]
        public partial double CallBestBid { get; set; }
        [NotifyProperty]
        public partial double CallBestAsk { get; set; }
        [NotifyProperty]
        public partial int CallCustTradeBidCount { get; set; }
        [NotifyProperty]
        public partial int CallCustTradeAskCount { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidInterpolated { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskInterpolated { get; set; }
        [NotifyProperty]
        public partial double CallMktMkrCross { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidInterpolatedBase { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskInterpolatedBase { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidInterpolatedNoChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskInterpolatedNoChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidInterpolatedBaseNoChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskInterpolatedBaseNoChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidAvgChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskAvgChange { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeBidInterpolatedUnderlyingPrice { get; set; }
        [NotifyProperty]
        public partial double CallCustTradeAskInterpolatedUnderlyingPrice { get; set; }
        [NotifyProperty]
        public partial bool CallCustBidTradeIsLatest { get; set; }
        [NotifyProperty]
        public partial bool CallCustAskTradeIsLatest { get; set; }
        [NotifyProperty]
        public partial OptionChainMktMkrMode CallBidCustCrossed { get; set; }
        [NotifyProperty]
        public partial OptionChainMktMkrMode CallAskCustCrossed { get; set; }
        [NotifyProperty]
        public partial DateTime CallCustBidTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial DateTime CallCustAskTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial int CallCustBidTradeTimespan { get; set; }
        [NotifyProperty]
        public partial int CallCustAskTradeTimespan { get; set; }
        [NotifyProperty]
        public partial int PutCustTradeBidCount { get; set; }
        [NotifyProperty]
        public partial int PutCustTradeAskCount { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidInterpolated { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskInterpolated { get; set; }
        [NotifyProperty]
        public partial double PutMktMkrCross { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidInterpolatedBase { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskInterpolatedBase { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidInterpolatedNoChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskInterpolatedNoChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidInterpolatedBaseNoChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskInterpolatedBaseNoChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidAvgChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskAvgChange { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeBidInterpolatedUnderlyingPrice { get; set; }
        [NotifyProperty]
        public partial double PutCustTradeAskInterpolatedUnderlyingPrice { get; set; }
        [NotifyProperty]
        public partial bool PutCustBidTradeIsLatest { get; set; }
        [NotifyProperty]
        public partial bool PutCustAskTradeIsLatest { get; set; }
        [NotifyProperty]
        public partial OptionChainMktMkrMode PutBidCustCrossed { get; set; }
        [NotifyProperty]
        public partial OptionChainMktMkrMode PutAskCustCrossed { get; set; }
        [NotifyProperty]
        public partial DateTime PutCustBidTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial DateTime PutCustAskTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial int PutCustBidTradeTimespan { get; set; }
        [NotifyProperty]
        public partial int PutCustAskTradeTimespan { get; set; }
        [NotifyProperty]
        public partial double PutBidTradeInterpolated { get; set; }
        [NotifyProperty]
        public partial int PutBidTradeCount { get; set; }
        [NotifyProperty]
        public partial int PutAskTradeCount { get; set; }
        [NotifyProperty]
        public partial double PutBestBid { get; set; }
        [NotifyProperty]
        public partial double PutBestAsk { get; set; }
        [NotifyProperty]
        public partial double PutAskTradeInterpolated { get; set; }
        [NotifyProperty]
        public partial bool PutTradeInterpolatedCrossed { get; set; }
        [NotifyProperty]
        public partial double PutBestBidBase { get; set; }
        [NotifyProperty]
        public partial double PutBestAskBase { get; set; }
        [NotifyProperty]
        public partial double PutBestBidUnderlying { get; set; }
        [NotifyProperty]
        public partial double PutBestAskUnderlying { get; set; }
        [NotifyProperty]
        public partial double PutBidTradeBase { get; set; }
        [NotifyProperty]
        public partial double PutAskTradeBase { get; set; }
        [NotifyProperty]
        public partial double PutBidTradeUnderlying { get; set; }
        [NotifyProperty]
        public partial double PutAskTradeUnderlying { get; set; }
        [NotifyProperty]
        public partial DateTime PutBidTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial DateTime PutAskTradeTimestamp { get; set; }
        [NotifyProperty]
        public partial double CallBestBuyPrice { get; set; }
        [NotifyProperty]
        public partial double CallBestBuyPriceUnder { get; set; }
        [NotifyProperty]
        public partial double CallBestBuyPriceDelta { get; set; }
        [NotifyProperty]
        public partial double CallBestBuyPriceAdjusted { get; set; }
        [NotifyProperty]
        public partial double CallBestSellPrice { get; set; }
        [NotifyProperty]
        public partial double CallBestSellPriceUnder { get; set; }
        [NotifyProperty]
        public partial double CallBestSellPriceDelta { get; set; }
        [NotifyProperty]
        public partial double CallBestSellPriceAdjusted { get; set; }
        [NotifyProperty]
        public partial Side? CallOpeningSide { get; set; }
        [NotifyProperty]
        public partial Side? CallHardSide { get; set; }
        [NotifyProperty]
        public partial double PutBestBuyPrice { get; set; }
        [NotifyProperty]
        public partial double PutBestBuyPriceUnder { get; set; }
        [NotifyProperty]
        public partial double PutBestBuyPriceDelta { get; set; }
        [NotifyProperty]
        public partial double PutBestBuyPriceAdjusted { get; set; }
        [NotifyProperty]
        public partial double PutBestSellPrice { get; set; }
        [NotifyProperty]
        public partial double PutBestSellPriceUnder { get; set; }
        [NotifyProperty]
        public partial double PutBestSellPriceDelta { get; set; }
        [NotifyProperty]
        public partial double PutBestSellPriceAdjusted { get; set; }
        [NotifyProperty]
        public partial Side? PutOpeningSide { get; set; }
        [NotifyProperty]
        public partial Side? PutHardSide { get; set; }
        [NotifyProperty]
        public partial double CallHighestBid { get; set; }
        [NotifyProperty]
        public partial double CallLowestAsk { get; set; }
        [NotifyProperty]
        public partial double CallHighestBidBase { get; set; }
        [NotifyProperty]
        public partial DateTime CallHighestBidTime { get; set; }
        [NotifyProperty]
        public partial DateTime CallLowestAskTime { get; set; }
        [NotifyProperty]
        public partial DateTime PutHighestBidTime { get; set; }
        [NotifyProperty]
        public partial DateTime PutLowestAskTime { get; set; }
        [NotifyProperty]
        public partial double CallLowestAskBase { get; set; }
        [NotifyProperty]
        public partial double CallHighestBidUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double CallLowestAskUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double PutHighestBid { get; set; }
        [NotifyProperty]
        public partial double PutLowestAsk { get; set; }
        [NotifyProperty]
        public partial double PutHighestBidBase { get; set; }
        [NotifyProperty]
        public partial double PutLowestAskBase { get; set; }
        [NotifyProperty]
        public partial double PutHighestBidUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double PutLowestAskUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBid { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAsk { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBidBase { get; set; }
        [NotifyProperty]
        public partial DateTime CallSkewAdjustedHighestBidTime { get; set; }
        [NotifyProperty]
        public partial DateTime CallSkewAdjustedLowestAskTime { get; set; }
        [NotifyProperty]
        public partial DateTime PutSkewAdjustedHighestBidTime { get; set; }
        [NotifyProperty]
        public partial DateTime PutSkewAdjustedLowestAskTime { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAskBase { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBidUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAskUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBid { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAsk { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBidBase { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAskBase { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBidUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAskUnderlyingMid { get; set; }
        [NotifyProperty]
        public partial double CallHighestBidLong { get; set; }
        [NotifyProperty]
        public partial double CallLowestAskLong { get; set; }
        [NotifyProperty]
        public partial double CallHighestBidBaseLong { get; set; }
        [NotifyProperty]
        public partial DateTime CallHighestBidTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime CallLowestAskTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime PutHighestBidTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime PutLowestAskTimeLong { get; set; }
        [NotifyProperty]
        public partial double CallLowestAskBaseLong { get; set; }
        [NotifyProperty]
        public partial double CallHighestBidUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double CallLowestAskUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double PutHighestBidLong { get; set; }
        [NotifyProperty]
        public partial double PutLowestAskLong { get; set; }
        [NotifyProperty]
        public partial double PutHighestBidBaseLong { get; set; }
        [NotifyProperty]
        public partial double PutLowestAskBaseLong { get; set; }
        [NotifyProperty]
        public partial double PutHighestBidUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double PutLowestAskUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBidLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAskLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBidBaseLong { get; set; }
        [NotifyProperty]
        public partial DateTime CallSkewAdjustedHighestBidTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime CallSkewAdjustedLowestAskTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime PutSkewAdjustedHighestBidTimeLong { get; set; }
        [NotifyProperty]
        public partial DateTime PutSkewAdjustedLowestAskTimeLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAskBaseLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedHighestBidUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double CallSkewAdjustedLowestAskUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBidLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAskLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBidBaseLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAskBaseLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedHighestBidUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double PutSkewAdjustedLowestAskUnderlyingMidLong { get; set; }
        [NotifyProperty]
        public partial double CallImpliedBid { get; set; }
        [NotifyProperty]
        public partial double CallImpliedAsk { get; set; }
        [NotifyProperty]
        public partial ImpliedQuoteModel CallImpliedBidRecord { get; set; }
        [NotifyProperty]
        public partial ImpliedQuoteModel CallImpliedAskRecord { get; set; }
        [NotifyProperty]
        public partial double CallImpliedBidMinusAsk { get; set; }
        [NotifyProperty]
        public partial double PutImpliedBid { get; set; }
        [NotifyProperty]
        public partial double PutImpliedAsk { get; set; }
        [NotifyProperty]
        public partial ImpliedQuoteModel PutImpliedBidRecord { get; set; }
        [NotifyProperty]
        public partial ImpliedQuoteModel PutImpliedAskRecord { get; set; }
        [NotifyProperty]
        public partial double PutImpliedBidMinusAsk { get; set; }
        #endregion

        private int _prevRequest;
        private bool _callITM;
        private bool _putITM;
        private string _callSymbol;
        private string _callAskExch;
        private bool _callBidInterpolatedEdge;
        private bool _callAskInterpolatedEdge;
        private string _callBidExch;
        private int _callTradingNetQty;
        private bool _callNetQtyInitialized;
        private int _callTradingSellQty;
        private int _callTradingBuyQty;
        private int _callOpeningQty;
        private string _putSymbol;
        private string _putAskExch;
        private bool _putBidInterpolatedEdge;
        private bool _putAskInterpolatedEdge;
        private string _putBidExch;
        private int _putTradingNetQty;
        private bool _putNetQtyInitialized;
        private int _putTradingSellQty;
        private int _putTradingBuyQty;
        private int _putOpeningQty;

        private bool _callTheoMktCross;
        private bool _putTheoMktCross;

        private Side? _callOpeningSide;
        private Side? _callHardSide;

        private Side? _putOpeningSide;
        private Side? _putHardSide;

        private DateTime _callHighestBidTime;
        private DateTime _callLowestAskTime;
        private DateTime _callSkewAdjustedHighestBidTime;
        private DateTime _callSkewAdjustedLowestAskTime;

        private DateTime _callHighestBidTimeLong;
        private DateTime _callLowestAskTimeLong;
        private DateTime _callSkewAdjustedHighestBidTimeLong;
        private DateTime _callSkewAdjustedLowestAskTimeLong;

        private DateTime _putHighestBidTime;
        private DateTime _putLowestAskTime;
        private DateTime _putSkewAdjustedHighestBidTime;
        private DateTime _putSkewAdjustedLowestAskTime;

        private DateTime _putHighestBidTimeLong;
        private DateTime _putLowestAskTimeLong;
        private DateTime _putSkewAdjustedHighestBidTimeLong;
        private DateTime _putSkewAdjustedLowestAskTimeLong;

        private bool _IsDisposed;

        private bool _callTradeInterpolatedCrossed;
        private bool _putTradeInterpolatedCrossed;
        private int _callBidTradeCount = 0;
        private int _callAskTradeCount = 0;
        private int _putBidTradeCount = 0;
        private int _putAskTradeCount = 0;

        int _callCustTradeBidCount;
        int _callCustTradeAskCount;
        double _callCustTradeBidInterpolated = double.NaN;
        double _callCustTradeAskInterpolated = double.NaN;
        double _callMktMkrCross = double.NaN;
        double _callCustTradeBidInterpolatedBase = double.NaN;
        double _callCustTradeAskInterpolatedBase = double.NaN;
        double _callCustTradeBidInterpolatedNoChange = double.NaN;
        double _callCustTradeAskInterpolatedNoChange = double.NaN;
        double _callCustTradeBidInterpolatedBaseNoChange = double.NaN;
        double _callCustTradeAskInterpolatedBaseNoChange = double.NaN;
        double _callCustTradeBidAvgChange = double.NaN;
        double _callCustTradeAskAvgChange = double.NaN;
        double _callCustTradeBidInterpolatedUnderlyingPrice = double.NaN;
        double _callCustTradeAskInterpolatedUnderlyingPrice = double.NaN;
        bool _callCustBidTradeIsLatest;
        bool _callCustAskTradeIsLatest;
        OptionChainMktMkrMode _callBidCustCrossed;
        OptionChainMktMkrMode _callAskCustCrossed;
        DateTime _callCustBidTradeTimestamp;
        DateTime _callCustAskTradeTimestamp;

        int _putCustTradeBidCount;
        int _putCustTradeAskCount;
        double _putCustTradeBidInterpolated = double.NaN;
        double _putCustTradeAskInterpolated = double.NaN;
        double _putMktMkrCross = double.NaN;
        double _putCustTradeBidInterpolatedBase = double.NaN;
        double _putCustTradeAskInterpolatedBase = double.NaN;
        double _putCustTradeBidInterpolatedNoChange = double.NaN;
        double _putCustTradeAskInterpolatedNoChange = double.NaN;
        double _putCustTradeBidInterpolatedBaseNoChange = double.NaN;
        double _putCustTradeAskInterpolatedBaseNoChange = double.NaN;
        double _putCustTradeBidAvgChange = double.NaN;
        double _putCustTradeAskAvgChange = double.NaN;
        double _putCustTradeBidInterpolatedUnderlyingPrice = double.NaN;
        double _putCustTradeAskInterpolatedUnderlyingPrice = double.NaN;
        bool _putCustBidTradeIsLatest;
        bool _putCustAskTradeIsLatest;
        OptionChainMktMkrMode _putBidCustCrossed;
        OptionChainMktMkrMode _putAskCustCrossed;
        DateTime _putCustBidTradeTimestamp;
        DateTime _putCustAskTradeTimestamp;
        DateTime _putBidTradeTimestamp;
        DateTime _putAskTradeTimestamp;
        private int _callCustBidTradeTimespan;
        private int _callCustAskTradeTimespan;
        private int _putCustBidTradeTimespan;
        private int _putCustAskTradeTimespan;
        private double _Strike = double.NaN;
        private double _callAsk = double.NaN;
        private double _callPnl = double.NaN;
        private double _callAskDelta = double.NaN;
        private double _callAskSize = double.NaN;
        private double _callBASprd = double.NaN;
        private double _callBid = double.NaN;
        private double _callBidInterpolated = double.NaN;
        private double _callAskInterpolated = double.NaN;
        private double _callBidDelta = double.NaN;
        private double _callBidSize = double.NaN;
        private double _callDelta = double.NaN;
        private double _callExtr = double.NaN;
        private double _callGamma = double.NaN;
        private double _callHigh = double.NaN;
        private double _callIntr = double.NaN;
        private double _callIvAsk = double.NaN;
        private double _callIvBid = double.NaN;
        private double _callImplied = double.NaN;
        private double _callLast = double.NaN;
        private double _callLastSize = double.NaN;
        private double _callLow = double.NaN;
        private double _callMark = double.NaN;
        private double _callNetChange = double.NaN;
        private double _callNetDelta = double.NaN;
        private double _callNetGamma = double.NaN;
        private double _callNetRho = double.NaN;
        private double _callNetTheta = double.NaN;
        private double _callNetVega = double.NaN;
        private double _callOpenInt = double.NaN;
        private double _callPosition = double.NaN;
        private double _callPrevClose = double.NaN;
        private double _callRho = double.NaN;
        private double _callSize = double.NaN;
        private double _callTheo = double.NaN;
        private double _callAdjTheo = double.NaN;
        private double _callTheoEdge = double.NaN;
        private double _callTheta = double.NaN;
        private double _callTradeQty = double.NaN;
        private double _callVega = double.NaN;
        private double _callVol = double.NaN;
        private double _callUnrealizedPL = double.NaN;
        private double _callTradingPL = double.NaN;
        private double _callTradingAveCost = double.NaN;
        private double _callNotionalValue = double.NaN;
        private double _callNetPL = double.NaN;
        private double _callMarketValue = double.NaN;
        private double _callDayPL = double.NaN;
        private double _callTradingSellAvePrice = double.NaN;
        private double _callRealizedPL = double.NaN;
        private double _callOpeningCost = double.NaN;
        private double _callMarkedCost = double.NaN;
        private double _callAveCost = double.NaN;
        private double _callTradingBuyAvePrice = double.NaN;
        private double _callUserNetQty = double.NaN;
        private double _callImpliedBid = double.NaN;
        private double _callImpliedAsk = double.NaN;
        private double _putImpliedBid = double.NaN;
        private double _putImpliedAsk = double.NaN;
        private ImpliedQuoteModel _callImpliedBidRecord = new();
        private ImpliedQuoteModel _callImpliedAskRecord = new();
        private ImpliedQuoteModel _putImpliedBidRecord = new();
        private ImpliedQuoteModel _putImpliedAskRecord = new();
        private double _callImpliedBidMinusAsk = double.NaN;
        private double _putImpliedBidMinusAsk = double.NaN;
        private double _putAsk = double.NaN;
        private double _putAskDelta = double.NaN;
        private double _putAskSize = double.NaN;
        private double _putBASprd = double.NaN;
        private double _putBid = double.NaN;
        private double _putBidInterpolated = double.NaN;
        private double _putAskInterpolated = double.NaN;
        private double _putPnl = double.NaN;
        private double _putBidDelta = double.NaN;
        private double _putBidSize = double.NaN;
        private double _putDelta = double.NaN;
        private double _putExtr = double.NaN;
        private double _putGamma = double.NaN;
        private double _putHigh = double.NaN;
        private double _putIntr = double.NaN;
        private double _putIvAsk = double.NaN;
        private double _putIvBid = double.NaN;
        private double _putImplied = double.NaN;
        private double _putLast = double.NaN;
        private double _putLastSize = double.NaN;
        private double _putLow = double.NaN;
        private double _putMark = double.NaN;
        private double _putNetChange = double.NaN;
        private double _putNetDelta = double.NaN;
        private double _putNetGamma = double.NaN;
        private double _putNetRho = double.NaN;
        private double _putNetTheta = double.NaN;
        private double _putNetVega = double.NaN;
        private double _putOpenInt = double.NaN;
        private double _putPosition = double.NaN;
        private double _putPrevClose = double.NaN;
        private double _putRho = double.NaN;
        private double _putSize = double.NaN;
        private double _putTheo = double.NaN;
        private double _putAdjTheo = double.NaN;
        private double _putTheoEdge = double.NaN;
        private double _putTheta = double.NaN;
        private double _putTradeQty = double.NaN;
        private double _putVega = double.NaN;
        private double _putVol = double.NaN;
        private double _putUnrealizedPL = double.NaN;
        private double _putTradingPL = double.NaN;
        private double _putTradingAveCost = double.NaN;
        private double _putNotionalValue = double.NaN;
        private double _putNetPL = double.NaN;
        private double _putMarketValue = double.NaN;
        private double _putDayPL = double.NaN;
        private double _putTradingSellAvePrice = double.NaN;
        private double _putRealizedPL = double.NaN;
        private double _putOpeningCost = double.NaN;
        private double _putMarkedCost = double.NaN;
        private double _putAveCost = double.NaN;
        private double _putTradingBuyAvePrice = double.NaN;
        private double _putUserNetQty = double.NaN;
        private double _callBestBuyPrice = double.NaN;
        private double _callBestBuyPriceUnder = double.NaN;
        private double _callBestBuyPriceDelta = double.NaN;
        private double _callBestBuyPriceAdjusted = double.NaN;
        private double _callBestSellPrice = double.NaN;
        private double _callBestSellPriceUnder = double.NaN;
        private double _callBestSellPriceDelta = double.NaN;
        private double _callBestSellPriceAdjusted = double.NaN;
        private double _putBestBuyPrice = double.NaN;
        private double _putBestBuyPriceUnder = double.NaN;
        private double _putBestBuyPriceDelta = double.NaN;
        private double _putBestBuyPriceAdjusted = double.NaN;
        private double _putBestSellPrice = double.NaN;
        private double _putBestSellPriceUnder = double.NaN;
        private double _putBestSellPriceDelta = double.NaN;
        private double _putBestSellPriceAdjusted = double.NaN;
        private double _callHighestBid;
        private double _callLowestAsk;
        private double _callHighestBidBase;
        private double _callLowestAskBase;
        private double _callHighestBidUnderlyingMid;
        private double _callLowestAskUnderlyingMid;
        private double _callSkewAdjustedHighestBid;
        private double _callSkewAdjustedLowestAsk;
        private double _callSkewAdjustedHighestBidBase;
        private double _callSkewAdjustedLowestAskBase;
        private double _callSkewAdjustedHighestBidUnderlyingMid;
        private double _callSkewAdjustedLowestAskUnderlyingMid;
        private double _callHighestBidLong;
        private double _callLowestAskLong;
        private double _callHighestBidBaseLong;
        private double _callLowestAskBaseLong;
        private double _callHighestBidUnderlyingMidLong;
        private double _callLowestAskUnderlyingMidLong;
        private double _callSkewAdjustedHighestBidLong;
        private double _callSkewAdjustedLowestAskLong;
        private double _callSkewAdjustedHighestBidBaseLong;
        private double _callSkewAdjustedLowestAskBaseLong;
        private double _callSkewAdjustedHighestBidUnderlyingMidLong;
        private double _callSkewAdjustedLowestAskUnderlyingMidLong;
        private double _putHighestBid;
        private double _putLowestAsk;
        private double _putHighestBidBase;
        private double _putLowestAskBase;
        private double _putHighestBidUnderlyingMid;
        private double _putLowestAskUnderlyingMid;
        private double _putSkewAdjustedHighestBid;
        private double _putSkewAdjustedLowestAsk;
        private double _putSkewAdjustedHighestBidBase;
        private double _putSkewAdjustedLowestAskBase;
        private double _putSkewAdjustedHighestBidUnderlyingMid;
        private double _putSkewAdjustedLowestAskUnderlyingMid;
        private double _putHighestBidLong;
        private double _putLowestAskLong;
        private double _putHighestBidBaseLong;
        private double _putLowestAskBaseLong;
        private double _putHighestBidUnderlyingMidLong;
        private double _putLowestAskUnderlyingMidLong;
        private double _putSkewAdjustedHighestBidLong;
        private double _putSkewAdjustedLowestAskLong;
        private double _putSkewAdjustedHighestBidBaseLong;
        private double _putSkewAdjustedLowestAskBaseLong;
        private double _putSkewAdjustedHighestBidUnderlyingMidLong;
        private double _putSkewAdjustedLowestAskUnderlyingMidLong;
        private double _callBidTradeInterpolated = double.NaN;
        private double _callAskTradeInterpolated = double.NaN;
        private double _putBidTradeInterpolated = double.NaN;
        private double _putAskTradeInterpolated = double.NaN;
        private double _callBestBid = double.NaN;
        private double _callBestAsk = double.NaN;
        private double _putBestBid = double.NaN;
        private double _putBestAsk = double.NaN;
        private double _callLastBidTheoSpread = double.NaN;
        private double _callLastAskTheoSpread = double.NaN;
        private double _callBidTheoSpreadEma = double.NaN;
        private double _callAskTheoSpreadEma = double.NaN;
        private double _putLastBidTheoSpread = double.NaN;
        private double _putLastAskTheoSpread = double.NaN;
        private double _putBidTheoSpreadEma = double.NaN;
        private double _putAskTheoSpreadEma = double.NaN;
        private double _callSmoothedAdjTheo = double.NaN;
        private double _callVolaTheoV0 = double.NaN;
        private double _callVolaAdjTheoV0 = double.NaN;
        private double _callPriceMetricV0 = double.NaN;
        private double _callVolaTheoV1 = double.NaN;
        private double _callVolaAdjTheoV1 = double.NaN;
        private double _callPriceMetricV1 = double.NaN;
        private double _callVolaTheoV2 = double.NaN;
        private double _callVolaAdjTheoV2 = double.NaN;
        private double _callPriceMetricV2 = double.NaN;
        private double _callVolaTheoV3 = double.NaN;
        private double _callVolaAdjTheoV3 = double.NaN;
        private double _callPriceMetricV3 = double.NaN;
        private double _putVolaTheoV0 = double.NaN;
        private double _putVolaAdjTheoV0 = double.NaN;
        private double _putPriceMetricV0 = double.NaN;
        private double _putSmoothedAdjTheo = double.NaN;
        private double _putVolaTheoV1 = double.NaN;
        private double _putVolaAdjTheoV1 = double.NaN;
        private double _putPriceMetricV1 = double.NaN;
        private double _putVolaTheoV2 = double.NaN;
        private double _putVolaAdjTheoV2 = double.NaN;
        private double _putPriceMetricV2 = double.NaN;
        private double _putVolaTheoV3 = double.NaN;
        private double _putVolaAdjTheoV3 = double.NaN;
        private double _putPriceMetricV3 = double.NaN;



        private readonly PortfolioManagerModel _portfolioManagerModel;
        protected OmsCore OmsCore { get; } = ServiceLocator.GetService<OmsCore>();

        internal Option CallOption { get; private set; }
        internal Option PutOption { get; private set; }
        public DateTime Expiration { get; set; }
        public bool Subscribed { get; private set; }

        private bool _callAttempted;
        private bool _putAttempted;
        double _callBestBidBase;
        double _callBestAskBase;
        double _callBestBidUnderlying;
        double _callBestAskUnderlying;
        double _callBidTradeBase;
        double _callAskTradeBase;
        double _callBidTradeUnderlying;
        double _callAskTradeUnderlying;
        DateTime _callBidTradeTimestamp;
        DateTime _callAskTradeTimestamp;


        double _putBestBidBase;
        double _putBestAskBase;
        double _putBestBidUnderlying;
        double _putBestAskUnderlying;
        double _putBidTradeBase;
        double _putAskTradeBase;
        double _putBidTradeUnderlying;
        double _putAskTradeUnderlying;


        public OptionChainItemModel(IGrouping<double, Option> strikes, PortfolioManagerModel portfolioManagerModel)
        {

            _portfolioManagerModel = portfolioManagerModel;
            _notifiers = GetGeneratedNotifiers();
            _notifiersCount = _notifiers.Length;
            for (int i = 0; i < _notifiersCount; i++)
                _notifiers[i].Updated();
            CallOption = strikes.FirstOrDefault(x => x.Type == OptionType.CALL);
            PutOption = strikes.FirstOrDefault(x => x.Type == OptionType.PUT);
            if (CallOption == null && PutOption == null)
            {
                return;
            }
            else if (CallOption == null)
            {
                CallOption = OptionsHelper.GetOptionFromSymbol(OptionsHelper.GetSymbolFromComponents(PutOption.UnderlyingSymbol, PutOption.Expiration, "CALL", PutOption.Strike));
            }
            else
            {
                PutOption ??= OptionsHelper.GetOptionFromSymbol(OptionsHelper.GetSymbolFromComponents(CallOption.UnderlyingSymbol, CallOption.Expiration, "PUT", CallOption.Strike));
            }

            CallSymbol = CallOption.OptionSymbol;
            Expiration = CallOption.Expiration;
            PutSymbol = PutOption?.OptionSymbol;
            Strike = strikes.Key;
        }

        internal void UpdateChanges(double lastPrice)
        {
            try
            {
                CheckMoneyness(lastPrice);
                UpdateTimespans();
                for (int i = 0; i < _notifiersCount; i++)
                {
                    if (_notifiers[i].IsUpdated)
                    {
                        _notifiers[i].IsUpdated = false;
                        RaisePropertyChanged(_notifiers[i].Name);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateChanges));
            }
        }

        internal void RequestSnapshots()
        {
            OmsCore.QuoteClient.GetSnapshotAsync(CallOption.OptionSymbol, SubscriptionFieldType.PreviousClose).ContinueWith(t => CallPrevClose = t.Result);
            OmsCore.QuoteClient.GetSnapshotAsync(PutOption.OptionSymbol, SubscriptionFieldType.PreviousClose).ContinueWith(t => PutPrevClose = t.Result);
        }

        public void SubscribeDataAsync(int bestPriceLookback)
        {
            if (IsDisposed || Subscribed)
            {
                return;
            }
            SubscribeData(CallOption, bestPriceLookback);
            SubscribeData(PutOption, bestPriceLookback);
            Subscribed = true;
        }

        public void UnsubscribeDataAsync()
        {
            if (Subscribed)
            {
                Subscribed = false;
                UnsubscribeData(CallOption);
                UnsubscribeData(PutOption);
            }
        }

        private void UnsubscribeDataAsync(string symbol, SubscriptionFieldType type)
        {
            OmsCore.QuoteClient.Unsubscribe(symbol, type, this);
            OmsCore.GreekClient.Unsubscribe(symbol, type, this);
            OmsCore.UpdateManager.Unsubscribe(symbol, type, this);
        }

        private void SubscribeData(Option option, int bestPriceLookback)
        {
            if (option != null && !string.IsNullOrWhiteSpace(option.OptionSymbol))
            {

                string id = option.Type + " " + option.UnderlyingSymbol + " " + option.Expiration.ToString("MMM-dd-yy") + " " + Convert.ToDecimal(option.Strike).ToString("G29");
                _portfolioManagerModel.Subscribe(id, SubscriptionFieldType.FirmSpreadPosition, this);
                _portfolioManagerModel.Subscribe(id, SubscriptionFieldType.FirmSymbolPosition, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Spread, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.LastSize, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.NetChange, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.OpenInterest, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Volume, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.AskExchange, this);
                OmsCore.QuoteClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.BidExchange, this);
                OmsCore.UpdateManager.Subscribe(option.OptionSymbol, SubscriptionFieldType.DeltaAdjTheo, this);
                OmsCore.UpdateManager.Subscribe(option.OptionSymbol, SubscriptionFieldType.DerivedValues, this);
                OmsCore.UpdateManager.Subscribe(option.OptionSymbol, SubscriptionFieldType.TheoToMarketSpread, this);
                OmsCore.OrderClient.SubscribePosition(option.OptionSymbol, OmsCore.Config.DefaultAccount, this);
                OmsCore.OrderClient.SubscribePosition(option.OptionSymbol, OmsCore.User.Username, this);
                OmsCore.GreekClient.Subscribe(option.OptionSymbol, SubscriptionFieldType.Greeks, this);

                if (bestPriceLookback > 0)
                {
                    if (_prevRequest != bestPriceLookback)
                    {
                        _prevRequest = bestPriceLookback;
                        OmsCore.HerculesClient.RequestSymbolEdgeMapAsync(option.OptionSymbol, DateTime.Today - TimeSpan.FromDays(bestPriceLookback)).ContinueWith(t =>
                        {
                            if (t.Result != null &&
                                t.Result.Any())
                            {
                                IEnumerable<ZeroPlus.Models.Data.Edge.SymbolEdgeMap> edges = t.Result;
                                double sampleUnder = edges.OrderByDescending(x => x.Date).FirstOrDefault().BestBuyPriceUnderlying;

                                foreach (ZeroPlus.Models.Data.Edge.SymbolEdgeMap edge in edges)
                                {
                                    double adjBuy = ((sampleUnder - edge.BestBuyPriceUnderlying) * edge.BestBuyPriceDelta) + edge.BestBuyPrice;
                                    double adjSell = ((sampleUnder - edge.BestSellPriceUnderlying) * edge.BestSellPriceDelta) + edge.BestSellPrice;
                                    switch (option.Type)
                                    {
                                        case OptionType.CALL:
                                            if ((double.IsNaN(CallBestBuyPrice) || adjBuy < CallBestBuyPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                                            {
                                                CallBestBuyPrice = edge.BestBuyPrice;
                                                CallBestBuyPriceUnder = edge.BestBuyPriceUnderlying;
                                                CallBestBuyPriceDelta = edge.BestBuyPriceDelta;
                                            }
                                            if ((double.IsNaN(CallBestSellPrice) || adjSell > CallBestSellPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                                            {
                                                CallBestSellPrice = edge.BestSellPrice;
                                                CallBestSellPriceUnder = edge.BestSellPriceUnderlying;
                                                CallBestSellPriceDelta = edge.BestSellPriceDelta;
                                            }
                                            break;
                                        case OptionType.PUT:
                                            if ((double.IsNaN(PutBestBuyPrice) || adjBuy < PutBestBuyPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Buy)
                                            {
                                                PutBestBuyPrice = edge.BestBuyPrice;
                                                PutBestBuyPriceUnder = edge.BestBuyPriceUnderlying;
                                                PutBestBuyPriceDelta = edge.BestBuyPriceDelta;
                                            }
                                            if ((double.IsNaN(PutBestSellPrice) || adjSell > PutBestSellPrice) && edge.OpeningSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                                            {
                                                PutBestSellPrice = edge.BestSellPrice;
                                                PutBestSellPriceUnder = edge.BestSellPriceUnderlying;
                                                PutBestSellPriceDelta = edge.BestSellPriceDelta;
                                            }
                                            break;
                                    }
                                }
                                switch (option.Type)
                                {
                                    case OptionType.CALL:
                                        if (edges.Select(x => x.OpeningSide).Distinct().Count() == 1)
                                        {
                                            CallOpeningSide = edges.FirstOrDefault()?.OpeningSide;
                                        }
                                        if (edges.Select(x => x.HardSide).Distinct().Count() == 1)
                                        {
                                            CallHardSide = edges.FirstOrDefault()?.HardSide;
                                        }
                                        break;
                                    case OptionType.PUT:
                                        if (edges.Select(x => x.OpeningSide).Distinct().Count() == 1)
                                        {
                                            PutOpeningSide = edges.FirstOrDefault()?.OpeningSide;
                                        }
                                        if (edges.Select(x => x.HardSide).Distinct().Count() == 1)
                                        {
                                            PutHardSide = edges.FirstOrDefault()?.HardSide;
                                        }
                                        break;
                                }

                                OmsCore.QuoteClient.Subscribe(option.UnderlyingSymbol, SubscriptionFieldType.MidPoint, this);
                            }
                        });
                    }
                    else
                    {
                        OmsCore.QuoteClient.Subscribe(option.UnderlyingSymbol, SubscriptionFieldType.MidPoint, this);
                    }
                }
            }
        }

        private void UnsubscribeData(Option option)
        {
            if (option != null && !string.IsNullOrWhiteSpace(option.OptionSymbol))
            {
                string id = option.Type.ToString() + " " + option.UnderlyingSymbol + " " + option.Expiration.ToString("MMM-dd-yy") + " " + option.Strike.ToString("G29");
                _portfolioManagerModel.Unsubscribe(id, SubscriptionFieldType.FirmSpreadPosition, this);
                _portfolioManagerModel.Unsubscribe(id, SubscriptionFieldType.FirmSymbolPosition, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Ask, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Bid, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.AskSize, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.BidSize, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Spread, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.High, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.LastPrice, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.LastSize, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Low, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.NetChange, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.OpenInterest, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Volume, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.AskExchange, this);
                OmsCore.QuoteClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.BidExchange, this);
                OmsCore.QuoteClient.Unsubscribe(option.UnderlyingSymbol, SubscriptionFieldType.MidPoint, this);
                OmsCore.UpdateManager.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.DeltaAdjTheo, this);
                OmsCore.UpdateManager.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.DerivedValues, this);
                OmsCore.UpdateManager.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.TheoToMarketSpread, this);
                OmsCore.OrderClient.UnsubscribePosition(option.OptionSymbol, OmsCore.Config.DefaultAccount, this);
                OmsCore.OrderClient.UnsubscribePosition(option.OptionSymbol, OmsCore.User.Username, this);
                OmsCore.GreekClient.Unsubscribe(option.OptionSymbol, SubscriptionFieldType.Greeks, this);
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

                string keyName = key.Symbol;
                SubscriptionFieldType keyType = key.Type;
                if (keyName == CallSymbol)
                {
                    CallUpdate(keyType, value);
                }
                else if (key.Symbol == PutSymbol)
                {
                    PutUpdate(keyType, value);
                }
                else if (keyType == SubscriptionFieldType.FirmSpreadPosition || keyType == SubscriptionFieldType.FirmSymbolPosition && keyName.Length > 0)
                {
                    if (keyName[0] == 'C')
                    {
                        CallUpdate(keyType, value);
                    }
                    else if (keyName[0] == 'P')
                    {
                        PutUpdate(keyType, value);
                    }
                }
                else if (keyName == CallOption.UnderlyingSymbol &&
                         keyType == SubscriptionFieldType.MidPoint && value is double midPoint)
                {
                    DeltaAdjustPrices(midPoint);
                }
                else
                {
                    UnsubscribeDataAsync(keyName, keyType);
                    return;
                }
            }
            catch (TaskCanceledException) { /* Ignore */ }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        private void DeltaAdjustPrices(double midPoint)
        {
            CallBestBuyPriceAdjusted = ((midPoint - CallBestBuyPriceUnder) * CallBestBuyPriceDelta) + CallBestBuyPrice;
            CallBestSellPriceAdjusted = ((midPoint - CallBestSellPriceUnder) * CallBestSellPriceDelta) + CallBestSellPrice;
            PutBestBuyPriceAdjusted = ((midPoint - PutBestBuyPriceUnder) * PutBestBuyPriceDelta) + PutBestBuyPrice;
            PutBestSellPriceAdjusted = ((midPoint - PutBestSellPriceUnder) * PutBestSellPriceDelta) + PutBestSellPrice;
        }

        private void UpdateTimespans()
        {
            DateTime nowEastern = DateTime.Now.ToEastern();
            CallCustBidTradeTimespan = CallCustBidTradeTimestamp.Date == DateTime.Today ? (int)(nowEastern - CallCustBidTradeTimestamp).TotalMinutes : 0;
            CallCustAskTradeTimespan = CallCustAskTradeTimestamp.Date == DateTime.Today ? (int)(nowEastern - CallCustAskTradeTimestamp).TotalMinutes : 0;
            PutCustBidTradeTimespan = PutCustBidTradeTimestamp.Date == DateTime.Today ? (int)(nowEastern - PutCustBidTradeTimestamp).TotalMinutes : 0;
            PutCustAskTradeTimespan = PutCustAskTradeTimestamp.Date == DateTime.Today ? (int)(nowEastern - PutCustAskTradeTimestamp).TotalMinutes : 0;
        }

        private void CheckMoneyness(double last)
        {
            bool callItm = Strike < last;
            bool putItm = Strike > last;
            string[] changes = new string[2];
            if (CallITM != callItm)
            {
                CallITM = callItm;
                changes[0] = nameof(CallITM);
            }

            if (PutITM != putItm)
            {
                PutITM = putItm;
                changes[1] = nameof(PutITM);
            }
        }

        internal void Dispose()
        {
            try
            {
                IsDisposed = true;
                _notifiers = null;
                DisposeGeneratedNotifiers();
                _portfolioManagerModel.UnsubscribeAll(this);
                OmsCore.QuoteClient.UnsubscribeAll(this);
                OmsCore.GreekClient.UnsubscribeAll(this);
                OmsCore.UpdateManager.UnsubscribeAll(this);
                OmsCore.OrderClient.UnsubscribeAllPosition(this);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        public void SubscibedPositionUpdateValue(Tuple<string, string> key, object value)
        {
            try
            {
                string symbol = key.Item1;
                string account = key.Item2;
                if (value == null || !(symbol == CallOption.OptionSymbol || symbol == PutOption.OptionSymbol))
                {
                    return;
                }
                else if (account == OmsCore.User.Username && value is double userNetQty)
                {
                    if (symbol == CallSymbol)
                    {
                        CallUserNetQty = userNetQty;
                    }
                    else if (symbol == PutSymbol)
                    {
                        PutUserNetQty = userNetQty;
                    }
                }
                else if (value is Comms.Models.Data.Trading.OMSSendPosition position)
                {
                    if (symbol == CallSymbol)
                    {
                        CallPositionUpdate(position);
                    }
                    else if (symbol == PutSymbol)
                    {
                        PutPositionUpdate(position);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscibedPositionUpdateValue)}");
            }
        }

        private void CallPositionUpdate(Comms.Models.Data.Trading.OMSSendPosition position)
        {
            CallUnrealizedPL = position.UnrealizedPL;
            CallTradingPL = position.TradingPL;
            CallTradingNetQty = position.TradingNetQty;
            CallTradingAveCost = position.TradingAveCost;
            CallNotionalValue = position.NotionalValue;
            CallPosition = position.NetQty;
            CallNetQtyInitialized = true;
            CallNetPL = position.NetPL;
            CallMarketValue = position.MarketValue;
            CallDayPL = position.DayPL;
            CallTradingSellQty = position.TradingSellQty;
            CallTradingSellAvePrice = position.TradingSellAvePrice;
            CallTradingBuyQty = position.TradingBuyQty;
            CallRealizedPL = position.RealizedPL;
            CallOpeningQty = position.OpeningQty;
            CallOpeningCost = position.OpeningCost;
            CallMarkedCost = position.MarkedCost;
            CallAveCost = position.AveCost;
            CallTradingBuyAvePrice = position.TradingBuyAvePrice;
        }

        private void PutPositionUpdate(Comms.Models.Data.Trading.OMSSendPosition position)
        {
            PutUnrealizedPL = position.UnrealizedPL;
            PutTradingPL = position.TradingPL;
            PutTradingNetQty = position.TradingNetQty;
            PutTradingAveCost = position.TradingAveCost;
            PutNotionalValue = position.NotionalValue;
            PutPosition = position.NetQty;
            PutNetQtyInitialized = true;
            PutNetPL = position.NetPL;
            PutMarketValue = position.MarketValue;
            PutDayPL = position.DayPL;
            PutTradingSellQty = position.TradingSellQty;
            PutTradingSellAvePrice = position.TradingSellAvePrice;
            PutTradingBuyQty = position.TradingBuyQty;
            PutRealizedPL = position.RealizedPL;
            PutOpeningQty = position.OpeningQty;
            PutOpeningCost = position.OpeningCost;
            PutMarkedCost = position.MarkedCost;
            PutAveCost = position.AveCost;
            PutTradingBuyAvePrice = position.TradingBuyAvePrice;
        }

        private void CallUpdate(SubscriptionFieldType quoteType, object value)
        {
            switch (quoteType)
            {
                case SubscriptionFieldType.FirmSpreadPosition when value is IPosition position:
                    CallPnl = position.AdjustedPnl;
                    break;
                case SubscriptionFieldType.FirmSymbolPosition when value is IPosition position:
                    CallAttempted = position.TotalSubmissions > 0;
                    break;
                case SubscriptionFieldType.Ask:
                    if (value is double ask && ask != CallAsk)
                    {
                        CallAskDelta = ask - CallAsk;
                        CallAsk = ask;
                        CallAskInterpolatedEdge = CallAskInterpolated > CallAsk;
                        CallTheoMktCross = CallAdjTheo < CallBid || CallAdjTheo > CallAsk;
                    }
                    break;
                case SubscriptionFieldType.AskExchange:
                    if (value is string askexch && askexch != CallAskExch)
                    {
                        CallAskExch = askexch;
                    }
                    break;
                case SubscriptionFieldType.AskSize:
                    if (value is double asksize && asksize != CallAskSize)
                    {
                        CallAskSize = asksize;
                    }
                    break;
                case SubscriptionFieldType.Spread:
                    if (value is double spread && spread != CallBASprd)
                    {
                        CallBASprd = spread;
                    }
                    break;
                case SubscriptionFieldType.Bid:
                    if (value is double bid && bid != CallBid)
                    {
                        CallBidDelta = bid - CallBid;
                        CallBid = bid;
                        CallBidInterpolatedEdge = CallBidInterpolated < CallBid;
                        CallTheoMktCross = CallAdjTheo < CallBid || CallAdjTheo > CallAsk;
                    }
                    break;
                case SubscriptionFieldType.DerivedValues:
                    if (value is DerivedValueUpdateModel update)
                    {
                        CallBidInterpolated = update.InterpolatedBidUpdate;
                        CallBidInterpolatedEdge = CallBidInterpolated < CallBid;
                        CallAskInterpolated = update.InterpolatedAskUpdate;
                        CallAskInterpolatedEdge = CallAskInterpolated > CallAsk;
                        CallBidTradeInterpolated = update.BidTradeUpdate;
                        CallAskTradeInterpolated = update.AskTradeUpdate;
                        CallTradeInterpolatedCrossed = update.BidTradeUpdate > update.AskTradeUpdate;
                        CallBidTradeCount = update.BidTradeCount;
                        CallAskTradeCount = update.AskTradeCount;
                        CallBestBid = update.BestBidUpdate;
                        CallBestAsk = update.BestAskUpdate;
                        CallBestBidBase = update.BestBidBase;
                        CallBestAskBase = update.BestAskBase;
                        CallBestBidUnderlying = update.BestBidUnderlying;
                        CallBestAskUnderlying = update.BestAskUnderlying;
                        CallBidTradeBase = update.BidTradeBase;
                        CallAskTradeBase = update.AskTradeBase;
                        CallBidTradeUnderlying = update.BidTradeUnderlying;
                        CallAskTradeUnderlying = update.AskTradeUnderlying;
                        CallBidTradeTimestamp = update.BidTradeTimestamp;
                        CallAskTradeTimestamp = update.AskTradeTimestamp;

                        CallCustTradeBidCount = update.CustTradeBidCount;
                        CallCustTradeAskCount = update.CustTradeAskCount;
                        CallCustTradeBidAvgChange = update.CustTradeBidAvgChange;
                        CallCustTradeAskAvgChange = update.CustTradeAskAvgChange;
                        CallCustTradeBidInterpolated = update.CustTradeBid;
                        CallCustTradeAskInterpolated = update.CustTradeAsk;
                        CallMktMkrCross = update.CustTradeAsk - update.CustTradeBid;
                        CallCustTradeBidInterpolatedBase = update.CustTradeBidBase;
                        CallCustTradeAskInterpolatedBase = update.CustTradeAskBase;
                        CallCustTradeBidInterpolatedNoChange = update.CustTradeBidNoChange;
                        CallCustTradeAskInterpolatedNoChange = update.CustTradeAskNoChange;
                        CallCustTradeBidInterpolatedBaseNoChange = update.CustTradeBidBaseNoChange;
                        CallCustTradeAskInterpolatedBaseNoChange = update.CustTradeAskBaseNoChange;
                        CallCustTradeBidInterpolatedUnderlyingPrice = update.CustTradeBidUnderlyingPrice;
                        CallCustTradeAskInterpolatedUnderlyingPrice = update.CustTradeAskUnderlyingPrice;
                        CallCustBidTradeIsLatest = update.CustBidTradeIsLatest;
                        CallCustAskTradeIsLatest = update.CustAskTradeIsLatest;
                        CallCustBidTradeTimestamp = update.CustBidTradeTimestamp;
                        CallCustAskTradeTimestamp = update.CustAskTradeTimestamp;

                        CallImpliedBid = update.ImpliedBid;
                        CallImpliedAsk = update.ImpliedAsk;

                        CallImpliedBidRecord = new(update, isBid: true);
                        CallImpliedAskRecord = new(update, isBid: false);

                        CallImpliedBidMinusAsk = CallImpliedBid - CallImpliedAsk;

                        if (update.HighestBidLowestAskResult != null)
                        {
                            CallHighestBid = update.HighestBidLowestAskResult.HighestBid;
                            CallLowestAsk = update.HighestBidLowestAskResult.LowestAsk;
                            CallHighestBidTime = ConvertTime(update.HighestBidLowestAskResult.HighestBidTime);
                            CallLowestAskTime = ConvertTime(update.HighestBidLowestAskResult.LowestAskTime);
                            CallHighestBidBase = update.HighestBidLowestAskResult.HighestBidBase;
                            CallLowestAskBase = update.HighestBidLowestAskResult.LowestAskBase;
                            CallHighestBidUnderlyingMid = update.HighestBidLowestAskResult.HighestBidUnderlyingMid;
                            CallLowestAskUnderlyingMid = update.HighestBidLowestAskResult.LowestAskUnderlyingMid;
                            CallSkewAdjustedHighestBid = update.HighestBidLowestAskResult.SkewAdjustedHighestBid;
                            CallSkewAdjustedLowestAsk = update.HighestBidLowestAskResult.SkewAdjustedLowestAsk;
                            CallSkewAdjustedHighestBidTime = ConvertTime(update.HighestBidLowestAskResult.SkewAdjustedHighestBidTime);
                            CallSkewAdjustedLowestAskTime = ConvertTime(update.HighestBidLowestAskResult.SkewAdjustedLowestAskTime);
                            CallSkewAdjustedHighestBidBase = update.HighestBidLowestAskResult.SkewAdjustedHighestBidBase;
                            CallSkewAdjustedLowestAskBase = update.HighestBidLowestAskResult.SkewAdjustedLowestAskBase;
                            CallSkewAdjustedHighestBidUnderlyingMid = update.HighestBidLowestAskResult.SkewAdjustedHighestBidUnderlyingMid;
                            CallSkewAdjustedLowestAskUnderlyingMid = update.HighestBidLowestAskResult.SkewAdjustedLowestAskUnderlyingMid;
                        }

                        if (update.HighestBidLowestAskResultLong != null)
                        {
                            CallHighestBidLong = update.HighestBidLowestAskResultLong.HighestBid;
                            CallLowestAskLong = update.HighestBidLowestAskResultLong.LowestAsk;
                            CallHighestBidTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.HighestBidTime);
                            CallLowestAskTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.LowestAskTime);
                            CallHighestBidBaseLong = update.HighestBidLowestAskResultLong.HighestBidBase;
                            CallLowestAskBaseLong = update.HighestBidLowestAskResultLong.LowestAskBase;
                            CallHighestBidUnderlyingMidLong = update.HighestBidLowestAskResultLong.HighestBidUnderlyingMid;
                            CallLowestAskUnderlyingMidLong = update.HighestBidLowestAskResultLong.LowestAskUnderlyingMid;
                            CallSkewAdjustedHighestBidLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBid;
                            CallSkewAdjustedLowestAskLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAsk;
                            CallSkewAdjustedHighestBidTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidTime);
                            CallSkewAdjustedLowestAskTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskTime);
                            CallSkewAdjustedHighestBidBaseLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidBase;
                            CallSkewAdjustedLowestAskBaseLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskBase;
                            CallSkewAdjustedHighestBidUnderlyingMidLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidUnderlyingMid;
                            CallSkewAdjustedLowestAskUnderlyingMidLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskUnderlyingMid;
                        }

                        if (update.CustTradeBid > update.CustTradeAsk)
                        {
                            if (update.CustTradeBid > CallAdjTheo)
                            {
                                CallBidCustCrossed = OptionChainMktMkrMode.CrossedAndAboveTheo;
                            }
                            else
                            {
                                CallBidCustCrossed = OptionChainMktMkrMode.CrossedMarket;
                            }
                        }
                        else if (update.CustTradeBid > CallAdjTheo)
                        {
                            CallBidCustCrossed = OptionChainMktMkrMode.AboveTheo;
                        }
                        else
                        {
                            CallBidCustCrossed = OptionChainMktMkrMode.Normal;
                        }

                        if (update.CustTradeBid > update.CustTradeAsk)
                        {
                            if (update.CustTradeAsk < CallAdjTheo)
                            {
                                CallAskCustCrossed = OptionChainMktMkrMode.CrossedAndAboveTheo;
                            }
                            else
                            {
                                CallAskCustCrossed = OptionChainMktMkrMode.CrossedMarket;
                            }
                        }
                        else if (update.CustTradeAsk < CallAdjTheo)
                        {
                            CallAskCustCrossed = OptionChainMktMkrMode.AboveTheo;
                        }
                        else
                        {
                            CallAskCustCrossed = OptionChainMktMkrMode.Normal;
                        }
                    }
                    break;
                case SubscriptionFieldType.BidExchange:
                    if (value is string bidexchange && bidexchange != CallBidExch)
                    {
                        CallBidExch = bidexchange;
                    }
                    break;
                case SubscriptionFieldType.BidSize:
                    if (value is double bidsize && bidsize != CallBidSize)
                    {
                        CallBidSize = bidsize;
                    }
                    break;
                case SubscriptionFieldType.High:
                    if (value is double high && high != CallHigh)
                    {
                        CallHigh = high;
                    }
                    break;
                case SubscriptionFieldType.LastPrice:
                    if (value is double lastprice && lastprice != CallLast)
                    {
                        CallLast = lastprice;
                    }
                    break;
                case SubscriptionFieldType.LastSize:
                    if (value is double lastsize && lastsize != CallLastSize)
                    {
                        CallLastSize = lastsize;
                    }
                    break;
                case SubscriptionFieldType.Low:
                    if (value is double low && low != CallLow)
                    {
                        CallLow = low;
                    }
                    break;
                case SubscriptionFieldType.MidPoint:
                    if (value is double midpoint && midpoint != CallMark)
                    {
                        CallMark = midpoint;
                    }
                    break;
                case SubscriptionFieldType.NetChange:
                    if (value is double netchange && netchange != CallNetChange)
                    {
                        CallNetChange = netchange;
                    }
                    break;
                case SubscriptionFieldType.OpenInterest:
                    if (value is double openinterest && openinterest != CallOpenInt)
                    {
                        CallOpenInt = openinterest;
                    }
                    break;
                case SubscriptionFieldType.Volume:
                    if (value is double volume && volume != CallVol)
                    {
                        CallVol = volume;
                    }
                    break;
                case SubscriptionFieldType.DeltaAdjTheo:
                    if (value is double deltaAdjTheo && deltaAdjTheo != CallAdjTheo)
                    {
                        CallAdjTheo = deltaAdjTheo;
                        CallTheoMktCross = CallAdjTheo < CallBid || CallAdjTheo > CallAsk;
                    }
                    else if (value is DeltaAdjTheo adjTheo && adjTheo.DeltaAdjustedTheo != CallAdjTheo)
                    {
                        switch (adjTheo.ModelId)
                        {
                            case 0:
                                CallAdjTheo = adjTheo.DeltaAdjustedTheo;
                                CallSmoothedAdjTheo = adjTheo.SmoothedDeltaAdjustedTheo;
                                CallVolaTheoV0 = adjTheo.SecondaryTheo;
                                CallVolaAdjTheoV0 = adjTheo.SecondaryTheoAdj;
                                CallPriceMetricV0 = adjTheo.PriceMetric;
                                CallTheoMktCross = CallAdjTheo < CallBid || CallAdjTheo > CallAsk;
                                break;
                            case 1:
                                CallVolaTheoV1 = adjTheo.SecondaryTheo;
                                CallVolaAdjTheoV1 = adjTheo.SecondaryTheoAdj;
                                CallPriceMetricV1 = adjTheo.PriceMetric;
                                break;
                            case 2:
                                CallVolaTheoV2 = adjTheo.SecondaryTheo;
                                CallVolaAdjTheoV2 = adjTheo.SecondaryTheoAdj;
                                CallPriceMetricV2 = adjTheo.PriceMetric;
                                break;
                            case 3:
                                CallVolaTheoV3 = adjTheo.SecondaryTheo;
                                CallVolaAdjTheoV3 = adjTheo.SecondaryTheoAdj;
                                CallPriceMetricV3 = adjTheo.PriceMetric;
                                break;
                        }
                    }
                    break;
                case SubscriptionFieldType.TheoToMarketSpread when value is TheoToMarketSpread theoToMarketSpread:
                    CallLastBidTheoSpread = theoToMarketSpread.LastBidTheoSpread;
                    CallLastAskTheoSpread = theoToMarketSpread.LastAskTheoSpread;
                    CallBidTheoSpreadEma = theoToMarketSpread.BidTheoSpreadEma;
                    CallAskTheoSpreadEma = theoToMarketSpread.AskTheoSpreadEma;
                    break;
                case SubscriptionFieldType.Greeks:
                    if (value is GreekUpdate greekUpdate)
                    {
                        CallDelta = greekUpdate.Delta;
                        CallGamma = greekUpdate.Gamma;
                        CallVega = greekUpdate.Vega;
                        CallTheta = greekUpdate.Theta;
                        CallRho = greekUpdate.Rho;
                        CallImplied = greekUpdate.Implied;
                        CallTheo = greekUpdate.Theo;
                    }

                    break;
                default:
                    return;
            }
        }

        private void PutUpdate(SubscriptionFieldType quoteType, object value)
        {
            switch (quoteType)
            {
                case SubscriptionFieldType.FirmSpreadPosition when value is IPosition position:
                    PutPnl = position.AdjustedPnl;
                    break;
                case SubscriptionFieldType.FirmSymbolPosition when value is IPosition position:
                    PutAttempted = position.TotalSubmissions > 0;
                    break;
                case SubscriptionFieldType.Ask:
                    if (value is double ask && ask != PutAsk)
                    {
                        PutAskDelta = ask - PutAsk;
                        PutAsk = ask;
                        PutTheoMktCross = PutAdjTheo < PutBid || PutAdjTheo > PutAsk;
                    }
                    break;
                case SubscriptionFieldType.AskExchange:
                    if (value is string askexch && askexch != PutAskExch)
                    {
                        PutAskExch = askexch;
                    }
                    break;
                case SubscriptionFieldType.AskSize:
                    if (value is double asksize && asksize != PutAskSize)
                    {
                        PutAskSize = asksize;
                    }
                    break;
                case SubscriptionFieldType.Spread:
                    if (value is double spread && spread != PutBASprd)
                    {
                        PutBASprd = spread;
                    }
                    break;
                case SubscriptionFieldType.Bid:
                    if (value is double bid && bid != PutBid)
                    {
                        PutBidDelta = bid - PutBid;
                        PutBid = bid;
                        PutTheoMktCross = PutAdjTheo < PutBid || PutAdjTheo > PutAsk;
                    }
                    break;
                case SubscriptionFieldType.DerivedValues:
                    if (value is DerivedValueUpdateModel update)
                    {
                        PutBidInterpolated = update.InterpolatedBidUpdate;
                        PutBidInterpolatedEdge = PutBidInterpolated < PutBid;
                        PutAskInterpolated = update.InterpolatedAskUpdate;
                        PutAskInterpolatedEdge = PutAskInterpolated > PutAsk;
                        PutBidTradeInterpolated = update.BidTradeUpdate;
                        PutAskTradeInterpolated = update.AskTradeUpdate;
                        PutTradeInterpolatedCrossed = update.BidTradeUpdate > update.AskTradeUpdate;
                        PutBidTradeCount = update.BidTradeCount;
                        PutAskTradeCount = update.AskTradeCount;
                        PutBestBid = update.BestBidUpdate;
                        PutBestAsk = update.BestAskUpdate;
                        PutBestBidBase = update.BestBidBase;
                        PutBestAskBase = update.BestAskBase;
                        PutBestBidUnderlying = update.BestBidUnderlying;
                        PutBestAskUnderlying = update.BestAskUnderlying;
                        PutBidTradeBase = update.BidTradeBase;
                        PutAskTradeBase = update.AskTradeBase;
                        PutBidTradeUnderlying = update.BidTradeUnderlying;
                        PutAskTradeUnderlying = update.AskTradeUnderlying;
                        PutBidTradeTimestamp = update.BidTradeTimestamp;
                        PutAskTradeTimestamp = update.AskTradeTimestamp;

                        PutCustTradeBidCount = update.CustTradeBidCount;
                        PutCustTradeAskCount = update.CustTradeAskCount;
                        PutCustTradeBidAvgChange = update.CustTradeBidAvgChange;
                        PutCustTradeAskAvgChange = update.CustTradeAskAvgChange;
                        PutCustTradeBidInterpolated = update.CustTradeBid;
                        PutCustTradeAskInterpolated = update.CustTradeAsk;
                        PutMktMkrCross = update.CustTradeAsk - update.CustTradeBid;
                        PutCustTradeBidInterpolatedBase = update.CustTradeBidBase;
                        PutCustTradeAskInterpolatedBase = update.CustTradeAskBase;
                        PutCustTradeBidInterpolatedNoChange = update.CustTradeBidNoChange;
                        PutCustTradeAskInterpolatedNoChange = update.CustTradeAskNoChange;
                        PutCustTradeBidInterpolatedBaseNoChange = update.CustTradeBidBaseNoChange;
                        PutCustTradeAskInterpolatedBaseNoChange = update.CustTradeAskBaseNoChange;
                        PutCustTradeBidInterpolatedUnderlyingPrice = update.CustTradeBidUnderlyingPrice;
                        PutCustTradeAskInterpolatedUnderlyingPrice = update.CustTradeAskUnderlyingPrice;
                        PutCustBidTradeIsLatest = update.CustBidTradeIsLatest;
                        PutCustAskTradeIsLatest = update.CustAskTradeIsLatest;
                        PutCustBidTradeTimestamp = update.CustBidTradeTimestamp;
                        PutCustAskTradeTimestamp = update.CustAskTradeTimestamp;

                        PutImpliedBid = update.ImpliedBid;
                        PutImpliedAsk = update.ImpliedAsk;

                        PutImpliedBidRecord = new(update, isBid: true);
                        PutImpliedAskRecord = new(update, isBid: false);

                        PutImpliedBidMinusAsk = PutImpliedBid - PutImpliedAsk;

                        if (update.HighestBidLowestAskResult != null)
                        {
                            PutHighestBid = update.HighestBidLowestAskResult.HighestBid;
                            PutLowestAsk = update.HighestBidLowestAskResult.LowestAsk;
                            PutHighestBidTime = ConvertTime(update.HighestBidLowestAskResult.HighestBidTime);
                            PutLowestAskTime = ConvertTime(update.HighestBidLowestAskResult.LowestAskTime);
                            PutHighestBidBase = update.HighestBidLowestAskResult.HighestBidBase;
                            PutLowestAskBase = update.HighestBidLowestAskResult.LowestAskBase;
                            PutHighestBidUnderlyingMid = update.HighestBidLowestAskResult.HighestBidUnderlyingMid;
                            PutLowestAskUnderlyingMid = update.HighestBidLowestAskResult.LowestAskUnderlyingMid;
                            PutSkewAdjustedHighestBid = update.HighestBidLowestAskResult.SkewAdjustedHighestBid;
                            PutSkewAdjustedLowestAsk = update.HighestBidLowestAskResult.SkewAdjustedLowestAsk;
                            PutSkewAdjustedHighestBidTime = ConvertTime(update.HighestBidLowestAskResult.SkewAdjustedHighestBidTime);
                            PutSkewAdjustedLowestAskTime = ConvertTime(update.HighestBidLowestAskResult.SkewAdjustedLowestAskTime);
                            PutSkewAdjustedHighestBidBase = update.HighestBidLowestAskResult.SkewAdjustedHighestBidBase;
                            PutSkewAdjustedLowestAskBase = update.HighestBidLowestAskResult.SkewAdjustedLowestAskBase;
                            PutSkewAdjustedHighestBidUnderlyingMid = update.HighestBidLowestAskResult.SkewAdjustedHighestBidUnderlyingMid;
                            PutSkewAdjustedLowestAskUnderlyingMid = update.HighestBidLowestAskResult.SkewAdjustedLowestAskUnderlyingMid;
                        }

                        if (update.HighestBidLowestAskResultLong != null)
                        {
                            PutHighestBidLong = update.HighestBidLowestAskResultLong.HighestBid;
                            PutLowestAskLong = update.HighestBidLowestAskResultLong.LowestAsk;
                            PutHighestBidTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.HighestBidTime);
                            PutLowestAskTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.LowestAskTime);
                            PutHighestBidBaseLong = update.HighestBidLowestAskResultLong.HighestBidBase;
                            PutLowestAskBaseLong = update.HighestBidLowestAskResultLong.LowestAskBase;
                            PutHighestBidUnderlyingMidLong = update.HighestBidLowestAskResultLong.HighestBidUnderlyingMid;
                            PutLowestAskUnderlyingMidLong = update.HighestBidLowestAskResultLong.LowestAskUnderlyingMid;
                            PutSkewAdjustedHighestBidLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBid;
                            PutSkewAdjustedLowestAskLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAsk;
                            PutSkewAdjustedHighestBidTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidTime);
                            PutSkewAdjustedLowestAskTimeLong = ConvertTime(update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskTime);
                            PutSkewAdjustedHighestBidBaseLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidBase;
                            PutSkewAdjustedLowestAskBaseLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskBase;
                            PutSkewAdjustedHighestBidUnderlyingMidLong = update.HighestBidLowestAskResultLong.SkewAdjustedHighestBidUnderlyingMid;
                            PutSkewAdjustedLowestAskUnderlyingMidLong = update.HighestBidLowestAskResultLong.SkewAdjustedLowestAskUnderlyingMid;
                        }

                        if (update.CustTradeBid > update.CustTradeAsk)
                        {
                            if (update.CustTradeBid > PutAdjTheo)
                            {
                                PutBidCustCrossed = OptionChainMktMkrMode.CrossedAndAboveTheo;
                            }
                            else
                            {
                                PutBidCustCrossed = OptionChainMktMkrMode.CrossedMarket;
                            }
                        }
                        else if (update.CustTradeBid > PutAdjTheo)
                        {
                            PutBidCustCrossed = OptionChainMktMkrMode.AboveTheo;
                        }
                        else
                        {
                            PutBidCustCrossed = OptionChainMktMkrMode.Normal;
                        }

                        if (update.CustTradeBid > update.CustTradeAsk)
                        {
                            if (update.CustTradeAsk < PutAdjTheo)
                            {
                                PutAskCustCrossed = OptionChainMktMkrMode.CrossedAndAboveTheo;
                            }
                            else
                            {
                                PutAskCustCrossed = OptionChainMktMkrMode.CrossedMarket;
                            }
                        }
                        else if (update.CustTradeAsk < PutAdjTheo)
                        {
                            PutAskCustCrossed = OptionChainMktMkrMode.AboveTheo;
                        }
                        else
                        {
                            PutAskCustCrossed = OptionChainMktMkrMode.Normal;
                        }
                    }
                    break;
                case SubscriptionFieldType.BidExchange:
                    if (value is string bidexchange && bidexchange != PutBidExch)
                    {
                        PutBidExch = bidexchange;
                    }
                    break;
                case SubscriptionFieldType.BidSize:
                    if (value is double bidsize && bidsize != PutBidSize)
                    {
                        PutBidSize = bidsize;
                    }
                    break;
                case SubscriptionFieldType.High:
                    if (value is double high && high != PutHigh)
                    {
                        PutHigh = high;
                    }
                    break;
                case SubscriptionFieldType.LastPrice:
                    if (value is double lastprice && lastprice != PutLast)
                    {
                        PutLast = lastprice;
                    }
                    break;
                case SubscriptionFieldType.LastSize:
                    if (value is double lastsize && lastsize != PutLastSize)
                    {
                        PutLastSize = lastsize;
                    }
                    break;
                case SubscriptionFieldType.Low:
                    if (value is double low && low != PutLow)
                    {
                        PutLow = low;
                    }
                    break;
                case SubscriptionFieldType.MidPoint:
                    if (value is double midpoint && midpoint != PutMark)
                    {
                        PutMark = midpoint;
                    }
                    break;
                case SubscriptionFieldType.NetChange:
                    if (value is double netchange && netchange != PutNetChange)
                    {
                        PutNetChange = netchange;
                    }
                    break;
                case SubscriptionFieldType.OpenInterest:
                    if (value is double openinterest && openinterest != PutOpenInt)
                    {
                        PutOpenInt = openinterest;
                    }
                    break;
                case SubscriptionFieldType.Volume:
                    if (value is double volume && volume != PutVol)
                    {
                        PutVol = volume;
                    }
                    break;
                case SubscriptionFieldType.DeltaAdjTheo:
                    if (value is double deltaAdjTheo && deltaAdjTheo != PutAdjTheo)
                    {
                        PutAdjTheo = deltaAdjTheo;
                        PutTheoMktCross = PutAdjTheo < PutBid || PutAdjTheo > PutAsk;
                    }
                    else if (value is DeltaAdjTheo adjTheo && adjTheo.DeltaAdjustedTheo != PutAdjTheo)
                    {
                        switch (adjTheo.ModelId)
                        {
                            case 0:
                                PutAdjTheo = adjTheo.DeltaAdjustedTheo;
                                PutSmoothedAdjTheo = adjTheo.SmoothedDeltaAdjustedTheo;
                                PutVolaTheoV0 = adjTheo.SecondaryTheo;
                                PutVolaAdjTheoV0 = adjTheo.SecondaryTheoAdj;
                                PutPriceMetricV0 = adjTheo.PriceMetric;
                                PutTheoMktCross = PutAdjTheo < PutBid || PutAdjTheo > PutAsk;
                                break;
                            case 1:
                                PutVolaTheoV1 = adjTheo.SecondaryTheo;
                                PutVolaAdjTheoV1 = adjTheo.SecondaryTheoAdj;
                                PutPriceMetricV1 = adjTheo.PriceMetric;
                                break;
                            case 2:
                                PutVolaTheoV2 = adjTheo.SecondaryTheo;
                                PutVolaAdjTheoV2 = adjTheo.SecondaryTheoAdj;
                                PutPriceMetricV2 = adjTheo.PriceMetric;
                                break;
                            case 3:
                                PutVolaTheoV3 = adjTheo.SecondaryTheo;
                                PutVolaAdjTheoV3 = adjTheo.SecondaryTheoAdj;
                                PutPriceMetricV3 = adjTheo.PriceMetric;
                                break;
                        }
                    }
                    break;
                case SubscriptionFieldType.TheoToMarketSpread when value is TheoToMarketSpread theoToMarketSpread:
                    PutLastBidTheoSpread = theoToMarketSpread.LastBidTheoSpread;
                    PutLastAskTheoSpread = theoToMarketSpread.LastAskTheoSpread;
                    PutBidTheoSpreadEma = theoToMarketSpread.BidTheoSpreadEma;
                    PutAskTheoSpreadEma = theoToMarketSpread.AskTheoSpreadEma;
                    break;
                case SubscriptionFieldType.Greeks:
                    if (value is GreekUpdate greekUpdate)
                    {
                        PutDelta = greekUpdate.Delta;
                        PutGamma = greekUpdate.Gamma;
                        PutVega = greekUpdate.Vega;
                        PutTheta = greekUpdate.Theta;
                        PutRho = greekUpdate.Rho;
                        PutImplied = greekUpdate.Implied;

                        PutTheo = greekUpdate.Theo;
                    }
                    break;
                default:
                    return;
            }
        }

        private static DateTime ConvertTime(ulong skewAdjustedLowestAskTime)
        {
            try
            {
                return new DateTime((long)skewAdjustedLowestAskTime);
            }
            catch (Exception)
            {
                return default;
            }
        }
    }
}
