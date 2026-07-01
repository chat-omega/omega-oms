using System;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading;

public class OrderSlim : IOrderSlim
{
    private string? _symbol;
    public bool IsComplexOrder { get; set; }
    public Venue? Venue { get; set; }
    public OrderSource OrderSource { get; set; }
    public DateTime SubmitTime { get; set; }
    public BaseStrategy BaseStrategy { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public string? Currency { get; set; }
    public string? SpreadId { get; set; }
    public Security? Security { get; set; }
    public Side? Side { get; set; }
    public MinimumTickStyle MinimumTickStyle { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
    public string? Tag { get; set; }
    public string? RouteOverride { get; set; }
    public string? PrimaryExchange { get; set; }
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
    public double DigBid { get; set; } = double.NaN;
    public double DigAsk { get; set; } = double.NaN;
    public uint DigBidSize { get; set; }
    public uint DigAskSize { get; set; }
    public double WeightedVega { get; set; } = double.NaN;
    public double UnderBid { get; set; }
    public double UnderMid { get; set; }
    public double UnderAsk { get; set; }
    public OrderSubType? SubType { get; set; }
    public string? SmartRoute { get; set; }
    public double AdjustedEdgeOverride { get; set; }
    public double EdgeOverride { get; set; }
    public double CloseEdgeOverride { get; set; } = double.NaN;
    public double CloseUnderBid { get; set; }
    public double CloseUnderAsk { get; set; }
    public double AveragePrice { get; set; }
    public string? Route { get; set; }
    public string? LocalID { get; set; }
    public double Multiplier { get; set; }
    public string? Destination { get; set; }
    public uint DestinationSequence { get; set; }
    public double TagEdge { get; set; }
    public string? AccountAcronym { get; set; }
    public TimeInForce TimeInForce { get; set; }
    public PositionEffect PositionEffect { get; set; }
    public double NewToCancelTime { get; set; }
    public string? Comment { get; set; }
    public ISecurityBook? SecurityBook { get; set; }
    public bool SkipNewPriceEvaluation { get; set; }
    public bool IsGTH { get; set; }
    public OrderTagModel? OrderTag { get; set; }
    public uint UserId { get; set; }
    public uint RiskCheckId { get; set; }
    public bool RiskCheckPassed { get; set; }
    public string? RiskCheckMessage { get; set; }
    public ulong IoiId { get; set; }
    public ulong SharedId { get; set; }
    public ushort Sequence { get; set; }
    public ModuleType TypeId { get; set; }
    public SubType SubTypeId { get; set; }
    public ushort SubTypeSequence { get; set; }
    public StockHedgeOrderModel? StockHedgeOrderModel { get; set; }
    public EdgeType EdgeType { get; set; }

    public string? Symbol
    {
        get => _symbol;
        set
        {
            _symbol = value;
            if (!IsComplexOrder && !string.IsNullOrEmpty(value))
            {
                Security = SecurityBook?.GetSecurity(value);
            }
        }
    }

    public OrderSlim()
    {
    }

    public OrderSlim(ISecurityBook? securityBook)
    {
        SecurityBook = securityBook;
    }

    public static OrderSlim Clone(OrderSlim orderSlim, bool invertSide = false)
    {
        if (orderSlim is ComplexOrderSlim complexOrder)
        {
            return ComplexOrderSlim.Clone(complexOrder, invertSide);
        }

        var order = new OrderSlim(orderSlim.SecurityBook)
        {
            IsComplexOrder = orderSlim.IsComplexOrder,
            Venue = orderSlim.Venue,
            BaseStrategy = orderSlim.BaseStrategy,
            UnderlyingSymbol = orderSlim.UnderlyingSymbol,
            Currency = orderSlim.Currency,
            SpreadId = orderSlim.SpreadId,
            Security = orderSlim.Security,
            Side = orderSlim.Side,
            MinimumTickStyle = orderSlim.MinimumTickStyle,
            Quantity = orderSlim.Quantity,
            Price = orderSlim.Price,
            Tag = orderSlim.Tag,
            RouteOverride = orderSlim.RouteOverride,
            PrimaryExchange = orderSlim.PrimaryExchange,
            Bid = orderSlim.Bid,
            Mid = orderSlim.Mid,
            Ask = orderSlim.Ask,
            Ema = orderSlim.Ema,
            TotalDelta = orderSlim.TotalDelta,
            HanweckTotalTheo = orderSlim.HanweckTotalTheo,
            DeltaAdjustedTheo = orderSlim.DeltaAdjustedTheo,
            VolaTheo = orderSlim.VolaTheo,
            VolaTheoAdj = orderSlim.VolaTheoAdj,
            VolaIv = orderSlim.VolaIv,
            TheoBid = orderSlim.TheoBid,
            TheoAsk = orderSlim.TheoAsk,
            DigBid = orderSlim.DigBid,
            DigAsk = orderSlim.DigAsk,
            DigBidSize = orderSlim.DigBidSize,
            DigAskSize = orderSlim.DigAskSize,
            WeightedVega = orderSlim.WeightedVega,
            UnderBid = orderSlim.UnderBid,
            UnderMid = orderSlim.UnderMid,
            UnderAsk = orderSlim.UnderAsk,
            SubType = orderSlim.SubType,
            SmartRoute = orderSlim.SmartRoute,
            AdjustedEdgeOverride = orderSlim.AdjustedEdgeOverride,
            EdgeOverride = orderSlim.EdgeOverride,
            CloseEdgeOverride = orderSlim.CloseEdgeOverride,
            CloseUnderBid = orderSlim.CloseUnderBid,
            CloseUnderAsk = orderSlim.CloseUnderAsk,
            AveragePrice = orderSlim.AveragePrice,
            Route = orderSlim.Route,
            LocalID = orderSlim.LocalID,
            Multiplier = orderSlim.Multiplier,
            Destination = orderSlim.Destination,
            DestinationSequence = orderSlim.DestinationSequence,
            TagEdge = orderSlim.TagEdge,
            AccountAcronym = orderSlim.AccountAcronym,
            TimeInForce = orderSlim.TimeInForce,
            PositionEffect = orderSlim.PositionEffect,
            NewToCancelTime = orderSlim.NewToCancelTime,
            Comment = orderSlim.Comment,
            SecurityBook = orderSlim.SecurityBook,
            SkipNewPriceEvaluation = orderSlim.SkipNewPriceEvaluation,
            IsGTH = orderSlim.IsGTH,
            OrderTag = orderSlim.OrderTag,
            UserId = orderSlim.UserId,
            RiskCheckId = orderSlim.RiskCheckId,
            IoiId = orderSlim.IoiId,
            SharedId = orderSlim.SharedId,
            Sequence = orderSlim.Sequence,
            TypeId = orderSlim.TypeId,
            SubTypeId = orderSlim.SubTypeId,
            SubTypeSequence = orderSlim.SubTypeSequence,
            StockHedgeOrderModel = orderSlim.StockHedgeOrderModel,
            Symbol = orderSlim.Symbol,
        };

        if (invertSide)
        {
            order.Reverse();
        }

        return order;
    }

    protected virtual void Reverse()
    {
        switch (Side)
        {
            case Enums.Side.Buy:
            case Enums.Side.BuyToCover:
                Side = Enums.Side.Sell;
                break;
            case Enums.Side.Sell:
            case Enums.Side.SellShort:
                Side = Enums.Side.Buy;
                break;
            case null:
                break;
        }
    }
}