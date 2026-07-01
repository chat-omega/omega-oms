using ExcelDna.Integration;
using ExcelDna.Integration.Rtd;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;

namespace ZeroPlus.Oms.AddIn.Rtd
{
    [Guid("73d60c30-8a60-401d-989f-069748dfab04")]
    [ComVisible(true)]
    [ProgId(PROG_ID)]
    public class OmsAddinRtdServer : ExcelRtdServer, IOmsDataSubscriber, IOrderInfoUpdateHandler
    {
        public const string PROG_ID = "zpoms.rtd";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<SubscriptionKey, HashSet<Topic>> _subscriptionKeyToTopicMap = new();
        private static readonly ConcurrentDictionary<Topic, SubscriptionKey> _topicToSubscriptionKeyReverseMap = new();
        private static readonly ConcurrentDictionary<Topic, byte> _topicToModelIdMap = new();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public static EmaConfig EmaConfig = ServiceLocator.GetService<EmaConfig>();
        public static PortfolioManager PortfolioManager = ServiceLocator.GetService<PortfolioManager>();

        private readonly EmaCalculatorGenerator _emaCalculatorGenerator;
        public bool IsDisposed { get; set; }

        public OmsAddinRtdServer()
        {
            _emaCalculatorGenerator = new EmaCalculatorGenerator(EmaConfig);
            OmsCore.AutoTraderClient.RegisterOrderUpdateHandler(this);
            PortfolioManager.PositionUpdate = PositionUpdate;
        }

        protected override object ConnectData(Topic topic, IList<string> topicInfo, ref bool newValues)
        {
            if (topicInfo.Count != 2 && topicInfo.Count != 3)
            {
                return "Invalid arguments";
            }

            string key = topicInfo[0];
            string type = topicInfo[1];
            string model = topicInfo.Count == 3 ? topicInfo[2] : string.Empty;
            int modelId = string.IsNullOrEmpty(model) ? 0 : int.Parse(model[1].ToString());     // Default to V0 if model is not provided/not needed

            if (string.IsNullOrWhiteSpace(key))
            {
                return "Symbol can not be empty";
            }

            if (!Enum.TryParse(type, true, out SubscriptionFieldType quoteType))
            {
                return "Invalid type '" + type + "'";
            }

            if (modelId > 2 || modelId < 0)
            {
                return "Invalid instance '" + model + "'";
            }

            key = key.ToUpper();
            SubscriptionKey subscriptionKey = new(key, quoteType);
            lock (subscriptionKey)
            {
                if (!_subscriptionKeyToTopicMap.TryGetValue(subscriptionKey, out HashSet<Topic> topics))
                {
                    topics = new HashSet<Topic>();
                    _subscriptionKeyToTopicMap[subscriptionKey] = topics;
                }
                topics.Add(topic);
            }
            _topicToSubscriptionKeyReverseMap[topic] = subscriptionKey;
            _topicToModelIdMap[topic] = (byte) modelId;
            switch (quoteType)
            {
                case SubscriptionFieldType.FullHanweck:
                case SubscriptionFieldType.HanweckBidSize:
                case SubscriptionFieldType.HanweckAskSize:
                case SubscriptionFieldType.HanweckBidMCID:
                case SubscriptionFieldType.HanweckAskMCID:
                case SubscriptionFieldType.HanweckInfoBits:
                case SubscriptionFieldType.HanweckBidPrice:
                case SubscriptionFieldType.HanweckAskPrice:
                case SubscriptionFieldType.HanweckTheo:
                case SubscriptionFieldType.HanweckImpliedVolatility:
                case SubscriptionFieldType.HanweckDelta:
                case SubscriptionFieldType.HanweckGamma:
                case SubscriptionFieldType.HanweckVega:
                case SubscriptionFieldType.HanweckTheta:
                case SubscriptionFieldType.HanweckRho:
                case SubscriptionFieldType.HanweckBidVol:
                case SubscriptionFieldType.HanweckAskVol:
                case SubscriptionFieldType.HanweckMidVol:
                case SubscriptionFieldType.HanweckUBidPrice:
                case SubscriptionFieldType.HanweckUAskPrice:
                case SubscriptionFieldType.HanweckTimeValue:
                case SubscriptionFieldType.HanweckIntrinsicValue:
                case SubscriptionFieldType.HanweckFvDivs:
                case SubscriptionFieldType.HanweckSequenceNumber:
                case SubscriptionFieldType.HanweckTradeVolume:
                case SubscriptionFieldType.HanweckTimeStamp:
                case SubscriptionFieldType.HanweckCollectorTimestamp:
                case SubscriptionFieldType.HanweckCollectorTimestampNanos:
                case SubscriptionFieldType.HanweckCalculationTimestampNanos:
                case SubscriptionFieldType.HanweckBidTimestampNanos:
                case SubscriptionFieldType.HanweckAskTimestampNanos:
                case SubscriptionFieldType.HanweckUTimestampNanos:
                case SubscriptionFieldType.HanweckPersistorTimestampNanos:
                case SubscriptionFieldType.HanweckPersistorSeqNum:
                    OmsCore.UpdateManager.Subscribe(key, SubscriptionFieldType.FullHanweck, this);
                    break;
                case SubscriptionFieldType.VolaTheo:
                case SubscriptionFieldType.VolaTheoAdj:
                case SubscriptionFieldType.VolaVol:
                    OmsCore.UpdateManager.Subscribe(key, SubscriptionFieldType.DeltaAdjTheo, this);
                    break;
                case SubscriptionFieldType.FullEma:
                case SubscriptionFieldType.DebugValue:
                case SubscriptionFieldType.DeltaAdjTheo:
                case SubscriptionFieldType.DerivedValues:
                case SubscriptionFieldType.DeltaAdjTheoBase:
                case SubscriptionFieldType.TheoToMarketSpread:
                    OmsCore.UpdateManager.Subscribe(key, quoteType, this);
                    break;
                case SubscriptionFieldType.DerivedBidEma:
                case SubscriptionFieldType.DerivedAskEma:
                    EmaConfig.SelectedEmaType = EmaType.Derived;
                    _emaCalculatorGenerator.Subscribe(key, quoteType, this);
                    break;
                case SubscriptionFieldType.BidIvEma:
                case SubscriptionFieldType.AskIvEma:
                    EmaConfig.SelectedEmaType = EmaType.IV;
                    _emaCalculatorGenerator.Subscribe(key, quoteType, this);
                    break;
                case SubscriptionFieldType.DerivedBid:
                case SubscriptionFieldType.DerivedAsk:
                    OmsCore.DerivedValueGenerator.Subscribe(key, quoteType, this);
                    break;
                case SubscriptionFieldType.DeltaAdjBidTheo:
                case SubscriptionFieldType.DeltaAdjAskTheo:
                case SubscriptionFieldType.DeltaAdjTheoDelta:
                case SubscriptionFieldType.DeltaAdjTheoMid:
                case SubscriptionFieldType.DeltaAdjTheoMidTime:
                case SubscriptionFieldType.OrderUpdate:
                case SubscriptionFieldType.OrderInfoUpdate:
                case SubscriptionFieldType.FirmSymbolPosition:
                case SubscriptionFieldType.FirmInstancePosition:
                case SubscriptionFieldType.FirmSpreadPosition:
                case SubscriptionFieldType.FirmUnderlyingPosition:
                case SubscriptionFieldType.UserInstancePosition:
                case SubscriptionFieldType.UserSpreadPosition:
                case SubscriptionFieldType.UserUnderlyingPosition:
                    break;
                case SubscriptionFieldType.VolaDelta:
                case SubscriptionFieldType.VolaGamma:
                case SubscriptionFieldType.VolaVega:
                    OmsCore.UpdateManager.Subscribe(key, SubscriptionFieldType.VolaGreeks, this);
                    break;
                default:
                    OmsCore.QuoteClient.Subscribe(key, quoteType, this);
                    break;
            }
            return ExcelErrorUtil.ToComError(ExcelError.ExcelErrorNA);
        }

        protected override void DisconnectData(Topic topic)
        {
            if (_topicToSubscriptionKeyReverseMap.TryRemove(topic, out SubscriptionKey key))
            {
                bool isEmpty = false;
                lock (key)
                {
                    if (_subscriptionKeyToTopicMap.TryGetValue(key, out HashSet<Topic> topics))
                    {
                        topics.Remove(topic);
                        isEmpty = topics.Count == 0;
                    }
                }

                if (isEmpty)
                {
                    string symbol = key.Symbol;
                    SubscriptionFieldType quoteType = key.Type;
                    Unsubscribe(symbol, quoteType);
                }
                _topicToModelIdMap.TryRemove(topic, out _);
            }
        }

        private void Unsubscribe(string key, SubscriptionFieldType quoteType)
        {
            switch (quoteType)
            {
                case SubscriptionFieldType.HanweckBidSize:
                case SubscriptionFieldType.HanweckAskSize:
                case SubscriptionFieldType.HanweckBidMCID:
                case SubscriptionFieldType.HanweckAskMCID:
                case SubscriptionFieldType.HanweckInfoBits:
                case SubscriptionFieldType.HanweckBidPrice:
                case SubscriptionFieldType.HanweckAskPrice:
                case SubscriptionFieldType.HanweckTheo:
                case SubscriptionFieldType.HanweckImpliedVolatility:
                case SubscriptionFieldType.HanweckDelta:
                case SubscriptionFieldType.HanweckGamma:
                case SubscriptionFieldType.HanweckVega:
                case SubscriptionFieldType.HanweckTheta:
                case SubscriptionFieldType.HanweckRho:
                case SubscriptionFieldType.HanweckBidVol:
                case SubscriptionFieldType.HanweckAskVol:
                case SubscriptionFieldType.HanweckMidVol:
                case SubscriptionFieldType.HanweckUBidPrice:
                case SubscriptionFieldType.HanweckUAskPrice:
                case SubscriptionFieldType.HanweckTimeValue:
                case SubscriptionFieldType.HanweckIntrinsicValue:
                case SubscriptionFieldType.HanweckFvDivs:
                case SubscriptionFieldType.HanweckSequenceNumber:
                case SubscriptionFieldType.HanweckTradeVolume:
                case SubscriptionFieldType.HanweckTimeStamp:
                case SubscriptionFieldType.HanweckCollectorTimestamp:
                case SubscriptionFieldType.HanweckCollectorTimestampNanos:
                case SubscriptionFieldType.HanweckCalculationTimestampNanos:
                case SubscriptionFieldType.HanweckBidTimestampNanos:
                case SubscriptionFieldType.HanweckAskTimestampNanos:
                case SubscriptionFieldType.HanweckUTimestampNanos:
                case SubscriptionFieldType.HanweckPersistorTimestampNanos:
                case SubscriptionFieldType.HanweckPersistorSeqNum:
                case SubscriptionFieldType.FullEma:
                case SubscriptionFieldType.DebugValue:
                case SubscriptionFieldType.DerivedValues:
                case SubscriptionFieldType.TheoToMarketSpread:
                case SubscriptionFieldType.DeltaAdjTheo:
                case SubscriptionFieldType.DeltaAdjTheoBase:
                    _ = OmsCore.UpdateManager.UnsubscribeAsync(key, quoteType, this);
                    break;
                case SubscriptionFieldType.VolaTheo:
                case SubscriptionFieldType.VolaTheoAdj:
                case SubscriptionFieldType.VolaVol:
                    OmsCore.UpdateManager.UnsubscribeAsync(key, SubscriptionFieldType.DeltaAdjTheo, this);
                    break;
                case SubscriptionFieldType.VolaDelta:
                case SubscriptionFieldType.VolaGamma:
                case SubscriptionFieldType.VolaVega:
                    OmsCore.UpdateManager.UnsubscribeAsync(key, SubscriptionFieldType.VolaGreeks, this);
                    break;
                case SubscriptionFieldType.DerivedBidEma:
                case SubscriptionFieldType.DerivedAskEma:
                case SubscriptionFieldType.BidIvEma:
                case SubscriptionFieldType.AskIvEma:
                    _emaCalculatorGenerator.Unsubscribe(key, quoteType, this);
                    break;
                case SubscriptionFieldType.DerivedBid:
                case SubscriptionFieldType.DerivedAsk:
                    _ = OmsCore.DerivedValueGenerator.UnsubscribeAsync(key, quoteType, this);
                    break;
                case SubscriptionFieldType.DeltaAdjTheoDelta:
                case SubscriptionFieldType.DeltaAdjTheoMid:
                case SubscriptionFieldType.DeltaAdjTheoMidTime:
                case SubscriptionFieldType.DeltaAdjBidTheo:
                case SubscriptionFieldType.DeltaAdjAskTheo:
                case SubscriptionFieldType.OrderUpdate:
                case SubscriptionFieldType.OrderInfoUpdate:
                case SubscriptionFieldType.FirmSymbolPosition:
                case SubscriptionFieldType.FirmInstancePosition:
                case SubscriptionFieldType.FirmSpreadPosition:
                case SubscriptionFieldType.FirmUnderlyingPosition:
                case SubscriptionFieldType.UserInstancePosition:
                case SubscriptionFieldType.UserSpreadPosition:
                case SubscriptionFieldType.UserUnderlyingPosition:
                    break;
                default:
                    _ = OmsCore.QuoteClient.UnsubscribeAsync(key, quoteType, this);
                    break;
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey subscriptionKey, object value, bool isFromCache)
        {
            try
            {
                var key = subscriptionKey.Symbol;
                var type = subscriptionKey.Type;
                if (subscriptionKey.Type == SubscriptionFieldType.FullHanweck)
                {
                    if (value is GreekUpdateModel greekUpdateModel)
                    {
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckBidSize, greekUpdateModel.BidSize);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckAskSize, greekUpdateModel.AskSize);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckBidMCID, greekUpdateModel.BidMCID);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckAskMCID, greekUpdateModel.AskMCID);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckInfoBits, greekUpdateModel.InfoBits);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckBidPrice, greekUpdateModel.BidPrice);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckAskPrice, greekUpdateModel.AskPrice);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckTheo, greekUpdateModel.Theo);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckImpliedVolatility, greekUpdateModel.ImpliedVolatility);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckDelta, greekUpdateModel.Delta);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckGamma, greekUpdateModel.Gamma);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckVega, greekUpdateModel.Vega);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckTheta, greekUpdateModel.Theta);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckRho, greekUpdateModel.Rho);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckBidVol, greekUpdateModel.BidVol);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckAskVol, greekUpdateModel.AskVol);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckMidVol, greekUpdateModel.MidVol);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckUBidPrice, greekUpdateModel.UBidPrice);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckUAskPrice, greekUpdateModel.UAskPrice);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckTimeValue, greekUpdateModel.TimeValue);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckIntrinsicValue, greekUpdateModel.IntrinsicValue);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckFvDivs, greekUpdateModel.FvDivs);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckSequenceNumber, greekUpdateModel.SequenceNumber);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckTradeVolume, greekUpdateModel.TradeVolume);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckTimeStamp, greekUpdateModel.TimeStamp);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckCollectorTimestamp, greekUpdateModel.CollectorTimestamp);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckCollectorTimestampNanos, greekUpdateModel.CollectorTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckCalculationTimestampNanos, greekUpdateModel.CalculationTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckBidTimestampNanos, greekUpdateModel.BidTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckAskTimestampNanos, greekUpdateModel.AskTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckUTimestampNanos, greekUpdateModel.UTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckPersistorTimestampNanos, greekUpdateModel.PersistorTimestampNanos);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.HanweckPersistorSeqNum, greekUpdateModel.PersistorSeqNum);
                    }
                }
                else if (subscriptionKey.Type == SubscriptionFieldType.DeltaAdjTheo)
                {
                    if (value is double deltaAdjTheo)
                    {
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.DeltaAdjTheo, deltaAdjTheo);
                    }
                    else if (value is DeltaAdjTheo adjTheo)
                    {
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.DeltaAdjTheo, adjTheo.DeltaAdjustedTheo);              // Should always use V0
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaTheo, adjTheo.SecondaryTheo, adjTheo.ModelId);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaTheoAdj, adjTheo.SecondaryTheoAdj, adjTheo.ModelId);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaVol, adjTheo.SecondaryVol, adjTheo.ModelId);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.DeltaAdjEma, adjTheo.AdjDaEma);                        // Should always use V0
                    }
                }
                else if (subscriptionKey.Type == SubscriptionFieldType.VolaGreeks)
                {
                    if (value is SlimGreekUpdateModel greekUpdateModel)
                    {
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaDelta, greekUpdateModel.Delta, greekUpdateModel.ModelId);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaGamma, greekUpdateModel.Gamma, greekUpdateModel.ModelId);
                        TryUpdateTopicSubscribers(key, SubscriptionFieldType.VolaVega, greekUpdateModel.Vega, greekUpdateModel.ModelId);
                    }
                }
                else if (_subscriptionKeyToTopicMap.TryGetValue(subscriptionKey, out HashSet<Topic> topics))
                {
                    switch (subscriptionKey.Type)
                    {
                        case SubscriptionFieldType.FullEma:
                            if (value is EmaUpdateModel emaUpdateModel)
                            {
                                string update = $"{Math.Round(emaUpdateModel.LowPeriodEma, 2)};{Math.Round(emaUpdateModel.LowPeriodEmaAdj, 2)};{Math.Round(emaUpdateModel.LowPeriodEmaUnderlying, 2)}|" +
                                                $"{Math.Round(emaUpdateModel.MidPeriodEma, 2)};{Math.Round(emaUpdateModel.MidPeriodEmaAdj, 2)};{Math.Round(emaUpdateModel.MidPeriodEmaUnderlying, 2)}|" +
                                                $"{Math.Round(emaUpdateModel.HighPeriodEma, 2)};{Math.Round(emaUpdateModel.HighPeriodEmaAdj, 2)};{Math.Round(emaUpdateModel.HighPeriodEmaUnderlying, 2)}";
                                ProcessUpdate(topics, update);
                            }
                            break;
                        case SubscriptionFieldType.DerivedValues:
                            if (value is DerivedValueUpdateModel updateModel)
                            {
                                StringBuilder updateBuilder = new StringBuilder();

                                if (updateModel.HighestBidLowestAskResult != null)
                                {
                                    // Header "Hi Bid, Lo Ask, Hi Bid Time, Lo Ask Time, Hi Bid Base, Lo Ask Base, Hi Bid Ul Mid, Lo Ask Ul Mid, Skew Adj Hi Bid, Skew Adj Lo Ask, Skew Adj Hi Bid Time, Skew Adj Lo Ask Time, Skew Adj Hi Bid Base, Skew Adj Lo Ask Base, Skew Adj Hi Bid Ul Mid,Skew Adj Lo Ask Ul Mid";
                                    updateBuilder.Append(Math.Round(updateModel.HighestBidLowestAskResult.HighestBid, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.LowestAsk, 2))
                                        .Append(",")
                                        .Append(new DateTime((long)updateModel.HighestBidLowestAskResult.HighestBidTime))
                                        .Append(",")
                                        .Append(new DateTime((long)updateModel.HighestBidLowestAskResult.LowestAskTime))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.HighestBidBase, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.LowestAskBase, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.HighestBidUnderlyingMid, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.LowestAskUnderlyingMid, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedHighestBid, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedLowestAsk, 2))
                                        .Append(",")
                                        .Append(new DateTime((long)updateModel.HighestBidLowestAskResult.SkewAdjustedHighestBidTime))
                                        .Append(",")
                                        .Append(new DateTime((long)updateModel.HighestBidLowestAskResult.SkewAdjustedLowestAskTime))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedHighestBidBase, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedLowestAskBase, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedHighestBidUnderlyingMid, 2))
                                        .Append(",")
                                        .Append(Math.Round(updateModel.HighestBidLowestAskResult.SkewAdjustedLowestAskUnderlyingMid, 2));
                                }

                                ProcessUpdate(topics, updateBuilder.ToString());
                            }
                            break;
                        case SubscriptionFieldType.DerivedBidEma:
                        case SubscriptionFieldType.DerivedAskEma:
                        case SubscriptionFieldType.BidIvEma:
                        case SubscriptionFieldType.AskIvEma:
                        case SubscriptionFieldType.BidEma:
                        case SubscriptionFieldType.AskEma:
                            if (value is OptionPricingModel pricingModel)
                            {
                                foreach (Topic topic in topics)
                                {
                                    var update = pricingModel.Greeks +
                                                 " IV:" + pricingModel.Volatility +
                                                 " Price:" + pricingModel.OptionPrice +
                                                 " EMA:" + pricingModel.OriginalPrice +
                                                 " Underlying:" + pricingModel.UnderlyingPrice;
                                    topic.UpdateValue(update);
                                    ProcessUpdate(topics, update);
                                }
                            }
                            break;
                        case SubscriptionFieldType.DebugValue:
                            if (value is DeltaAdjTheo debugAdjTheo)
                            {
                                var update = debugAdjTheo.DeltaAdjustedTheo;
                                ProcessUpdate(topics, update);
                            }
                            break;
                        case SubscriptionFieldType.DeltaAdjTheoBase:
                            if (value is DeltaAdjTheo adjTheoBase)
                            {
                                string csv = adjTheoBase.ToString();
                                ProcessUpdate(topics, csv);
                            }
                            else
                            {
                                ProcessUpdate(topics, value);
                            }
                            break;
                        default:
                            ProcessUpdate(topics, value);
                            break;
                    }
                }
                else
                {
                    Unsubscribe(key, type);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        private void TryUpdateTopicSubscribers(string symbol, SubscriptionFieldType subscriptionFieldType, object update, byte modelId = 0)
        {
            if (_subscriptionKeyToTopicMap.TryGetValue(new SubscriptionKey(symbol, subscriptionFieldType), out var hanweckBidSizeTopics))
            {
                ProcessUpdate(hanweckBidSizeTopics, update, modelId);
            }
        }

        private static void ProcessUpdate(HashSet<Topic> topics, object update, byte modelId = 0)
        {
            try
            {
                foreach (Topic topic in topics)
                {
                    try
                    {
                        if (_topicToModelIdMap.TryGetValue(topic, out byte topicModelId))
                        {
                            if (modelId == topicModelId)
                            {
                                topic.UpdateValue(update);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, nameof(ProcessUpdate));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessUpdate));
            }
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
        }

        public void OrderUpdated(OrderUpdateValues orderUpdate)
        {
            var update = ConvertUpdate(orderUpdate);
            TryUpdateTopicSubscribers(orderUpdate.LocalOrderId?.ToUpper(), SubscriptionFieldType.OrderUpdate, update);
            TryUpdateTopicSubscribers(orderUpdate.ParentLocalOrderId?.ToUpper(), SubscriptionFieldType.OrderUpdate, update);
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
        }

        public void AutomationStateChanged(bool running)
        {
        }

        private static string ConvertUpdate(OrderUpdateValues orderUpdate)
        {
            return $"{orderUpdate.LocalOrderId}|{orderUpdate.OrderStatus.ToString()}|{orderUpdate.Filled}|{orderUpdate.LeavesQuantity}|{orderUpdate.AveragePrice}|{orderUpdate.LastPrice}|N/A|{orderUpdate.OrderId}|{orderUpdate.OriginalOrderId}|{orderUpdate.Message}|{orderUpdate.Status}";
        }

        private void PositionUpdate(SubscriptionFieldType type, IPosition position)
        {
            TryUpdateTopicSubscribers(position.Name, type, position.NetQty);
        }
    }
}
