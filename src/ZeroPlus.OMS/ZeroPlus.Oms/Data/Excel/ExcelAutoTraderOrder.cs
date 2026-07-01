using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Data.Excel
{
    [Serializable]
    public class ExcelAutoTraderOrder : IComplexOrderSlim
    {
        private readonly ISecurityBook _securityBook;
        public ExcelAutoTraderOrder(ISecurityBook securityBook)
        {
            _securityBook = securityBook;
        }
        public ExcelAutoTraderOrder(ISecurityBook securityBook, string symbol)
        {
            _securityBook = securityBook;
            Symbol = symbol;
            this.AddLegs(_securityBook, out string underlyingSymbol);
            UnderlyingSymbol = underlyingSymbol;
        }

        public bool IsComplexOrder { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public string Symbol { get; set; }
        public string UnderlyingSymbol { get; set; }
        public string Currency { get; set; }
        public string SpreadId { get; set; }
        public Security Security { get; set; }
        public Side? Side { get; set; }
        public MinimumTickStyle MinimumTickStyle { get; set; }
        public int Quantity { get; set; }
        public double Price { get; set; }
        #region Properties
        public string Tag { get; set; }
        public OrderSubType? SubType { get; set; }
        public string Route { get; set; }
        public string LocalID { get; set; }
        public double Multiplier { get; set; }
        public string Destination { get; set; }
        public uint DestinationSequence { get; set; }
        public string AccountAcronym { get; set; }
        public TimeInForce TimeInForce { get; set; }
        public PositionEffect PositionEffect { get; set; }
        public double NewToCancelTime { get; set; }
        public string Comment { get; set; }
        public Venue? Venue { get; set; }
        public OrderSource OrderSource { get; set; }
        public string RouteOverride { get; set; }
        public string PrimaryExchange { get; set; }
        public double Bid { get; set; }
        public double Mid { get; set; }
        public double Ask { get; set; }
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
        public double UnderMid { get; set; }
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
        public DateTime SubmitTime { get; set; }

        #endregion
        public HashSet<IComplexOrderLeg> Legs { get; set; } = new();
        public OrderTagModel OrderTag { get; set; }

        public IComplexOrderLeg GetLeg(string legId) => Legs.FirstOrDefault(l => l.LegID == legId);
        public uint UserId { get; set; }
        public uint RiskCheckId { get; set; }
        public bool RiskCheckPassed { get; set; }
        public string RiskCheckMessage { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ModuleType TypeId { get; set; } = ModuleType.Dominator;
        public SubType SubTypeId { get; set; }
        public ushort SubTypeSequence { get; set; }
        public StockHedgeOrderModel StockHedgeOrderModel { get; set; }
        public EdgeType EdgeType { get; set; }
        public double DigBid { get; set; }
        public double DigAsk { get; set; }
        public uint DigBidSize { get; set; }
        public uint DigAskSize { get; set; }
        public double WeightedVega { get; set; }
        public double CloseEdgeOverride { get; set; } = double.NaN;
        public ulong IoiId { get; set; }
    }
}
