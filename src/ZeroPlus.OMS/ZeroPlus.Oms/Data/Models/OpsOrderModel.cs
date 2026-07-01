using System;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;
using PositionEffect = ZeroPlus.Models.Data.Enums.PositionEffect;
using Side = ZeroPlus.Models.Data.Enums.Side;
using TimeInForce = ZeroPlus.Models.Data.Enums.TimeInForce;

namespace ZeroPlus.Oms.Data.Models
{
    public class OpsOrderModel : OMSOrder, IOrderSlim
    {
        private TimeInForce _timeInForce;
        public TimeInForce TimeInForce
        {
            get => _timeInForce;
            set
            {
                _timeInForce = value;
                Tif = value.ToString();
            }
        }
        public bool IsComplexOrder { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public string Currency { get; set; }
        public string SpreadId { get; set; }
        public Security Security { get; set; }
        public Side? Side { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
        public int Quantity { get; set; }
        public OrderSubType? SubType { get; set; }
        public double Multiplier { get; set; }
        public string Destination { get; set; }
        public uint DestinationSequence { get; set; }
        public string AccountAcronym { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public double NewToCancelTime { get; set; }
        public string Comment { get; set; }
        public Venue? Venue { get; set; }
        public OrderSource OrderSource { get; set; }
        public DateTime SubmitTime { get; set; }
        public string RouteOverride { get; set; }
        public string PrimaryExchange { get; set; }
        public double Mid { get; set; }
        public double Ema { get; set; }
        public double TotalDelta { get; set; }
        public double HanweckTotalTheo { get; set; }
        public double DeltaAdjustedTheo { get; set; }
        public double VolaTheo { get; set; }
        public double VolaTheoAdj { get; set; }
        public double VolaIv { get; set; }
        public double TheoBid { get; set; }
        public double TheoAsk { get; set; }
        public double UnderBid { get; set; }
        public double UnderMid => (UnderBid + UnderAsk) / 2;
        public double UnderAsk { get; set; }
        public string SmartRoute { get; set; }
        public double AdjustedEdgeOverride { get; set; }
        public double EdgeOverride { get; set; }
        public double CloseUnderBid { get; set; }
        public double CloseUnderAsk { get; set; }
        public double AveragePrice { get; set; }
        public double TagEdge { get; set; }
        public bool SkipNewPriceEvaluation { get; set; }
        public bool IsGTH { get; set; }
        public OrderTagModel OrderTag { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string RiskCheckMessage { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; }
        public SubType SubTypeId { get; set; }
        public ushort SubTypeSequence { get; set; }
        public StockHedgeOrderModel StockHedgeOrderModel { get; set; }
        public new uint UserId { get; set; }
        public EdgeType EdgeType { get; set; }
        public double DigBid { get; set; }
        public double DigAsk { get; set; }
        public uint DigBidSize { get; set; }
        public uint DigAskSize { get; set; }
        public double WeightedVega { get; set; }
        public double CloseEdgeOverride { get; set; } = double.NaN;
        public ulong IoiId { get; set; }

        public void SetCancelDelay(double cancelDelay)
        {
            CancelDelay = cancelDelay;
            NewToCancelTime = cancelDelay;
        }
    }
}
