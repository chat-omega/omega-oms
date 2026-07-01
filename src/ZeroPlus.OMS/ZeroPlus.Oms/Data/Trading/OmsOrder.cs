using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;
using Side = ZeroPlus.Models.Data.Enums.Side;
using TimeInForce = ZeroPlus.Models.Data.Enums.TimeInForce;

namespace ZeroPlus.Oms.Data.Trading
{
    [Serializable]
    public class OmsOrder : IOmsOrder
    {
        [JsonIgnore]
        public string Exchanges { get; set; }
        public double SpreadAvgPrice { get; set; }
        public string Guid { get; set; }
        public string Username { get; set; }
        public string AccountAcronym { get; set; }
        public string Symbol { get; set; }
        public string UnderlyingSymbol { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public DateTime SubmitTime { get; set; }
        public int LastQuantity { get; set; }
        public int FilledQty { get; set; }
        public int LeavesQuantity { get; set; }
        public int CumulativeQuantity { get; set; }
        public double AveragePrice { get; set; } = double.NaN;
        public Side? Side { get; set; }
        public string SideString { get; set; }
        public double Price { get; set; } = double.NaN;
        public int Quantity { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string SpreadId { get; set; }
        public string SpreadType { get; set; }
        public int TransactionID { get; set; }
        public string Source { get; set; }
        public string Tag { get; set; }
        public string Trader { get; set; }
        public double TagEdge { get; set; }
        public double TagMid { get; set; }
        public double TagBid { get; set; }
        public double TagAsk { get; set; }
        public double TagTheo { get; set; }
        public string Type { get; set; }
        public string Subtype { get; set; }
        public double TagEma { get; set; }
        public string Comment { get; set; }
        public string FullTag { get; set; }
        public string ExchangeOrderID { get; set; }
        public string ExecutingBroker { get; set; }
        public string ExecutionID { get; set; }
        public string ExecutionReferenceID { get; set; }
        public string LastExchange { get; set; }
        public double Fee1 { get; set; }
        public double Fee2 { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
        public double TV { get; set; }
        public double Delta { get; set; }
        public double ExchangeFee1 { get; set; }
        public double ExchangeFee2 { get; set; }
        public double BrokerFee1 { get; set; }
        public double BrokerFee2 { get; set; }
        public string PermID { get; set; }
        public string OrderID { get; set; }
        public string OriginalOrderID { get; set; }
        public string RequestAccountAcronym { get; set; }
        public string Route { get; set; }
        public string RequestSymbol { get; set; }
        public string Destination { get; set; }
        public string PositionEffect { get; set; }
        public string RoutingSession { get; set; }
        public string ClearingFirm { get; set; }
        public string ClearingID { get; set; }
        public int AccountID { get; set; }
        public bool MultiLeg { get; set; }
        public int Position { get; set; }
        public double RealizedPnL { get; set; }
        public double AdjustedPnl { get; set; }
        public double TotalContracts { get; set; }
        public double LegsCount { get; set; }
        public double FillTime { get; set; } = double.NaN;
        public double TradeToNewTime { get; set; } = double.NaN;
        public double SubmitToNewTime { get; set; } = double.NaN;
        public double NewToCancelTime { get; set; } = double.NaN;
        public double BidPercentOfFillPrice { get; set; }
        public double CloseBidPercentOfFillPrice { get; set; }
        public double OmsBidPercentOfFillPrice { get; set; }
        public double TotalDelta { get; set; }
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
        public double CloseTV { get; set; }
        public double CloseDelta { get; set; }
        public double CloseTotalDelta { get; set; }
        public double CloseHanweckTotalTheo { get; set; }
        public double CloseHanweckTotalGamma { get; set; }
        public double CloseHanweckTotalVega { get; set; }
        public double CloseHanweckTotalTheta { get; set; }
        public double CloseHanweckTotalRho { get; set; }
        public double CloseHanweckTotalIV { get; set; }
        public double CloseBid { get; set; }
        public double CloseAsk { get; set; }
        public double CloseUnderBid { get; set; }
        public double CloseUnderAsk { get; set; }
        public double CloseHanweckTotalUnder { get; set; }
        public double CloseHanweckTotalUBid { get; set; }
        public double CloseHanweckTotalUAsk { get; set; }
        public double CloseHanweckTotalBid { get; set; }
        public double CloseHanweckTotalAsk { get; set; }
        public double DeltaAdjustedTheo { get; set; } = double.NaN;
        public double CloseDeltaAdjustedTheo { get; set; } = double.NaN;
        public int BidSize { get; set; }
        public int AskSize { get; set; }
        public int CloseBidSize { get; set; }
        public int CloseAskSize { get; set; }
        public int UnderlyingBidSize { get; set; }
        public int UnderlyingAskSize { get; set; }
        public int CloseUnderlyingBidSize { get; set; }
        public int CloseUnderlyingAskSize { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public double UnderMid => (UnderAsk + UnderBid) * 0.5;
        public DateTime NewStatusTimeStamp { get; set; } = DateTime.MinValue;
        public List<OmsOrderLeg> Legs { get; set; } = new List<OmsOrderLeg>();
        public int DataRequestMask { get; set; }
        public Dictionary<string, string> UserData { get; set; } = new Dictionary<string, string>();
        public double EdgeOverride { get; set; } = double.NaN;
        public double AdjustedEdgeOverride { get; set; } = double.NaN;
        public double EdgeCurveAdjustment { get; set; } = double.NaN;
        public double TotalCommissions => Fee1 + Fee2 + BrokerFee1 + BrokerFee2 + ExchangeFee1 + ExchangeFee2;
        public bool IsFill => OrderStatus.IsFilled();
        public double PriceImprovement => IsFill ? Price - AveragePrice : double.NaN;
        [JsonIgnore]
        public List<IOmsOrderLeg> OrderLegs => Legs.Select(x => (IOmsOrderLeg)x).ToList();
        [JsonIgnore]
        public List<IOmsOrderLeg> TradedLegs { get; set; } = new List<IOmsOrderLeg>();
        [JsonIgnore]
        public List<IOrderUpdate> OrderUpdates { get; set; } = new List<IOrderUpdate>();

    }
}
