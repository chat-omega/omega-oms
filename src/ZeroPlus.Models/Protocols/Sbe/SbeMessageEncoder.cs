using Generated;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.SbeTool.Sbe.Dll;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using ZeroPlus.Models.Data.Auth;
using ZeroPlus.Models.Data.Edge;
using ZeroPlus.Models.Data.EdgeScanner;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Models.Databento;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Data.Subscription;
using ZeroPlus.Models.Data.Subscription.Topics;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Models.Protocols.Sbe.Interfaces;
using static System.Math;
using OrderStatus = Generated.OrderStatus;
using PortfolioType = Generated.PortfolioType;
using PositionEffect = Generated.PositionEffect;
using PositionType = Generated.PositionType;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Models.Protocols.Sbe
{
    public class SbeMessageEncoder : ISbeMessageEncoder
    {
        private readonly ILogger<SbeMessageEncoder> _logger;

        public SbeMessageEncoder(ILogger<SbeMessageEncoder> logger)
        {
            _logger = logger;
        }

        public int EncodeClientAuthentication(DirectBuffer directBuffer, int offset, ref ClientAuthenticationModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ClientAuthentication.BlockLength;
            messageHeader.SchemaId = ClientAuthentication.SchemaId;
            messageHeader.TemplateId = ClientAuthentication.TemplateId;
            messageHeader.Version = ClientAuthentication.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            ClientAuthentication message = new ClientAuthentication();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.UserId = model.UserId;
            message.SetUsername(model.Username ?? "");
            message.SetUserToken(model.UserToken ?? "");
            message.SetAppId(model.AppId ?? "");
            message.SetAppVersion(model.AppVersion.ToString() ?? "");
            message.SetHostname(model.Hostname ?? "");

            return message.Limit - offset;
        }

        public int EncodeClientRegistration(DirectBuffer directBuffer, int offset, ref ClientRegistrationModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ClientRegistration.BlockLength;
            messageHeader.SchemaId = ClientRegistration.SchemaId;
            messageHeader.TemplateId = ClientRegistration.TemplateId;
            messageHeader.Version = ClientRegistration.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            ClientRegistration message = new ClientRegistration();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetUsername(model.Username);
            message.SetAppId(model.AppId);
            message.SetAppVersion(model.AppVersion.ToString() ?? "");
            message.SetHostname(model.Hostname);

            return message.Limit - offset;
        }

        public int EncodeLatencyMeterEvent(DirectBuffer directBuffer, int offset, ref LatencyMeterEventModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.LatencyMeterEventMessage.BlockLength;
            messageHeader.SchemaId = Generated.LatencyMeterEventMessage.SchemaId;
            messageHeader.TemplateId = Generated.LatencyMeterEventMessage.TemplateId;
            messageHeader.Version = Generated.LatencyMeterEventMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            LatencyMeterEventMessage message = new LatencyMeterEventMessage();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.BoxId = model.BoxId;
            message.ProgId = model.ProgId;
            message.InstanceId = model.InstanceId;
            message.EventType = model.EventType;
            message.SetEventId(model.EventId ?? "");
            message.TimingSource = model.TimingSource;
            message.TimestampNanos = model.TimestampNanos;

            return message.Limit - offset;
        }

        public int EncodeStateSnapshot(DirectBuffer directBuffer, int offset, ref StateSnapshotModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.StateSnapshotMessage.BlockLength;
            messageHeader.SchemaId = Generated.StateSnapshotMessage.SchemaId;
            messageHeader.TemplateId = Generated.StateSnapshotMessage.TemplateId;
            messageHeader.Version = Generated.StateSnapshotMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            StateSnapshotMessage message = new StateSnapshotMessage();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.BoxId = model.BoxId;
            message.ProgId = model.ProgId;
            message.InstanceId = model.InstanceId;
            message.SetSnapshotName(model.SnapshotName ?? "");
            message.TimestampNanos = model.TimestampNanos;

            int entryCount = model.Entries?.Length ?? 0;
            StateSnapshotMessage.EntriesGroup entries = message.EntriesCount(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                entries.Next();
                entries.SetKey(model.Entries![i].Key ?? "");
                entries.SetValue(model.Entries[i].Value ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeSubscribeMarketDataRequest(DirectBuffer directBuffer, int offset, ref SubscribeMarketDataModel subscribeMarketData)
        {
            if (subscribeMarketData.Symbol.Length < SubscribeMarketDataRequest.SymbolLength)
            {
                int bufferOffset = offset;

                MessageHeader messageHeader = new MessageHeader();
                messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
                messageHeader.BlockLength = SubscribeMarketDataRequest.BlockLength;
                messageHeader.SchemaId = SubscribeMarketDataRequest.SchemaId;
                messageHeader.TemplateId = SubscribeMarketDataRequest.TemplateId;
                messageHeader.Version = SubscribeMarketDataRequest.SchemaVersion;

                bufferOffset += MessageHeader.Size;

                SubscribeMarketDataRequest message = new SubscribeMarketDataRequest();

                message.WrapForEncode(directBuffer, bufferOffset);
                message.RequestID = subscribeMarketData.RequestId;
                message.MarketDataType = (short)subscribeMarketData.RequestType;
                message.SetSymbol(subscribeMarketData.Symbol ?? "");

                return message.Limit - offset;
            }
            else
            {
                int bufferOffset = offset;

                MessageHeader messageHeader = new MessageHeader();
                messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
                messageHeader.BlockLength = SubscribeSpreadDataRequest.BlockLength;
                messageHeader.SchemaId = SubscribeSpreadDataRequest.SchemaId;
                messageHeader.TemplateId = SubscribeSpreadDataRequest.TemplateId;
                messageHeader.Version = SubscribeSpreadDataRequest.SchemaVersion;

                bufferOffset += MessageHeader.Size;

                SubscribeSpreadDataRequest message = new SubscribeSpreadDataRequest();

                message.WrapForEncode(directBuffer, bufferOffset);
                message.RequestID = subscribeMarketData.RequestId;
                message.MarketDataType = (short)subscribeMarketData.RequestType;
                message.SetSymbol(subscribeMarketData.Symbol ?? "");

                return message.Limit - offset;
            }
        }

        public int EncodeUnsubscribeMarketDataRequest(DirectBuffer directBuffer, int offset, ref UnsubscribeMarketDataModel unsubscribeMarketData)
        {
            if (unsubscribeMarketData.Symbol.Length < SubscribeMarketDataRequest.SymbolLength)
            {
                int bufferOffset = offset;

                MessageHeader messageHeader = new MessageHeader();
                UnsubscribeMarketDataRequest message = new UnsubscribeMarketDataRequest();

                messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
                messageHeader.BlockLength = UnsubscribeMarketDataRequest.BlockLength;
                messageHeader.SchemaId = UnsubscribeMarketDataRequest.SchemaId;
                messageHeader.TemplateId = UnsubscribeMarketDataRequest.TemplateId;
                messageHeader.Version = UnsubscribeMarketDataRequest.SchemaVersion;

                bufferOffset += MessageHeader.Size;

                message.WrapForEncode(directBuffer, bufferOffset);
                message.RequestID = unsubscribeMarketData.RequestId;
                message.MarketDataType = (short)unsubscribeMarketData.RequestType;
                message.SetSymbol(unsubscribeMarketData.Symbol ?? "");

                return message.Limit - offset;
            }
            else
            {
                int bufferOffset = offset;

                MessageHeader messageHeader = new MessageHeader();
                UnsubscribeSpreadDataRequest message = new UnsubscribeSpreadDataRequest();

                messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
                messageHeader.BlockLength = UnsubscribeSpreadDataRequest.BlockLength;
                messageHeader.SchemaId = UnsubscribeSpreadDataRequest.SchemaId;
                messageHeader.TemplateId = UnsubscribeSpreadDataRequest.TemplateId;
                messageHeader.Version = UnsubscribeSpreadDataRequest.SchemaVersion;

                bufferOffset += MessageHeader.Size;

                message.WrapForEncode(directBuffer, bufferOffset);
                message.RequestID = unsubscribeMarketData.RequestId;
                message.MarketDataType = (short)unsubscribeMarketData.RequestType;
                message.SetSymbol(unsubscribeMarketData.Symbol ?? "");

                return message.Limit - offset;
            }
        }

        public int EncodeSubscribeTransactionRequest(DirectBuffer directBuffer, int offset, ref SubscribeTransactionModel subscribeTransaction)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SubscribeTransactionRequest.BlockLength;
            messageHeader.SchemaId = SubscribeTransactionRequest.SchemaId;
            messageHeader.TemplateId = SubscribeTransactionRequest.TemplateId;
            messageHeader.Version = SubscribeTransactionRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SubscribeTransactionRequest message = new SubscribeTransactionRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = subscribeTransaction.RequestId;
            message.RequestTime = ToUnixEpoch(subscribeTransaction.RequestTime);
            message.SequenceNumber = subscribeTransaction.SequenceNumber;
            message.FillsOnly = subscribeTransaction.FillsOnly ? BooleanEnum.True : BooleanEnum.False;
            message.AllOwn = subscribeTransaction.AllOwn ? BooleanEnum.True : BooleanEnum.False;

            SubscribeTransactionRequest.AccountsGroup accounts = message.AccountsCount(subscribeTransaction.Accounts.Count);

            foreach (string account in subscribeTransaction.Accounts)
            {
                accounts.Next();
                accounts.SetAccount(account ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeUnsubscribeTransactionRequest(DirectBuffer directBuffer, int offset, ref UnsubscribeTransactionModel unsubscribeTransaction)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = UnsubscribeTransactionRequest.BlockLength;
            messageHeader.SchemaId = UnsubscribeTransactionRequest.SchemaId;
            messageHeader.TemplateId = UnsubscribeTransactionRequest.TemplateId;
            messageHeader.Version = UnsubscribeTransactionRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            UnsubscribeTransactionRequest message = new UnsubscribeTransactionRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = unsubscribeTransaction.RequestId;
            message.RequestTime = ToUnixEpoch(unsubscribeTransaction.RequestTime);

            return message.Limit - offset;
        }

        public int EncodeSubscribePnlRequest(DirectBuffer directBuffer, int offset, ref SubscribePnlModel subscribePnl)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SubscribePnlRequest.BlockLength;
            messageHeader.SchemaId = SubscribePnlRequest.SchemaId;
            messageHeader.TemplateId = SubscribePnlRequest.TemplateId;
            messageHeader.Version = SubscribePnlRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SubscribePnlRequest message = new SubscribePnlRequest();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = subscribePnl.RequestId;
            message.RequestTime = ToUnixEpoch(subscribePnl.RequestTime);
            message.PositionSubscription = (byte)subscribePnl.PositionSubscription;

            return message.Limit - offset;
        }

        public int EncodeUnsubscribePnlRequest(DirectBuffer directBuffer, int offset, ref UnsubscribePnlModel unsubscribePnl)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            UnsubscribePnlRequest message = new UnsubscribePnlRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = UnsubscribePnlRequest.BlockLength;
            messageHeader.SchemaId = UnsubscribePnlRequest.SchemaId;
            messageHeader.TemplateId = UnsubscribePnlRequest.TemplateId;
            messageHeader.Version = UnsubscribePnlRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = unsubscribePnl.RequestId;
            message.RequestTime = ToUnixEpoch(unsubscribePnl.RequestTime);

            return message.Limit - offset;
        }

        public int EncodeOrderAdded(DirectBuffer directBuffer, int offset, IOrder order)
        {

            if (order.IsComplexOrder)
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                foreach (IComplexOrderLeg leg in complexOrder.Legs)
                {
                }
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderAdded message = new OrderAdded();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderAdded.BlockLength;
            messageHeader.SchemaId = OrderAdded.SchemaId;
            messageHeader.TemplateId = OrderAdded.TemplateId;
            messageHeader.Version = OrderAdded.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermID(order.PermID);
            message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;

            message.PartiallyFilled = order.PartiallyFilled ? BooleanEnum.True : BooleanEnum.False;
            message.IsFirstFill = order.IsFirstFill ? BooleanEnum.True : BooleanEnum.False;

            message.LastQuantity = order.LastQuantity;
            message.FilledQty = order.FilledQty;
            message.LeavesQuantity = order.LeavesQuantity;
            message.CumulativeQuantity = order.CumulativeQuantity;
            message.Quantity = order.Quantity;

            message.SpreadAvgPrice.Mantissa = EncodeMantissa(order.SpreadAvgPrice, message.SpreadAvgPrice.Exponent);
            message.AveragePrice.Mantissa = EncodeMantissa(order.AveragePrice, message.AveragePrice.Exponent);
            message.Price.Mantissa = EncodeMantissa(order.Price, message.Price.Exponent);
            message.LastPrice.Mantissa = EncodeMantissa(order.LastPrice, message.LastPrice.Exponent);
            message.MinPrice.Mantissa = EncodeMantissa(order.MinPrice, message.MinPrice.Exponent);
            message.MaxPrice.Mantissa = EncodeMantissa(order.MaxPrice, message.MaxPrice.Exponent);
            message.TagEdge.Mantissa = EncodeMantissa(order.TagEdge, message.TagEdge.Exponent);
            message.TagMid.Mantissa = EncodeMantissa(order.TagMid, message.TagMid.Exponent);
            message.TagBid.Mantissa = EncodeMantissa(order.TagBid, message.TagBid.Exponent);
            message.TagAsk.Mantissa = EncodeMantissa(order.TagAsk, message.TagAsk.Exponent);
            message.TagTheo.Mantissa = EncodeMantissa(order.TagTheo, message.TagTheo.Exponent);
            message.TagVolaV0.Mantissa = EncodeMantissa(order.TagVolaV0, message.TagVolaV0.Exponent);
            message.TagVolaV1.Mantissa = EncodeMantissa(order.TagVolaV1, message.TagVolaV1.Exponent);
            message.TagVolaV2.Mantissa = EncodeMantissa(order.TagVolaV2, message.TagVolaV2.Exponent);
            message.TagEma.Mantissa = EncodeMantissa(order.TagEma, message.TagEma.Exponent);
            message.TagVolaIv = order.VolaIv;
            message.TheoBid.Mantissa = EncodeMantissa(order.TheoBid, message.TheoBid.Exponent);
            message.TheoAsk.Mantissa = EncodeMantissa(order.TheoAsk, message.TheoAsk.Exponent);
            message.Fee1.Mantissa = EncodeMantissa(order.Fee1, message.Fee1.Exponent);
            message.Fee2.Mantissa = EncodeMantissa(order.Fee2, message.Fee2.Exponent);
            message.Bid.Mantissa = EncodeMantissa(order.Bid, message.Bid.Exponent);
            message.Ask.Mantissa = EncodeMantissa(order.Ask, message.Ask.Exponent);
            message.UnderBid.Mantissa = EncodeMantissa(order.UnderBid, message.UnderBid.Exponent);
            message.UnderAsk.Mantissa = EncodeMantissa(order.UnderAsk, message.UnderAsk.Exponent);
            message.TV.Mantissa = EncodeMantissa(order.TV, message.TV.Exponent);
            message.Delta.Mantissa = EncodeMantissa(order.Delta, message.Delta.Exponent);
            message.ExchangeFee1.Mantissa = EncodeMantissa(order.ExchangeFee1, message.ExchangeFee1.Exponent);
            message.ExchangeFee2.Mantissa = EncodeMantissa(order.ExchangeFee2, message.ExchangeFee2.Exponent);
            message.BrokerFee1.Mantissa = EncodeMantissa(order.BrokerFee1, message.BrokerFee1.Exponent);
            message.BrokerFee2.Mantissa = EncodeMantissa(order.BrokerFee2, message.BrokerFee2.Exponent);
            message.TotalContracts.Mantissa = EncodeMantissa(order.TotalContracts, message.TotalContracts.Exponent);
            message.FillTime.Mantissa = EncodeMantissa(order.FillTime, message.FillTime.Exponent);
            message.TradeToNewTime.Mantissa = EncodeMantissa(order.TradeToNewTime, message.TradeToNewTime.Exponent);
            message.SubmitToNewTime.Mantissa = EncodeMantissa(order.SubmitToNewTime, message.SubmitToNewTime.Exponent);
            message.NewToCancelTime.Mantissa = EncodeMantissa(order.NewToCancelTime, message.NewToCancelTime.Exponent);
            message.BidPercentOfFillPrice.Mantissa = EncodeMantissa(order.BidPercentOfFillPrice, message.BidPercentOfFillPrice.Exponent);
            message.OmsBidPercentOfFillPrice.Mantissa = EncodeMantissa(order.OmsBidPercentOfFillPrice, message.OmsBidPercentOfFillPrice.Exponent);
            message.TotalDelta.Mantissa = EncodeMantissa(order.TotalDelta, message.TotalDelta.Exponent);
            message.HanweckTotalTheo.Mantissa = EncodeMantissa(order.HanweckTotalTheo, message.HanweckTotalTheo.Exponent);
            message.HanweckTotalGamma.Mantissa = EncodeMantissa(order.HanweckTotalGamma, message.HanweckTotalGamma.Exponent);
            message.HanweckTotalVega.Mantissa = EncodeMantissa(order.HanweckTotalVega, message.HanweckTotalVega.Exponent);
            message.HanweckTotalTheta.Mantissa = EncodeMantissa(order.HanweckTotalTheta, message.HanweckTotalTheta.Exponent);
            message.HanweckTotalRho.Mantissa = EncodeMantissa(order.HanweckTotalRho, message.HanweckTotalRho.Exponent);
            message.HanweckTotalIV.Mantissa = EncodeMantissa(order.HanweckTotalIV, message.HanweckTotalIV.Exponent);
            message.HanweckTotalUnder.Mantissa = EncodeMantissa(order.HanweckTotalUnder, message.HanweckTotalUnder.Exponent);
            message.HanweckTotalUBid.Mantissa = EncodeMantissa(order.HanweckTotalUBid, message.HanweckTotalUBid.Exponent);
            message.HanweckTotalUAsk.Mantissa = EncodeMantissa(order.HanweckTotalUAsk, message.HanweckTotalUAsk.Exponent);
            message.HanweckTotalBid.Mantissa = EncodeMantissa(order.HanweckTotalBid, message.HanweckTotalBid.Exponent);
            message.HanweckTotalAsk.Mantissa = EncodeMantissa(order.HanweckTotalAsk, message.HanweckTotalAsk.Exponent);
            message.EdgeOverride.Mantissa = EncodeMantissa(order.EdgeOverride, message.EdgeOverride.Exponent);
            message.AdjustedEdgeOverride.Mantissa = EncodeMantissa(order.AdjustedEdgeOverride, message.AdjustedEdgeOverride.Exponent);
            message.EdgeToTheo.Mantissa = EncodeMantissa(order.EdgeToTheo, message.EdgeToTheo.Exponent);
            message.TagEdgeToTheo.Mantissa = EncodeMantissa(order.TagEdgeToTheo, message.TagEdgeToTheo.Exponent);
            message.TagEdgeToEma.Mantissa = EncodeMantissa(order.TagEdgeToEma, message.TagEdgeToEma.Exponent);
            message.TagEdgeToVolaV0.Mantissa = EncodeMantissa(order.TagEdgeToVolaV0, message.TagEdgeToVolaV0.Exponent);
            message.TagEdgeToVolaV1.Mantissa = EncodeMantissa(order.TagEdgeToVolaV1, message.TagEdgeToVolaV1.Exponent);
            message.TagEdgeToVolaV2.Mantissa = EncodeMantissa(order.TagEdgeToVolaV2, message.TagEdgeToVolaV2.Exponent);
            message.TagBestBid.Mantissa = EncodeMantissa(order.TagBestBid, message.TagBestBid.Exponent);
            message.TagBestAsk.Mantissa = EncodeMantissa(order.TagBestAsk, message.TagBestAsk.Exponent);
            message.TagMktMkrBid.Mantissa = EncodeMantissa(order.TagMktMkrBid, message.TagMktMkrBid.Exponent);
            message.TagMktMkrAsk.Mantissa = EncodeMantissa(order.TagMktMkrAsk, message.TagMktMkrAsk.Exponent);
            message.InitialEdge.Mantissa = EncodeMantissa(order.InitialEdge, message.InitialEdge.Exponent);
            message.OpenEdge.Mantissa = EncodeMantissa(order.OpenEdge, message.OpenEdge.Exponent);
            message.CloseEdge.Mantissa = EncodeMantissa(order.CloseEdge, message.CloseEdge.Exponent);

            message.LastEdge.Mantissa = EncodeMantissa(order.LastEdge, message.LastEdge.Exponent);
            message.DeltaAdjLastEdge.Mantissa = EncodeMantissa(order.DeltaAdjLastEdge, message.DeltaAdjLastEdge.Exponent);
            message.DeltaAdjLastEdgeNotional.Mantissa = EncodeMantissa(order.DeltaAdjLastEdgeNotional, message.DeltaAdjLastEdgeNotional.Exponent);
            message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent);

            message.DeltaAdjChange.Mantissa = EncodeMantissa(order.DeltaAdjChange, message.DeltaAdjChange.Exponent);
            message.DeltaAdjChangeNotional.Mantissa = EncodeMantissa(order.DeltaAdjChangeNotional, message.DeltaAdjChangeNotional.Exponent);

            message.EdgeScanFeedEdge.Mantissa = EncodeMantissa(order.EdgeScanFeedEdge, message.EdgeScanFeedEdge.Exponent);
            message.EdgeScanFeedTimespan.Mantissa = EncodeMantissa(order.EdgeScanFeedTimespan, message.EdgeScanFeedTimespan.Exponent);

            message.EdgeScanFeedBuyPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedBuyPrice, message.EdgeScanFeedBuyPrice.Exponent);
            message.EdgeScanFeedBuyQty = order.EdgeScanFeedBuyQty;
            message.EdgeScanFeedSellPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedSellPrice, message.EdgeScanFeedSellPrice.Exponent);
            message.EdgeScanFeedSellQty = order.EdgeScanFeedSellQty;
            message.EdgeScanFeedBuyTime = order.EdgeScanFeedBuyTime.ToUnixEpoch();
            message.EdgeScanFeedSellTime = order.EdgeScanFeedSellTime.ToUnixEpoch();
            message.EdgeScanFeedRespondLatency.Mantissa = EncodeMantissa(order.EdgeScanFeedRespondLatency, message.EdgeScanFeedRespondLatency.Exponent);

            message.EdgeScanFeedConditionCode = (byte)order.EdgeScanFeedConditionCode;

            message.ResubmitCount = order.ResubmitCount;
            message.TotalEstimatedResubmit = order.TotalEstimatedResubmit;

            var side = order.Side;
            message.AggressorSide = side == Side.Buy ? AggressorSide.Buy : AggressorSide.Sell;
            message.OrderStatus = (OrderStatus)order.OrderStatus;
            message.BaseStrategy = (Generated.BaseStrategy)order.BaseStrategy;
            message.PositionEffect = (PositionEffect)order.PositionEffect;
            message.TimeInForce = (Generated.TimeInForce)order.TimeInForce;

            message.OrderSource = (Generated.OrderSource)order.OrderSource;

            message.SetUsername(order.Username == null ? "" : order.Username.Length > 10 ? order.Username[..10] : order.Username);
            message.SetUnderlyingSymbol(order.UnderlyingSymbol);

            message.SubmitTime = ToUnixEpoch(order.SubmitTime);
            message.LastUpdateTime = ToUnixEpoch(order.LastUpdateTime);
            message.Timestamp = ToUnixEpoch(order.Timestamp);
            message.NewStatusTimeStamp = ToUnixEpoch(order.NewStatusTimeStamp);

            message.DeltaAdjustedTheo.Mantissa = EncodeMantissa(order.DeltaAdjustedTheo, message.DeltaAdjustedTheo.Exponent);
            message.BidSize = order.BidSize;
            message.AskSize = order.AskSize;
            message.UnderlyingBidSize = order.UnderlyingBidSize;
            message.UnderlyingAskSize = order.UnderlyingAskSize;

            message.EdgeType = (int)order.EdgeType;
            message.Edge.Mantissa = EncodeMantissa(order.Edge, message.Edge.Exponent);

            message.IsDeltaAdjusted = order.IsDeltaAdjusted ? BooleanEnum.True : BooleanEnum.False;
            message.LoopInitLatency.Mantissa = EncodeMantissa(order.LoopInitLatency, message.LoopInitLatency.Exponent);
            message.TagUnderBid.Mantissa = EncodeMantissa(order.TagUnderBid, message.TagUnderBid.Exponent);
            message.TagUnderAsk.Mantissa = EncodeMantissa(order.TagUnderAsk, message.TagUnderAsk.Exponent);
            message.DigBid.Mantissa = EncodeMantissa(order.DigBid, message.DigBid.Exponent);
            message.DigAsk.Mantissa = EncodeMantissa(order.DigAsk, message.DigAsk.Exponent);
            message.WeightedVega.Mantissa = EncodeMantissa(order.WeightedVega, message.WeightedVega.Exponent);
            message.DigBidSize = order.DigBidSize;
            message.DigAskSize = order.DigAskSize;
            message.IsTagged = order.IsTagged ? BooleanEnum.True : BooleanEnum.False;

            var hardSide = order.HardSide;
            message.HardSide = hardSide.HasValue ? (Generated.Side)hardSide : Generated.Side.NULL_VALUE;
            message.HardSideDesignationTime = order.HardSideDesignationTime.ToUnixEpoch();
            message.HardSideBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideBuyGiveUp, message.HardSideBuyGiveUp.Exponent);
            message.HardSideSellGiveUp.Mantissa = EncodeMantissa(order.HardSideSellGiveUp, message.HardSideSellGiveUp.Exponent);
            var hardSideAtTrade = order.HardSideAtTrade;
            message.HardSideAtTrade = hardSideAtTrade.HasValue ? (Generated.Side)hardSideAtTrade : Generated.Side.NULL_VALUE;
            message.HardSideAtTradeDesignationTime = order.HardSideAtTradeDesignationTime.ToUnixEpoch();
            message.HardSideAtTradeBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeBuyGiveUp, message.HardSideAtTradeBuyGiveUp.Exponent);
            message.HardSideAtTradeSellGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeSellGiveUp, message.HardSideAtTradeSellGiveUp.Exponent);

            message.EdgeGiveUp.Mantissa = EncodeMantissa(order.EdgeGiveUp, message.EdgeGiveUp.Exponent);
            message.CloseSubs.Mantissa = EncodeMantissa(order.CloseSubs, message.CloseSubs.Exponent);
            message.OrderEdgeToTheo.Mantissa = EncodeMantissa(order.OrderEdgeToTheo, message.OrderEdgeToTheo.Exponent);

            message.TimeValue.Mantissa = EncodeMantissa(order.TimeValue, message.TimeValue.Exponent);
            message.IntrinsicValue.Mantissa = EncodeMantissa(order.IntrinsicValue, message.IntrinsicValue.Exponent);
            message.FVDivs.Mantissa = EncodeMantissa(order.FVDivs, message.FVDivs.Exponent);
            message.UFwd.Mantissa = EncodeMantissa(order.UFwd, message.UFwd.Exponent);
            message.UFwdFactor.Mantissa = EncodeMantissa(order.UFwdFactor, message.UFwdFactor.Exponent);
            message.BorrowCost.Mantissa = EncodeMantissa(order.BorrowCost, message.BorrowCost.Exponent);
            message.BorrowRate.Mantissa = EncodeMantissa(order.BorrowRate, message.BorrowRate.Exponent);
            message.UPrice.Mantissa = EncodeMantissa(order.UPrice, message.UPrice.Exponent);
            message.UTheo.Mantissa = EncodeMantissa(order.UTheo, message.UTheo.Exponent);

            message.SharedId = order.SharedId;
            message.Sequence = order.Sequence;
            message.TypeId = (ushort)order.TypeId;
            message.SubTypeCode = (ushort)order.SubTypeId;
            message.SubTypeSequence = order.SubTypeSequence;
            var venue = order.Venue;
            message.Venue = venue.HasValue ? (byte)venue : OrderAdded.VenueNullValue;
            message.CostOfHedging.Mantissa = EncodeMantissa(order.CostOfHedging, message.CostOfHedging.Exponent);
            var subType = order.SubType;
            message.SubType = subType.HasValue ? (byte)subType : OrderAdded.SubTypeNullValue;

            if (!order.IsComplexOrder)
            {
                message.NoLegsCount(0);
            }
            else
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                OrderAdded.NoLegsGroup legMessage = message.NoLegsCount(complexOrder.Legs.Count);
                for (int i = 0; i < complexOrder.Legs.Count; i++)
                {
                    IComplexOrderLeg leg = complexOrder.Legs.ElementAt(i);
                    legMessage.Next();

                    legMessage.SetLegID(leg.LegID);

                    legMessage.Ratio = leg.Ratio;
                    legMessage.Quantity = leg.Quantity;
                    legMessage.LastQuantity = leg.LastQuantity;
                    legMessage.LeavesQuantity = leg.LeavesQuantity;
                    legMessage.CumulativeQuantity = leg.CumulativeQuantity;

                    legMessage.ExchangeFee2.Mantissa = EncodeMantissa(leg.ExchangeFee2, legMessage.ExchangeFee2.Exponent);
                    legMessage.ExchangeFee1.Mantissa = EncodeMantissa(leg.ExchangeFee1, legMessage.ExchangeFee1.Exponent);
                    legMessage.Fee2.Mantissa = EncodeMantissa(leg.Fee2, legMessage.Fee2.Exponent);
                    legMessage.Fee1.Mantissa = EncodeMantissa(leg.Fee1, legMessage.Fee1.Exponent);
                    legMessage.Delta.Mantissa = EncodeMantissa(leg.Delta, legMessage.Delta.Exponent);
                    legMessage.TV.Mantissa = EncodeMantissa(leg.TV, legMessage.TV.Exponent);
                    legMessage.Ask.Mantissa = EncodeMantissa(leg.Ask, legMessage.Ask.Exponent);
                    legMessage.Bid.Mantissa = EncodeMantissa(leg.Bid, legMessage.Bid.Exponent);
                    legMessage.AveragePrice.Mantissa = EncodeMantissa(leg.AveragePrice, legMessage.AveragePrice.Exponent);
                    legMessage.LastPrice.Mantissa = EncodeMantissa(leg.LastPrice, legMessage.LastPrice.Exponent);
                    legMessage.BrokerFee1.Mantissa = EncodeMantissa(leg.BrokerFee1, legMessage.BrokerFee1.Exponent);
                    legMessage.BrokerFee2.Mantissa = EncodeMantissa(leg.BrokerFee2, legMessage.BrokerFee2.Exponent);
                    legMessage.HanweckTV.Mantissa = EncodeMantissa(leg.HanweckTV, legMessage.HanweckTV.Exponent);
                    legMessage.HanweckGamma.Mantissa = EncodeMantissa(leg.HanweckGamma, legMessage.HanweckGamma.Exponent);
                    legMessage.HanweckVega.Mantissa = EncodeMantissa(leg.HanweckVega, legMessage.HanweckVega.Exponent);
                    legMessage.HanweckTheta.Mantissa = EncodeMantissa(leg.HanweckTheta, legMessage.HanweckTheta.Exponent);
                    legMessage.HanweckRho.Mantissa = EncodeMantissa(leg.HanweckRho, legMessage.HanweckRho.Exponent);
                    legMessage.HanweckIV.Mantissa = EncodeMantissa(leg.HanweckIV, legMessage.HanweckIV.Exponent);
                    legMessage.HanweckUnder.Mantissa = EncodeMantissa(leg.HanweckUnder, legMessage.HanweckUnder.Exponent);
                    legMessage.HanweckUnderBid.Mantissa = EncodeMantissa(leg.HanweckUnderBid, legMessage.HanweckUnderBid.Exponent);
                    legMessage.HanweckUnderAsk.Mantissa = EncodeMantissa(leg.HanweckUnderAsk, legMessage.HanweckUnderAsk.Exponent);
                    legMessage.HanweckBid.Mantissa = EncodeMantissa(leg.HanweckBid, legMessage.HanweckBid.Exponent);
                    legMessage.HanweckAsk.Mantissa = EncodeMantissa(leg.HanweckAsk, legMessage.HanweckAsk.Exponent);

                    legMessage.DeltaAdjustedTheo.Mantissa = EncodeMantissa(leg.DeltaAdjustedTheo, legMessage.DeltaAdjustedTheo.Exponent);
                    legMessage.BidSize = leg.BidSize;
                    legMessage.AskSize = leg.AskSize;

                    legMessage.PositionEffect = (PositionEffect)leg.PositionEffect;
                    legMessage.LegSide = leg.Side == Side.Buy ? LegSide.BuySide : LegSide.SellSide;
                    legMessage.OrderStatus = (OrderStatus)leg.OrderStatus;

                    legMessage.Timestamp = ToUnixEpoch(leg.Timestamp);
                    legMessage.LastUpdateTime = ToUnixEpoch(leg.LastUpdateTime);
                    legMessage.HanweckBidTime = ToUnixEpoch(leg.HanweckBidTime);
                    legMessage.HanweckAskTime = ToUnixEpoch(leg.HanweckAskTime);
                    legMessage.HanweckTimestamp = ToUnixEpoch(leg.HanweckTimestamp);

                    legMessage.TimeValue.Mantissa = EncodeMantissa(leg.TimeValue, legMessage.TimeValue.Exponent);
                    legMessage.IntrinsicValue.Mantissa = EncodeMantissa(leg.IntrinsicValue, legMessage.IntrinsicValue.Exponent);
                    legMessage.FVDivs.Mantissa = EncodeMantissa(leg.FVDivs, legMessage.FVDivs.Exponent);
                    legMessage.UFwd.Mantissa = EncodeMantissa(leg.UFwd, legMessage.UFwd.Exponent);
                    legMessage.UFwdFactor.Mantissa = EncodeMantissa(leg.UFwdFactor, legMessage.UFwdFactor.Exponent);
                    legMessage.BorrowCost.Mantissa = EncodeMantissa(leg.BorrowCost, legMessage.BorrowCost.Exponent);
                    legMessage.BorrowRate.Mantissa = EncodeMantissa(leg.BorrowRate, legMessage.BorrowRate.Exponent);
                    legMessage.UPrice.Mantissa = EncodeMantissa(leg.UPrice, legMessage.UPrice.Exponent);
                    legMessage.UTheo.Mantissa = EncodeMantissa(leg.UTheo, legMessage.UTheo.Exponent);

                    EncodeLegContraFields_OrderAdded(legMessage, leg);

                    legMessage.SetPermID((leg.PermID) ?? "");
                    legMessage.SetOrderID((leg.OrderID) ?? "");
                    legMessage.SetSymbol((leg.Symbol) ?? "");
                }
            }

            EncodeOrderContraFields_OrderAdded(message, order);

            message.SetLastExchange((order.LastExchange) ?? "");
            message.SetExchanges((order.Exchanges) ?? "");
            message.SetReason((order.Reason) ?? "");
            message.SetSource((order.Source) ?? "");
            message.SetAccountAcronym((order.AccountAcronym) ?? "");
            message.SetTag((order.Tag) ?? "");
            message.SetTrader((order.Trader) ?? "");
            message.SetOrderType((order.Type) ?? "");
            message.SetOrderID((order.OrderID) ?? "");
            message.SetRoute((order.Route) ?? "");
            message.SetSymbol((order.Symbol) ?? "");
            message.SetDescription((order.Description) ?? "");
            message.SetSpreadId((order.SpreadId) ?? "");
            message.SetFullTag((order.Tag) ?? "");
            message.SetComment((order.Comment) ?? "");
            message.SetAutomationType((order.AutomationType) ?? "");
            message.SetSpreadHash((order.SpreadHash) ?? "");
            message.SetTagger(order.Tagger ?? "");
            message.SetTaggedMessage(order.TaggedMessage ?? "");

            return message.Limit - offset;
        }

        private static void EncodeLegContraFields_OrderAdded(OrderAdded.NoLegsGroup legMessage, IComplexOrderLeg leg)
        {
            var legCaps = leg.ContraCapacities;
            var legCapsGroup = legMessage.NoLegContraCapacitiesCount(legCaps?.Count ?? 0);
            if (legCaps != null)
            {
                for (int j = 0; j < legCaps.Count; j++)
                {
                    legCapsGroup.Next().Value = (byte)legCaps[j];
                }
            }

            var legBrokers = leg.ContraBrokerNames;
            var legBrokersGroup = legMessage.NoLegContraBrokerNamesCount(legBrokers?.Count ?? 0);
            if (legBrokers != null)
            {
                for (int j = 0; j < legBrokers.Count; j++)
                {
                    legBrokersGroup.Next().Value = (byte)legBrokers[j];
                }
            }

            var legCmtas = leg.ContraCmtas;
            var legCmtasGroup = legMessage.NoLegContraCmtasCount(legCmtas?.Count ?? 0);
            if (legCmtas != null)
            {
                for (int j = 0; j < legCmtas.Count; j++)
                {
                    legCmtasGroup.Next().Value = (byte)legCmtas[j];
                }
            }

            var legTraders = leg.ContraTraders;
            var legTradersGroup = legMessage.NoLegContraTradersCount(legTraders?.Count ?? 0);
            if (legTraders != null)
            {
                for (int j = 0; j < legTraders.Count; j++)
                {
                    legTradersGroup.Next().Value = (byte)legTraders[j];
                }
            }
        }

        private static void EncodeOrderContraFields_OrderAdded(OrderAdded message, IOrder order)
        {
            var orderCaps = order.ContraCapacities;
            var orderCapsGroup = message.NoContraCapacitiesCount(orderCaps?.Count ?? 0);
            if (orderCaps != null)
            {
                for (int i = 0; i < orderCaps.Count; i++)
                {
                    orderCapsGroup.Next().Value = (byte)orderCaps[i];
                }
            }

            var orderBrokers = order.ContraBrokerNames;
            var orderBrokersGroup = message.NoContraBrokerNamesCount(orderBrokers?.Count ?? 0);
            if (orderBrokers != null)
            {
                for (int i = 0; i < orderBrokers.Count; i++)
                {
                    orderBrokersGroup.Next().Value = (byte)orderBrokers[i];
                }
            }

            var orderCmtas = order.ContraCmtas;
            var orderCmtasGroup = message.NoContraCmtasCount(orderCmtas?.Count ?? 0);
            if (orderCmtas != null)
            {
                for (int i = 0; i < orderCmtas.Count; i++)
                {
                    orderCmtasGroup.Next().Value = (byte)orderCmtas[i];
                }
            }

            var orderTraders = order.ContraTraders;
            var orderTradersGroup = message.NoContraTradersCount(orderTraders?.Count ?? 0);
            if (orderTraders != null)
            {
                for (int i = 0; i < orderTraders.Count; i++)
                {
                    orderTradersGroup.Next().Value = (byte)orderTraders[i];
                }
            }
        }

        public int EncodeSendOrder(DirectBuffer directBuffer, int offset, IOrderSlim order)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SendOrder message = new SendOrder();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SendOrder.BlockLength;
            messageHeader.SchemaId = SendOrder.SchemaId;
            messageHeader.TemplateId = SendOrder.TemplateId;
            messageHeader.Version = SendOrder.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetLocalID(order.LocalID);
            message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;
            message.Quantity = order.Quantity;

            message.Price = order.Price;
            message.AveragePrice = order.AveragePrice;
            message.Bid = order.Bid;
            message.Ask = order.Ask;
            message.UnderBid = order.UnderBid;
            message.UnderAsk = order.UnderAsk;
            message.NewToCancelTime = order.NewToCancelTime;
            message.TotalDelta = order.TotalDelta;
            message.HanweckTotalTheo = order.HanweckTotalTheo;
            message.EdgeOverride = order.EdgeOverride;
            message.AdjustedEdgeOverride = order.AdjustedEdgeOverride;
            var side = order.Side;
            message.Side = side != null ? (Generated.Side)side : Generated.Side.NULL_VALUE;

            message.BaseStrategy = (Generated.BaseStrategy)order.BaseStrategy;
            message.PositionEffect = (PositionEffect)order.PositionEffect;
            message.TimeInForce = (Generated.TimeInForce)order.TimeInForce;
            message.SetUnderlyingSymbol(order.UnderlyingSymbol);
            message.SetCurrency(order.Currency ?? "");
            message.CloseUnderBid = order.CloseUnderBid;
            message.CloseUnderAsk = order.CloseUnderAsk;
            message.DeltaAdjustedTheo = order.DeltaAdjustedTheo;
            message.MinimumTickStyle = (Generated.MinimumTickStyle)order.MinimumTickStyle;
            message.Multiplier = order.Multiplier;
            message.TagEdge = order.TagEdge;
            message.SkipNewPriceEvaluation = order.SkipNewPriceEvaluation ? BooleanEnum.True : BooleanEnum.False;
            message.IsGTH = order.IsGTH ? BooleanEnum.True : BooleanEnum.False;

            var venue = order.Venue;
            message.Venue = venue.HasValue ? (byte)venue : SendOrder.VenueNullValue;
            var subType = order.SubType;
            message.SubType = subType.HasValue ? (byte)subType : SendOrder.SubTypeNullValue;

            message.DestinationSequence = order.DestinationSequence;

            message.UserId = order.UserId;
            message.RiskCheckId = order.RiskCheckId;

            message.IoiId = order.IoiId;

            message.SharedId = order.SharedId;
            message.Sequence = order.Sequence;
            message.TypeId = (ushort)order.TypeId;
            message.SubTypeCode = (ushort)order.SubTypeId;
            message.SubTypeSequence = order.SubTypeSequence;
            message.OrderSource = (byte)order.OrderSource;
            message.SubmitTime = order.SubmitTime.ToUnixEpoch();

            message.VolaTheo = order.VolaTheo;
            message.VolaTheoAdj = order.VolaTheoAdj;
            message.VolaIv = order.VolaIv;
            message.TheoBid = order.TheoBid;
            message.TheoAsk = order.TheoAsk;

            message.EdgeType = (byte)order.EdgeType;

            message.DigBid = order.DigBid;
            message.DigAsk = order.DigAsk;
            message.DigBidSize = order.DigBidSize;
            message.DigAskSize = order.DigAskSize;
            message.WeightedVega = order.WeightedVega;
            message.CloseEdgeOverride = order.CloseEdgeOverride;

            if (!order.IsComplexOrder)
            {
                message.NoLegsCount(0);
            }
            else
            {
                IComplexOrderSlim complexOrder = (IComplexOrderSlim)order;
                SendOrder.NoLegsGroup legMessage = message.NoLegsCount(complexOrder.Legs.Count);
                for (int i = 0; i < complexOrder.Legs.Count; i++)
                {
                    IComplexOrderLeg leg = complexOrder.Legs.ElementAt(i);
                    legMessage.Next();

                    legMessage.SetLegID(leg.LegID);

                    legMessage.Ratio = leg.Ratio;
                    legMessage.Quantity = leg.Quantity;
                    legMessage.LastQuantity = leg.LastQuantity;
                    legMessage.TransactionID = leg.TransactionID;
                    legMessage.LeavesQuantity = leg.LeavesQuantity;
                    legMessage.CumulativeQuantity = leg.CumulativeQuantity;

                    legMessage.ExchangeFee2 = leg.ExchangeFee2;
                    legMessage.ExchangeFee1 = leg.ExchangeFee1;
                    legMessage.Fee2 = leg.Fee2;
                    legMessage.Fee1 = leg.Fee1;
                    legMessage.Delta = leg.Delta;
                    legMessage.TV = leg.TV;
                    legMessage.Ask = leg.Ask;
                    legMessage.Bid = leg.Bid;
                    legMessage.AveragePrice = leg.AveragePrice;
                    legMessage.LastPrice = leg.LastPrice;
                    legMessage.BrokerFee1 = leg.BrokerFee1;
                    legMessage.BrokerFee2 = leg.BrokerFee2;
                    legMessage.HanweckTV = leg.HanweckTV;
                    legMessage.HanweckGamma = leg.HanweckGamma;
                    legMessage.HanweckVega = leg.HanweckVega;
                    legMessage.HanweckTheta = leg.HanweckTheta;
                    legMessage.HanweckRho = leg.HanweckRho;
                    legMessage.HanweckIV = leg.HanweckIV;
                    legMessage.HanweckUnder = leg.HanweckUnder;
                    legMessage.HanweckUnderBid = leg.HanweckUnderBid;
                    legMessage.HanweckUnderAsk = leg.HanweckUnderAsk;
                    legMessage.HanweckBid = leg.HanweckBid;
                    legMessage.HanweckAsk = leg.HanweckAsk;

                    legMessage.DeltaAdjustedTheo = leg.DeltaAdjustedTheo;
                    legMessage.BidSize = leg.BidSize;
                    legMessage.AskSize = leg.AskSize;

                    legMessage.PositionEffect = (PositionEffect)leg.PositionEffect;
                    legMessage.LegSide = leg.Side == Side.Buy ? LegSide.BuySide : LegSide.SellSide;
                    legMessage.OrderStatus = (OrderStatus)leg.OrderStatus;

                    legMessage.Timestamp = ToUnixEpoch(leg.Timestamp);
                    legMessage.LastUpdateTime = ToUnixEpoch(leg.LastUpdateTime);
                    legMessage.HanweckBidTime = ToUnixEpoch(leg.HanweckBidTime);
                    legMessage.HanweckAskTime = ToUnixEpoch(leg.HanweckAskTime);
                    legMessage.HanweckTimestamp = ToUnixEpoch(leg.HanweckTimestamp);

                    legMessage.SetExecutionID((leg.ExecutionID) ?? "");
                    legMessage.SetPermID((leg.PermID) ?? "");
                    legMessage.SetOrderID((leg.OrderID) ?? "");
                    legMessage.SetSymbol((leg.Symbol) ?? "");
                }
            }

            OrderTagModel? orderTag = order.OrderTag;
            EdgeScanFeedOrderTagModel? esfOrderTag = orderTag as EdgeScanFeedOrderTagModel;

            message.HasOrderTag = orderTag != null ? BooleanEnum.True : BooleanEnum.False;
            message.OrderTagIsEdgeScanFeed = esfOrderTag != null ? BooleanEnum.True : BooleanEnum.False;

            if (orderTag != null)
            {
                message.OrderTagOrderDate = orderTag.OrderDate.ToUnixEpoch();
                message.OrderTagBid = orderTag.Bid;
                message.OrderTagAsk = orderTag.Ask;
                message.OrderTagBidSize = orderTag.BidSize;
                message.OrderTagAskSize = orderTag.AskSize;
                message.OrderTagTheo = orderTag.Theo;
                message.OrderTagEma = orderTag.Ema;
                message.OrderTagEdge = orderTag.Edge;
                message.OrderTagEdgeType = (byte)orderTag.EdgeType;
                message.OrderTagVolaTheo = orderTag.VolaTheo;
                message.OrderTagVolaTheoAdj = orderTag.VolaTheoAdj;
                message.OrderTagVolaIv = orderTag.VolaIv;
                message.OrderTagTheoBid = orderTag.TheoBid;
                message.OrderTagTheoAsk = orderTag.TheoAsk;
                message.OrderTagUnderBid = orderTag.UnderBid;
                message.OrderTagUnderAsk = orderTag.UnderAsk;
                message.OrderTagUnderBidSize = orderTag.UnderBidSize;
                message.OrderTagUnderAskSize = orderTag.UnderAskSize;
                message.OrderTagDigBid = orderTag.DigBid;
                message.OrderTagDigAsk = orderTag.DigAsk;
                message.OrderTagDigBidSize = orderTag.DigBidSize;
                message.OrderTagDigAskSize = orderTag.DigAskSize;
                message.OrderTagWeightedVega = orderTag.WeightedVega;
                message.OrderTagOrderSource = (byte)orderTag.OrderSource;
                message.OrderTagModuleType = (ushort)orderTag.ModuleType;
                message.OrderTagSubTypeCode = (ushort)orderTag.SubType;
                message.OrderTagSharedId = orderTag.SharedId;
                message.OrderTagSequence = orderTag.Sequence;
                message.OrderTagSubTypeSequence = orderTag.SubTypeSequence;
                message.OrderTagOrderSubType = (ushort)orderTag.OrderSubType;
                message.OrderTagResubmitCount = orderTag.ResubmitCount;
                message.OrderTagTotalEstimatedResubmit = orderTag.TotalEstimatedResubmit;
                message.OrderTagSessionId = orderTag.SessionId;

                if (esfOrderTag != null)
                {
                    message.OrderTagEsfEdgeScannerType = (ushort)esfOrderTag.EdgeScannerType;
                    message.OrderTagEsfConditionCode = (byte)esfOrderTag.EdgeScanFeedConditionCode;
                    message.OrderTagEsfEdge = esfOrderTag.EdgeScanFeedEdge;
                    message.OrderTagEsfTimespan = esfOrderTag.EdgeScanFeedTimespan;
                    message.OrderTagEsfRespondLatency = esfOrderTag.EdgeScanFeedRespondLatency;
                    message.OrderTagEsfDeltaAdjPrice = esfOrderTag.EdgeScanFeedDeltaAdjPrice;
                    message.OrderTagEsfBuyPrice = esfOrderTag.EdgeScanFeedBuyPrice;
                    message.OrderTagEsfSellPrice = esfOrderTag.EdgeScanFeedSellPrice;
                    message.OrderTagEsfBuyQty = esfOrderTag.EdgeScanFeedBuyQty;
                    message.OrderTagEsfSellQty = esfOrderTag.EdgeScanFeedSellQty;
                    message.OrderTagEsfBuyTime = esfOrderTag.EdgeScanFeedBuyTime.ToUnixEpoch();
                    message.OrderTagEsfSellTime = esfOrderTag.EdgeScanFeedSellTime.ToUnixEpoch();
                }
            }

            message.SetRoute((order.Route) ?? "");
            message.SetDestination((order.Destination) ?? "");
            message.SetSymbol((order.Symbol) ?? "");
            message.SetSpreadId((order.SpreadId) ?? "");
            message.SetAccountAcronym((order.AccountAcronym) ?? "");
            message.SetTag((order.Tag) ?? "");
            message.SetComment((order.Comment) ?? "");
            message.SetSmartRoute((order.SmartRoute) ?? "");
            message.SetRouteOverride((order.RouteOverride) ?? "");
            message.SetPrimaryExchange((order.PrimaryExchange) ?? "");

            message.SetOrderTagPermId(orderTag?.PermId ?? "");
            message.SetOrderTagTrader(orderTag?.Trader ?? "");
            message.SetOrderTagInstance(orderTag?.Instance ?? "");
            message.SetOrderTagParentSpreadHash(orderTag?.ParentSpreadHash ?? "");

            return message.Limit - offset;
        }

        public int EncodeMultipleOrderAdded(DirectBuffer directBuffer, int offset, int requestId, IOrder[] orders, int count, int totalQueued, int lastMessageIndex)
        {
            for (int index = 0; index < count; index++)
            {
                IOrder order = orders[index];

                if (order.IsComplexOrder && order is IComplexOrder complexOrder)
                {
                    foreach (var leg in complexOrder.Legs)
                    {
                    }
                }
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MultipleOrderAdded parentMessage = new MultipleOrderAdded();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MultipleOrderAdded.BlockLength;
            messageHeader.SchemaId = MultipleOrderAdded.SchemaId;
            messageHeader.TemplateId = MultipleOrderAdded.TemplateId;
            messageHeader.Version = MultipleOrderAdded.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            parentMessage.WrapForEncode(directBuffer, bufferOffset);

            parentMessage.RequestId = requestId;
            parentMessage.IncomingOrdersCount = totalQueued;
            parentMessage.LastOrderIndex = lastMessageIndex;
            MultipleOrderAdded.NoOrdersGroup message = parentMessage.NoOrdersCount(count);
            for (int index = 0; index < count; index++)
            {
                IOrder order = orders[index];
                message.Next();

                message.SetPermID(order.PermID);

                message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;
                message.PartiallyFilled = order.PartiallyFilled ? BooleanEnum.True : BooleanEnum.False;
                message.IsFirstFill = order.IsFirstFill ? BooleanEnum.True : BooleanEnum.False;

                message.LastQuantity = order.LastQuantity;
                message.FilledQty = order.FilledQty;
                message.LeavesQuantity = order.LeavesQuantity;
                message.CumulativeQuantity = order.CumulativeQuantity;
                message.Quantity = order.Quantity;

                message.SpreadAvgPrice.Mantissa = EncodeMantissa(order.SpreadAvgPrice, message.SpreadAvgPrice.Exponent);
                message.AveragePrice.Mantissa = EncodeMantissa(order.AveragePrice, message.AveragePrice.Exponent);
                message.Price.Mantissa = EncodeMantissa(order.Price, message.Price.Exponent);
                message.LastPrice.Mantissa = EncodeMantissa(order.LastPrice, message.LastPrice.Exponent);
                message.MinPrice.Mantissa = EncodeMantissa(order.MinPrice, message.MinPrice.Exponent);
                message.MaxPrice.Mantissa = EncodeMantissa(order.MaxPrice, message.MaxPrice.Exponent);
                message.TagEdge.Mantissa = EncodeMantissa(order.TagEdge, message.TagEdge.Exponent);
                message.TagMid.Mantissa = EncodeMantissa(order.TagMid, message.TagMid.Exponent);
                message.TagBid.Mantissa = EncodeMantissa(order.TagBid, message.TagBid.Exponent);
                message.TagAsk.Mantissa = EncodeMantissa(order.TagAsk, message.TagAsk.Exponent);
                message.TagTheo.Mantissa = EncodeMantissa(order.TagTheo, message.TagTheo.Exponent);
                message.TagVolaV0.Mantissa = EncodeMantissa(order.TagVolaV0, message.TagVolaV0.Exponent);
                message.TagVolaV1.Mantissa = EncodeMantissa(order.TagVolaV1, message.TagVolaV1.Exponent);
                message.TagVolaV2.Mantissa = EncodeMantissa(order.TagVolaV2, message.TagVolaV2.Exponent);
                message.TagEma.Mantissa = EncodeMantissa(order.TagEma, message.TagEma.Exponent);
                message.TagVolaIv = order.VolaIv;
                message.TheoBid.Mantissa = EncodeMantissa(order.TheoBid, message.TheoBid.Exponent);
                message.TheoAsk.Mantissa = EncodeMantissa(order.TheoAsk, message.TheoAsk.Exponent);
                message.Fee1.Mantissa = EncodeMantissa(order.Fee1, message.Fee1.Exponent);
                message.Fee2.Mantissa = EncodeMantissa(order.Fee2, message.Fee2.Exponent);
                message.Bid.Mantissa = EncodeMantissa(order.Bid, message.Bid.Exponent);
                message.Ask.Mantissa = EncodeMantissa(order.Ask, message.Ask.Exponent);
                message.UnderBid.Mantissa = EncodeMantissa(order.UnderBid, message.UnderBid.Exponent);
                message.UnderAsk.Mantissa = EncodeMantissa(order.UnderAsk, message.UnderAsk.Exponent);
                message.TV.Mantissa = EncodeMantissa(order.TV, message.TV.Exponent);
                message.Delta.Mantissa = EncodeMantissa(order.Delta, message.Delta.Exponent);
                message.ExchangeFee1.Mantissa = EncodeMantissa(order.ExchangeFee1, message.ExchangeFee1.Exponent);
                message.ExchangeFee2.Mantissa = EncodeMantissa(order.ExchangeFee2, message.ExchangeFee2.Exponent);
                message.BrokerFee1.Mantissa = EncodeMantissa(order.BrokerFee1, message.BrokerFee1.Exponent);
                message.BrokerFee2.Mantissa = EncodeMantissa(order.BrokerFee2, message.BrokerFee2.Exponent);
                message.TotalContracts.Mantissa = EncodeMantissa(order.TotalContracts, message.TotalContracts.Exponent);
                message.FillTime.Mantissa = EncodeMantissa(order.FillTime, message.FillTime.Exponent);
                message.TradeToNewTime.Mantissa = EncodeMantissa(order.TradeToNewTime, message.TradeToNewTime.Exponent);
                message.SubmitToNewTime.Mantissa = EncodeMantissa(order.SubmitToNewTime, message.SubmitToNewTime.Exponent);
                message.NewToCancelTime.Mantissa = EncodeMantissa(order.NewToCancelTime, message.NewToCancelTime.Exponent);
                message.BidPercentOfFillPrice.Mantissa = EncodeMantissa(order.BidPercentOfFillPrice, message.BidPercentOfFillPrice.Exponent);
                message.OmsBidPercentOfFillPrice.Mantissa = EncodeMantissa(order.OmsBidPercentOfFillPrice, message.OmsBidPercentOfFillPrice.Exponent);
                message.TotalDelta.Mantissa = EncodeMantissa(order.TotalDelta, message.TotalDelta.Exponent);
                message.HanweckTotalTheo.Mantissa = EncodeMantissa(order.HanweckTotalTheo, message.HanweckTotalTheo.Exponent);
                message.HanweckTotalGamma.Mantissa = EncodeMantissa(order.HanweckTotalGamma, message.HanweckTotalGamma.Exponent);
                message.HanweckTotalVega.Mantissa = EncodeMantissa(order.HanweckTotalVega, message.HanweckTotalVega.Exponent);
                message.HanweckTotalTheta.Mantissa = EncodeMantissa(order.HanweckTotalTheta, message.HanweckTotalTheta.Exponent);
                message.HanweckTotalRho.Mantissa = EncodeMantissa(order.HanweckTotalRho, message.HanweckTotalRho.Exponent);
                message.HanweckTotalIV.Mantissa = EncodeMantissa(order.HanweckTotalIV, message.HanweckTotalIV.Exponent);
                message.HanweckTotalUnder.Mantissa = EncodeMantissa(order.HanweckTotalUnder, message.HanweckTotalUnder.Exponent);
                message.HanweckTotalUBid.Mantissa = EncodeMantissa(order.HanweckTotalUBid, message.HanweckTotalUBid.Exponent);
                message.HanweckTotalUAsk.Mantissa = EncodeMantissa(order.HanweckTotalUAsk, message.HanweckTotalUAsk.Exponent);
                message.HanweckTotalBid.Mantissa = EncodeMantissa(order.HanweckTotalBid, message.HanweckTotalBid.Exponent);
                message.HanweckTotalAsk.Mantissa = EncodeMantissa(order.HanweckTotalAsk, message.HanweckTotalAsk.Exponent);

                message.EdgeOverride.Mantissa = EncodeMantissa(order.EdgeOverride, message.EdgeOverride.Exponent);
                message.AdjustedEdgeOverride.Mantissa = EncodeMantissa(order.AdjustedEdgeOverride, message.AdjustedEdgeOverride.Exponent);
                message.EdgeToTheo.Mantissa = EncodeMantissa(order.EdgeToTheo, message.EdgeToTheo.Exponent);
                message.TagEdgeToTheo.Mantissa = EncodeMantissa(order.TagEdgeToTheo, message.TagEdgeToTheo.Exponent);
                message.TagEdgeToEma.Mantissa = EncodeMantissa(order.TagEdgeToEma, message.TagEdgeToEma.Exponent);
                message.TagEdgeToVolaV0.Mantissa = EncodeMantissa(order.TagEdgeToVolaV0, message.TagEdgeToVolaV0.Exponent);
                message.TagEdgeToVolaV1.Mantissa = EncodeMantissa(order.TagEdgeToVolaV1, message.TagEdgeToVolaV1.Exponent);
                message.TagEdgeToVolaV2.Mantissa = EncodeMantissa(order.TagEdgeToVolaV2, message.TagEdgeToVolaV2.Exponent);
                message.TagBestBid.Mantissa = EncodeMantissa(order.TagBestBid, message.TagBestBid.Exponent);
                message.TagBestAsk.Mantissa = EncodeMantissa(order.TagBestAsk, message.TagBestAsk.Exponent);
                message.TagMktMkrBid.Mantissa = EncodeMantissa(order.TagMktMkrBid, message.TagMktMkrBid.Exponent);
                message.TagMktMkrAsk.Mantissa = EncodeMantissa(order.TagMktMkrAsk, message.TagMktMkrAsk.Exponent);
                message.InitialEdge.Mantissa = EncodeMantissa(order.InitialEdge, message.InitialEdge.Exponent);
                message.OpenEdge.Mantissa = EncodeMantissa(order.OpenEdge, message.OpenEdge.Exponent);
                message.CloseEdge.Mantissa = EncodeMantissa(order.CloseEdge, message.CloseEdge.Exponent);
                message.FirstEdgeAcquired = order.FirstEdgeAcquired ? BooleanEnum.True : BooleanEnum.False;
                message.FirstEdge.Mantissa = EncodeMantissa(order.FirstEdge, message.FirstEdge.Exponent);
                message.LastEdge.Mantissa = EncodeMantissa(order.LastEdge, message.LastEdge.Exponent);
                message.DeltaAdjLastEdge.Mantissa = EncodeMantissa(order.DeltaAdjLastEdge, message.DeltaAdjLastEdge.Exponent);
                message.DeltaAdjLastEdgeNotional.Mantissa = EncodeMantissa(order.DeltaAdjLastEdgeNotional, message.DeltaAdjLastEdgeNotional.Exponent);
                message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent);

                message.DeltaAdjChange.Mantissa = EncodeMantissa(order.DeltaAdjChange, message.DeltaAdjChange.Exponent);
                message.DeltaAdjChangeNotional.Mantissa = EncodeMantissa(order.DeltaAdjChangeNotional, message.DeltaAdjChangeNotional.Exponent);

                message.EdgeScanFeedEdge.Mantissa = EncodeMantissa(order.EdgeScanFeedEdge, message.EdgeScanFeedEdge.Exponent);
                message.EdgeScanFeedTimespan.Mantissa = EncodeMantissa(order.EdgeScanFeedTimespan, message.EdgeScanFeedTimespan.Exponent);

                message.EdgeScanFeedBuyPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedBuyPrice, message.EdgeScanFeedBuyPrice.Exponent);
                message.EdgeScanFeedBuyQty = order.EdgeScanFeedBuyQty;
                message.EdgeScanFeedSellPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedSellPrice, message.EdgeScanFeedSellPrice.Exponent);
                message.EdgeScanFeedSellQty = order.EdgeScanFeedSellQty;
                message.EdgeScanFeedBuyTime = order.EdgeScanFeedBuyTime.ToUnixEpoch();
                message.EdgeScanFeedSellTime = order.EdgeScanFeedSellTime.ToUnixEpoch();
                message.EdgeScanFeedRespondLatency.Mantissa = EncodeMantissa(order.EdgeScanFeedRespondLatency, message.EdgeScanFeedRespondLatency.Exponent);

                message.EdgeScanFeedConditionCode = (byte)order.EdgeScanFeedConditionCode;

                message.ResubmitCount = order.ResubmitCount;
                message.TotalEstimatedResubmit = order.TotalEstimatedResubmit;

                var side = order.Side;
                message.AggressorSide = side == Side.Buy ? AggressorSide.Buy : AggressorSide.Sell;
                message.OrderStatus = (OrderStatus)order.OrderStatus;
                message.BaseStrategy = (Generated.BaseStrategy)order.BaseStrategy;
                message.PositionEffect = (PositionEffect)order.PositionEffect;
                message.TimeInForce = (Generated.TimeInForce)order.TimeInForce;

                message.OrderSource = (Generated.OrderSource)order.OrderSource;

                message.SetUsername(order.Username == null ? "" : order.Username.Length > 10 ? order.Username[..10] : order.Username);
                message.SetUnderlyingSymbol(order.UnderlyingSymbol);

                message.SubmitTime = ToUnixEpoch(order.SubmitTime);
                message.LastUpdateTime = ToUnixEpoch(order.LastUpdateTime);
                message.Timestamp = ToUnixEpoch(order.Timestamp);
                message.NewStatusTimeStamp = ToUnixEpoch(order.NewStatusTimeStamp);

                message.DeltaAdjustedTheo.Mantissa = EncodeMantissa(order.DeltaAdjustedTheo, message.DeltaAdjustedTheo.Exponent);
                message.BidSize = order.BidSize;
                message.AskSize = order.AskSize;
                message.UnderlyingBidSize = order.UnderlyingBidSize;
                message.UnderlyingAskSize = order.UnderlyingAskSize;

                message.EdgeType = (int)order.EdgeType;
                message.Edge.Mantissa = EncodeMantissa(order.Edge, message.Edge.Exponent);

                message.IsDeltaAdjusted = order.IsDeltaAdjusted ? BooleanEnum.True : BooleanEnum.False;
                message.LoopInitLatency.Mantissa = EncodeMantissa(order.LoopInitLatency, message.LoopInitLatency.Exponent);
                message.TagUnderBid.Mantissa = EncodeMantissa(order.TagUnderBid, message.TagUnderBid.Exponent);
                message.TagUnderAsk.Mantissa = EncodeMantissa(order.TagUnderAsk, message.TagUnderAsk.Exponent);
                message.DigBid.Mantissa = EncodeMantissa(order.DigBid, message.DigBid.Exponent);
                message.DigAsk.Mantissa = EncodeMantissa(order.DigAsk, message.DigAsk.Exponent);
                message.WeightedVega.Mantissa = EncodeMantissa(order.WeightedVega, message.WeightedVega.Exponent);
                message.DigBidSize = order.DigBidSize;
                message.DigAskSize = order.DigAskSize;

                message.IsTagged = order.IsTagged ? BooleanEnum.True : BooleanEnum.False;

                var hardSide = order.HardSide;
                message.HardSide = hardSide.HasValue ? (Generated.Side)hardSide : Generated.Side.NULL_VALUE;
                message.HardSideDesignationTime = order.HardSideDesignationTime.ToUnixEpoch();
                message.HardSideBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideBuyGiveUp, message.HardSideBuyGiveUp.Exponent);
                message.HardSideSellGiveUp.Mantissa = EncodeMantissa(order.HardSideSellGiveUp, message.HardSideSellGiveUp.Exponent);
                var hardSideAtTrade = order.HardSideAtTrade;
                message.HardSideAtTrade = hardSideAtTrade.HasValue ? (Generated.Side)hardSideAtTrade : Generated.Side.NULL_VALUE;
                message.HardSideAtTradeDesignationTime = order.HardSideAtTradeDesignationTime.ToUnixEpoch();
                message.HardSideAtTradeBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeBuyGiveUp, message.HardSideAtTradeBuyGiveUp.Exponent);
                message.HardSideAtTradeSellGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeSellGiveUp, message.HardSideAtTradeSellGiveUp.Exponent);

                message.EdgeGiveUp.Mantissa = EncodeMantissa(order.EdgeGiveUp, message.EdgeGiveUp.Exponent);
                message.CloseSubs.Mantissa = EncodeMantissa(order.CloseSubs, message.CloseSubs.Exponent);
                message.OrderEdgeToTheo.Mantissa = EncodeMantissa(order.OrderEdgeToTheo, message.OrderEdgeToTheo.Exponent);

                message.TimeValue.Mantissa = EncodeMantissa(order.TimeValue, message.TimeValue.Exponent);
                message.IntrinsicValue.Mantissa = EncodeMantissa(order.IntrinsicValue, message.IntrinsicValue.Exponent);
                message.FVDivs.Mantissa = EncodeMantissa(order.FVDivs, message.FVDivs.Exponent);
                message.UFwd.Mantissa = EncodeMantissa(order.UFwd, message.UFwd.Exponent);
                message.UFwdFactor.Mantissa = EncodeMantissa(order.UFwdFactor, message.UFwdFactor.Exponent);
                message.BorrowCost.Mantissa = EncodeMantissa(order.BorrowCost, message.BorrowCost.Exponent);
                message.BorrowRate.Mantissa = EncodeMantissa(order.BorrowRate, message.BorrowRate.Exponent);
                message.UPrice.Mantissa = EncodeMantissa(order.UPrice, message.UPrice.Exponent);
                message.UTheo.Mantissa = EncodeMantissa(order.UTheo, message.UTheo.Exponent);

                message.SharedId = order.SharedId;
                message.Sequence = order.Sequence;
                message.TypeId = (ushort)order.TypeId;
                message.SubTypeCode = (ushort)order.SubTypeId;
                message.SubTypeSequence = order.SubTypeSequence;
                var venue = order.Venue;
                message.Venue = venue.HasValue ? (byte)venue : MultipleOrderAdded.NoOrdersGroup.VenueNullValue;
                message.CostOfHedging.Mantissa = EncodeMantissa(order.CostOfHedging, message.CostOfHedging.Exponent);
                var subType = order.SubType;
                message.SubType = subType.HasValue ? (byte)subType : MultipleOrderAdded.NoOrdersGroup.SubTypeNullValue;

                if (!order.IsComplexOrder)
                {
                    message.NoLegsCount(0);
                }
                else
                {
                    IComplexOrder complexOrder = (IComplexOrder)order;
                    MultipleOrderAdded.NoOrdersGroup.NoLegsGroup legMessage = message.NoLegsCount(complexOrder.Legs.Count);
                    for (int i = 0; i < complexOrder.Legs.Count; i++)
                    {
                        IComplexOrderLeg leg = complexOrder.Legs.ElementAt(i);
                        legMessage.Next();

                        legMessage.SetLegID(leg.LegID);

                        legMessage.Ratio = leg.Ratio;
                        legMessage.Quantity = leg.Quantity;
                        legMessage.LastQuantity = leg.LastQuantity;
                        legMessage.LeavesQuantity = leg.LeavesQuantity;
                        legMessage.CumulativeQuantity = leg.CumulativeQuantity;

                        legMessage.ExchangeFee2.Mantissa = EncodeMantissa(leg.ExchangeFee2, legMessage.ExchangeFee2.Exponent);
                        legMessage.ExchangeFee1.Mantissa = EncodeMantissa(leg.ExchangeFee1, legMessage.ExchangeFee1.Exponent);
                        legMessage.Fee2.Mantissa = EncodeMantissa(leg.Fee2, legMessage.Fee2.Exponent);
                        legMessage.Fee1.Mantissa = EncodeMantissa(leg.Fee1, legMessage.Fee1.Exponent);
                        legMessage.Delta.Mantissa = EncodeMantissa(leg.Delta, legMessage.Delta.Exponent);
                        legMessage.TV.Mantissa = EncodeMantissa(leg.TV, legMessage.TV.Exponent);
                        legMessage.Ask.Mantissa = EncodeMantissa(leg.Ask, legMessage.Ask.Exponent);
                        legMessage.Bid.Mantissa = EncodeMantissa(leg.Bid, legMessage.Bid.Exponent);
                        legMessage.AveragePrice.Mantissa = EncodeMantissa(leg.AveragePrice, legMessage.AveragePrice.Exponent);
                        legMessage.LastPrice.Mantissa = EncodeMantissa(leg.LastPrice, legMessage.LastPrice.Exponent);
                        legMessage.BrokerFee1.Mantissa = EncodeMantissa(leg.BrokerFee1, legMessage.BrokerFee1.Exponent);
                        legMessage.BrokerFee2.Mantissa = EncodeMantissa(leg.BrokerFee2, legMessage.BrokerFee2.Exponent);
                        legMessage.HanweckTV.Mantissa = EncodeMantissa(leg.HanweckTV, legMessage.HanweckTV.Exponent);
                        legMessage.HanweckGamma.Mantissa = EncodeMantissa(leg.HanweckGamma, legMessage.HanweckGamma.Exponent);
                        legMessage.HanweckVega.Mantissa = EncodeMantissa(leg.HanweckVega, legMessage.HanweckVega.Exponent);
                        legMessage.HanweckTheta.Mantissa = EncodeMantissa(leg.HanweckTheta, legMessage.HanweckTheta.Exponent);
                        legMessage.HanweckRho.Mantissa = EncodeMantissa(leg.HanweckRho, legMessage.HanweckRho.Exponent);
                        legMessage.HanweckIV.Mantissa = EncodeMantissa(leg.HanweckIV, legMessage.HanweckIV.Exponent);
                        legMessage.HanweckUnder.Mantissa = EncodeMantissa(leg.HanweckUnder, legMessage.HanweckUnder.Exponent);
                        legMessage.HanweckUnderBid.Mantissa = EncodeMantissa(leg.HanweckUnderBid, legMessage.HanweckUnderBid.Exponent);
                        legMessage.HanweckUnderAsk.Mantissa = EncodeMantissa(leg.HanweckUnderAsk, legMessage.HanweckUnderAsk.Exponent);
                        legMessage.HanweckBid.Mantissa = EncodeMantissa(leg.HanweckBid, legMessage.HanweckBid.Exponent);
                        legMessage.HanweckAsk.Mantissa = EncodeMantissa(leg.HanweckAsk, legMessage.HanweckAsk.Exponent);

                        legMessage.DeltaAdjustedTheo.Mantissa = EncodeMantissa(leg.DeltaAdjustedTheo, legMessage.DeltaAdjustedTheo.Exponent);
                        legMessage.BidSize = leg.BidSize;
                        legMessage.AskSize = leg.AskSize;

                        legMessage.PositionEffect = (PositionEffect)leg.PositionEffect;
                        legMessage.LegSide = leg.Side == Side.Buy ? LegSide.BuySide : LegSide.SellSide;
                        legMessage.OrderStatus = (OrderStatus)leg.OrderStatus;

                        legMessage.Timestamp = ToUnixEpoch(leg.Timestamp);
                        legMessage.LastUpdateTime = ToUnixEpoch(leg.LastUpdateTime);
                        legMessage.HanweckBidTime = ToUnixEpoch(leg.HanweckBidTime);
                        legMessage.HanweckAskTime = ToUnixEpoch(leg.HanweckAskTime);
                        legMessage.HanweckTimestamp = ToUnixEpoch(leg.HanweckTimestamp);

                        legMessage.TimeValue.Mantissa = EncodeMantissa(leg.TimeValue, legMessage.TimeValue.Exponent);
                        legMessage.IntrinsicValue.Mantissa = EncodeMantissa(leg.IntrinsicValue, legMessage.IntrinsicValue.Exponent);
                        legMessage.FVDivs.Mantissa = EncodeMantissa(leg.FVDivs, legMessage.FVDivs.Exponent);
                        legMessage.UFwd.Mantissa = EncodeMantissa(leg.UFwd, legMessage.UFwd.Exponent);
                        legMessage.UFwdFactor.Mantissa = EncodeMantissa(leg.UFwdFactor, legMessage.UFwdFactor.Exponent);
                        legMessage.BorrowCost.Mantissa = EncodeMantissa(leg.BorrowCost, legMessage.BorrowCost.Exponent);
                        legMessage.BorrowRate.Mantissa = EncodeMantissa(leg.BorrowRate, legMessage.BorrowRate.Exponent);
                        legMessage.UPrice.Mantissa = EncodeMantissa(leg.UPrice, legMessage.UPrice.Exponent);
                        legMessage.UTheo.Mantissa = EncodeMantissa(leg.UTheo, legMessage.UTheo.Exponent);

                        EncodeLegContraFields_MultipleOrderAdded(legMessage, leg);

                        legMessage.SetPermID((leg.PermID) ?? "");
                        legMessage.SetOrderID((leg.OrderID) ?? "");
                        legMessage.SetSymbol((leg.Symbol) ?? "");
                    }
                }

                EncodeOrderContraFields_MultipleOrderAdded(message, order);

                message.SetLastExchange((order.LastExchange) ?? "");
                message.SetExchanges((order.Exchanges) ?? "");
                message.SetReason((order.Reason) ?? "");
                message.SetSource((order.Source) ?? "");
                message.SetAccountAcronym((order.AccountAcronym) ?? "");
                message.SetTag((order.Tag) ?? "");
                message.SetTrader((order.Trader) ?? "");
                message.SetOrderType((order.Type) ?? "");
                message.SetOrderID((order.OrderID) ?? "");
                message.SetRoute((order.Route) ?? "");
                message.SetDestination((order.Destination) ?? "");
                message.SetSymbol((order.Symbol) ?? "");
                message.SetDescription((order.Description) ?? "");
                message.SetSpreadId((order.SpreadId) ?? "");
                message.SetFullTag((order.Tag) ?? "");
                message.SetComment((order.Comment) ?? "");
                message.SetAutomationType((order.AutomationType) ?? "");
                message.SetSpreadHash((order.SpreadHash) ?? "");
                message.SetTagger(order.Tagger ?? "");
                message.SetTaggedMessage(order.TaggedMessage ?? "");
            }


            return parentMessage.Limit - offset;
        }

        private static void EncodeLegContraFields_MultipleOrderAdded(MultipleOrderAdded.NoOrdersGroup.NoLegsGroup legMessage, IComplexOrderLeg leg)
        {
            var legCaps = leg.ContraCapacities;
            var legCapsGroup = legMessage.NoLegContraCapacitiesCount(legCaps?.Count ?? 0);
            if (legCaps != null)
            {
                for (int j = 0; j < legCaps.Count; j++)
                {
                    legCapsGroup.Next().Value = (byte)legCaps[j];
                }
            }

            var legBrokers = leg.ContraBrokerNames;
            var legBrokersGroup = legMessage.NoLegContraBrokerNamesCount(legBrokers?.Count ?? 0);
            if (legBrokers != null)
            {
                for (int j = 0; j < legBrokers.Count; j++)
                {
                    legBrokersGroup.Next().Value = (byte)legBrokers[j];
                }
            }

            var legCmtas = leg.ContraCmtas;
            var legCmtasGroup = legMessage.NoLegContraCmtasCount(legCmtas?.Count ?? 0);
            if (legCmtas != null)
            {
                for (int j = 0; j < legCmtas.Count; j++)
                {
                    legCmtasGroup.Next().Value = (byte)legCmtas[j];
                }
            }

            var legTraders = leg.ContraTraders;
            var legTradersGroup = legMessage.NoLegContraTradersCount(legTraders?.Count ?? 0);
            if (legTraders != null)
            {
                for (int j = 0; j < legTraders.Count; j++)
                {
                    legTradersGroup.Next().Value = (byte)legTraders[j];
                }
            }
        }

        private static void EncodeOrderContraFields_MultipleOrderAdded(MultipleOrderAdded.NoOrdersGroup message, IOrder order)
        {
            var orderCaps = order.ContraCapacities;
            var orderCapsGroup = message.NoContraCapacitiesCount(orderCaps?.Count ?? 0);
            if (orderCaps != null)
            {
                for (int i = 0; i < orderCaps.Count; i++)
                {
                    orderCapsGroup.Next().Value = (byte)orderCaps[i];
                }
            }

            var orderBrokers = order.ContraBrokerNames;
            var orderBrokersGroup = message.NoContraBrokerNamesCount(orderBrokers?.Count ?? 0);
            if (orderBrokers != null)
            {
                for (int i = 0; i < orderBrokers.Count; i++)
                {
                    orderBrokersGroup.Next().Value = (byte)orderBrokers[i];
                }
            }

            var orderCmtas = order.ContraCmtas;
            var orderCmtasGroup = message.NoContraCmtasCount(orderCmtas?.Count ?? 0);
            if (orderCmtas != null)
            {
                for (int i = 0; i < orderCmtas.Count; i++)
                {
                    orderCmtasGroup.Next().Value = (byte)orderCmtas[i];
                }
            }

            var orderTraders = order.ContraTraders;
            var orderTradersGroup = message.NoContraTradersCount(orderTraders?.Count ?? 0);
            if (orderTraders != null)
            {
                for (int i = 0; i < orderTraders.Count; i++)
                {
                    orderTradersGroup.Next().Value = (byte)orderTraders[i];
                }
            }
        }

        public int EncodeOrderUpdate(DirectBuffer directBuffer, int offset, IOrder order)
        {


            if (order.IsComplexOrder)
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                foreach (IComplexOrderLeg leg in complexOrder.Legs)
                {
                }
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderUpdated message = new OrderUpdated();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderUpdated.BlockLength;
            messageHeader.SchemaId = OrderUpdated.SchemaId;
            messageHeader.TemplateId = OrderUpdated.TemplateId;
            messageHeader.Version = OrderUpdated.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermID(order.PermID);

            message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;
            message.PartiallyFilled = order.PartiallyFilled ? BooleanEnum.True : BooleanEnum.False;

            message.LastQuantity = order.LastQuantity;
            message.FilledQty = order.FilledQty;
            message.LeavesQuantity = order.LeavesQuantity;
            message.CumulativeQuantity = order.CumulativeQuantity;
            message.Quantity = order.Quantity;

            message.SpreadAvgPrice.Mantissa = EncodeMantissa(order.SpreadAvgPrice, message.SpreadAvgPrice.Exponent);
            message.AveragePrice.Mantissa = EncodeMantissa(order.AveragePrice, message.AveragePrice.Exponent);
            message.Price.Mantissa = EncodeMantissa(order.Price, message.Price.Exponent);
            message.LastPrice.Mantissa = EncodeMantissa(order.LastPrice, message.LastPrice.Exponent);
            message.MinPrice.Mantissa = EncodeMantissa(order.MinPrice, message.MinPrice.Exponent);
            message.MaxPrice.Mantissa = EncodeMantissa(order.MaxPrice, message.MaxPrice.Exponent);
            message.Fee1.Mantissa = EncodeMantissa(order.Fee1, message.Fee1.Exponent);
            message.Fee2.Mantissa = EncodeMantissa(order.Fee2, message.Fee2.Exponent);
            message.Bid.Mantissa = EncodeMantissa(order.Bid, message.Bid.Exponent);
            message.Ask.Mantissa = EncodeMantissa(order.Ask, message.Ask.Exponent);
            message.UnderBid.Mantissa = EncodeMantissa(order.UnderBid, message.UnderBid.Exponent);
            message.UnderAsk.Mantissa = EncodeMantissa(order.UnderAsk, message.UnderAsk.Exponent);
            message.TV.Mantissa = EncodeMantissa(order.TV, message.TV.Exponent);
            message.Delta.Mantissa = EncodeMantissa(order.Delta, message.Delta.Exponent);
            message.ExchangeFee1.Mantissa = EncodeMantissa(order.ExchangeFee1, message.ExchangeFee1.Exponent);
            message.ExchangeFee2.Mantissa = EncodeMantissa(order.ExchangeFee2, message.ExchangeFee2.Exponent);
            message.BrokerFee1.Mantissa = EncodeMantissa(order.BrokerFee1, message.BrokerFee1.Exponent);
            message.BrokerFee2.Mantissa = EncodeMantissa(order.BrokerFee2, message.BrokerFee2.Exponent);
            message.TotalContracts.Mantissa = EncodeMantissa(order.TotalContracts, message.TotalContracts.Exponent);
            message.FillTime.Mantissa = EncodeMantissa(order.FillTime, message.FillTime.Exponent);
            message.TradeToNewTime.Mantissa = EncodeMantissa(order.TradeToNewTime, message.TradeToNewTime.Exponent);
            message.SubmitToNewTime.Mantissa = EncodeMantissa(order.SubmitToNewTime, message.SubmitToNewTime.Exponent);
            message.NewToCancelTime.Mantissa = EncodeMantissa(order.NewToCancelTime, message.NewToCancelTime.Exponent);
            message.BidPercentOfFillPrice.Mantissa = EncodeMantissa(order.BidPercentOfFillPrice, message.BidPercentOfFillPrice.Exponent);
            message.OmsBidPercentOfFillPrice.Mantissa = EncodeMantissa(order.OmsBidPercentOfFillPrice, message.OmsBidPercentOfFillPrice.Exponent);
            message.TotalDelta.Mantissa = EncodeMantissa(order.TotalDelta, message.TotalDelta.Exponent);
            message.HanweckTotalTheo.Mantissa = EncodeMantissa(order.HanweckTotalTheo, message.HanweckTotalTheo.Exponent);
            message.HanweckTotalGamma.Mantissa = EncodeMantissa(order.HanweckTotalGamma, message.HanweckTotalGamma.Exponent);
            message.HanweckTotalVega.Mantissa = EncodeMantissa(order.HanweckTotalVega, message.HanweckTotalVega.Exponent);
            message.HanweckTotalTheta.Mantissa = EncodeMantissa(order.HanweckTotalTheta, message.HanweckTotalTheta.Exponent);
            message.HanweckTotalRho.Mantissa = EncodeMantissa(order.HanweckTotalRho, message.HanweckTotalRho.Exponent);
            message.HanweckTotalIV.Mantissa = EncodeMantissa(order.HanweckTotalIV, message.HanweckTotalIV.Exponent);
            message.HanweckTotalUnder.Mantissa = EncodeMantissa(order.HanweckTotalUnder, message.HanweckTotalUnder.Exponent);
            message.HanweckTotalUBid.Mantissa = EncodeMantissa(order.HanweckTotalUBid, message.HanweckTotalUBid.Exponent);
            message.HanweckTotalUAsk.Mantissa = EncodeMantissa(order.HanweckTotalUAsk, message.HanweckTotalUAsk.Exponent);
            message.HanweckTotalBid.Mantissa = EncodeMantissa(order.HanweckTotalBid, message.HanweckTotalBid.Exponent);
            message.HanweckTotalAsk.Mantissa = EncodeMantissa(order.HanweckTotalAsk, message.HanweckTotalAsk.Exponent);
            message.EdgeToTheo.Mantissa = EncodeMantissa(order.EdgeToTheo, message.EdgeToTheo.Exponent);
            message.TagEdgeToTheo.Mantissa = EncodeMantissa(order.TagEdgeToTheo, message.TagEdgeToTheo.Exponent);
            message.TagEdgeToEma.Mantissa = EncodeMantissa(order.TagEdgeToEma, message.TagEdgeToEma.Exponent);
            message.TagEdgeToVolaV0.Mantissa = EncodeMantissa(order.TagEdgeToVolaV0, message.TagEdgeToVolaV0.Exponent);
            message.TagEdgeToVolaV1.Mantissa = EncodeMantissa(order.TagEdgeToVolaV1, message.TagEdgeToVolaV1.Exponent);
            message.TagEdgeToVolaV2.Mantissa = EncodeMantissa(order.TagEdgeToVolaV2, message.TagEdgeToVolaV2.Exponent);
            message.InitialEdge.Mantissa = EncodeMantissa(order.InitialEdge, message.InitialEdge.Exponent);
            message.OpenEdge.Mantissa = EncodeMantissa(order.OpenEdge, message.OpenEdge.Exponent);
            message.CloseEdge.Mantissa = EncodeMantissa(order.CloseEdge, message.CloseEdge.Exponent);

            message.OrderStatus = (OrderStatus)order.OrderStatus;

            message.LastUpdateTime = ToUnixEpoch(order.LastUpdateTime);
            message.NewStatusTimeStamp = ToUnixEpoch(order.NewStatusTimeStamp);

            message.DeltaAdjustedTheo.Mantissa = EncodeMantissa(order.DeltaAdjustedTheo, message.DeltaAdjustedTheo.Exponent);
            message.BidSize = order.BidSize;
            message.AskSize = order.AskSize;
            message.UnderlyingBidSize = order.UnderlyingBidSize;
            message.UnderlyingAskSize = order.UnderlyingAskSize;

            message.LastEdge.Mantissa = EncodeMantissa(order.LastEdge, message.LastEdge.Exponent);
            message.DeltaAdjLastEdge.Mantissa = EncodeMantissa(order.DeltaAdjLastEdge, message.DeltaAdjLastEdge.Exponent);
            message.DeltaAdjLastEdgeNotional.Mantissa = EncodeMantissa(order.DeltaAdjLastEdgeNotional, message.DeltaAdjLastEdgeNotional.Exponent);
            message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent);

            message.DeltaAdjChange.Mantissa = EncodeMantissa(order.DeltaAdjChange, message.DeltaAdjChange.Exponent);
            message.DeltaAdjChangeNotional.Mantissa = EncodeMantissa(order.DeltaAdjChangeNotional, message.DeltaAdjChangeNotional.Exponent);

            message.PositionEffect = (PositionEffect)order.PositionEffect;
            message.LoopInitLatency.Mantissa = EncodeMantissa(order.LoopInitLatency, message.LoopInitLatency.Exponent);
            message.IsTagged = order.IsTagged ? BooleanEnum.True : BooleanEnum.False;
            var hardSideAtTrade = order.HardSideAtTrade;
            message.HardSideAtTrade = hardSideAtTrade.HasValue ? (Generated.Side)hardSideAtTrade : Generated.Side.NULL_VALUE;
            message.HardSideAtTradeDesignationTime = order.HardSideAtTradeDesignationTime.ToUnixEpoch();

            message.EdgeGiveUp.Mantissa = EncodeMantissa(order.EdgeGiveUp, message.EdgeGiveUp.Exponent);
            message.CloseSubs.Mantissa = EncodeMantissa(order.CloseSubs, message.CloseSubs.Exponent);
            message.OrderEdgeToTheo.Mantissa = EncodeMantissa(order.OrderEdgeToTheo, message.OrderEdgeToTheo.Exponent);

            message.TimeValue.Mantissa = EncodeMantissa(order.TimeValue, message.TimeValue.Exponent);
            message.IntrinsicValue.Mantissa = EncodeMantissa(order.IntrinsicValue, message.IntrinsicValue.Exponent);
            message.FVDivs.Mantissa = EncodeMantissa(order.FVDivs, message.FVDivs.Exponent);
            message.UFwd.Mantissa = EncodeMantissa(order.UFwd, message.UFwd.Exponent);
            message.UFwdFactor.Mantissa = EncodeMantissa(order.UFwdFactor, message.UFwdFactor.Exponent);
            message.BorrowCost.Mantissa = EncodeMantissa(order.BorrowCost, message.BorrowCost.Exponent);
            message.BorrowRate.Mantissa = EncodeMantissa(order.BorrowRate, message.BorrowRate.Exponent);
            message.UPrice.Mantissa = EncodeMantissa(order.UPrice, message.UPrice.Exponent);
            message.UTheo.Mantissa = EncodeMantissa(order.UTheo, message.UTheo.Exponent);
            message.CostOfHedging.Mantissa = EncodeMantissa(order.CostOfHedging, message.CostOfHedging.Exponent);
            message.DigBid.Mantissa = EncodeMantissa(order.DigBid, message.DigBid.Exponent);
            message.DigAsk.Mantissa = EncodeMantissa(order.DigAsk, message.DigAsk.Exponent);
            message.WeightedVega.Mantissa = EncodeMantissa(order.WeightedVega, message.WeightedVega.Exponent);
            message.DigBidSize = order.DigBidSize;
            message.DigAskSize = order.DigAskSize;

            if (!order.IsComplexOrder)
            {
                message.NoLegsCount(0);
            }
            else
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                OrderUpdated.NoLegsGroup legMessage = message.NoLegsCount(complexOrder.Legs.Count);
                for (int i = 0; i < complexOrder.Legs.Count; i++)
                {
                    legMessage.Next();
                    IComplexOrderLeg leg = complexOrder.Legs.ElementAt(i);

                    legMessage.SetLegID(leg.LegID);

                    legMessage.LastQuantity = leg.LastQuantity;
                    legMessage.LeavesQuantity = leg.LeavesQuantity;
                    legMessage.CumulativeQuantity = leg.CumulativeQuantity;

                    legMessage.ExchangeFee2.Mantissa = EncodeMantissa(leg.ExchangeFee2, legMessage.ExchangeFee2.Exponent);
                    legMessage.ExchangeFee1.Mantissa = EncodeMantissa(leg.ExchangeFee1, legMessage.ExchangeFee1.Exponent);
                    legMessage.Fee2.Mantissa = EncodeMantissa(leg.Fee2, legMessage.Fee2.Exponent);
                    legMessage.Fee1.Mantissa = EncodeMantissa(leg.Fee1, legMessage.Fee1.Exponent);
                    legMessage.Delta.Mantissa = EncodeMantissa(leg.Delta, legMessage.Delta.Exponent);
                    legMessage.TV.Mantissa = EncodeMantissa(leg.TV, legMessage.TV.Exponent);
                    legMessage.Ask.Mantissa = EncodeMantissa(leg.Ask, legMessage.Ask.Exponent);
                    legMessage.Bid.Mantissa = EncodeMantissa(leg.Bid, legMessage.Bid.Exponent);
                    legMessage.AveragePrice.Mantissa = EncodeMantissa(leg.AveragePrice, legMessage.AveragePrice.Exponent);
                    legMessage.LastPrice.Mantissa = EncodeMantissa(leg.LastPrice, legMessage.LastPrice.Exponent);
                    legMessage.BrokerFee1.Mantissa = EncodeMantissa(leg.BrokerFee1, legMessage.BrokerFee1.Exponent);
                    legMessage.BrokerFee2.Mantissa = EncodeMantissa(leg.BrokerFee2, legMessage.BrokerFee2.Exponent);
                    legMessage.HanweckTV.Mantissa = EncodeMantissa(leg.HanweckTV, legMessage.HanweckTV.Exponent);
                    legMessage.HanweckGamma.Mantissa = EncodeMantissa(leg.HanweckGamma, legMessage.HanweckGamma.Exponent);
                    legMessage.HanweckVega.Mantissa = EncodeMantissa(leg.HanweckVega, legMessage.HanweckVega.Exponent);
                    legMessage.HanweckTheta.Mantissa = EncodeMantissa(leg.HanweckTheta, legMessage.HanweckTheta.Exponent);
                    legMessage.HanweckRho.Mantissa = EncodeMantissa(leg.HanweckRho, legMessage.HanweckRho.Exponent);
                    legMessage.HanweckIV.Mantissa = EncodeMantissa(leg.HanweckIV, legMessage.HanweckIV.Exponent);
                    legMessage.HanweckUnder.Mantissa = EncodeMantissa(leg.HanweckUnder, legMessage.HanweckUnder.Exponent);
                    legMessage.HanweckUnderBid.Mantissa = EncodeMantissa(leg.HanweckUnderBid, legMessage.HanweckUnderBid.Exponent);
                    legMessage.HanweckUnderAsk.Mantissa = EncodeMantissa(leg.HanweckUnderAsk, legMessage.HanweckUnderAsk.Exponent);
                    legMessage.HanweckBid.Mantissa = EncodeMantissa(leg.HanweckBid, legMessage.HanweckBid.Exponent);
                    legMessage.HanweckAsk.Mantissa = EncodeMantissa(leg.HanweckAsk, legMessage.HanweckAsk.Exponent);

                    legMessage.DeltaAdjustedTheo.Mantissa = EncodeMantissa(leg.DeltaAdjustedTheo, legMessage.DeltaAdjustedTheo.Exponent);
                    legMessage.BidSize = leg.BidSize;
                    legMessage.AskSize = leg.AskSize;

                    legMessage.OrderStatus = (OrderStatus)leg.OrderStatus;

                    legMessage.LastUpdateTime = ToUnixEpoch(leg.LastUpdateTime);
                    legMessage.HanweckBidTime = ToUnixEpoch(leg.HanweckBidTime);
                    legMessage.HanweckAskTime = ToUnixEpoch(leg.HanweckAskTime);
                    legMessage.HanweckTimestamp = ToUnixEpoch(leg.HanweckTimestamp);

                    legMessage.TimeValue.Mantissa = EncodeMantissa(leg.TimeValue, legMessage.TimeValue.Exponent);
                    legMessage.IntrinsicValue.Mantissa = EncodeMantissa(leg.IntrinsicValue, legMessage.IntrinsicValue.Exponent);
                    legMessage.FVDivs.Mantissa = EncodeMantissa(leg.FVDivs, legMessage.FVDivs.Exponent);
                    legMessage.UFwd.Mantissa = EncodeMantissa(leg.UFwd, legMessage.UFwd.Exponent);
                    legMessage.UFwdFactor.Mantissa = EncodeMantissa(leg.UFwdFactor, legMessage.UFwdFactor.Exponent);
                    legMessage.BorrowCost.Mantissa = EncodeMantissa(leg.BorrowCost, legMessage.BorrowCost.Exponent);
                    legMessage.BorrowRate.Mantissa = EncodeMantissa(leg.BorrowRate, legMessage.BorrowRate.Exponent);
                    legMessage.UPrice.Mantissa = EncodeMantissa(leg.UPrice, legMessage.UPrice.Exponent);
                    legMessage.UTheo.Mantissa = EncodeMantissa(leg.UTheo, legMessage.UTheo.Exponent);

                    EncodeLegContraFields_OrderUpdated(legMessage, leg);
                }
            }

            EncodeOrderContraFields_OrderUpdated(message, order);

            message.SetLastExchange((order.LastExchange) ?? "");
            message.SetExchanges((order.Exchanges) ?? "");
            message.SetReason((order.Reason) ?? "");
            message.SetOrderID((order.OrderID) ?? "");
            message.SetTagger(order.Tagger ?? "");
            message.SetTaggedMessage(order.TaggedMessage ?? "");

            return message.Limit - offset;
        }

        private static void EncodeLegContraFields_OrderUpdated(OrderUpdated.NoLegsGroup legMessage, IComplexOrderLeg leg)
        {
            var legCaps = leg.ContraCapacities;
            var legCapsGroup = legMessage.NoLegContraCapacitiesCount(legCaps?.Count ?? 0);
            if (legCaps != null)
            {
                for (int j = 0; j < legCaps.Count; j++)
                {
                    legCapsGroup.Next().Value = (byte)legCaps[j];
                }
            }

            var legBrokers = leg.ContraBrokerNames;
            var legBrokersGroup = legMessage.NoLegContraBrokerNamesCount(legBrokers?.Count ?? 0);
            if (legBrokers != null)
            {
                for (int j = 0; j < legBrokers.Count; j++)
                {
                    legBrokersGroup.Next().Value = (byte)legBrokers[j];
                }
            }

            var legCmtas = leg.ContraCmtas;
            var legCmtasGroup = legMessage.NoLegContraCmtasCount(legCmtas?.Count ?? 0);
            if (legCmtas != null)
            {
                for (int j = 0; j < legCmtas.Count; j++)
                {
                    legCmtasGroup.Next().Value = (byte)legCmtas[j];
                }
            }

            var legTraders = leg.ContraTraders;
            var legTradersGroup = legMessage.NoLegContraTradersCount(legTraders?.Count ?? 0);
            if (legTraders != null)
            {
                for (int j = 0; j < legTraders.Count; j++)
                {
                    legTradersGroup.Next().Value = (byte)legTraders[j];
                }
            }
        }

        private static void EncodeOrderContraFields_OrderUpdated(OrderUpdated message, IOrder order)
        {
            var orderCaps = order.ContraCapacities;
            var orderCapsGroup = message.NoContraCapacitiesCount(orderCaps?.Count ?? 0);
            if (orderCaps != null)
            {
                for (int i = 0; i < orderCaps.Count; i++)
                {
                    orderCapsGroup.Next().Value = (byte)orderCaps[i];
                }
            }

            var orderBrokers = order.ContraBrokerNames;
            var orderBrokersGroup = message.NoContraBrokerNamesCount(orderBrokers?.Count ?? 0);
            if (orderBrokers != null)
            {
                for (int i = 0; i < orderBrokers.Count; i++)
                {
                    orderBrokersGroup.Next().Value = (byte)orderBrokers[i];
                }
            }

            var orderCmtas = order.ContraCmtas;
            var orderCmtasGroup = message.NoContraCmtasCount(orderCmtas?.Count ?? 0);
            if (orderCmtas != null)
            {
                for (int i = 0; i < orderCmtas.Count; i++)
                {
                    orderCmtasGroup.Next().Value = (byte)orderCmtas[i];
                }
            }

            var orderTraders = order.ContraTraders;
            var orderTradersGroup = message.NoContraTradersCount(orderTraders?.Count ?? 0);
            if (orderTraders != null)
            {
                for (int i = 0; i < orderTraders.Count; i++)
                {
                    orderTradersGroup.Next().Value = (byte)orderTraders[i];
                }
            }
        }

        public int EncodeOrderIndicatorUpdate(DirectBuffer directBuffer, int offset, IOrder order)
        {
            string permId = order.PermID ?? "";
            string tagger = order.Tagger ?? "";
            string taggedMessage = order.TaggedMessage ?? "";
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderIndicatorsUpdate message = new OrderIndicatorsUpdate();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderIndicatorsUpdate.BlockLength;
            messageHeader.SchemaId = OrderIndicatorsUpdate.SchemaId;
            messageHeader.TemplateId = OrderIndicatorsUpdate.TemplateId;
            messageHeader.Version = OrderIndicatorsUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermID(permId);
            message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;
            message.IsFirstFill = order.IsFirstFill ? BooleanEnum.True : BooleanEnum.False;
            message.IsCitadel = order.IsCitadel ? BooleanEnum.True : BooleanEnum.False;
            message.FirstEdgeAcquired = order.FirstEdgeAcquired ? BooleanEnum.True : BooleanEnum.False;
            switch (order.CitadelSide)
            {
                case null:
                    message.CitadelSide = AggressorSide.NoAggressor;
                    break;
                case Side.Buy:
                    message.CitadelSide = AggressorSide.Buy;
                    break;
                case Side.Sell:
                    message.CitadelSide = AggressorSide.Sell;
                    break;
            }
            switch (order.SpreadPositionEffect)
            {
                case Data.Enums.PositionEffect.Open:
                    message.SpreadPositionEffect = PositionEffect.Open;
                    break;
                case Data.Enums.PositionEffect.Close:
                    message.SpreadPositionEffect = PositionEffect.Close;
                    break;
                case null:
                    message.SpreadPositionEffect = PositionEffect.AUTO;
                    break;
            }
            message.FirstEdge.Mantissa = EncodeMantissa(order.FirstEdge, message.FirstEdge.Exponent);

            message.LastEdge.Mantissa = EncodeMantissa(order.LastEdge, message.LastEdge.Exponent);
            message.DeltaAdjLastEdge.Mantissa = EncodeMantissa(order.DeltaAdjLastEdge, message.DeltaAdjLastEdge.Exponent);
            message.DeltaAdjLastEdgeNotional.Mantissa = EncodeMantissa(order.DeltaAdjLastEdgeNotional, message.DeltaAdjLastEdgeNotional.Exponent);
            message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent);

            message.DeltaAdjChange.Mantissa = EncodeMantissa(order.DeltaAdjChange, message.DeltaAdjChange.Exponent);
            message.DeltaAdjChangeNotional.Mantissa = EncodeMantissa(order.DeltaAdjChangeNotional, message.DeltaAdjChangeNotional.Exponent);
            message.LoopInitLatency.Mantissa = EncodeMantissa(order.LoopInitLatency, message.LoopInitLatency.Exponent);

            message.IsTagged = order.IsTagged ? BooleanEnum.True : BooleanEnum.False;

            message.EdgeGiveUp.Mantissa = EncodeMantissa(order.EdgeGiveUp, message.EdgeGiveUp.Exponent);
            message.CloseSubs.Mantissa = EncodeMantissa(order.CloseSubs, message.CloseSubs.Exponent);

            message.SetTagger(tagger);
            message.SetTaggedMessage(taggedMessage);


            return message.Limit - offset;
        }

        public int EncodeOrderTagUpdate(DirectBuffer directBuffer, int offset, IOrder order)
        {

            string comment = order.Comment ?? "";
            string reason = order.Reason ?? "";
            string automationType = order.AutomationType ?? "";
            string tag = order.Tag ?? "";
            string trader = order.Trader ?? "";


            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderTagUpdate message = new OrderTagUpdate();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderTagUpdate.BlockLength;
            messageHeader.SchemaId = OrderTagUpdate.SchemaId;
            messageHeader.TemplateId = OrderTagUpdate.TemplateId;
            messageHeader.Version = OrderTagUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermID(order.PermID ?? "");
            message.TagEdge.Mantissa = EncodeMantissa(order.TagEdge, message.TagEdge.Exponent);
            message.TagMid.Mantissa = EncodeMantissa(order.TagMid, message.TagMid.Exponent);
            message.TagBid.Mantissa = EncodeMantissa(order.TagBid, message.TagBid.Exponent);
            message.TagAsk.Mantissa = EncodeMantissa(order.TagAsk, message.TagAsk.Exponent);
            message.TagUnderBid.Mantissa = EncodeMantissa(order.TagUnderBid, message.TagUnderBid.Exponent);
            message.TagUnderAsk.Mantissa = EncodeMantissa(order.TagUnderAsk, message.TagUnderAsk.Exponent);
            message.TagTheo.Mantissa = EncodeMantissa(order.TagTheo, message.TagTheo.Exponent);
            message.TagVolaV0.Mantissa = EncodeMantissa(order.TagVolaV0, message.TagVolaV0.Exponent);
            message.TagVolaV1.Mantissa = EncodeMantissa(order.TagVolaV1, message.TagVolaV1.Exponent);
            message.TagVolaV2.Mantissa = EncodeMantissa(order.TagVolaV2, message.TagVolaV2.Exponent);
            message.TagEma.Mantissa = EncodeMantissa(order.TagEma, message.TagEma.Exponent);
            message.TagVolaIv = order.VolaIv;
            message.TheoBid.Mantissa = EncodeMantissa(order.TheoBid, message.TheoBid.Exponent);
            message.TheoAsk.Mantissa = EncodeMantissa(order.TheoAsk, message.TheoAsk.Exponent);

            message.SharedId = order.SharedId;
            message.Sequence = order.Sequence;
            message.TypeId = (ushort)order.TypeId;
            message.SubTypeCode = (ushort)order.SubTypeId;
            message.SubTypeSequence = order.SubTypeSequence;
            var subType = order.SubType;
            message.SubType = subType == null ? OrderTagUpdate.SubTypeNullValue : (byte)subType;

            message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedEdge.Mantissa = EncodeMantissa(order.EdgeScanFeedEdge, message.EdgeScanFeedEdge.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedTimespan.Mantissa = EncodeMantissa(order.EdgeScanFeedTimespan, message.EdgeScanFeedTimespan.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedBuyPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedBuyPrice, message.EdgeScanFeedBuyPrice.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedSellPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedSellPrice, message.EdgeScanFeedSellPrice.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedBuyQty = order.EdgeScanFeedBuyQty;
            message.EdgeScanFeedSellQty = order.EdgeScanFeedSellQty;
            message.EdgeScanFeedBuyTime = order.EdgeScanFeedBuyTime.ToUnixEpoch();
            message.EdgeScanFeedSellTime = order.EdgeScanFeedSellTime.ToUnixEpoch();
            message.EdgeScanFeedRespondLatency.Mantissa = EncodeMantissa(order.EdgeScanFeedRespondLatency, message.EdgeScanFeedRespondLatency.Exponent, DOUBLENULL2.MantissaNullValue);
            message.EdgeScanFeedConditionCode = (byte)order.EdgeScanFeedConditionCode;

            message.TradeToNewTime.Mantissa = EncodeMantissa(order.TradeToNewTime, message.TradeToNewTime.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OmsBidPercentOfFillPrice.Mantissa = EncodeMantissa(order.OmsBidPercentOfFillPrice, message.OmsBidPercentOfFillPrice.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OrderSource = (Generated.OrderSource)order.OrderSource;
            message.EdgeToTheo.Mantissa = EncodeMantissa(order.EdgeToTheo, message.EdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TagEdgeToTheo.Mantissa = EncodeMantissa(order.TagEdgeToTheo, message.TagEdgeToTheo.Exponent);
            message.TagEdgeToEma.Mantissa = EncodeMantissa(order.TagEdgeToEma, message.TagEdgeToEma.Exponent);
            message.TagEdgeToVolaV0.Mantissa = EncodeMantissa(order.TagEdgeToVolaV0, message.TagEdgeToVolaV0.Exponent);
            message.TagEdgeToVolaV1.Mantissa = EncodeMantissa(order.TagEdgeToVolaV1, message.TagEdgeToVolaV1.Exponent);
            message.TagEdgeToVolaV2.Mantissa = EncodeMantissa(order.TagEdgeToVolaV2, message.TagEdgeToVolaV2.Exponent);
            message.OrderEdgeToTheo.Mantissa = EncodeMantissa(order.OrderEdgeToTheo, message.OrderEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
            message.InitialEdge.Mantissa = EncodeMantissa(order.InitialEdge, message.InitialEdge.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OpenEdge.Mantissa = EncodeMantissa(order.OpenEdge, message.OpenEdge.Exponent, DOUBLENULL2.MantissaNullValue);
            message.CloseEdge.Mantissa = EncodeMantissa(order.CloseEdge, message.CloseEdge.Exponent, DOUBLENULL2.MantissaNullValue);
            message.CostOfHedging.Mantissa = EncodeMantissa(order.CostOfHedging, message.CostOfHedging.Exponent, DOUBLENULL2.MantissaNullValue);

            message.EdgeType = (byte)order.EdgeType;
            message.DigBid.Mantissa = EncodeMantissa(order.DigBid, message.DigBid.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DigAsk.Mantissa = EncodeMantissa(order.DigAsk, message.DigAsk.Exponent, DOUBLENULL2.MantissaNullValue);
            message.WeightedVega.Mantissa = EncodeMantissa(order.WeightedVega, message.WeightedVega.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DigBidSize = order.DigBidSize;
            message.DigAskSize = order.DigAskSize;

            message.SetComment(order.Comment ?? "");
            message.SetReason(order.Reason ?? "");
            message.SetAutomationType(order.AutomationType ?? "");
            message.SetTag(order.Tag ?? "");
            message.SetTrader(order.Trader ?? "");


            return message.Limit - offset;
        }

        public int EncodePortfolioAdded(DirectBuffer directBuffer, int offset, IPortfolio portfolio)
        {
            string portfolioName = (portfolio.Name) ?? "";
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PortfolioAddedMessage message = new PortfolioAddedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PortfolioAddedMessage.BlockLength;
            messageHeader.SchemaId = PortfolioAddedMessage.SchemaId;
            messageHeader.TemplateId = PortfolioAddedMessage.TemplateId;
            messageHeader.Version = PortfolioAddedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.PortfolioId = portfolio.Id;
            message.PortfolioType = (PortfolioType)portfolio.PortfolioType;
            message.PortfolioDate = portfolio.PortfolioDate.ToUnixEpoch();
            message.SetPortfolioName(portfolioName);


            return message.Limit - offset;
        }

        public int EncodePositionAdded(DirectBuffer directBuffer, int offset, IPortfolio portfolio, IPosition position)
        {
            string portfolioName = portfolio.Name ?? "";
            string positionName = position.Name ?? "";
            string positionSymbol = position.Symbol ?? "";
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PositionAddedMessage message = new PositionAddedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PositionAddedMessage.BlockLength;
            messageHeader.SchemaId = PositionAddedMessage.SchemaId;
            messageHeader.TemplateId = PositionAddedMessage.TemplateId;
            messageHeader.Version = PositionAddedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.PortfolioId = portfolio.Id;
            message.PortfolioType = (PortfolioType)portfolio.PortfolioType;
            message.PortfolioDate = portfolio.PortfolioDate.ToUnixEpoch();
            message.ParentPositionId = position.ParentPositionId;
            message.PositionId = position.Id;
            message.PositionType = (PositionType)position.PositionType;
            message.PositionDate = ToUnixEpoch(position.PositionDate);

            message.SetPortfolioName(portfolioName);
            message.SetPositionName(positionName);
            message.SetPositionSymbol(positionSymbol);


            return message.Limit - offset;
        }

        public int EncodePositionUpdated(DirectBuffer directBuffer, int offset, IPortfolio portfolio, IPosition[] positions, bool isReplay)
        {
            int instanceFieldLen = 0;
            Dictionary<IPosition, string> positionToLastInstanceMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToLastTraderMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToAccountMap = new Dictionary<IPosition, string>();
            var count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                IPosition position = positions[i];
                if (position == null)
                {
                    break;
                }

                count++;
                string lastInstance = position.LastInstance ?? "";
                positionToLastInstanceMap[position] = lastInstance;
                instanceFieldLen += GetBufferEstimate(lastInstance);

                string lastTrader = position.LastTrader ?? "";
                positionToLastTraderMap[position] = lastTrader;
                instanceFieldLen += GetBufferEstimate(lastTrader);

                string account = position.Account ?? "";
                positionToAccountMap[position] = account;
                instanceFieldLen += GetBufferEstimate(account);
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PortfolioUpdateMessage message = new PortfolioUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PortfolioUpdateMessage.BlockLength;
            messageHeader.SchemaId = PortfolioUpdateMessage.SchemaId;
            messageHeader.TemplateId = PortfolioUpdateMessage.TemplateId;
            messageHeader.Version = PortfolioUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.PortfolioId = (ushort)portfolio.Id;
            message.PortfolioType = (PortfolioType)portfolio.PortfolioType;
            message.TotalSubmissions = portfolio.TotalSubmissions;
            message.TotalSingleLegSubmissions = portfolio.TotalSingleLegSubmissions;
            message.TotalSpreadSubmissions = portfolio.TotalSpreadSubmissions;
            message.TotalSingleFills = (ushort)portfolio.TotalSingleFills;
            message.TotalSpreadFills = (ushort)portfolio.TotalSpreadFills;
            message.UniqueSubmissions = portfolio.UniqueSubmissions;
            message.UniqueSpreadSubmissions = portfolio.UniqueSpreadSubmissions;
            message.TotalFills = (ushort)portfolio.TotalFills;
            message.UniqueFills = (ushort)portfolio.UniqueFills;
            message.UniqueSpreadFills = (ushort)portfolio.UniqueSpreadFills;
            message.StockContracts = portfolio.StockContracts;
            message.TotalContracts = portfolio.TotalContracts;
            message.UniqueContracts = portfolio.UniqueContracts;
            message.UniqueSpreadContracts = portfolio.UniqueSpreadContracts;
            message.NetQty = (short)portfolio.NetQty;
            message.ShortQty = (short)portfolio.ShortQty;
            message.LongQty = (short)portfolio.LongQty;

            message.FillRate.Mantissa = EncodeMantissa(portfolio.FillRate, message.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.OrderFillRate.Mantissa = EncodeMantissa(portfolio.OrderFillRate, message.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.IbOrderFillRate.Mantissa = EncodeMantissa(portfolio.IbOrderFillRate, message.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            message.LowestRealizedPnl.Mantissa = EncodeMantissa(portfolio.LowestRealizedPnl, message.LowestRealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.HighestRealizedPnl.Mantissa = EncodeMantissa(portfolio.HighestRealizedPnl, message.HighestRealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.RealizedPnl.Mantissa = EncodeMantissa(portfolio.RealizedPnl, message.RealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.LowestAdjustedPnl.Mantissa = EncodeMantissa(portfolio.LowestAdjustedPnl, message.LowestAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.HighestAdjustedPnl.Mantissa = EncodeMantissa(portfolio.HighestAdjustedPnl, message.HighestAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.AdjustedPnl.Mantissa = EncodeMantissa(portfolio.AdjustedPnl, message.AdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SingleLegAdjustedPnl.Mantissa = EncodeMantissa(portfolio.SingleLegAdjustedPnl, message.SingleLegAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SpreadAdjustedPnl.Mantissa = EncodeMantissa(portfolio.SpreadAdjustedPnl, message.SpreadAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.UnrealizedPnl.Mantissa = EncodeMantissa(portfolio.UnrealizedPnl, message.UnrealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.NetDelta.Mantissa = EncodeMantissa(portfolio.NetDelta, message.NetDelta.Exponent, DOUBLENULL2.MantissaNullValue);

            message.MaxResubmitEstimate = (ushort)portfolio.MaxResubmitEstimate;
            message.MaxResubmitForFill = (ushort)portfolio.MaxResubmitForFill;
            message.AvgResubmitEstimate = (ushort)portfolio.AvgResubmitEstimate;
            message.AvgResubmitForFill = (ushort)portfolio.AvgResubmitForFill;

            message.DeltaAdjustedBurn.Mantissa = EncodeMantissa(portfolio.DeltaAdjustedBurn, message.DeltaAdjustedBurn.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DeltaAdjustedHelp.Mantissa = EncodeMantissa(portfolio.DeltaAdjustedHelp, message.DeltaAdjustedHelp.Exponent, DOUBLENULL2.MantissaNullValue);
            message.HighestOpenNotional.Mantissa = EncodeMantissa(portfolio.HighestOpenNotional, message.HighestOpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TotalOpenNotional.Mantissa = EncodeMantissa(portfolio.TotalOpenNotional, message.TotalOpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);

            message.TotalOutOfMarketOrders = portfolio.TotalOutOfMarketOrders;
            message.TotalOutOfMarketFills = (ushort)portfolio.TotalOutOfMarketFills;

            message.SubmissionRatePerSec = (ushort)portfolio.SubmissionRatePerSec;
            message.MaxOrdersPerSec = (ushort)portfolio.MaxOrdersPerSec;

            message.WinnerTrades = (ushort)portfolio.WinnerTrades;
            message.LoserTrades = (ushort)portfolio.LoserTrades;
            message.SizeWinnerTrades = (ushort)portfolio.SizeWinnerTrades;
            message.SizeLoserTrades = (ushort)portfolio.SizeLoserTrades;
            message.AvgCloseSubs = (ushort)portfolio.AvgCloseSubs;

            message.IntroducingBrokerFee.Mantissa = EncodeMantissa(portfolio.IntroducingBrokerFee, message.IntroducingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.ExecutingBrokerFee.Mantissa = EncodeMantissa(portfolio.ExecutingBrokerFee, message.ExecutingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.ExchangeFee.Mantissa = EncodeMantissa(portfolio.ExchangeFee, message.ExchangeFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OrfFee.Mantissa = EncodeMantissa(portfolio.OrfFee, message.OrfFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SecFee.Mantissa = EncodeMantissa(portfolio.SecFee, message.SecFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TotalFees.Mantissa = EncodeMantissa(portfolio.TotalFees, message.TotalFees.Exponent, DOUBLENULL2.MantissaNullValue);

            message.AvgOpenSubsCount.Mantissa = EncodeMantissa((ushort)portfolio.AvgOpenSubsCount, message.AvgOpenSubsCount.Exponent, DOUBLENULL2.MantissaNullValue);
            message.AvgSubsBetweenFillsCount.Mantissa = EncodeMantissa((ushort)portfolio.AvgSubsBetweenFillsCount, message.AvgSubsBetweenFillsCount.Exponent, DOUBLENULL2.MantissaNullValue);

            message.IsReplay = isReplay ? BooleanEnum.True : BooleanEnum.False;

            message.GroupSubmissionsAvg = portfolio.GroupSubmissionsAvg;
            message.GroupAvgFillRate.Mantissa = EncodeMantissa(portfolio.GroupAvgFillRate, message.GroupAvgFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            PortfolioUpdateMessage.PositionsGroup positionMessage = message.PositionsCount(count);
            for (int i = 0; i < count; i++)
            {
                positionMessage.Next();
                IPosition position = positions[i];
                positionMessage.ParentPositionId = position.ParentPositionId;
                positionMessage.PositionId = position.Id;
                positionMessage.PositionType = (PositionType)position.PositionType;
                positionMessage.NetQty = (short)position.NetQty;
                positionMessage.RealizedPnl.Mantissa = EncodeMantissa(position.RealizedPnl, positionMessage.RealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.AdjustedPnl.Mantissa = EncodeMantissa(position.AdjustedPnl, positionMessage.AdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.UnrealizedPnl.Mantissa = EncodeMantissa(position.UnrealizedPnl, positionMessage.UnrealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.NetDelta.Mantissa = EncodeMantissa(position.NetDelta, positionMessage.NetDelta.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestSellPrice.Mantissa = EncodeMantissa(position.BestSellPrice, positionMessage.BestSellPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestSellPriceUnderMid.Mantissa = EncodeMantissa(position.BestSellPriceUnderMid, positionMessage.BestSellPriceUnderMid.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestBuyPrice.Mantissa = EncodeMantissa(position.BestBuyPrice, positionMessage.BestBuyPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestBuyPriceUnderMid.Mantissa = EncodeMantissa(position.BestBuyPriceUnderMid, positionMessage.BestBuyPriceUnderMid.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.TotalSubmissions = position.TotalSubmissions;
                positionMessage.TotalSingleLegSubmissions = position.TotalSingleLegSubmissions;
                positionMessage.TotalSpreadSubmissions = position.TotalSpreadSubmissions;
                positionMessage.TotalSingleFills = (ushort)position.TotalSingleFills;
                positionMessage.TotalSpreadFills = (ushort)position.TotalSpreadFills;
                positionMessage.UniqueSubmissions = position.UniqueSubmissions;
                positionMessage.TotalFills = (ushort)position.TotalFills;
                positionMessage.UniqueFills = (ushort)position.UniqueFills;
                positionMessage.TotalContracts = position.TotalContracts;
                positionMessage.UniqueContracts = position.UniqueContracts;

                positionMessage.FillRate.Mantissa = EncodeMantissa(position.FillRate, positionMessage.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                positionMessage.OrderFillRate.Mantissa = EncodeMantissa(position.OrderFillRate, positionMessage.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                positionMessage.IbOrderFillRate.Mantissa = EncodeMantissa(position.IbOrderFillRate, positionMessage.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

                positionMessage.OpenPositionAveragePrice.Mantissa = EncodeMantissa(position.OpenPositionAveragePrice, positionMessage.OpenPositionAveragePrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.OpenPositionFillUnderPrice.Mantissa = EncodeMantissa(position.OpenPositionFillUnderPrice, positionMessage.OpenPositionFillUnderPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastTradeTime = ToUnixEpoch(position.LastTradeTime);
                positionMessage.LastEdge.Mantissa = EncodeMantissa(position.LastEdge, positionMessage.LastEdge.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastBuyEdge.Mantissa = EncodeMantissa(position.LastBuyEdge, positionMessage.LastBuyEdge.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellEdge.Mantissa = EncodeMantissa(position.LastSellEdge, positionMessage.LastSellEdge.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastBuyEdgeToTheo.Mantissa = EncodeMantissa(position.LastBuyEdgeToTheo, positionMessage.LastBuyEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellEdgeToTheo.Mantissa = EncodeMantissa(position.LastSellEdgeToTheo, positionMessage.LastSellEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastBuyFillEdgeToTheo.Mantissa = EncodeMantissa(position.LastBuyFillEdgeToTheo, positionMessage.LastBuyFillEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellFillEdgeToTheo.Mantissa = EncodeMantissa(position.LastSellFillEdgeToTheo, positionMessage.LastSellFillEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastBuyAttemptEdgeToTheo.Mantissa = EncodeMantissa(position.LastBuyAttemptEdgeToTheo, positionMessage.LastBuyAttemptEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellAttemptEdgeToTheo.Mantissa = EncodeMantissa(position.LastSellAttemptEdgeToTheo, positionMessage.LastSellAttemptEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.LastPermBuyFillEdgeToTheo.Mantissa = EncodeMantissa(position.LastPermBuyFillEdgeToTheo, positionMessage.LastPermBuyFillEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastPermSellFillEdgeToTheo.Mantissa = EncodeMantissa(position.LastPermSellFillEdgeToTheo, positionMessage.LastPermSellFillEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastPermBuyAttemptEdgeToTheo.Mantissa = EncodeMantissa(position.LastPermBuyAttemptEdgeToTheo, positionMessage.LastPermBuyAttemptEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastPermSellAttemptEdgeToTheo.Mantissa = EncodeMantissa(position.LastPermSellAttemptEdgeToTheo, positionMessage.LastPermSellAttemptEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.BestBuyEdgeToTheo.Mantissa = EncodeMantissa(position.BestBuyEdgeToTheo, positionMessage.BestBuyEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.WorstBuyEdgeToTheo.Mantissa = EncodeMantissa(position.WorstBuyEdgeToTheo, positionMessage.WorstBuyEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestSellEdgeToTheo.Mantissa = EncodeMantissa(position.BestSellEdgeToTheo, positionMessage.BestSellEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.WorstSellEdgeToTheo.Mantissa = EncodeMantissa(position.WorstSellEdgeToTheo, positionMessage.WorstSellEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.OpenNotional.Mantissa = EncodeMantissa(position.OpenNotional, positionMessage.OpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.MaxResubmitEstimate = (ushort)position.MaxResubmitEstimate;
                positionMessage.MaxResubmitForFill = (ushort)position.MaxResubmitForFill;
                positionMessage.AvgResubmitEstimate = (ushort)position.AvgResubmitEstimate;
                positionMessage.AvgResubmitForFill = (ushort)position.AvgResubmitForFill;

                positionMessage.FirstEdge.Mantissa = EncodeMantissa(position.FirstEdge, positionMessage.FirstEdge.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.TotalOutOfMarketOrders = position.TotalOutOfMarketOrders;
                positionMessage.TotalOutOfMarketFills = (ushort)position.TotalOutOfMarketFills;

                positionMessage.HardSide = position.HardSide.HasValue ? (Generated.Side)position.HardSide : Generated.Side.NULL_VALUE;
                positionMessage.HardSideDesignationTime = position.HardSideDesignationTime.ToUnixEpoch();
                positionMessage.HardSideBuyGiveUp.Mantissa = EncodeMantissa(position.HardSideBuyGiveUp, positionMessage.HardSideBuyGiveUp.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.HardSideSellGiveUp.Mantissa = EncodeMantissa(position.HardSideSellGiveUp, positionMessage.HardSideSellGiveUp.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.SubmissionRatePerSec = (ushort)position.SubmissionRatePerSec;
                positionMessage.MaxOrdersPerSec = (ushort)position.MaxOrdersPerSec;

                positionMessage.WinnerTrades = (ushort)position.WinnerTrades;
                positionMessage.LoserTrades = (ushort)position.LoserTrades;
                positionMessage.SizeWinnerTrades = (ushort)position.SizeWinnerTrades;
                positionMessage.SizeLoserTrades = (ushort)position.SizeLoserTrades;
                positionMessage.AvgCloseSubs = (ushort)position.AvgCloseSubs;
                positionMessage.OpenSubsCount = (ushort)position.OpenSubsCount;
                positionMessage.SubsBetweenFillsCount = (ushort)position.SubsBetweenFillsCount;

                positionMessage.IntroducingBrokerFee.Mantissa = EncodeMantissa(position.IntroducingBrokerFee, positionMessage.IntroducingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.ExecutingBrokerFee.Mantissa = EncodeMantissa(position.ExecutingBrokerFee, positionMessage.ExecutingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.ExchangeFee.Mantissa = EncodeMantissa(position.ExchangeFee, positionMessage.ExchangeFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.OrfFee.Mantissa = EncodeMantissa(position.OrfFee, positionMessage.OrfFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.SecFee.Mantissa = EncodeMantissa(position.SecFee, positionMessage.SecFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.TotalFees.Mantissa = EncodeMantissa(position.TotalFees, positionMessage.TotalFees.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastTradeSide = position.LastTradeSide.HasValue ? (Generated.Side)position.LastTradeSide : Generated.Side.NULL_VALUE;

                positionMessage.LastBuyAttempt.Mantissa = EncodeMantissa(position.LastBuyAttempt, positionMessage.LastBuyAttempt.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastBuyAttemptUnderlying.Mantissa = EncodeMantissa(position.LastBuyAttemptUnderlying, positionMessage.LastBuyAttemptUnderlying.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellAttempt.Mantissa = EncodeMantissa(position.LastSellAttempt, positionMessage.LastSellAttempt.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellAttemptUnderlying.Mantissa = EncodeMantissa(position.LastSellAttemptUnderlying, positionMessage.LastSellAttemptUnderlying.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.RawNetQty = (short)position.RawNetQty;

                positionMessage.LastInstance = position.LastInstanceId;
                positionMessage.LastTrader = position.LastTraderId;
                positionMessage.Account = position.AccountId;

                positionMessage.SingleLegAdjustedPnl.Mantissa = EncodeMantissa(position.SingleLegAdjustedPnl, positionMessage.SingleLegAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.SpreadAdjustedPnl.Mantissa = EncodeMantissa(position.SpreadAdjustedPnl, positionMessage.SpreadAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            }


            return message.Limit - offset;
        }

        public int EncodePositionUpdatedSlim(DirectBuffer directBuffer, int offset, IPortfolio portfolio, IPosition[] positions, bool isReplay)
        {
            var count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                IPosition position = positions[i];
                if (position == null)
                {
                    break;
                }
                count++;
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PortfolioUpdateMessageSlim message = new PortfolioUpdateMessageSlim();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PortfolioUpdateMessageSlim.BlockLength;
            messageHeader.SchemaId = PortfolioUpdateMessageSlim.SchemaId;
            messageHeader.TemplateId = PortfolioUpdateMessageSlim.TemplateId;
            messageHeader.Version = PortfolioUpdateMessageSlim.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.PortfolioId = (ushort)portfolio.Id;
            message.PortfolioType = (PortfolioType)portfolio.PortfolioType;
            message.TotalSubmissions = portfolio.TotalSubmissions;
            message.TotalSingleLegSubmissions = portfolio.TotalSingleLegSubmissions;
            message.TotalSpreadSubmissions = portfolio.TotalSpreadSubmissions;
            message.TotalSingleFills = (ushort)portfolio.TotalSingleFills;
            message.TotalSpreadFills = (ushort)portfolio.TotalSpreadFills;
            message.UniqueSubmissions = portfolio.UniqueSubmissions;
            message.UniqueSpreadSubmissions = portfolio.UniqueSpreadSubmissions;
            message.TotalFills = (ushort)portfolio.TotalFills;
            message.UniqueFills = (ushort)portfolio.UniqueFills;
            message.UniqueSpreadFills = (ushort)portfolio.UniqueSpreadFills;
            message.StockContracts = portfolio.StockContracts;
            message.TotalContracts = portfolio.TotalContracts;
            message.UniqueContracts = portfolio.UniqueContracts;
            message.UniqueSpreadContracts = portfolio.UniqueSpreadContracts;
            message.NetQty = (short)portfolio.NetQty;
            message.ShortQty = (short)portfolio.ShortQty;
            message.LongQty = (short)portfolio.LongQty;

            message.FillRate.Mantissa = EncodeMantissa(portfolio.FillRate, message.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.OrderFillRate.Mantissa = EncodeMantissa(portfolio.OrderFillRate, message.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.IbOrderFillRate.Mantissa = EncodeMantissa(portfolio.IbOrderFillRate, message.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            message.RealizedPnl.Mantissa = EncodeMantissa(portfolio.RealizedPnl, message.RealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.AdjustedPnl.Mantissa = EncodeMantissa(portfolio.AdjustedPnl, message.AdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SingleLegAdjustedPnl.Mantissa = EncodeMantissa(portfolio.SingleLegAdjustedPnl, message.SingleLegAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SpreadAdjustedPnl.Mantissa = EncodeMantissa(portfolio.SpreadAdjustedPnl, message.SpreadAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.UnrealizedPnl.Mantissa = EncodeMantissa(portfolio.UnrealizedPnl, message.UnrealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            message.NetDelta.Mantissa = EncodeMantissa(portfolio.NetDelta, message.NetDelta.Exponent, DOUBLENULL2.MantissaNullValue);

            message.MaxResubmitEstimate = (ushort)portfolio.MaxResubmitEstimate;
            message.MaxResubmitForFill = (ushort)portfolio.MaxResubmitForFill;
            message.AvgResubmitEstimate = (ushort)portfolio.AvgResubmitEstimate;
            message.AvgResubmitForFill = (ushort)portfolio.AvgResubmitForFill;

            message.DeltaAdjustedBurn.Mantissa = EncodeMantissa(portfolio.DeltaAdjustedBurn, message.DeltaAdjustedBurn.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DeltaAdjustedHelp.Mantissa = EncodeMantissa(portfolio.DeltaAdjustedHelp, message.DeltaAdjustedHelp.Exponent, DOUBLENULL2.MantissaNullValue);
            message.HighestOpenNotional.Mantissa = EncodeMantissa(portfolio.HighestOpenNotional, message.HighestOpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TotalOpenNotional.Mantissa = EncodeMantissa(portfolio.TotalOpenNotional, message.TotalOpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);

            message.TotalOutOfMarketOrders = portfolio.TotalOutOfMarketOrders;
            message.TotalOutOfMarketFills = (ushort)portfolio.TotalOutOfMarketFills;

            message.SubmissionRatePerSec = (ushort)portfolio.SubmissionRatePerSec;
            message.MaxOrdersPerSec = (ushort)portfolio.MaxOrdersPerSec;

            message.WinnerTrades = (ushort)portfolio.WinnerTrades;
            message.LoserTrades = (ushort)portfolio.LoserTrades;
            message.SizeWinnerTrades = (ushort)portfolio.SizeWinnerTrades;
            message.SizeLoserTrades = (ushort)portfolio.SizeLoserTrades;
            message.AvgCloseSubs = (ushort)portfolio.AvgCloseSubs;

            message.IntroducingBrokerFee.Mantissa = EncodeMantissa(portfolio.IntroducingBrokerFee, message.IntroducingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.ExecutingBrokerFee.Mantissa = EncodeMantissa(portfolio.ExecutingBrokerFee, message.ExecutingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.ExchangeFee.Mantissa = EncodeMantissa(portfolio.ExchangeFee, message.ExchangeFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OrfFee.Mantissa = EncodeMantissa(portfolio.OrfFee, message.OrfFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SecFee.Mantissa = EncodeMantissa(portfolio.SecFee, message.SecFee.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TotalFees.Mantissa = EncodeMantissa(portfolio.TotalFees, message.TotalFees.Exponent, DOUBLENULL2.MantissaNullValue);

            message.AvgOpenSubsCount.Mantissa = EncodeMantissa((ushort)portfolio.AvgOpenSubsCount, message.AvgOpenSubsCount.Exponent, DOUBLENULL2.MantissaNullValue);
            message.AvgSubsBetweenFillsCount.Mantissa = EncodeMantissa((ushort)portfolio.AvgSubsBetweenFillsCount, message.AvgSubsBetweenFillsCount.Exponent, DOUBLENULL2.MantissaNullValue);

            message.IsReplay = isReplay ? BooleanEnum.True : BooleanEnum.False;

            message.GroupSubmissionsAvg = portfolio.GroupSubmissionsAvg;
            message.GroupAvgFillRate.Mantissa = EncodeMantissa(portfolio.GroupAvgFillRate, message.GroupAvgFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            PortfolioUpdateMessageSlim.PositionsGroup positionMessage = message.PositionsCount(count);
            for (int i = 0; i < count; i++)
            {
                positionMessage.Next();
                IPosition position = positions[i];
                positionMessage.ParentPositionId = position.ParentPositionId;
                positionMessage.PositionId = position.Id;
                positionMessage.PositionType = (PositionType)position.PositionType;
                positionMessage.NetQty = (short)position.NetQty;
                positionMessage.RealizedPnl.Mantissa = EncodeMantissa(position.RealizedPnl, positionMessage.RealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.AdjustedPnl.Mantissa = EncodeMantissa(position.AdjustedPnl, positionMessage.AdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.UnrealizedPnl.Mantissa = EncodeMantissa(position.UnrealizedPnl, positionMessage.UnrealizedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.NetDelta.Mantissa = EncodeMantissa(position.NetDelta, positionMessage.NetDelta.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.TotalSubmissions = position.TotalSubmissions;
                positionMessage.TotalFills = (ushort)position.TotalFills;
                positionMessage.OpenPositionAveragePrice.Mantissa = EncodeMantissa(position.OpenPositionAveragePrice, positionMessage.OpenPositionAveragePrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.OpenPositionFillUnderPrice.Mantissa = EncodeMantissa(position.OpenPositionFillUnderPrice, positionMessage.OpenPositionFillUnderPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastTradeTime = ToUnixEpoch(position.LastTradeTime);

                positionMessage.OpenNotional.Mantissa = EncodeMantissa(position.OpenNotional, positionMessage.OpenNotional.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.MaxResubmitEstimate = (ushort)position.MaxResubmitEstimate;
                positionMessage.MaxResubmitForFill = (ushort)position.MaxResubmitForFill;
                positionMessage.AvgResubmitEstimate = (ushort)position.AvgResubmitEstimate;
                positionMessage.AvgResubmitForFill = (ushort)position.AvgResubmitForFill;

                positionMessage.FirstEdge.Mantissa = EncodeMantissa(position.FirstEdge, positionMessage.FirstEdge.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.TotalOutOfMarketOrders = position.TotalOutOfMarketOrders;
                positionMessage.TotalOutOfMarketFills = (ushort)position.TotalOutOfMarketFills;

                positionMessage.SubmissionRatePerSec = (ushort)position.SubmissionRatePerSec;
                positionMessage.MaxOrdersPerSec = (ushort)position.MaxOrdersPerSec;

                positionMessage.AvgCloseSubs = (ushort)position.AvgCloseSubs;
                positionMessage.OpenSubsCount = (ushort)position.OpenSubsCount;
                positionMessage.SubsBetweenFillsCount = (ushort)position.SubsBetweenFillsCount;


                positionMessage.IntroducingBrokerFee.Mantissa = EncodeMantissa(position.IntroducingBrokerFee, positionMessage.IntroducingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.ExecutingBrokerFee.Mantissa = EncodeMantissa(position.ExecutingBrokerFee, positionMessage.ExecutingBrokerFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.ExchangeFee.Mantissa = EncodeMantissa(position.ExchangeFee, positionMessage.ExchangeFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.OrfFee.Mantissa = EncodeMantissa(position.OrfFee, positionMessage.OrfFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.SecFee.Mantissa = EncodeMantissa(position.SecFee, positionMessage.SecFee.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.TotalFees.Mantissa = EncodeMantissa(position.TotalFees, positionMessage.TotalFees.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.RawNetQty = (short)position.RawNetQty;

                positionMessage.LastBuyEdgeToTheo.Mantissa = EncodeMantissa(position.LastBuyEdgeToTheo, positionMessage.LastBuyEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.LastSellEdgeToTheo.Mantissa = EncodeMantissa(position.LastSellEdgeToTheo, positionMessage.LastSellEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.BestBuyEdgeToTheo.Mantissa = EncodeMantissa(position.BestBuyEdgeToTheo, positionMessage.BestBuyEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.BestSellEdgeToTheo.Mantissa = EncodeMantissa(position.BestSellEdgeToTheo, positionMessage.BestSellEdgeToTheo.Exponent, DOUBLENULL2.MantissaNullValue);

                positionMessage.LastTrader = position.LastTraderId;

                positionMessage.SingleLegAdjustedPnl.Mantissa = EncodeMantissa(position.SingleLegAdjustedPnl, positionMessage.SingleLegAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
                positionMessage.SpreadAdjustedPnl.Mantissa = EncodeMantissa(position.SpreadAdjustedPnl, positionMessage.SpreadAdjustedPnl.Exponent, DOUBLENULL2.MantissaNullValue);
            }


            return message.Limit - offset;
        }

        public int EncodeMultiplePortfoliosAddedMessage(DirectBuffer directBuffer, int offset, int requestId, ICollection<IPortfolio> portfolios)
        {
            int portfolioNames = portfolios.Select(x => x.Name).Sum(x => GetBufferEstimate(x));

            IEnumerable<IPosition> positions = portfolios.SelectMany(x => x.Positions);
            int positionNames = positions.Select(x => x.Name).Sum(x => GetBufferEstimate(x));
            int positionSymbols = positions.Select(x => x.Symbol).Sum(x => GetBufferEstimate(x));

            int instanceFieldLen = 0;
            Dictionary<IPosition, string> positionToLastInstanceMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToLastTraderMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToAccountMap = new Dictionary<IPosition, string>();
            foreach (IPosition position in positions)
            {
                string lastInstance = position.LastInstance ?? "";
                positionToLastInstanceMap[position] = lastInstance;
                instanceFieldLen += GetBufferEstimate(lastInstance);

                string lastTrader = position.LastTrader ?? "";
                positionToLastTraderMap[position] = lastTrader;
                instanceFieldLen += GetBufferEstimate(lastTrader);

                string account = position.Account ?? "";
                positionToAccountMap[position] = account;
                instanceFieldLen += GetBufferEstimate(account);
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MultiplePortfoliosAddedMessage message = new MultiplePortfoliosAddedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MultiplePortfoliosAddedMessage.BlockLength;
            messageHeader.SchemaId = MultiplePortfoliosAddedMessage.SchemaId;
            messageHeader.TemplateId = MultiplePortfoliosAddedMessage.TemplateId;
            messageHeader.Version = MultiplePortfoliosAddedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            int count = portfolios.Count;
            message.RequestId = requestId;
            MultiplePortfoliosAddedMessage.NoPortfoliosGroup childMessage = message.NoPortfoliosCount(count);

            foreach (IPortfolio portfolio in portfolios)
            {
                childMessage.Next();

                childMessage.PortfolioId = portfolio.Id;
                childMessage.PortfolioType = (PortfolioType)portfolio.PortfolioType;
                childMessage.PortfolioDate = portfolio.PortfolioDate.ToUnixEpoch();
                childMessage.TotalSubmissions = portfolio.TotalSubmissions;
                childMessage.TotalSingleLegSubmissions = portfolio.TotalSingleLegSubmissions;
                childMessage.TotalSpreadSubmissions = portfolio.TotalSpreadSubmissions;
                childMessage.TotalSingleFills = portfolio.TotalSingleFills;
                childMessage.TotalSpreadFills = portfolio.TotalSpreadFills;
                childMessage.UniqueSubmissions = portfolio.UniqueSubmissions;
                childMessage.UniqueSpreadSubmissions = portfolio.UniqueSpreadSubmissions;
                childMessage.TotalFills = portfolio.TotalFills;
                childMessage.UniqueFills = portfolio.UniqueFills;
                childMessage.UniqueSpreadFills = portfolio.UniqueSpreadFills;
                childMessage.StockContracts = portfolio.StockContracts;
                childMessage.TotalContracts = portfolio.TotalContracts;
                childMessage.UniqueContracts = portfolio.UniqueContracts;
                childMessage.UniqueSpreadContracts = portfolio.UniqueSpreadContracts;
                childMessage.NetQty = portfolio.NetQty;
                childMessage.ShortQty = portfolio.ShortQty;
                childMessage.LongQty = portfolio.LongQty;

                childMessage.FillRate.Mantissa = EncodeMantissa(portfolio.FillRate, childMessage.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                childMessage.OrderFillRate.Mantissa = EncodeMantissa(portfolio.OrderFillRate, childMessage.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                childMessage.IbOrderFillRate.Mantissa = EncodeMantissa(portfolio.IbOrderFillRate, childMessage.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

                childMessage.LowestRealizedPnl = portfolio.LowestRealizedPnl;
                childMessage.HighestRealizedPnl = portfolio.HighestRealizedPnl;
                childMessage.RealizedPnl = portfolio.RealizedPnl;
                childMessage.LowestAdjustedPnl = portfolio.LowestAdjustedPnl;
                childMessage.HighestAdjustedPnl = portfolio.HighestAdjustedPnl;
                childMessage.AdjustedPnl = portfolio.AdjustedPnl;
                childMessage.SingleLegAdjustedPnl = portfolio.SingleLegAdjustedPnl;
                childMessage.SpreadAdjustedPnl = portfolio.SpreadAdjustedPnl;
                childMessage.UnrealizedPnl = portfolio.UnrealizedPnl;
                childMessage.NetDelta = portfolio.NetDelta;

                childMessage.MaxResubmitEstimate = portfolio.MaxResubmitEstimate;
                childMessage.MaxResubmitForFill = portfolio.MaxResubmitForFill;
                childMessage.AvgResubmitEstimate = portfolio.AvgResubmitEstimate;
                childMessage.AvgResubmitForFill = portfolio.AvgResubmitForFill;

                childMessage.DeltaAdjustedBurn = portfolio.DeltaAdjustedBurn;
                childMessage.DeltaAdjustedHelp = portfolio.DeltaAdjustedHelp;
                childMessage.HighestOpenNotional = portfolio.HighestOpenNotional;
                childMessage.TotalOpenNotional = portfolio.TotalOpenNotional;

                childMessage.TotalOutOfMarketOrders = portfolio.TotalOutOfMarketOrders;
                childMessage.TotalOutOfMarketFills = portfolio.TotalOutOfMarketFills;

                childMessage.SubmissionRatePerSec = portfolio.SubmissionRatePerSec;
                childMessage.MaxOrdersPerSec = portfolio.MaxOrdersPerSec;

                childMessage.WinnerTrades = portfolio.WinnerTrades;
                childMessage.LoserTrades = portfolio.LoserTrades;
                childMessage.SizeWinnerTrades = portfolio.SizeWinnerTrades;
                childMessage.SizeLoserTrades = portfolio.SizeLoserTrades;
                childMessage.AvgCloseSubs = portfolio.AvgCloseSubs;

                childMessage.IntroducingBrokerFee = portfolio.IntroducingBrokerFee;
                childMessage.ExecutingBrokerFee = portfolio.ExecutingBrokerFee;
                childMessage.ExchangeFee = portfolio.ExchangeFee;
                childMessage.OrfFee = portfolio.OrfFee;
                childMessage.SecFee = portfolio.SecFee;
                childMessage.TotalFees = portfolio.TotalFees;

                childMessage.AvgOpenSubsCount = portfolio.AvgOpenSubsCount;
                childMessage.AvgSubsBetweenFillsCount = portfolio.AvgSubsBetweenFillsCount;

                childMessage.GroupSubmissionsAvg = portfolio.GroupSubmissionsAvg;
                childMessage.GroupAvgFillRate.Mantissa = EncodeMantissa(portfolio.GroupAvgFillRate, childMessage.GroupAvgFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

                List<IPosition> positionsList = portfolio.Positions.Take(65534).ToList();
                MultiplePortfoliosAddedMessage.NoPortfoliosGroup.PositionsGroup positionMessage = childMessage.PositionsCount(positionsList.Count);
                for (int i = 0; i < positionsList.Count; i++)
                {
                    IPosition position = positionsList[i];
                    positionMessage.Next();
                    positionMessage.ParentPositionId = position.ParentPositionId;
                    positionMessage.PositionId = position.Id;
                    positionMessage.PositionType = (PositionType)position.PositionType;
                    positionMessage.NetQty = position.NetQty;
                    positionMessage.RealizedPnl = position.RealizedPnl;
                    positionMessage.AdjustedPnl = position.AdjustedPnl;
                    positionMessage.UnrealizedPnl = position.UnrealizedPnl;
                    positionMessage.NetDelta = position.NetDelta;
                    positionMessage.BestSellPrice = position.BestSellPrice;
                    positionMessage.BestSellPriceUnderMid = position.BestSellPriceUnderMid;
                    positionMessage.BestBuyPrice = position.BestBuyPrice;
                    positionMessage.BestBuyPriceUnderMid = position.BestBuyPriceUnderMid;
                    positionMessage.TotalSubmissions = position.TotalSubmissions;
                    positionMessage.TotalSingleLegSubmissions = position.TotalSingleLegSubmissions;
                    positionMessage.TotalSpreadSubmissions = position.TotalSpreadSubmissions;
                    positionMessage.TotalSingleFills = position.TotalSingleFills;
                    positionMessage.TotalSpreadFills = position.TotalSpreadFills;
                    positionMessage.UniqueSubmissions = position.UniqueSubmissions;
                    positionMessage.TotalFills = position.TotalFills;
                    positionMessage.UniqueFills = position.UniqueFills;
                    positionMessage.TotalContracts = position.TotalContracts;
                    positionMessage.UniqueContracts = position.UniqueContracts;

                    positionMessage.FillRate.Mantissa = EncodeMantissa(position.FillRate, positionMessage.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                    positionMessage.OrderFillRate.Mantissa = EncodeMantissa(position.OrderFillRate, positionMessage.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                    positionMessage.IbOrderFillRate.Mantissa = EncodeMantissa(position.IbOrderFillRate, positionMessage.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

                    positionMessage.OpenPositionAveragePrice = position.OpenPositionAveragePrice;
                    positionMessage.OpenPositionFillUnderPrice = position.OpenPositionFillUnderPrice;

                    positionMessage.LastTradeTime = ToUnixEpoch(position.LastTradeTime);
                    positionMessage.PositionDate = ToUnixEpoch(position.PositionDate);
                    positionMessage.LastEdge = position.LastEdge;
                    positionMessage.LastBuyEdge = position.LastBuyEdge;
                    positionMessage.LastSellEdge = position.LastSellEdge;
                    positionMessage.LastBuyEdgeToTheo = position.LastBuyEdgeToTheo;
                    positionMessage.LastSellEdgeToTheo = position.LastSellEdgeToTheo;

                    positionMessage.LastBuyFillEdgeToTheo = position.LastBuyFillEdgeToTheo;
                    positionMessage.LastSellFillEdgeToTheo = position.LastSellFillEdgeToTheo;
                    positionMessage.LastBuyAttemptEdgeToTheo = position.LastBuyAttemptEdgeToTheo;
                    positionMessage.LastSellAttemptEdgeToTheo = position.LastSellAttemptEdgeToTheo;

                    positionMessage.LastPermBuyFillEdgeToTheo = position.LastPermBuyFillEdgeToTheo;
                    positionMessage.LastPermSellFillEdgeToTheo = position.LastPermSellFillEdgeToTheo;
                    positionMessage.LastPermBuyAttemptEdgeToTheo = position.LastPermBuyAttemptEdgeToTheo;
                    positionMessage.LastPermSellAttemptEdgeToTheo = position.LastPermSellAttemptEdgeToTheo;

                    positionMessage.BestBuyEdgeToTheo = position.BestBuyEdgeToTheo;
                    positionMessage.WorstBuyEdgeToTheo = position.WorstBuyEdgeToTheo;
                    positionMessage.BestSellEdgeToTheo = position.BestSellEdgeToTheo;
                    positionMessage.WorstSellEdgeToTheo = position.WorstSellEdgeToTheo;
                    positionMessage.OpenNotional = position.OpenNotional;

                    positionMessage.MaxResubmitEstimate = position.MaxResubmitEstimate;
                    positionMessage.MaxResubmitForFill = position.MaxResubmitForFill;
                    positionMessage.AvgResubmitEstimate = position.AvgResubmitEstimate;
                    positionMessage.AvgResubmitForFill = position.AvgResubmitForFill;

                    positionMessage.FirstEdge = position.FirstEdge;

                    positionMessage.TotalOutOfMarketOrders = position.TotalOutOfMarketOrders;
                    positionMessage.TotalOutOfMarketFills = position.TotalOutOfMarketFills;

                    positionMessage.HardSide = position.HardSide.HasValue ? (Generated.Side)position.HardSide : Generated.Side.NULL_VALUE;
                    positionMessage.HardSideDesignationTime = position.HardSideDesignationTime.ToUnixEpoch();
                    positionMessage.HardSideBuyGiveUp = position.HardSideBuyGiveUp;
                    positionMessage.HardSideSellGiveUp = position.HardSideSellGiveUp;

                    positionMessage.SubmissionRatePerSec = position.SubmissionRatePerSec;
                    positionMessage.MaxOrdersPerSec = position.MaxOrdersPerSec;

                    positionMessage.WinnerTrades = position.WinnerTrades;
                    positionMessage.LoserTrades = position.LoserTrades;
                    positionMessage.SizeWinnerTrades = position.SizeWinnerTrades;
                    positionMessage.SizeLoserTrades = position.SizeLoserTrades;
                    positionMessage.AvgCloseSubs = position.AvgCloseSubs;
                    positionMessage.OpenSubsCount = position.OpenSubsCount;
                    positionMessage.SubsBetweenFillsCount = position.SubsBetweenFillsCount;

                    positionMessage.IntroducingBrokerFee = position.IntroducingBrokerFee;
                    positionMessage.ExecutingBrokerFee = position.ExecutingBrokerFee;
                    positionMessage.ExchangeFee = position.ExchangeFee;
                    positionMessage.OrfFee = position.OrfFee;
                    positionMessage.SecFee = position.SecFee;
                    positionMessage.TotalFees = position.TotalFees;
                    positionMessage.LastTradeSide = position.LastTradeSide.HasValue ? (Generated.Side)position.LastTradeSide : Generated.Side.NULL_VALUE;

                    positionMessage.LastBuyAttempt = position.LastBuyAttempt;
                    positionMessage.LastBuyAttemptUnderlying = position.LastBuyAttemptUnderlying;
                    positionMessage.LastSellAttempt = position.LastSellAttempt;
                    positionMessage.LastSellAttemptUnderlying = position.LastSellAttemptUnderlying;

                    positionMessage.RawNetQty = position.RawNetQty;

                    if (!positionToLastInstanceMap.TryGetValue(position, out string? lastInsatnce))
                    {
                        lastInsatnce = "";
                    }
                    positionMessage.SetLastInstance(lastInsatnce);

                    if (!positionToLastTraderMap.TryGetValue(position, out string? lastTrader))
                    {
                        lastTrader = "";
                    }
                    positionMessage.SetLastTrader(lastInsatnce);

                    if (!positionToAccountMap.TryGetValue(position, out string? account))
                    {
                        account = "";
                    }
                    positionMessage.SetAccount(account);

                    positionMessage.SingleLegAdjustedPnl = position.SingleLegAdjustedPnl;
                    positionMessage.SpreadAdjustedPnl = position.SpreadAdjustedPnl;

                    positionMessage.SetPositionName(position.Name ?? "");
                    positionMessage.SetPositionSymbol(position.Symbol ?? "");
                }
                string portfolioName = portfolio.Name ?? "";
                childMessage.SetPortfolioName(portfolioName);
            }


            return message.Limit - offset;
        }

        public int EncodeSecurityDoubleDecimalUpdate(DirectBuffer directBuffer, int offset, int id, SubscriptionFieldType type, double bidUpdate, double askUpdate, DateTime lastUpdateTime, double bidChange, double askChange, int bidSize, int askSize, double lastPrice, double latencyMs = 0)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SecurityDecimalUpdate.BlockLength;
            messageHeader.SchemaId = SecurityDecimalUpdate.SchemaId;
            messageHeader.TemplateId = SecurityDecimalUpdate.TemplateId;
            messageHeader.Version = SecurityDecimalUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SecurityDecimalUpdate message = new SecurityDecimalUpdate();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.TickerId = id;
            message.UpdateType = (short)type;
            message.BidUpdate.Mantissa = EncodeMantissa(bidUpdate, message.BidUpdate.Exponent, DOUBLENULL2.MantissaNullValue);
            message.AskUpdate.Mantissa = EncodeMantissa(askUpdate, message.AskUpdate.Exponent, DOUBLENULL2.MantissaNullValue);
            message.LastPrice.Mantissa = EncodeMantissa(lastPrice, message.LastPrice.Exponent, DOUBLENULL2.MantissaNullValue);
            message.Timestamp = ToUnixEpoch(lastUpdateTime);
            message.BidChange = bidChange < 0 ? QuoteChangeType.Down : bidChange > 0 ? QuoteChangeType.Up : QuoteChangeType.None;
            message.AskChange = askChange < 0 ? QuoteChangeType.Down : askChange > 0 ? QuoteChangeType.Up : QuoteChangeType.None;
            message.BidSize = bidSize;
            message.AskSize = askSize;
            message.LatencyMs.Mantissa = EncodeMantissa(latencyMs, message.LatencyMs.Exponent, DOUBLENULL4.MantissaNullValue);

            return message.Limit - offset;
        }

        public int EncodeDerivedValueUpdate(DirectBuffer directBuffer, int offset, DerivedValueUpdateModelContainer model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = DerivedValueUpdate.BlockLength;
            messageHeader.SchemaId = DerivedValueUpdate.SchemaId;
            messageHeader.TemplateId = DerivedValueUpdate.TemplateId;
            messageHeader.Version = DerivedValueUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            DerivedValueUpdate message = new DerivedValueUpdate();

            message.WrapForEncode(directBuffer, bufferOffset);
            lock (model.Lock)
            {
                message.TickerId = model.Final.TickerId;
                message.InterpolatedBidUpdate = model.Final.InterpolatedBidUpdate;
                message.InterpolatedAskUpdate = model.Final.InterpolatedAskUpdate;
                message.BestBidUpdate = model.Final.BestBidUpdate;
                message.BestAskUpdate = model.Final.BestAskUpdate;
                message.BestBidBase = model.Final.BestBidBase;
                message.BestAskBase = model.Final.BestAskBase;
                message.BestBidUnderlying = model.Final.BestBidUnderlying;
                message.BestAskUnderlying = model.Final.BestAskUnderlying;
                message.BidTradeUpdate = model.Final.BidTradeUpdate;
                message.AskTradeUpdate = model.Final.AskTradeUpdate;
                message.BidTradeBase = model.Final.BidTradeBase;
                message.AskTradeBase = model.Final.AskTradeBase;
                message.BidTradeUnderlying = model.Final.BidTradeUnderlying;
                message.AskTradeUnderlying = model.Final.AskTradeUnderlying;
                message.BidTradeTimestamp = model.Final.BidTradeTimestamp.ToUnixEpoch();
                message.AskTradeTimestamp = model.Final.AskTradeTimestamp.ToUnixEpoch();
                message.BidTradeCount = model.Final.BidTradeCount;
                message.AskTradeCount = model.Final.AskTradeCount;
                message.BidTradeIsLatest = model.Final.BidTradeIsLatest ? BooleanEnum.True : BooleanEnum.False;
                message.AskTradeIsLatest = model.Final.AskTradeIsLatest ? BooleanEnum.True : BooleanEnum.False;
                message.CustTradeBidCount = model.Final.CustTradeBidCount;
                message.CustTradeAskCount = model.Final.CustTradeAskCount;
                message.CustTradeBid = model.Final.CustTradeBid;
                message.CustTradeAsk = model.Final.CustTradeAsk;
                message.CustTradeBidBase = model.Final.CustTradeBidBase;
                message.CustTradeAskBase = model.Final.CustTradeAskBase;
                message.CustTradeBidNoChange = model.Final.CustTradeBidNoChange;
                message.CustTradeAskNoChange = model.Final.CustTradeAskNoChange;
                message.CustTradeBidBaseNoChange = model.Final.CustTradeBidBaseNoChange;
                message.CustTradeAskBaseNoChange = model.Final.CustTradeAskBaseNoChange;
                message.CustTradeBidUnderlyingPrice = model.Final.CustTradeBidUnderlyingPrice;
                message.CustTradeAskUnderlyingPrice = model.Final.CustTradeAskUnderlyingPrice;
                message.CustBidTradeIsLatest = model.Final.CustBidTradeIsLatest ? BooleanEnum.True : BooleanEnum.False;
                message.CustAskTradeIsLatest = model.Final.CustAskTradeIsLatest ? BooleanEnum.True : BooleanEnum.False;
                message.CustBidTradeTimestamp = model.Final.CustBidTradeTimestamp.ToUnixEpoch();
                message.CustAskTradeTimestamp = model.Final.CustAskTradeTimestamp.ToUnixEpoch();

                message.HighestBid = model.Final.HighestBidLowestAskResult!.HighestBid;
                message.LowestAsk = model.Final.HighestBidLowestAskResult!.LowestAsk;
                message.HighestBidTime = model.Final.HighestBidLowestAskResult!.HighestBidTime;
                message.LowestAskTime = model.Final.HighestBidLowestAskResult!.LowestAskTime;
                message.HighestBidBase = model.Final.HighestBidLowestAskResult!.HighestBidBase;
                message.LowestAskBase = model.Final.HighestBidLowestAskResult!.LowestAskBase;
                message.HighestBidUnderlyingMid = model.Final.HighestBidLowestAskResult!.HighestBidUnderlyingMid;
                message.LowestAskUnderlyingMid = model.Final.HighestBidLowestAskResult!.LowestAskUnderlyingMid;
                message.SkewAdjustedHighestBid = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBid;
                message.SkewAdjustedLowestAsk = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAsk;
                message.SkewAdjustedHighestBidTime = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidTime;
                message.SkewAdjustedLowestAskTime = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskTime;
                message.SkewAdjustedHighestBidBase = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidBase;
                message.SkewAdjustedLowestAskBase = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskBase;
                message.SkewAdjustedHighestBidUnderlyingMid = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidUnderlyingMid;
                message.SkewAdjustedLowestAskUnderlyingMid = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskUnderlyingMid;

                message.HighestBidLong = model.Final.HighestBidLowestAskResult!.HighestBid;
                message.LowestAskLong = model.Final.HighestBidLowestAskResult!.LowestAsk;
                message.HighestBidTimeLong = model.Final.HighestBidLowestAskResult!.HighestBidTime;
                message.LowestAskTimeLong = model.Final.HighestBidLowestAskResult!.LowestAskTime;
                message.HighestBidBaseLong = model.Final.HighestBidLowestAskResult!.HighestBidBase;
                message.LowestAskBaseLong = model.Final.HighestBidLowestAskResult!.LowestAskBase;
                message.HighestBidUnderlyingMidLong = model.Final.HighestBidLowestAskResult!.HighestBidUnderlyingMid;
                message.LowestAskUnderlyingMidLong = model.Final.HighestBidLowestAskResult!.LowestAskUnderlyingMid;
                message.SkewAdjustedHighestBidLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBid;
                message.SkewAdjustedLowestAskLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAsk;
                message.SkewAdjustedHighestBidTimeLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidTime;
                message.SkewAdjustedLowestAskTimeLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskTime;
                message.SkewAdjustedHighestBidBaseLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidBase;
                message.SkewAdjustedLowestAskBaseLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskBase;
                message.SkewAdjustedHighestBidUnderlyingMidLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedHighestBidUnderlyingMid;
                message.SkewAdjustedLowestAskUnderlyingMidLong = model.Final.HighestBidLowestAskResult!.SkewAdjustedLowestAskUnderlyingMid;

                EncodePriceNull3(message.ImpliedBid, model.Final.ImpliedBid);
                EncodePriceNull3(message.ImpliedAsk, model.Final.ImpliedAsk);
                EncodePriceNull3(message.ImpliedBidRecord, model.Final.ImpliedBidRecord);
                EncodePriceNull3(message.ImpliedAskRecord, model.Final.ImpliedAskRecord);
                EncodePriceNull3(message.ImpliedBidRecordTheo, model.Final.ImpliedBidRecordTheo);
                EncodePriceNull3(message.ImpliedBidRecordTheoMovement, model.Final.ImpliedBidRecordTheoMovement);
                EncodePriceNull3(message.ImpliedBidRecordNonDeltaMovement, model.Final.ImpliedBidRecordNonDeltaMovement);
                message.ImpliedBidRecordTimestamp = model.Final.ImpliedBidRecordTimestamp.ToUnixEpoch();
                EncodePriceNull3(message.ImpliedAskRecordTheo, model.Final.ImpliedAskRecordTheo);
                EncodePriceNull3(message.ImpliedAskRecordTheoMovement, model.Final.ImpliedAskRecordTheoMovement);
                EncodePriceNull3(message.ImpliedAskRecordNonDeltaMovement, model.Final.ImpliedAskRecordNonDeltaMovement);
                message.ImpliedAskRecordTimestamp = model.Final.ImpliedAskRecordTimestamp.ToUnixEpoch();
            }
            return message.Limit - offset;
        }

        public int EncodeGreekUpdate(DirectBuffer directBuffer, int offset, GreekUpdateModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = GreekUpdateMessage.BlockLength;
            messageHeader.SchemaId = GreekUpdateMessage.SchemaId;
            messageHeader.TemplateId = GreekUpdateMessage.TemplateId;
            messageHeader.Version = GreekUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            GreekUpdateMessage message = new GreekUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);
            lock (model.Lock)
            {
                message.Index = model.Index;
                message.TimeStamp = model.TimeStamp;
                message.CollectorTimestamp = model.CollectorTimestamp;
                message.CollectorTimestampNanos = model.CollectorTimestampNanos;
                message.CalculationTimestampNanos = model.CalculationTimestampNanos;
                message.BidTimestampNanos = model.BidTimestampNanos;
                message.AskTimestampNanos = model.AskTimestampNanos;
                message.SequenceNumber = model.SequenceNumber;
                message.TradeVolume = model.TradeVolume;
                message.BidSize = model.BidSize;
                message.AskSize = model.AskSize;
                message.BidPrice = model.BidPrice;
                message.AskPrice = model.AskPrice;
                message.Theo = model.Theo;
                message.ImpliedVolatility = model.ImpliedVolatility;
                message.Delta = model.Delta;
                message.Gamma = model.Gamma;
                message.Vega = model.Vega;
                message.Theta = model.Theta;
                message.Rho = model.Rho;
                message.BidVol = model.BidVol;
                message.AskVol = model.AskVol;
                message.MidVol = model.MidVol;
                message.BidMCID = model.BidMCID;
                message.AskMCID = model.AskMCID;
                message.UBidPrice = model.UBidPrice;
                message.UAskPrice = model.UAskPrice;
                message.UTimestampNanos = model.UTimestampNanos;
                message.PersistorTimestampNanos = model.PersistorTimestampNanos;
                message.PersistorSeqNum = model.PersistorSeqNum;
                message.InfoBits = model.InfoBits;
                message.TimeValue = model.TimeValue;
                message.IntrinsicValue = model.IntrinsicValue;
                message.FvDivs = model.FvDivs;
            }
            return message.Limit - offset;
        }

        public int EncodeIbQuoteUpdate(DirectBuffer directBuffer, int offset, IbQuoteUpdateModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = IbQuoteUpdateMessage.BlockLength;
            messageHeader.SchemaId = IbQuoteUpdateMessage.SchemaId;
            messageHeader.TemplateId = IbQuoteUpdateMessage.TemplateId;
            messageHeader.Version = IbQuoteUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            IbQuoteUpdateMessage message = new IbQuoteUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.TickerId = model.TickerId;
            message.BidSize = model.BidSize;
            message.AskSize = model.AskSize;
            message.LastSize = model.LastSize;
            message.Volume = model.Volume;

            message.Bid = model.Bid;
            message.Ask = model.Ask;
            message.Last = model.Last;
            message.High = model.High;
            message.Low = model.Low;
            message.Open = model.Open;
            message.Close = model.Close;

            message.ImpliedVolatility = model.ImpliedVolatility;
            message.Delta = model.Delta;
            message.OptPrice = model.OptPrice;
            message.PvDividend = model.PvDividend;
            message.Gamma = model.Gamma;
            message.Vega = model.Vega;
            message.Theta = model.Theta;
            message.UndPrice = model.UndPrice;

            message.SetBidExch(model.BidExch ?? "");
            message.SetAskExch(model.AskExch ?? "");
            message.SetLastExch(model.LastExch ?? "");
            message.SetSymbol(model.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeDoubleUpdate(DirectBuffer directBuffer, int offset, int id,
                                         SubscriptionFieldType fieldType,
                                         uint updateSequence,
                                         ulong underlyingTimestamp,
                                         ulong snapshotTimestamp,
                                         ulong hanweckTimestamp,
                                         double theo,
                                         double delta,
                                         double gamma,
                                         double vega,
                                         double theta,
                                         double rho,
                                         double implied,
                                         double latestMidPrice,
                                         double snapshotMidPrice,
                                         double deltaAdjustedTheo,
                                         bool jumpDetected)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = DoubleUpdate.BlockLength;
            messageHeader.SchemaId = DoubleUpdate.SchemaId;
            messageHeader.TemplateId = DoubleUpdate.TemplateId;
            messageHeader.Version = DoubleUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            DoubleUpdate message = new DoubleUpdate();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.TickerId = id;
            message.UpdateType = (short)fieldType;
            message.Sequence = updateSequence;

            message.UnderlyingTimestamp = underlyingTimestamp;
            message.SnapshotTimestamp = snapshotTimestamp;
            message.HanweckTimestamp = hanweckTimestamp;
            message.Theo = theo;
            message.Delta = delta;
            message.Gamma = gamma;
            message.Vega = vega;
            message.Theta = theta;
            message.Rho = rho;
            message.Implied = implied;
            message.LatestMidPrice = latestMidPrice;
            message.SnapshotMidPrice = snapshotMidPrice;
            message.DeltaAdjustedTheo = deltaAdjustedTheo;
            message.JumpDetected = jumpDetected ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodeDeltaAdjTheoUpdate(DirectBuffer directBuffer, int offset, ref AdjTheoUpdate theoUpdate)
        {
            if (theoUpdate.TickerId < 0 || theoUpdate.TickerId > 16777215)
            {
                throw new ArgumentOutOfRangeException(nameof(theoUpdate.TickerId));
            }
            if (theoUpdate.Sequence > 16777215)
            {
                throw new ArgumentOutOfRangeException(nameof(theoUpdate.Sequence));
            }
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = DeltaAdjustedTheoUpdateMessage.BlockLength;
            messageHeader.SchemaId = DeltaAdjustedTheoUpdateMessage.SchemaId;
            messageHeader.TemplateId = DeltaAdjustedTheoUpdateMessage.TemplateId;
            messageHeader.Version = DeltaAdjustedTheoUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            DeltaAdjustedTheoUpdateMessage message = new DeltaAdjustedTheoUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetTickerId(0, (byte)(theoUpdate.TickerId >> 16));
            message.SetTickerId(1, (byte)(theoUpdate.TickerId >> 8));
            message.SetTickerId(2, (byte)theoUpdate.TickerId);

            message.SetSequence(0, (byte)(theoUpdate.Sequence >> 16));
            message.SetSequence(1, (byte)(theoUpdate.Sequence >> 8));
            message.SetSequence(2, (byte)theoUpdate.Sequence);

            message.JumpDetected = theoUpdate.JumpDetected ? BooleanEnum.True : BooleanEnum.False;
            message.ModelId = theoUpdate.ModelId;
            EncodePriceNull3(message.Theo, theoUpdate.Theo);
            EncodePriceNull3(message.SmoothedTheo, theoUpdate.SmoothTheo);
            EncodePriceNull3(message.Underlying, theoUpdate.Underlying);
            EncodePriceNull3(message.SecondaryTheo, theoUpdate.SecondaryTheo);
            EncodePriceNull3(message.SecondaryTheoAdj, theoUpdate.SecondaryTheoAdj);
            EncodePriceNull3(message.PriceMetric, theoUpdate.PriceMetric);
            message.SecondaryVol = theoUpdate.SecondaryVol;
            EncodePriceNull3(message.ChangeInPremium, theoUpdate.ChangeInPremium);
            EncodePriceNull3(message.SecondarySpot, theoUpdate.SecondarySpot);
            EncodePriceNull3(message.DaEma, theoUpdate.DaEma);
            EncodePriceNull3(message.VolaEma, theoUpdate.VolaEma);

            return message.Limit - offset;
        }

        public int EncodeRequestTransactionsFromArchiveMessage(DirectBuffer directBuffer, int offset, int requestId,
                                                                  DateTime startDateTime,
                                                                  DateTime endDateTime,
                                                                  bool ordersOnly,
                                                                  List<Data.Enums.OrderStatus> orderStatus,
                                                                  List<string> apiUsernames,
                                                                  List<string> tags,
                                                                  List<string> symbols,
                                                                  List<string> underlyings)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RequestTransactionsFromArchive message = new RequestTransactionsFromArchive();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RequestTransactionsFromArchive.BlockLength;
            messageHeader.SchemaId = RequestTransactionsFromArchive.SchemaId;
            messageHeader.TemplateId = RequestTransactionsFromArchive.TemplateId;
            messageHeader.Version = RequestTransactionsFromArchive.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.StartDateTime = ToUnixEpoch(startDateTime);
            message.EndDateTime = ToUnixEpoch(endDateTime);
            message.FillsOnly = ordersOnly ? BooleanEnum.True : BooleanEnum.False;

            RequestTransactionsFromArchive.ApiUsernamesGroup apiUsernamesGroup = message.ApiUsernamesCount(apiUsernames.Count);
            foreach (string username in apiUsernames)
            {
                apiUsernamesGroup.Next();
                apiUsernamesGroup.SetApiUsername(username ?? string.Empty);
            }

            RequestTransactionsFromArchive.ApiUsernamesGroup tagsGroup = message.ApiUsernamesCount(tags.Count);
            foreach (string tag in tags)
            {
                tagsGroup.Next();
                tagsGroup.SetApiUsername(tag ?? string.Empty);
            }

            RequestTransactionsFromArchive.ApiUsernamesGroup symbolsGroup = message.ApiUsernamesCount(symbols.Count);
            foreach (string symbol in symbols)
            {
                symbolsGroup.Next();
                symbolsGroup.SetApiUsername(symbol ?? string.Empty);
            }

            RequestTransactionsFromArchive.ApiUsernamesGroup underlyingsGroup = message.ApiUsernamesCount(underlyings.Count);
            foreach (string underlying in underlyings)
            {
                underlyingsGroup.Next();
                underlyingsGroup.SetApiUsername(underlying ?? string.Empty);
            }

            RequestTransactionsFromArchive.OrderStatusGroup OrderStatussGroup = message.OrderStatusCount(orderStatus.Count);
            foreach (Data.Enums.OrderStatus status in orderStatus)
            {
                OrderStatussGroup.Next();
                OrderStatussGroup.OrderStatus = (OrderStatus)status;
            }

            return message.Limit - offset;
        }

        public int EncodeRequestPnlFromArchiveMessage(DirectBuffer directBuffer, int offset, int requestId,
                                                         DateTime startDateTime,
                                                         DateTime endDateTime,
                                                         bool requestPreCalcs,
                                                         bool includeBreakdownStats,
                                                         List<string> apiUsernames,
                                                         List<string> tags,
                                                         List<string> symbols,
                                                         List<string> underlyings)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RequestPnlFromArchive message = new RequestPnlFromArchive();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RequestPnlFromArchive.BlockLength;
            messageHeader.SchemaId = RequestPnlFromArchive.SchemaId;
            messageHeader.TemplateId = RequestPnlFromArchive.TemplateId;
            messageHeader.Version = RequestPnlFromArchive.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.StartDateTime = ToUnixEpoch(startDateTime);
            message.EndDateTime = ToUnixEpoch(endDateTime);
            message.RequestPreCalcs = requestPreCalcs ? BooleanEnum.True : BooleanEnum.False;
            message.IncludeBreakdownStats = includeBreakdownStats ? BooleanEnum.True : BooleanEnum.False;

            RequestPnlFromArchive.ApiUsernamesGroup apiUsernamesGroup = message.ApiUsernamesCount(apiUsernames.Count);
            foreach (string username in apiUsernames)
            {
                apiUsernamesGroup.Next();
                apiUsernamesGroup.SetApiUsername(username ?? string.Empty);
            }

            RequestPnlFromArchive.ApiUsernamesGroup tagsGroup = message.ApiUsernamesCount(tags.Count);
            foreach (string tag in tags)
            {
                tagsGroup.Next();
                tagsGroup.SetApiUsername(tag ?? string.Empty);
            }

            RequestPnlFromArchive.ApiUsernamesGroup symbolsGroup = message.ApiUsernamesCount(symbols.Count);
            foreach (string symbol in symbols)
            {
                symbolsGroup.Next();
                symbolsGroup.SetApiUsername(symbol ?? string.Empty);
            }

            RequestPnlFromArchive.ApiUsernamesGroup underlyingsGroup = message.ApiUsernamesCount(underlyings.Count);
            foreach (string underlying in underlyings)
            {
                underlyingsGroup.Next();
                underlyingsGroup.SetApiUsername(underlying ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeRequestAuditTrailMessage(DirectBuffer directBuffer, int offset, int requestId, string orderId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RequestAuditTrail message = new RequestAuditTrail();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RequestAuditTrail.BlockLength;
            messageHeader.SchemaId = RequestAuditTrail.SchemaId;
            messageHeader.TemplateId = RequestAuditTrail.TemplateId;
            messageHeader.Version = RequestAuditTrail.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetOrderId(orderId ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuditTrailResponseMessage(DirectBuffer directBuffer, int offset, int requestId, string orderId, XmlDocument xmlDocument)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AuditTrailResponse message = new AuditTrailResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AuditTrailResponse.BlockLength;
            messageHeader.SchemaId = AuditTrailResponse.SchemaId;
            messageHeader.TemplateId = AuditTrailResponse.TemplateId;
            messageHeader.Version = AuditTrailResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetOrderId(orderId ?? "");

            MemoryStream memoryStream = new MemoryStream();
            xmlDocument.Save(memoryStream);
            byte[] xmlBytes = memoryStream.ToArray();
            message.SetRawData(xmlBytes);

            return message.Limit - offset;
        }

        public int EncodeRequestOrderDetailsMessage(DirectBuffer directBuffer, int offset, int requestId, string orderId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RequestOrderDetails message = new RequestOrderDetails();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RequestOrderDetails.BlockLength;
            messageHeader.SchemaId = RequestOrderDetails.SchemaId;
            messageHeader.TemplateId = RequestOrderDetails.TemplateId;
            messageHeader.Version = RequestOrderDetails.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetOrderId(orderId ?? "");
            return message.Limit - offset;
        }

        public int EncodeOrderDetailsResponseMessage(DirectBuffer directBuffer, int offset, int requestId, string orderId, string json)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderDetailsResponse message = new OrderDetailsResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderDetailsResponse.BlockLength;
            messageHeader.SchemaId = OrderDetailsResponse.SchemaId;
            messageHeader.TemplateId = OrderDetailsResponse.TemplateId;
            messageHeader.Version = OrderDetailsResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetOrderId(orderId ?? "");
            message.SetJson(json ?? "");

            return message.Limit - offset;
        }

        public int EncodeHanweckUpdatesWithMatchingTimestampsRequestMessage(DirectBuffer directBuffer, int offset, int requestId, List<string> symbols)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            HanweckUpdatesWithMatchingTimestampsRequest message = new HanweckUpdatesWithMatchingTimestampsRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HanweckUpdatesWithMatchingTimestampsRequest.BlockLength;
            messageHeader.SchemaId = HanweckUpdatesWithMatchingTimestampsRequest.SchemaId;
            messageHeader.TemplateId = HanweckUpdatesWithMatchingTimestampsRequest.TemplateId;
            messageHeader.Version = HanweckUpdatesWithMatchingTimestampsRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            HanweckUpdatesWithMatchingTimestampsRequest.SymbolsGroup symbolsGroup = message.SymbolsCount(symbols.Count);

            for (int i = 0; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                symbolsGroup.Next();
                symbolsGroup.SetSymbol(symbol ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeHanweckUpdatesWithMatchingTimestampsResponseMessage(DirectBuffer directBuffer, int offset, int requestId, bool updateFound, DateTime timestamp, double price, Dictionary<string, double> symbolToTheoMap)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            HanweckUpdatesWithMatchingTimestampsResponse message = new HanweckUpdatesWithMatchingTimestampsResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HanweckUpdatesWithMatchingTimestampsResponse.BlockLength;
            messageHeader.SchemaId = HanweckUpdatesWithMatchingTimestampsResponse.SchemaId;
            messageHeader.TemplateId = HanweckUpdatesWithMatchingTimestampsResponse.TemplateId;
            messageHeader.Version = HanweckUpdatesWithMatchingTimestampsResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.UpdateFound = updateFound ? BooleanEnum.True : BooleanEnum.False;
            message.Timestamp = ToUnixEpoch(timestamp);
            message.Price = price;
            HanweckUpdatesWithMatchingTimestampsResponse.SymbolsGroup symbolsGroup = message.SymbolsCount(symbolToTheoMap.Count);
            for (int i = 0; i < symbolToTheoMap.Count; i++)
            {
                KeyValuePair<string, double> symbolTheoPair = symbolToTheoMap.ElementAt(i);
                symbolsGroup.Next();
                symbolsGroup.Theo = symbolTheoPair.Value;
                symbolsGroup.SetSymbol(symbolTheoPair.Key ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeSymbolMapRequestMessage(DirectBuffer directBuffer, int offset, int requestId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolMapRequestMessage message = new SymbolMapRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolMapRequestMessage.BlockLength;
            messageHeader.SchemaId = SymbolMapRequestMessage.SchemaId;
            messageHeader.TemplateId = SymbolMapRequestMessage.TemplateId;
            messageHeader.Version = SymbolMapRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            return message.Limit - offset;
        }

        public int EncodeSymbolMapResponseMessage(DirectBuffer directBuffer, int offset, int requestId, Dictionary<string, int> symbolToIndexMap, bool lastGroup)
        {
            int count = symbolToIndexMap.Count;
            int size = MessageHeader.Size + SymbolMapResponseMessage.BlockLength + SymbolMapResponseMessage.SymbolMapGroup.SbeHeaderSize + (count * SymbolMapResponseMessage.SymbolMapGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolMapResponseMessage message = new SymbolMapResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolMapResponseMessage.BlockLength;
            messageHeader.SchemaId = SymbolMapResponseMessage.SchemaId;
            messageHeader.TemplateId = SymbolMapResponseMessage.TemplateId;
            messageHeader.Version = SymbolMapResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            SymbolMapResponseMessage.SymbolMapGroup symbolsGroup = message.SymbolMapCount(count);
            foreach (KeyValuePair<string, int> symbolTheoPair in symbolToIndexMap)
            {
                symbolsGroup.Next();
                symbolsGroup.Index = symbolTheoPair.Value;
                symbolsGroup.SetSymbol(symbolTheoPair.Key ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeRootSymbolMapRequestMessage(DirectBuffer directBuffer, int offset, int requestId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RootSymbolMapRequestMessage message = new RootSymbolMapRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RootSymbolMapRequestMessage.BlockLength;
            messageHeader.SchemaId = RootSymbolMapRequestMessage.SchemaId;
            messageHeader.TemplateId = RootSymbolMapRequestMessage.TemplateId;
            messageHeader.Version = RootSymbolMapRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            return message.Limit - offset;
        }

        public int EncodeRootSymbolMapResponseMessage(DirectBuffer directBuffer, int offset, int requestId, Dictionary<Security, int> symbolToIndexMap, bool lastGroup)
        {
            HashSet<Option> options = symbolToIndexMap.Keys.Where(x => x.SecurityType == SecurityType.Option).Select(x => (Option)x).ToHashSet();
            Dictionary<string, List<Option>> groupedByRoot = options.GroupBy(x => x.RootSymbol).Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToDictionary(x => x.Key!, y => y.ToList());
            int count = symbolToIndexMap.Count;
            int size = MessageHeader.Size + RootSymbolMapResponseMessage.BlockLength + RootSymbolMapResponseMessage.RootSymbolMapGroup.SbeHeaderSize + (groupedByRoot.Count * RootSymbolMapResponseMessage.RootSymbolMapGroup.SbeBlockLength) + (options.Count * RootSymbolMapResponseMessage.RootSymbolMapGroup.ExpirationSymbolMapGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RootSymbolMapResponseMessage.BlockLength;
            messageHeader.SchemaId = RootSymbolMapResponseMessage.SchemaId;
            messageHeader.TemplateId = RootSymbolMapResponseMessage.TemplateId;
            messageHeader.Version = RootSymbolMapResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            RootSymbolMapResponseMessage message = new RootSymbolMapResponseMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            RootSymbolMapResponseMessage.RootSymbolMapGroup rootSymbolsGroup = message.RootSymbolMapCount(groupedByRoot.Count);
            foreach (KeyValuePair<string, List<Option>> rootGroup in groupedByRoot)
            {
                if (!string.IsNullOrWhiteSpace(rootGroup.Key))
                {
                    rootSymbolsGroup.Next();
                    rootSymbolsGroup.SetRootSymbol(rootGroup.Key ?? string.Empty);
                    RootSymbolMapResponseMessage.RootSymbolMapGroup.ExpirationSymbolMapGroup expirations = rootSymbolsGroup.ExpirationSymbolMapCount(rootGroup.Value.Count);
                    foreach (Option option in rootGroup.Value)
                    {
                        RootSymbolMapResponseMessage.RootSymbolMapGroup.ExpirationSymbolMapGroup messageExp = expirations.Next();
                        if (symbolToIndexMap.TryGetValue(option, out int index))
                        {
                            DateTime parsedDate = option.Expiration;
                            messageExp.Index = index;
                            messageExp.Expiration = (parsedDate.Year * 10000) + (parsedDate.Month * 100) + parsedDate.Day;
                            messageExp.PutCall = option.PutCall == Data.Enums.PutCall.Put ? Generated.PutCall.Put : Generated.PutCall.Call;
                            messageExp.Strike = option.Strike;
                        }
                    }
                }
            }

            return message.Limit - offset;
        }

        public int EncodeOptionSymbolMapResponseMessage(DirectBuffer directBuffer, int offset, int requestId, Dictionary<Option, int> symbolToIndexMap, bool lastGroup)
        {
            IEnumerable<IGrouping<string?, KeyValuePair<Option, int>>> roots = symbolToIndexMap.GroupBy(x => x.Key.RootSymbol);
            int rootsCount = roots.Count();
            int count = symbolToIndexMap.Count;

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OptionSymbolMapResponseMessage message = new OptionSymbolMapResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OptionSymbolMapResponseMessage.BlockLength;
            messageHeader.SchemaId = OptionSymbolMapResponseMessage.SchemaId;
            messageHeader.TemplateId = OptionSymbolMapResponseMessage.TemplateId;
            messageHeader.Version = OptionSymbolMapResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;

            OptionSymbolMapResponseMessage.SymbolMapGroup symbolsGroup = message.SymbolMapCount(rootsCount);
            foreach (IGrouping<string?, KeyValuePair<Option, int>> symbolTheoPair in roots)
            {
                string? key = symbolTheoPair.Key;
                int rootCount = symbolTheoPair.Count();
                symbolsGroup.Next();
                symbolsGroup.SetRoot(key);
                OptionSymbolMapResponseMessage.SymbolMapGroup.RootMapGroup rootGroup = symbolsGroup.RootMapCount(rootCount);
                foreach (KeyValuePair<Option, int> kvp in symbolTheoPair)
                {
                    Option option = kvp.Key;
                    int index = kvp.Value;
                    rootGroup.Next();
                    rootGroup.Index = index;
                    rootGroup.PutCall = option.PutCall == Data.Enums.PutCall.Put ? Generated.PutCall.Put : Generated.PutCall.Call;
                    rootGroup.Expiration = Convert.ToInt32(option.Expiration.ToString("yyyymmdd"));
                    rootGroup.Strike = option.Strike;
                }
            }

            return message.Limit - offset;
        }

        public int EncodeMultipleSecurityDecimalUpdateMessage(DirectBuffer directBuffer, int offset, SubscriptionFieldType subscriptionFieldType, Dictionary<int, (double update, double bidUpdate, double askUpdate)> symbolIdToUpdateMap)
        {
            int count = symbolIdToUpdateMap.Count;
            int size = MessageHeader.Size + MultipleSecurityDecimalUpdateMessage.BlockLength + MultipleSecurityDecimalUpdateMessage.UpdatesGroup.SbeHeaderSize + (count * MultipleSecurityDecimalUpdateMessage.UpdatesGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MultipleSecurityDecimalUpdateMessage message = new MultipleSecurityDecimalUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MultipleSecurityDecimalUpdateMessage.BlockLength;
            messageHeader.SchemaId = MultipleSecurityDecimalUpdateMessage.SchemaId;
            messageHeader.TemplateId = MultipleSecurityDecimalUpdateMessage.TemplateId;
            messageHeader.Version = MultipleSecurityDecimalUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.UpdateType = (short)subscriptionFieldType;

            MultipleSecurityDecimalUpdateMessage.UpdatesGroup updatesGroup = message.UpdatesCount(count);
            foreach (KeyValuePair<int, (double update, double bidUpdate, double askUpdate)> symbolIdUpdatePair in symbolIdToUpdateMap)
            {
                updatesGroup.Next();
                updatesGroup.SymbolId = symbolIdUpdatePair.Key;
                updatesGroup.Update = symbolIdUpdatePair.Value.update;
                updatesGroup.BidUpdate = symbolIdUpdatePair.Value.bidUpdate;
                updatesGroup.AskUpdate = symbolIdUpdatePair.Value.askUpdate;
            }

            return message.Limit - offset;
        }

        public int EncodeOptionSnapshotRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol, DateTime expiration, double delta, DateTime startDateTime, DateTime endDateTime)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OptionSnapshotsRequest message = new OptionSnapshotsRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OptionSnapshotsRequest.BlockLength;
            messageHeader.SchemaId = OptionSnapshotsRequest.SchemaId;
            messageHeader.TemplateId = OptionSnapshotsRequest.TemplateId;
            messageHeader.Version = OptionSnapshotsRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.Delta = delta;
            message.Expiration = ToUnixEpoch(expiration);
            message.StartDateTime = ToUnixEpoch(startDateTime);
            message.EndDateTime = ToUnixEpoch(endDateTime);
            message.SetSymbol(symbol ?? string.Empty);

            return message.Limit - offset;
        }

        public int EncodeOptionSnapshotResponseMessage(DirectBuffer directBuffer, int offset, int requestId, bool found, List<Data.Responses.OptionSnapshot> snapshots)
        {
            int count = snapshots.Count;
            int size = MessageHeader.Size + OptionSnapshotsResponse.BlockLength + OptionSnapshotsResponse.SnapshotsGroup.SbeHeaderSize + (count * (OptionSnapshotsResponse.SnapshotsGroup.SbeBlockLength + 200));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OptionSnapshotsResponse message = new OptionSnapshotsResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OptionSnapshotsResponse.BlockLength;
            messageHeader.SchemaId = OptionSnapshotsResponse.SchemaId;
            messageHeader.TemplateId = OptionSnapshotsResponse.TemplateId;
            messageHeader.Version = OptionSnapshotsResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.UpdateFound = found ? BooleanEnum.True : BooleanEnum.False;

            OptionSnapshotsResponse.SnapshotsGroup nextSnapshot = message.SnapshotsCount(count);
            foreach (Data.Responses.OptionSnapshot snapshot in snapshots)
            {
                nextSnapshot.Next();

                nextSnapshot.AdjTheo = snapshot.AdjTheo;
                nextSnapshot.Bid = snapshot.Bid;
                nextSnapshot.Ask = snapshot.Ask;
                nextSnapshot.UnderBid = snapshot.UnderBid;
                nextSnapshot.UnderAsk = snapshot.UnderAsk;
                nextSnapshot.Theo = snapshot.Theo;
                nextSnapshot.Delta = snapshot.Delta;
                nextSnapshot.Vega = snapshot.Vega;
                nextSnapshot.Iv = snapshot.Iv;

                nextSnapshot.QuoteTime = ToUnixEpoch(snapshot.QuoteTime);
                nextSnapshot.SnapshotTime = ToUnixEpoch(snapshot.SnapshotTime);
                nextSnapshot.HanweckCalcTime = ToUnixEpoch(snapshot.HanweckCalcTime);
                nextSnapshot.AdjTheoTime = ToUnixEpoch(snapshot.AdjTheoTime);
                nextSnapshot.SetSymbol(snapshot.Symbol ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeMarketCrossScanRequestMessage(DirectBuffer directBuffer, int offset, int requestId, double lookbackInSeconds, double minMarketCross, double currentMarketWidth)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MarketCrossScanRequest message = new MarketCrossScanRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MarketCrossScanRequest.BlockLength;
            messageHeader.SchemaId = MarketCrossScanRequest.SchemaId;
            messageHeader.TemplateId = MarketCrossScanRequest.TemplateId;
            messageHeader.Version = MarketCrossScanRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LookbackInSeconds = lookbackInSeconds;
            message.MinMarketCross = minMarketCross;
            message.CurrentMarketWidth = currentMarketWidth;

            return message.Limit - offset;
        }

        public int EncodeMarketCrossScanResponseMessage(DirectBuffer directBuffer, int offset, int requestId, bool found, List<Data.Responses.MarketCrossScanResult> snapshots)
        {
            int count = snapshots.Count;
            int size = MessageHeader.Size + MarketCrossScanResponse.BlockLength + MarketCrossScanResponse.SnapshotsGroup.SbeHeaderSize + (count * (MarketCrossScanResponse.SnapshotsGroup.SbeBlockLength + 200));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MarketCrossScanResponse message = new MarketCrossScanResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MarketCrossScanResponse.BlockLength;
            messageHeader.SchemaId = MarketCrossScanResponse.SchemaId;
            messageHeader.TemplateId = MarketCrossScanResponse.TemplateId;
            messageHeader.Version = MarketCrossScanResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.UpdateFound = found ? BooleanEnum.True : BooleanEnum.False;

            MarketCrossScanResponse.SnapshotsGroup nextSnapshot = message.SnapshotsCount(count);
            foreach (Data.Responses.MarketCrossScanResult snapshot in snapshots)
            {
                nextSnapshot.Next();
                nextSnapshot.HighestBid = snapshot.HighestBid;
                nextSnapshot.LowestAsk = snapshot.LowestAsk;
                nextSnapshot.UnderMid = snapshot.UnderMid;
                nextSnapshot.SetSymbol(snapshot.Symbol ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeBestEdgeToTheoRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string underlying, Data.Enums.BaseStrategy baseStrategy, int expirationIds)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            BestEdgeToTheoRequest message = new BestEdgeToTheoRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = BestEdgeToTheoRequest.BlockLength;
            messageHeader.SchemaId = BestEdgeToTheoRequest.SchemaId;
            messageHeader.TemplateId = BestEdgeToTheoRequest.TemplateId;
            messageHeader.Version = BestEdgeToTheoRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.BaseStrategy = (Generated.BaseStrategy)baseStrategy;
            message.ExpirationId = expirationIds;
            message.SetUnderlyingSymbol(underlying ?? "");

            return message.Limit - offset;
        }

        public int EncodeBestEdgeToTheoResponseMessage(DirectBuffer directBuffer, int offset, int requestId, double bestBuyEdgeToTheo, double avgBuyEdgeToTheo, double lastBuyEdgeToTheo, DateTime lastBuyEdgeToTheoTime, double bestSellEdgeToTheo, double avgSellEdgeToTheo, double lastSellEdgeToTheo, DateTime lastSellEdgeToTheoTime)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            BestEdgeToTheoResponse message = new BestEdgeToTheoResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = BestEdgeToTheoResponse.BlockLength;
            messageHeader.SchemaId = BestEdgeToTheoResponse.SchemaId;
            messageHeader.TemplateId = BestEdgeToTheoResponse.TemplateId;
            messageHeader.Version = BestEdgeToTheoResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            message.BestBuyEdgeToTheo = bestBuyEdgeToTheo;
            message.AvgBuyEdgeToTheo = avgBuyEdgeToTheo;
            message.LastBuyEdgeToTheo = lastBuyEdgeToTheo;
            message.LastBuyEdgeToTheoTime = ToUnixEpoch(lastBuyEdgeToTheoTime);

            message.BestSellEdgeToTheo = bestSellEdgeToTheo;
            message.AvgSellEdgeToTheo = avgSellEdgeToTheo;
            message.LastSellEdgeToTheo = lastSellEdgeToTheo;
            message.LastSellEdgeToTheoTime = ToUnixEpoch(lastSellEdgeToTheoTime);

            return message.Limit - offset;
        }

        public int EncodeSymbolTradeRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol)
        {
            symbol ??= "";

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolTradeRequestMessage message = new SymbolTradeRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolTradeRequestMessage.BlockLength;
            messageHeader.SchemaId = SymbolTradeRequestMessage.SchemaId;
            messageHeader.TemplateId = SymbolTradeRequestMessage.TemplateId;
            messageHeader.Version = SymbolTradeRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetSymbol(symbol);

            return message.Limit - offset;
        }

        public int EncodeSymbolTradeResponseMessage(DirectBuffer directBuffer, int offset, int requestId, FishStatus fishStatus, double fishLevel, double fishEdge, double fishLevelSell, double fishEdgeSell, DateTime lastfishTime)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolTradeResponseMessage message = new SymbolTradeResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolTradeResponseMessage.BlockLength;
            messageHeader.SchemaId = SymbolTradeResponseMessage.SchemaId;
            messageHeader.TemplateId = SymbolTradeResponseMessage.TemplateId;
            messageHeader.Version = SymbolTradeResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.FishStatus = fishStatus;
            message.FishLevelBuy = fishLevel;
            message.FishEdgeBuy = fishEdge;
            message.FishLevelSell = fishLevelSell;
            message.FishEdgeSell = fishEdgeSell;
            message.LastFishTime = ToUnixEpoch(lastfishTime);

            return message.Limit - offset;
        }

        public int EncodeSymbolsTradeRequestMessage(DirectBuffer directBuffer, int offset, int requestId, bool includeOutrights, bool includeSpreads, bool includeBackdays, DateTime lastDateToInclude, string underlyings, string symbols, string tags)
        {
            tags ??= "";
            symbols ??= "";
            underlyings ??= "";

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolsTradeRequestMessage message = new SymbolsTradeRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolsTradeRequestMessage.BlockLength;
            messageHeader.SchemaId = SymbolsTradeRequestMessage.SchemaId;
            messageHeader.TemplateId = SymbolsTradeRequestMessage.TemplateId;
            messageHeader.Version = SymbolsTradeRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.IncludeOutrights = includeOutrights ? BooleanEnum.True : BooleanEnum.False;
            message.IncludeSpreads = includeSpreads ? BooleanEnum.True : BooleanEnum.False;
            message.IncludeSpreads = includeBackdays ? BooleanEnum.True : BooleanEnum.False;
            message.LastDate = ToUnixEpoch(lastDateToInclude);
            message.SetUnderlyings(underlyings);
            message.SetSymbols(symbols);
            message.SetTags(tags);

            return message.Limit - offset;
        }

        public int EncodeSymbolTradesResponseMessage(DirectBuffer directBuffer, int offset, int requestId, List<Data.Responses.SymbolFishStatusResponse> responses, bool lastMessage)
        {
            int count = responses.Count;
            int size = MessageHeader.Size + SymbolsTradeResponseMessage.BlockLength + SymbolsTradeResponseMessage.TradedSymbolsGroup.SbeHeaderSize + (count * SymbolsTradeResponseMessage.TradedSymbolsGroup.SbeBlockLength) + responses.Sum(x => GetBufferEstimate(x.Symbol));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolsTradeResponseMessage message = new SymbolsTradeResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolsTradeResponseMessage.BlockLength;
            messageHeader.SchemaId = SymbolsTradeResponseMessage.SchemaId;
            messageHeader.TemplateId = SymbolsTradeResponseMessage.TemplateId;
            messageHeader.Version = SymbolsTradeResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastMessage ? BooleanEnum.True : BooleanEnum.False;

            SymbolsTradeResponseMessage.TradedSymbolsGroup symbolsGroup = message.TradedSymbolsCount(count);
            foreach (Data.Responses.SymbolFishStatusResponse response in responses)
            {
                symbolsGroup.Next();
                symbolsGroup.FishStatus = response.FishStatus;
                symbolsGroup.FishLevelBuy = response.FishLevel;
                symbolsGroup.FishEdgeBuy = response.FishEdge;
                symbolsGroup.FishLevelSell = response.FishLevelSell;
                symbolsGroup.FishEdgeSell = response.FishEdgeSell;
                symbolsGroup.LastFishTime = ToUnixEpoch(response.LastFishTime);
                symbolsGroup.SetSymbol(response.Symbol ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedModel(DirectBuffer directBuffer, int offset, IEdgeScanFeedModel model, string? sessionId = null, string? message = null, string? reason = null)
        {
            string exchange = model.Exchange ?? "";
            string description = model.Description ?? "";
            string spreadId = model.SpreadId ?? "";
            string spreadType = model.SpreadType ?? "";
            string buySymbol = model.BuySymbol ?? "";
            string sellSymbol = model.SellSymbol ?? "";
            string underSymbol = model.UnderSymbol ?? "";
            string extraTag = model.ExtraTag ?? "";
            sessionId ??= "";
            string messageText = message ?? model.Message ?? "";
            string reasonText = reason ?? model.Reason ?? "";

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            EdgeScanFeedModelMessage sbeMessage = new EdgeScanFeedModelMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedModelMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedModelMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedModelMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedModelMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            sbeMessage.WrapForEncode(directBuffer, bufferOffset);

            sbeMessage.IsFirm = model.IsFirm ? BooleanEnum.True : BooleanEnum.False;
            sbeMessage.PossibleFirm = model.PossibleFirm ? BooleanEnum.True : BooleanEnum.False;
            sbeMessage.PossibleCopyCat = model.PossibleCopyCat ? BooleanEnum.True : BooleanEnum.False;
            sbeMessage.Uncertain = model.Uncertain ? BooleanEnum.True : BooleanEnum.False;

            sbeMessage.QtyMismatch = model.QtyMismatch ? BooleanEnum.True : BooleanEnum.False;

            sbeMessage.ScannerId = (byte)model.EdgeScannerType;

            sbeMessage.BuyConditionCode = (byte)model.BuyConditionCode;
            sbeMessage.SellConditionCode = (byte)model.SellConditionCode;

            sbeMessage.AdjSide = (Generated.Side)model.AdjSide;
            sbeMessage.IbCobSide = (Generated.Side)model.IbCobSide;

            sbeMessage.LegsCount = model.LegsCount;

            sbeMessage.BuyQty = model.BuyQty;
            sbeMessage.SellQty = model.SellQty;

            sbeMessage.BuyBidSize = model.BuyBidSize;
            sbeMessage.BuyAskSize = model.BuyAskSize;
            sbeMessage.SellBidSize = model.SellBidSize;
            sbeMessage.SellAskSize = model.SellAskSize;

            sbeMessage.FlipCount = model.FlipCount;

            sbeMessage.IbCobBid.Mantissa = EncodeMantissa(model.IbCobBid, sbeMessage.IbCobBid.Exponent);
            sbeMessage.IbCobAsk.Mantissa = EncodeMantissa(model.IbCobAsk, sbeMessage.IbCobAsk.Exponent);
            sbeMessage.AdjustedPnl.Mantissa = EncodeMantissa(model.AdjustedPnl, sbeMessage.AdjustedPnl.Exponent);
            sbeMessage.BuyPrice.Mantissa = EncodeMantissa(model.BuyPrice, sbeMessage.BuyPrice.Exponent);
            sbeMessage.BuyTradeOriginalPrice.Mantissa = EncodeMantissa(model.BuyTradeOriginalPrice, sbeMessage.BuyTradeOriginalPrice.Exponent);
            sbeMessage.SellPrice.Mantissa = EncodeMantissa(model.SellPrice, sbeMessage.SellPrice.Exponent);
            sbeMessage.SellTradeOriginalPrice.Mantissa = EncodeMantissa(model.SellTradeOriginalPrice, sbeMessage.SellTradeOriginalPrice.Exponent);
            sbeMessage.BuyEdgeToTheo.Mantissa = EncodeMantissa(model.BuyEdgeToTheo, sbeMessage.BuyEdgeToTheo.Exponent);
            sbeMessage.BuyVolaEdgeToTheo.Mantissa = EncodeMantissa(model.BuyVolaEdgeToTheo, sbeMessage.BuyVolaEdgeToTheo.Exponent);
            sbeMessage.SellEdgeToTheo.Mantissa = EncodeMantissa(model.SellEdgeToTheo, sbeMessage.SellEdgeToTheo.Exponent);
            sbeMessage.SellVolaEdgeToTheo.Mantissa = EncodeMantissa(model.SellVolaEdgeToTheo, sbeMessage.SellVolaEdgeToTheo.Exponent);
            sbeMessage.Ttl.Mantissa = EncodeMantissa(model.Ttl, sbeMessage.Ttl.Exponent);
            sbeMessage.SpreadWidth.Mantissa = EncodeMantissa(model.SpreadWidth, sbeMessage.SpreadWidth.Exponent);
            sbeMessage.BuyTradeBid.Mantissa = EncodeMantissa(model.BuyTradeBid, sbeMessage.BuyTradeBid.Exponent);
            sbeMessage.BuyTradeMid.Mantissa = EncodeMantissa(model.BuyTradeMid, sbeMessage.BuyTradeMid.Exponent);
            sbeMessage.BuyTradeAsk.Mantissa = EncodeMantissa(model.BuyTradeAsk, sbeMessage.BuyTradeAsk.Exponent);
            sbeMessage.BuyTradeTheo.Mantissa = EncodeMantissa(model.BuyTradeTheo, sbeMessage.BuyTradeTheo.Exponent);
            sbeMessage.BuyTradeDelta.Mantissa = EncodeMantissa(model.BuyTradeDelta, sbeMessage.BuyTradeDelta.Exponent);
            sbeMessage.SellTradeBid.Mantissa = EncodeMantissa(model.SellTradeBid, sbeMessage.SellTradeBid.Exponent);
            sbeMessage.SellTradeMid.Mantissa = EncodeMantissa(model.SellTradeMid, sbeMessage.SellTradeMid.Exponent);
            sbeMessage.SellTradeAsk.Mantissa = EncodeMantissa(model.SellTradeAsk, sbeMessage.SellTradeAsk.Exponent);
            sbeMessage.SellTradeTheo.Mantissa = EncodeMantissa(model.SellTradeTheo, sbeMessage.SellTradeTheo.Exponent);
            sbeMessage.SellTradeDelta.Mantissa = EncodeMantissa(model.SellTradeDelta, sbeMessage.SellTradeDelta.Exponent);
            sbeMessage.BuyTradeUnderlyingMid.Mantissa = EncodeMantissa(model.BuyTradeUnderlyingMid, sbeMessage.BuyTradeUnderlyingMid.Exponent);
            sbeMessage.SellTradeUnderlyingMid.Mantissa = EncodeMantissa(model.SellTradeUnderlyingMid, sbeMessage.SellTradeUnderlyingMid.Exponent);
            sbeMessage.BuyUnderlyingWidth.Mantissa = EncodeMantissa(model.BuyUnderlyingWidth, sbeMessage.BuyUnderlyingWidth.Exponent);
            sbeMessage.SellUnderlyingWidth.Mantissa = EncodeMantissa(model.SellUnderlyingWidth, sbeMessage.SellUnderlyingWidth.Exponent);
            sbeMessage.DeltaAdjEdge.Mantissa = EncodeMantissa(model.DeltaAdjEdge, sbeMessage.DeltaAdjEdge.Exponent);
            sbeMessage.HighestLegDelta.Mantissa = EncodeMantissa(model.HighestLegDelta, sbeMessage.HighestLegDelta.Exponent);
            sbeMessage.SpreadWeightedVega.Mantissa = EncodeMantissa(model.SpreadWeightedVega, sbeMessage.SpreadWeightedVega.Exponent);
            sbeMessage.ReceiveLatency.Mantissa = EncodeMantissa(model.ReceiveLatency, sbeMessage.ReceiveLatency.Exponent);
            sbeMessage.IvPctChange.Mantissa = EncodeMantissa(model.IvPctChange, sbeMessage.IvPctChange.Exponent);

            sbeMessage.BuyTime = ToUnixEpoch(model.BuyTime);
            sbeMessage.SellTime = ToUnixEpoch(model.SellTime);
            sbeMessage.NearExpiration = ToUnixEpoch(model.NearExpiration);
            sbeMessage.FarExpiration = ToUnixEpoch(model.FarExpiration);

            sbeMessage.SetUnderSymbol(underSymbol);
            sbeMessage.SetSpreadId(spreadId);
            sbeMessage.SetSpreadType(spreadType);
            sbeMessage.SetBuySymbol(buySymbol);
            sbeMessage.SetSellSymbol(sellSymbol);
            sbeMessage.SetExtraTag(extraTag);
            sbeMessage.SetExchange(exchange);
            sbeMessage.SetSessionId(sessionId);
            sbeMessage.SetDescription(description);
            sbeMessage.SetMessage(messageText);
            sbeMessage.SetReason(reasonText);

            return sbeMessage.Limit - offset;
        }

        public int EncodeTradeFeedModel(DirectBuffer directBuffer, int offset, int id, List<ITradeFeedModel> models, bool isLast)
        {
            models.Select(x => x.Underlying).Sum(underlying => GetBufferEstimate(underlying));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TradeFeedMessage message = new TradeFeedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TradeFeedMessage.BlockLength;
            messageHeader.SchemaId = TradeFeedMessage.SchemaId;
            messageHeader.TemplateId = TradeFeedMessage.TemplateId;
            messageHeader.Version = TradeFeedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);

            message.Id = id;
            message.IsLast = isLast ? BooleanEnum.True : BooleanEnum.False;
            int count = models.Count;

            TradeFeedMessage.TradesGroup tradeGroup = message.TradesCount(count);
            foreach (ITradeFeedModel model in models)
            {
                tradeGroup.Next();

                tradeGroup.IsFirm = model.IsFirm ? BooleanEnum.True : BooleanEnum.False;
                tradeGroup.IsCopyCat = model.IsCopyCat ? BooleanEnum.True : BooleanEnum.False;

                tradeGroup.Quantity = model.Quantity;
                tradeGroup.BaseStrategy = (Generated.BaseStrategy)model.BaseStrategy;
                tradeGroup.Side = (Generated.Side)model.Side;
                tradeGroup.Price = model.Price;
                tradeGroup.Bid = model.Bid;
                tradeGroup.Ask = model.Ask;
                tradeGroup.Delta = model.Delta;
                tradeGroup.TradeTime = model.TradeTime.ToUnixEpoch();

                tradeGroup.SetExchange(model.Exchange ?? "");
                tradeGroup.SetDescription(model.Description ?? "");
                tradeGroup.SetDescription(model.Underlying ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeTimeUpdateMessage(DirectBuffer directBuffer, int offset, TimeFeedType timeFeedType, DateTime timestamp)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TimeUpdateMessage message = new TimeUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TimeUpdateMessage.BlockLength;
            messageHeader.SchemaId = TimeUpdateMessage.SchemaId;
            messageHeader.TemplateId = TimeUpdateMessage.TemplateId;
            messageHeader.Version = TimeUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);

            message.UpdateType = (short)timeFeedType;
            message.Timestamp = ToUnixEpoch(timestamp);

            return message.Limit - offset;
        }

        public int EncodeTradeUpdate(DirectBuffer directBuffer, int offset, ref TradeUpdateModel lastTrade)
        {
            string spreadId = lastTrade.SpreadId ?? "";
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TradeUpdateMessage message = new TradeUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TradeUpdateMessage.BlockLength;
            messageHeader.SchemaId = TradeUpdateMessage.SchemaId;
            messageHeader.TemplateId = TradeUpdateMessage.TemplateId;
            messageHeader.Version = TradeUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);

            message.Side = lastTrade.Side == Side.Buy ? Generated.Side.Buy : Generated.Side.Sell;
            message.Qty = lastTrade.Qty;
            message.Price = lastTrade.Price;
            message.UnderBid = lastTrade.UnderBid;
            message.UnderAsk = lastTrade.UnderAsk;
            message.SetSpreadId(spreadId);

            return message.Limit - offset;
        }

        public int EncodeSecurityEmaUpdate(DirectBuffer directBuffer, int offset, int tickerId, SubscriptionFieldType type, EmaUpdateModel emaUpdateModel)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SecurityEmaUpdate.BlockLength;
            messageHeader.SchemaId = SecurityEmaUpdate.SchemaId;
            messageHeader.TemplateId = SecurityEmaUpdate.TemplateId;
            messageHeader.Version = SecurityEmaUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SecurityEmaUpdate message = new SecurityEmaUpdate();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.UpdateType = (ushort)type;

            message.SetTickerId(0, (byte)(tickerId >> 16));
            message.SetTickerId(1, (byte)(tickerId >> 8));
            message.SetTickerId(2, (byte)tickerId);

            var sequence = emaUpdateModel.Sequence;
            message.SetSequenceNumber(0, (byte)(sequence >> 16));
            message.SetSequenceNumber(1, (byte)(sequence >> 8));
            message.SetSequenceNumber(2, (byte)sequence);

            message.LowPeriodEma.Mantissa = EncodeMantissa(emaUpdateModel.LowPeriodEma, message.LowPeriodEma.Exponent);
            message.LowPeriodEmaAdj.Mantissa = EncodeMantissa(emaUpdateModel.LowPeriodEmaAdj, message.LowPeriodEmaAdj.Exponent);
            message.LowPeriodEmaUnderlying.Mantissa = EncodeMantissa(emaUpdateModel.LowPeriodEmaUnderlying, message.LowPeriodEmaUnderlying.Exponent);
            message.MidPeriodEma.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodEma, message.MidPeriodEma.Exponent);
            message.MidPeriodEmaAdj.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodEmaAdj, message.MidPeriodEmaAdj.Exponent);
            message.MidPeriodEmaUnderlying.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodEmaUnderlying, message.MidPeriodEmaUnderlying.Exponent);
            message.HighPeriodEma.Mantissa = EncodeMantissa(emaUpdateModel.HighPeriodEma, message.HighPeriodEma.Exponent);
            message.HighPeriodEmaAdj.Mantissa = EncodeMantissa(emaUpdateModel.HighPeriodEmaAdj, message.HighPeriodEmaAdj.Exponent);
            message.HighPeriodEmaUnderlying.Mantissa = EncodeMantissa(emaUpdateModel.HighPeriodEmaUnderlying, message.HighPeriodEmaUnderlying.Exponent);
            message.MidPeriodBidEma.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodBidEma, message.MidPeriodBidEma.Exponent);
            message.MidPeriodBidEmaAdj.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodBidEmaAdj, message.MidPeriodBidEmaAdj.Exponent);
            message.MidPeriodAskEma.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodAskEma, message.MidPeriodAskEma.Exponent);
            message.MidPeriodAskEmaAdj.Mantissa = EncodeMantissa(emaUpdateModel.MidPeriodAskEmaAdj, message.MidPeriodAskEmaAdj.Exponent);

            message.QuoteTimestampNanos = emaUpdateModel.QuoteTimestampNanos;
            message.CalculationTimestampNanos = emaUpdateModel.CalculationTimestampNanos;
            message.LowPeriodEmaTimestampNanos = emaUpdateModel.LowPeriodEmaTimestampNanos;
            message.MidPeriodEmaTimestampNanos = emaUpdateModel.MidPeriodEmaTimestampNanos;
            message.HighPeriodEmaTimestampNanos = emaUpdateModel.HighPeriodEmaTimestampNanos;

            return message.Limit - offset;
        }

        public int EncodeSingleOrderRequestMessage(DirectBuffer directBuffer, int offset, Data.Requests.SingleOrderRequest request)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SingleOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = SingleOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = SingleOrderRequestMessage.TemplateId;
            messageHeader.Version = SingleOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SingleOrderRequestMessage message = new SingleOrderRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.OrderType = (Generated.OrderType)request.OrderType;
            message.Staged = request.Staged ? BooleanEnum.True : BooleanEnum.False;
            message.ClaimRequire = request.ClaimRequire ? BooleanEnum.True : BooleanEnum.False;

            message.Price = request.Price;
            message.Quantity = request.Quantity;
            message.Side = (Generated.Side)request.Side;

            message.SetSymbol(request.Symbol ?? "");
            message.SetAccount(request.Account ?? "");
            message.SetRoute(request.Route ?? "");
            message.SetTag(request.Tag ?? "");
            message.SetClientOrderId(request.ClientOrderId ?? "");
            message.SetLocate(request.Locate ?? "");

            return message.Limit - offset;
        }

        public int EncodePairOrderRequestMessage(DirectBuffer directBuffer, int offset, Data.Requests.PairOrderRequest request)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PairOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = PairOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = PairOrderRequestMessage.TemplateId;
            messageHeader.Version = PairOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            PairOrderRequestMessage message = new PairOrderRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.PairOrderRequestType = (Generated.PairOrderRequestType)request.PairOrderRequestType;
            message.OrderType = (Generated.OrderType)request.OrderType;
            message.TriggerValue = request.TriggerValue;
            message.BuyTermsRatio = request.BuyTermsRatio;
            message.SellTermsRatio = request.SellTermsRatio;
            message.InitSide = (byte)request.InitSide;
            message.TimeInForce = (Generated.TimeInForce)request.TimeInForce;
            message.Staged = request.Staged ? BooleanEnum.True : BooleanEnum.False;
            message.ClaimRequire = request.ClaimRequire ? BooleanEnum.True : BooleanEnum.False;

            message.Leg1Quantity = request.Leg1Quantity;
            message.Leg1Side = (Generated.Side)request.Leg1Side;

            message.Leg2Quantity = request.Leg2Quantity;
            message.Leg2Side = (Generated.Side)request.Leg2Side;

            message.SetLeg1Symbol(request.Leg1Symbol ?? "");
            message.SetLeg2Symbol(request.Leg2Symbol ?? "");

            message.SetAccount(request.Account ?? "");
            message.SetRoute(request.Route ?? "");
            message.SetTag(request.Tag ?? "");

            message.SetClientOrderId(request.ClientOrderId ?? "");
            message.SetClientOrderIdLeg1(request.ClientOrderIdLeg1 ?? "");
            message.SetClientOrderIdLeg2(request.ClientOrderIdLeg2 ?? "");

            message.SetLocate(request.Locate ?? "");
            message.SetStyle(request.Style ?? "");
            message.SetTriggerMethod(request.TriggerMethod ?? "");
            message.SetTriggerValueCurrency(request.TriggerValueCurrency ?? "");

            message.UserId = request.UserId;
            message.RiskCheckId = request.RiskCheckId;

            return message.Limit - offset;
        }

        private static ulong ToUnixEpoch(DateTime dateTime)
        {
            try
            {
                return dateTime == default ? ToUnixEpoch(DateTime.UnixEpoch) : (ulong)((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
            }
            catch (Exception)
            {
                return ToUnixEpoch(DateTime.UnixEpoch);
            }
        }

        /// <summary>
        /// Computes the byte size needed for a variable-length SBE string field:
        /// max var-data header (4 bytes) + the actual UTF-8 encoded byte count.
        /// Uses 4-byte header as safe upper bound (most fields use 2-byte headers).
        /// </summary>
        private static int GetBufferEstimate(string? content)
        {
            const int maxSbeVarDataHeaderSize = 4;
            if (content == null || content.Length == 0)
            {
                return maxSbeVarDataHeaderSize;
            }
            return maxSbeVarDataHeaderSize + System.Text.Encoding.UTF8.GetByteCount(content);
        }


        public int EncodeAccountRequestMessage(DirectBuffer directBuffer, int offset, int requestId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AccountRequestMessage message = new AccountRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AccountRequestMessage.BlockLength;
            messageHeader.SchemaId = AccountRequestMessage.SchemaId;
            messageHeader.TemplateId = AccountRequestMessage.TemplateId;
            messageHeader.Version = AccountRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;

            return message.Limit - offset;
        }

        public int EncodeAccountResponseMessage(DirectBuffer directBuffer, int offset, int requestId, List<Account> accounts)
        {
            int count = accounts.Count;
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AccountResponseMessage message = new AccountResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AccountResponseMessage.BlockLength;
            messageHeader.SchemaId = AccountResponseMessage.SchemaId;
            messageHeader.TemplateId = AccountResponseMessage.TemplateId;
            messageHeader.Version = AccountResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.Count = count;

            AccountResponseMessage.AccountsGroup accountsGroup = message.AccountsCount(count);
            foreach (Account account in accounts)
            {
                accountsGroup.Next();
                accountsGroup.Id = account.Id;
                accountsGroup.AccountId = account.AccountId;
                AccountResponseMessage.AccountsGroup.RoutesGroup routesGroup = accountsGroup.RoutesCount(account.Routes.Count);
                foreach (var routingInfo in account.Routes)
                {
                    routesGroup.Next();
                    routesGroup.Id = routingInfo.Id;
                    routesGroup.Venue_Id = routingInfo.VenueId;
                    routesGroup.OrderRoute_Id = routingInfo.OrderRouteId;
                    routesGroup.OrderType_Id = routingInfo.OrderTypeId;
                    routesGroup.Broker_Id = routingInfo.BrokerId;
                    routesGroup.RouteType_Id = routingInfo.RouteTypeId;
                    routesGroup.Route_Id = routingInfo.RouteId;
                    routesGroup.Active = routingInfo.Active ? BooleanEnum.True : BooleanEnum.False;
                    routesGroup.SetVenue(routingInfo.Venue ?? "");
                    routesGroup.SetOrderType(routingInfo.OrderType ?? "");
                    routesGroup.SetBroker(routingInfo.Broker ?? "");
                    routesGroup.SetRouteType(routingInfo.RouteType ?? "");
                    routesGroup.SetRoute(routingInfo.Route ?? "");
                    routesGroup.SetExpectedName(routingInfo.ExpectedName ?? "");
                    routesGroup.SetFixExpectedName(routingInfo.FixExpectedName ?? "");
                }
                accountsGroup.SetAcronym(account.Acronym ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeOrderInfoUpdate(DirectBuffer directBuffer, int offset, OrderInfoUpdate orderInfo)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderInfoUpdateMessage message = new OrderInfoUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderInfoUpdateMessage.BlockLength;
            messageHeader.SchemaId = OrderInfoUpdateMessage.SchemaId;
            messageHeader.TemplateId = OrderInfoUpdateMessage.TemplateId;
            messageHeader.Version = OrderInfoUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SpreadNumLegs = orderInfo.SpreadNumLegs;
            message.SpreadLegCount = orderInfo.SpreadLegCount;
            message.Minmove = orderInfo.Minmove;
            message.RemainingVolume = orderInfo.RemainingVolume;
            message.OrderResidual = orderInfo.OrderResidual;
            message.VolumeTraded = orderInfo.VolumeTraded;
            message.SpreadLegNumber = orderInfo.SpreadLegNumber;
            message.PairImbalanceLimitType = orderInfo.PairImbalanceLimitType;
            message.UtcOffset = orderInfo.UtcOffset;
            message.OriginalVolume = orderInfo.OriginalVolume;
            message.Volume = orderInfo.Volume;
            message.WorkingQty = orderInfo.WorkingQty;

            message.Side = (Generated.Side)orderInfo.Side;
            message.OrderType = (Generated.OrderType)orderInfo.OrderType;
            message.TimeInForce = (Generated.TimeInForce)orderInfo.TimeInForce;
            message.OrderStatus = (Generated.OrderStatus)orderInfo.OrderStatus;

            message.Ask = orderInfo.Ask;
            message.Bid = orderInfo.Bid;
            message.Price = orderInfo.Price;
            message.PairTarget = orderInfo.PairTarget;
            message.PairLeg2Benchmark = orderInfo.PairLeg2Benchmark;
            message.PairLeg1Benchmark = orderInfo.PairLeg1Benchmark;
            message.PairImbalanceLimit = orderInfo.PairImbalanceLimit;
            message.PairCash = orderInfo.PairCash;
            message.PairRatio = orderInfo.PairRatio;
            message.Latency6 = orderInfo.Latency6;
            message.Latency3 = orderInfo.Latency3;
            message.Basisvalue = orderInfo.Basisvalue;
            message.OriginalPrice = orderInfo.OriginalPrice;
            message.StrikePrc = orderInfo.StrikePrc;
            message.StopPrice = orderInfo.StopPrice;
            message.ServerArrivalPrice = orderInfo.ServerArrivalPrice;

            message.TimeStamp = orderInfo.TimeStamp.ToUnixEpoch();
            message.SubmitTime = orderInfo.SubmitTime.ToUnixEpoch();
            message.NewsDate = orderInfo.NewsDate.ToUnixEpoch();
            message.ExpirDate = orderInfo.ExpirDate.ToUnixEpoch();
            message.NewsTime = orderInfo.NewsTime.ToUnixEpoch();
            message.TrdTime = orderInfo.TrdTime.ToUnixEpoch();

            OrderInfoUpdateMessage.LegsGroup legUpdateGroup = message.LegsCount(orderInfo.ChildOrderInfoUpdates.Count);
            foreach (OrderInfoUpdate legUpdate in orderInfo.ChildOrderInfoUpdates)
            {
                legUpdateGroup.Next();
                legUpdateGroup.SpreadNumLegs = legUpdate.SpreadNumLegs;
                legUpdateGroup.SpreadLegCount = legUpdate.SpreadLegCount;
                legUpdateGroup.Minmove = legUpdate.Minmove;
                legUpdateGroup.RemainingVolume = legUpdate.RemainingVolume;
                legUpdateGroup.OrderResidual = legUpdate.OrderResidual;
                legUpdateGroup.VolumeTraded = legUpdate.VolumeTraded;
                legUpdateGroup.SpreadLegNumber = legUpdate.SpreadLegNumber;
                legUpdateGroup.PairImbalanceLimitType = legUpdate.PairImbalanceLimitType;
                legUpdateGroup.UtcOffset = legUpdate.UtcOffset;
                legUpdateGroup.OriginalVolume = legUpdate.OriginalVolume;
                legUpdateGroup.Volume = legUpdate.Volume;
                legUpdateGroup.WorkingQty = legUpdate.WorkingQty;

                legUpdateGroup.Side = (Generated.Side)legUpdate.Side;
                legUpdateGroup.OrderType = (Generated.OrderType)legUpdate.OrderType;
                legUpdateGroup.TimeInForce = (Generated.TimeInForce)legUpdate.TimeInForce;
                legUpdateGroup.OrderStatus = (Generated.OrderStatus)legUpdate.OrderStatus;

                legUpdateGroup.Ask = legUpdate.Ask;
                legUpdateGroup.Bid = legUpdate.Bid;
                legUpdateGroup.Price = legUpdate.Price;
                legUpdateGroup.PairTarget = legUpdate.PairTarget;
                legUpdateGroup.PairLeg2Benchmark = legUpdate.PairLeg2Benchmark;
                legUpdateGroup.PairLeg1Benchmark = legUpdate.PairLeg1Benchmark;
                legUpdateGroup.PairImbalanceLimit = legUpdate.PairImbalanceLimit;
                legUpdateGroup.PairCash = legUpdate.PairCash;
                legUpdateGroup.PairRatio = legUpdate.PairRatio;
                legUpdateGroup.Latency6 = legUpdate.Latency6;
                legUpdateGroup.Latency3 = legUpdate.Latency3;
                legUpdateGroup.Basisvalue = legUpdate.Basisvalue;
                legUpdateGroup.OriginalPrice = legUpdate.OriginalPrice;
                legUpdateGroup.StrikePrc = legUpdate.StrikePrc;
                legUpdateGroup.StopPrice = legUpdate.StopPrice;
                legUpdateGroup.ServerArrivalPrice = legUpdate.ServerArrivalPrice;

                legUpdateGroup.TimeStamp = legUpdate.TimeStamp.ToUnixEpoch();
                legUpdateGroup.SubmitTime = legUpdate.SubmitTime.ToUnixEpoch();
                legUpdateGroup.NewsDate = legUpdate.NewsDate.ToUnixEpoch();
                legUpdateGroup.ExpirDate = legUpdate.ExpirDate.ToUnixEpoch();
                legUpdateGroup.NewsTime = legUpdate.NewsTime.ToUnixEpoch();
                legUpdateGroup.TrdTime = legUpdate.TrdTime.ToUnixEpoch();

                legUpdateGroup.SetBookingType(legUpdate.BookingType ?? string.Empty);
                legUpdateGroup.SetRefMgrNotes(legUpdate.RefMgrNotes ?? string.Empty);
                legUpdateGroup.SetPairSpreadType(legUpdate.PairSpreadType ?? string.Empty);
                legUpdateGroup.SetReason(legUpdate.Reason ?? string.Empty);
                legUpdateGroup.SetLinkedOrderCancellation(legUpdate.LinkedOrderCancellation ?? string.Empty);
                legUpdateGroup.SetLinkedOrderRelationship(legUpdate.LinkedOrderRelationship ?? string.Empty);
                legUpdateGroup.SetCommissionRateType(legUpdate.CommissionRateType ?? string.Empty);
                legUpdateGroup.SetAccount(legUpdate.Account ?? string.Empty);
                legUpdateGroup.SetRoute(legUpdate.Route ?? string.Empty);
                legUpdateGroup.SetOrderId(legUpdate.OrderId ?? string.Empty);
                legUpdateGroup.SetLinkedOrderId(legUpdate.LinkedOrderId ?? string.Empty);
                legUpdateGroup.SetRefersToId(legUpdate.RefersToId ?? string.Empty);
                legUpdateGroup.SetTicketId(legUpdate.TicketId ?? string.Empty);
                legUpdateGroup.SetOriginalOrderId(legUpdate.OriginalOrderId ?? string.Empty);
                legUpdateGroup.SetSymbol(legUpdate.Symbol ?? string.Empty);
                legUpdateGroup.SetType(legUpdate.Type ?? string.Empty);
                legUpdateGroup.SetCurrentStatus(legUpdate.CurrentStatus ?? string.Empty);
                legUpdateGroup.SetTraderId(legUpdate.TraderId ?? string.Empty);
                legUpdateGroup.SetClaimedByClerk(legUpdate.ClaimedByClerk ?? string.Empty);
                legUpdateGroup.SetSpreadLegPriceType(legUpdate.SpreadLegPriceType ?? string.Empty);
                legUpdateGroup.SetSpreadLegLeanPriority(legUpdate.SpreadLegLeanPriority ?? string.Empty);
                legUpdateGroup.SetOrderFlags(legUpdate.OrderFlags ?? string.Empty);
                legUpdateGroup.SetFornexSourceFlags(legUpdate.FornexSourceFlags ?? string.Empty);
                legUpdateGroup.SetExternalAcceptanceFlag(legUpdate.ExternalAcceptanceFlag ?? string.Empty);
                legUpdateGroup.SetExtendedStateFlags2(legUpdate.ExtendedStateFlags2 ?? string.Empty);
                legUpdateGroup.SetExtendedStateFlags(legUpdate.ExtendedStateFlags ?? string.Empty);
                legUpdateGroup.SetCrossFlag(legUpdate.CrossFlag ?? string.Empty);
                legUpdateGroup.SetSpreadClipType(legUpdate.SpreadClipType ?? string.Empty);
                legUpdateGroup.SetPairLeg2BenchmarkType(legUpdate.PairLeg2BenchmarkType ?? string.Empty);
                legUpdateGroup.SetPairLeg1BenchmarkType(legUpdate.PairLeg1BenchmarkType ?? string.Empty);
                legUpdateGroup.SetSharesAllocated(legUpdate.SharesAllocated ?? string.Empty);
                legUpdateGroup.SetOrderFlags2(legUpdate.OrderFlags2 ?? string.Empty);
                legUpdateGroup.SetAcctType(legUpdate.AcctType ?? string.Empty);
                legUpdateGroup.SetRank(legUpdate.Rank ?? string.Empty);
                legUpdateGroup.SetGwBookSeqNo(legUpdate.GwBookSeqNo ?? string.Empty);
                legUpdateGroup.SetDateIndex(legUpdate.DateIndex ?? string.Empty);
                legUpdateGroup.SetBookId(legUpdate.BookId ?? string.Empty);
                legUpdateGroup.SetTboAccountId(legUpdate.TboAccountId ?? string.Empty);
                legUpdateGroup.SetOmsClientType(legUpdate.OmsClientType ?? string.Empty);
                legUpdateGroup.SetExecutionState(legUpdate.ExecutionState ?? string.Empty);
                legUpdateGroup.SetStyp(legUpdate.Styp ?? string.Empty);
                legUpdateGroup.SetCommissionCode(legUpdate.CommissionCode ?? string.Empty);
                legUpdateGroup.SetShortLocateId(legUpdate.ShortLocateId ?? string.Empty);
                legUpdateGroup.SetUndersym(legUpdate.Undersym ?? string.Empty);
                legUpdateGroup.SetPutcallind(legUpdate.Putcallind ?? string.Empty);
                legUpdateGroup.SetUserMessage(legUpdate.UserMessage ?? string.Empty);
                legUpdateGroup.SetOppositeParty(legUpdate.OppositeParty ?? string.Empty);
                legUpdateGroup.SetCurrency(legUpdate.Currency ?? string.Empty);
                legUpdateGroup.SetDispName(legUpdate.DispName ?? string.Empty);
                legUpdateGroup.SetDeposit(legUpdate.Deposit ?? string.Empty);
                legUpdateGroup.SetCustomer(legUpdate.Customer ?? string.Empty);
                legUpdateGroup.SetBranch(legUpdate.Branch ?? string.Empty);
                legUpdateGroup.SetBank(legUpdate.Bank ?? string.Empty);
                legUpdateGroup.SetGoodFrom(legUpdate.GoodFrom ?? string.Empty);
                legUpdateGroup.SetRemoteId(legUpdate.RemoteId ?? string.Empty);
                legUpdateGroup.SetOriginalTraderId(legUpdate.OriginalTraderId ?? string.Empty);
                legUpdateGroup.SetClientOrderId(legUpdate.ClientOrderId ?? string.Empty);
                legUpdateGroup.SetNewRemoteId(legUpdate.NewRemoteId ?? string.Empty);
                legUpdateGroup.SetPriceType(legUpdate.PriceType ?? string.Empty);
                legUpdateGroup.SetVolumeType(legUpdate.VolumeType ?? string.Empty);
                legUpdateGroup.SetGoodUntil(legUpdate.GoodUntil ?? string.Empty);
                legUpdateGroup.SetBuyorsell(legUpdate.Buyorsell ?? string.Empty);
                legUpdateGroup.SetExitVehicle(legUpdate.ExitVehicle ?? string.Empty);
                legUpdateGroup.SetTable(legUpdate.Table ?? string.Empty);
                legUpdateGroup.SetTraderCapacity(legUpdate.TraderCapacity ?? string.Empty);
                legUpdateGroup.SetFixTraderId(legUpdate.FixTraderId ?? string.Empty);
                legUpdateGroup.SetExchange(legUpdate.Exchange ?? string.Empty);
                legUpdateGroup.SetOrderTag(legUpdate.OrderTag ?? string.Empty);
                legUpdateGroup.SetCommissionRate(legUpdate.CommissionRate ?? string.Empty);
                legUpdateGroup.SetCommission(legUpdate.Commission ?? string.Empty);
                legUpdateGroup.SetAvgPrice(legUpdate.AvgPrice ?? string.Empty);
                legUpdateGroup.SetPairSpread(legUpdate.PairSpread ?? string.Empty);
                legUpdateGroup.SetAllocatedValue(legUpdate.AllocatedValue ?? string.Empty);
                legUpdateGroup.SetEcnFee(legUpdate.EcnFee ?? string.Empty);
                legUpdateGroup.SetSpreadClip(legUpdate.SpreadClip ?? string.Empty);
                legUpdateGroup.SetServerTimeZone(legUpdate.ServerTimeZone ?? string.Empty);
            }

            message.SetBookingType(orderInfo.BookingType ?? string.Empty);
            message.SetRefMgrNotes(orderInfo.RefMgrNotes ?? string.Empty);
            message.SetPairSpreadType(orderInfo.PairSpreadType ?? string.Empty);
            message.SetReason(orderInfo.Reason ?? string.Empty);
            message.SetLinkedOrderCancellation(orderInfo.LinkedOrderCancellation ?? string.Empty);
            message.SetLinkedOrderRelationship(orderInfo.LinkedOrderRelationship ?? string.Empty);
            message.SetCommissionRateType(orderInfo.CommissionRateType ?? string.Empty);
            message.SetAccount(orderInfo.Account ?? string.Empty);
            message.SetRoute(orderInfo.Route ?? string.Empty);
            message.SetOrderId(orderInfo.OrderId ?? string.Empty);
            message.SetLinkedOrderId(orderInfo.LinkedOrderId ?? string.Empty);
            message.SetRefersToId(orderInfo.RefersToId ?? string.Empty);
            message.SetTicketId(orderInfo.TicketId ?? string.Empty);
            message.SetOriginalOrderId(orderInfo.OriginalOrderId ?? string.Empty);
            message.SetSymbol(orderInfo.Symbol ?? string.Empty);
            message.SetType(orderInfo.Type ?? string.Empty);
            message.SetCurrentStatus(orderInfo.CurrentStatus ?? string.Empty);
            message.SetTraderId(orderInfo.TraderId ?? string.Empty);
            message.SetClaimedByClerk(orderInfo.ClaimedByClerk ?? string.Empty);
            message.SetSpreadLegPriceType(orderInfo.SpreadLegPriceType ?? string.Empty);
            message.SetSpreadLegLeanPriority(orderInfo.SpreadLegLeanPriority ?? string.Empty);
            message.SetOrderFlags(orderInfo.OrderFlags ?? string.Empty);
            message.SetFornexSourceFlags(orderInfo.FornexSourceFlags ?? string.Empty);
            message.SetExternalAcceptanceFlag(orderInfo.ExternalAcceptanceFlag ?? string.Empty);
            message.SetExtendedStateFlags2(orderInfo.ExtendedStateFlags2 ?? string.Empty);
            message.SetExtendedStateFlags(orderInfo.ExtendedStateFlags ?? string.Empty);
            message.SetCrossFlag(orderInfo.CrossFlag ?? string.Empty);
            message.SetSpreadClipType(orderInfo.SpreadClipType ?? string.Empty);
            message.SetPairLeg2BenchmarkType(orderInfo.PairLeg2BenchmarkType ?? string.Empty);
            message.SetPairLeg1BenchmarkType(orderInfo.PairLeg1BenchmarkType ?? string.Empty);
            message.SetSharesAllocated(orderInfo.SharesAllocated ?? string.Empty);
            message.SetOrderFlags2(orderInfo.OrderFlags2 ?? string.Empty);
            message.SetAcctType(orderInfo.AcctType ?? string.Empty);
            message.SetRank(orderInfo.Rank ?? string.Empty);
            message.SetGwBookSeqNo(orderInfo.GwBookSeqNo ?? string.Empty);
            message.SetDateIndex(orderInfo.DateIndex ?? string.Empty);
            message.SetBookId(orderInfo.BookId ?? string.Empty);
            message.SetTboAccountId(orderInfo.TboAccountId ?? string.Empty);
            message.SetOmsClientType(orderInfo.OmsClientType ?? string.Empty);
            message.SetExecutionState(orderInfo.ExecutionState ?? string.Empty);
            message.SetStyp(orderInfo.Styp ?? string.Empty);
            message.SetCommissionCode(orderInfo.CommissionCode ?? string.Empty);
            message.SetShortLocateId(orderInfo.ShortLocateId ?? string.Empty);
            message.SetUndersym(orderInfo.Undersym ?? string.Empty);
            message.SetPutcallind(orderInfo.Putcallind ?? string.Empty);
            message.SetUserMessage(orderInfo.UserMessage ?? string.Empty);
            message.SetOppositeParty(orderInfo.OppositeParty ?? string.Empty);
            message.SetCurrency(orderInfo.Currency ?? string.Empty);
            message.SetDispName(orderInfo.DispName ?? string.Empty);
            message.SetDeposit(orderInfo.Deposit ?? string.Empty);
            message.SetCustomer(orderInfo.Customer ?? string.Empty);
            message.SetBranch(orderInfo.Branch ?? string.Empty);
            message.SetBank(orderInfo.Bank ?? string.Empty);
            message.SetGoodFrom(orderInfo.GoodFrom ?? string.Empty);
            message.SetRemoteId(orderInfo.RemoteId ?? string.Empty);
            message.SetOriginalTraderId(orderInfo.OriginalTraderId ?? string.Empty);
            message.SetClientOrderId(orderInfo.ClientOrderId ?? string.Empty);
            message.SetNewRemoteId(orderInfo.NewRemoteId ?? string.Empty);
            message.SetPriceType(orderInfo.PriceType ?? string.Empty);
            message.SetVolumeType(orderInfo.VolumeType ?? string.Empty);
            message.SetGoodUntil(orderInfo.GoodUntil ?? string.Empty);
            message.SetBuyorsell(orderInfo.Buyorsell ?? string.Empty);
            message.SetExitVehicle(orderInfo.ExitVehicle ?? string.Empty);
            message.SetTable(orderInfo.Table ?? string.Empty);
            message.SetTraderCapacity(orderInfo.TraderCapacity ?? string.Empty);
            message.SetFixTraderId(orderInfo.FixTraderId ?? string.Empty);
            message.SetExchange(orderInfo.Exchange ?? string.Empty);
            message.SetOrderTag(orderInfo.OrderTag ?? string.Empty);
            message.SetCommissionRate(orderInfo.CommissionRate ?? string.Empty);
            message.SetCommission(orderInfo.Commission ?? string.Empty);
            message.SetAvgPrice(orderInfo.AvgPrice ?? string.Empty);
            message.SetPairSpread(orderInfo.PairSpread ?? string.Empty);
            message.SetAllocatedValue(orderInfo.AllocatedValue ?? string.Empty);
            message.SetEcnFee(orderInfo.EcnFee ?? string.Empty);
            message.SetSpreadClip(orderInfo.SpreadClip ?? string.Empty);
            message.SetServerTimeZone(orderInfo.ServerTimeZone ?? string.Empty);


            return message.Limit - offset;
        }

        public int EncodeBasketOrderRequestMessage(DirectBuffer directBuffer, int offset, BasketOrderRequest basketOrderRequest)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            BasketOrderRequestMessage message = new BasketOrderRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = BasketOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = BasketOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = BasketOrderRequestMessage.TemplateId;
            messageHeader.Version = BasketOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            BasketOrderRequestMessage.BasketOrderRowsGroup basketOrderRowsGroup = message.BasketOrderRowsCount(basketOrderRequest.BasketOrderRows.Count);
            foreach (BasketOrderRow basketRow in basketOrderRequest.BasketOrderRows)
            {
                basketOrderRowsGroup.Next();
                basketOrderRowsGroup.AcctType = basketRow.AcctType;
                basketOrderRowsGroup.BookId = basketRow.BookId;
                basketOrderRowsGroup.CommissionRateType = basketRow.CommissionRateType;
                basketOrderRowsGroup.CrossFlag = basketRow.CrossFlag;
                basketOrderRowsGroup.DateIndex = basketRow.DateIndex;
                basketOrderRowsGroup.ExecutionState = basketRow.ExecutionState;
                basketOrderRowsGroup.ExtendedStateFlags = basketRow.ExtendedStateFlags;
                basketOrderRowsGroup.ExtendedStateFlags2 = basketRow.ExtendedStateFlags2;
                basketOrderRowsGroup.ExternalAcceptanceFlag = basketRow.ExternalAcceptanceFlag;
                basketOrderRowsGroup.FornexSourceFlags = basketRow.FornexSourceFlags;
                basketOrderRowsGroup.GwBookSeqNo = basketRow.GwBookSeqNo;
                basketOrderRowsGroup.LinkedOrderCancellation = basketRow.LinkedOrderCancellation;
                basketOrderRowsGroup.LinkedOrderRelationship = basketRow.LinkedOrderRelationship;
                basketOrderRowsGroup.Minmove = basketRow.Minmove;
                basketOrderRowsGroup.OmsClientType = basketRow.OmsClientType;
                basketOrderRowsGroup.OrderFlags = basketRow.OrderFlags;
                basketOrderRowsGroup.OrderFlags2 = basketRow.OrderFlags2;
                basketOrderRowsGroup.OrderResidual = basketRow.OrderResidual;
                basketOrderRowsGroup.OriginalVolume = basketRow.OriginalVolume;
                basketOrderRowsGroup.PairImbalanceLimitType = basketRow.PairImbalanceLimitType;
                basketOrderRowsGroup.PairLeg1BenchmarkType = basketRow.PairLeg1BenchmarkType;
                basketOrderRowsGroup.PairLeg2BenchmarkType = basketRow.PairLeg2BenchmarkType;
                basketOrderRowsGroup.PairSpreadType = basketRow.PairSpreadType;
                basketOrderRowsGroup.Rank = basketRow.Rank;
                basketOrderRowsGroup.RemainingVolume = basketRow.RemainingVolume;
                basketOrderRowsGroup.SharesAllocated = basketRow.SharesAllocated;
                basketOrderRowsGroup.SpreadClipType = basketRow.SpreadClipType;
                basketOrderRowsGroup.SpreadLegCount = basketRow.SpreadLegCount;
                basketOrderRowsGroup.SpreadLegLeanPriority = basketRow.SpreadLegLeanPriority;
                basketOrderRowsGroup.SpreadLegNumber = basketRow.SpreadLegNumber;
                basketOrderRowsGroup.SpreadLegPriceType = basketRow.SpreadLegPriceType;
                basketOrderRowsGroup.SpreadNumLegs = basketRow.SpreadNumLegs;
                basketOrderRowsGroup.Styp = basketRow.Styp;
                basketOrderRowsGroup.TboAccountId = basketRow.TboAccountId;
                basketOrderRowsGroup.UtcOffset = basketRow.UtcOffset;
                basketOrderRowsGroup.Volume = basketRow.Volume;
                basketOrderRowsGroup.VolumeTraded = basketRow.VolumeTraded;
                basketOrderRowsGroup.WorkingQty = basketRow.WorkingQty;
                basketOrderRowsGroup.AllocatedValue = basketRow.AllocatedValue;
                basketOrderRowsGroup.AvgPrice = basketRow.AvgPrice;
                basketOrderRowsGroup.Basisvalue = basketRow.Basisvalue;
                basketOrderRowsGroup.Commission = basketRow.Commission;
                basketOrderRowsGroup.EcnFee = basketRow.EcnFee;
                basketOrderRowsGroup.Latency3 = basketRow.Latency3;
                basketOrderRowsGroup.Latency6 = basketRow.Latency6;
                basketOrderRowsGroup.PairCash = basketRow.PairCash;
                basketOrderRowsGroup.PairImbalanceLimit = basketRow.PairImbalanceLimit;
                basketOrderRowsGroup.PairLeg1Benchmark = basketRow.PairLeg1Benchmark;
                basketOrderRowsGroup.PairLeg2Benchmark = basketRow.PairLeg2Benchmark;
                basketOrderRowsGroup.PairRatio = basketRow.PairRatio;
                basketOrderRowsGroup.PairSpread = basketRow.PairSpread;
                basketOrderRowsGroup.PairTarget = basketRow.PairTarget;
                basketOrderRowsGroup.SpreadClip = basketRow.SpreadClip;

                basketOrderRowsGroup.Buyorsell = (Generated.Side)basketRow.Buyorsell;
                basketOrderRowsGroup.PriceType = (Generated.OrderType)basketRow.PriceType;

                basketOrderRowsGroup.NewsDate = basketRow.NewsDate.ToUnixEpoch();
                basketOrderRowsGroup.ExpirDate = basketRow.ExpirDate.ToUnixEpoch();

                basketOrderRowsGroup.NewsTime = basketRow.NewsTime.TotalMilliseconds;
                basketOrderRowsGroup.TrdTime = basketRow.TrdTime.TotalMilliseconds;

                IEnumerable<KeyValuePair<string, string>> extendedFields = basketRow.ExtendedFields.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value));
                BasketOrderRequestMessage.BasketOrderRowsGroup.ExtendedFieldsGroup extendedFieldsGroup = basketOrderRowsGroup.ExtendedFieldsCount(extendedFields.Count());
                foreach (KeyValuePair<string, string> kvp in extendedFields)
                {
                    extendedFieldsGroup.Next();
                    if (!string.IsNullOrWhiteSpace(kvp.Key) &&
                        !string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        extendedFieldsGroup.SetKey(kvp.Key);
                        extendedFieldsGroup.SetValue(kvp.Value);
                    }
                }

                basketOrderRowsGroup.SetAsk(basketRow.Ask ?? string.Empty);
                basketOrderRowsGroup.SetBid(basketRow.Bid ?? string.Empty);
                basketOrderRowsGroup.SetOriginalPrice(basketRow.OriginalPrice ?? string.Empty);
                basketOrderRowsGroup.SetPrice(basketRow.Price ?? string.Empty);
                basketOrderRowsGroup.SetStopPrice(basketRow.StopPrice ?? string.Empty);
                basketOrderRowsGroup.SetStrikePrc(basketRow.StrikePrc ?? string.Empty);
                basketOrderRowsGroup.SetBank(basketRow.Bank ?? string.Empty);
                basketOrderRowsGroup.SetBranch(basketRow.Branch ?? string.Empty);
                basketOrderRowsGroup.SetClaimedByClerk(basketRow.ClaimedByClerk ?? string.Empty);
                basketOrderRowsGroup.SetClientOrderId(basketRow.ClientOrderId ?? string.Empty);
                basketOrderRowsGroup.SetCommissionCode(basketRow.CommissionCode ?? string.Empty);
                basketOrderRowsGroup.SetCurrency(basketRow.Currency ?? string.Empty);
                basketOrderRowsGroup.SetCurrentStatus(basketRow.CurrentStatus ?? string.Empty);
                basketOrderRowsGroup.SetCustomer(basketRow.Customer ?? string.Empty);
                basketOrderRowsGroup.SetDeposit(basketRow.Deposit ?? string.Empty);
                basketOrderRowsGroup.SetDispName(basketRow.DispName ?? string.Empty);
                basketOrderRowsGroup.SetExchange(basketRow.Exchange ?? string.Empty);
                basketOrderRowsGroup.SetExitVehicle(basketRow.ExitVehicle ?? string.Empty);
                basketOrderRowsGroup.SetFixTraderId(basketRow.FixTraderId ?? string.Empty);
                basketOrderRowsGroup.SetGoodFrom(basketRow.GoodFrom ?? string.Empty);
                basketOrderRowsGroup.SetGoodUntil(basketRow.GoodUntil ?? string.Empty);
                basketOrderRowsGroup.SetLinkedOrderId(basketRow.LinkedOrderId ?? string.Empty);
                basketOrderRowsGroup.SetNewRemoteId(basketRow.NewRemoteId ?? string.Empty);
                basketOrderRowsGroup.SetOppositeParty(basketRow.OppositeParty ?? string.Empty);
                basketOrderRowsGroup.SetOrderId(basketRow.OrderId ?? string.Empty);
                basketOrderRowsGroup.SetOrderTag(basketRow.OrderTag ?? string.Empty);
                basketOrderRowsGroup.SetOriginalOrderId(basketRow.OriginalOrderId ?? string.Empty);
                basketOrderRowsGroup.SetOriginalTraderId(basketRow.OriginalTraderId ?? string.Empty);
                basketOrderRowsGroup.SetPutcallind(basketRow.Putcallind ?? string.Empty);
                basketOrderRowsGroup.SetReason(basketRow.Reason ?? string.Empty);
                basketOrderRowsGroup.SetRefersToId(basketRow.RefersToId ?? string.Empty);
                basketOrderRowsGroup.SetRemoteId(basketRow.RemoteId ?? string.Empty);
                basketOrderRowsGroup.SetShortLocateId(basketRow.ShortLocateId ?? string.Empty);
                basketOrderRowsGroup.SetTable(basketRow.Table ?? string.Empty);
                basketOrderRowsGroup.SetTicketId(basketRow.TicketId ?? string.Empty);
                basketOrderRowsGroup.SetTimeStamp(basketRow.TimeStamp ?? string.Empty);
                basketOrderRowsGroup.SetTraderCapacity(basketRow.TraderCapacity ?? string.Empty);
                basketOrderRowsGroup.SetTraderId(basketRow.TraderId ?? string.Empty);
                basketOrderRowsGroup.SetType(basketRow.Type ?? string.Empty);
                basketOrderRowsGroup.SetUndersym(basketRow.Undersym ?? string.Empty);
                basketOrderRowsGroup.SetUserMessage(basketRow.UserMessage ?? string.Empty);
                basketOrderRowsGroup.SetVolumeType(basketRow.VolumeType ?? string.Empty);
                basketOrderRowsGroup.SetRoute(basketRow.Route ?? string.Empty);
            }

            message.SetToken(basketOrderRequest.Token ?? string.Empty);
            message.SetClientOrderId(basketOrderRequest.ClientOrderId ?? string.Empty);


            return message.Limit - offset;
        }

        public int EncodeResetBaseLineRequestMessage(DirectBuffer directBuffer, int offset, List<string> symbols)
        {
            int count = symbols.Count;
            int size = MessageHeader.Size + ResetBaseLineRequestMessage.BlockLength + ResetBaseLineRequestMessage.SymbolsGroup.SbeHeaderSize + symbols.Sum(x => GetBufferEstimate(x));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ResetBaseLineRequestMessage message = new ResetBaseLineRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ResetBaseLineRequestMessage.BlockLength;
            messageHeader.SchemaId = ResetBaseLineRequestMessage.SchemaId;
            messageHeader.TemplateId = ResetBaseLineRequestMessage.TemplateId;
            messageHeader.Version = ResetBaseLineRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            ResetBaseLineRequestMessage.SymbolsGroup symbolsGroup = message.SymbolsCount(count);
            foreach (string symbol in symbols)
            {
                symbolsGroup.Next();
                symbolsGroup.SetSymbol(symbol ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeAutoTraderConfigMessage(DirectBuffer directBuffer, int offset, int requestId, string json)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AutoTraderConfigMessage message = new AutoTraderConfigMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AutoTraderConfigMessage.BlockLength;
            messageHeader.SchemaId = AutoTraderConfigMessage.SchemaId;
            messageHeader.TemplateId = AutoTraderConfigMessage.TemplateId;
            messageHeader.Version = AutoTraderConfigMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetJson(json ?? "");

            return message.Limit - offset;
        }


        public int EncodeOrderUpdateValuesMessage(DirectBuffer directBuffer, int offset, OrderUpdateValues orderUpdate)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderUpdateValuesMessage.BlockLength;
            messageHeader.SchemaId = OrderUpdateValuesMessage.SchemaId;
            messageHeader.TemplateId = OrderUpdateValuesMessage.TemplateId;
            messageHeader.Version = OrderUpdateValuesMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            OrderUpdateValuesMessage message = new OrderUpdateValuesMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.StatusAsSpan().Clear();
            message.OrderIdAsSpan().Clear();
            message.LocalOrderIdAsSpan().Clear();
            message.ParentLocalOrderIdAsSpan().Clear();
            message.OriginalOrderIdAsSpan().Clear();
            message.MessageAsSpan().Clear();

            message.ClearOrderIdSet = orderUpdate.ClearOrderIdSet ? BooleanEnum.True : BooleanEnum.False;
            message.IsCancelEnabled = orderUpdate.IsCancelEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.IsModifyEnabled = orderUpdate.IsModifyEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.IsSubmitEnabled = orderUpdate.IsSubmitEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.IsMainOrder = orderUpdate.IsMainOrder ? BooleanEnum.True : BooleanEnum.False;
            message.IsContraOrder = orderUpdate.IsContraOrder ? BooleanEnum.True : BooleanEnum.False;
            message.IsHedgeOrder = orderUpdate.IsHedgeOrder ? BooleanEnum.True : BooleanEnum.False;
            message.IsLooping = orderUpdate.IsLooping ? BooleanEnum.True : BooleanEnum.False;
            message.RequiresManualIntervention = orderUpdate.RequiresManualIntervention ? BooleanEnum.True : BooleanEnum.False;
            message.Filled = orderUpdate.Filled;
            message.CumQuantity = orderUpdate.CumQuantity;
            message.LastQuantity = orderUpdate.LastQuantity;
            message.LeavesQuantity = orderUpdate.LeavesQuantity;
            message.StatusMode = (int)orderUpdate.StatusMode;
            message.LastUpdateTime = orderUpdate.LastUpdateTime.ToUnixEpoch();
            message.OrderStatus = (OrderStatus)orderUpdate.OrderStatus;
            message.LastPrice = orderUpdate.LastPrice;
            message.AveragePrice = orderUpdate.AveragePrice;
            message.AveragePriceAfterFees = orderUpdate.AveragePriceAfterFees;
            message.SetStatus(orderUpdate.Status ?? "");
            message.SetOrderId(orderUpdate.OrderId ?? "");
            message.SetLocalOrderId(orderUpdate.LocalOrderId ?? "");
            message.SetParentLocalOrderId(orderUpdate.ParentLocalOrderId ?? "");
            message.SetOriginalOrderId(orderUpdate.OriginalOrderId ?? "");
            message.SetMessage(orderUpdate.Message ?? "");
            message.UnderlyingMidPrice = orderUpdate.UnderlyingMidPrice;
            message.Price = orderUpdate.Price;
            message.AutomationRunning = orderUpdate.AutomationRunning ? BooleanEnum.True : BooleanEnum.False;
            message.ContraTrader = orderUpdate.ContraTrader != null ? (byte)orderUpdate.ContraTrader : OrderUpdateValuesMessage.ContraTraderNullValue;

            return message.Limit - offset;
        }

        public int EncodeSymbolStatModelAddedMessage(DirectBuffer directBuffer, int offset, ISymbolStatModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolStatModelAddedMessage message = new SymbolStatModelAddedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolStatModelAddedMessage.BlockLength;
            messageHeader.SchemaId = SymbolStatModelAddedMessage.SchemaId;
            messageHeader.TemplateId = SymbolStatModelAddedMessage.TemplateId;
            messageHeader.Version = SymbolStatModelAddedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.Id = model.Id;
            message.SetSymbol(model.Symbol ?? "");
            message.MultiLegTradesCount = model.MultiLegTradesCount;
            message.SingleLegTradesCount = model.SingleLegTradesCount;
            message.MultiLegTradesPerHour = model.MultiLegTradesPerHour;
            message.SingleLegTradesPerHour = model.SingleLegTradesPerHour;
            message.MultiLegTradesPerMinute = model.MultiLegTradesPerMinute;
            message.SingleLegTradesPerMinute = model.SingleLegTradesPerMinute;

            message.Volume = model.Volume;
            message.OptionVolume = model.OptionVolume;
            message.DayPercentChange = model.DayPercentChange;
            message.HourPercentChange = model.HourPercentChange;
            message.HalfHourPercentChange = model.HalfHourPercentChange;
            message.QuarterHourPercentChange = model.QuarterHourPercentChange;

            message.DayNetChange = model.DayNetChange;
            message.HourNetChange = model.HourNetChange;
            message.HalfHourNetChange = model.HalfHourNetChange;
            message.QuarterHourNetChange = model.QuarterHourNetChange;
            message.Last = model.Last;

            return message.Limit - offset;
        }

        public int EncodeSymbolStatModelUpdateMessage(DirectBuffer directBuffer, int offset, ISymbolStatModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolStatModelUpdateMessage message = new SymbolStatModelUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolStatModelUpdateMessage.BlockLength;
            messageHeader.SchemaId = SymbolStatModelUpdateMessage.SchemaId;
            messageHeader.TemplateId = SymbolStatModelUpdateMessage.TemplateId;
            messageHeader.Version = SymbolStatModelUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.Id = model.Id;
            message.MultiLegTradesCount = model.MultiLegTradesCount;
            message.SingleLegTradesCount = model.SingleLegTradesCount;
            message.MultiLegTradesPerHour = model.MultiLegTradesPerHour;
            message.SingleLegTradesPerHour = model.SingleLegTradesPerHour;
            message.MultiLegTradesPerMinute = model.MultiLegTradesPerMinute;
            message.SingleLegTradesPerMinute = model.SingleLegTradesPerMinute;

            message.Volume = model.Volume;
            message.OptionVolume = model.OptionVolume;
            message.DayPercentChange = model.DayPercentChange;
            message.HourPercentChange = model.HourPercentChange;
            message.HalfHourPercentChange = model.HalfHourPercentChange;
            message.QuarterHourPercentChange = model.QuarterHourPercentChange;

            message.DayNetChange = model.DayNetChange;
            message.HourNetChange = model.HourNetChange;
            message.HalfHourNetChange = model.HalfHourNetChange;
            message.QuarterHourNetChange = model.QuarterHourNetChange;
            message.Last = model.Last;

            return message.Limit - offset;
        }

        public int EncodeSpreadRiskModel(DirectBuffer directBuffer, int offset, ISpreadRiskModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadRiskModelUpdateMessage message = new SpreadRiskModelUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadRiskModelUpdateMessage.BlockLength;
            messageHeader.SchemaId = SpreadRiskModelUpdateMessage.SchemaId;
            messageHeader.TemplateId = SpreadRiskModelUpdateMessage.TemplateId;
            messageHeader.Version = SpreadRiskModelUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.Id = model.Id;
            message.TotalOpen = model.TotalOpen;
            message.TotalClose = model.TotalClose;
            message.Action = model.Action ? BooleanEnum.True : BooleanEnum.False;
            message.LastTradeTime = model.LastTradeTime.ToUnixEpoch();
            message.SetSpreadDescription(model.SpreadDescription ?? "");
            message.SetUnderlying(model.Underlying ?? "");
            message.SetTags(model.Tags ?? "");

            return message.Limit - offset;
        }

        public int EncodeSelfTradeWarningModel(DirectBuffer directBuffer, int offset, ISelfTradeModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SelfTradeWarningMessage message = new SelfTradeWarningMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadRiskModelUpdateMessage.BlockLength;
            messageHeader.SchemaId = SpreadRiskModelUpdateMessage.SchemaId;
            messageHeader.TemplateId = SpreadRiskModelUpdateMessage.TemplateId;
            messageHeader.Version = SpreadRiskModelUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.Qty = model.Qty;
            message.TradeTime = model.TradeTime.ToUnixEpoch();
            message.SetSymbol(model.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeCancelOrderRequestMessage(DirectBuffer directBuffer, int offset, CancelRequest cancelRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CancelOrderRequestMessage message = new CancelOrderRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CancelOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = CancelOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = CancelOrderRequestMessage.TemplateId;
            messageHeader.Version = CancelOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetLocalId(cancelRequest.LocalId ?? "");
            message.SetPermId(cancelRequest.PermId ?? "");
            message.SetOrderId(cancelRequest.OrderId ?? "");
            message.SetAccount(cancelRequest.Account ?? "");
            message.Venue = cancelRequest.Venue.HasValue ? (byte)cancelRequest.Venue : CancelOrderRequestMessage.VenueNullValue;
            message.UserId = cancelRequest.UserId;
            message.RiskCheckId = cancelRequest.RiskCheckId;
            return message.Limit - offset;
        }

        public int EncodeTagOrderMessage(DirectBuffer directBuffer, int offset, string permId, bool isTagged, string tagger, string taggedMessage)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TagOrderMessage message = new TagOrderMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TagOrderMessage.BlockLength;
            messageHeader.SchemaId = TagOrderMessage.SchemaId;
            messageHeader.TemplateId = TagOrderMessage.TemplateId;
            messageHeader.Version = TagOrderMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetPermID(permId);
            message.IsTagged = isTagged ? BooleanEnum.True : BooleanEnum.False;
            message.SetTagger(tagger ?? "");
            message.SetTaggedMessage(taggedMessage ?? "");

            return message.Limit - offset;
        }

        public int EncodeSymbolEdgeMapRequest(DirectBuffer directBuffer, int offset, int requestId, string symbol, DateTime start)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolEdgeMapRequest message = new SymbolEdgeMapRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolEdgeMapRequest.BlockLength;
            messageHeader.SchemaId = SymbolEdgeMapRequest.SchemaId;
            messageHeader.TemplateId = SymbolEdgeMapRequest.TemplateId;
            messageHeader.Version = SymbolEdgeMapRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.Start = start.ToUnixEpoch();
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeSymbolEdgeMapResponse(DirectBuffer directBuffer, int offset, int requestId, string symbol, DateTime start, IEnumerable<SymbolEdgeMap> symbolEdgeMaps)
        {
            int count = symbolEdgeMaps.Count();
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolEdgeMapResponse message = new SymbolEdgeMapResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolEdgeMapResponse.BlockLength;
            messageHeader.SchemaId = SymbolEdgeMapResponse.SchemaId;
            messageHeader.TemplateId = SymbolEdgeMapResponse.TemplateId;
            messageHeader.Version = SymbolEdgeMapResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.Start = start.ToUnixEpoch();

            SymbolEdgeMapResponse.UpdatesGroup edgeMapGroup = message.UpdatesCount(count);
            foreach (var edgeMap in symbolEdgeMaps)
            {
                edgeMapGroup.Next();
                edgeMapGroup.Id = edgeMap.Id;
                edgeMapGroup.Date = edgeMap.Date.ToUnixEpoch();
                edgeMapGroup.BestBuyPrice = edgeMap.BestBuyPrice;
                edgeMapGroup.BestBuyPriceUnderlying = edgeMap.BestBuyPriceUnderlying;
                edgeMapGroup.BestBuyPriceDelta = edgeMap.BestBuyPriceDelta;
                edgeMapGroup.BestSellPrice = edgeMap.BestSellPrice;
                edgeMapGroup.BestSellPriceUnderlying = edgeMap.BestSellPriceUnderlying;
                edgeMapGroup.BestSellPriceDelta = edgeMap.BestSellPriceDelta;
                edgeMapGroup.OpeningSide = edgeMap.OpeningSide.HasValue ? (Generated.Side)edgeMap.OpeningSide : Generated.Side.NULL_VALUE;
                edgeMapGroup.HardSide = edgeMap.HardSide.HasValue ? (Generated.Side)edgeMap.HardSide : Generated.Side.NULL_VALUE;
            }

            return message.Limit - offset;
        }

        public int EncodeBarRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol, DateTime rangeStart, DateTime rangeEnd)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            BarRequestMessage message = new BarRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = BarRequestMessage.BlockLength;
            messageHeader.SchemaId = BarRequestMessage.SchemaId;
            messageHeader.TemplateId = BarRequestMessage.TemplateId;
            messageHeader.Version = BarRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetSymbol(symbol);
            message.RangeStart = rangeStart.ToUnixEpoch();
            message.RangeEnd = rangeEnd.ToUnixEpoch();

            return message.Limit - offset;
        }

        public int EncodeBarResponseMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol, List<BarModel> bars, bool lastGroup)
        {
            int count = bars.Count;
            int size = MessageHeader.Size + BarResponseMessage.BlockLength + BarResponseMessage.BarsGroup.SbeHeaderSize + (count * BarResponseMessage.BarsGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            BarResponseMessage message = new BarResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = BarResponseMessage.BlockLength;
            messageHeader.SchemaId = BarResponseMessage.SchemaId;
            messageHeader.TemplateId = BarResponseMessage.TemplateId;
            messageHeader.Version = BarResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.SetSymbol(symbol ?? "");
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            BarResponseMessage.BarsGroup symbolsGroup = message.BarsCount(count);
            foreach (var bar in bars)
            {
                symbolsGroup.Next();
                symbolsGroup.Timestamp = bar.Timestamp.ToUnixEpoch();
                symbolsGroup.Open = bar.Open;
                symbolsGroup.High = bar.High;
                symbolsGroup.Low = bar.Low;
                symbolsGroup.Close = bar.Close;
            }

            return message.Limit - offset;
        }

        public int EncodeAlertMessage(DirectBuffer directBuffer, int offset, AlertMessageModel alertMessage)
        {
            int size = MessageHeader.Size + AlertMessage.BlockLength + GetBufferEstimate(alertMessage.Message);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AlertMessage message = new AlertMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AlertMessage.BlockLength;
            messageHeader.SchemaId = AlertMessage.SchemaId;
            messageHeader.TemplateId = AlertMessage.TemplateId;
            messageHeader.Version = AlertMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.AlertId = alertMessage.AlertId;
            message.Time = alertMessage.Time.ToUnixEpoch();
            message.SetMessage(alertMessage.Message ?? "");

            return message.Limit - offset;
        }

        public int EncodeModifyOrderRequestMessage(DirectBuffer directBuffer, int offset, ModifyRequest modifyRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ModifyOrderRequestMessage message = new ModifyOrderRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ModifyOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = ModifyOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = ModifyOrderRequestMessage.TemplateId;
            messageHeader.Version = ModifyOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.Price = modifyRequest.Price;
            message.Quantity = modifyRequest.Quantity;
            message.SetLocalId(modifyRequest.LocalId ?? "");
            message.SetPermId(modifyRequest.PermId ?? "");
            message.SetOrderId(modifyRequest.OrderId ?? "");
            message.SetAccount(modifyRequest.Account ?? "");
            message.Venue = modifyRequest.Venue.HasValue ? (byte)modifyRequest.Venue : ModifyOrderRequestMessage.VenueNullValue;
            message.UserId = modifyRequest.UserId;
            message.RiskCheckId = modifyRequest.RiskCheckId;
            return message.Limit - offset;
        }

        public int EncodeEdgeToTheoUpdate(DirectBuffer directBuffer, int offset, EdgeToTheoUpdateModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeToTheoUpdateMessage.BlockLength;
            messageHeader.SchemaId = EdgeToTheoUpdateMessage.SchemaId;
            messageHeader.TemplateId = EdgeToTheoUpdateMessage.TemplateId;
            messageHeader.Version = EdgeToTheoUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeToTheoUpdateMessage message = new EdgeToTheoUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.BuyEdgeToTheo = model.BuyEdgeToTheo;
            message.SellEdgeToTheo = model.SellEdgeToTheo;

            message.SetSymbol(model.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeSymbolStrikeRangeRequest(DirectBuffer directBuffer, int offset, int reqId, string symbol, double delta, DateTime expiration)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolStrikeRangeRequestMessage.BlockLength;
            messageHeader.SchemaId = SymbolStrikeRangeRequestMessage.SchemaId;
            messageHeader.TemplateId = SymbolStrikeRangeRequestMessage.TemplateId;
            messageHeader.Version = SymbolStrikeRangeRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SymbolStrikeRangeRequestMessage message = new SymbolStrikeRangeRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = reqId;
            message.Delta = delta;
            message.Expiration = expiration.ToUnixEpoch();
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeSymbolStrikeRangeResponse(DirectBuffer directBuffer, int offset, int reqId, double strikeRange)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolStrikeRangeResponseMessage.BlockLength;
            messageHeader.SchemaId = SymbolStrikeRangeResponseMessage.SchemaId;
            messageHeader.TemplateId = SymbolStrikeRangeResponseMessage.TemplateId;
            messageHeader.Version = SymbolStrikeRangeResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SymbolStrikeRangeResponseMessage message = new SymbolStrikeRangeResponseMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = reqId;
            message.StrikeRange = strikeRange;

            return message.Limit - offset;
        }

        public int EncodePermEdgeToTheoMappingMessage(DirectBuffer directBuffer, int offset, string symbol, List<EdgeToTheoTrackerModel> mappings)
        {
            int count = mappings.Count;
            int size = MessageHeader.Size + PermEdgeToTheoMappingMessage.BlockLength + PermEdgeToTheoMappingMessage.MappingsGroup.SbeHeaderSize + (count * PermEdgeToTheoMappingMessage.MappingsGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PermEdgeToTheoMappingMessage message = new PermEdgeToTheoMappingMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PermEdgeToTheoMappingMessage.BlockLength;
            messageHeader.SchemaId = PermEdgeToTheoMappingMessage.SchemaId;
            messageHeader.TemplateId = PermEdgeToTheoMappingMessage.TemplateId;
            messageHeader.Version = PermEdgeToTheoMappingMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetSymbol(symbol ?? "");
            message.Count = count;

            PermEdgeToTheoMappingMessage.MappingsGroup group = message.MappingsCount(count);
            for (var index = 0; index < count; index++)
            {
                var map = mappings[index];
                group.Next();
                group.StrikeStart = map.StrikeStart;
                group.StrikeEnd = map.StrikeEnd;

                group.BuyAttemptEdgeToTheo = map.BuyAttemptEdgeToTheo;
                group.SellAttemptEdgeToTheo = map.SellAttemptEdgeToTheo;

                group.BuyFillEdgeToTheo = map.BuyFillEdgeToTheo;
                group.SellFillEdgeToTheo = map.SellFillEdgeToTheo;
            }

            return message.Limit - offset;
        }

        public int EncodeRegisterEdgeScanFeedServerRunnerJson(DirectBuffer directBuffer, int offset, string json)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RegisterEdgeScanFeedServerRunnerJson.BlockLength;
            messageHeader.SchemaId = RegisterEdgeScanFeedServerRunnerJson.SchemaId;
            messageHeader.TemplateId = RegisterEdgeScanFeedServerRunnerJson.TemplateId;
            messageHeader.Version = RegisterEdgeScanFeedServerRunnerJson.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            RegisterEdgeScanFeedServerRunnerJson message = new RegisterEdgeScanFeedServerRunnerJson();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetJson(json ?? "");

            return message.Limit - offset;
        }

        public int EncodeUnregisterEdgeScanFeedServerRunner(DirectBuffer directBuffer, int offset, string id)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = UnregisterEdgeScanFeedServerRunner.BlockLength;
            messageHeader.SchemaId = UnregisterEdgeScanFeedServerRunner.SchemaId;
            messageHeader.TemplateId = UnregisterEdgeScanFeedServerRunner.TemplateId;
            messageHeader.Version = UnregisterEdgeScanFeedServerRunner.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            UnregisterEdgeScanFeedServerRunner message = new UnregisterEdgeScanFeedServerRunner();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetId(id ?? "");

            return message.Limit - offset;
        }

        public int EncodeSpreadGeneratorRequestMessage(DirectBuffer directBuffer, int offset, string json)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadGeneratorRequestMessage.BlockLength;
            messageHeader.SchemaId = SpreadGeneratorRequestMessage.SchemaId;
            messageHeader.TemplateId = SpreadGeneratorRequestMessage.TemplateId;
            messageHeader.Version = SpreadGeneratorRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SpreadGeneratorRequestMessage message = new SpreadGeneratorRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetJson(json ?? "");

            return message.Limit - offset;
        }

        public int EncodeSpreadGeneratorRequestMessage(DirectBuffer directBuffer, int offset, int requestId, SpreadsGeneratorConfig config)
        {
            var json = JsonConvert.SerializeObject(config);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadGeneratorRequestMessage.BlockLength;
            messageHeader.SchemaId = SpreadGeneratorRequestMessage.SchemaId;
            messageHeader.TemplateId = SpreadGeneratorRequestMessage.TemplateId;
            messageHeader.Version = SpreadGeneratorRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SpreadGeneratorRequestMessage message = new SpreadGeneratorRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.SetJson(json ?? "");

            return message.Limit - offset;
        }

        public int EncodeSpreadGeneratorResultsMessage(DirectBuffer directBuffer, int offset, int requestId, Span<Spread> results, bool lastGroup)
        {
            var count = Min(results.Length, GroupSize16.NumInGroupMaxValue);
            var legsCount = 0;
            foreach (var result in results)
            {
                legsCount += result.Legs.Count;
            }

            int size = MessageHeader.Size + SpreadGeneratorResultsMessage.BlockLength + SpreadGeneratorResultsMessage.SpreadsGroup.SbeHeaderSize + (count * (SpreadGeneratorResultsMessage.SpreadsGroup.SbeBlockLength + SpreadGeneratorResultsMessage.SpreadsGroup.LegsGroup.SbeHeaderSize)) + (legsCount * SpreadGeneratorResultsMessage.SpreadsGroup.LegsGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadGeneratorResultsMessage message = new SpreadGeneratorResultsMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadGeneratorResultsMessage.BlockLength;
            messageHeader.SchemaId = SpreadGeneratorResultsMessage.SchemaId;
            messageHeader.TemplateId = SpreadGeneratorResultsMessage.TemplateId;
            messageHeader.Version = SpreadGeneratorResultsMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            SpreadGeneratorResultsMessage.SpreadsGroup symbolsGroup = message.SpreadsCount(count);
            for (var index = 0; index < count; index++)
            {
                var symbol = results[index];
                symbolsGroup.Next();
                SpreadGeneratorResultsMessage.SpreadsGroup.LegsGroup? legsGroup =
                    symbolsGroup.LegsCount(symbol.Legs.Count(x => x.Option != null));
                foreach (var leg in symbol.Legs)
                {
                    var legOption = leg.Option;
                    if (legOption == null)
                    {
                        continue;
                    }

                    legsGroup.Next();
                    legsGroup.SetRoot(legOption.RootSymbol ?? "");
                    legsGroup.Expiration = legOption.Expiration.ConvertToYyMMddInt();
                    legsGroup.PutCall = (Generated.PutCall)legOption.PutCall;
                    legsGroup.Strike = legOption.Strike;
                    legsGroup.Side = (Generated.Side)leg.Side;
                    legsGroup.Ratio = leg.Ratio;
                }
            }

            return message.Limit - offset;
        }

        public int EncodeSymbolsRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol, string secType, string exchange, string currency)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolsRequest.BlockLength;
            messageHeader.SchemaId = SymbolsRequest.SchemaId;
            messageHeader.TemplateId = SymbolsRequest.TemplateId;
            messageHeader.Version = SymbolsRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SymbolsRequest message = new SymbolsRequest();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.SetSymbol(symbol ?? "");
            message.SetSecType(secType ?? "");
            message.SetExchange(exchange ?? "");
            message.SetCurrency(currency ?? "");

            return message.Limit - offset;
        }

        public int EncodeSymbolsResponseMessage(DirectBuffer directBuffer, int offset, int requestId, List<Option> results, bool lastGroup)
        {

            int count = Min(results.Count, GroupSize16.NumInGroupMaxValue - 1);
            int size = MessageHeader.Size + SymbolsResponse.BlockLength + SymbolsResponse.SymbolsGroup.SbeHeaderSize + (count * SymbolsResponse.SymbolsGroup.SbeBlockLength);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolsResponse message = new SymbolsResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolsResponse.BlockLength;
            messageHeader.SchemaId = SymbolsResponse.SchemaId;
            messageHeader.TemplateId = SymbolsResponse.TemplateId;
            messageHeader.Version = SymbolsResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            SymbolsResponse.SymbolsGroup symbolsGroup = message.SymbolsCount(count);
            var counter = 0;
            foreach (var option in results)
            {
                if (++counter > count)
                {
                    break;
                }
                symbolsGroup.Next();

                if (option == null)
                {
                    continue;
                }

                symbolsGroup.SetRoot(option.RootSymbol ?? "");
                symbolsGroup.Expiration = option.Expiration.ConvertToYyMMddInt();
                symbolsGroup.PutCall = (Generated.PutCall)option.PutCall;
                symbolsGroup.Strike = option.Strike;
            }

            return message.Limit - offset;
        }

        public int EncodeOptionChainRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string underlying,
            int? expiryFromYyMMdd, int? expiryToYyMMdd, double? strikeMin, double? strikeMax, Data.Enums.PutCall? putCallFilter)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OptionChainRequest.BlockLength;
            messageHeader.SchemaId = OptionChainRequest.SchemaId;
            messageHeader.TemplateId = OptionChainRequest.TemplateId;
            messageHeader.Version = OptionChainRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            OptionChainRequest message = new OptionChainRequest();
            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.SetUnderlyingSymbol(underlying ?? "");
            message.ExpiryFrom = expiryFromYyMMdd ?? OptionChainRequest.ExpiryFromNullValue;
            message.ExpiryTo = expiryToYyMMdd ?? OptionChainRequest.ExpiryToNullValue;
            message.StrikeMin = strikeMin ?? double.NaN;
            message.StrikeMax = strikeMax ?? double.NaN;
            message.PutCallFilter = putCallFilter == null ? Generated.PutCall.NULL_VALUE : (Generated.PutCall)putCallFilter;

            return message.Limit - offset;
        }

        public int EncodeOptionChainResponseMessage(DirectBuffer directBuffer, int offset, int requestId, IReadOnlyList<Option> results, bool lastGroup)
        {
            int count = Min(results.Count, GroupSize16.NumInGroupMaxValue - 1);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OptionChainResponse message = new OptionChainResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OptionChainResponse.BlockLength;
            messageHeader.SchemaId = OptionChainResponse.SchemaId;
            messageHeader.TemplateId = OptionChainResponse.TemplateId;
            messageHeader.Version = OptionChainResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            OptionChainResponse.OptionsGroup optionsGroup = message.OptionsCount(count);
            var counter = 0;
            foreach (var option in results)
            {
                if (++counter > count)
                {
                    break;
                }
                optionsGroup.Next();

                if (option == null)
                {
                    continue;
                }

                optionsGroup.SetRoot(option.RootSymbol ?? "");
                optionsGroup.Expiration = option.Expiration.ConvertToYyMMddInt();
                optionsGroup.PutCall = (Generated.PutCall)option.PutCall;
                optionsGroup.Strike = option.Strike;
                optionsGroup.MinimumTick = option.MinimumTick;
                optionsGroup.Multiplier = option.Multiplier;
                optionsGroup.MinimumTickStyle = (Generated.MinimumTickStyle)option.MinimumTickStyle;
                optionsGroup.SetPrimaryExchange(option.PrimaryExchange ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedStatisticsModel(DirectBuffer directBuffer, int offset, IEdgeScanFeedStatisticsSummary model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedStatisticsMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedStatisticsMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedStatisticsMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedStatisticsMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeScanFeedStatisticsMessage message = new EdgeScanFeedStatisticsMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetInstanceId(model.InstanceId ?? "");

            message.TotalSubs = model.TotalSubs;
            message.TotalAttempts = model.TotalAttempts;

            message.StartTime = model.StartTime.ToUnixEpoch();

            message.SetState(model.State ?? "");
            message.SetUser(model.User ?? "");
            message.SetScannerConfig(model.ScannerConfig ?? "");
            message.SetBasketConfig(model.BasketConfig ?? "");

            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedStatisticsMiniUpdate(DirectBuffer directBuffer, int offset, IEdgeScanFeedStatisticsSummary model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedStatisticsUpdateMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedStatisticsUpdateMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedStatisticsUpdateMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedStatisticsUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeScanFeedStatisticsUpdateMessage message = new EdgeScanFeedStatisticsUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetInstanceId(model.InstanceId ?? "");

            message.TotalSubs = model.TotalSubs;
            message.TotalAttempts = model.TotalAttempts;
            message.Submissions = model.Submissions;
            message.Received = model.Received;
            message.Timestamp = model.Timestamp.ToUnixEpoch();
            message.SetState(model.State ?? "");

            return message.Limit - offset;
        }

        public int EncodeChartSeriesUpdateMessage(DirectBuffer directBuffer, int offset, string id, SubscriptionFieldType type, IReadOnlyList<ChartValueModel> updates, int startIndex)
        {

            var count = updates.Count - startIndex;
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ChartSeriesUpdateMessage.BlockLength;
            messageHeader.SchemaId = ChartSeriesUpdateMessage.SchemaId;
            messageHeader.TemplateId = ChartSeriesUpdateMessage.TemplateId;
            messageHeader.Version = ChartSeriesUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            ChartSeriesUpdateMessage message = new ChartSeriesUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetId(id ?? "");
            message.Type = (short)type;

            ChartSeriesUpdateMessage.UpdatesGroup group = message.UpdatesCount(count);
            for (var index = startIndex; index < count; index++)
            {
                var update = updates[index];
                group.Next();

                group.Timestamp = update.Timestamp.ToUnixEpoch();
                group.Value = update.Value;
            }

            return message.Limit - offset;
        }

        public int EncodeFirmOrderAndTradeSummary(DirectBuffer directBuffer, int offset, FirmOrderAndTradeSummary summary)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = FirmOrderAndTradeSummaryMessage.BlockLength;
            messageHeader.SchemaId = FirmOrderAndTradeSummaryMessage.SchemaId;
            messageHeader.TemplateId = FirmOrderAndTradeSummaryMessage.TemplateId;
            messageHeader.Version = FirmOrderAndTradeSummaryMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            FirmOrderAndTradeSummaryMessage message = new FirmOrderAndTradeSummaryMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.Index = summary.Index;

            message.BuyLastAttemptPx = summary.BuySummary?.LastAttemptPx ?? double.NaN;
            message.BuyLastAttemptUnderPx = summary.BuySummary?.LastAttemptUnderPx ?? double.NaN;
            message.BuyLastAttemptTime = summary.BuySummary?.LastAttemptTime.ToUnixEpoch() ?? DateTime.UnixEpoch.ToUnixEpoch();

            message.BuyLastFillPx = summary.BuySummary?.LastFillPx ?? double.NaN;
            message.BuyLastFillUnderPx = summary.BuySummary?.LastFillUnderPx ?? double.NaN;
            message.BuyLastFillTime = summary.BuySummary?.LastFillTime.ToUnixEpoch() ?? DateTime.UnixEpoch.ToUnixEpoch();

            message.BuyLowestAttemptedEdgeToTheo = summary.BuySummary?.LowestAttemptedEdgeToTheo ?? double.NaN;
            message.BuyHighestFilledEdgeToTheo = summary.BuySummary?.HighestFilledEdgeToTheo ?? double.NaN;

            message.SellLastAttemptPx = summary.SellSummary?.LastAttemptPx ?? double.NaN;
            message.SellLastAttemptUnderPx = summary.SellSummary?.LastAttemptUnderPx ?? double.NaN;
            message.SellLastAttemptTime = summary.SellSummary?.LastAttemptTime.ToUnixEpoch() ?? DateTime.UnixEpoch.ToUnixEpoch();

            message.SellLastFillPx = summary.SellSummary?.LastFillPx ?? double.NaN;
            message.SellLastFillUnderPx = summary.SellSummary?.LastFillUnderPx ?? double.NaN;
            message.SellLastFillTime = summary.SellSummary?.LastFillTime.ToUnixEpoch() ?? DateTime.UnixEpoch.ToUnixEpoch();

            message.SellLowestAttemptedEdgeToTheo = summary.SellSummary?.LowestAttemptedEdgeToTheo ?? double.NaN;
            message.SellHighestFilledEdgeToTheo = summary.SellSummary?.HighestFilledEdgeToTheo ?? double.NaN;

            message.SetId(summary.Id ?? "");

            return message.Limit - offset;
        }

        public int EncodeDataRequestMessage(DirectBuffer directBuffer, int offset, int requestId, SubscriptionFieldType type)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = DataRequestMessage.BlockLength;
            messageHeader.SchemaId = DataRequestMessage.SchemaId;
            messageHeader.TemplateId = DataRequestMessage.TemplateId;
            messageHeader.Version = DataRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            DataRequestMessage message = new DataRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.DataType = (short)type;
            return message.Limit - offset;
        }

        public int EncodeHistoricHighestBidLowestAskRequestMessage(DirectBuffer directBuffer, int offset, int requestId, int tickerId, string symbol)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HistoricHighestBidLowestAskRequestMessage.BlockLength;
            messageHeader.SchemaId = HistoricHighestBidLowestAskRequestMessage.SchemaId;
            messageHeader.TemplateId = HistoricHighestBidLowestAskRequestMessage.TemplateId;
            messageHeader.Version = HistoricHighestBidLowestAskRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            HistoricHighestBidLowestAskRequestMessage message = new();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.TickerId = tickerId;
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeHistoricHighestBidLowestAskResponseMessage(DirectBuffer directBuffer, int offset, int requestId, int tickerId, string symbol, List<HighestBidLowestAskTrackerModel> updates)
        {
            short count = (short)Min(updates.Count, short.MaxValue);
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HistoricHighestBidLowestAskResponseMessage.BlockLength;
            messageHeader.SchemaId = HistoricHighestBidLowestAskResponseMessage.SchemaId;
            messageHeader.TemplateId = HistoricHighestBidLowestAskResponseMessage.TemplateId;
            messageHeader.Version = HistoricHighestBidLowestAskResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            HistoricHighestBidLowestAskResponseMessage message = new();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.TickerId = tickerId;
            message.Count = count;

            HistoricHighestBidLowestAskResponseMessage.UpdatesGroup group = message.UpdatesCount(count);
            for (var index = 0; index < count; index++)
            {
                var update = updates[index];
                group.Next();

                group.StartTime = update.StartTime.ToUnixEpoch();
                group.EndTime = update.EndTime.ToUnixEpoch();
                group.HighestBid = update.HighestBid;
                group.HighestBidUnderlyingMid = update.HighestBidUnderlyingMid;
                group.HighestBidTime = update.HighestBidTime;
                group.LowestAsk = update.LowestAsk;
                group.LowestAskUnderlyingMid = update.LowestAskUnderlyingMid;
                group.LowestAskTime = update.LowestAskTime;
                group.Delta = update.Delta;
            }

            message.SetSymbol(symbol ?? "");
            return message.Limit - offset;
        }

        public int EncodePositionsRequest(DirectBuffer directBuffer, int offset, int requestId, string portfolioName, Data.Enums.PortfolioType portfolioType, Data.Enums.PositionType positionType)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PositionsRequestMessage.BlockLength;
            messageHeader.SchemaId = PositionsRequestMessage.SchemaId;
            messageHeader.TemplateId = PositionsRequestMessage.TemplateId;
            messageHeader.Version = PositionsRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            PositionsRequestMessage message = new PositionsRequestMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.PortfolioType = (PortfolioType)portfolioType;
            message.PositionType = (PositionType)positionType;
            message.SetPortfolioName(portfolioName ?? "");

            return message.Limit - offset;
        }

        public int EncodeMultiplePositionsAdded(DirectBuffer directBuffer, int offset, int requestId, IPortfolio portfolio, IPosition[] positions)
        {
            int instanceFieldLen = 0;
            Dictionary<IPosition, string> positionToLastInstanceMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToLastTraderMap = new Dictionary<IPosition, string>();
            Dictionary<IPosition, string> positionToAccountMap = new Dictionary<IPosition, string>();
            var count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                IPosition position = positions[i];
                if (position == null)
                {
                    break;
                }

                count++;
                string lastInstance = position.LastInstance ?? "";
                positionToLastInstanceMap[position] = lastInstance;
                instanceFieldLen += GetBufferEstimate(lastInstance);

                string lastTrader = position.LastTrader ?? "";
                positionToLastTraderMap[position] = lastTrader;
                instanceFieldLen += GetBufferEstimate(lastTrader);

                string account = position.Account ?? "";
                positionToAccountMap[position] = account;
                instanceFieldLen += GetBufferEstimate(account);

                instanceFieldLen += GetBufferEstimate(position.Name);
                instanceFieldLen += GetBufferEstimate(position.Symbol);
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MultiplePositionsAddedMessage message = new MultiplePositionsAddedMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MultiplePositionsAddedMessage.BlockLength;
            messageHeader.SchemaId = MultiplePositionsAddedMessage.SchemaId;
            messageHeader.TemplateId = MultiplePositionsAddedMessage.TemplateId;
            messageHeader.Version = MultiplePositionsAddedMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.PortfolioId = portfolio.Id;
            message.PortfolioType = (PortfolioType)portfolio.PortfolioType;
            message.PortfolioDate = portfolio.PortfolioDate.ToUnixEpoch();
            message.TotalSubmissions = portfolio.TotalSubmissions;
            message.TotalSingleLegSubmissions = portfolio.TotalSingleLegSubmissions;
            message.TotalSpreadSubmissions = portfolio.TotalSpreadSubmissions;
            message.TotalSingleFills = portfolio.TotalSingleFills;
            message.TotalSpreadFills = portfolio.TotalSpreadFills;
            message.UniqueSubmissions = portfolio.UniqueSubmissions;
            message.UniqueSpreadSubmissions = portfolio.UniqueSpreadSubmissions;
            message.TotalFills = portfolio.TotalFills;
            message.UniqueFills = portfolio.UniqueFills;
            message.UniqueSpreadFills = portfolio.UniqueSpreadFills;
            message.StockContracts = portfolio.StockContracts;
            message.TotalContracts = portfolio.TotalContracts;
            message.UniqueContracts = portfolio.UniqueContracts;
            message.UniqueSpreadContracts = portfolio.UniqueSpreadContracts;
            message.NetQty = portfolio.NetQty;
            message.ShortQty = portfolio.ShortQty;
            message.LongQty = portfolio.LongQty;

            message.FillRate.Mantissa = EncodeMantissa(portfolio.FillRate, message.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.OrderFillRate.Mantissa = EncodeMantissa(portfolio.OrderFillRate, message.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
            message.IbOrderFillRate.Mantissa = EncodeMantissa(portfolio.IbOrderFillRate, message.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            message.LowestRealizedPnl = portfolio.LowestRealizedPnl;
            message.HighestRealizedPnl = portfolio.HighestRealizedPnl;
            message.RealizedPnl = portfolio.RealizedPnl;
            message.LowestAdjustedPnl = portfolio.LowestAdjustedPnl;
            message.HighestAdjustedPnl = portfolio.HighestAdjustedPnl;
            message.AdjustedPnl = portfolio.AdjustedPnl;
            message.SingleLegAdjustedPnl = portfolio.SingleLegAdjustedPnl;
            message.SpreadAdjustedPnl = portfolio.SpreadAdjustedPnl;
            message.UnrealizedPnl = portfolio.UnrealizedPnl;
            message.NetDelta = portfolio.NetDelta;

            message.MaxResubmitEstimate = portfolio.MaxResubmitEstimate;
            message.MaxResubmitForFill = portfolio.MaxResubmitForFill;
            message.AvgResubmitEstimate = portfolio.AvgResubmitEstimate;
            message.AvgResubmitForFill = portfolio.AvgResubmitForFill;

            message.DeltaAdjustedBurn = portfolio.DeltaAdjustedBurn;
            message.DeltaAdjustedHelp = portfolio.DeltaAdjustedHelp;
            message.HighestOpenNotional = portfolio.HighestOpenNotional;
            message.TotalOpenNotional = portfolio.TotalOpenNotional;

            message.TotalOutOfMarketOrders = portfolio.TotalOutOfMarketOrders;
            message.TotalOutOfMarketFills = portfolio.TotalOutOfMarketFills;

            message.SubmissionRatePerSec = portfolio.SubmissionRatePerSec;
            message.MaxOrdersPerSec = portfolio.MaxOrdersPerSec;

            message.WinnerTrades = portfolio.WinnerTrades;
            message.LoserTrades = portfolio.LoserTrades;
            message.SizeWinnerTrades = portfolio.SizeWinnerTrades;
            message.SizeLoserTrades = portfolio.SizeLoserTrades;
            message.AvgCloseSubs = portfolio.AvgCloseSubs;

            message.IntroducingBrokerFee = portfolio.IntroducingBrokerFee;
            message.ExecutingBrokerFee = portfolio.ExecutingBrokerFee;
            message.ExchangeFee = portfolio.ExchangeFee;
            message.OrfFee = portfolio.OrfFee;
            message.SecFee = portfolio.SecFee;
            message.TotalFees = portfolio.TotalFees;

            message.AvgOpenSubsCount = portfolio.AvgOpenSubsCount;
            message.AvgSubsBetweenFillsCount = portfolio.AvgSubsBetweenFillsCount;

            message.GroupSubmissionsAvg = portfolio.GroupSubmissionsAvg;
            message.GroupAvgFillRate.Mantissa = EncodeMantissa(portfolio.GroupAvgFillRate, message.GroupAvgFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

            MultiplePositionsAddedMessage.PositionsGroup positionMessage = message.PositionsCount(count);
            for (int i = 0; i < count; i++)
            {
                positionMessage.Next();
                IPosition position = positions[i];
                positionMessage.ParentPositionId = position.ParentPositionId;
                positionMessage.PositionId = position.Id;
                positionMessage.PositionType = (PositionType)position.PositionType;
                positionMessage.NetQty = position.NetQty;
                positionMessage.RealizedPnl = position.RealizedPnl;
                positionMessage.AdjustedPnl = position.AdjustedPnl;
                positionMessage.UnrealizedPnl = position.UnrealizedPnl;
                positionMessage.NetDelta = position.NetDelta;
                positionMessage.BestSellPrice = position.BestSellPrice;
                positionMessage.BestSellPriceUnderMid = position.BestSellPriceUnderMid;
                positionMessage.BestBuyPrice = position.BestBuyPrice;
                positionMessage.BestBuyPriceUnderMid = position.BestBuyPriceUnderMid;
                positionMessage.TotalSubmissions = position.TotalSubmissions;
                positionMessage.TotalSingleLegSubmissions = position.TotalSingleLegSubmissions;
                positionMessage.TotalSpreadSubmissions = position.TotalSpreadSubmissions;
                positionMessage.TotalSingleFills = position.TotalSingleFills;
                positionMessage.TotalSpreadFills = position.TotalSpreadFills;
                positionMessage.UniqueSubmissions = position.UniqueSubmissions;
                positionMessage.TotalFills = position.TotalFills;
                positionMessage.UniqueFills = position.UniqueFills;
                positionMessage.TotalContracts = position.TotalContracts;
                positionMessage.UniqueContracts = position.UniqueContracts;

                positionMessage.FillRate.Mantissa = EncodeMantissa(position.FillRate, positionMessage.FillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                positionMessage.OrderFillRate.Mantissa = EncodeMantissa(position.OrderFillRate, positionMessage.OrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);
                positionMessage.IbOrderFillRate.Mantissa = EncodeMantissa(position.IbOrderFillRate, positionMessage.IbOrderFillRate.Exponent, DOUBLENULL4.MantissaNullValue);

                positionMessage.OpenPositionAveragePrice = position.OpenPositionAveragePrice;
                positionMessage.OpenPositionFillUnderPrice = position.OpenPositionFillUnderPrice;
                positionMessage.LastTradeTime = ToUnixEpoch(position.LastTradeTime);
                positionMessage.PositionDate = ToUnixEpoch(position.PositionDate);
                positionMessage.LastEdge = position.LastEdge;
                positionMessage.LastBuyEdge = position.LastBuyEdge;
                positionMessage.LastSellEdge = position.LastSellEdge;
                positionMessage.LastBuyEdgeToTheo = position.LastBuyEdgeToTheo;
                positionMessage.LastSellEdgeToTheo = position.LastSellEdgeToTheo;
                positionMessage.LastBuyFillEdgeToTheo = position.LastBuyFillEdgeToTheo;
                positionMessage.LastSellFillEdgeToTheo = position.LastSellFillEdgeToTheo;
                positionMessage.LastBuyAttemptEdgeToTheo = position.LastBuyAttemptEdgeToTheo;
                positionMessage.LastSellAttemptEdgeToTheo = position.LastSellAttemptEdgeToTheo;

                positionMessage.LastPermBuyFillEdgeToTheo = position.LastPermBuyFillEdgeToTheo;
                positionMessage.LastPermSellFillEdgeToTheo = position.LastPermSellFillEdgeToTheo;
                positionMessage.LastPermBuyAttemptEdgeToTheo = position.LastPermBuyAttemptEdgeToTheo;
                positionMessage.LastPermSellAttemptEdgeToTheo = position.LastPermSellAttemptEdgeToTheo;

                positionMessage.BestBuyEdgeToTheo = position.BestBuyEdgeToTheo;
                positionMessage.WorstBuyEdgeToTheo = position.WorstBuyEdgeToTheo;
                positionMessage.BestSellEdgeToTheo = position.BestSellEdgeToTheo;
                positionMessage.WorstSellEdgeToTheo = position.WorstSellEdgeToTheo;
                positionMessage.OpenNotional = position.OpenNotional;

                positionMessage.MaxResubmitEstimate = position.MaxResubmitEstimate;
                positionMessage.MaxResubmitForFill = position.MaxResubmitForFill;
                positionMessage.AvgResubmitEstimate = position.AvgResubmitEstimate;
                positionMessage.AvgResubmitForFill = position.AvgResubmitForFill;

                positionMessage.FirstEdge = position.FirstEdge;

                positionMessage.TotalOutOfMarketOrders = position.TotalOutOfMarketOrders;
                positionMessage.TotalOutOfMarketFills = position.TotalOutOfMarketFills;

                positionMessage.HardSide = position.HardSide.HasValue ? (Generated.Side)position.HardSide : Generated.Side.NULL_VALUE;
                positionMessage.HardSideDesignationTime = position.HardSideDesignationTime.ToUnixEpoch();
                positionMessage.HardSideBuyGiveUp = position.HardSideBuyGiveUp;
                positionMessage.HardSideSellGiveUp = position.HardSideSellGiveUp;

                positionMessage.SubmissionRatePerSec = position.SubmissionRatePerSec;
                positionMessage.MaxOrdersPerSec = position.MaxOrdersPerSec;

                positionMessage.WinnerTrades = position.WinnerTrades;
                positionMessage.LoserTrades = position.LoserTrades;
                positionMessage.SizeWinnerTrades = position.SizeWinnerTrades;
                positionMessage.SizeLoserTrades = position.SizeLoserTrades;
                positionMessage.AvgCloseSubs = position.AvgCloseSubs;
                positionMessage.OpenSubsCount = position.OpenSubsCount;
                positionMessage.SubsBetweenFillsCount = position.SubsBetweenFillsCount;

                positionMessage.IntroducingBrokerFee = position.IntroducingBrokerFee;
                positionMessage.ExecutingBrokerFee = position.ExecutingBrokerFee;
                positionMessage.ExchangeFee = position.ExchangeFee;
                positionMessage.OrfFee = position.OrfFee;
                positionMessage.SecFee = position.SecFee;
                positionMessage.TotalFees = position.TotalFees;
                positionMessage.LastTradeSide = position.LastTradeSide.HasValue ? (Generated.Side)position.LastTradeSide : Generated.Side.NULL_VALUE;

                positionMessage.LastBuyAttempt = position.LastBuyAttempt;
                positionMessage.LastBuyAttemptUnderlying = position.LastBuyAttemptUnderlying;
                positionMessage.LastSellAttempt = position.LastSellAttempt;
                positionMessage.LastSellAttemptUnderlying = position.LastSellAttemptUnderlying;

                positionMessage.RawNetQty = position.RawNetQty;

                if (!positionToLastInstanceMap.TryGetValue(position, out string? lastInsatnce))
                {
                    lastInsatnce = "";
                }
                positionMessage.SetLastInstance(lastInsatnce);

                if (!positionToLastTraderMap.TryGetValue(position, out string? lastTrader))
                {
                    lastTrader = "";
                }
                positionMessage.SetLastTrader(lastTrader);

                if (!positionToAccountMap.TryGetValue(position, out string? account))
                {
                    account = "";
                }
                positionMessage.SetAccount(account);
                positionMessage.SingleLegAdjustedPnl = position.SingleLegAdjustedPnl;
                positionMessage.SpreadAdjustedPnl = position.SpreadAdjustedPnl;
                positionMessage.SetPositionName(position.Name ?? "");
                positionMessage.SetPositionSymbol(position.Symbol ?? "");

            }


            return message.Limit - offset;
        }

        public int EncodeTheoToMarketSpreadUpdate(DirectBuffer directBuffer, int offset, TheoToMarketSpread model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TheoToMarketSpreadUpdate.BlockLength;
            messageHeader.SchemaId = TheoToMarketSpreadUpdate.SchemaId;
            messageHeader.TemplateId = TheoToMarketSpreadUpdate.TemplateId;
            messageHeader.Version = TheoToMarketSpreadUpdate.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            TheoToMarketSpreadUpdate message = new TheoToMarketSpreadUpdate();

            message.WrapForEncode(directBuffer, bufferOffset);

            var tickerId = model.TickerId;
            message.SetTickerId(0, (byte)(tickerId >> 16));
            message.SetTickerId(1, (byte)(tickerId >> 8));
            message.SetTickerId(2, (byte)tickerId);

            message.LastBidTheoSpread = (int)(model.LastBidTheoSpread * 10000);
            message.LastAskTheoSpread = (int)(model.LastAskTheoSpread * 10000);
            message.BidTheoSpreadEma = (int)(model.BidTheoSpreadEma * 10000);
            message.AskTheoSpreadEma = (int)(model.AskTheoSpreadEma * 10000);

            return message.Limit - offset;
        }

        public int EncodePriceChainModel(DirectBuffer directBuffer, int offset, IPriceChainModel model, string? sessionId = null)
        {
            string exchange = model.Exchange ?? "";
            string description = model.Description ?? "";
            string spreadId = model.SpreadId ?? "";
            string spreadType = model.SpreadType ?? "";
            string buySymbol = model.BuySymbol ?? "";
            string sellSymbol = model.SellSymbol ?? "";
            string underSymbol = model.UnderSymbol ?? "";
            string extraTag = model.ExtraTag ?? "";
            sessionId ??= "";

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PriceChainModelMessage message = new PriceChainModelMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PriceChainModelMessage.BlockLength;
            messageHeader.SchemaId = PriceChainModelMessage.SchemaId;
            messageHeader.TemplateId = PriceChainModelMessage.TemplateId;
            messageHeader.Version = PriceChainModelMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);

            message.IsFirm = model.IsFirm ? BooleanEnum.True : BooleanEnum.False;
            message.PossibleFirm = model.PossibleFirm ? BooleanEnum.True : BooleanEnum.False;
            message.PossibleCopyCat = model.PossibleCopyCat ? BooleanEnum.True : BooleanEnum.False;
            message.Uncertain = model.Uncertain ? BooleanEnum.True : BooleanEnum.False;

            message.QtyMismatch = model.QtyMismatch ? BooleanEnum.True : BooleanEnum.False;

            message.ScannerId = (byte)model.EdgeScannerType;

            message.BuyConditionCode = (byte)model.BuyConditionCode;
            message.SellConditionCode = (byte)model.SellConditionCode;

            message.AdjSide = (Generated.Side)model.AdjSide;
            message.IbCobSide = (Generated.Side)model.IbCobSide;

            message.LegsCount = model.LegsCount;

            message.BuyQty = model.BuyQty;
            message.SellQty = model.SellQty;

            message.BuyBidSize = model.BuyBidSize;
            message.BuyAskSize = model.BuyAskSize;
            message.SellBidSize = model.SellBidSize;
            message.SellAskSize = model.SellAskSize;

            message.FlipCount = model.FlipCount;

            message.IbCobBid.Mantissa = EncodeMantissa(model.IbCobBid, message.IbCobBid.Exponent);
            message.IbCobAsk.Mantissa = EncodeMantissa(model.IbCobAsk, message.IbCobAsk.Exponent);
            message.AdjustedPnl.Mantissa = EncodeMantissa(model.AdjustedPnl, message.AdjustedPnl.Exponent);
            message.BuyPrice.Mantissa = EncodeMantissa(model.BuyPrice, message.BuyPrice.Exponent);
            message.BuyTradeOriginalPrice.Mantissa = EncodeMantissa(model.BuyTradeOriginalPrice, message.BuyTradeOriginalPrice.Exponent);
            message.SellPrice.Mantissa = EncodeMantissa(model.SellPrice, message.SellPrice.Exponent);
            message.SellTradeOriginalPrice.Mantissa = EncodeMantissa(model.SellTradeOriginalPrice, message.SellTradeOriginalPrice.Exponent);
            message.BuyEdgeToTheo.Mantissa = EncodeMantissa(model.BuyEdgeToTheo, message.BuyEdgeToTheo.Exponent);
            message.SellEdgeToTheo.Mantissa = EncodeMantissa(model.SellEdgeToTheo, message.SellEdgeToTheo.Exponent);
            message.Ttl.Mantissa = EncodeMantissa(model.Ttl, message.Ttl.Exponent);
            message.SpreadWidth.Mantissa = EncodeMantissa(model.SpreadWidth, message.SpreadWidth.Exponent);
            message.BuyTradeBid.Mantissa = EncodeMantissa(model.BuyTradeBid, message.BuyTradeBid.Exponent);
            message.BuyTradeMid.Mantissa = EncodeMantissa(model.BuyTradeMid, message.BuyTradeMid.Exponent);
            message.BuyTradeAsk.Mantissa = EncodeMantissa(model.BuyTradeAsk, message.BuyTradeAsk.Exponent);
            message.BuyTradeTheo.Mantissa = EncodeMantissa(model.BuyTradeTheo, message.BuyTradeTheo.Exponent);
            message.BuyTradeDelta.Mantissa = EncodeMantissa(model.BuyTradeDelta, message.BuyTradeDelta.Exponent);
            message.SellTradeBid.Mantissa = EncodeMantissa(model.SellTradeBid, message.SellTradeBid.Exponent);
            message.SellTradeMid.Mantissa = EncodeMantissa(model.SellTradeMid, message.SellTradeMid.Exponent);
            message.SellTradeAsk.Mantissa = EncodeMantissa(model.SellTradeAsk, message.SellTradeAsk.Exponent);
            message.SellTradeTheo.Mantissa = EncodeMantissa(model.SellTradeTheo, message.SellTradeTheo.Exponent);
            message.SellTradeDelta.Mantissa = EncodeMantissa(model.SellTradeDelta, message.SellTradeDelta.Exponent);
            message.BuyTradeUnderlyingMid.Mantissa = EncodeMantissa(model.BuyTradeUnderlyingMid, message.BuyTradeUnderlyingMid.Exponent);
            message.SellTradeUnderlyingMid.Mantissa = EncodeMantissa(model.SellTradeUnderlyingMid, message.SellTradeUnderlyingMid.Exponent);
            message.BuyUnderlyingWidth.Mantissa = EncodeMantissa(model.BuyUnderlyingWidth, message.BuyUnderlyingWidth.Exponent);
            message.SellUnderlyingWidth.Mantissa = EncodeMantissa(model.SellUnderlyingWidth, message.SellUnderlyingWidth.Exponent);
            message.DeltaAdjEdge.Mantissa = EncodeMantissa(model.DeltaAdjEdge, message.DeltaAdjEdge.Exponent);
            message.HighestLegDelta.Mantissa = EncodeMantissa(model.HighestLegDelta, message.HighestLegDelta.Exponent);
            message.SpreadWeightedVega.Mantissa = EncodeMantissa(model.SpreadWeightedVega, message.SpreadWeightedVega.Exponent);
            message.ReceiveLatency.Mantissa = EncodeMantissa(model.ReceiveLatency, message.ReceiveLatency.Exponent);
            message.IvPctChange.Mantissa = EncodeMantissa(model.IvPctChange, message.IvPctChange.Exponent);

            message.BuyTime = ToUnixEpoch(model.BuyTime);
            message.SellTime = ToUnixEpoch(model.SellTime);
            message.NearExpiration = ToUnixEpoch(model.NearExpiration);
            message.FarExpiration = ToUnixEpoch(model.FarExpiration);

            message.PriceChainTradePrice = model.PriceChainTradePrice;
            message.PriceChainTotalBidDeviations = model.PriceChainTotalBidDeviations;
            message.PriceChainTotalAskDeviations = model.PriceChainTotalAskDeviations;
            message.PriceChainDeviationSequence = model.PriceChainDeviationSequence;
            message.PriceChainRecentBidDeviation = model.PriceChainRecentBidDeviation;
            message.PriceChainRecentBidDeviationTimeDiff = model.PriceChainRecentBidDeviationTimeDiff;
            message.PriceChainRecentBidDeviationUnderBid = model.PriceChainRecentBidDeviationUnderBid;
            message.PriceChainRecentBidDeviationUnderAsk = model.PriceChainRecentBidDeviationUnderAsk;
            message.PriceChainRecentBidDeviationBid = model.PriceChainRecentBidDeviationBid;
            message.PriceChainRecentBidDeviationAsk = model.PriceChainRecentBidDeviationAsk;
            message.PriceChainRecentAskDeviation = model.PriceChainRecentAskDeviation;
            message.PriceChainRecentAskDeviationTimeDiff = model.PriceChainRecentAskDeviationTimeDiff;
            message.PriceChainRecentAskDeviationUnderBid = model.PriceChainRecentAskDeviationUnderBid;
            message.PriceChainRecentAskDeviationUnderAsk = model.PriceChainRecentAskDeviationUnderAsk;
            message.PriceChainRecentAskDeviationBid = model.PriceChainRecentAskDeviationBid;
            message.PriceChainRecentAskDeviationAsk = model.PriceChainRecentAskDeviationAsk;
            message.PriceChainHighestBidDeviation = model.PriceChainHighestBidDeviation;
            message.PriceChainHighestBidDeviationTimeDiff = model.PriceChainHighestBidDeviationTimeDiff;
            message.PriceChainHighestBidDeviationUnderBid = model.PriceChainHighestBidDeviationUnderBid;
            message.PriceChainHighestBidDeviationUnderAsk = model.PriceChainHighestBidDeviationUnderAsk;
            message.PriceChainHighestBidDeviationBid = model.PriceChainHighestBidDeviationBid;
            message.PriceChainHighestBidDeviationAsk = model.PriceChainHighestBidDeviationAsk;
            message.PriceChainHighestAskDeviation = model.PriceChainHighestAskDeviation;
            message.PriceChainHighestAskDeviationTimeDiff = model.PriceChainHighestAskDeviationTimeDiff;
            message.PriceChainHighestAskDeviationUnderBid = model.PriceChainHighestAskDeviationUnderBid;
            message.PriceChainHighestAskDeviationUnderAsk = model.PriceChainHighestAskDeviationUnderAsk;
            message.PriceChainHighestAskDeviationBid = model.PriceChainHighestAskDeviationBid;
            message.PriceChainHighestAskDeviationAsk = model.PriceChainHighestAskDeviationAsk;

            message.PriceChainRecentBidDeviationIvOffset = model.PriceChainRecentBidDeviationIvOffset;
            message.PriceChainHighestBidDeviationIvOffset = model.PriceChainHighestBidDeviationIvOffset;
            message.PriceChainRecentAskDeviationIvOffset = model.PriceChainRecentAskDeviationIvOffset;
            message.PriceChainHighestAskDeviationIvOffset = model.PriceChainHighestAskDeviationIvOffset;

            message.SetUnderSymbol(underSymbol);
            message.SetDescription(description);
            message.SetSpreadId(spreadId);
            message.SetSpreadType(spreadType);
            message.SetBuySymbol(buySymbol);
            message.SetSellSymbol(sellSymbol);
            message.SetExtraTag(extraTag);
            message.SetExchange(exchange);
            message.SetSessionId(sessionId);

            return message.Limit - offset;
        }

        public int EncodeMatrixSyntheticSpreadMessage(DirectBuffer directBuffer, int offset, SyntheticSpread model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MatrixSyntheticSpreadMessage.BlockLength;
            messageHeader.SchemaId = MatrixSyntheticSpreadMessage.SchemaId;
            messageHeader.TemplateId = MatrixSyntheticSpreadMessage.TemplateId;
            messageHeader.Version = MatrixSyntheticSpreadMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            MatrixSyntheticSpreadMessage message = new MatrixSyntheticSpreadMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.InstrumentType = model.InstrumentType != null
                ? (byte)model.InstrumentType.Value
                : MatrixSyntheticSpreadMessage.InstrumentTypeNullValue;
            message.ExecutionType = model.ExecutionType != null
                ? (byte)model.ExecutionType.Value
                : MatrixSyntheticSpreadMessage.ExecutionTypeNullValue;
            message.Strategy = model.Strategy != null
                ? (byte)model.Strategy.Value
                : MatrixSyntheticSpreadMessage.StrategyNullValue;
            message.OpenClose = model.OpenClose != null
                ? (byte)model.OpenClose.Value
                : MatrixSyntheticSpreadMessage.OpenCloseNullValue;

            message.Tif = (byte)model.Tif;
            message.TifTake = (byte)model.TifTake;

            message.PegMethod = model.PegMethod != null
                ? (byte)model.PegMethod.Value
                : MatrixSyntheticSpreadMessage.PegMethodNullValue;
            message.PegDirection = model.PegDirection != null
                ? (byte)model.PegDirection.Value
                : MatrixSyntheticSpreadMessage.PegDirectionNullValue;

            message.Price.Mantissa = EncodeMantissa(model.Price, message.Price.Exponent, DOUBLENULL2.MantissaNullValue);
            message.PegOffset.Mantissa = model.PegOffset != null
                ? EncodeMantissa(model.PegOffset.Value, message.PegOffset.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.Discretion.Mantissa = model.Discretion != null
                ? EncodeMantissa(model.Discretion.Value, message.Discretion.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.OrderQuantity = model.OrderQuantity;

            message.DisplayQty = model.DisplayQty != null
                ? model.DisplayQty.Value
                : MatrixSyntheticSpreadMessage.DisplayQtyNullValue;

            message.RemoveOnOut = model.RemoveOnOut != null
                ? model.RemoveOnOut.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.ExtTradingHours = model.ExtTradingHours != null
                ? model.ExtTradingHours.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.CancelDelay = model.CancelDelay;
            message.UserId = model.UserId;
            message.RiskCheckId = model.RiskCheckId;

            var strategyData = model.StrategyData;

            message.StrDataType = (byte)strategyData.Type;
            message.StrDataInstrumentType = (byte)strategyData.InstrumentType;
            message.StrDataTakeHidden = strategyData.TakeHidden != null
                ? strategyData.TakeHidden.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataAtsMode = strategyData.AtsMode != null
                ? strategyData.AtsMode.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataCancelOnHalt = strategyData.CancelOnHalt != null
                ? strategyData.CancelOnHalt.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataSpreadPriceDiscretion = strategyData.SpreadPriceDiscretion != null
                ? strategyData.SpreadPriceDiscretion.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataSeparateEquityLeg = strategyData.SeparateEquityLeg != null
                ? strategyData.SeparateEquityLeg.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataExtTradingHours = strategyData.ExtTradingHours != null
                ? strategyData.ExtTradingHours.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataLeggingOnly = strategyData.LeggingOnly != null
                ? strategyData.LeggingOnly.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataSynthFeeOptimal = strategyData.SynthFeeOptimal != null
                ? strategyData.SynthFeeOptimal.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataSynthComplexTakeOnly = strategyData.SynthComplexTakeOnly != null
                ? strategyData.SynthComplexTakeOnly.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataHedge = strategyData.Hedge != null
                ? strategyData.Hedge.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.StrDataMakeTake = strategyData.MakeTake != null
                ? (byte)strategyData.MakeTake.Value
                : MatrixSyntheticSpreadMessage.StrDataMakeTakeNullValue;
            message.StrDataAlgorithm = strategyData.Algorithm != null
                ? (byte)strategyData.Algorithm.Value
                : MatrixSyntheticSpreadMessage.StrDataAlgorithmNullValue;
            message.StrDataPriceMethod = strategyData.PriceMethod != null
                ? (byte)strategyData.PriceMethod.Value
                : MatrixSyntheticSpreadMessage.StrDataPriceMethodNullValue;
            message.StrDataSynthPassiveMode = strategyData.SynthPassiveMode != null
                ? (byte)strategyData.SynthPassiveMode.Value
                : MatrixSyntheticSpreadMessage.StrDataSynthPassiveModeNullValue;
            message.StrDataLegExecType = strategyData.LegExecType != null
                ? (byte)strategyData.LegExecType.Value
                : MatrixSyntheticSpreadMessage.StrDataLegExecTypeNullValue;
            message.StrDataLegTif = strategyData.LegTif != null
                ? (byte)strategyData.LegTif.Value
                : MatrixSyntheticSpreadMessage.StrDataLegTifNullValue;

            message.StrDataReminderQty = strategyData.ReminderQty != null
                ? strategyData.ReminderQty.Value
                : MatrixSyntheticSpreadMessage.StrDataReminderQtyNullValue;
            message.StrDataMinWorkingQty = strategyData.MinWorkingQty != null
                ? strategyData.MinWorkingQty.Value
                : MatrixSyntheticSpreadMessage.StrDataMinWorkingQtyNullValue;
            message.StrDataMinQuoteQty = strategyData.MinQuoteQty != null
                ? strategyData.MinQuoteQty.Value
                : MatrixSyntheticSpreadMessage.StrDataMinQuoteQtyNullValue;
            message.StrDataNumOfTries = strategyData.NumOfTries != null
                ? strategyData.NumOfTries.Value
                : MatrixSyntheticSpreadMessage.StrDataNumOfTriesNullValue;
            message.StrDataBadRatioTryThreshold = strategyData.BadRatioTryThreshold != null
                ? strategyData.BadRatioTryThreshold.Value
                : MatrixSyntheticSpreadMessage.StrDataBadRatioTryThresholdNullValue;
            message.StrDataWorkingQty = strategyData.WorkingQty != null
                ? strategyData.WorkingQty.Value
                : MatrixSyntheticSpreadMessage.StrDataWorkingQtyNullValue;
            message.StrDataSynthPassiveCancelDelayMs = strategyData.SynthPassiveCancelDelayMs != null
                ? strategyData.SynthPassiveCancelDelayMs.Value
                : MatrixSyntheticSpreadMessage.StrDataSynthPassiveCancelDelayMsNullValue;
            message.StrDataLegTimeout = strategyData.LegTimeout != null
                ? strategyData.LegTimeout.Value
                : MatrixSyntheticSpreadMessage.StrDataLegTimeoutNullValue;
            message.StrDataDisplayQty = strategyData.DisplayQty != null
                ? strategyData.DisplayQty.Value
                : MatrixSyntheticSpreadMessage.StrDataDisplayQtyNullValue;
            message.StrDataQtyIncluded = strategyData.QtyIncluded != null
                ? strategyData.QtyIncluded.Value
                : MatrixSyntheticSpreadMessage.StrDataQtyIncludedNullValue;

            message.StrDataBadRatioTimeout.Mantissa = strategyData.BadRatioTimeout != null
                ? EncodeMantissa(strategyData.BadRatioTimeout.Value, message.StrDataBadRatioTimeout.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataDiscretionTake.Mantissa = strategyData.DiscretionTake != null
                ? EncodeMantissa(strategyData.DiscretionTake.Value, message.StrDataDiscretionTake.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataUndPrice.Mantissa = strategyData.UndPrice != null
                ? EncodeMantissa(strategyData.UndPrice.Value, message.StrDataUndPrice.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMaxPriceUnd.Mantissa = strategyData.MaxPriceUnd != null
                ? EncodeMantissa(strategyData.MaxPriceUnd.Value, message.StrDataMaxPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMinPriceUnd.Mantissa = strategyData.MinPriceUnd != null
                ? EncodeMantissa(strategyData.MinPriceUnd.Value, message.StrDataMinPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataPriceRange.Mantissa = strategyData.PriceRange != null
                ? EncodeMantissa(strategyData.PriceRange.Value, message.StrDataPriceRange.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataBadRatioPriceDiscretion.Mantissa = strategyData.BadRatioPriceDiscretion != null
                ? EncodeMantissa(strategyData.BadRatioPriceDiscretion.Value, message.StrDataBadRatioPriceDiscretion.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            var exchangesCount = strategyData.Exchanges?.Count ?? 0;
            MatrixSyntheticSpreadMessage.StrDataExchangesGroup? exchangesGroup = message.StrDataExchangesCount(exchangesCount);
            if (exchangesCount > 0)
            {
                foreach (var exchange in strategyData.Exchanges!)
                {
                    exchangesGroup.Next();
                    exchangesGroup.SetExchange(exchange);
                }
            }

            var exchangesTakeCount = strategyData.ExchangesTake?.Count ?? 0;
            MatrixSyntheticSpreadMessage.StrDataExchangesTakeGroup? exchangesTakeGroup = message.StrDataExchangesTakeCount(exchangesTakeCount);
            if (exchangesTakeCount > 0)
            {
                foreach (var exchange in strategyData.ExchangesTake!)
                {
                    exchangesTakeGroup.Next();
                    exchangesTakeGroup.SetExchange(exchange);
                }
            }

            MatrixSyntheticSpreadMessage.LegsGroup? legsGroup = message.LegsCount(model.LegsCount);
            for (int i = 0; i < model.LegsCount; i++)
            {
                legsGroup.Next();
                var leg = model.Legs[i];
                legsGroup.InstrumentType = (byte)leg.InstrumentType;
                legsGroup.Side = (byte)leg.Side;
                legsGroup.LegRatio = leg.LegRatio;
                legsGroup.OpenClose = leg.OpenClose != null
                    ? (byte)leg.OpenClose.Value
                    : MatrixSyntheticSpreadMessage.LegsGroup.OpenCloseNullValue;
                legsGroup.SetSymbol(leg.Symbol ?? "");
                legsGroup.SetClientGuid(leg.ClientGuid ?? "");
            }

            message.SetClientGuid(model.ClientGuid ?? "");
            message.SetAccount(model.Account ?? "");
            message.SetExchange(model.Exchange ?? "");
            message.SetMemo(model.Memo ?? "");
            message.SetSource(model.Source ?? "");
            message.SetDestination(model.Destination ?? "");

            return message.Limit - offset;
        }

        public int EncodeMatrixScrapeMessage(DirectBuffer directBuffer, int offset, Scrape model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MatrixScrapeMessage.BlockLength;
            messageHeader.SchemaId = MatrixScrapeMessage.SchemaId;
            messageHeader.TemplateId = MatrixScrapeMessage.TemplateId;
            messageHeader.Version = MatrixScrapeMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            MatrixScrapeMessage message = new MatrixScrapeMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.InstrumentType = model.InstrumentType != null
                ? (byte)model.InstrumentType.Value
                : MatrixScrapeMessage.InstrumentTypeNullValue;
            message.ExecutionType = model.ExecutionType != null
                ? (byte)model.ExecutionType.Value
                : MatrixScrapeMessage.ExecutionTypeNullValue;
            message.Strategy = model.Strategy != null
                ? (byte)model.Strategy.Value
                : MatrixScrapeMessage.StrategyNullValue;
            message.OpenClose = model.OpenClose != null
                ? (byte)model.OpenClose.Value
                : MatrixScrapeMessage.OpenCloseNullValue;
            message.Side = model.Side != null
                ? (byte)model.Side.Value
                : MatrixScrapeMessage.SideNullValue;

            message.Tif = (byte)model.Tif;
            message.TifTake = (byte)model.TifTake;

            message.PegMethod = model.PegMethod != null
                ? (byte)model.PegMethod.Value
                : MatrixScrapeMessage.PegMethodNullValue;
            message.PegDirection = model.PegDirection != null
                ? (byte)model.PegDirection.Value
                : MatrixScrapeMessage.PegDirectionNullValue;

            message.Price.Mantissa = EncodeMantissa(model.Price, message.Price.Exponent, DOUBLENULL2.MantissaNullValue);
            message.PegOffset.Mantissa = model.PegOffset != null
                ? EncodeMantissa(model.PegOffset.Value, message.PegOffset.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.Discretion.Mantissa = model.Discretion != null
                ? EncodeMantissa(model.Discretion.Value, message.Discretion.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.OrderQuantity = model.OrderQuantity;

            message.DisplayQty = model.DisplayQty != null
                ? model.DisplayQty.Value
                : MatrixScrapeMessage.DisplayQtyNullValue;

            message.RemoveOnOut = model.RemoveOnOut != null
                ? model.RemoveOnOut.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.ExtTradingHours = model.ExtTradingHours != null
                ? model.ExtTradingHours.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.CancelDelay = model.CancelDelay;
            message.MinimumTickStyle = (Generated.MinimumTickStyle)model.MinimumTickStyle;
            message.UserId = model.UserId;
            message.RiskCheckId = model.RiskCheckId;

            var strategyData = model.StrategyData;

            message.StrDataType = (byte)strategyData.Type;
            message.StrDataInstrumentType = (byte)strategyData.InstrumentType;
            message.StrDataTakeHidden = strategyData.TakeHidden != null
                ? strategyData.TakeHidden.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataAtsMode = strategyData.AtsMode != null
                ? strategyData.AtsMode.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataCancelOnHalt = strategyData.CancelOnHalt != null
                ? strategyData.CancelOnHalt.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.StrDataMakeTake = strategyData.MakeTake != null
                ? (byte)strategyData.MakeTake.Value
                : MatrixScrapeMessage.StrDataMakeTakeNullValue;
            message.StrDataAlgorithm = strategyData.Algorithm != null
                ? (byte)strategyData.Algorithm.Value
                : MatrixScrapeMessage.StrDataAlgorithmNullValue;
            message.StrDataPriceMethod = strategyData.PriceMethod != null
                ? (byte)strategyData.PriceMethod.Value
                : MatrixScrapeMessage.StrDataPriceMethodNullValue;

            message.StrDataReminderQty = strategyData.ReminderQty != null
                ? strategyData.ReminderQty.Value
                : MatrixScrapeMessage.StrDataReminderQtyNullValue;
            message.StrDataMinWorkingQty = strategyData.MinWorkingQty != null
                ? strategyData.MinWorkingQty.Value
                : MatrixScrapeMessage.StrDataMinWorkingQtyNullValue;
            message.StrDataMinQuoteQty = strategyData.MinQuoteQty != null
                ? strategyData.MinQuoteQty.Value
                : MatrixScrapeMessage.StrDataMinQuoteQtyNullValue;

            message.StrDataWorkingQty = strategyData.WorkingQty != null
                ? strategyData.WorkingQty.Value
                : MatrixScrapeMessage.StrDataWorkingQtyNullValue;

            message.StrDataDiscretionTake.Mantissa = strategyData.DiscretionTake != null
                ? EncodeMantissa(strategyData.DiscretionTake.Value, message.StrDataDiscretionTake.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataUndPrice.Mantissa = strategyData.UndPrice != null
                ? EncodeMantissa(strategyData.UndPrice.Value, message.StrDataUndPrice.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMaxPriceUnd.Mantissa = strategyData.MaxPriceUnd != null
                ? EncodeMantissa(strategyData.MaxPriceUnd.Value, message.StrDataMaxPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMinPriceUnd.Mantissa = strategyData.MinPriceUnd != null
                ? EncodeMantissa(strategyData.MinPriceUnd.Value, message.StrDataMinPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataPriceRange.Mantissa = strategyData.PriceRange != null
                ? EncodeMantissa(strategyData.PriceRange.Value, message.StrDataPriceRange.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.StrDataLimitToMarketTime = strategyData.LimitToMarketTime != null
                ? strategyData.LimitToMarketTime.Value.ToUnixEpoch()
                : MatrixScrapeMessage.StrDataLimitToMarketTimeNullValue;

            var exchangesCount = strategyData.Exchanges?.Count ?? 0;
            MatrixScrapeMessage.StrDataExchangesGroup? exchangesGroup = message.StrDataExchangesCount(exchangesCount);
            if (exchangesCount > 0)
            {
                foreach (var exchange in strategyData.Exchanges!)
                {
                    exchangesGroup.Next();
                    exchangesGroup.SetExchange(exchange);
                }
            }

            var exchangesTakeCount = strategyData.ExchangesTake?.Count ?? 0;
            MatrixScrapeMessage.StrDataExchangesTakeGroup? exchangesTakeGroup = message.StrDataExchangesTakeCount(exchangesTakeCount);
            if (exchangesTakeCount > 0)
            {
                foreach (var exchange in strategyData.ExchangesTake!)
                {
                    exchangesTakeGroup.Next();
                    exchangesTakeGroup.SetExchange(exchange);
                }
            }

            message.SetClientGuid(model.ClientGuid ?? "");
            message.SetAccount(model.Account ?? "");
            message.SetSymbol(model.Symbol ?? "");
            message.SetExchange(model.Exchange ?? "");
            message.SetMemo(model.Memo ?? "");
            message.SetSource(model.Source ?? "");
            message.SetDestination(model.Destination ?? "");

            return message.Limit - offset;
        }

        public int EncodeMatrixSeekerMessage(DirectBuffer directBuffer, int offset, Seeker model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MatrixSeekerMessage.BlockLength;
            messageHeader.SchemaId = MatrixSeekerMessage.SchemaId;
            messageHeader.TemplateId = MatrixSeekerMessage.TemplateId;
            messageHeader.Version = MatrixSeekerMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            MatrixSeekerMessage message = new MatrixSeekerMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.InstrumentType = model.InstrumentType != null
                ? (byte)model.InstrumentType.Value
                : MatrixSeekerMessage.InstrumentTypeNullValue;
            message.ExecutionType = model.ExecutionType != null
                ? (byte)model.ExecutionType.Value
                : MatrixSeekerMessage.ExecutionTypeNullValue;
            message.Strategy = model.Strategy != null
                ? (byte)model.Strategy.Value
                : MatrixSeekerMessage.StrategyNullValue;
            message.OpenClose = model.OpenClose != null
                ? (byte)model.OpenClose.Value
                : MatrixSeekerMessage.OpenCloseNullValue;
            message.Side = model.Side != null
                ? (byte)model.Side.Value
                : MatrixSeekerMessage.SideNullValue;

            message.Tif = (byte)model.Tif;
            message.TifTake = (byte)model.TifTake;

            message.PegMethod = model.PegMethod != null
                ? (byte)model.PegMethod.Value
                : MatrixSeekerMessage.PegMethodNullValue;
            message.PegDirection = model.PegDirection != null
                ? (byte)model.PegDirection.Value
                : MatrixSeekerMessage.PegDirectionNullValue;

            message.Price.Mantissa = EncodeMantissa(model.Price, message.Price.Exponent, DOUBLENULL2.MantissaNullValue);
            message.PegOffset.Mantissa = model.PegOffset != null
                ? EncodeMantissa(model.PegOffset.Value, message.PegOffset.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.Discretion.Mantissa = model.Discretion != null
                ? EncodeMantissa(model.Discretion.Value, message.Discretion.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.OrderQuantity = model.OrderQuantity;

            message.DisplayQty = model.DisplayQty != null
                ? model.DisplayQty.Value
                : MatrixSeekerMessage.DisplayQtyNullValue;

            message.RemoveOnOut = model.RemoveOnOut != null
                ? model.RemoveOnOut.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.ExtTradingHours = model.ExtTradingHours != null
                ? model.ExtTradingHours.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.CancelDelay = model.CancelDelay;
            message.MinimumTickStyle = (Generated.MinimumTickStyle)model.MinimumTickStyle;

            message.UserId = model.UserId;
            message.RiskCheckId = model.RiskCheckId;

            var strategyData = model.StrategyData;

            message.StrDataType = (byte)strategyData.Type;
            message.StrDataInstrumentType = (byte)strategyData.InstrumentType;
            message.StrDataTakeHidden = strategyData.TakeHidden != null
                ? strategyData.TakeHidden.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataAtsMode = strategyData.AtsMode != null
                ? strategyData.AtsMode.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataCancelOnHalt = strategyData.CancelOnHalt != null
                ? strategyData.CancelOnHalt.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.StrDataMakeTake = strategyData.MakeTake != null
                ? (byte)strategyData.MakeTake.Value
                : MatrixSeekerMessage.StrDataMakeTakeNullValue;
            message.StrDataAlgorithm = strategyData.Algorithm != null
                ? (byte)strategyData.Algorithm.Value
                : MatrixSeekerMessage.StrDataAlgorithmNullValue;
            message.StrDataPriceMethod = strategyData.PriceMethod != null
                ? (byte)strategyData.PriceMethod.Value
                : MatrixSeekerMessage.StrDataPriceMethodNullValue;

            message.StrDataReminderQty = strategyData.ReminderQty != null
                ? strategyData.ReminderQty.Value
                : MatrixSeekerMessage.StrDataReminderQtyNullValue;
            message.StrDataMinWorkingQty = strategyData.MinWorkingQty != null
                ? strategyData.MinWorkingQty.Value
                : MatrixSeekerMessage.StrDataMinWorkingQtyNullValue;
            message.StrDataMinQuoteQty = strategyData.MinQuoteQty != null
                ? strategyData.MinQuoteQty.Value
                : MatrixSeekerMessage.StrDataMinQuoteQtyNullValue;

            message.StrDataWorkingQty = strategyData.WorkingQty != null
                ? strategyData.WorkingQty.Value
                : MatrixSeekerMessage.StrDataWorkingQtyNullValue;

            message.StrDataDiscretionTake.Mantissa = strategyData.DiscretionTake != null
                ? EncodeMantissa(strategyData.DiscretionTake.Value, message.StrDataDiscretionTake.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataUndPrice.Mantissa = strategyData.UndPrice != null
                ? EncodeMantissa(strategyData.UndPrice.Value, message.StrDataUndPrice.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMaxPriceUnd.Mantissa = strategyData.MaxPriceUnd != null
                ? EncodeMantissa(strategyData.MaxPriceUnd.Value, message.StrDataMaxPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMinPriceUnd.Mantissa = strategyData.MinPriceUnd != null
                ? EncodeMantissa(strategyData.MinPriceUnd.Value, message.StrDataMinPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataPriceRange.Mantissa = strategyData.PriceRange != null
                ? EncodeMantissa(strategyData.PriceRange.Value, message.StrDataPriceRange.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            var exchangesCount = strategyData.Exchanges?.Count ?? 0;
            MatrixSeekerMessage.StrDataExchangesGroup? exchangesGroup = message.StrDataExchangesCount(exchangesCount);
            if (exchangesCount > 0)
            {
                foreach (var exchange in strategyData.Exchanges!)
                {
                    exchangesGroup.Next();
                    exchangesGroup.SetExchange(exchange);
                }
            }

            var exchangesTakeCount = strategyData.ExchangesTake?.Count ?? 0;
            MatrixSeekerMessage.StrDataExchangesTakeGroup? exchangesTakeGroup = message.StrDataExchangesTakeCount(exchangesTakeCount);
            if (exchangesTakeCount > 0)
            {
                foreach (var exchange in strategyData.ExchangesTake!)
                {
                    exchangesTakeGroup.Next();
                    exchangesTakeGroup.SetExchange(exchange);
                }
            }

            message.SetClientGuid(model.ClientGuid ?? "");
            message.SetAccount(model.Account ?? "");
            message.SetAccount(model.Symbol ?? "");
            message.SetExchange(model.Exchange ?? "");
            message.SetMemo(model.Memo ?? "");
            message.SetSource(model.Source ?? "");
            message.SetDestination(model.Destination ?? "");

            return message.Limit - offset;
        }

        public int EncodeMatrixSeekerSpreadMessage(DirectBuffer directBuffer, int offset, SeekerSpread model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SeekerSpreadMessage.BlockLength;
            messageHeader.SchemaId = SeekerSpreadMessage.SchemaId;
            messageHeader.TemplateId = SeekerSpreadMessage.TemplateId;
            messageHeader.Version = SeekerSpreadMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SeekerSpreadMessage message = new SeekerSpreadMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.InstrumentType = model.InstrumentType != null
                ? (byte)model.InstrumentType.Value
                : SeekerSpreadMessage.InstrumentTypeNullValue;
            message.ExecutionType = model.ExecutionType != null
                ? (byte)model.ExecutionType.Value
                : SeekerSpreadMessage.ExecutionTypeNullValue;
            message.Strategy = model.Strategy != null
                ? (byte)model.Strategy.Value
                : SeekerSpreadMessage.StrategyNullValue;
            message.OpenClose = model.OpenClose != null
                ? (byte)model.OpenClose.Value
                : SeekerSpreadMessage.OpenCloseNullValue;

            message.Tif = (byte)model.Tif;
            message.TifTake = (byte)model.TifTake;

            message.PegMethod = model.PegMethod != null
                ? (byte)model.PegMethod.Value
                : SeekerSpreadMessage.PegMethodNullValue;
            message.PegDirection = model.PegDirection != null
                ? (byte)model.PegDirection.Value
                : SeekerSpreadMessage.PegDirectionNullValue;

            message.Price.Mantissa = EncodeMantissa(model.Price, message.Price.Exponent, DOUBLENULL2.MantissaNullValue);
            message.PegOffset.Mantissa = model.PegOffset != null
                ? EncodeMantissa(model.PegOffset.Value, message.PegOffset.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.Discretion.Mantissa = model.Discretion != null
                ? EncodeMantissa(model.Discretion.Value, message.Discretion.Exponent, DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.OrderQuantity = model.OrderQuantity;

            message.DisplayQty = model.DisplayQty != null
                ? model.DisplayQty.Value
                : SeekerSpreadMessage.DisplayQtyNullValue;

            message.RemoveOnOut = model.RemoveOnOut != null
                ? model.RemoveOnOut.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.ExtTradingHours = model.ExtTradingHours != null
                ? model.ExtTradingHours.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.CancelDelay = model.CancelDelay;

            message.UserId = model.UserId;
            message.RiskCheckId = model.RiskCheckId;

            var strategyData = model.StrategyData;

            message.StrDataType = (byte)strategyData.Type;
            message.StrDataInstrumentType = (byte)strategyData.InstrumentType;
            message.StrDataTakeHidden = strategyData.TakeHidden != null
                ? strategyData.TakeHidden.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataAtsMode = strategyData.AtsMode != null
                ? strategyData.AtsMode.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataCancelOnHalt = strategyData.CancelOnHalt != null
                ? strategyData.CancelOnHalt.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.StrDataMakeTake = strategyData.MakeTake != null
                ? (byte)strategyData.MakeTake.Value
                : SeekerSpreadMessage.StrDataMakeTakeNullValue;
            message.StrDataAlgorithm = strategyData.Algorithm != null
                ? (byte)strategyData.Algorithm.Value
                : SeekerSpreadMessage.StrDataAlgorithmNullValue;
            message.StrDataPriceMethod = strategyData.PriceMethod != null
                ? (byte)strategyData.PriceMethod.Value
                : SeekerSpreadMessage.StrDataPriceMethodNullValue;

            message.StrDataReminderQty = strategyData.ReminderQty != null
                ? strategyData.ReminderQty.Value
                : SeekerSpreadMessage.StrDataReminderQtyNullValue;
            message.StrDataMinWorkingQty = strategyData.MinWorkingQty != null
                ? strategyData.MinWorkingQty.Value
                : SeekerSpreadMessage.StrDataMinWorkingQtyNullValue;
            message.StrDataMinQuoteQty = strategyData.MinQuoteQty != null
                ? strategyData.MinQuoteQty.Value
                : SeekerSpreadMessage.StrDataMinQuoteQtyNullValue;

            message.StrDataWorkingQty = strategyData.WorkingQty != null
                ? strategyData.WorkingQty.Value
                : SeekerSpreadMessage.StrDataWorkingQtyNullValue;

            message.StrDataDiscretionTake.Mantissa = strategyData.DiscretionTake != null
                ? EncodeMantissa(strategyData.DiscretionTake.Value, message.StrDataDiscretionTake.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataUndPrice.Mantissa = strategyData.UndPrice != null
                ? EncodeMantissa(strategyData.UndPrice.Value, message.StrDataUndPrice.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMaxPriceUnd.Mantissa = strategyData.MaxPriceUnd != null
                ? EncodeMantissa(strategyData.MaxPriceUnd.Value, message.StrDataMaxPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMinPriceUnd.Mantissa = strategyData.MinPriceUnd != null
                ? EncodeMantissa(strategyData.MinPriceUnd.Value, message.StrDataMinPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataPriceRange.Mantissa = strategyData.PriceRange != null
                ? EncodeMantissa(strategyData.PriceRange.Value, message.StrDataPriceRange.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            var exchangesCount = strategyData.Exchanges?.Count ?? 0;
            SeekerSpreadMessage.StrDataExchangesGroup? exchangesGroup = message.StrDataExchangesCount(exchangesCount);
            if (exchangesCount > 0)
            {
                foreach (var exchange in strategyData.Exchanges!)
                {
                    exchangesGroup.Next();
                    exchangesGroup.SetExchange(exchange);
                }
            }

            var exchangesTakeCount = strategyData.ExchangesTake?.Count ?? 0;
            SeekerSpreadMessage.StrDataExchangesTakeGroup? exchangesTakeGroup = message.StrDataExchangesTakeCount(exchangesTakeCount);
            if (exchangesTakeCount > 0)
            {
                foreach (var exchange in strategyData.ExchangesTake!)
                {
                    exchangesTakeGroup.Next();
                    exchangesTakeGroup.SetExchange(exchange);
                }
            }

            SeekerSpreadMessage.LegsGroup? legsGroup = message.LegsCount(model.LegsCount);
            for (int i = 0; i < model.LegsCount; i++)
            {
                legsGroup.Next();
                var leg = model.Legs[i];
                legsGroup.InstrumentType = (byte)leg.InstrumentType;
                legsGroup.Side = (byte)leg.Side;
                legsGroup.LegRatio = leg.LegRatio;
                legsGroup.OpenClose = leg.OpenClose != null
                    ? (byte)leg.OpenClose.Value
                    : SeekerSpreadMessage.LegsGroup.OpenCloseNullValue;
                legsGroup.SetSymbol(leg.Symbol ?? "");
                legsGroup.SetClientGuid(leg.ClientGuid ?? "");
            }

            message.SetClientGuid(model.ClientGuid ?? "");
            message.SetAccount(model.Account ?? "");
            message.SetExchange(model.Exchange ?? "");
            message.SetMemo(model.Memo ?? "");
            message.SetSource(model.Source ?? "");
            message.SetDestination(model.Destination ?? "");

            return message.Limit - offset;
        }

        public int EncodeTransactionMessage(DirectBuffer directBuffer, int offset, Transaction model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ExecutionTransactionMessage.BlockLength;
            messageHeader.SchemaId = ExecutionTransactionMessage.SchemaId;
            messageHeader.TemplateId = ExecutionTransactionMessage.TemplateId;
            messageHeader.Version = ExecutionTransactionMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            ExecutionTransactionMessage message = new ExecutionTransactionMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.UpdateTime = model.UpdateTime.ToUnixEpoch();
            message.Venue = model.Venue.HasValue ? (byte)model.Venue : ExecutionTransactionMessage.VenueNullValue;
            message.SetAccount(model.Account ?? "");
            message.SetOrderId(model.OrderId ?? "");
            message.SetUnderlying(model.Underlying ?? "");
            message.SetSymbol(model.Symbol ?? "");
            message.SetTrader(model.Trader ?? "");
            message.Price.Mantissa = EncodeMantissa(model.Price, message.Price.Exponent, DOUBLENULL2.MantissaNullValue);
            message.Side = (byte)model.Side;
            message.OrderQty = model.OrderQty;
            message.FilledQty = model.Qty;
            message.ExecutionType = (byte)model.ExecutionType;
            message.Multiplier.Mantissa = EncodeMantissa(model.Multiplier, message.Multiplier.Exponent, DOUBLENULL2.MantissaNullValue);

            return message.Limit - offset;
        }

        public int EncodeOrderTagMessage(DirectBuffer directBuffer, int offset, OrderTagModel model)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderTagMessage.BlockLength;
            messageHeader.SchemaId = OrderTagMessage.SchemaId;
            messageHeader.TemplateId = OrderTagMessage.TemplateId;
            messageHeader.Version = OrderTagMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            OrderTagMessage message = new OrderTagMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermId(TruncateForSbe(model.PermId, OrderTagMessage.PermIdLength));
            message.SetTrader(TruncateForSbe(model.Trader, OrderTagMessage.TraderLength));
            message.SetInstanceLegacy(TruncateForSbe(model.Instance, OrderTagMessage.InstanceLegacyLength));
            message.SetParentSpreadHash(TruncateForSbe(model.ParentSpreadHash, OrderTagMessage.ParentSpreadHashLength));

            message.Bid.Mantissa = EncodeMantissa(model.Bid, message.Bid.Exponent, DOUBLENULL2.MantissaNullValue);
            message.Ask.Mantissa = EncodeMantissa(model.Ask, message.Ask.Exponent, DOUBLENULL2.MantissaNullValue);
            message.BidSize = model.BidSize;
            message.AskSize = model.AskSize;

            message.Theo.Mantissa = EncodeMantissa(model.Theo, message.Theo.Exponent, DOUBLENULL2.MantissaNullValue);
            message.Ema.Mantissa = EncodeMantissa(model.Ema, message.Ema.Exponent, DOUBLENULL2.MantissaNullValue);
            message.Edge.Mantissa = EncodeMantissa(model.Edge, message.Edge.Exponent, DOUBLENULL2.MantissaNullValue);
            message.UnderBid.Mantissa = EncodeMantissa(model.UnderBid, message.UnderBid.Exponent, DOUBLENULL2.MantissaNullValue);
            message.UnderAsk.Mantissa = EncodeMantissa(model.UnderAsk, message.UnderAsk.Exponent, DOUBLENULL2.MantissaNullValue);
            message.UnderBidSize = model.UnderBidSize;
            message.UnderAskSize = model.UnderAskSize;

            message.TypeId = (ushort)model.ModuleType;
            message.SubTypeCode = (ushort)model.SubType;
            message.SharedId = model.SharedId;
            message.Sequence = model.Sequence;
            message.SubTypeSequence = model.SubTypeSequence;
            message.OrderSubType = (ushort)model.OrderSubType;

            message.ResubmitCount = model.ResubmitCount;
            message.TotalEstimatedResubmit = model.TotalEstimatedResubmit;

            if (model.ModuleType == ModuleType.EdgeScanFeed &&
                model is EdgeScanFeedOrderTagModel esfOrderTagModel)
            {
                message.EdgeScannerType = (ushort)esfOrderTagModel.EdgeScannerType;
                message.EdgeScanFeedConditionCode = (byte)esfOrderTagModel.EdgeScanFeedConditionCode;

                message.EdgeScanFeedEdge.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedEdge, message.EdgeScanFeedEdge.Exponent, DOUBLENULL2.MantissaNullValue);
                message.EdgeScanFeedTimespan.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedTimespan, message.EdgeScanFeedTimespan.Exponent, DOUBLENULL2.MantissaNullValue);
                message.EdgeScanFeedRespondLatency.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedRespondLatency, message.EdgeScanFeedRespondLatency.Exponent, DOUBLENULL2.MantissaNullValue);
                message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                message.EdgeScanFeedBuyPrice.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedBuyPrice, message.EdgeScanFeedBuyPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                message.EdgeScanFeedSellPrice.Mantissa = EncodeMantissa(esfOrderTagModel.EdgeScanFeedSellPrice, message.EdgeScanFeedSellPrice.Exponent, DOUBLENULL2.MantissaNullValue);

                message.EdgeScanFeedBuyQty = esfOrderTagModel.EdgeScanFeedBuyQty;
                message.EdgeScanFeedSellQty = esfOrderTagModel.EdgeScanFeedSellQty;

                message.EdgeScanFeedBuyTime = esfOrderTagModel.EdgeScanFeedBuyTime.ToUnixEpoch();
                message.EdgeScanFeedSellTime = esfOrderTagModel.EdgeScanFeedSellTime.ToUnixEpoch();
            }

            message.VolaTheo.Mantissa = EncodeMantissa(model.VolaTheo, message.VolaTheo.Exponent, DOUBLENULL2.MantissaNullValue);
            message.VolaTheoAdj.Mantissa = EncodeMantissa(model.VolaTheoAdj, message.VolaTheoAdj.Exponent, DOUBLENULL2.MantissaNullValue);
            message.VolaIv = model.VolaIv;
            message.TheoBid.Mantissa = EncodeMantissa(model.TheoBid, message.TheoBid.Exponent, DOUBLENULL2.MantissaNullValue);
            message.TheoAsk.Mantissa = EncodeMantissa(model.TheoAsk, message.TheoAsk.Exponent, DOUBLENULL2.MantissaNullValue);
            message.OrderSource = (byte)model.OrderSource;
            message.SessionId = model.SessionId;
            message.EdgeType = (byte)model.EdgeType;
            message.DigBid.Mantissa = EncodeMantissa(model.DigBid, message.DigBid.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DigAsk.Mantissa = EncodeMantissa(model.DigAsk, message.DigAsk.Exponent, DOUBLENULL2.MantissaNullValue);
            message.DigBidSize = model.DigBidSize;
            message.DigAskSize = model.DigAskSize;
            message.WeightedVega.Mantissa = EncodeMantissa(model.WeightedVega, message.WeightedVega.Exponent, DOUBLENULL2.MantissaNullValue);
            message.SetInstance(model.Instance ?? "");

            return message.Limit - offset;
        }

        public int EncodeModifySmartOrderRequestMessage(DirectBuffer directBuffer, int offset, ModifySmartRequest modifySmartRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ModifySmartOrderRequestMessage message = new ModifySmartOrderRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ModifySmartOrderRequestMessage.BlockLength;
            messageHeader.SchemaId = ModifySmartOrderRequestMessage.SchemaId;
            messageHeader.TemplateId = ModifySmartOrderRequestMessage.TemplateId;
            messageHeader.Version = ModifySmartOrderRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.Price = modifySmartRequest.Price;
            message.Quantity = modifySmartRequest.Quantity;
            message.SetLocalId(modifySmartRequest.LocalId ?? "");
            message.SetPermId(modifySmartRequest.PermId ?? "");
            message.SetOrderId(modifySmartRequest.OrderId ?? "");
            message.SetAccount(modifySmartRequest.Account ?? "");
            message.Venue = modifySmartRequest.Venue.HasValue ? (byte)modifySmartRequest.Venue : ModifySmartOrderRequestMessage.VenueNullValue;

            message.UserId = modifySmartRequest.UserId;
            message.RiskCheckId = modifySmartRequest.RiskCheckId;

            var strategyData = modifySmartRequest.ScrapeStrategyData;

            message.StrDataType = (byte)strategyData.Type;
            message.StrDataInstrumentType = (byte)strategyData.InstrumentType;
            message.StrDataTakeHidden = strategyData.TakeHidden != null
                ? strategyData.TakeHidden.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataAtsMode = strategyData.AtsMode != null
                ? strategyData.AtsMode.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;
            message.StrDataCancelOnHalt = strategyData.CancelOnHalt != null
                ? strategyData.CancelOnHalt.Value ? BooleanEnum.True : BooleanEnum.False
                : BooleanEnum.NULL_VALUE;

            message.StrDataMakeTake = strategyData.MakeTake != null
                ? (byte)strategyData.MakeTake.Value
                : ModifySmartOrderRequestMessage.StrDataMakeTakeNullValue;
            message.StrDataAlgorithm = strategyData.Algorithm != null
                ? (byte)strategyData.Algorithm.Value
                : ModifySmartOrderRequestMessage.StrDataAlgorithmNullValue;
            message.StrDataPriceMethod = strategyData.PriceMethod != null
                ? (byte)strategyData.PriceMethod.Value
                : ModifySmartOrderRequestMessage.StrDataPriceMethodNullValue;

            message.StrDataReminderQty = strategyData.ReminderQty != null
                ? strategyData.ReminderQty.Value
                : ModifySmartOrderRequestMessage.StrDataReminderQtyNullValue;
            message.StrDataMinWorkingQty = strategyData.MinWorkingQty != null
                ? strategyData.MinWorkingQty.Value
                : ModifySmartOrderRequestMessage.StrDataMinWorkingQtyNullValue;
            message.StrDataMinQuoteQty = strategyData.MinQuoteQty != null
                ? strategyData.MinQuoteQty.Value
                : ModifySmartOrderRequestMessage.StrDataMinQuoteQtyNullValue;

            message.StrDataWorkingQty = strategyData.WorkingQty != null
                ? strategyData.WorkingQty.Value
                : ModifySmartOrderRequestMessage.StrDataWorkingQtyNullValue;

            message.StrDataDiscretionTake.Mantissa = strategyData.DiscretionTake != null
                ? EncodeMantissa(strategyData.DiscretionTake.Value, message.StrDataDiscretionTake.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataUndPrice.Mantissa = strategyData.UndPrice != null
                ? EncodeMantissa(strategyData.UndPrice.Value, message.StrDataUndPrice.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMaxPriceUnd.Mantissa = strategyData.MaxPriceUnd != null
                ? EncodeMantissa(strategyData.MaxPriceUnd.Value, message.StrDataMaxPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataMinPriceUnd.Mantissa = strategyData.MinPriceUnd != null
                ? EncodeMantissa(strategyData.MinPriceUnd.Value, message.StrDataMinPriceUnd.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;
            message.StrDataPriceRange.Mantissa = strategyData.PriceRange != null
                ? EncodeMantissa(strategyData.PriceRange.Value, message.StrDataPriceRange.Exponent,
                    DOUBLENULL2.MantissaNullValue)
                : DOUBLENULL2.MantissaNullValue;

            message.StrDataLimitToMarketTime = strategyData.LimitToMarketTime != null
                ? strategyData.LimitToMarketTime.Value.ToUnixEpoch()
                : ModifySmartOrderRequestMessage.StrDataLimitToMarketTimeNullValue;

            return message.Limit - offset;
        }

        public int EncodeModeledTheoUpdateMessage(DirectBuffer directBuffer, int offset, ModeledTheoUpdate model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ModeledTheoUpdateMessage message = new ModeledTheoUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ModeledTheoUpdateMessage.BlockLength;
            messageHeader.SchemaId = ModeledTheoUpdateMessage.SchemaId;
            messageHeader.TemplateId = ModeledTheoUpdateMessage.TemplateId;
            messageHeader.Version = ModeledTheoUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.ModelId = model.ModelId;
            message.UnderlyingPrice = model.UnderlyingPrice;
            message.CalcTime = model.CalcTime;
            message.Count = (ushort)model.Theos.Count;

            ModeledTheoUpdateMessage.TheosGroup theosGroup = message.TheosCount(model.Theos.Count);
            foreach (var theoModel in model.Theos)
            {
                theosGroup.Next();
                theosGroup.SymbolId = theoModel.SymbolId;
                theosGroup.Theo = theoModel.Theo;
            }

            return message.Limit - offset;
        }

        public int EncodeSpreadBookQuoteMessage(DirectBuffer directBuffer, int offset, SpreadBookQuote model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadBookQuoteMessage message = new SpreadBookQuoteMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadBookQuoteMessage.BlockLength;
            messageHeader.SchemaId = SpreadBookQuoteMessage.SchemaId;
            messageHeader.TemplateId = SpreadBookQuoteMessage.TemplateId;
            messageHeader.Version = SpreadBookQuoteMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.FromCache = model.FromCache ? BooleanEnum.True : BooleanEnum.False;
            message.IsBidPrice1Valid = model.IsBidPrice1Valid ? BooleanEnum.True : BooleanEnum.False;
            message.IsAskPrice1Valid = model.IsAskPrice1Valid ? BooleanEnum.True : BooleanEnum.False;
            message.IsBidPrice2Valid = model.IsBidPrice2Valid ? BooleanEnum.True : BooleanEnum.False;
            message.IsAskPrice2Valid = model.IsAskPrice2Valid ? BooleanEnum.True : BooleanEnum.False;
            message.BidExch1 = (byte)model.BidExch1;
            message.AskExch1 = (byte)model.AskExch1;
            message.UpdateType = (byte)model.UpdateType;
            message.BidMask1 = model.BidMask1;
            message.AskMask1 = model.AskMask1;
            message.BidSize1 = model.BidSize1;
            message.AskSize1 = model.AskSize1;
            message.BidSize2 = model.BidSize2;
            message.AskSize2 = model.AskSize2;
            message.PrintVolume = model.PrintVolume;
            message.BidPrice1 = model.BidPrice1;
            message.AskPrice1 = model.AskPrice1;
            message.BidPrice2 = model.BidPrice2;
            message.AskPrice2 = model.AskPrice2;
            message.BidTime = model.BidTime.ToUnixEpoch();
            message.AskTime = model.AskTime.ToUnixEpoch();
            message.Timestamp = model.Timestamp.ToUnixEpoch();
            message.SrcTimestamp = model.SrcTimestamp;
            message.NetTimestamp = model.NetTimestamp;
            message.SpreadKey = model.SpreadKey;
            message.SetUnderlyingSymbol(model.Underlying ?? "");
            message.SetSpreadSymbol(model.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeSpreadExchOrderMessage(DirectBuffer directBuffer, int offset, SpreadExchOrder model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadExchOrderMessage message = new SpreadExchOrderMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadExchOrderMessage.BlockLength;
            messageHeader.SchemaId = SpreadExchOrderMessage.SchemaId;
            messageHeader.TemplateId = SpreadExchOrderMessage.TemplateId;
            messageHeader.Version = SpreadExchOrderMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.FromCache = model.FromCache ? BooleanEnum.True : BooleanEnum.False;
            message.AllOrNone = model.AllOrNone ? BooleanEnum.True : BooleanEnum.False;
            message.FlipSide = model.FlipSide ? BooleanEnum.True : BooleanEnum.False;
            message.IsPriceValid = model.IsPriceValid ? BooleanEnum.True : BooleanEnum.False;

            message.Exch = (byte)model.Exch;
            message.FirmType = (byte)model.FirmType;
            message.MarketQualifier = (byte)model.MarketQualifier;
            message.OrderStatus = (byte)model.OrderStatus;
            message.OrderType = (byte)model.OrderType;
            message.TimeInForce = (byte)model.TimeInForce;
            message.BaseStrategy = (byte)model.BaseStrategy;

            message.OrigOrderSize = model.OrigOrderSize;
            message.OrderSize = model.OrderSize;
            message.Price = model.Price;

            message.DgwTimestamp = model.DgwTimestamp;
            message.SrcTimestamp = model.SrcTimestamp;
            message.NetTimestamp = model.NetTimestamp;
            message.Timestamp = model.Timestamp.ToUnixEpoch();

            message.SetUnderlying(model.Underlying ?? "");
            message.SetSpreadSymbol(model.Symbol ?? "");
            message.SetClearingAccount(model.ClearingAccount ?? "");
            message.SetClearingFirm(model.ClearingFirm ?? "");
            message.SetOrderID(model.OrderID ?? "");
            message.SetSpreadKey(model.SpreadKey ?? "");
            message.SetSpreadId(model.SpreadId ?? "");
            message.SetSpreadDescription(model.SpreadDescription ?? "");

            return message.Limit - offset;
        }

        public int EncodeSpreadPrintMessage(DirectBuffer directBuffer, int offset, SpreadPrint model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadPrintMessage message = new SpreadPrintMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadPrintMessage.BlockLength;
            messageHeader.SchemaId = SpreadPrintMessage.SchemaId;
            messageHeader.TemplateId = SpreadPrintMessage.TemplateId;
            messageHeader.Version = SpreadPrintMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.FromCache = model.FromCache ? BooleanEnum.True : BooleanEnum.False;

            message.Side = model.Side == null ? SpreadPrintMessage.SideNullValue : (byte)model.Side;
            message.PrtExch = (byte)model.PrtExch;
            message.BaseStrategy = (byte)model.BaseStrategy;

            message.PrtSize = model.PrtSize;
            message.PrtPrice = model.PrtPrice;

            message.SrcTimestamp = model.SrcTimestamp;
            message.NetTimestamp = model.NetTimestamp;
            message.Timestamp = model.Timestamp.ToUnixEpoch();

            message.SetUnderlying(model.Underlying ?? "");
            message.SetSymbol(model.Symbol ?? "");
            message.SetSpreadId(model.SpreadId ?? "");
            message.SetSpreadDescription(model.SpreadDescription ?? "");

            return message.Limit - offset;
        }

        public int EncodeAuctionPrintMessage(DirectBuffer directBuffer, int offset, AuctionPrint model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AuctionPrintMessage message = new AuctionPrintMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AuctionPrintMessage.BlockLength;
            messageHeader.SchemaId = AuctionPrintMessage.SchemaId;
            messageHeader.TemplateId = AuctionPrintMessage.TemplateId;
            messageHeader.Version = AuctionPrintMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.FromCache = model.FromCache ? BooleanEnum.True : BooleanEnum.False;
            message.ContainsFlex = model.ContainsFlex.HasValue ? model.ContainsFlex.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;
            message.ContainsHedge = model.ContainsHedge.HasValue ? model.ContainsHedge.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;
            message.ContainsMultiHedge = model.ContainsMultiHedge.HasValue ? model.ContainsMultiHedge.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;
            message.HasCustPrc = model.HasCustPrc.HasValue ? model.HasCustPrc.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;
            message.IsTestAuction = model.IsTestAuction.HasValue ? model.IsTestAuction.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;

            message.BidMask = model.BidMask;
            message.AskMask = model.AskMask;
            message.PrtSize = model.PrtSize;
            message.PrtSize2 = model.PrtSize2;
            message.CustQty = model.CustQty;
            message.NumOptLegs = model.NumOptLegs;
            message.ExchBidSz = model.ExchBidSz;
            message.ExchAskSz = model.ExchAskSz;
            message.BidPrc = model.BidPrc;
            message.AskPrc = model.AskPrc;
            message.BidPrc10M = model.BidPrc10M;
            message.AskPrc10M = model.AskPrc10M;
            message.BidPrc1M = model.BidPrc1M;
            message.AskPrc1M = model.AskPrc1M;
            message.UAvgDailyVlm = model.UAvgDailyVlm;
            message.UPrc10M = model.UPrc10M;
            message.UPrc1M = model.UPrc1M;
            message.PrtSurfPrc = model.PrtSurfPrc;
            message.PrtSurfVol = model.PrtSurfVol;
            message.CommEnhancement = model.CommEnhancement;
            message.NetDe = model.NetDe;
            message.NetGa = model.NetGa;
            message.NetTh = model.NetTh;
            message.NetVe = model.NetVe;
            message.ExchAskPrc = model.ExchAskPrc;
            message.ExchBidPrc = model.ExchBidPrc;
            message.SurfVol1M = model.SurfVol1M;
            message.SurfVol10M = model.SurfVol10M;
            message.SurfPrc1M = model.SurfPrc1M;
            message.SurfPrc10M = model.SurfPrc10M;
            message.PkgAskPrc = model.PkgAskPrc;
            message.PkgBidPrc = model.PkgBidPrc;
            message.PkgSurfPrc = model.PkgSurfPrc;
            message.UBid = model.UBid;
            message.UAsk = model.UAsk;
            message.PrtUBid = model.PrtUBid;
            message.PrtUAsk = model.PrtUAsk;
            message.PrtUPrc = model.PrtUPrc;
            message.PrtPrice = model.PrtPrice;
            message.PrtPrice2 = model.PrtPrice2;
            message.CustPrc = model.CustPrc;
            message.PrtType = (ushort)model.PrtType;
            message.AuctionSource = (ushort)model.AuctionSource;
            message.AuctionType = (ushort)model.AuctionType;
            message.CustFirmType = (ushort)model.CustFirmType;
            message.SpreadClass = (ushort)model.SpreadClass;
            message.SpreadFlavor = (ushort)model.SpreadFlavor;
            message.CustSide = model.CustSide.HasValue ? (ushort)model.CustSide : AuctionPrintMessage.CustSideNullValue;

            message.PrtTime = model.PrtTime.ToUnixEpoch();
            message.Timestamp = model.Timestamp.ToUnixEpoch();
            message.NoticeTime = model.NoticeTime.ToUnixEpoch();
            message.TradeDate = model.TradeDate.ToUnixEpoch();

            message.Pkey = model.Pkey;
            message.SetUnderlying(model.Underlying ?? "");

            var legsGroup = message.LegsCount(model.Legs.Count);
            foreach (var leg in model.Legs)
            {
                legsGroup.Next();

                legsGroup.SetSymbol(leg.LegSymbol ?? "");
                legsGroup.LegSecType = (byte)leg.LegSecType;
                legsGroup.LegSide = leg.LegSide.HasValue ? (byte)leg.LegSide.Value : AuctionPrintMessage.LegsGroup.LegSideNullValue;
                legsGroup.LegExpType = (byte)leg.LegExpType;
                legsGroup.LegBidMask = leg.LegBidMask;
                legsGroup.LegAskMask = leg.LegAskMask;
                legsGroup.LegRatio = leg.LegRatio;
                legsGroup.LegBidSz = leg.LegBidSz;
                legsGroup.LegAskSz = leg.LegAskSz;
                legsGroup.LegUndPerCn = leg.LegUndPerCn;
                legsGroup.LegPointValue = leg.LegPointValue;
                legsGroup.LegYears = leg.LegYears;
                legsGroup.LegRate = leg.LegRate;
                legsGroup.LegAtmVol = leg.LegAtmVol;
                legsGroup.LegDdivPv = leg.LegDdivPv;
                legsGroup.LegTVol = leg.LegTVol;
                legsGroup.LegSVol = leg.LegSVol;
                legsGroup.LegSDiv = leg.LegSDiv;
                legsGroup.LegSPrc = leg.LegSPrc;
                legsGroup.LegDe = leg.LegDe;
                legsGroup.LegGa = leg.LegGa;
                legsGroup.LegTh = leg.LegTh;
                legsGroup.LegVe = leg.LegVe;
                legsGroup.LegBid = leg.LegBid;
                legsGroup.LegAsk = leg.LegAsk;
                legsGroup.LegSVolOk = leg.LegSVolOk.HasValue ? leg.LegSVolOk.Value ? BooleanEnum.True : BooleanEnum.False : BooleanEnum.NULL_VALUE;
            }

            message.SetSymbol(model.Symbol ?? "");
            message.SetSpreadId(model.SpreadId ?? "");
            message.SetSpreadDescription(model.SpreadDescription ?? "");
            message.SetCustAgentMPID(model.CustAgentMPID ?? "");
            message.SetIndustry(model.Industry ?? "");

            return message.Limit - offset;
        }

        public int EncodeCobTradeRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string underlying, DateTime startTime, DateTime endTime, int limit)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CobTradeRequestMessage message = new CobTradeRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CobTradeRequestMessage.BlockLength;
            messageHeader.SchemaId = CobTradeRequestMessage.SchemaId;
            messageHeader.TemplateId = CobTradeRequestMessage.TemplateId;
            messageHeader.Version = CobTradeRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.SetUnderlyingSymbol(underlying ?? "");
            message.StartTime = startTime.ToUnixEpoch();
            message.EndTime = endTime.ToUnixEpoch();
            message.LimitCount = limit;

            return message.Limit - offset;
        }

        public int EncodeCancelDataRequestMessage(DirectBuffer directBuffer, int offset, int requestId, SubscriptionFieldType fieldType)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CancelDataRequestMessage message = new CancelDataRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CancelDataRequestMessage.BlockLength;
            messageHeader.SchemaId = CancelDataRequestMessage.SchemaId;
            messageHeader.TemplateId = CancelDataRequestMessage.TemplateId;
            messageHeader.Version = CancelDataRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.FieldType = (short)fieldType;

            return message.Limit - offset;
        }

        public int EncodeCobTradeResponseMessage(DirectBuffer directBuffer, int offset, int requestId, List<SpreadExchPrint> prints)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SpreadExchPrintMessage message = new SpreadExchPrintMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SpreadExchPrintMessage.BlockLength;
            messageHeader.SchemaId = SpreadExchPrintMessage.SchemaId;
            messageHeader.TemplateId = SpreadExchPrintMessage.TemplateId;
            messageHeader.Version = SpreadExchPrintMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.Count = prints.Count;

            var printGroup = message.PrintsCount(prints.Count);
            foreach (var print in prints)
            {
                printGroup.Next();

                printGroup.IsPrintPriceValid = print.IsPrintPriceValid ? BooleanEnum.True : BooleanEnum.False;
                printGroup.HasFlexLeg = print.HasFlexLeg ? BooleanEnum.True : BooleanEnum.False;
                printGroup.HasHedgeLeg = print.HasHedgeLeg ? BooleanEnum.True : BooleanEnum.False;

                printGroup.Exch = (byte)print.Exch;
                printGroup.StrategyClass = (byte)print.StrategyClass;

                printGroup.Side = print.Side == null ? SpreadExchPrintMessage.PrintsGroup.SideNullValue : (byte)print.Side;
                printGroup.MinAnchorSide = print.MinAnchorSide == null ? SpreadExchPrintMessage.PrintsGroup.SideNullValue : (byte)print.MinAnchorSide;
                printGroup.MaxAnchorSide = print.MaxAnchorSide == null ? SpreadExchPrintMessage.PrintsGroup.SideNullValue : (byte)print.MaxAnchorSide;
                printGroup.StockLegSide = print.StockLegSide == null ? SpreadExchPrintMessage.PrintsGroup.SideNullValue : (byte)print.StockLegSide;
                printGroup.FutureLegSide = print.FutureLegSide == null ? SpreadExchPrintMessage.PrintsGroup.SideNullValue : (byte)print.FutureLegSide;

                printGroup.PrintSize = print.PrintSize;
                printGroup.PrintPrice.Mantissa = EncodeMantissa(print.PrintPrice, printGroup.PrintPrice.Exponent, DOUBLENULL2.MantissaNullValue);
                printGroup.PrintNumber = print.PrintNumber;
                printGroup.StcTimestamp = print.StcTimestamp;
                printGroup.NetTimestamp = print.NetTimestamp;

                printGroup.Timestamp = print.Timestamp.ToUnixEpoch();
                printGroup.SetMinAnchorLeg(print.MinAnchorLeg ?? "");
                printGroup.SetMaxAnchorLeg(print.MaxAnchorLeg ?? "");
                printGroup.SetUnderlyingSymbol(print.Underlying ?? "");
                printGroup.NumOptLegs = (byte)print.Legs.Count;

                var legsGroup = printGroup.LegsCount(print.Legs.Count);

                foreach (var leg in print.Legs)
                {
                    legsGroup.Next();
                    legsGroup.SetSecurity(leg.LegSecurity ?? "");
                    legsGroup.Side = leg.LegSide == null
                        ? SpreadExchPrintMessage.PrintsGroup.LegsGroup.SideNullValue
                        : (byte)leg.LegSide;
                    legsGroup.LegRatio = leg.LegRatio;
                    legsGroup.OpenClose = (byte)leg.LegPositionType;
                }
            }

            return message.Limit - offset;
        }

        public int EncodeModelDescriptionMessage(DirectBuffer directBuffer, int offset, byte modelId, string description)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ModelDescriptionMessage message = new ModelDescriptionMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ModelDescriptionMessage.BlockLength;
            messageHeader.SchemaId = ModelDescriptionMessage.SchemaId;
            messageHeader.TemplateId = ModelDescriptionMessage.TemplateId;
            messageHeader.Version = ModelDescriptionMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.ModelId = modelId;
            message.SetDescription(description ?? "");

            return message.Limit - offset;
        }

        public int EncodeSlimGreekUpdateMessage(DirectBuffer directBuffer, int offset, SlimGreekUpdateModel update)
        {
            if (update.TickerId < 0 || update.TickerId > 16777215)
            {
                throw new ArgumentOutOfRangeException(nameof(update.TickerId));
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SlimGreekUpdateMessage.BlockLength;
            messageHeader.SchemaId = SlimGreekUpdateMessage.SchemaId;
            messageHeader.TemplateId = SlimGreekUpdateMessage.TemplateId;
            messageHeader.Version = SlimGreekUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            SlimGreekUpdateMessage message = new SlimGreekUpdateMessage();

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetTickerId(0, (byte)(update.TickerId >> 16));
            message.SetTickerId(1, (byte)(update.TickerId >> 8));
            message.SetTickerId(2, (byte)update.TickerId);
            message.ModelId = update.ModelId;
            EncodePriceNull3(message.Theo, update.Theo);
            EncodePriceNull3(message.Delta, update.Delta);
            EncodePriceNull3(message.Gamma, update.Gamma);
            EncodePriceNull3(message.Vega, update.Vega);
            EncodePriceNull3(message.Vol, update.Vol);
            message.TimeStamp = update.TimeStamp;

            return message.Limit - offset;
        }

        public int EncodeCancelTokenMessage(DirectBuffer directBuffer, int offset, int requestId, string token)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CancelTokenMessage message = new CancelTokenMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CancelTokenMessage.BlockLength;
            messageHeader.SchemaId = CancelTokenMessage.SchemaId;
            messageHeader.TemplateId = CancelTokenMessage.TemplateId;
            messageHeader.Version = CancelTokenMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.SetToken(token ?? "");

            return message.Limit - offset;
        }

        public int EncodeImpliedQuoteUpdateMessage(DirectBuffer directBuffer, int offset, ImpliedQuoteUpdate update)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ImpliedQuoteUpdateMessage message = new ImpliedQuoteUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ImpliedQuoteUpdateMessage.BlockLength;
            messageHeader.SchemaId = ImpliedQuoteUpdateMessage.SchemaId;
            messageHeader.TemplateId = ImpliedQuoteUpdateMessage.TemplateId;
            messageHeader.Version = ImpliedQuoteUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetIndex(0, (byte)(update.Index >> 16));
            message.SetIndex(1, (byte)(update.Index >> 8));
            message.SetIndex(2, (byte)update.Index);
            message.SetUnderlyingSymbol(update.Underlying ?? "");
            EncodePriceNull3(message.Bid, update.Bid);
            EncodePriceNull3(message.Ask, update.Ask);
            EncodePriceNull3(message.Theo, update.Theo);
            EncodePriceNull3(message.UnderBid, update.UnderBid);
            EncodePriceNull3(message.UnderAsk, update.UnderAsk);
            EncodePriceNull3(message.ImpliedQuoteBid, update.ImpliedBid);
            EncodePriceNull3(message.ImpliedQuoteAsk, update.ImpliedAsk);
            EncodePriceNull3(message.ImpliedQuoteBidRecordPrice, update.ImpliedBidRecordPrice);
            EncodePriceNull3(message.ImpliedQuoteBidRecordTheo, update.ImpliedBidRecordTheo);
            EncodePriceNull3(message.ImpliedQuoteBidRecordMovement, update.ImpliedBidRecordMovement);
            EncodePriceNull3(message.ImpliedBidRecordNonDeltaMovement, update.ImpliedBidRecordNonDeltaMovement);
            message.ImpliedQuoteBidRecordTime = update.ImpliedBidRecordTime.ToUnixEpoch();
            EncodePriceNull3(message.ImpliedQuoteAskRecordPrice, update.ImpliedAskRecordPrice);
            EncodePriceNull3(message.ImpliedQuoteAskRecordTheo, update.ImpliedAskRecordTheo);
            EncodePriceNull3(message.ImpliedQuoteAskRecordMovement, update.ImpliedAskRecordMovement);
            EncodePriceNull3(message.ImpliedAskRecordNonDeltaMovement, update.ImpliedAskRecordNonDeltaMovement);
            message.ImpliedQuoteAskRecordTime = update.ImpliedAskRecordTime.ToUnixEpoch();

            return message.Limit - offset;
        }

        public int EncodeGetClosestOptionRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string underlying, SubscriptionFieldType field, Data.Enums.PutCall putCall, DateTime expiration, double value)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            GetClosestOptionRequestMessage message = new GetClosestOptionRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = GetClosestOptionRequestMessage.BlockLength;
            messageHeader.SchemaId = GetClosestOptionRequestMessage.SchemaId;
            messageHeader.TemplateId = GetClosestOptionRequestMessage.TemplateId;
            messageHeader.Version = GetClosestOptionRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.SetUnderlyingSymbol(underlying ?? "");
            message.PutCall = (Generated.PutCall)putCall;
            message.Expiration = expiration.ToUnixEpoch();
            message.Field = (short)field;
            message.Value = value;

            return message.Limit - offset;
        }

        public int EncodeGetClosestOptionResponseMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            GetClosestOptionResponseMessage message = new GetClosestOptionResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = GetClosestOptionResponseMessage.BlockLength;
            messageHeader.SchemaId = GetClosestOptionResponseMessage.SchemaId;
            messageHeader.TemplateId = GetClosestOptionResponseMessage.TemplateId;
            messageHeader.Version = GetClosestOptionResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestID = requestId;
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeNextOptionPermsRequestMessage(DirectBuffer directBuffer, int offset, int requestId, string symbol, PermutationDirection direction, PermMode mode, int count)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            NextOptionPermsRequestMessage message = new NextOptionPermsRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = NextOptionPermsRequestMessage.BlockLength;
            messageHeader.SchemaId = NextOptionPermsRequestMessage.SchemaId;
            messageHeader.TemplateId = NextOptionPermsRequestMessage.TemplateId;
            messageHeader.Version = NextOptionPermsRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.Direction = (sbyte)direction;
            message.Mode = (sbyte)mode;
            message.Count = (short)count;
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeNextOptionPermsResponseMessage(DirectBuffer directBuffer, int offset, int requestId, IReadOnlyList<string> symbols, bool lastGroup)
        {
            int count = symbols.Count;
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            NextOptionPermsResponseMessage message = new NextOptionPermsResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = NextOptionPermsResponseMessage.BlockLength;
            messageHeader.SchemaId = NextOptionPermsResponseMessage.SchemaId;
            messageHeader.TemplateId = NextOptionPermsResponseMessage.TemplateId;
            messageHeader.Version = NextOptionPermsResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            NextOptionPermsResponseMessage.SymbolsGroup symbolsGroup = message.SymbolsCount(count);
            for (int i = 0; i < count; i++)
            {
                symbolsGroup.Next();
                symbolsGroup.SetSymbol(symbols[i] ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeNextSpreadPermsRequestMessage(DirectBuffer directBuffer, int offset, int requestId, IReadOnlyList<PermLegRequest> legs, PermutationDirection direction, PermMode mode, Data.Enums.PermSide permSide, int count, Data.Enums.BaseStrategy baseStrategy, bool maintainBaseStrategy, bool maintainBaseStrategyFlyException, bool skipCheck)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            NextSpreadPermsRequestMessage message = new NextSpreadPermsRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = NextSpreadPermsRequestMessage.BlockLength;
            messageHeader.SchemaId = NextSpreadPermsRequestMessage.SchemaId;
            messageHeader.TemplateId = NextSpreadPermsRequestMessage.TemplateId;
            messageHeader.Version = NextSpreadPermsRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.Direction = (sbyte)direction;
            message.Mode = (sbyte)mode;
            message.PermSide = (sbyte)permSide;
            message.Count = (short)count;
            message.BaseStrategy = (byte)baseStrategy;
            message.MaintainBaseStrategy = maintainBaseStrategy ? BooleanEnum.True : BooleanEnum.False;
            message.MaintainBaseStrategyFlyException = maintainBaseStrategyFlyException ? BooleanEnum.True : BooleanEnum.False;
            message.SkipCheck = skipCheck ? BooleanEnum.True : BooleanEnum.False;

            NextSpreadPermsRequestMessage.LegsGroup legsGroup = message.LegsCount(legs.Count);
            for (int i = 0; i < legs.Count; i++)
            {
                PermLegRequest leg = legs[i];
                legsGroup.Next();
                legsGroup.Side = (Generated.Side)leg.Side;
                legsGroup.Ratio = leg.Ratio;
                legsGroup.SetSymbol(leg.Symbol ?? string.Empty);
            }

            return message.Limit - offset;
        }

        public int EncodeNextSpreadPermsResponseMessage(DirectBuffer directBuffer, int offset, int requestId, IReadOnlyList<PermSpreadResult> perms, bool lastGroup)
        {
            int count = perms.Count;
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            NextSpreadPermsResponseMessage message = new NextSpreadPermsResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = NextSpreadPermsResponseMessage.BlockLength;
            messageHeader.SchemaId = NextSpreadPermsResponseMessage.SchemaId;
            messageHeader.TemplateId = NextSpreadPermsResponseMessage.TemplateId;
            messageHeader.Version = NextSpreadPermsResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.LastGroup = lastGroup ? BooleanEnum.True : BooleanEnum.False;
            message.Count = count;

            NextSpreadPermsResponseMessage.PermsGroup permsGroup = message.PermsCount(count);
            for (int i = 0; i < count; i++)
            {
                permsGroup.Next();
                IReadOnlyList<PermLegResult> legs = perms[i].Legs;
                NextSpreadPermsResponseMessage.PermsGroup.LegsGroup legsGroup = permsGroup.LegsCount(legs.Count);
                for (int j = 0; j < legs.Count; j++)
                {
                    PermLegResult leg = legs[j];
                    legsGroup.Next();
                    legsGroup.Side = (Generated.Side)leg.Side;
                    legsGroup.Ratio = leg.Ratio;
                    legsGroup.SetSymbol(leg.Symbol ?? string.Empty);
                }
            }

            return message.Limit - offset;
        }

        public int EncodeJsonRequestMessage(DirectBuffer directBuffer, int offset, JsonRequest jsonRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            JsonRequestMessage message = new JsonRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = JsonRequestMessage.BlockLength;
            messageHeader.SchemaId = JsonRequestMessage.SchemaId;
            messageHeader.TemplateId = JsonRequestMessage.TemplateId;
            messageHeader.Version = JsonRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = jsonRequest.RequestId;
            message.RequestType = (short)jsonRequest.RequestType;
            message.SetContent(jsonRequest.Content ?? "");

            return message.Limit - offset;
        }

        public int EncodeJsonResponseMessage(DirectBuffer directBuffer, int offset, JsonResponse jsonResponse)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            JsonResponseMessage message = new JsonResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = JsonResponseMessage.BlockLength;
            messageHeader.SchemaId = JsonResponseMessage.SchemaId;
            messageHeader.TemplateId = JsonResponseMessage.TemplateId;
            messageHeader.Version = JsonResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = jsonResponse.RequestId;
            message.IsSuccess = jsonResponse.IsSuccess ? BooleanEnum.True : BooleanEnum.False;
            message.SetContent(jsonResponse.Content ?? "");

            return message.Limit - offset;
        }

        public int EncodeRiskCheckResultMessage(DirectBuffer directBuffer, int offset, IHaveRisk riskCheckResult)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RiskCheckResultMessage message = new RiskCheckResultMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RiskCheckResultMessage.BlockLength;
            messageHeader.SchemaId = RiskCheckResultMessage.SchemaId;
            messageHeader.TemplateId = RiskCheckResultMessage.TemplateId;
            messageHeader.Version = RiskCheckResultMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RiskCheckId = riskCheckResult.RiskCheckId;
            message.Passed = riskCheckResult.RiskCheckPassed ? BooleanEnum.True : BooleanEnum.False;
            message.SetMessage(riskCheckResult.RiskCheckMessage ?? "");

            return message.Limit - offset;
        }

        public int EncodeOrderRiskRequestMessage(DirectBuffer directBuffer, int offset, OrderRisk request)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderRiskRequestMessage message = new OrderRiskRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderRiskRequestMessage.BlockLength;
            messageHeader.SchemaId = OrderRiskRequestMessage.SchemaId;
            messageHeader.TemplateId = OrderRiskRequestMessage.TemplateId;
            messageHeader.Version = OrderRiskRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.UserId = request.UserId;
            message.RiskCheckId = request.RiskCheckId;
            message.IsOpening = request.IsOpening ? BooleanEnum.True : BooleanEnum.False;
            message.Venue = request.Venue != null ? (byte)request.Venue : OrderRiskRequestMessage.VenueNullValue;
            message.Side = request.Side != null ? (byte)request.Side : OrderRiskRequestMessage.SideNullValue;
            message.Qty = request.Qty;
            EncodeDoubleNull2(message.Price, request.Price);
            message.SetRoute(request.Route ?? "");
            message.BaseStrategy = (byte)request.BaseStrategy;
            message.StrikeSpacing = request.StrikeSpacing;
            message.SetOrderId(request.OrderId ?? "");
            message.SetUnderlyingSymbol(request.UnderlyingSymbol ?? "");
            message.BrokerId = request.Broker != null ? (byte)request.Broker : OrderRiskRequestMessage.BrokerIdNullValue;
            message.ExchangeId = request.Exchange != null ? (byte)request.Exchange : OrderRiskRequestMessage.ExchangeIdNullValue;
            message.SubType = request.SubType.HasValue ? (byte)request.SubType.Value : OrderRiskRequestMessage.SubTypeNullValue;
            message.SetDescription(request.Description ?? "");
            message.SetSymbol(request.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeCancelRiskRequestMessage(DirectBuffer directBuffer, int offset, CancelRisk request)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CancelRiskRequestMessage message = new CancelRiskRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CancelRiskRequestMessage.BlockLength;
            messageHeader.SchemaId = CancelRiskRequestMessage.SchemaId;
            messageHeader.TemplateId = CancelRiskRequestMessage.TemplateId;
            messageHeader.Version = CancelRiskRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.UserId = request.UserId;
            message.RiskCheckId = request.RiskCheckId;
            message.IsOpening = request.IsOpening ? BooleanEnum.True : BooleanEnum.False;
            message.Venue = request.Venue != null ? (byte)request.Venue : CancelRiskRequestMessage.VenueNullValue;
            message.Side = request.Side != null ? (byte)request.Side : CancelRiskRequestMessage.SideNullValue;
            message.Qty = request.Qty;
            EncodeDoubleNull2(message.Price, request.Price);
            message.SetRoute(request.Route ?? "");
            message.BaseStrategy = (byte)request.BaseStrategy;
            message.StrikeSpacing = request.StrikeSpacing;
            message.SetOrderId(request.OrderId ?? "");
            message.SubmitTime = request.SubmitTime.ToUnixEpoch();
            message.SetUnderlyingSymbol(request.UnderlyingSymbol ?? "");
            message.SetDescription(request.Description ?? "");
            message.SetSymbol(request.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeCancelReplaceRiskRequestMessage(DirectBuffer directBuffer, int offset, CancelReplaceRisk request)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            CancelReplaceRiskRequestMessage message = new CancelReplaceRiskRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = CancelReplaceRiskRequestMessage.BlockLength;
            messageHeader.SchemaId = CancelReplaceRiskRequestMessage.SchemaId;
            messageHeader.TemplateId = CancelReplaceRiskRequestMessage.TemplateId;
            messageHeader.Version = CancelReplaceRiskRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.UserId = request.UserId;
            message.RiskCheckId = request.RiskCheckId;
            message.IsOpening = request.IsOpening ? BooleanEnum.True : BooleanEnum.False;
            message.Venue = request.Venue != null ? (byte)request.Venue : CancelReplaceRiskRequestMessage.VenueNullValue;
            message.Side = request.Side != null ? (byte)request.Side : CancelReplaceRiskRequestMessage.SideNullValue;
            message.Qty = request.Qty;
            EncodeDoubleNull2(message.Price, request.Price);
            message.SetRoute(request.Route ?? "");
            message.NewQty = request.NewQty;
            EncodeDoubleNull2(message.NewPrice, request.NewPrice);
            message.SetNewRoute(request.NewRoute ?? "");
            message.BaseStrategy = (byte)request.BaseStrategy;
            message.StrikeSpacing = request.StrikeSpacing;
            message.SetOrderId(request.OrderId ?? "");
            message.SubmitTime = request.SubmitTime.ToUnixEpoch();
            message.SetUnderlyingSymbol(request.UnderlyingSymbol ?? "");
            message.SetDescription(request.Description ?? "");
            message.SetSymbol(request.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeOrderUpdateModelMessage(DirectBuffer directBuffer, int offset, OrderUpdateModel orderUpdateModel)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderUpdateModelMessage message = new OrderUpdateModelMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderUpdateModelMessage.BlockLength;
            messageHeader.SchemaId = OrderUpdateModelMessage.SchemaId;
            messageHeader.TemplateId = OrderUpdateModelMessage.TemplateId;
            messageHeader.Version = OrderUpdateModelMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.OrderStatus = (byte)orderUpdateModel.OrderStatus;
            message.ExecutionType = (byte)orderUpdateModel.ExecutionType;
            EncodeDoubleNull2(message.Price, orderUpdateModel.Price);
            EncodeDoubleNull2(message.AvgPrice, orderUpdateModel.AvgPrice);
            EncodeDoubleNull2(message.LastPrice, orderUpdateModel.LastPx);
            message.LastQty = orderUpdateModel.LastQty;
            message.CumQty = orderUpdateModel.CumQty;
            message.LeavesQty = orderUpdateModel.LeavesQty;
            message.Qty = orderUpdateModel.Qty;
            message.LastUpdateTime = orderUpdateModel.LastUpdateTime.ToUnixEpoch();
            message.IsCancelReject = orderUpdateModel.IsCancelReject ? BooleanEnum.True : BooleanEnum.False;
            message.Side = orderUpdateModel.Side != null ? (byte)orderUpdateModel.Side : OrderUpdateModelMessage.SideNullValue;
            message.ContraTrader = orderUpdateModel.ContraTrader != null ? (byte)orderUpdateModel.ContraTrader : OrderUpdateModelMessage.ContraTraderNullValue;
            message.SetClientOrderId(orderUpdateModel.ClientOrderId ?? "");
            message.SetPrevClientOrderId(orderUpdateModel.PrevClientOrderId ?? "");
            message.SetOrigOrderId(orderUpdateModel.OrigOrderId ?? "");
            message.SetOrderId(orderUpdateModel.OrderId ?? "");
            message.SetLastExchange(orderUpdateModel.LastExchange ?? "");
            message.SetMessage(orderUpdateModel.Message ?? "");
            message.SetRoute(orderUpdateModel.Route ?? "");

            return message.Limit - offset;
        }

        public int EncodeFitUpdateMessage(DirectBuffer directBuffer, int offset, UnderFitResult underFitResult)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            FitUpdateMessage message = new FitUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = FitUpdateMessage.BlockLength;
            messageHeader.SchemaId = FitUpdateMessage.SchemaId;
            messageHeader.TemplateId = FitUpdateMessage.TemplateId;
            messageHeader.Version = FitUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            lock (underFitResult.Lock)
            {
                message.Index = underFitResult.Index;
                message.Sequence = underFitResult.Sequence;
                EncodePriceNull3(message.UnderlyingSpot, underFitResult.UnderlyingSpot);
                EncodePriceNull3(message.UnderlyingMid, underFitResult.UnderlyingMid);
                EncodeDoubleNull4(message.PriceMetric, underFitResult.PriceMetric);
                message.SnapshotTime = underFitResult.SnapshotTime.ToUnixEpoch();

                var fitResultsGroup = message.FitResultsCount(underFitResult.FitResults.Count);
                foreach (var fitResult in underFitResult.FitResults)
                {
                    fitResultsGroup.Next();
                    fitResultsGroup.Index = fitResult.Index;
                    EncodePriceNull3(fitResultsGroup.Theo, fitResult.Theo);
                    EncodeDoubleNull4(fitResultsGroup.Delta, fitResult.Delta);
                    EncodeDoubleNull4(fitResultsGroup.Gamma, fitResult.Gamma);
                    EncodeDoubleNull4(fitResultsGroup.Vega, fitResult.Vega);
                    fitResultsGroup.Iv = fitResult.Iv;
                }
            }

            return message.Limit - offset;
        }

        public int EncodeAutomationStateChangeMessage(DirectBuffer directBuffer, int offset, string id, bool automationRunning)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AutomationStateChangeMessage message = new AutomationStateChangeMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AutomationStateChangeMessage.BlockLength;
            messageHeader.SchemaId = AutomationStateChangeMessage.SchemaId;
            messageHeader.TemplateId = AutomationStateChangeMessage.TemplateId;
            messageHeader.Version = AutomationStateChangeMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetLocalOrderId(id ?? "");
            message.AutomationRunning = automationRunning ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodePerformanceModeRequestMessage(DirectBuffer directBuffer, int offset, bool isPerformanceModeEnabled)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PerformanceModeRequestMessage message = new PerformanceModeRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PerformanceModeRequestMessage.BlockLength;
            messageHeader.SchemaId = PerformanceModeRequestMessage.SchemaId;
            messageHeader.TemplateId = PerformanceModeRequestMessage.TemplateId;
            messageHeader.Version = PerformanceModeRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.IsPerformanceModeEnabled = isPerformanceModeEnabled ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodeSubmissionSummaryUpdateMessage(DirectBuffer directBuffer, int offset, SubmissionsSummary uniqueSubmissionsSummary)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SubmissionSummaryUpdateMessage message = new SubmissionSummaryUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SubmissionSummaryUpdateMessage.BlockLength;
            messageHeader.SchemaId = SubmissionSummaryUpdateMessage.SchemaId;
            messageHeader.TemplateId = SubmissionSummaryUpdateMessage.TemplateId;
            messageHeader.Version = SubmissionSummaryUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.BrokerId = (byte)uniqueSubmissionsSummary.Broker;
            message.BrokerTotalSubmissions = uniqueSubmissionsSummary.BrokerTotalSubmissions;
            message.BrokerUniqueSubmissions = uniqueSubmissionsSummary.BrokerUniqueSubmissions;
            message.ExchangeId = (byte)uniqueSubmissionsSummary.Exchange;
            message.ExchangeTotalSubmissions = uniqueSubmissionsSummary.ExchangeTotalSubmissions;
            message.ExchangeUniqueSubmissions = uniqueSubmissionsSummary.ExchangeUniqueSubmissions;
            message.SetUnderlyingSymbol(uniqueSubmissionsSummary.Underlying ?? "");
            message.UnderlyingTotalSubmissions = uniqueSubmissionsSummary.UnderlyingTotalSubmissions;
            message.UnderlyingUniqueSubmissions = uniqueSubmissionsSummary.UnderlyingUniqueSubmissions;
            message.SetTrader(uniqueSubmissionsSummary.Trader ?? "");
            message.TraderTotalSubmissions = uniqueSubmissionsSummary.TraderTotalSubmissions;
            message.TraderUniqueSubmissions = uniqueSubmissionsSummary.TraderUniqueSubmissions;

            return message.Limit - offset;
        }

        public int EncodePricingRequestMessage(DirectBuffer directBuffer, int offset, PricingRequestModel request)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PricingRequestMessage message = new PricingRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PricingRequestMessage.BlockLength;
            messageHeader.SchemaId = PricingRequestMessage.SchemaId;
            messageHeader.TemplateId = PricingRequestMessage.TemplateId;
            messageHeader.Version = PricingRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = request.RequestId;
            var legMessage = message.LegsCount(request.Legs.Count);

            for (int i = 0; i < request.Legs.Count; i++)
            {
                var leg = request.Legs[i];
                legMessage.Next();

                legMessage.SetTickerId(0, (byte)(leg.TickerId >> 16));
                legMessage.SetTickerId(1, (byte)(leg.TickerId >> 8));
                legMessage.SetTickerId(2, (byte)leg.TickerId);
                legMessage.Side = (Generated.Side)leg.Side;
                legMessage.Ratio = leg.Ratio;
            }

            return message.Limit - offset;
        }

        public int EncodePricingResponseMessage(DirectBuffer directBuffer, int offset, PricingResponseModel response)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            PricingResponseMessage message = new PricingResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = PricingResponseMessage.BlockLength;
            messageHeader.SchemaId = PricingResponseMessage.SchemaId;
            messageHeader.TemplateId = PricingResponseMessage.TemplateId;
            messageHeader.Version = PricingResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = response.RequestId;
            EncodePriceNull3(message.Bid, response.Bid);
            EncodePriceNull3(message.Ask, response.Ask);
            EncodePriceNull3(message.HwTheo, response.HwTheo);
            EncodePriceNull3(message.HwAdjTheo, response.HwAdjTheo);
            EncodePriceNull3(message.HwDelta, response.HwDelta);
            EncodePriceNull3(message.VolaTheo, response.VolaTheo);
            EncodePriceNull3(message.VolaAdjTheo, response.VolaAdjTheo);
            EncodePriceNull3(message.AdjVolaEma, response.AdjVolaEma);
            EncodePriceNull3(message.AdjDaEma, response.AdjDaEma);
            EncodePriceNull3(message.UnderBid, response.UnderBid);
            EncodePriceNull3(message.UnderAsk, response.UnderAsk);

            return message.Limit - offset;
        }

        public int EncodeTradeRequestMessage(DirectBuffer directBuffer, int offset, uint requestId, string symbol, DateTime start, DateTime end)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TradesRequestMessage message = new TradesRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TradesRequestMessage.BlockLength;
            messageHeader.SchemaId = TradesRequestMessage.SchemaId;
            messageHeader.TemplateId = TradesRequestMessage.TemplateId;
            messageHeader.Version = TradesRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.SetSymbol(symbol ?? string.Empty);
            message.StartTime = start.ConvertToUnixEpoch();
            message.EndTime = end.ConvertToUnixEpoch();

            return message.Limit - offset;
        }

        public int EncodeTradeResponseMessage(DirectBuffer directBuffer, int offset, uint requestId, bool lastMessage, uint count, List<MbpTradeModel> trades)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TradesResponseMessage message = new TradesResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TradesResponseMessage.BlockLength;
            messageHeader.SchemaId = TradesResponseMessage.SchemaId;
            messageHeader.TemplateId = TradesResponseMessage.TemplateId;
            messageHeader.Version = TradesResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestId = requestId;
            message.LastGroup = lastMessage ? BooleanEnum.True : BooleanEnum.False;
            message.BatchCount = (uint)trades.Count;
            message.TotalCount = count;

            var tradesGroup = message.TradesCount(trades.Count);
            foreach (var trade in trades)
            {
                tradesGroup.Next();
                tradesGroup.Publisher = (ushort)trade.Publisher;
                tradesGroup.InstrumentId = trade.InstrumentId;
                tradesGroup.TsEvent = trade.TsEvent;
                tradesGroup.TsRecv = trade.TsRecv;
                EncodePriceNull3(tradesGroup.Price, trade.Price);
                tradesGroup.Size = trade.Size;
                tradesGroup.Action = (byte)trade.Action;
                tradesGroup.Side = (byte)trade.Side;
                tradesGroup.Flags = (byte)trade.Flags;
                tradesGroup.Depth = trade.Depth;
                tradesGroup.Sequence = trade.Sequence;
            }

            return message.Limit - offset;
        }

        public int EncodeAddRemoveMultipleTradesMessage(DirectBuffer directBuffer, int offset, bool add, List<string> permIds)
        {
            int count = permIds.Count;
            int size = MessageHeader.Size + AddRemoveMultipleTradesRequestMessage.BlockLength + AddRemoveMultipleTradesRequestMessage.PermIdsGroup.SbeHeaderSize + permIds.Sum(x => GetBufferEstimate(x));
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AddRemoveMultipleTradesRequestMessage message = new AddRemoveMultipleTradesRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AddRemoveMultipleTradesRequestMessage.BlockLength;
            messageHeader.SchemaId = AddRemoveMultipleTradesRequestMessage.SchemaId;
            messageHeader.TemplateId = AddRemoveMultipleTradesRequestMessage.TemplateId;
            messageHeader.Version = AddRemoveMultipleTradesRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            AddRemoveMultipleTradesRequestMessage.PermIdsGroup permIdsGroup = message.PermIdsCount(count);
            foreach (string permId in permIds)
            {
                permIdsGroup.Next();
                permIdsGroup.SetPermId(permId ?? string.Empty);
            }

            message.Add = add ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodeOrderRemoved(DirectBuffer directBuffer, int offset, string permId)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OrderRemoved message = new OrderRemoved();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OrderRemoved.BlockLength;
            messageHeader.SchemaId = OrderRemoved.SchemaId;
            messageHeader.TemplateId = OrderRemoved.TemplateId;
            messageHeader.Version = OrderRemoved.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermId(permId ?? "");

            return message.Limit - offset;
        }

        public int EncodeMultipleContrapartyReportsAddedMessage(DirectBuffer directBuffer, int offset, DateTime targetDate, ContraPartyReportModel[] reports, int count)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MultipleContrapartyReportsAdded message = new MultipleContrapartyReportsAdded();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MultipleContrapartyReportsAdded.BlockLength;
            messageHeader.SchemaId = MultipleContrapartyReportsAdded.SchemaId;
            messageHeader.TemplateId = MultipleContrapartyReportsAdded.TemplateId;
            messageHeader.Version = MultipleContrapartyReportsAdded.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            message.WrapForEncode(directBuffer, bufferOffset);
            message.TargetDate = ToUnixEpoch(targetDate);

            MultipleContrapartyReportsAdded.NoReportsGroup reportsGroup = message.NoReportsCount(count);
            for (int i = 0; i < count; i++)
            {
                var report = reports[i];
                reportsGroup.Next();

                reportsGroup.SetAccount(report.Account);
                reportsGroup.SetClOrdID(report.ClOrdID);
                reportsGroup.SetOCCID(report.OCCID);
                reportsGroup.SetSide(report.Side);
                reportsGroup.SetSymbol(report.Symbol);
                reportsGroup.SetRQDClOrdID(report.RQDClOrdID);
                reportsGroup.SetContraOpenClose(report.ContraOpenClose);
                reportsGroup.SetContraAccountType(report.ContraAccountType);
                reportsGroup.SetMarketMakerSubAccountCode(report.MarketMakerSubAccountCode);
                reportsGroup.SetTheirExtraText(report.TheirExtraText);
                reportsGroup.SetTheirClientOrderID(report.TheirClientOrderID);
                reportsGroup.SetTheirBrokerID(report.TheirBrokerID);
                reportsGroup.SetExchange(report.Exchange);
                reportsGroup.SetLiquidityIndicator(report.LiquidityIndicator);

                reportsGroup.ExecutionTime = report.ExecutionTime.ToUnixEpoch();
                reportsGroup.TradeDate = report.TradeDate.HasValue
                    ? ToUnixEpoch(report.TradeDate.Value.ToDateTime(TimeOnly.MinValue))
                    : MultipleContrapartyReportsAdded.NoReportsGroup.TradeDateNullValue;

                reportsGroup.Quantity = report.Quantity;
                EncodePriceNull3(reportsGroup.Price, report.Price);
                reportsGroup.ContraClearingFirm = report.ContraClearingFirm ?? 0;
            }

            return message.Limit - offset;
        }

        public int EncodeAutoTraderConfigBinaryMessage(DirectBuffer directBuffer, int offset, AutoTraderConfig config)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AutoTraderConfigBinaryMessage message = new AutoTraderConfigBinaryMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AutoTraderConfigBinaryMessage.BlockLength;
            messageHeader.SchemaId = AutoTraderConfigBinaryMessage.SchemaId;
            messageHeader.TemplateId = AutoTraderConfigBinaryMessage.TemplateId;
            messageHeader.Version = AutoTraderConfigBinaryMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetConfigId(config.ConfigId ?? "");
            message.UserId = config.UserId;
            message.RiskCheckId = config.RiskCheckId;
            message.RiskCheckPassed = config.RiskCheckPassed ? BooleanEnum.True : BooleanEnum.False;
            message.Sequence = config.Sequence;
            message.Venue = (byte)config.Venue;
            message.EdgeType = (byte)config.EdgeType;
            message.EdgeValue = config.EdgeValue;

            message.TheoModel = (int)config.TheoModel;
            message.FishLossTheoModel = (int)config.FishLossTheoModel;
            message.AutoCancelTheoModel = (int)config.AutoCancelTheoModel;
            message.ForMarketCrossPriceUseSweepEnabled = config.ForMarketCrossPriceUseSweepEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxSizeEnabled = config.CancelWithMaxSizeEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithOrderPriceEdgeToTheoEnabled = config.CancelWithOrderPriceEdgeToTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithOrderPriceEdgeToModelTheoEnabled = config.CancelWithOrderPriceEdgeToModelTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithTimerEnabled = config.CancelWithTimerEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToTheoEnabled = config.CancelWithEdgeToTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToAdjTheoEnabled = config.CancelWithEdgeToAdjTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInUnderlyingPxEnabled = config.CancelWithChangeInUnderlyingPxEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInUnderlyingDeltaPxEnabled = config.CancelWithChangeInUnderlyingDeltaPxEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToMidEnabled = config.CancelWithEdgeToMidEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInWidthEnabled = config.CancelWithChangeInWidthEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxWidthEnabled = config.CancelWithMaxWidthEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxSizeLimit = config.CancelWithMaxSizeLimit;
            message.CancelWithOrderPriceEdgeToTheo = config.CancelWithOrderPriceEdgeToTheo;
            message.CancelWithOrderPriceEdgeToModelTheo = config.CancelWithOrderPriceEdgeToModelTheo;
            message.CancelWithTimer = config.CancelWithTimer;
            message.CancelWithTheoEdge = config.CancelWithTheoEdge;
            message.CancelWithAdjTheoEdge = config.CancelWithAdjTheoEdge;
            message.CancelWithUnderlyingPxThreshold = config.CancelWithUnderlyingPxThreshold;
            message.CancelWithUnderlyingDeltaPx = config.CancelWithUnderlyingDeltaPx;
            message.CancelWithMidEdge = config.CancelWithMidEdge;
            message.CancelWithWidthThreshold = config.CancelWithWidthThreshold;
            message.CancelWithMaxWidthThreshold = config.CancelWithMaxWidthThreshold;
            message.MinEdgeToTheoCheckEnabled = config.MinEdgeToTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToHwTheoCheckEnabled = config.MinEdgeToHwTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToV0TheoCheckEnabled = config.MinEdgeToV0TheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToMidCheckEnabled = config.MinEdgeToMidCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToEmaCheckEnabled = config.MinEdgeToEmaCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToMarketCheckEnabled = config.MinEdgeToMarketCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidPercentCheckEnabled = config.MinBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MaxBidPercentCheckEnabled = config.MaxBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidAskSizeCheckEnabled = config.MinBidAskSizeCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEmaWidthPercentEdgeToTheoCheckEnabled = config.MinEmaWidthPercentEdgeToTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidCheckEnabled = config.MinBidCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinTheoCheckEnabled = config.MinTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToTheo = config.MinEdgeToTheo;
            message.MinEdgeToHwTheo = config.MinEdgeToHwTheo;
            message.MinEdgeToV0Theo = config.MinEdgeToV0Theo;
            message.MinEdgeToMid = config.MinEdgeToMid;
            message.MinEdgeToEma = config.MinEdgeToEma;
            message.MinEdgeToMarket = config.MinEdgeToMarket;
            message.MinBidPercent = config.MinBidPercent;
            message.MaxBidPercent = config.MaxBidPercent;
            message.MinBidAskSize = config.MinBidAskSize;
            message.MinEmaWidthPercentEdgeToTheoCheckEdge = config.MinEmaWidthPercentEdgeToTheoCheckEdge;
            message.MinBidCheckBidValue = config.MinBidCheckBidValue;
            message.MinTheoCheckTheoValue = config.MinTheoCheckTheoValue;
            message.EdgeToAdjTheoWithOverrideUsePercentage = config.EdgeToAdjTheoWithOverrideUsePercentage ? BooleanEnum.True : BooleanEnum.False;
            message.EdgeToAdjTheoWithOverrideStatic = config.EdgeToAdjTheoWithOverrideStatic;
            message.EdgeToAdjTheoWithOverridePercent = config.EdgeToAdjTheoWithOverridePercent;
            message.CheckForRecentAttempt = config.CheckForRecentAttempt ? BooleanEnum.True : BooleanEnum.False;
            message.CheckForRecentAttemptTimespan = config.CheckForRecentAttemptTimespan;
            message.CheckForRecentFill = config.CheckForRecentFill ? BooleanEnum.True : BooleanEnum.False;
            message.CheckForRecentFillTimespan = config.CheckForRecentFillTimespan;
            message.MinSpxAuction = config.MinSpxAuction;
            message.MinSpxSpreadAuction = config.MinSpxSpreadAuction;
            message.MinSingleLegAuction = config.MinSingleLegAuction;
            message.MinSpreadAuction = config.MinSpreadAuction;

            message.SetSweepRoute(config.SweepRoute ?? "");

            message.BestOfAdjTheoEnabled = config.BestOfAdjTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfAdjTheoEdge = config.BestOfAdjTheoEdge;
            message.BestOfAdjTheoModel = config.BestOfAdjTheoModel;
            message.BestOfHwTheoEnabled = config.BestOfHwTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfHwTheoEdge = config.BestOfHwTheoEdge;
            message.BestOfV0TheoEnabled = config.BestOfV0TheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfV0TheoEdge = config.BestOfV0TheoEdge;
            message.BestOfMidEnabled = config.BestOfMidEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfMidEdge = config.BestOfMidEdge;
            message.BestOfEmaEnabled = config.BestOfEmaEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfEmaEdge = config.BestOfEmaEdge;
            message.BestOfBidPercentEnabled = config.BestOfBidPercentEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfBidPercentEdge = config.BestOfBidPercentEdge;
            message.BestOfDigBidPercentEnabled = config.BestOfDigBidPercentEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfDigBidPercentEdge = config.BestOfDigBidPercentEdge;
            message.MaxDigBidPercentCheckEnabled = config.MaxDigBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MaxDigBidPercent = config.MaxDigBidPercent;

            message.AutoPermEnabled = config.AutoPermEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.AutoPermMinEdge = config.AutoPermMinEdge;
            message.AutoPermOrderCount = config.AutoPermOrderCount;
            message.AutoPermMaxGeneration = config.AutoPermMaxGeneration;
            message.AutoPermSubmissionStyle = (byte)config.AutoPermSubmissionStyle;
            message.AutoPermOrderInitialSize = config.AutoPermOrderInitialSize;

            var automationConfigsGroup = message.AutomationConfigsCount((config.UnderlyingToAutomationConfigs?.Count ?? 0) + 1);
            automationConfigsGroup.Next();
            EncodeAutomationConfig(automationConfigsGroup, config.DefaultAutomationConfig, true);
            if (config.UnderlyingToAutomationConfigs != null)
            {
                foreach (var automationConfig in config.UnderlyingToAutomationConfigs)
                {
                    automationConfigsGroup.Next();
                    EncodeAutomationConfig(automationConfigsGroup, automationConfig);
                }
            }

            var openRouteSmartmap = message.OpenRouteSmartMapCount(config.OpenRouteSmartMap?.Count ?? 0);
            if (config.OpenRouteSmartMap != null)
            {
                foreach (var routeMap in config.OpenRouteSmartMap)
                {
                    openRouteSmartmap.Next();
                    openRouteSmartmap.SetRoute(routeMap.Item1);
                    openRouteSmartmap.Delay = routeMap.Item2;
                }
            }

            var closeRouteSmartmap = message.CloseRouteSmartMapCount(config.CloseRouteSmartMap?.Count ?? 0);
            if (config.CloseRouteSmartMap != null)
            {
                foreach (var routeMap in config.CloseRouteSmartMap)
                {
                    closeRouteSmartmap.Next();
                    closeRouteSmartmap.SetRoute(routeMap.Item1);
                    closeRouteSmartmap.Delay = routeMap.Item2;
                }
            }

            var openRouteSingleLegSmartmap = message.OpenRouteSingleLegSmartMapCount(config.OpenRouteSingleLegSmartMap?.Count ?? 0);
            if (config.OpenRouteSingleLegSmartMap != null)
            {
                foreach (var routeMap in config.OpenRouteSingleLegSmartMap)
                {
                    openRouteSingleLegSmartmap.Next();
                    openRouteSingleLegSmartmap.SetRoute(routeMap.Item1);
                    openRouteSingleLegSmartmap.Delay = routeMap.Item2;
                }
            }

            var closeRouteSingleLegSmartmap = message.CloseRouteSingleLegSmartMapCount(config.CloseRouteSingleLegSmartMap?.Count ?? 0);
            if (config.CloseRouteSingleLegSmartMap != null)
            {
                foreach (var routeMap in config.CloseRouteSingleLegSmartMap)
                {
                    closeRouteSingleLegSmartmap.Next();
                    closeRouteSingleLegSmartmap.SetRoute(routeMap.Item1);
                    closeRouteSingleLegSmartmap.Delay = routeMap.Item2;
                }
            }

            message.SetRiskCheckMessage(config.RiskCheckMessage ?? "");
            message.SetConfigName(config.ConfigName ?? "");

            return message.Limit - offset;
        }

        private static void EncodeAutomationConfig(AutoTraderConfigBinaryMessage.AutomationConfigsGroup message, AutomationConfig config, bool isDefault = false)
        {
            message.IsDefault = isDefault ? BooleanEnum.True : BooleanEnum.False;
            message.SetUnderlyingSymbol(config.ConfigKey?.Underlying ?? "");
            message.Increment = config.ConfigKey?.Increment ?? 0;
            message.LoopingEnabled =
                config.LoopingEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CloseEdgeType = (byte)config.CloseEdgeType;
            message.StaticCloseEdge = config.StaticCloseEdge;
            message.StaticMinLoopEdge = config.StaticMinLoopEdge;
            message.StaticMaxLoss = config.StaticMaxLoss;
            message.LooperDynamicRouting = config.LooperDynamicRouting
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AttemptIncrementUsingDynamicRoute =
                config.AttemptIncrementUsingDynamicRoute ? BooleanEnum.True : BooleanEnum.False;
            message.EnableDynamicRouteForOpeningOrders =
                config.EnableDynamicRouteForOpeningOrders ? BooleanEnum.True : BooleanEnum.False;
            message.EnableDynamicRouteForClosingOrders =
                config.EnableDynamicRouteForClosingOrders ? BooleanEnum.True : BooleanEnum.False;
            message.CloseIntervalType = (byte)config.CloseIntervalType;
            message.StaticCloseInterval = config.StaticCloseInterval;
            message.StaticCloseIntervalMax = config.StaticCloseIntervalMax;
            message.StaticLoopInterval = config.StaticLoopInterval;
            message.StaticLoopIntervalMax = config.StaticLoopIntervalMax;
            message.IncrementType = (byte)config.IncrementType;
            message.StaticIncrement = config.StaticIncrement;
            message.SizeUpType = (byte)config.SizeUpType;
            message.StaticSizeUpLoopCountBeforeSizeup =
                config.StaticSizeUpLoopCountBeforeSizeup;
            message.StaticSizeUp = config.StaticSizeUp;
            message.AutoAggressorEnabled = config.AutoAggressorEnabled
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AutoAggressorMode = (byte)config.AutoAggressorMode;
            message.AutoAggressorEdgeTightenMode =
                (byte)config.AutoAggressorEdgeTightenMode;
            message.AutoAggressorEdgeTightenPercentage =
                config.AutoAggressorEdgeTightenPercentage;
            message.ScratchOnLowDeltaSize = config.ScratchOnLowDeltaSize
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.ScratchOnLowDeltaMax = config.ScratchOnLowDeltaMax;
            message.ScratchOnLowDeltaMaxLoss = config.ScratchOnLowDeltaMaxLoss;
            message.ScratchOnLowDeltaMinSize = config.ScratchOnLowDeltaMinSize;
            message.FreeLookRequireMinFillTime = config.FreeLookRequireMinFillTime
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookMinFillTime = config.FreeLookMinFillTime;
            message.FreeLookOnLosers =
                config.FreeLookOnLosers ? BooleanEnum.True : BooleanEnum.False;
            message.FreeLookOnLosersMax = config.FreeLookOnLosersMax;
            message.FreeLookOnAll =
                config.FreeLookOnAll ? BooleanEnum.True : BooleanEnum.False;
            message.FreeWhenGettingCloseEdge = config.FreeWhenGettingCloseEdge
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookAfterLastAttempt = config.FreeLookAfterLastAttempt
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookBackUpIncrement = config.FreeLookBackUpIncrement;
            message.FreeLookOnAllWalkBackIncrement =
                config.FreeLookOnAllWalkBackIncrement;
            message.LoopFreeLookOnAllUsingTicks =
                config.LoopFreeLookOnAllUsingTicks ? BooleanEnum.True : BooleanEnum.False;
            message.FreeLookOnAllIncrementTicks =
                config.FreeLookOnAllIncrementTicks;
            message.FreeLookOnAllWalkBackIncrementTicks =
                config.FreeLookOnAllWalkBackIncrementTicks;
            message.LoopFreeLookOnNickelNames = config.LoopFreeLookOnNickelNames
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.LoopFreeLookOnNickelNamesIncrement =
                config.LoopFreeLookOnNickelNamesIncrement;
            message.LoopFreeLookOnDimeNames = config.LoopFreeLookOnDimeNames
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.LoopFreeLookOnDimeNamesIncrement =
                config.LoopFreeLookOnDimeNamesIncrement;
            message.MaintainLastEdge =
                config.MaintainLastEdge ? BooleanEnum.True : BooleanEnum.False;
            message.AttemptResubmitCount = config.AttemptResubmitCount;
            message.LastFillResubmitCount = config.LastFillResubmitCount;
            message.MaxNumberOfLoops = config.MaxNumberOfLoops;
            message.PartialFillPercentage = config.PartialFillPercentage;
            message.PartialFillResubmit = config.PartialFillResubmit;
            message.LoopPricingMode = (byte)config.LoopPricingMode;
            message.AdjustClosingPriceToMarketWinnersOnly =
                config.AdjustClosingPriceToMarketWinnersOnly
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.PxCrossOption = (byte)config.PxCrossOption;
            message.ClosePxCrossOption = (byte)config.ClosePxCrossOption;
            message.AutoHedgeOnClose =
                config.AutoHedgeOnClose ? BooleanEnum.True : BooleanEnum.False;
            message.AutoHedgeOnCloseSizeOnly = config.AutoHedgeOnCloseSizeOnly
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.MinHedgeHouseEdge = config.MinHedgeHouseEdge;
            message.AutoHedgeOnFailure = config.AutoHedgeOnFailure
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AutoHedgePartial =
                config.AutoHedgePartial ? BooleanEnum.True : BooleanEnum.False;
            message.AutoLegEnabled =
                config.AutoLegEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.AutoLegMaxWidth = config.AutoLegMaxWidth;
            message.AutoLegCloseEdge = config.AutoLegCloseEdge;
            message.AutoLegMaxLoss = config.AutoLegMaxLoss;
            message.AutoLegCloseIncrement = config.AutoLegCloseIncrement;
            message.AutoLegRestTime = config.AutoLegRestTime;
            message.SetOpenRoute(config.OpenRoute ?? "");
            message.SetCloseRoute(config.CloseRoute ?? "");
            message.SetOpenRouteSingleLeg(config.OpenRouteSingleLeg ?? "");
            message.SetCloseRouteSingleLeg(config.CloseRouteSingleLeg ?? "");
            message.SetOpenRouteSize(config.OpenRouteSize ?? "");
            message.SetCloseRouteSize(config.CloseRouteSize ?? "");
            message.SetOpenRouteSingleLegSize(config.OpenRouteSingleLegSize ?? "");
            message.SetCloseRouteSingleLegSize(
                config.CloseRouteSingleLegSize ?? "");
            message.SetLoopFreeLookOnNickelNamesRoute(
                config.LoopFreeLookOnNickelNamesRoute ?? "");
            message.SetLoopFreeLookOnDimeNamesRoute(
                config.LoopFreeLookOnDimeNamesRoute ?? "");
            message.SetAutoLegCloseRoute(config.AutoLegCloseRoute ?? "");

            message.DynamicIntervalDefaultInterval =
                config.DynamicCloseInterval?.DefaultInterval ?? 0;
            message.DynamicIntervalDefaultResubmitCount =
                config.DynamicCloseInterval?.DefaultResubmit ?? 0;

            message.DynamicCloseEdgeEnabled =
                config.DynamicCloseEdge != null ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicSizeUpEnabled =
                config.DynamicSizeUp != null ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicCloseIntervalEnabled =
                config.DynamicCloseInterval != null ? BooleanEnum.True : BooleanEnum.False;

            message.DynamicEdgePercentBidRangeEnabled =
                (config.DynamicCloseEdge?.PercentBidRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeBaseEdgeEnabled =
                (config.DynamicCloseEdge?.BaseEdgeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeEmaRangeEnabled =
                (config.DynamicCloseEdge?.EmaRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeTradePxRangeEnabled =
                (config.DynamicCloseEdge?.TradePxRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeMinMarketWidthEnabled =
                (config.DynamicCloseEdge?.MinMarketWidthEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeMinMarketCrossEnabled =
                (config.DynamicCloseEdge?.MinMarketCrossEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeTheoRangeEnabled =
                (config.DynamicCloseEdge?.TheoRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeVolaRangeEnabled =
                (config.DynamicCloseEdge?.VolaRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeVolaModel = (int)(config.DynamicCloseEdge?.VolaModel ?? TheoModel.Hanw);
            message.DynamicEdgeDynamicVolaRangeEnabled =
                (config.DynamicCloseEdge?.DynamicVolaRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeDynamicVolaModel =
                (int)(config.DynamicCloseEdge?.DynamicVolaModel ?? TheoModel.Hanw);
            message.DynamicEdgeDynamicLookupMode = (config.DynamicCloseEdge?.DynamicLookupMode ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeUnderDivisor = config.DynamicCloseEdge?.UnderDivisor ?? 0;

            var automationConfigDteConfigGroup = message.DteConfigsCount(
                CountNonNull(config.DynamicCloseEdge?.DteTable)
                + CountNonNull(config.DynamicCloseEdge?.DynamicDteTable));
            EncodeDteConfigs(automationConfigDteConfigGroup, config.DynamicCloseEdge?.DteTable);
            EncodeDteConfigs(automationConfigDteConfigGroup, config.DynamicCloseEdge?.DynamicDteTable, true);

            var deltaMessage = message.DeltaConfigsCount(config.DynamicCloseEdge?.DeltaTable?.Count ?? 0);
            if (config.DynamicCloseEdge?.DeltaTable != null)
            {
                foreach (var deltaConfig in config.DynamicCloseEdge.DeltaTable)
                {
                    deltaMessage.Next();
                    deltaMessage.Active = deltaConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                    deltaMessage.Delta = deltaConfig.Delta;
                    deltaMessage.AdditionalEdgePerContract = deltaConfig.AdditionalEdgePerContract;
                    deltaMessage.AddedEdge = deltaConfig.AddedEdge;
                }
            }

            var dynamicSizeUpMessage = message.DynamicSizeUpConfigsCount(config.DynamicSizeUp?.SizeUpConfigs?.Count ?? 0);
            if (config.DynamicSizeUp?.SizeUpConfigs != null)
            {
                foreach (var sizeUpConfig in config.DynamicSizeUp.SizeUpConfigs)
                {
                    dynamicSizeUpMessage.Next();
                    dynamicSizeUpMessage.Enabled = sizeUpConfig.Enabled ? BooleanEnum.True : BooleanEnum.False;
                    dynamicSizeUpMessage.Edge = sizeUpConfig.Edge;
                    dynamicSizeUpMessage.AdditionalEdgePerContract = sizeUpConfig.AdditionalEdgePerContract;
                    dynamicSizeUpMessage.MaxAbsDelta = sizeUpConfig.MaxAbsDelta;
                    dynamicSizeUpMessage.MaxUnderWidth = sizeUpConfig.MaxUnderWidth;
                    dynamicSizeUpMessage.Size = sizeUpConfig.Size;
                    dynamicSizeUpMessage.ResubmitSizeOption = (byte)sizeUpConfig.ResubmitSizeOption;
                    dynamicSizeUpMessage.RequiredLoop = sizeUpConfig.RequiredLoop;
                    dynamicSizeUpMessage.ResubmitCount = sizeUpConfig.ResubmitCount;
                    dynamicSizeUpMessage.MatchSignalQtyLimit = sizeUpConfig.MatchSignalQtyLimit;
                }
            }

            var dynamicIntervalConfigs = message.DynamicIntervalConfigsCount(config.DynamicCloseInterval?.IntervalTable?.Count ?? 0);
            if (config.DynamicCloseInterval?.IntervalTable != null)
            {
                foreach (var sizeUpConfig in config.DynamicCloseInterval.IntervalTable)
                {
                    dynamicIntervalConfigs.Next();
                    dynamicIntervalConfigs.Active = sizeUpConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                    dynamicIntervalConfigs.MinDelta = sizeUpConfig.MinDelta;
                    dynamicIntervalConfigs.MaxDelta = sizeUpConfig.MaxDelta;
                    dynamicIntervalConfigs.AttemptedEdge = sizeUpConfig.AttemptedEdge;
                    dynamicIntervalConfigs.Interval = sizeUpConfig.Interval;
                    dynamicIntervalConfigs.ResubmitCount = sizeUpConfig.ResubmitCount;
                    dynamicIntervalConfigs.SetRoute(sizeUpConfig.Route ?? "");
                    dynamicIntervalConfigs.DisableRounding = sizeUpConfig.DisableRounding ? BooleanEnum.True : BooleanEnum.False;
                }
            }

            var exchToRouteList = message.ExchToRouteListCount(config.ExchToRouteList?.Count ?? 0);
            if (config.ExchToRouteList != null)
            {
                foreach (var exchRoutePair in config.ExchToRouteList)
                {
                    exchToRouteList.Next();
                    exchToRouteList.SetExch(exchRoutePair.Item1);
                    exchToRouteList.SetRoute(exchRoutePair.Item2);
                }
            }

            var dynamicIncrements = message.DynamicIncrementConfigsCount(config.DynamicIncrement?.Count ?? 0);
            if (config.DynamicIncrement != null)
            {
                foreach (var dynamicIncrement in config.DynamicIncrement)
                {
                    dynamicIncrements.Next();
                    dynamicIncrements.Edge = dynamicIncrement.Edge;
                    dynamicIncrements.Increment = dynamicIncrement.Increment;
                }
            }
        }

        private static void EncodeDteConfigs(AutoTraderConfigBinaryMessage.AutomationConfigsGroup.DteConfigsGroup message, List<DaysToExpirationEdgeModel?>? configs, bool isDynamic = false)
        {
            if (configs == null)
            {
                return;
            }
            foreach (var dteConfig in configs)
            {
                if (dteConfig == null)
                {
                    continue;
                }
                message.Next();
                message.IsDynamic = isDynamic ? BooleanEnum.True : BooleanEnum.False;
                message.Active = dteConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                message.DaysToExpiration = dteConfig.DaysToExpiration;
                message.MinBidAskSize = dteConfig.MinBidAskSize;
                message.MinIncrement = dteConfig.MinIncrement;
                message.MinWidth = dteConfig.MinWidth;
                message.MinSpacingForVertical = dteConfig.MinSpacingForVertical;
                message.MinSpacingForFlys = dteConfig.MinSpacingForFlys;
                message.MinSpacingForVerticalPercentage =
                    dteConfig.MinSpacingForVerticalPercentage;
                message.MinSpacingForFlysPercentage = dteConfig.MinSpacingForFlysPercentage;
                message.BaseEdge = dteConfig.BaseEdge;
                message.CloseEdge = dteConfig.CloseEdge;
                message.LoopMinEdge = dteConfig.LoopMinEdge;
                message.AutoPermMinEdge = dteConfig.AutoPermMinEdge;
                message.VerticalQty = dteConfig.VerticalQty;
                message.Qty = dteConfig.Qty;
                message.MaxPercentBid = dteConfig.MaxPercentBid;
                message.LoopMaxLoss = dteConfig.LoopMaxLoss;
                message.AdditionalEdgePerContract =
                    dteConfig.AdditionalEdgePerContract;
                message.AdditionalEdgePerWeightedVega =
                    dteConfig.AdditionalEdgePerWeightedVega;
                message.MaxAllowedAboveEma = dteConfig.MaxAllowedAboveEma;
                message.MaxAllowedAboveTheo = dteConfig.MaxAllowedAboveTheo;
                message.MaxAllowedAboveVola = dteConfig.MaxAllowedAboveVola;
                message.MinMarketWidth = dteConfig.MinMarketWidth;
                message.MaxThroughTradePx = dteConfig.MaxThroughTradePx;
                message.MinMarketCross = dteConfig.MinMarketCross;
                message.DynamicBaseEdge = dteConfig.DynamicBaseEdge;
                message.DynamicBaseEdgeAddition = dteConfig.DynamicBaseEdgeAddition;
                message.AdditionalEdgePerWidth = dteConfig.AdditionalEdgePerWidth;
                message.DynamicCloseEdge = dteConfig.DynamicCloseEdge;
                message.DynamicCloseEdgeAddition =
                    dteConfig.DynamicCloseEdgeAddition;
                message.AdditionalCloseEdgePerWidth =
                    dteConfig.AdditionalCloseEdgePerWidth;
                message.DynamicAutoPermMinEdge = dteConfig.DynamicAutoPermMinEdge;
                message.DynamicAutoPermMinEdgeAddition =
                    dteConfig.DynamicAutoPermMinEdgeAddition;
                message.DynamicLoopMinEdge = dteConfig.DynamicLoopMinEdge;
                message.DynamicLoopMinEdgeAddition =
                    dteConfig.DynamicLoopMinEdgeAddition;
                message.DynamicLoopMaxLoss = dteConfig.DynamicLoopMaxLoss;
                message.DynamicLoopMaxLossAddition =
                    dteConfig.DynamicLoopMaxLossAddition;
                message.DynamicAdditionalEdgePerContract =
                    dteConfig.DynamicAdditionalEdgePerContract;
                message.DynamicAdditionalEdgePerContractAddition =
                    dteConfig.DynamicAdditionalEdgePerContractAddition;
                message.DynamicAdditionalEdgePerWeightedVega =
                    dteConfig.DynamicAdditionalEdgePerWeightedVega;
                message.DynamicAdditionalEdgePerWeightedVegaAddition =
                    dteConfig.DynamicAdditionalEdgePerWeightedVegaAddition;
                message.DynamicMaxAllowedPercentBid =
                    dteConfig.DynamicMaxAllowedPercentBid;
                message.DynamicMaxAllowedPercentBidAddition =
                    dteConfig.DynamicMaxAllowedPercentBidAddition;
                message.DynamicMaxAllowedAboveEma =
                    dteConfig.DynamicMaxAllowedAboveEma;
                message.DynamicMaxAllowedAboveEmaAddition =
                    dteConfig.DynamicMaxAllowedAboveEmaAddition;
                message.DynamicMaxAllowedAboveTheo =
                    dteConfig.DynamicMaxAllowedAboveTheo;
                message.DynamicMaxAllowedAboveTheoAddition =
                    dteConfig.DynamicMaxAllowedAboveTheoAddition;
                message.DynamicMaxAllowedAboveVola =
                    dteConfig.DynamicMaxAllowedAboveVola;
                message.DynamicMaxAllowedAboveVolaAddition =
                    dteConfig.DynamicMaxAllowedAboveVolaAddition;
                message.DynamicMinMarketWidth = dteConfig.DynamicMinMarketWidth;
                message.DynamicMinMarketWidthAddition =
                    dteConfig.DynamicMinMarketWidthAddition;
            }
        }

        public int EncodeTheoBatchUpdateMessage(DirectBuffer directBuffer, int offset, TheoBatchUpdate batchUpdate, long timestamp)
        {
            var adjustedFlags = batchUpdate.AdjustedFlags;
            var updates = batchUpdate.Updates;

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TheoBatchUpdateMessage message = new TheoBatchUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TheoBatchUpdateMessage.BlockLength;
            messageHeader.SchemaId = TheoBatchUpdateMessage.SchemaId;
            messageHeader.TemplateId = TheoBatchUpdateMessage.TemplateId;
            messageHeader.Version = TheoBatchUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetUnderIndex(0, (byte)(batchUpdate.UnderIndex >> 16));
            message.SetUnderIndex(1, (byte)(batchUpdate.UnderIndex >> 8));
            message.SetUnderIndex(2, (byte)batchUpdate.UnderIndex);
            message.Timestamp = timestamp;

            var updatesGroup = message.UpdatesCount(updates.Count);
            for (int i = 0; i < updates.Count; i++)
            {
                if (!adjustedFlags[i])
                {
                    continue;
                }

                var update = updates[i];
                updatesGroup.Next();
                updatesGroup.OptionIndex = (ushort)i;
                EncodeDoubleNull2(updatesGroup.HanweckTheo, update.Theo);
                EncodeDoubleNull2(updatesGroup.HanweckAdjTheo, update.DeltaAdjustedTheo);
                EncodeDoubleNull2(updatesGroup.VolaTheo, update.VolaTheoResult.Theo);
                EncodeDoubleNull2(updatesGroup.VolaAdjTheo, update.VolaTheoResult.DeltaAdjustedTheo);
                EncodeDoubleNull4(updatesGroup.Delta, update.Delta);
                EncodeDoubleNull2(updatesGroup.VolaUnderlyingSpot, update.VolaTheoResult.Underlying);
                EncodeDoubleNull2(updatesGroup.VolaUnderlyingSnap, update.VolaTheoResult.SnapshotUnderlying);
                EncodeDoubleNull4(updatesGroup.VolaDelta, update.VolaTheoResult.Delta);
                updatesGroup.SnapshotTicks = update.SnapshotTicks;
                updatesGroup.VolaIv = update.VolaTheoResult.Iv;
            }
            updatesGroup.ResetCountToIndex();

            return message.Limit - offset;
        }

        public int EncodeAdjTheoBatchUpdateMessage(DirectBuffer directBuffer, int offset, TheoBatchUpdate batchUpdate, long timestamp)
        {
            var adjustedFlags = batchUpdate.AdjustedFlags;
            var updates = batchUpdate.Updates;

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            AdjTheoBatchUpdateMessage message = new AdjTheoBatchUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = AdjTheoBatchUpdateMessage.BlockLength;
            messageHeader.SchemaId = AdjTheoBatchUpdateMessage.SchemaId;
            messageHeader.TemplateId = AdjTheoBatchUpdateMessage.TemplateId;
            messageHeader.Version = AdjTheoBatchUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetUnderIndex(0, (byte)(batchUpdate.UnderIndex >> 16));
            message.SetUnderIndex(1, (byte)(batchUpdate.UnderIndex >> 8));
            message.SetUnderIndex(2, (byte)batchUpdate.UnderIndex);
            message.Timestamp = timestamp;

            var updatesGroup = message.UpdatesCount(updates.Count);
            for (int i = 0; i < updates.Count; i++)
            {
                if (!adjustedFlags[i])
                {
                    continue;
                }

                var update = updates[i];
                updatesGroup.Next();
                updatesGroup.OptionIndex = (ushort)i;
                EncodeDoubleNull2(updatesGroup.HanweckAdjTheo, update.DeltaAdjustedTheo);
                EncodeDoubleNull2(updatesGroup.VolaAdjTheo, update.VolaTheoResult.DeltaAdjustedTheo);
            }
            updatesGroup.ResetCountToIndex();

            return message.Limit - offset;
        }

        public int EncodeSingleFieldUpdateMessage(DirectBuffer directBuffer, int offset, int tickerId, SubscriptionFieldType updateType, double value)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SingleFieldUpdateMessage message = new SingleFieldUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SingleFieldUpdateMessage.BlockLength;
            messageHeader.SchemaId = SingleFieldUpdateMessage.SchemaId;
            messageHeader.TemplateId = SingleFieldUpdateMessage.TemplateId;
            messageHeader.Version = SingleFieldUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.TickerId = tickerId;
            message.UpdateType = (short)updateType;
            message.Value = value;

            return message.Limit - offset;
        }

        public int EncodeZpTheoUpdateMessage(DirectBuffer directBuffer, int offset, int tickerId, ulong sequence, double theoBid, double theoAsk)
        {
            if (tickerId < 0 || tickerId > 16777215)
            {
                throw new ArgumentOutOfRangeException(nameof(tickerId));
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ZpTheoUpdateMessage message = new ZpTheoUpdateMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ZpTheoUpdateMessage.BlockLength;
            messageHeader.SchemaId = ZpTheoUpdateMessage.SchemaId;
            messageHeader.TemplateId = ZpTheoUpdateMessage.TemplateId;
            messageHeader.Version = ZpTheoUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetTickerId(0, (byte)(tickerId >> 16));
            message.SetTickerId(1, (byte)(tickerId >> 8));
            message.SetTickerId(2, (byte)tickerId);
            message.Sequence = sequence;
            EncodePriceNull3(message.TheoBid, theoBid);
            EncodePriceNull3(message.TheoAsk, theoAsk);

            return message.Limit - offset;
        }

        public int EncodeOpenSpreadExchOrderMessage(DirectBuffer directBuffer, int offset, IOpenSpreadExchOrder model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OpenSpreadExchOrderMessage message = new OpenSpreadExchOrderMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OpenSpreadExchOrderMessage.BlockLength;
            messageHeader.SchemaId = OpenSpreadExchOrderMessage.SchemaId;
            messageHeader.TemplateId = OpenSpreadExchOrderMessage.TemplateId;
            messageHeader.Version = OpenSpreadExchOrderMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetUnderlying(model.Underlying ?? "");
            message.SetOrderID(model.OrderID ?? "");

            message.FlipSide = model.FlipSide ? BooleanEnum.True : BooleanEnum.False;

            message.Exch = (byte)model.Exch;

            message.OrigOrderSize = model.OrigOrderSize;
            message.OrderSize = model.OrderSize;
            message.Price = model.Price;
            message.Timestamp = model.Timestamp.ToUnixEpoch();

            message.SetSpreadSymbol(model.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeRemoveSpreadExchOrderMessage(DirectBuffer directBuffer, int offset, IOpenSpreadExchOrder model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RemoveSpreadExchOrderMessage message = new RemoveSpreadExchOrderMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RemoveSpreadExchOrderMessage.BlockLength;
            messageHeader.SchemaId = RemoveSpreadExchOrderMessage.SchemaId;
            messageHeader.TemplateId = RemoveSpreadExchOrderMessage.TemplateId;
            messageHeader.Version = RemoveSpreadExchOrderMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetUnderlying(model.Underlying ?? "");
            message.SetOrderID(model.OrderID ?? "");

            return message.Limit - offset;
        }

        public int EncodeVolSurfaceRequest(DirectBuffer directBuffer, int offset, VolSurfaceRequestModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            VolSurfaceRequest message = new VolSurfaceRequest();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = VolSurfaceRequest.BlockLength;
            messageHeader.SchemaId = VolSurfaceRequest.SchemaId;
            messageHeader.TemplateId = VolSurfaceRequest.TemplateId;
            messageHeader.Version = VolSurfaceRequest.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = model.RequestId;
            message.RequestTime = ToUnixEpoch(model.RequestTime);
            message.SymbolId = model.SymbolId;
            message.TenorIndex = model.TenorIndex;

            return message.Limit - offset;
        }

        public int EncodeVolSurfaceResponse(DirectBuffer directBuffer, int offset, VolSurfaceResponseModel model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            VolSurfaceResponse message = new VolSurfaceResponse();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = VolSurfaceResponse.BlockLength;
            messageHeader.SchemaId = VolSurfaceResponse.SchemaId;
            messageHeader.TemplateId = VolSurfaceResponse.TemplateId;
            messageHeader.Version = VolSurfaceResponse.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestID = model.RequestId;
            message.Success = model.Success ? BooleanEnum.True : BooleanEnum.False;
            message.MarketDataSnapshotTime = model.MarketDataSnapshotTime.Ticks;

            // Encode VolaCurvePoints group
            var volaCurvePointsGroup = message.VolaCurvePoints;
            volaCurvePointsGroup.WrapForEncode(message, directBuffer, model.VolaCurvePoints.Count);
            foreach (var point in model.VolaCurvePoints)
            {
                volaCurvePointsGroup.Next();
                volaCurvePointsGroup.NormalizedStrike = point.NormalizedStrike;
                volaCurvePointsGroup.Volatility = point.Volatility;
                volaCurvePointsGroup.Strike = point.Strike;
                volaCurvePointsGroup.TheoPrice = point.TheoPrice;
            }

            // Encode PutsMarketData group
            var putsMarketDataGroup = message.PutsMarketData;
            putsMarketDataGroup.WrapForEncode(message, directBuffer, model.PutsMarketData.Count);
            foreach (var put in model.PutsMarketData)
            {
                putsMarketDataGroup.Next();
                putsMarketDataGroup.NormalizedStrike = put.NormalizedStrike;
                putsMarketDataGroup.PutBidIV = put.BidIV;
                putsMarketDataGroup.PutAskIV = put.AskIV;
                putsMarketDataGroup.PutBid = put.Bid;
                putsMarketDataGroup.PutAsk = put.Ask;
            }

            // Encode CallsMarketData group
            var callsMarketDataGroup = message.CallsMarketData;
            callsMarketDataGroup.WrapForEncode(message, directBuffer, model.CallsMarketData.Count);
            foreach (var call in model.CallsMarketData)
            {
                callsMarketDataGroup.Next();
                callsMarketDataGroup.NormalizedStrike = call.NormalizedStrike;
                callsMarketDataGroup.CallBidIV = call.BidIV;
                callsMarketDataGroup.CallAskIV = call.AskIV;
                callsMarketDataGroup.CallBid = call.Bid;
                callsMarketDataGroup.CallAsk = call.Ask;
            }

            return message.Limit - offset;
        }

        public int EncodeHerculesEchoRequestMessage(DirectBuffer directBuffer, int offset, bool requestEcho)
        {

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            HerculesEchoRequestMessage message = new HerculesEchoRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HerculesEchoRequestMessage.BlockLength;
            messageHeader.SchemaId = HerculesEchoRequestMessage.SchemaId;
            messageHeader.TemplateId = HerculesEchoRequestMessage.TemplateId;
            messageHeader.Version = HerculesEchoRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.RequestEcho = requestEcho ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodeHerculesEchoMessage(DirectBuffer directBuffer, int offset, IOrder order, string? source, Venue venue, int updateType)
        {

            if (order.IsComplexOrder)
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                foreach (IComplexOrderLeg leg in complexOrder.Legs)
                {
                }
            }

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            HerculesEchoMessage message = new HerculesEchoMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = HerculesEchoMessage.BlockLength;
            messageHeader.SchemaId = HerculesEchoMessage.SchemaId;
            messageHeader.TemplateId = HerculesEchoMessage.TemplateId;
            messageHeader.Version = HerculesEchoMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.SetPermID(order.PermID);
            message.IsComplexOrder = order.IsComplexOrder ? BooleanEnum.True : BooleanEnum.False;

            message.PartiallyFilled = order.PartiallyFilled ? BooleanEnum.True : BooleanEnum.False;
            message.IsFirstFill = order.IsFirstFill ? BooleanEnum.True : BooleanEnum.False;
            message.ExecutionType = (byte)order.ExecutionType;

            message.LastQuantity = order.LastQuantity;
            message.FilledQty = order.FilledQty;
            message.LeavesQuantity = order.LeavesQuantity;
            message.CumulativeQuantity = order.CumulativeQuantity;
            message.Quantity = order.Quantity;

            message.SpreadAvgPrice.Mantissa = EncodeMantissa(order.SpreadAvgPrice, message.SpreadAvgPrice.Exponent);
            message.AveragePrice.Mantissa = EncodeMantissa(order.AveragePrice, message.AveragePrice.Exponent);
            message.Price.Mantissa = EncodeMantissa(order.Price, message.Price.Exponent);
            message.LastPrice.Mantissa = EncodeMantissa(order.LastPrice, message.LastPrice.Exponent);
            message.MinPrice.Mantissa = EncodeMantissa(order.MinPrice, message.MinPrice.Exponent);
            message.MaxPrice.Mantissa = EncodeMantissa(order.MaxPrice, message.MaxPrice.Exponent);
            message.TagEdge.Mantissa = EncodeMantissa(order.TagEdge, message.TagEdge.Exponent);
            message.TagMid.Mantissa = EncodeMantissa(order.TagMid, message.TagMid.Exponent);
            message.TagBid.Mantissa = EncodeMantissa(order.TagBid, message.TagBid.Exponent);
            message.TagAsk.Mantissa = EncodeMantissa(order.TagAsk, message.TagAsk.Exponent);
            message.TagTheo.Mantissa = EncodeMantissa(order.TagTheo, message.TagTheo.Exponent);
            message.TagVolaV0.Mantissa = EncodeMantissa(order.TagVolaV0, message.TagVolaV0.Exponent);
            message.TagVolaV1.Mantissa = EncodeMantissa(order.TagVolaV1, message.TagVolaV1.Exponent);
            message.TagVolaV2.Mantissa = EncodeMantissa(order.TagVolaV2, message.TagVolaV2.Exponent);
            message.TagEma.Mantissa = EncodeMantissa(order.TagEma, message.TagEma.Exponent);
            message.TagVolaIv = order.VolaIv;
            message.TheoBid.Mantissa = EncodeMantissa(order.TheoBid, message.TheoBid.Exponent);
            message.TheoAsk.Mantissa = EncodeMantissa(order.TheoAsk, message.TheoAsk.Exponent);
            message.Fee1.Mantissa = EncodeMantissa(order.Fee1, message.Fee1.Exponent);
            message.Fee2.Mantissa = EncodeMantissa(order.Fee2, message.Fee2.Exponent);
            message.Bid.Mantissa = EncodeMantissa(order.Bid, message.Bid.Exponent);
            message.Ask.Mantissa = EncodeMantissa(order.Ask, message.Ask.Exponent);
            message.UnderBid.Mantissa = EncodeMantissa(order.UnderBid, message.UnderBid.Exponent);
            message.UnderAsk.Mantissa = EncodeMantissa(order.UnderAsk, message.UnderAsk.Exponent);
            message.TV.Mantissa = EncodeMantissa(order.TV, message.TV.Exponent);
            message.Delta.Mantissa = EncodeMantissa(order.Delta, message.Delta.Exponent);
            message.ExchangeFee1.Mantissa = EncodeMantissa(order.ExchangeFee1, message.ExchangeFee1.Exponent);
            message.ExchangeFee2.Mantissa = EncodeMantissa(order.ExchangeFee2, message.ExchangeFee2.Exponent);
            message.BrokerFee1.Mantissa = EncodeMantissa(order.BrokerFee1, message.BrokerFee1.Exponent);
            message.BrokerFee2.Mantissa = EncodeMantissa(order.BrokerFee2, message.BrokerFee2.Exponent);
            message.TotalContracts.Mantissa = EncodeMantissa(order.TotalContracts, message.TotalContracts.Exponent);
            message.FillTime.Mantissa = EncodeMantissa(order.FillTime, message.FillTime.Exponent);
            message.TradeToNewTime.Mantissa = EncodeMantissa(order.TradeToNewTime, message.TradeToNewTime.Exponent);
            message.SubmitToNewTime.Mantissa = EncodeMantissa(order.SubmitToNewTime, message.SubmitToNewTime.Exponent);
            message.NewToCancelTime.Mantissa = EncodeMantissa(order.NewToCancelTime, message.NewToCancelTime.Exponent);
            message.BidPercentOfFillPrice.Mantissa = EncodeMantissa(order.BidPercentOfFillPrice, message.BidPercentOfFillPrice.Exponent);
            message.OmsBidPercentOfFillPrice.Mantissa = EncodeMantissa(order.OmsBidPercentOfFillPrice, message.OmsBidPercentOfFillPrice.Exponent);
            message.TotalDelta.Mantissa = EncodeMantissa(order.TotalDelta, message.TotalDelta.Exponent);
            message.HanweckTotalTheo.Mantissa = EncodeMantissa(order.HanweckTotalTheo, message.HanweckTotalTheo.Exponent);
            message.HanweckTotalGamma.Mantissa = EncodeMantissa(order.HanweckTotalGamma, message.HanweckTotalGamma.Exponent);
            message.HanweckTotalVega.Mantissa = EncodeMantissa(order.HanweckTotalVega, message.HanweckTotalVega.Exponent);
            message.HanweckTotalTheta.Mantissa = EncodeMantissa(order.HanweckTotalTheta, message.HanweckTotalTheta.Exponent);
            message.HanweckTotalRho.Mantissa = EncodeMantissa(order.HanweckTotalRho, message.HanweckTotalRho.Exponent);
            message.HanweckTotalIV.Mantissa = EncodeMantissa(order.HanweckTotalIV, message.HanweckTotalIV.Exponent);
            message.HanweckTotalUnder.Mantissa = EncodeMantissa(order.HanweckTotalUnder, message.HanweckTotalUnder.Exponent);
            message.HanweckTotalUBid.Mantissa = EncodeMantissa(order.HanweckTotalUBid, message.HanweckTotalUBid.Exponent);
            message.HanweckTotalUAsk.Mantissa = EncodeMantissa(order.HanweckTotalUAsk, message.HanweckTotalUAsk.Exponent);
            message.HanweckTotalBid.Mantissa = EncodeMantissa(order.HanweckTotalBid, message.HanweckTotalBid.Exponent);
            message.HanweckTotalAsk.Mantissa = EncodeMantissa(order.HanweckTotalAsk, message.HanweckTotalAsk.Exponent);
            message.EdgeOverride.Mantissa = EncodeMantissa(order.EdgeOverride, message.EdgeOverride.Exponent);
            message.AdjustedEdgeOverride.Mantissa = EncodeMantissa(order.AdjustedEdgeOverride, message.AdjustedEdgeOverride.Exponent);
            message.EdgeToTheo.Mantissa = EncodeMantissa(order.EdgeToTheo, message.EdgeToTheo.Exponent);
            message.TagEdgeToTheo.Mantissa = EncodeMantissa(order.TagEdgeToTheo, message.TagEdgeToTheo.Exponent);
            message.TagEdgeToEma.Mantissa = EncodeMantissa(order.TagEdgeToEma, message.TagEdgeToEma.Exponent);
            message.TagEdgeToVolaV0.Mantissa = EncodeMantissa(order.TagEdgeToVolaV0, message.TagEdgeToVolaV0.Exponent);
            message.TagEdgeToVolaV1.Mantissa = EncodeMantissa(order.TagEdgeToVolaV1, message.TagEdgeToVolaV1.Exponent);
            message.TagEdgeToVolaV2.Mantissa = EncodeMantissa(order.TagEdgeToVolaV2, message.TagEdgeToVolaV2.Exponent);
            message.TagBestBid.Mantissa = EncodeMantissa(order.TagBestBid, message.TagBestBid.Exponent);
            message.TagBestAsk.Mantissa = EncodeMantissa(order.TagBestAsk, message.TagBestAsk.Exponent);
            message.TagMktMkrBid.Mantissa = EncodeMantissa(order.TagMktMkrBid, message.TagMktMkrBid.Exponent);
            message.TagMktMkrAsk.Mantissa = EncodeMantissa(order.TagMktMkrAsk, message.TagMktMkrAsk.Exponent);
            message.InitialEdge.Mantissa = EncodeMantissa(order.InitialEdge, message.InitialEdge.Exponent);
            message.OpenEdge.Mantissa = EncodeMantissa(order.OpenEdge, message.OpenEdge.Exponent);
            message.CloseEdge.Mantissa = EncodeMantissa(order.CloseEdge, message.CloseEdge.Exponent);

            message.LastEdge.Mantissa = EncodeMantissa(order.LastEdge, message.LastEdge.Exponent);
            message.DeltaAdjLastEdge.Mantissa = EncodeMantissa(order.DeltaAdjLastEdge, message.DeltaAdjLastEdge.Exponent);
            message.DeltaAdjLastEdgeNotional.Mantissa = EncodeMantissa(order.DeltaAdjLastEdgeNotional, message.DeltaAdjLastEdgeNotional.Exponent);
            message.EdgeScanFeedDeltaAdjPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedDeltaAdjPrice, message.EdgeScanFeedDeltaAdjPrice.Exponent);

            message.DeltaAdjChange.Mantissa = EncodeMantissa(order.DeltaAdjChange, message.DeltaAdjChange.Exponent);
            message.DeltaAdjChangeNotional.Mantissa = EncodeMantissa(order.DeltaAdjChangeNotional, message.DeltaAdjChangeNotional.Exponent);

            message.EdgeScanFeedEdge.Mantissa = EncodeMantissa(order.EdgeScanFeedEdge, message.EdgeScanFeedEdge.Exponent);
            message.EdgeScanFeedTimespan.Mantissa = EncodeMantissa(order.EdgeScanFeedTimespan, message.EdgeScanFeedTimespan.Exponent);

            message.EdgeScanFeedBuyPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedBuyPrice, message.EdgeScanFeedBuyPrice.Exponent);
            message.EdgeScanFeedBuyQty = order.EdgeScanFeedBuyQty;
            message.EdgeScanFeedSellPrice.Mantissa = EncodeMantissa(order.EdgeScanFeedSellPrice, message.EdgeScanFeedSellPrice.Exponent);
            message.EdgeScanFeedSellQty = order.EdgeScanFeedSellQty;
            message.EdgeScanFeedBuyTime = order.EdgeScanFeedBuyTime.ToUnixEpoch();
            message.EdgeScanFeedSellTime = order.EdgeScanFeedSellTime.ToUnixEpoch();
            message.EdgeScanFeedRespondLatency.Mantissa = EncodeMantissa(order.EdgeScanFeedRespondLatency, message.EdgeScanFeedRespondLatency.Exponent);

            message.EdgeScanFeedConditionCode = (byte)order.EdgeScanFeedConditionCode;

            message.ResubmitCount = order.ResubmitCount;
            message.TotalEstimatedResubmit = order.TotalEstimatedResubmit;

            var side = order.Side;
            message.AggressorSide = side == Side.Buy ? AggressorSide.Buy : AggressorSide.Sell;
            message.OrderStatus = (OrderStatus)order.OrderStatus;
            message.BaseStrategy = (Generated.BaseStrategy)order.BaseStrategy;
            message.PositionEffect = (PositionEffect)order.PositionEffect;
            message.TimeInForce = (Generated.TimeInForce)order.TimeInForce;

            message.OrderSource = (Generated.OrderSource)order.OrderSource;

            message.SetUsername(order.Username == null ? "" : order.Username.Length > 10 ? order.Username[..10] : order.Username);
            message.SetUnderlyingSymbol(order.UnderlyingSymbol);

            message.SubmitTime = ToUnixEpoch(order.SubmitTime);
            message.LastUpdateTime = ToUnixEpoch(order.LastUpdateTime);
            message.Timestamp = ToUnixEpoch(order.Timestamp);
            message.NewStatusTimeStamp = ToUnixEpoch(order.NewStatusTimeStamp);

            message.DeltaAdjustedTheo.Mantissa = EncodeMantissa(order.DeltaAdjustedTheo, message.DeltaAdjustedTheo.Exponent);
            message.BidSize = order.BidSize;
            message.AskSize = order.AskSize;
            message.UnderlyingBidSize = order.UnderlyingBidSize;
            message.UnderlyingAskSize = order.UnderlyingAskSize;

            message.EdgeType = (int)order.EdgeType;
            message.Edge.Mantissa = EncodeMantissa(order.Edge, message.Edge.Exponent);

            message.IsDeltaAdjusted = order.IsDeltaAdjusted ? BooleanEnum.True : BooleanEnum.False;
            message.LoopInitLatency.Mantissa = EncodeMantissa(order.LoopInitLatency, message.LoopInitLatency.Exponent);
            message.TagUnderBid.Mantissa = EncodeMantissa(order.TagUnderBid, message.TagUnderBid.Exponent);
            message.TagUnderAsk.Mantissa = EncodeMantissa(order.TagUnderAsk, message.TagUnderAsk.Exponent);
            message.IsTagged = order.IsTagged ? BooleanEnum.True : BooleanEnum.False;

            var hardSide = order.HardSide;
            message.HardSide = hardSide.HasValue ? (Generated.Side)hardSide : Generated.Side.NULL_VALUE;
            message.HardSideDesignationTime = order.HardSideDesignationTime.ToUnixEpoch();
            message.HardSideBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideBuyGiveUp, message.HardSideBuyGiveUp.Exponent);
            message.HardSideSellGiveUp.Mantissa = EncodeMantissa(order.HardSideSellGiveUp, message.HardSideSellGiveUp.Exponent);
            var hardSideAtTrade = order.HardSideAtTrade;
            message.HardSideAtTrade = hardSideAtTrade.HasValue ? (Generated.Side)hardSideAtTrade : Generated.Side.NULL_VALUE;
            message.HardSideAtTradeDesignationTime = order.HardSideAtTradeDesignationTime.ToUnixEpoch();
            message.HardSideAtTradeBuyGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeBuyGiveUp, message.HardSideAtTradeBuyGiveUp.Exponent);
            message.HardSideAtTradeSellGiveUp.Mantissa = EncodeMantissa(order.HardSideAtTradeSellGiveUp, message.HardSideAtTradeSellGiveUp.Exponent);

            message.EdgeGiveUp.Mantissa = EncodeMantissa(order.EdgeGiveUp, message.EdgeGiveUp.Exponent);
            message.CloseSubs.Mantissa = EncodeMantissa(order.CloseSubs, message.CloseSubs.Exponent);
            message.OrderEdgeToTheo.Mantissa = EncodeMantissa(order.OrderEdgeToTheo, message.OrderEdgeToTheo.Exponent);

            message.TimeValue.Mantissa = EncodeMantissa(order.TimeValue, message.TimeValue.Exponent);
            message.IntrinsicValue.Mantissa = EncodeMantissa(order.IntrinsicValue, message.IntrinsicValue.Exponent);
            message.FVDivs.Mantissa = EncodeMantissa(order.FVDivs, message.FVDivs.Exponent);
            message.UFwd.Mantissa = EncodeMantissa(order.UFwd, message.UFwd.Exponent);
            message.UFwdFactor.Mantissa = EncodeMantissa(order.UFwdFactor, message.UFwdFactor.Exponent);
            message.BorrowCost.Mantissa = EncodeMantissa(order.BorrowCost, message.BorrowCost.Exponent);
            message.BorrowRate.Mantissa = EncodeMantissa(order.BorrowRate, message.BorrowRate.Exponent);
            message.UPrice.Mantissa = EncodeMantissa(order.UPrice, message.UPrice.Exponent);
            message.UTheo.Mantissa = EncodeMantissa(order.UTheo, message.UTheo.Exponent);

            message.SharedId = order.SharedId;
            message.Sequence = order.Sequence;
            message.TypeId = (ushort)order.TypeId;
            message.SubTypeCode = (ushort)order.SubTypeId;
            message.SubTypeSequence = order.SubTypeSequence;
            var orderVenue = order.Venue;
            message.Venue = orderVenue.HasValue ? (byte)orderVenue : HerculesEchoMessage.VenueNullValue;
            message.CostOfHedging.Mantissa = EncodeMantissa(order.CostOfHedging, message.CostOfHedging.Exponent);
            var orderSubType = order.SubType;
            message.SubType = orderSubType.HasValue ? (byte)orderSubType : HerculesEchoMessage.SubTypeNullValue;

            if (!order.IsComplexOrder)
            {
                message.NoLegsCount(0);
            }
            else
            {
                IComplexOrder complexOrder = (IComplexOrder)order;
                HerculesEchoMessage.NoLegsGroup legMessage = message.NoLegsCount(complexOrder.Legs.Count);
                for (int i = 0; i < complexOrder.Legs.Count; i++)
                {
                    IComplexOrderLeg leg = complexOrder.Legs.ElementAt(i);
                    legMessage.Next();

                    legMessage.SetLegID(leg.LegID);

                    legMessage.Ratio = leg.Ratio;
                    legMessage.Quantity = leg.Quantity;
                    legMessage.LastQuantity = leg.LastQuantity;
                    legMessage.LeavesQuantity = leg.LeavesQuantity;
                    legMessage.CumulativeQuantity = leg.CumulativeQuantity;

                    legMessage.ExchangeFee2.Mantissa = EncodeMantissa(leg.ExchangeFee2, legMessage.ExchangeFee2.Exponent);
                    legMessage.ExchangeFee1.Mantissa = EncodeMantissa(leg.ExchangeFee1, legMessage.ExchangeFee1.Exponent);
                    legMessage.Fee2.Mantissa = EncodeMantissa(leg.Fee2, legMessage.Fee2.Exponent);
                    legMessage.Fee1.Mantissa = EncodeMantissa(leg.Fee1, legMessage.Fee1.Exponent);
                    legMessage.Delta.Mantissa = EncodeMantissa(leg.Delta, legMessage.Delta.Exponent);
                    legMessage.TV.Mantissa = EncodeMantissa(leg.TV, legMessage.TV.Exponent);
                    legMessage.Ask.Mantissa = EncodeMantissa(leg.Ask, legMessage.Ask.Exponent);
                    legMessage.Bid.Mantissa = EncodeMantissa(leg.Bid, legMessage.Bid.Exponent);
                    legMessage.AveragePrice.Mantissa = EncodeMantissa(leg.AveragePrice, legMessage.AveragePrice.Exponent);
                    legMessage.LastPrice.Mantissa = EncodeMantissa(leg.LastPrice, legMessage.LastPrice.Exponent);
                    legMessage.BrokerFee1.Mantissa = EncodeMantissa(leg.BrokerFee1, legMessage.BrokerFee1.Exponent);
                    legMessage.BrokerFee2.Mantissa = EncodeMantissa(leg.BrokerFee2, legMessage.BrokerFee2.Exponent);
                    legMessage.HanweckTV.Mantissa = EncodeMantissa(leg.HanweckTV, legMessage.HanweckTV.Exponent);
                    legMessage.HanweckGamma.Mantissa = EncodeMantissa(leg.HanweckGamma, legMessage.HanweckGamma.Exponent);
                    legMessage.HanweckVega.Mantissa = EncodeMantissa(leg.HanweckVega, legMessage.HanweckVega.Exponent);
                    legMessage.HanweckTheta.Mantissa = EncodeMantissa(leg.HanweckTheta, legMessage.HanweckTheta.Exponent);
                    legMessage.HanweckRho.Mantissa = EncodeMantissa(leg.HanweckRho, legMessage.HanweckRho.Exponent);
                    legMessage.HanweckIV.Mantissa = EncodeMantissa(leg.HanweckIV, legMessage.HanweckIV.Exponent);
                    legMessage.HanweckUnder.Mantissa = EncodeMantissa(leg.HanweckUnder, legMessage.HanweckUnder.Exponent);
                    legMessage.HanweckUnderBid.Mantissa = EncodeMantissa(leg.HanweckUnderBid, legMessage.HanweckUnderBid.Exponent);
                    legMessage.HanweckUnderAsk.Mantissa = EncodeMantissa(leg.HanweckUnderAsk, legMessage.HanweckUnderAsk.Exponent);
                    legMessage.HanweckBid.Mantissa = EncodeMantissa(leg.HanweckBid, legMessage.HanweckBid.Exponent);
                    legMessage.HanweckAsk.Mantissa = EncodeMantissa(leg.HanweckAsk, legMessage.HanweckAsk.Exponent);

                    legMessage.DeltaAdjustedTheo.Mantissa = EncodeMantissa(leg.DeltaAdjustedTheo, legMessage.DeltaAdjustedTheo.Exponent);
                    legMessage.BidSize = leg.BidSize;
                    legMessage.AskSize = leg.AskSize;

                    legMessage.PositionEffect = (PositionEffect)leg.PositionEffect;
                    legMessage.LegSide = leg.Side == Side.Buy ? LegSide.BuySide : LegSide.SellSide;
                    legMessage.OrderStatus = (OrderStatus)leg.OrderStatus;

                    legMessage.Timestamp = ToUnixEpoch(leg.Timestamp);
                    legMessage.LastUpdateTime = ToUnixEpoch(leg.LastUpdateTime);
                    legMessage.HanweckBidTime = ToUnixEpoch(leg.HanweckBidTime);
                    legMessage.HanweckAskTime = ToUnixEpoch(leg.HanweckAskTime);
                    legMessage.HanweckTimestamp = ToUnixEpoch(leg.HanweckTimestamp);

                    legMessage.TimeValue.Mantissa = EncodeMantissa(leg.TimeValue, legMessage.TimeValue.Exponent);
                    legMessage.IntrinsicValue.Mantissa = EncodeMantissa(leg.IntrinsicValue, legMessage.IntrinsicValue.Exponent);
                    legMessage.FVDivs.Mantissa = EncodeMantissa(leg.FVDivs, legMessage.FVDivs.Exponent);
                    legMessage.UFwd.Mantissa = EncodeMantissa(leg.UFwd, legMessage.UFwd.Exponent);
                    legMessage.UFwdFactor.Mantissa = EncodeMantissa(leg.UFwdFactor, legMessage.UFwdFactor.Exponent);
                    legMessage.BorrowCost.Mantissa = EncodeMantissa(leg.BorrowCost, legMessage.BorrowCost.Exponent);
                    legMessage.BorrowRate.Mantissa = EncodeMantissa(leg.BorrowRate, legMessage.BorrowRate.Exponent);
                    legMessage.UPrice.Mantissa = EncodeMantissa(leg.UPrice, legMessage.UPrice.Exponent);
                    legMessage.UTheo.Mantissa = EncodeMantissa(leg.UTheo, legMessage.UTheo.Exponent);

                    EncodeLegContraFields_HerculesEcho(legMessage, leg);

                    legMessage.SetPermID((leg.PermID) ?? "");
                    legMessage.SetOrderID((leg.OrderID) ?? "");
                    legMessage.SetSymbol((leg.Symbol) ?? "");
                }
            }

            EncodeOrderContraFields_HerculesEcho(message, order);

            message.SetLastExchange((order.LastExchange) ?? "");
            message.SetExchanges((order.Exchanges) ?? "");
            message.SetReason((order.Reason) ?? "");
            message.SetSource((order.Source) ?? "");
            message.SetAccountAcronym((order.AccountAcronym) ?? "");
            message.SetTag((order.Tag) ?? "");
            message.SetTrader((order.Trader) ?? "");
            message.SetOrderType((order.Type) ?? "");
            message.SetOrderID((order.OrderID) ?? "");
            message.SetRoute((order.Route) ?? "");
            message.SetSymbol((order.Symbol) ?? "");
            message.SetDescription((order.Description) ?? "");
            message.SetSpreadId((order.SpreadId) ?? "");
            message.SetFullTag((order.Tag) ?? "");
            message.SetComment((order.Comment) ?? "");
            message.SetAutomationType((order.AutomationType) ?? "");
            message.SetSpreadHash((order.SpreadHash) ?? "");
            message.SetTagger(order.Tagger ?? "");
            message.SetTaggedMessage(order.TaggedMessage ?? "");
            message.SetSource(source ?? "");
            message.VenueTypeId = (ushort)venue;
            message.UpdateTypeId = (ushort)updateType;

            return message.Limit - offset;
        }

        private static void EncodeLegContraFields_HerculesEcho(HerculesEchoMessage.NoLegsGroup legMessage, IComplexOrderLeg leg)
        {
            var legCaps = leg.ContraCapacities;
            var legCapsGroup = legMessage.NoLegContraCapacitiesCount(legCaps?.Count ?? 0);
            if (legCaps != null)
            {
                for (int j = 0; j < legCaps.Count; j++)
                {
                    legCapsGroup.Next().Value = (byte)legCaps[j];
                }
            }

            var legBrokers = leg.ContraBrokerNames;
            var legBrokersGroup = legMessage.NoLegContraBrokerNamesCount(legBrokers?.Count ?? 0);
            if (legBrokers != null)
            {
                for (int j = 0; j < legBrokers.Count; j++)
                {
                    legBrokersGroup.Next().Value = (byte)legBrokers[j];
                }
            }

            var legCmtas = leg.ContraCmtas;
            var legCmtasGroup = legMessage.NoLegContraCmtasCount(legCmtas?.Count ?? 0);
            if (legCmtas != null)
            {
                for (int j = 0; j < legCmtas.Count; j++)
                {
                    legCmtasGroup.Next().Value = (byte)legCmtas[j];
                }
            }

            var legTraders = leg.ContraTraders;
            var legTradersGroup = legMessage.NoLegContraTradersCount(legTraders?.Count ?? 0);
            if (legTraders != null)
            {
                for (int j = 0; j < legTraders.Count; j++)
                {
                    legTradersGroup.Next().Value = (byte)legTraders[j];
                }
            }
        }

        private static void EncodeOrderContraFields_HerculesEcho(HerculesEchoMessage message, IOrder order)
        {
            var orderCaps = order.ContraCapacities;
            var orderCapsGroup = message.NoContraCapacitiesCount(orderCaps?.Count ?? 0);
            if (orderCaps != null)
            {
                for (int i = 0; i < orderCaps.Count; i++)
                {
                    orderCapsGroup.Next().Value = (byte)orderCaps[i];
                }
            }

            var orderBrokers = order.ContraBrokerNames;
            var orderBrokersGroup = message.NoContraBrokerNamesCount(orderBrokers?.Count ?? 0);
            if (orderBrokers != null)
            {
                for (int i = 0; i < orderBrokers.Count; i++)
                {
                    orderBrokersGroup.Next().Value = (byte)orderBrokers[i];
                }
            }

            var orderCmtas = order.ContraCmtas;
            var orderCmtasGroup = message.NoContraCmtasCount(orderCmtas?.Count ?? 0);
            if (orderCmtas != null)
            {
                for (int i = 0; i < orderCmtas.Count; i++)
                {
                    orderCmtasGroup.Next().Value = (byte)orderCmtas[i];
                }
            }

            var orderTraders = order.ContraTraders;
            var orderTradersGroup = message.NoContraTradersCount(orderTraders?.Count ?? 0);
            if (orderTraders != null)
            {
                for (int i = 0; i < orderTraders.Count; i++)
                {
                    orderTradersGroup.Next().Value = (byte)orderTraders[i];
                }
            }
        }

        private static void EncodeDoubleNull2(DOUBLENULL2 doubleNull, double update)
        {
            doubleNull.Mantissa = EncodeMantissa(update, doubleNull.Exponent, DOUBLENULL2.MantissaNullValue);
        }

        private static void EncodeDoubleNull4(DOUBLENULL4 doubleNull, double update)
        {
            doubleNull.Mantissa = EncodeMantissa(update, doubleNull.Exponent, DOUBLENULL4.MantissaNullValue);
        }

        private static void EncodePriceNull3(PRICENULL3 priceNull3, double update)
        {
            priceNull3.Mantissa = EncodeMantissa(update, priceNull3.Exponent, PRICENULL3.MantissaNullValue);
        }

        private static int EncodeMantissa(double value, sbyte exp, int nullValue = PRICENULL3.MantissaNullValue)
        {
            if (double.IsNaN(value))
            {
                return nullValue;
            }
            else
            {
                return (int)(Round(Round(value, Abs(exp)) / Pow(10, exp)));
            }
        }

        private static string TruncateForSbe(string? value, int maxBytes)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (System.Text.Encoding.UTF8.GetByteCount(value) < maxBytes) return value;
            while (value.Length > 0 && System.Text.Encoding.UTF8.GetByteCount(value) >= maxBytes)
                value = value[..^1];
            return value;
        }

        public int EncodeLiveVolDataRequestMessage(DirectBuffer directBuffer, int offset, int requestId, bool getLatest, string? symbol, DateTime startTime, DateTime endTime)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = LiveVolRequestMessage.BlockLength;
            messageHeader.SchemaId = LiveVolRequestMessage.SchemaId;
            messageHeader.TemplateId = LiveVolRequestMessage.TemplateId;
            messageHeader.Version = LiveVolRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            LiveVolRequestMessage message = new LiveVolRequestMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.GetLatest = getLatest ? BooleanEnum.True : BooleanEnum.False;
            message.StartTimestamp = ToUnixEpoch(startTime);
            message.EndTimestamp = ToUnixEpoch(endTime);
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeLiveVolDataResponseMessage(DirectBuffer directBuffer, int offset, int requestId, List<LiveVolDataModel> results)
        {
            int count = results.Count;
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            LiveVolResponseMessage message = new LiveVolResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = LiveVolResponseMessage.BlockLength;
            messageHeader.SchemaId = LiveVolResponseMessage.SchemaId;
            messageHeader.TemplateId = LiveVolResponseMessage.TemplateId;
            messageHeader.Version = LiveVolResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = requestId;
            message.Count = count;

            LiveVolResponseMessage.LiveVolDataGroup liveVolDataGroup = message.LiveVolDataCount(count);
            for (int index = 0; index < count; index++)
            {
                LiveVolDataModel liveVolRow = results[index];
                liveVolDataGroup.Next();
                liveVolDataGroup.UploadTimestamp = (ulong)liveVolRow.UploadTimestamp.ToUnixTimeMilliseconds();
                EncodeDoubleNull2(liveVolDataGroup.Week52High, liveVolRow.Week52High ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Week52Low, liveVolRow.Week52Low ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.ClosePrice, liveVolRow.ClosePrice ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.HighPrice, liveVolRow.HighPrice ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.LastPrice, liveVolRow.LastPrice ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.LowPrice, liveVolRow.LowPrice ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.OpenPrice, liveVolRow.OpenPrice ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentChangeFromClose, liveVolRow.PercentChangeFromClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentChangeFromOpen, liveVolRow.PercentChangeFromOpen ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PriceChangeFromClose, liveVolRow.PriceChangeFromClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PriceChangeFromOpen, liveVolRow.PriceChangeFromOpen ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PricePercentOf52WeekRange, liveVolRow.PricePercentOf52WeekRange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PricePercentileRank, liveVolRow.PricePercentileRank ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.SdChangeFromClose, liveVolRow.SdChangeFromClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AverageIv30, liveVolRow.AverageIv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1Iv, liveVolRow.Expiry1Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1Iv1DayClose, liveVolRow.Expiry1Iv1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1Iv1WeekClose, liveVolRow.Expiry1Iv1WeekClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1IvChange, liveVolRow.Expiry1IvChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1IvPercentageChange, liveVolRow.Expiry1IvPercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2Iv, liveVolRow.Expiry2Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2Iv1DayClose, liveVolRow.Expiry2Iv1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2Iv1WeekClose, liveVolRow.Expiry2Iv1WeekClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2IvChange, liveVolRow.Expiry2IvChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2IvPercentageChange, liveVolRow.Expiry2IvPercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry3Iv, liveVolRow.Expiry3Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry3IvChange, liveVolRow.Expiry3IvChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry3IvPercentageChange, liveVolRow.Expiry3IvPercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry4Iv, liveVolRow.Expiry4Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry5Iv, liveVolRow.Expiry5Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry6Iv, liveVolRow.Expiry6Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry7Iv, liveVolRow.Expiry7Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry8Iv, liveVolRow.Expiry8Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv10, liveVolRow.Hv10 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv180, liveVolRow.Hv180 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv20, liveVolRow.Hv20 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30, liveVolRow.Hv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30_1DayClose, liveVolRow.Hv30_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30_3DayClose, liveVolRow.Hv30_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30_5DayClose, liveVolRow.Hv30_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30PercentOf52WeekRange, liveVolRow.Hv30PercentOf52WeekRange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30PercentileRank, liveVolRow.Hv30PercentileRank ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv30WeekAgo, liveVolRow.Hv30WeekAgo ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv360, liveVolRow.Hv360 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv60, liveVolRow.Hv60 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv60_1DayClose, liveVolRow.Hv60_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv60_3DayClose, liveVolRow.Hv60_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv60_5DayClose, liveVolRow.Hv60_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv90, liveVolRow.Hv90 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv90_1DayClose, liveVolRow.Hv90_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv90_3DayClose, liveVolRow.Hv90_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv90_5DayClose, liveVolRow.Hv90_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv180, liveVolRow.Iv180 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv180_1DayClose, liveVolRow.Iv180_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30, liveVolRow.Iv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_1DayClose, liveVolRow.Iv30_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_1MonthClose, liveVolRow.Iv30_1MonthClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_1WeekClose, liveVolRow.Iv30_1WeekClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_3DayChange, liveVolRow.Iv30_3DayChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_3DayClose, liveVolRow.Iv30_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_5DayChange, liveVolRow.Iv30_5DayChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_5DayClose, liveVolRow.Iv30_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_52WeekHigh, liveVolRow.Iv30_52WeekHigh ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30_52WeekLow, liveVolRow.Iv30_52WeekLow ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30Change, liveVolRow.Iv30Change ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30Open, liveVolRow.Iv30Open ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30PercentOf52WeekRange, liveVolRow.Iv30PercentOf52WeekRange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30PercentageChange, liveVolRow.Iv30PercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30PercentileRank, liveVolRow.Iv30PercentileRank ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv360, liveVolRow.Iv360 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv360_1DayClose, liveVolRow.Iv360_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60, liveVolRow.Iv60 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60_1DayClose, liveVolRow.Iv60_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60_3DayClose, liveVolRow.Iv60_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60_5DayChange, liveVolRow.Iv60_5DayChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60_5DayClose, liveVolRow.Iv60_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60Change, liveVolRow.Iv60Change ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60PercentOf52WeekRange, liveVolRow.Iv60PercentOf52WeekRange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60PercentageChange, liveVolRow.Iv60PercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60PercentileRank, liveVolRow.Iv60PercentileRank ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90, liveVolRow.Iv90 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90_1DayClose, liveVolRow.Iv90_1DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90_3DayClose, liveVolRow.Iv90_3DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90_5DayChange, liveVolRow.Iv90_5DayChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90_5DayClose, liveVolRow.Iv90_5DayClose ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90Change, liveVolRow.Iv90Change ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90PercentOf52WeekRange, liveVolRow.Iv90PercentOf52WeekRange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90PercentageChange, liveVolRow.Iv90PercentageChange ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90PercentileRank, liveVolRow.Iv90PercentileRank ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.OneDayStandardDeviation, liveVolRow.OneDayStandardDeviation ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfAverageIv30, liveVolRow.PercentOfAverageIv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1IvVsExpiry2Iv, liveVolRow.Expiry1IvVsExpiry2Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1IvVsExpiry3Iv, liveVolRow.Expiry1IvVsExpiry3Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1IvVsExpiry4Iv, liveVolRow.Expiry1IvVsExpiry4Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1VsExpiry2VolRatio, liveVolRow.Expiry1VsExpiry2VolRatio ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry1VsExpiry3VolRatio, liveVolRow.Expiry1VsExpiry3VolRatio ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2IvVsExpiry3Iv, liveVolRow.Expiry2IvVsExpiry3Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2IvVsExpiry4Iv, liveVolRow.Expiry2IvVsExpiry4Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry2VsExpiry3IvRatio, liveVolRow.Expiry2VsExpiry3IvRatio ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry3IvVsExpiry4Iv, liveVolRow.Expiry3IvVsExpiry4Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry4IvVsExpiry5Iv, liveVolRow.Expiry4IvVsExpiry5Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry5IvVsExpiry6Iv, liveVolRow.Expiry5IvVsExpiry6Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry6IvVsExpiry7Iv, liveVolRow.Expiry6IvVsExpiry7Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Expiry7IvVsExpiry8Iv, liveVolRow.Expiry7IvVsExpiry8Iv ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv180VsIv30, liveVolRow.Hv180VsIv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Hv180VsIv60, liveVolRow.Hv180VsIv60 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30Hv30Ratio, liveVolRow.Iv30Hv30Ratio ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30VsHv10, liveVolRow.Iv30VsHv10 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30VsHv20, liveVolRow.Iv30VsHv20 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30VsHv30, liveVolRow.Iv30VsHv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30VsIv60, liveVolRow.Iv30VsIv60 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv30VsIv90, liveVolRow.Iv30VsIv90 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv360VsHv360, liveVolRow.Iv360VsHv360 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60Hv60Ratio, liveVolRow.Iv60Hv60Ratio ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60VsHv10, liveVolRow.Iv60VsHv10 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60VsHv20, liveVolRow.Iv60VsHv20 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60VsHv30, liveVolRow.Iv60VsHv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60VsHv60, liveVolRow.Iv60VsHv60 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv60VsIv90, liveVolRow.Iv60VsIv90 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90VsHv10, liveVolRow.Iv90VsHv10 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90VsHv20, liveVolRow.Iv90VsHv20 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90VsHv30, liveVolRow.Iv90VsHv30 ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.Iv90VsHv90, liveVolRow.Iv90VsHv90 ?? double.NaN);
                liveVolDataGroup.AverageUnderlyingVolume = liveVolRow.AverageUnderlyingVolume.HasValue ? (ulong)liveVolRow.AverageUnderlyingVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageUnderlyingVolumeNullValue;
                liveVolDataGroup.PercentAverageUnderlyingVolume = liveVolRow.PercentAverageUnderlyingVolume.HasValue ? (ulong)liveVolRow.PercentAverageUnderlyingVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentAverageUnderlyingVolumeNullValue;
                liveVolDataGroup.UnderlyingVolume = liveVolRow.UnderlyingVolume.HasValue ? (ulong)liveVolRow.UnderlyingVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.UnderlyingVolumeNullValue;
                EncodeDoubleNull2(liveVolDataGroup.Vwap, liveVolRow.Vwap ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AverageCallDelta, liveVolRow.AverageCallDelta ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AverageCallGamma, liveVolRow.AverageCallGamma ?? double.NaN);
                liveVolDataGroup.AverageCallOpenInterest = liveVolRow.AverageCallOpenInterest.HasValue ? (ulong)liveVolRow.AverageCallOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageCallOpenInterestNullValue;
                EncodeDoubleNull2(liveVolDataGroup.AverageCallPremium, liveVolRow.AverageCallPremium ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AverageCallVega, liveVolRow.AverageCallVega ?? double.NaN);
                liveVolDataGroup.AverageCallVolume = liveVolRow.AverageCallVolume.HasValue ? (ulong)liveVolRow.AverageCallVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageCallVolumeNullValue;
                liveVolDataGroup.AverageCallsBetweenBidAsk = liveVolRow.AverageCallsBetweenBidAsk.HasValue ? (ulong)liveVolRow.AverageCallsBetweenBidAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageCallsBetweenBidAskNullValue;
                liveVolDataGroup.AverageCallsOnAsk = liveVolRow.AverageCallsOnAsk.HasValue ? (ulong)liveVolRow.AverageCallsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageCallsOnAskNullValue;
                liveVolDataGroup.AverageCallsOnBid = liveVolRow.AverageCallsOnBid.HasValue ? (ulong)liveVolRow.AverageCallsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageCallsOnBidNullValue;
                liveVolDataGroup.AverageOpenInterest = liveVolRow.AverageOpenInterest.HasValue ? (ulong)liveVolRow.AverageOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOpenInterestNullValue;
                liveVolDataGroup.AverageOptionVolume = liveVolRow.AverageOptionVolume.HasValue ? (ulong)liveVolRow.AverageOptionVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOptionVolumeNullValue;
                liveVolDataGroup.AverageOtmCallsOnAsk = liveVolRow.AverageOtmCallsOnAsk.HasValue ? (ulong)liveVolRow.AverageOtmCallsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOtmCallsOnAskNullValue;
                liveVolDataGroup.AverageOtmCallsOnBid = liveVolRow.AverageOtmCallsOnBid.HasValue ? (ulong)liveVolRow.AverageOtmCallsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOtmCallsOnBidNullValue;
                liveVolDataGroup.AverageOtmPutsOnAsk = liveVolRow.AverageOtmPutsOnAsk.HasValue ? (ulong)liveVolRow.AverageOtmPutsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOtmPutsOnAskNullValue;
                liveVolDataGroup.AverageOtmPutsOnBid = liveVolRow.AverageOtmPutsOnBid.HasValue ? (ulong)liveVolRow.AverageOtmPutsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.AverageOtmPutsOnBidNullValue;
                EncodeDoubleNull2(liveVolDataGroup.AveragePutDelta, liveVolRow.AveragePutDelta ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AveragePutGamma, liveVolRow.AveragePutGamma ?? double.NaN);
                liveVolDataGroup.AveragePutOpenInterest = liveVolRow.AveragePutOpenInterest.HasValue ? (ulong)liveVolRow.AveragePutOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.AveragePutOpenInterestNullValue;
                EncodeDoubleNull2(liveVolDataGroup.AveragePutPremium, liveVolRow.AveragePutPremium ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AveragePutVega, liveVolRow.AveragePutVega ?? double.NaN);
                liveVolDataGroup.AveragePutVolume = liveVolRow.AveragePutVolume.HasValue ? (ulong)liveVolRow.AveragePutVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.AveragePutVolumeNullValue;
                liveVolDataGroup.AveragePutsBetweenBidAsk = liveVolRow.AveragePutsBetweenBidAsk.HasValue ? (ulong)liveVolRow.AveragePutsBetweenBidAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AveragePutsBetweenBidAskNullValue;
                liveVolDataGroup.AveragePutsOnAsk = liveVolRow.AveragePutsOnAsk.HasValue ? (ulong)liveVolRow.AveragePutsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.AveragePutsOnAskNullValue;
                liveVolDataGroup.AveragePutsOnBid = liveVolRow.AveragePutsOnBid.HasValue ? (ulong)liveVolRow.AveragePutsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.AveragePutsOnBidNullValue;
                EncodeDoubleNull2(liveVolDataGroup.AverageTradeSize, liveVolRow.AverageTradeSize ?? double.NaN);
                liveVolDataGroup.CallOpenInterest = liveVolRow.CallOpenInterest.HasValue ? (ulong)liveVolRow.CallOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.CallOpenInterestNullValue;
                liveVolDataGroup.CallOpenInterest1DayAgo = liveVolRow.CallOpenInterest1DayAgo.HasValue ? (ulong)liveVolRow.CallOpenInterest1DayAgo.Value : LiveVolResponseMessage.LiveVolDataGroup.CallOpenInterest1DayAgoNullValue;
                EncodeDoubleNull2(liveVolDataGroup.CallOpenInterest1DayChangePercent, liveVolRow.CallOpenInterest1DayChangePercent ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CallPremium, liveVolRow.CallPremium ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CallPutRatio, liveVolRow.CallPutRatio ?? double.NaN);
                liveVolDataGroup.CallTradeCount = liveVolRow.CallTradeCount.HasValue ? (ulong)liveVolRow.CallTradeCount.Value : LiveVolResponseMessage.LiveVolDataGroup.CallTradeCountNullValue;
                liveVolDataGroup.CallVolume = liveVolRow.CallVolume.HasValue ? (ulong)liveVolRow.CallVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.CallVolumeNullValue;
                liveVolDataGroup.CallVolume1DayAgo = liveVolRow.CallVolume1DayAgo.HasValue ? (ulong)liveVolRow.CallVolume1DayAgo.Value : LiveVolResponseMessage.LiveVolDataGroup.CallVolume1DayAgoNullValue;
                EncodeDoubleNull2(liveVolDataGroup.CallVolumePercentOfCallOpenInterest, liveVolRow.CallVolumePercentOfCallOpenInterest ?? double.NaN);
                liveVolDataGroup.CallsBetweenBidAndAsk = liveVolRow.CallsBetweenBidAndAsk.HasValue ? (ulong)liveVolRow.CallsBetweenBidAndAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.CallsBetweenBidAndAskNullValue;
                liveVolDataGroup.CallsOnAsk = liveVolRow.CallsOnAsk.HasValue ? (ulong)liveVolRow.CallsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.CallsOnAskNullValue;
                liveVolDataGroup.CallsOnBid = liveVolRow.CallsOnBid.HasValue ? (ulong)liveVolRow.CallsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.CallsOnBidNullValue;
                EncodeDoubleNull2(liveVolDataGroup.CumulativeCallDelta, liveVolRow.CumulativeCallDelta ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CumulativeCallGamma, liveVolRow.CumulativeCallGamma ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CumulativeCallVega, liveVolRow.CumulativeCallVega ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CumulativePutDelta, liveVolRow.CumulativePutDelta ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CumulativePutGamma, liveVolRow.CumulativePutGamma ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.CumulativePutVega, liveVolRow.CumulativePutVega ?? double.NaN);
                liveVolDataGroup.OptionVolume = liveVolRow.OptionVolume.HasValue ? (ulong)liveVolRow.OptionVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.OptionVolumeNullValue;
                EncodeDoubleNull2(liveVolDataGroup.OptionVolumePercentOfOptionOpenInterest, liveVolRow.OptionVolumePercentOfOptionOpenInterest ?? double.NaN);
                liveVolDataGroup.OtmCallsOnAsk = liveVolRow.OtmCallsOnAsk.HasValue ? (ulong)liveVolRow.OtmCallsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.OtmCallsOnAskNullValue;
                liveVolDataGroup.OtmCallsOnBid = liveVolRow.OtmCallsOnBid.HasValue ? (ulong)liveVolRow.OtmCallsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.OtmCallsOnBidNullValue;
                liveVolDataGroup.OtmPutsOnAsk = liveVolRow.OtmPutsOnAsk.HasValue ? (ulong)liveVolRow.OtmPutsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.OtmPutsOnAskNullValue;
                liveVolDataGroup.OtmPutsOnBid = liveVolRow.OtmPutsOnBid.HasValue ? (ulong)liveVolRow.OtmPutsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.OtmPutsOnBidNullValue;
                liveVolDataGroup.PartialDayAverageCallVolume = liveVolRow.PartialDayAverageCallVolume.HasValue ? (ulong)liveVolRow.PartialDayAverageCallVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageCallVolumeNullValue;
                liveVolDataGroup.PartialDayAverageOptionVolume = liveVolRow.PartialDayAverageOptionVolume.HasValue ? (ulong)liveVolRow.PartialDayAverageOptionVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageOptionVolumeNullValue;
                liveVolDataGroup.PartialDayAveragePutVolume = liveVolRow.PartialDayAveragePutVolume.HasValue ? (ulong)liveVolRow.PartialDayAveragePutVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PartialDayAveragePutVolumeNullValue;
                liveVolDataGroup.PartialDayAverageUnderlyingVolume = liveVolRow.PartialDayAverageUnderlyingVolume.HasValue ? (ulong)liveVolRow.PartialDayAverageUnderlyingVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PartialDayAverageUnderlyingVolumeNullValue;
                liveVolDataGroup.PercentOfAverageCallVolume = liveVolRow.PercentOfAverageCallVolume.HasValue ? (ulong)liveVolRow.PercentOfAverageCallVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfAverageCallVolumeNullValue;
                liveVolDataGroup.PercentOfAveragePutVolume = liveVolRow.PercentOfAveragePutVolume.HasValue ? (ulong)liveVolRow.PercentOfAveragePutVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfAveragePutVolumeNullValue;
                liveVolDataGroup.PercentOfAverageVolume = liveVolRow.PercentOfAverageVolume.HasValue ? (ulong)liveVolRow.PercentOfAverageVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfAverageVolumeNullValue;
                EncodeDoubleNull2(liveVolDataGroup.PercentOfCallsOnAsk, liveVolRow.PercentOfCallsOnAsk ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfCallsOnBid, liveVolRow.PercentOfCallsOnBid ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfOtmCallsOnAsk, liveVolRow.PercentOfOtmCallsOnAsk ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfOtmCallsOnBid, liveVolRow.PercentOfOtmCallsOnBid ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfOtmPutsOnAsk, liveVolRow.PercentOfOtmPutsOnAsk ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfOtmPutsOnBid, liveVolRow.PercentOfOtmPutsOnBid ?? double.NaN);
                liveVolDataGroup.PercentOfPartialDayAverageCallVolume = liveVolRow.PercentOfPartialDayAverageCallVolume.HasValue ? (ulong)liveVolRow.PercentOfPartialDayAverageCallVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageCallVolumeNullValue;
                liveVolDataGroup.PercentOfPartialDayAverageOptionVolume = liveVolRow.PercentOfPartialDayAverageOptionVolume.HasValue ? (ulong)liveVolRow.PercentOfPartialDayAverageOptionVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageOptionVolumeNullValue;
                liveVolDataGroup.PercentOfPartialDayAveragePutVolume = liveVolRow.PercentOfPartialDayAveragePutVolume.HasValue ? (ulong)liveVolRow.PercentOfPartialDayAveragePutVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAveragePutVolumeNullValue;
                liveVolDataGroup.PercentOfPartialDayAverageUnderlyingVolume = liveVolRow.PercentOfPartialDayAverageUnderlyingVolume.HasValue ? (ulong)liveVolRow.PercentOfPartialDayAverageUnderlyingVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PercentOfPartialDayAverageUnderlyingVolumeNullValue;
                EncodeDoubleNull2(liveVolDataGroup.PercentOfPutsOnAsk, liveVolRow.PercentOfPutsOnAsk ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PercentOfPutsOnBid, liveVolRow.PercentOfPutsOnBid ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PutCallRatio, liveVolRow.PutCallRatio ?? double.NaN);
                liveVolDataGroup.PutOpenInterest = liveVolRow.PutOpenInterest.HasValue ? (ulong)liveVolRow.PutOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.PutOpenInterestNullValue;
                liveVolDataGroup.PutOpenInterest1DayAgo = liveVolRow.PutOpenInterest1DayAgo.HasValue ? (ulong)liveVolRow.PutOpenInterest1DayAgo.Value : LiveVolResponseMessage.LiveVolDataGroup.PutOpenInterest1DayAgoNullValue;
                EncodeDoubleNull2(liveVolDataGroup.PutOpenInterest1DayChangePercent, liveVolRow.PutOpenInterest1DayChangePercent ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.PutPremium, liveVolRow.PutPremium ?? double.NaN);
                liveVolDataGroup.PutTradeCount = liveVolRow.PutTradeCount.HasValue ? (ulong)liveVolRow.PutTradeCount.Value : LiveVolResponseMessage.LiveVolDataGroup.PutTradeCountNullValue;
                liveVolDataGroup.PutVolume = liveVolRow.PutVolume.HasValue ? (ulong)liveVolRow.PutVolume.Value : LiveVolResponseMessage.LiveVolDataGroup.PutVolumeNullValue;
                liveVolDataGroup.PutVolume1DayAgo = liveVolRow.PutVolume1DayAgo.HasValue ? (ulong)liveVolRow.PutVolume1DayAgo.Value : LiveVolResponseMessage.LiveVolDataGroup.PutVolume1DayAgoNullValue;
                EncodeDoubleNull2(liveVolDataGroup.PutVolumePercentOfPutOpenInterest, liveVolRow.PutVolumePercentOfPutOpenInterest ?? double.NaN);
                liveVolDataGroup.PutsBetweenBidAndAsk = liveVolRow.PutsBetweenBidAndAsk.HasValue ? (ulong)liveVolRow.PutsBetweenBidAndAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.PutsBetweenBidAndAskNullValue;
                liveVolDataGroup.PutsOnAsk = liveVolRow.PutsOnAsk.HasValue ? (ulong)liveVolRow.PutsOnAsk.Value : LiveVolResponseMessage.LiveVolDataGroup.PutsOnAskNullValue;
                liveVolDataGroup.PutsOnBid = liveVolRow.PutsOnBid.HasValue ? (ulong)liveVolRow.PutsOnBid.Value : LiveVolResponseMessage.LiveVolDataGroup.PutsOnBidNullValue;
                liveVolDataGroup.SumCallVolume3Day = liveVolRow.SumCallVolume3Day.HasValue ? (ulong)liveVolRow.SumCallVolume3Day.Value : LiveVolResponseMessage.LiveVolDataGroup.SumCallVolume3DayNullValue;
                liveVolDataGroup.SumCallVolume5Day = liveVolRow.SumCallVolume5Day.HasValue ? (ulong)liveVolRow.SumCallVolume5Day.Value : LiveVolResponseMessage.LiveVolDataGroup.SumCallVolume5DayNullValue;
                liveVolDataGroup.SumCallVolumeLast2D = liveVolRow.SumCallVolumeLast2D.HasValue ? (ulong)liveVolRow.SumCallVolumeLast2D.Value : LiveVolResponseMessage.LiveVolDataGroup.SumCallVolumeLast2DNullValue;
                liveVolDataGroup.SumCallVolumeLast4D = liveVolRow.SumCallVolumeLast4D.HasValue ? (ulong)liveVolRow.SumCallVolumeLast4D.Value : LiveVolResponseMessage.LiveVolDataGroup.SumCallVolumeLast4DNullValue;
                liveVolDataGroup.SumPutVolume3Day = liveVolRow.SumPutVolume3Day.HasValue ? (ulong)liveVolRow.SumPutVolume3Day.Value : LiveVolResponseMessage.LiveVolDataGroup.SumPutVolume3DayNullValue;
                liveVolDataGroup.SumPutVolume5Day = liveVolRow.SumPutVolume5Day.HasValue ? (ulong)liveVolRow.SumPutVolume5Day.Value : LiveVolResponseMessage.LiveVolDataGroup.SumPutVolume5DayNullValue;
                liveVolDataGroup.SumPutVolumeLast2D = liveVolRow.SumPutVolumeLast2D.HasValue ? (ulong)liveVolRow.SumPutVolumeLast2D.Value : LiveVolResponseMessage.LiveVolDataGroup.SumPutVolumeLast2DNullValue;
                liveVolDataGroup.SumPutVolumeLast4D = liveVolRow.SumPutVolumeLast4D.HasValue ? (ulong)liveVolRow.SumPutVolumeLast4D.Value : LiveVolResponseMessage.LiveVolDataGroup.SumPutVolumeLast4DNullValue;
                liveVolDataGroup.TotalOpenInterest = liveVolRow.TotalOpenInterest.HasValue ? (ulong)liveVolRow.TotalOpenInterest.Value : LiveVolResponseMessage.LiveVolDataGroup.TotalOpenInterestNullValue;
                EncodeDoubleNull2(liveVolDataGroup.TotalOpenInterest1DayChangePercent, liveVolRow.TotalOpenInterest1DayChangePercent ?? double.NaN);
                liveVolDataGroup.TotalOptionTradesOnTheDay = liveVolRow.TotalOptionTradesOnTheDay.HasValue ? (ulong)liveVolRow.TotalOptionTradesOnTheDay.Value : LiveVolResponseMessage.LiveVolDataGroup.TotalOptionTradesOnTheDayNullValue;
                liveVolDataGroup.MarketCapitalization = liveVolRow.MarketCapitalization.HasValue ? (ulong)liveVolRow.MarketCapitalization.Value : LiveVolResponseMessage.LiveVolDataGroup.MarketCapitalizationNullValue;
                EncodeDoubleNull2(liveVolDataGroup.PriceToEarningsRatio, liveVolRow.PriceToEarningsRatio ?? double.NaN);
                liveVolDataGroup.SharesOutstanding = liveVolRow.SharesOutstanding.HasValue ? (ulong)liveVolRow.SharesOutstanding.Value : LiveVolResponseMessage.LiveVolDataGroup.SharesOutstandingNullValue;
                EncodeDoubleNull2(liveVolDataGroup.AverageHistoricalEarningsMove, liveVolRow.AverageHistoricalEarningsMove ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.AverageImpliedEarningsMove, liveVolRow.AverageImpliedEarningsMove ?? double.NaN);
                liveVolDataGroup.DaysAfterEarnings = liveVolRow.DaysAfterEarnings.HasValue ? (ulong)liveVolRow.DaysAfterEarnings.Value : LiveVolResponseMessage.LiveVolDataGroup.DaysAfterEarningsNullValue;
                liveVolDataGroup.DaysToNextEarningsDate = liveVolRow.DaysToNextEarningsDate.HasValue ? (ulong)liveVolRow.DaysToNextEarningsDate.Value : LiveVolResponseMessage.LiveVolDataGroup.DaysToNextEarningsDateNullValue;
                liveVolDataGroup.DaysUntilNextDividendDate = liveVolRow.DaysUntilNextDividendDate.HasValue ? (ulong)liveVolRow.DaysUntilNextDividendDate.Value : LiveVolResponseMessage.LiveVolDataGroup.DaysUntilNextDividendDateNullValue;
                EncodeDoubleNull2(liveVolDataGroup.ForwardVolatility, liveVolRow.ForwardVolatility ?? double.NaN);
                EncodeDoubleNull2(liveVolDataGroup.ImpliedEarningsMove, liveVolRow.ImpliedEarningsMove ?? double.NaN);
                liveVolDataGroup.LastEarningsDate = liveVolRow.LastEarningsDate.HasValue ? ToUnixEpoch(liveVolRow.LastEarningsDate.Value) : LiveVolResponseMessage.LiveVolDataGroup.LastEarningsDateNullValue;
                EncodeDoubleNull2(liveVolDataGroup.NextDividendAmount, liveVolRow.NextDividendAmount ?? double.NaN);
                liveVolDataGroup.NextDividendDate = liveVolRow.NextDividendDate.HasValue ? ToUnixEpoch(liveVolRow.NextDividendDate.Value) : LiveVolResponseMessage.LiveVolDataGroup.NextDividendDateNullValue;
                liveVolDataGroup.SetSymbol(liveVolRow.Symbol ?? "");
                liveVolDataGroup.SetCompanyName(liveVolRow.CompanyName ?? "");
                liveVolDataGroup.SetIndustry(liveVolRow.Industry ?? "");
                liveVolDataGroup.SetSector(liveVolRow.Sector ?? "");
                liveVolDataGroup.SetLastEarningsTimeOfDay(liveVolRow.LastEarningsTimeOfDay ?? "");
                liveVolDataGroup.SetNextEarningsStatus(liveVolRow.NextEarningsStatus ?? "");
                liveVolDataGroup.SetNextEarningsTime(liveVolRow.NextEarningsTime ?? "");
            }

            return message.Limit - offset;
        }

        public int EncodeRbboUpdate(DirectBuffer directBuffer, int offset, RbboUpdateModel updateModel)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RbboUpdateMessage.BlockLength;
            messageHeader.SchemaId = RbboUpdateMessage.SchemaId;
            messageHeader.TemplateId = RbboUpdateMessage.TemplateId;
            messageHeader.Version = RbboUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            RbboUpdateMessage message = new RbboUpdateMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.TickerId = updateModel.SymbolIndex;
            message.DateTime = (ulong)DateTime.UtcNow.Ticks;
            message.KnownMcids = updateModel.KnownMcids;
            message.ChangedMcids = updateModel.ChangedMcids;

            var slotsGroup = message.SlotsCount(updateModel.SlotCount);
            for (int i = 0; i < updateModel.SlotCount; i++)
            {
                slotsGroup.Next();
                slotsGroup.Mcid = updateModel.Slots[i].Mcid;
                EncodeDoubleNull2(slotsGroup.BidPrice, updateModel.Slots[i].BidPrice);
                slotsGroup.BidQty = updateModel.Slots[i].BidQty;
                EncodeDoubleNull2(slotsGroup.AskPrice, updateModel.Slots[i].AskPrice);
                slotsGroup.AskQty = updateModel.Slots[i].AskQty;
                slotsGroup.Flags = updateModel.Slots[i].Flags;
            }

            return message.Limit - offset;
        }

        public int EncodeSymbolIndexMapping(DirectBuffer directBuffer, int offset, int tickerId, string symbol, SubscriptionFieldType subscriptionType)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            SymbolIndexMappingMessage message = new SymbolIndexMappingMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = SymbolIndexMappingMessage.BlockLength;
            messageHeader.SchemaId = SymbolIndexMappingMessage.SchemaId;
            messageHeader.TemplateId = SymbolIndexMappingMessage.TemplateId;
            messageHeader.Version = SymbolIndexMappingMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.TickerId = tickerId;
            message.SubscriptionType = (int)subscriptionType;
            message.SetSymbol(symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeRegisterForeignUpdateRoute(DirectBuffer directBuffer, int offset, RegisterForeignUpdateRouteRequest model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            RegisterForeignUpdateRouteMessage message = new RegisterForeignUpdateRouteMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = RegisterForeignUpdateRouteMessage.BlockLength;
            messageHeader.SchemaId = RegisterForeignUpdateRouteMessage.SchemaId;
            messageHeader.TemplateId = RegisterForeignUpdateRouteMessage.TemplateId;
            messageHeader.Version = RegisterForeignUpdateRouteMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.OrderSource = (ushort)model.Rule.Key.OrderSource;
            message.OrderSubType = (ushort)model.Rule.Key.SubType;
            message.SetDestination(model.Rule.Key.Destination ?? "");
            message.SetProfileId(model.Rule.ProfileId);
            message.SetProfileName(model.Rule.ProfileName);

            return message.Limit - offset;
        }

        public int EncodeUnregisterForeignUpdateRoute(DirectBuffer directBuffer, int offset, UnregisterForeignUpdateRouteRequest model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            UnregisterForeignUpdateRouteMessage message = new UnregisterForeignUpdateRouteMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = UnregisterForeignUpdateRouteMessage.BlockLength;
            messageHeader.SchemaId = UnregisterForeignUpdateRouteMessage.SchemaId;
            messageHeader.TemplateId = UnregisterForeignUpdateRouteMessage.TemplateId;
            messageHeader.Version = UnregisterForeignUpdateRouteMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            message.OrderSource = (ushort)model.Key.OrderSource;
            message.OrderSubType = (ushort)model.Key.SubType;
            message.SetDestination(model.Key.Destination ?? "");

            return message.Limit - offset;
        }

        public int EncodeReplaceForeignUpdateRoutes(DirectBuffer directBuffer, int offset, ReplaceForeignUpdateRoutesRequest model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ReplaceForeignUpdateRoutesMessage message = new ReplaceForeignUpdateRoutesMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ReplaceForeignUpdateRoutesMessage.BlockLength;
            messageHeader.SchemaId = ReplaceForeignUpdateRoutesMessage.SchemaId;
            messageHeader.TemplateId = ReplaceForeignUpdateRoutesMessage.TemplateId;
            messageHeader.Version = ReplaceForeignUpdateRoutesMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            var rulesGroup = message.RulesCount(model.Rules.Length);
            for (int i = 0; i < rulesGroup.Count; i++)
            {
                rulesGroup.Next();
                rulesGroup.OrderSource = (ushort)model.Rules[i].Key.OrderSource;
                rulesGroup.OrderSubType = (ushort)model.Rules[i].Key.SubType;
                rulesGroup.SetDestination(model.Rules[i].Key.Destination ?? "");
                rulesGroup.SetProfileId(model.Rules[i].ProfileId);
                rulesGroup.SetProfileName(model.Rules[i].ProfileName);
            }

            return message.Limit - offset;
        }

        public int EncodeListForeignUpdateRoutes(DirectBuffer directBuffer, int offset, ForeignUpdateRoutesResponse model)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            ForeignUpdateRoutesMessage message = new ForeignUpdateRoutesMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = ForeignUpdateRoutesMessage.BlockLength;
            messageHeader.SchemaId = ForeignUpdateRoutesMessage.SchemaId;
            messageHeader.TemplateId = ForeignUpdateRoutesMessage.TemplateId;
            messageHeader.Version = ForeignUpdateRoutesMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);
            var rulesGroup = message.RulesCount(model.Rules.Length);
            for (int i = 0; i < rulesGroup.Count; i++)
            {
                rulesGroup.Next();
                rulesGroup.OrderSource = (ushort)model.Rules[i].Key.OrderSource;
                rulesGroup.OrderSubType = (ushort)model.Rules[i].Key.SubType;
                rulesGroup.SetDestination(model.Rules[i].Key.Destination ?? "");
                rulesGroup.SetProfileId(model.Rules[i].ProfileId);
                rulesGroup.SetProfileName(model.Rules[i].ProfileName);
            }

            return message.Limit - offset;
        }

        public int EncodeMassCancelRequestMessage(DirectBuffer directBuffer, int offset, MassCancelRequest massCancelRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            MassCancelRequestMessage message = new MassCancelRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = MassCancelRequestMessage.BlockLength;
            messageHeader.SchemaId = MassCancelRequestMessage.SchemaId;
            messageHeader.TemplateId = MassCancelRequestMessage.TemplateId;
            messageHeader.Version = MassCancelRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            message.Venue = (byte)massCancelRequest.Venue;
            message.Broker = (byte)massCancelRequest.Broker;
            message.CancelType = (byte)massCancelRequest.CancelType;
            message.SetExchange(massCancelRequest.Exchange ?? "");
            message.SetAccount(massCancelRequest.Account ?? "");
            message.SetSymbol(massCancelRequest.Symbol ?? "");

            return message.Limit - offset;
        }

        public int EncodeOpraDatabaseTradesRequestMessage(DirectBuffer directBuffer, int offset, OpraDatabaseTradesRequest opraDatabaseTradesRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OpraDatabaseTradesRequestMessage message = new OpraDatabaseTradesRequestMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OpraDatabaseTradesRequestMessage.BlockLength;
            messageHeader.SchemaId = OpraDatabaseTradesRequestMessage.SchemaId;
            messageHeader.TemplateId = OpraDatabaseTradesRequestMessage.TemplateId;
            messageHeader.Version = OpraDatabaseTradesRequestMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            List<string> symbols = opraDatabaseTradesRequest.Symbols;
            List<string> underlyingSymbols = opraDatabaseTradesRequest.UnderlyingSymbols;
            OpraDatabaseTradesRequestMessage.SymbolsGroup symbolsGroup = message.SymbolsCount(symbols.Count);
            for (int i = 0; i < symbols.Count; i++)
            {
                string symbol = symbols[i];
                symbolsGroup.Next();
                symbolsGroup.SetSymbol(symbol ?? string.Empty);
            }

            OpraDatabaseTradesRequestMessage.UnderlyingSymbolsGroup underlyingSymbolsGroup = message.UnderlyingSymbolsCount(underlyingSymbols.Count);
            for (int i = 0; i < underlyingSymbols.Count; i++)
            {
                string underlyingSymbol = underlyingSymbols[i];
                underlyingSymbolsGroup.Next();
                underlyingSymbolsGroup.SetUnderlyingSymbol(underlyingSymbol ?? string.Empty);
            }

            message.RequestId = (uint)opraDatabaseTradesRequest.RequestId;
            message.RequestSpreads = opraDatabaseTradesRequest.RequestSpreads ? BooleanEnum.True : BooleanEnum.False;
            message.RealTime = opraDatabaseTradesRequest.RealTime ? BooleanEnum.True : BooleanEnum.False;
            message.IsStopRequest = opraDatabaseTradesRequest.Stop ? BooleanEnum.True : BooleanEnum.False;
            message.MatchIoiTrades = opraDatabaseTradesRequest.MatchIoiTrades ? BooleanEnum.True : BooleanEnum.False;
            message.StartTime = ToUnixEpoch(opraDatabaseTradesRequest.StartTime);
            message.EndTime = ToUnixEpoch(opraDatabaseTradesRequest.EndTime);
            message.DeltaAdjEdgeIntervalSeconds = opraDatabaseTradesRequest.DeltaAdjEdgeInterval;
            message.SetConstraint1(opraDatabaseTradesRequest.Constraint1 ?? "");
            message.SetConstraint2(opraDatabaseTradesRequest.Constraint2 ?? "");

            return message.Limit - offset;
        }

        public int EncodeOpraDatabaseTradesResponseMessage(DirectBuffer directBuffer, int offset, OpraDatabaseTradesResponse opraDatabaseTradesResponse)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            OpraDatabaseTradesResponseMessage message = new OpraDatabaseTradesResponseMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = OpraDatabaseTradesResponseMessage.BlockLength;
            messageHeader.SchemaId = OpraDatabaseTradesResponseMessage.SchemaId;
            messageHeader.TemplateId = OpraDatabaseTradesResponseMessage.TemplateId;
            messageHeader.Version = OpraDatabaseTradesResponseMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            message.WrapForEncode(directBuffer, bufferOffset);

            List<OpraDatabaseTradeModel> trades = opraDatabaseTradesResponse.Trades;
            OpraDatabaseTradesResponseMessage.TradesGroup tradesGroup = message.TradesCount(trades.Count);
            for (int i = 0; i < trades.Count; i++)
            {
                OpraDatabaseTradeModel trade = trades[i];
                tradesGroup.Next();

                tradesGroup.MinTime = ToUnixEpoch(trade.MinTime);
                tradesGroup.MaxTime = ToUnixEpoch(trade.MaxTime);
                tradesGroup.SetUnderSymbol(trade.UnderSymbol ?? "");
                tradesGroup.SetExchange(trade.Exchange ?? "");
                tradesGroup.SetCondition(trade.Condition ?? "");
                tradesGroup.LegCount = trade.LegCount;
                tradesGroup.SetSpreadType(trade.SpreadType ?? "");
                tradesGroup.Quantity = trade.Quantity;
                tradesGroup.SetSymbol(trade.Symbol ?? "");
                tradesGroup.UnderPrice.Mantissa = EncodeMantissa(trade.UnderPrice, tradesGroup.UnderPrice.Exponent);
                tradesGroup.MinTue.Mantissa = EncodeMantissa(trade.MinTUE, tradesGroup.MinTue.Exponent);
                tradesGroup.MinBid.Mantissa = EncodeMantissa(trade.MinBid, tradesGroup.MinBid.Exponent);
                tradesGroup.Bid.Mantissa = EncodeMantissa(trade.Bid, tradesGroup.Bid.Exponent);
                tradesGroup.Ask.Mantissa = EncodeMantissa(trade.Ask, tradesGroup.Ask.Exponent);
                tradesGroup.Price.Mantissa = EncodeMantissa(trade.Price, tradesGroup.Price.Exponent);
                tradesGroup.MidMarket.Mantissa = EncodeMantissa(trade.MidMarket, tradesGroup.MidMarket.Exponent);
                tradesGroup.AboveMid.Mantissa = EncodeMantissa(trade.AboveMid, tradesGroup.AboveMid.Exponent);
                tradesGroup.TradeDelta = trade.TradeDelta;
                tradesGroup.SQLTime = ToUnixEpoch(trade.SQLTime);
                tradesGroup.SpreadID = (ulong)trade.SpreadID;
                tradesGroup.UnsureSymbol = trade.UnsureSymbol ? BooleanEnum.True : BooleanEnum.False;
                tradesGroup.TradeTime = ToUnixEpoch(trade.TradeTime);
                tradesGroup.UnderBid.Mantissa = EncodeMantissa(trade.UnderBid, tradesGroup.UnderBid.Exponent);
                tradesGroup.UnderAsk.Mantissa = EncodeMantissa(trade.UnderAsk, tradesGroup.UnderAsk.Exponent);
                tradesGroup.UnderLast.Mantissa = EncodeMantissa(trade.UnderLast, tradesGroup.UnderLast.Exponent);
                tradesGroup.HwTv.Mantissa = EncodeMantissa(trade.HWTV, tradesGroup.HwTv.Exponent);
                tradesGroup.HWTime = ToUnixEpoch(trade.HWTime);
                tradesGroup.Cond1 = (byte)trade.Cond1;
                tradesGroup.Cond2 = (byte)trade.Cond2;
                tradesGroup.Cond3 = (byte)trade.Cond3;
                tradesGroup.DeltaAdjTheo.Mantissa = EncodeMantissa(trade.DeltaAdjTheo, tradesGroup.DeltaAdjTheo.Exponent);
                tradesGroup.DeltaAdjTime = ToUnixEpoch(trade.DeltaAdjTime);
                tradesGroup.BidSize = trade.BidSize;
                tradesGroup.AskSize = trade.AskSize;
                tradesGroup.HwTheta = trade.HWTheta;
                tradesGroup.HwVega = trade.HWVega;
                tradesGroup.HwGamma = trade.HWGamma;
                tradesGroup.HwRho = trade.HWRho;
                tradesGroup.TimeValue = trade.TimeValue;
                tradesGroup.IntrinsicValue = trade.IntrinsicValue;
                tradesGroup.FvDivs = trade.FVDivs;
                tradesGroup.UFwd = trade.UFwd;
                tradesGroup.UFwdFactor = trade.UFwdFactor;
                tradesGroup.BorrowCost = trade.BorrowCost;
                tradesGroup.BorrowRate = trade.BorrowRate;
                tradesGroup.UPrice.Mantissa = EncodeMantissa(trade.UPrice, tradesGroup.UPrice.Exponent);
                tradesGroup.UTheo.Mantissa = EncodeMantissa(trade.UTheo, tradesGroup.UTheo.Exponent);
                tradesGroup.HwIv = trade.HWIV;
                tradesGroup.VolaTv.Mantissa = EncodeMantissa(trade.VolaTV, tradesGroup.VolaTv.Exponent);
                tradesGroup.VolaDeltaAdjTheo.Mantissa = EncodeMantissa(trade.VolaDeltaAdjTheo, tradesGroup.VolaDeltaAdjTheo.Exponent);
                tradesGroup.VolaIv = trade.VolaIV;
                tradesGroup.IsFirm = trade.IsFirm ? BooleanEnum.True : BooleanEnum.False;
                tradesGroup.SetFirmSide(trade.FirmSide ?? "");
                tradesGroup.DeltaAdjEdge.Mantissa = EncodeMantissa(trade.DeltaAdjEdge, tradesGroup.DeltaAdjEdge.Exponent);
                tradesGroup.DeltaAdjEdgeRefTime = ToUnixEpoch(trade.DeltaAdjEdgeRefTime);

                tradesGroup.IoiTimestamp = ToUnixEpoch(trade.IoiModel?.Timestamp ?? default(DateTime));
                tradesGroup.SetIoiDescription(trade.IoiModel?.Description ?? string.Empty);
                tradesGroup.IoiId = trade.IoiModel?.IoiId ?? 0;
                tradesGroup.IoiLimitPrice.Mantissa = EncodeMantissa(trade.IoiModel?.LimitPrice ?? 0, tradesGroup.IoiLimitPrice.Exponent);
                tradesGroup.IoiOrderQuantity = trade.IoiModel?.OrderQuantity ?? 0;
                tradesGroup.SetIoiRoute(trade.IoiModel?.Route ?? "");

                var legsCount = trade.IoiModel?.Legs.Count ?? 0;
                OpraDatabaseTradesResponseMessage.TradesGroup.IoiLegsGroup legsGroup = tradesGroup.IoiLegsCount(legsCount);
                if (trade.IoiModel == null)
                    continue;
                for (int j = 0; j < legsCount; j++)
                {
                    IoiLegModel leg = trade.IoiModel.Legs[j];
                    legsGroup.Next();

                    legsGroup.SetIoiLegUnderlyingSymbol(leg.UnderlyingSymbol);
                    legsGroup.IoiLegSecurityType = (int)leg.SecurityType;
                    legsGroup.SetIoiLegSide(leg.Side.ToString());
                    legsGroup.IoiLegType = (byte)leg.Type;
                    legsGroup.IoiLegStrike.Mantissa = EncodeMantissa(leg.Strike, legsGroup.IoiLegStrike.Exponent);
                    legsGroup.IoiLegExpiration = leg.Expiration;
                }
            }

            message.RequestId = (uint)opraDatabaseTradesResponse.RequestId;
            message.IsLastMessage = opraDatabaseTradesResponse.IsLastMessage ? BooleanEnum.True : BooleanEnum.False;

            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedRunnerStartMessage(DirectBuffer directBuffer, int offset, EdgeScanFeedRunnerStartRequest startRequest)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedRunnerStartMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedRunnerStartMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedRunnerStartMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedRunnerStartMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeScanFeedRunnerStartMessage message = new EdgeScanFeedRunnerStartMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            // NOTE: Per SBE spec, all top-level variable-length data MUST be written
            // AFTER all groups. Writing var-data before groups corrupts the shared
            // Limit cursor and silently zeroes out trailing var-data on decode.
            // RunnerId / FilterString / FilterConfig are therefore written at the end
            // alongside the AutoTraderConfig / OrderDefaults var-data (see bottom of
            // this method).
            // filter config
            EdgeScanFeedRunnerFilterConfig filterConfig = startRequest.FilterConfig;
            message.AutoTraderSkipActiveOrders = filterConfig.AutoTraderSkipActiveOrders ? BooleanEnum.True : BooleanEnum.False;
            message.MarkPrices = filterConfig.MarkPrices ? BooleanEnum.True : BooleanEnum.False;
            message.MarkPricesMinEdge = filterConfig.MarkPricesMinEdge;
            message.FilterConfigID = filterConfig.FilterConfigId;
            message.AutoTraderEdgeOverride = (byte)filterConfig.AutoTraderEdgeOverride;
            message.AutoTraderUseTradePrice = filterConfig.AutoTraderUseTradePrice ? BooleanEnum.True : BooleanEnum.False;
            message.AutoTraderAttemptBothSides = filterConfig.AutoTraderAttemptBothSides ? BooleanEnum.True : BooleanEnum.False;
            message.AutoTraderDoNotTradeThroughFillPrice = filterConfig.AutoTraderDoNotTradeThroughFillPrice ? BooleanEnum.True : BooleanEnum.False;
            message.AutoTraderMinQty = filterConfig.AutoTraderMinQty;
            message.AutoTraderMaxLatency = filterConfig.AutoTraderMaxLatency;
            message.AutoTraderMaxOpenPos = filterConfig.AutoTraderMaxOpenPos;
            message.AutoTraderResubmitCount = filterConfig.AutoTraderResubmitCount;
            message.AutoTraderMaxAllowedOrders = filterConfig.AutoTraderMaxAllowedOrders;
            message.AutoTraderMaxOrderRate = filterConfig.AutoTraderMaxOrderRate;
            message.BlockAlreadyTradedSymbols = filterConfig.BlockAlreadyTradedSymbols ? BooleanEnum.True : BooleanEnum.False;
            message.BlockAlreadyTradedSymbolsTimeout = filterConfig.BlockAlreadyTradedSymbolsTimeout;
            message.AutoTraderSideSelector = (byte)filterConfig.AutoTraderSideSelector;
            message.AutoTraderRouteOption = (byte)filterConfig.AutoTraderRouteOption;
            message.CutoffTime = ToUnixEpoch(filterConfig.CutoffTime);
            message.AutoStop = filterConfig.AutoStop ? BooleanEnum.True : BooleanEnum.False;
            message.BlockFirmTradesForTime = filterConfig.BlockFirmTradesForTime ? BooleanEnum.True : BooleanEnum.False;
            message.BlockFirmTradesForTimeInterval = filterConfig.BlockFirmTradesForTimeInterval;
            message.BlockArea = filterConfig.BlockArea ? BooleanEnum.True : BooleanEnum.False;
            message.BlockAreaStrikeRange = filterConfig.BlockAreaStrikeRange;
            message.MinPnlForAutoTraderEnabled = filterConfig.MinPnlForAutoTraderEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinPnlForAutoTrader = filterConfig.MinPnlForAutoTrader;

            // Dimension count MUST match the number of Next() calls below, otherwise
            // each extra entry advances the shared Limit cursor on decode by a full
            // group block-length and silently truncates all trailing var-data.
            var rteGroup = message.ExchToRouteMapV3Count(filterConfig.ExchToRouteMapV3?.Count ?? 0);
            if (filterConfig.ExchToRouteMapV3 != null)
            {
                foreach (KeyValuePair<string, string> kvp in filterConfig.ExchToRouteMapV3)
                {
                    rteGroup.Next();
                    rteGroup.SetExch(kvp.Key ?? "");
                    rteGroup.SetRoute(kvp.Value ?? "");

                }
            }

            message.AutoTraderEnablePayUpTicks = filterConfig.AutoTraderEnablePayUpTicks ? BooleanEnum.True : BooleanEnum.False;
            message.AutoTraderPayUpTicks = filterConfig.AutoTraderPayUpTicks;
            message.MinPnlMaxQtyCheckEnabled = filterConfig.MinPnlMaxQtyCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinPnlMaxQty = filterConfig.MinPnlMaxQty;

            message.ReportFilter = (int)startRequest.ReportFilter;

            // AT Config
            AutoTraderConfig config = startRequest.AutoTraderConfig;
            message.UserId = config.UserId;
            message.RiskCheckId = config.RiskCheckId;
            message.RiskCheckPassed = config.RiskCheckPassed ? BooleanEnum.True : BooleanEnum.False;
            message.Sequence = config.Sequence;
            message.Venue = (byte)config.Venue;
            message.EdgeType = (byte)config.EdgeType;
            message.EdgeValue = config.EdgeValue;

            message.TheoModel = (int)config.TheoModel;
            message.FishLossTheoModel = (int)config.FishLossTheoModel;
            message.AutoCancelTheoModel = (int)config.AutoCancelTheoModel;
            message.ForMarketCrossPriceUseSweepEnabled = config.ForMarketCrossPriceUseSweepEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxSizeEnabled = config.CancelWithMaxSizeEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithOrderPriceEdgeToTheoEnabled = config.CancelWithOrderPriceEdgeToTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithOrderPriceEdgeToModelTheoEnabled = config.CancelWithOrderPriceEdgeToModelTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithTimerEnabled = config.CancelWithTimerEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToTheoEnabled = config.CancelWithEdgeToTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToAdjTheoEnabled = config.CancelWithEdgeToAdjTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInUnderlyingPxEnabled = config.CancelWithChangeInUnderlyingPxEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInUnderlyingDeltaPxEnabled = config.CancelWithChangeInUnderlyingDeltaPxEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithEdgeToMidEnabled = config.CancelWithEdgeToMidEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithChangeInWidthEnabled = config.CancelWithChangeInWidthEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxWidthEnabled = config.CancelWithMaxWidthEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CancelWithMaxSizeLimit = config.CancelWithMaxSizeLimit;
            message.CancelWithOrderPriceEdgeToTheo = config.CancelWithOrderPriceEdgeToTheo;
            message.CancelWithOrderPriceEdgeToModelTheo = config.CancelWithOrderPriceEdgeToModelTheo;
            message.CancelWithTimer = config.CancelWithTimer;
            message.CancelWithTheoEdge = config.CancelWithTheoEdge;
            message.CancelWithAdjTheoEdge = config.CancelWithAdjTheoEdge;
            message.CancelWithUnderlyingPxThreshold = config.CancelWithUnderlyingPxThreshold;
            message.CancelWithUnderlyingDeltaPx = config.CancelWithUnderlyingDeltaPx;
            message.CancelWithMidEdge = config.CancelWithMidEdge;
            message.CancelWithWidthThreshold = config.CancelWithWidthThreshold;
            message.CancelWithMaxWidthThreshold = config.CancelWithMaxWidthThreshold;
            message.MinEdgeToTheoCheckEnabled = config.MinEdgeToTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToHwTheoCheckEnabled = config.MinEdgeToHwTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToV0TheoCheckEnabled = config.MinEdgeToV0TheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToMidCheckEnabled = config.MinEdgeToMidCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToEmaCheckEnabled = config.MinEdgeToEmaCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToMarketCheckEnabled = config.MinEdgeToMarketCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidPercentCheckEnabled = config.MinBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MaxBidPercentCheckEnabled = config.MaxBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidAskSizeCheckEnabled = config.MinBidAskSizeCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEmaWidthPercentEdgeToTheoCheckEnabled = config.MinEmaWidthPercentEdgeToTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinBidCheckEnabled = config.MinBidCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinTheoCheckEnabled = config.MinTheoCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MinEdgeToTheo = config.MinEdgeToTheo;
            message.MinEdgeToHwTheo = config.MinEdgeToHwTheo;
            message.MinEdgeToV0Theo = config.MinEdgeToV0Theo;
            message.MinEdgeToMid = config.MinEdgeToMid;
            message.MinEdgeToEma = config.MinEdgeToEma;
            message.MinEdgeToMarket = config.MinEdgeToMarket;
            message.MinBidPercent = config.MinBidPercent;
            message.MaxBidPercent = config.MaxBidPercent;
            message.MinBidAskSize = config.MinBidAskSize;
            message.MinEmaWidthPercentEdgeToTheoCheckEdge = config.MinEmaWidthPercentEdgeToTheoCheckEdge;
            message.MinBidCheckBidValue = config.MinBidCheckBidValue;
            message.MinTheoCheckTheoValue = config.MinTheoCheckTheoValue;
            message.EdgeToAdjTheoWithOverrideUsePercentage = config.EdgeToAdjTheoWithOverrideUsePercentage ? BooleanEnum.True : BooleanEnum.False;
            message.EdgeToAdjTheoWithOverrideStatic = config.EdgeToAdjTheoWithOverrideStatic;
            message.EdgeToAdjTheoWithOverridePercent = config.EdgeToAdjTheoWithOverridePercent;
            message.CheckForRecentAttempt = config.CheckForRecentAttempt ? BooleanEnum.True : BooleanEnum.False;
            message.CheckForRecentAttemptTimespan = config.CheckForRecentAttemptTimespan;
            message.CheckForRecentFill = config.CheckForRecentFill ? BooleanEnum.True : BooleanEnum.False;
            message.CheckForRecentFillTimespan = config.CheckForRecentFillTimespan;
            message.MinSpxAuction = config.MinSpxAuction;
            message.MinSpxSpreadAuction = config.MinSpxSpreadAuction;
            message.MinSingleLegAuction = config.MinSingleLegAuction;
            message.MinSpreadAuction = config.MinSpreadAuction;

            message.BestOfAdjTheoEnabled = config.BestOfAdjTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfAdjTheoEdge = config.BestOfAdjTheoEdge;
            message.BestOfAdjTheoModel = config.BestOfAdjTheoModel;
            message.BestOfHwTheoEnabled = config.BestOfHwTheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfHwTheoEdge = config.BestOfHwTheoEdge;
            message.BestOfV0TheoEnabled = config.BestOfV0TheoEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfV0TheoEdge = config.BestOfV0TheoEdge;
            message.BestOfMidEnabled = config.BestOfMidEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfMidEdge = config.BestOfMidEdge;
            message.BestOfEmaEnabled = config.BestOfEmaEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfEmaEdge = config.BestOfEmaEdge;
            message.BestOfBidPercentEnabled = config.BestOfBidPercentEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfBidPercentEdge = config.BestOfBidPercentEdge;
            message.BestOfDigBidPercentEnabled = config.BestOfDigBidPercentEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.BestOfDigBidPercentEdge = config.BestOfDigBidPercentEdge;
            message.MaxDigBidPercentCheckEnabled = config.MaxDigBidPercentCheckEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.MaxDigBidPercent = config.MaxDigBidPercent;

            message.AutoPermEnabled = config.AutoPermEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.AutoPermMinEdge = config.AutoPermMinEdge;
            message.AutoPermOrderCount = config.AutoPermOrderCount;
            message.AutoPermMaxGeneration = config.AutoPermMaxGeneration;
            message.AutoPermSubmissionStyle = (byte)config.AutoPermSubmissionStyle;
            message.AutoPermOrderInitialSize = config.AutoPermOrderInitialSize;

            var automationConfigsGroup = message.AutomationConfigsCount((config.UnderlyingToAutomationConfigs?.Count ?? 0) + 1);
            automationConfigsGroup.Next();
            EncodeAutomationConfig(automationConfigsGroup, config.DefaultAutomationConfig, true);
            if (config.UnderlyingToAutomationConfigs != null)
            {
                foreach (var automationConfig in config.UnderlyingToAutomationConfigs)
                {
                    automationConfigsGroup.Next();
                    EncodeAutomationConfig(automationConfigsGroup, automationConfig);
                }
            }

            var openRouteSmartmap = message.OpenRouteSmartMapCount(config.OpenRouteSmartMap?.Count ?? 0);
            if (config.OpenRouteSmartMap != null)
            {
                foreach (var routeMap in config.OpenRouteSmartMap)
                {
                    openRouteSmartmap.Next();
                    openRouteSmartmap.SetRoute(routeMap.Item1);
                    openRouteSmartmap.Delay = routeMap.Item2;
                }
            }

            var closeRouteSmartmap = message.CloseRouteSmartMapCount(config.CloseRouteSmartMap?.Count ?? 0);
            if (config.CloseRouteSmartMap != null)
            {
                foreach (var routeMap in config.CloseRouteSmartMap)
                {
                    closeRouteSmartmap.Next();
                    closeRouteSmartmap.SetRoute(routeMap.Item1);
                    closeRouteSmartmap.Delay = routeMap.Item2;
                }
            }

            var openRouteSingleLegSmartmap = message.OpenRouteSingleLegSmartMapCount(config.OpenRouteSingleLegSmartMap?.Count ?? 0);
            if (config.OpenRouteSingleLegSmartMap != null)
            {
                foreach (var routeMap in config.OpenRouteSingleLegSmartMap)
                {
                    openRouteSingleLegSmartmap.Next();
                    openRouteSingleLegSmartmap.SetRoute(routeMap.Item1);
                    openRouteSingleLegSmartmap.Delay = routeMap.Item2;
                }
            }

            var closeRouteSingleLegSmartmap = message.CloseRouteSingleLegSmartMapCount(config.CloseRouteSingleLegSmartMap?.Count ?? 0);
            if (config.CloseRouteSingleLegSmartMap != null)
            {
                foreach (var routeMap in config.CloseRouteSingleLegSmartMap)
                {
                    closeRouteSingleLegSmartmap.Next();
                    closeRouteSingleLegSmartmap.SetRoute(routeMap.Item1);
                    closeRouteSingleLegSmartmap.Delay = routeMap.Item2;
                }
            }

            // All trailing var-data must be written in schema order (see template data
            // ids 121..133) because each SetXxx call advances the shared Limit cursor.
            // The decoder reads them in this exact order.
            message.SetRunnerId(startRequest.RunnerId);
            message.SetFilterString(filterConfig.FilterString ?? "");
            message.SetFilterConfig(filterConfig.FilterConfig ?? "");
            message.SetRiskCheckMessage(config.RiskCheckMessage ?? "");
            message.SetConfigId(config.ConfigId ?? "");
            message.SetConfigName(config.ConfigName ?? "");
            message.SetSweepRoute(config.SweepRoute ?? "");

            OrderSubmissionDefaults orderDefaults = startRequest.OrderDefaults;
            message.SetOrderDefaultAccount(orderDefaults.Account ?? "");
            message.SetOrderDefaultRoute(orderDefaults.Route ?? "");
            message.SetOrderDefaultSingleLegRoute(orderDefaults.SingleLegRoute ?? "");
            message.SetOrderDefaultRouteSpxRutXsp(orderDefaults.RouteSpxRutXsp ?? "");
            message.SetOrderDefaultRouteNdx(orderDefaults.RouteNdx ?? "");
            message.SetBlockedSymbolModel(
                startRequest.BlockedSymbolModel != null
                    ? JsonConvert.SerializeObject(startRequest.BlockedSymbolModel)
                    : "");
            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedRunnerStopMessage(DirectBuffer directBuffer, int offset, string runnerId)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedRunnerStopMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedRunnerStopMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedRunnerStopMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedRunnerStopMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeScanFeedRunnerStopMessage message = new EdgeScanFeedRunnerStopMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetRunnerId(runnerId);
            return message.Limit - offset;
        }

        public int EncodeEdgeScanFeedRunnerUpdateMessage(DirectBuffer directBuffer, int offset, string runnerId, EdgeScanFeedRunnerState state)
        {
            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = EdgeScanFeedRunnerUpdateMessage.BlockLength;
            messageHeader.SchemaId = EdgeScanFeedRunnerUpdateMessage.SchemaId;
            messageHeader.TemplateId = EdgeScanFeedRunnerUpdateMessage.TemplateId;
            messageHeader.Version = EdgeScanFeedRunnerUpdateMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;

            EdgeScanFeedRunnerUpdateMessage message = new EdgeScanFeedRunnerUpdateMessage();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.SetRunnerId(runnerId);
            message.RunnerState = (byte)state;
            return message.Limit - offset;
        }

        public int EncodeTradeSlim(DirectBuffer directBuffer, int offset, TradeSlim model)
        {
            string symbol = model.Symbol ?? "";
            string underSymbol = model.UnderSymbol ?? "";
            string exchange = model.Exchange ?? "";

            int bufferOffset = offset;

            MessageHeader messageHeader = new MessageHeader();
            TradeSlimMessage sbeMessage = new TradeSlimMessage();

            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = TradeSlimMessage.BlockLength;
            messageHeader.SchemaId = TradeSlimMessage.SchemaId;
            messageHeader.TemplateId = TradeSlimMessage.TemplateId;
            messageHeader.Version = TradeSlimMessage.SchemaVersion;

            bufferOffset += MessageHeader.Size;
            sbeMessage.WrapForEncode(directBuffer, bufferOffset);

            sbeMessage.UnsureSymbol = model.UnsureSymbol ? BooleanEnum.True : BooleanEnum.False;
            sbeMessage.Condition = (byte)model.Condition;

            sbeMessage.LegCount = model.LegCount;
            sbeMessage.Quantity = model.Quantity;
            sbeMessage.BidSize = model.BidSize;
            sbeMessage.AskSize = model.AskSize;

            sbeMessage.Price = model.Price;
            sbeMessage.TradeDelta = model.TradeDelta;
            sbeMessage.Bid = model.Bid;
            sbeMessage.Ask = model.Ask;
            sbeMessage.UnderBid = model.UnderBid;
            sbeMessage.UnderAsk = model.UnderAsk;
            sbeMessage.DeltaAdjTheo = model.DeltaAdjTheo;
            sbeMessage.VolaDeltaAdjTheo = model.VolaDeltaAdjTheo;
            sbeMessage.ImpliedVol = model.ImpliedVol;
            sbeMessage.Vega = model.Vega;

            sbeMessage.TradeTime = ToUnixEpoch(model.TradeTime);

            sbeMessage.SetSymbol(symbol);
            sbeMessage.SetUnderSymbol(underSymbol);
            sbeMessage.SetExchange(exchange);

            return sbeMessage.Limit - offset;
        }

        private static void EncodeAutomationConfig(EdgeScanFeedRunnerStartMessage.AutomationConfigsGroup message, AutomationConfig config, bool isDefault = false)
        {
            message.IsDefault = isDefault ? BooleanEnum.True : BooleanEnum.False;
            message.SetUnderlyingSymbol(config.ConfigKey?.Underlying ?? "");
            message.Increment = config.ConfigKey?.Increment ?? 0;
            message.LoopingEnabled =
                config.LoopingEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.CloseEdgeType = (byte)config.CloseEdgeType;
            message.StaticCloseEdge = config.StaticCloseEdge;
            message.StaticMinLoopEdge = config.StaticMinLoopEdge;
            message.StaticMaxLoss = config.StaticMaxLoss;
            message.LooperDynamicRouting = config.LooperDynamicRouting
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AttemptIncrementUsingDynamicRoute =
                config.AttemptIncrementUsingDynamicRoute ? BooleanEnum.True : BooleanEnum.False;
            message.EnableDynamicRouteForOpeningOrders =
                config.EnableDynamicRouteForOpeningOrders ? BooleanEnum.True : BooleanEnum.False;
            message.EnableDynamicRouteForClosingOrders =
                config.EnableDynamicRouteForClosingOrders ? BooleanEnum.True : BooleanEnum.False;
            message.CloseIntervalType = (byte)config.CloseIntervalType;
            message.StaticCloseInterval = config.StaticCloseInterval;
            message.StaticCloseIntervalMax = config.StaticCloseIntervalMax;
            message.StaticLoopInterval = config.StaticLoopInterval;
            message.StaticLoopIntervalMax = config.StaticLoopIntervalMax;
            message.IncrementType = (byte)config.IncrementType;
            message.StaticIncrement = config.StaticIncrement;
            message.SizeUpType = (byte)config.SizeUpType;
            message.StaticSizeUpLoopCountBeforeSizeup =
                config.StaticSizeUpLoopCountBeforeSizeup;
            message.StaticSizeUp = config.StaticSizeUp;
            message.AutoAggressorEnabled = config.AutoAggressorEnabled
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AutoAggressorMode = (byte)config.AutoAggressorMode;
            message.AutoAggressorEdgeTightenMode =
                (byte)config.AutoAggressorEdgeTightenMode;
            message.AutoAggressorEdgeTightenPercentage =
                config.AutoAggressorEdgeTightenPercentage;
            message.ScratchOnLowDeltaSize = config.ScratchOnLowDeltaSize
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.ScratchOnLowDeltaMax = config.ScratchOnLowDeltaMax;
            message.ScratchOnLowDeltaMaxLoss = config.ScratchOnLowDeltaMaxLoss;
            message.ScratchOnLowDeltaMinSize = config.ScratchOnLowDeltaMinSize;
            message.FreeLookRequireMinFillTime = config.FreeLookRequireMinFillTime
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookMinFillTime = config.FreeLookMinFillTime;
            message.FreeLookOnLosers =
                config.FreeLookOnLosers ? BooleanEnum.True : BooleanEnum.False;
            message.FreeLookOnLosersMax = config.FreeLookOnLosersMax;
            message.FreeLookOnAll =
                config.FreeLookOnAll ? BooleanEnum.True : BooleanEnum.False;
            message.FreeWhenGettingCloseEdge = config.FreeWhenGettingCloseEdge
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookAfterLastAttempt = config.FreeLookAfterLastAttempt
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.FreeLookBackUpIncrement = config.FreeLookBackUpIncrement;
            message.FreeLookOnAllWalkBackIncrement =
                config.FreeLookOnAllWalkBackIncrement;
            message.LoopFreeLookOnAllUsingTicks =
                config.LoopFreeLookOnAllUsingTicks ? BooleanEnum.True : BooleanEnum.False;
            message.FreeLookOnAllIncrementTicks =
                config.FreeLookOnAllIncrementTicks;
            message.FreeLookOnAllWalkBackIncrementTicks =
                config.FreeLookOnAllWalkBackIncrementTicks;
            message.LoopFreeLookOnNickelNames = config.LoopFreeLookOnNickelNames
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.LoopFreeLookOnNickelNamesIncrement =
                config.LoopFreeLookOnNickelNamesIncrement;
            message.LoopFreeLookOnDimeNames = config.LoopFreeLookOnDimeNames
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.LoopFreeLookOnDimeNamesIncrement =
                config.LoopFreeLookOnDimeNamesIncrement;
            message.MaintainLastEdge =
                config.MaintainLastEdge ? BooleanEnum.True : BooleanEnum.False;
            message.AttemptResubmitCount = config.AttemptResubmitCount;
            message.LastFillResubmitCount = config.LastFillResubmitCount;
            message.MaxNumberOfLoops = config.MaxNumberOfLoops;
            message.PartialFillPercentage = config.PartialFillPercentage;
            message.PartialFillResubmit = config.PartialFillResubmit;
            message.LoopPricingMode = (byte)config.LoopPricingMode;
            message.AdjustClosingPriceToMarketWinnersOnly =
                config.AdjustClosingPriceToMarketWinnersOnly
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.PxCrossOption = (byte)config.PxCrossOption;
            message.ClosePxCrossOption = (byte)config.ClosePxCrossOption;
            message.AutoHedgeOnClose =
                config.AutoHedgeOnClose ? BooleanEnum.True : BooleanEnum.False;
            message.AutoHedgeOnCloseSizeOnly = config.AutoHedgeOnCloseSizeOnly
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.MinHedgeHouseEdge = config.MinHedgeHouseEdge;
            message.AutoHedgeOnFailure = config.AutoHedgeOnFailure
                ? BooleanEnum.True
                : BooleanEnum.False;
            message.AutoHedgePartial =
                config.AutoHedgePartial ? BooleanEnum.True : BooleanEnum.False;
            message.AutoLegEnabled =
                config.AutoLegEnabled ? BooleanEnum.True : BooleanEnum.False;
            message.AutoLegMaxWidth = config.AutoLegMaxWidth;
            message.AutoLegCloseEdge = config.AutoLegCloseEdge;
            message.AutoLegMaxLoss = config.AutoLegMaxLoss;
            message.AutoLegCloseIncrement = config.AutoLegCloseIncrement;
            message.AutoLegRestTime = config.AutoLegRestTime;
            message.SetOpenRoute(config.OpenRoute ?? "");
            message.SetCloseRoute(config.CloseRoute ?? "");
            message.SetOpenRouteSingleLeg(config.OpenRouteSingleLeg ?? "");
            message.SetCloseRouteSingleLeg(config.CloseRouteSingleLeg ?? "");
            message.SetOpenRouteSize(config.OpenRouteSize ?? "");
            message.SetCloseRouteSize(config.CloseRouteSize ?? "");
            message.SetOpenRouteSingleLegSize(config.OpenRouteSingleLegSize ?? "");
            message.SetCloseRouteSingleLegSize(
                config.CloseRouteSingleLegSize ?? "");
            message.SetLoopFreeLookOnNickelNamesRoute(
                config.LoopFreeLookOnNickelNamesRoute ?? "");
            message.SetLoopFreeLookOnDimeNamesRoute(
                config.LoopFreeLookOnDimeNamesRoute ?? "");
            message.SetAutoLegCloseRoute(config.AutoLegCloseRoute ?? "");

            message.DynamicIntervalDefaultInterval =
                config.DynamicCloseInterval?.DefaultInterval ?? 0;
            message.DynamicIntervalDefaultResubmitCount =
                config.DynamicCloseInterval?.DefaultResubmit ?? 0;

            message.DynamicCloseEdgeEnabled =
                config.DynamicCloseEdge != null ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicSizeUpEnabled =
                config.DynamicSizeUp != null ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicCloseIntervalEnabled =
                config.DynamicCloseInterval != null ? BooleanEnum.True : BooleanEnum.False;

            message.DynamicEdgePercentBidRangeEnabled =
                (config.DynamicCloseEdge?.PercentBidRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeBaseEdgeEnabled =
                (config.DynamicCloseEdge?.BaseEdgeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeEmaRangeEnabled =
                (config.DynamicCloseEdge?.EmaRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeTradePxRangeEnabled =
                (config.DynamicCloseEdge?.TradePxRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeMinMarketWidthEnabled =
                (config.DynamicCloseEdge?.MinMarketWidthEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeMinMarketCrossEnabled =
                (config.DynamicCloseEdge?.MinMarketCrossEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeTheoRangeEnabled =
                (config.DynamicCloseEdge?.TheoRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeVolaRangeEnabled =
                (config.DynamicCloseEdge?.VolaRangeEnabled ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeVolaModel = (int)(config.DynamicCloseEdge?.VolaModel ?? TheoModel.Hanw);
            message.DynamicEdgeDynamicVolaRangeEnabled =
                (config.DynamicCloseEdge?.DynamicVolaRangeEnabled ?? false)
                    ? BooleanEnum.True
                    : BooleanEnum.False;
            message.DynamicEdgeDynamicVolaModel =
                (int)(config.DynamicCloseEdge?.DynamicVolaModel ?? TheoModel.Hanw);
            message.DynamicEdgeDynamicLookupMode = (config.DynamicCloseEdge?.DynamicLookupMode ?? false) ? BooleanEnum.True : BooleanEnum.False;
            message.DynamicEdgeUnderDivisor = config.DynamicCloseEdge?.UnderDivisor ?? 0;

            var automationConfigDteConfigGroup = message.DteConfigsCount(
                CountNonNull(config.DynamicCloseEdge?.DteTable)
                + CountNonNull(config.DynamicCloseEdge?.DynamicDteTable));
            EncodeDteConfigs(automationConfigDteConfigGroup, config.DynamicCloseEdge?.DteTable);
            EncodeDteConfigs(automationConfigDteConfigGroup, config.DynamicCloseEdge?.DynamicDteTable, true);

            var deltaMessage = message.DeltaConfigsCount(config.DynamicCloseEdge?.DeltaTable?.Count ?? 0);
            if (config.DynamicCloseEdge?.DeltaTable != null)
            {
                foreach (var deltaConfig in config.DynamicCloseEdge.DeltaTable)
                {
                    deltaMessage.Next();
                    deltaMessage.Active = deltaConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                    deltaMessage.Delta = deltaConfig.Delta;
                    deltaMessage.AdditionalEdgePerContract = deltaConfig.AdditionalEdgePerContract;
                    deltaMessage.AddedEdge = deltaConfig.AddedEdge;
                }
            }

            var dynamicSizeUpMessage = message.DynamicSizeUpConfigsCount(config.DynamicSizeUp?.SizeUpConfigs?.Count ?? 0);
            if (config.DynamicSizeUp?.SizeUpConfigs != null)
            {
                foreach (var sizeUpConfig in config.DynamicSizeUp.SizeUpConfigs)
                {
                    dynamicSizeUpMessage.Next();
                    dynamicSizeUpMessage.Enabled = sizeUpConfig.Enabled ? BooleanEnum.True : BooleanEnum.False;
                    dynamicSizeUpMessage.Edge = sizeUpConfig.Edge;
                    dynamicSizeUpMessage.AdditionalEdgePerContract = sizeUpConfig.AdditionalEdgePerContract;
                    dynamicSizeUpMessage.MaxAbsDelta = sizeUpConfig.MaxAbsDelta;
                    dynamicSizeUpMessage.MaxUnderWidth = sizeUpConfig.MaxUnderWidth;
                    dynamicSizeUpMessage.Size = sizeUpConfig.Size;
                    dynamicSizeUpMessage.ResubmitSizeOption = (byte)sizeUpConfig.ResubmitSizeOption;
                    dynamicSizeUpMessage.RequiredLoop = sizeUpConfig.RequiredLoop;
                    dynamicSizeUpMessage.ResubmitCount = sizeUpConfig.ResubmitCount;
                    dynamicSizeUpMessage.MatchSignalQtyLimit = sizeUpConfig.MatchSignalQtyLimit;
                }
            }

            var dynamicIntervalConfigs = message.DynamicIntervalConfigsCount(config.DynamicCloseInterval?.IntervalTable?.Count ?? 0);
            if (config.DynamicCloseInterval?.IntervalTable != null)
            {
                foreach (var sizeUpConfig in config.DynamicCloseInterval.IntervalTable)
                {
                    dynamicIntervalConfigs.Next();
                    dynamicIntervalConfigs.Active = sizeUpConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                    dynamicIntervalConfigs.MinDelta = sizeUpConfig.MinDelta;
                    dynamicIntervalConfigs.MaxDelta = sizeUpConfig.MaxDelta;
                    dynamicIntervalConfigs.AttemptedEdge = sizeUpConfig.AttemptedEdge;
                    dynamicIntervalConfigs.Interval = sizeUpConfig.Interval;
                    dynamicIntervalConfigs.ResubmitCount = sizeUpConfig.ResubmitCount;
                    dynamicIntervalConfigs.SetRoute(sizeUpConfig.Route ?? "");
                    dynamicIntervalConfigs.DisableRounding = sizeUpConfig.DisableRounding ? BooleanEnum.True : BooleanEnum.False;
                }
            }

            var exchToRouteList = message.ExchToRouteListCount(config.ExchToRouteList?.Count ?? 0);
            if (config.ExchToRouteList != null)
            {
                foreach (var exchRoutePair in config.ExchToRouteList)
                {
                    exchToRouteList.Next();
                    exchToRouteList.SetExch(exchRoutePair.Item1);
                    exchToRouteList.SetRoute(exchRoutePair.Item2);
                }
            }

            var dynamicIncrements = message.DynamicIncrementConfigsCount(config.DynamicIncrement?.Count ?? 0);
            if (config.DynamicIncrement != null)
            {
                foreach (var dynamicIncrement in config.DynamicIncrement)
                {
                    dynamicIncrements.Next();
                    dynamicIncrements.Edge = dynamicIncrement.Edge;
                    dynamicIncrements.Increment = dynamicIncrement.Increment;
                }
            }
        }

        private static void EncodeDteConfigs(EdgeScanFeedRunnerStartMessage.AutomationConfigsGroup.DteConfigsGroup message, List<DaysToExpirationEdgeModel?>? configs, bool isDynamic = false)
        {
            if (configs == null)
            {
                return;
            }
            foreach (var dteConfig in configs)
            {
                if (dteConfig == null)
                {
                    continue;
                }
                message.Next();
                message.IsDynamic = isDynamic ? BooleanEnum.True : BooleanEnum.False;
                message.Active = dteConfig.Active ? BooleanEnum.True : BooleanEnum.False;
                message.DaysToExpiration = dteConfig.DaysToExpiration;
                message.MinBidAskSize = dteConfig.MinBidAskSize;
                message.MinIncrement = dteConfig.MinIncrement;
                message.MinWidth = dteConfig.MinWidth;
                message.MinSpacingForVertical = dteConfig.MinSpacingForVertical;
                message.MinSpacingForFlys = dteConfig.MinSpacingForFlys;
                message.MinSpacingForVerticalPercentage =
                    dteConfig.MinSpacingForVerticalPercentage;
                message.MinSpacingForFlysPercentage = dteConfig.MinSpacingForFlysPercentage;
                message.BaseEdge = dteConfig.BaseEdge;
                message.CloseEdge = dteConfig.CloseEdge;
                message.LoopMinEdge = dteConfig.LoopMinEdge;
                message.AutoPermMinEdge = dteConfig.AutoPermMinEdge;
                message.VerticalQty = dteConfig.VerticalQty;
                message.Qty = dteConfig.Qty;
                message.MaxPercentBid = dteConfig.MaxPercentBid;
                message.LoopMaxLoss = dteConfig.LoopMaxLoss;
                message.AdditionalEdgePerContract =
                    dteConfig.AdditionalEdgePerContract;
                message.AdditionalEdgePerWeightedVega =
                    dteConfig.AdditionalEdgePerWeightedVega;
                message.MaxAllowedAboveEma = dteConfig.MaxAllowedAboveEma;
                message.MaxAllowedAboveTheo = dteConfig.MaxAllowedAboveTheo;
                message.MaxAllowedAboveVola = dteConfig.MaxAllowedAboveVola;
                message.MinMarketWidth = dteConfig.MinMarketWidth;
                message.MaxThroughTradePx = dteConfig.MaxThroughTradePx;
                message.MinMarketCross = dteConfig.MinMarketCross;
                message.DynamicBaseEdge = dteConfig.DynamicBaseEdge;
                message.DynamicBaseEdgeAddition = dteConfig.DynamicBaseEdgeAddition;
                message.AdditionalEdgePerWidth = dteConfig.AdditionalEdgePerWidth;
                message.DynamicCloseEdge = dteConfig.DynamicCloseEdge;
                message.DynamicCloseEdgeAddition =
                    dteConfig.DynamicCloseEdgeAddition;
                message.AdditionalCloseEdgePerWidth =
                    dteConfig.AdditionalCloseEdgePerWidth;
                message.DynamicAutoPermMinEdge = dteConfig.DynamicAutoPermMinEdge;
                message.DynamicAutoPermMinEdgeAddition =
                    dteConfig.DynamicAutoPermMinEdgeAddition;
                message.DynamicLoopMinEdge = dteConfig.DynamicLoopMinEdge;
                message.DynamicLoopMinEdgeAddition =
                    dteConfig.DynamicLoopMinEdgeAddition;
                message.DynamicLoopMaxLoss = dteConfig.DynamicLoopMaxLoss;
                message.DynamicLoopMaxLossAddition =
                    dteConfig.DynamicLoopMaxLossAddition;
                message.DynamicAdditionalEdgePerContract =
                    dteConfig.DynamicAdditionalEdgePerContract;
                message.DynamicAdditionalEdgePerContractAddition =
                    dteConfig.DynamicAdditionalEdgePerContractAddition;
                message.DynamicAdditionalEdgePerWeightedVega =
                    dteConfig.DynamicAdditionalEdgePerWeightedVega;
                message.DynamicAdditionalEdgePerWeightedVegaAddition =
                    dteConfig.DynamicAdditionalEdgePerWeightedVegaAddition;
                message.DynamicMaxAllowedPercentBid =
                    dteConfig.DynamicMaxAllowedPercentBid;
                message.DynamicMaxAllowedPercentBidAddition =
                    dteConfig.DynamicMaxAllowedPercentBidAddition;
                message.DynamicMaxAllowedAboveEma =
                    dteConfig.DynamicMaxAllowedAboveEma;
                message.DynamicMaxAllowedAboveEmaAddition =
                    dteConfig.DynamicMaxAllowedAboveEmaAddition;
                message.DynamicMaxAllowedAboveTheo =
                    dteConfig.DynamicMaxAllowedAboveTheo;
                message.DynamicMaxAllowedAboveTheoAddition =
                    dteConfig.DynamicMaxAllowedAboveTheoAddition;
                message.DynamicMaxAllowedAboveVola =
                    dteConfig.DynamicMaxAllowedAboveVola;
                message.DynamicMaxAllowedAboveVolaAddition =
                    dteConfig.DynamicMaxAllowedAboveVolaAddition;
                message.DynamicMinMarketWidth = dteConfig.DynamicMinMarketWidth;
                message.DynamicMinMarketWidthAddition =
                    dteConfig.DynamicMinMarketWidthAddition;
            }
        }

        private static int CountNonNull<T>(List<T?>? items) where T : class
        {
            if (items == null)
            {
                return 0;
            }

            int count = 0;
            foreach (T? item in items)
            {
                if (item != null)
                {
                    count++;
                }
            }

            return count;
        }

        #region Auth Server Messages

        public int EncodeAuthLoginRequest(DirectBuffer directBuffer, int offset, AuthLoginRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthLoginRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthLoginRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthLoginRequest.TemplateId;
            messageHeader.Version = Generated.AuthLoginRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthLoginRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.IsReauth = model.IsReauth ? BooleanEnum.True : BooleanEnum.False;
            message.SetUsername(model.Username ?? "");
            message.SetPassword(model.Password ?? "");
            message.SetAppCode(model.AppCode ?? "");
            message.SetVersion(model.Version ?? "");
            message.SetSystemInfo(model.SystemInfo ?? "");
            message.SetAuthCode(model.AuthCode ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthLoginResponse(DirectBuffer directBuffer, int offset, AuthLoginResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthLoginResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthLoginResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthLoginResponse.TemplateId;
            messageHeader.Version = Generated.AuthLoginResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthLoginResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.IsAuthenticated = model.IsAuthenticated ? BooleanEnum.True : BooleanEnum.False;
            message.UserId = model.UserId;
            message.ServerTime = (ulong)model.ServerTime.Ticks;
            message.MaxDuplicateSessions = model.MaxDuplicateSessions;
            message.SetAuthCode(model.AuthCode ?? "");
            message.SetUserJson(model.UserJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthUpdatePasswordRequest(DirectBuffer directBuffer, int offset, AuthUpdatePasswordRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthUpdatePasswordRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthUpdatePasswordRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthUpdatePasswordRequest.TemplateId;
            messageHeader.Version = Generated.AuthUpdatePasswordRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthUpdatePasswordRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetNewPassword(model.NewPassword ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthUpdatePasswordResponse(DirectBuffer directBuffer, int offset, AuthUpdatePasswordResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthUpdatePasswordResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthUpdatePasswordResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthUpdatePasswordResponse.TemplateId;
            messageHeader.Version = Generated.AuthUpdatePasswordResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthUpdatePasswordResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.IsSuccess = model.IsSuccess ? BooleanEnum.True : BooleanEnum.False;
            message.SetComment(model.Comment ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthGetUsersRequest(DirectBuffer directBuffer, int offset, AuthGetUsersRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetUsersRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetUsersRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetUsersRequest.TemplateId;
            messageHeader.Version = Generated.AuthGetUsersRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetUsersRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            return message.Limit - offset;
        }

        public int EncodeAuthGetUsersResponse(DirectBuffer directBuffer, int offset, AuthGetUsersResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetUsersResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetUsersResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetUsersResponse.TemplateId;
            messageHeader.Version = Generated.AuthGetUsersResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetUsersResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetUsersJson(model.UsersJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthGetConfigsRequest(DirectBuffer directBuffer, int offset, AuthGetConfigsRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetConfigsRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetConfigsRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetConfigsRequest.TemplateId;
            messageHeader.Version = Generated.AuthGetConfigsRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetConfigsRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.ModuleId = model.ModuleId;
            return message.Limit - offset;
        }

        public int EncodeAuthGetConfigsResponse(DirectBuffer directBuffer, int offset, AuthGetConfigsResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetConfigsResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetConfigsResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetConfigsResponse.TemplateId;
            messageHeader.Version = Generated.AuthGetConfigsResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetConfigsResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetConfigsJson(model.ConfigsJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthDeleteConfigRequest(DirectBuffer directBuffer, int offset, AuthDeleteConfigRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthDeleteConfigRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthDeleteConfigRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthDeleteConfigRequest.TemplateId;
            messageHeader.Version = Generated.AuthDeleteConfigRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthDeleteConfigRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.ConfigId = model.ConfigId;
            return message.Limit - offset;
        }

        public int EncodeAuthDeleteConfigResponse(DirectBuffer directBuffer, int offset, AuthDeleteConfigResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthDeleteConfigResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthDeleteConfigResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthDeleteConfigResponse.TemplateId;
            messageHeader.Version = Generated.AuthDeleteConfigResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthDeleteConfigResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetMessage(model.Message ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthConfigSave(DirectBuffer directBuffer, int offset, AuthConfigSaveModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthConfigSave.BlockLength;
            messageHeader.SchemaId = Generated.AuthConfigSave.SchemaId;
            messageHeader.TemplateId = Generated.AuthConfigSave.TemplateId;
            messageHeader.Version = Generated.AuthConfigSave.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthConfigSave();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.OwnerId = model.OwnerId;
            message.ModuleId = model.ModuleId;
            message.ConfigId = model.ConfigId;
            message.SaveTime = (ulong)model.SaveTime.Ticks;
            message.SetUsername(model.Username ?? "");
            message.SetTitle(model.Title ?? "");
            message.SetGroupName(model.GroupName ?? "");
            message.SetConfigJson(model.ConfigJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthConfigShare(DirectBuffer directBuffer, int offset, AuthConfigShareModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthConfigShare.BlockLength;
            messageHeader.SchemaId = Generated.AuthConfigShare.SchemaId;
            messageHeader.TemplateId = Generated.AuthConfigShare.TemplateId;
            messageHeader.Version = Generated.AuthConfigShare.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthConfigShare();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetConfigJson(model.ConfigJson ?? "");
            message.SetReceiverIdsJson(model.ReceiverIdsJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthGetDomListInfosRequest(DirectBuffer directBuffer, int offset, AuthGetDomListInfosRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetDomListInfosRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetDomListInfosRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetDomListInfosRequest.TemplateId;
            messageHeader.Version = Generated.AuthGetDomListInfosRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetDomListInfosRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            return message.Limit - offset;
        }

        public int EncodeAuthGetDomListInfosResponse(DirectBuffer directBuffer, int offset, AuthGetDomListInfosResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetDomListInfosResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetDomListInfosResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetDomListInfosResponse.TemplateId;
            messageHeader.Version = Generated.AuthGetDomListInfosResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetDomListInfosResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetDomListInfosJson(model.DomListInfosJson ?? "");
            return message.Limit - offset;
        }

        public int EncodeAuthGetCommissionsRequest(DirectBuffer directBuffer, int offset, AuthGetCommissionsRequestModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetCommissionsRequest.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetCommissionsRequest.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetCommissionsRequest.TemplateId;
            messageHeader.Version = Generated.AuthGetCommissionsRequest.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetCommissionsRequest();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            return message.Limit - offset;
        }

        public int EncodeAuthGetCommissionsResponse(DirectBuffer directBuffer, int offset, AuthGetCommissionsResponseModel model)
        {
            int bufferOffset = offset;
            MessageHeader messageHeader = new MessageHeader();
            messageHeader.Wrap(directBuffer, bufferOffset, MessageHeader.SbeSchemaVersion);
            messageHeader.BlockLength = Generated.AuthGetCommissionsResponse.BlockLength;
            messageHeader.SchemaId = Generated.AuthGetCommissionsResponse.SchemaId;
            messageHeader.TemplateId = Generated.AuthGetCommissionsResponse.TemplateId;
            messageHeader.Version = Generated.AuthGetCommissionsResponse.SchemaVersion;
            bufferOffset += MessageHeader.Size;

            var message = new Generated.AuthGetCommissionsResponse();
            message.WrapForEncode(directBuffer, bufferOffset);
            message.RequestId = model.RequestId;
            message.SetCommissionsJson(model.CommissionsJson ?? "");
            return message.Limit - offset;
        }

        #endregion
    }
}
