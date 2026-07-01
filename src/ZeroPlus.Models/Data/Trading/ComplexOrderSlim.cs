using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Trading;

public class ComplexOrderSlim : OrderSlim, IComplexOrderSlim
{
    private readonly object _lock;
    private readonly ConcurrentDictionary<int, IComplexOrderLeg> _legIdToLegMap = new ConcurrentDictionary<int, IComplexOrderLeg>();
    public HashSet<IComplexOrderLeg> Legs { get; set; }

    public ComplexOrderSlim()
    {
        _lock = new object();
        Legs = new HashSet<IComplexOrderLeg>();
        IsComplexOrder = true;
    }

    public ComplexOrderSlim(ISecurityBook? securityBook) : base(securityBook)
    {
        _lock = new object();
        Legs = new HashSet<IComplexOrderLeg>();
        IsComplexOrder = true;
    }

    public static ComplexOrderSlim Clone(ComplexOrderSlim orderSlim, bool invertSide = false)
    {
        var order = new ComplexOrderSlim(orderSlim.SecurityBook)
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
            StockHedgeOrderModel = orderSlim.StockHedgeOrderModel
        };

        foreach (var leg in orderSlim.Legs)
        {
            var newLeg = order.GetLeg(leg.LegID);
            newLeg.Clone(leg);
        }

        if (invertSide)
        {
            order.Reverse();
        }

        return order;
    }

    protected override void Reverse()
    {
        base.Reverse();
        foreach (var leg in Legs)
        {
            switch (leg.Side)
            {
                case Enums.Side.Buy:
                case Enums.Side.BuyToCover:
                    leg.Side = Enums.Side.Sell;
                    break;
                case Enums.Side.Sell:
                case Enums.Side.SellShort:
                    leg.Side = Enums.Side.Buy;
                    break;
                case null:
                    break;
            }
        }
    }

    public IComplexOrderLeg GetLeg(string? legId)
    {
        if (string.IsNullOrWhiteSpace(legId))
        {
            legId = "";
        }
        int id = GetHashCode() + legId.GetHashCode();
        if (!_legIdToLegMap.TryGetValue(id, out IComplexOrderLeg? orderLeg))
        {
            orderLeg = new ComplexOrderLeg(SecurityBook);
            _legIdToLegMap[id] = orderLeg;
            lock (_lock)
            {
                Legs.Add(orderLeg);
            }
        }
        return orderLeg;
    }
}