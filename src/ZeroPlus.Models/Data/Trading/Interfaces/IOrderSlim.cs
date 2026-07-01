using System;
using System.ComponentModel.DataAnnotations;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading.Interfaces;

public interface IOrderSlim : IHaveRisk
{
    [Required]
    bool IsComplexOrder { get; }
    [Required]
    BaseStrategy BaseStrategy { get; set; }
    [Required]
    string? Symbol { get; set; }
    [Required]
    string? UnderlyingSymbol { get; set; }
    string? Currency { get; set; }
    [Required]
    string? SpreadId { get; set; }
    [Required]
    Security? Security { get; set; }
    [Required]
    Side? Side { get; set; }
    [Required]
    MinimumTickStyle MinimumTickStyle { get; set; }
    [Required]
    int Quantity { get; set; }
    [Required]
    double Price { get; set; }
    [Required]
    string? Tag { get; set; }
    [Required]
    OrderSubType? SubType { get; set; }
    [Required]
    string? Route { get; set; }
    [Required]
    string? LocalID { get; set; }
    [Required]
    double Multiplier { get; set; }
    [Required]
    string? Destination { get; set; }
    [Required]
    uint DestinationSequence { get; set; }
    [Required]
    string? AccountAcronym { get; set; }
    [Required]
    TimeInForce TimeInForce { get; set; }
    [Required]
    PositionEffect PositionEffect { get; set; }
    [Required]
    double NewToCancelTime { get; set; }
    [Required]
    string? Comment { get; set; }
    Venue? Venue { get; set; }
    OrderSource OrderSource { get; set; }
    DateTime SubmitTime { get; set; }
    string? RouteOverride { get; set; }
    string? PrimaryExchange { get; set; }
    double Bid { get; set; }
    double Mid { get; set; }
    double Ask { get; set; }
    double Ema { get; set; }
    double TotalDelta { get; set; }
    double HanweckTotalTheo { get; set; }
    double DeltaAdjustedTheo { get; set; }
    double VolaTheo { get; set; }
    double VolaTheoAdj { get; set; }
    double VolaIv { get; set; }
    double TheoBid { get; set; }
    double TheoAsk { get; set; }
    double DigBid { get; set; }
    double DigAsk { get; set; }
    uint DigBidSize { get; set; }
    uint DigAskSize { get; set; }
    double WeightedVega { get; set; }
    double UnderBid { get; set; }
    double UnderMid { get; }
    double UnderAsk { get; set; }
    string? SmartRoute { get; set; }
    double AdjustedEdgeOverride { get; set; }
    double EdgeOverride { get; set; }
    double CloseEdgeOverride { get; set; }
    double CloseUnderBid { get; set; }
    double CloseUnderAsk { get; set; }
    double AveragePrice { get; set; }
    double TagEdge { get; set; }
    EdgeType EdgeType { get; set; }
    bool SkipNewPriceEvaluation { get; set; }
    bool IsGTH { get; set; }
    OrderTagModel? OrderTag { get; set; }
    uint UserId { get; set; }
    uint RiskCheckId { get; set; }
    ulong IoiId { get; set; }
    ulong SharedId { get; set; }
    ushort Sequence { get; set; }
    ModuleType TypeId { get; set; }
    SubType SubTypeId { get; set; }
    ushort SubTypeSequence { get; set; }
    StockHedgeOrderModel? StockHedgeOrderModel { get; set; }
}