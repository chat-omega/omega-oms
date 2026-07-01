using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums.Contra;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;

namespace ZeroPlus.Models.Data.Models;

public class OrderTransactionModel : IResettable
{
    public long MsgSequence { get; set; }
    public string? PermId { get; set; }
    public string? OrderId { get; set; }
    public string? OriginalOrderId { get; set; }
    public string? Username { get; set; }
    public string? AccountAcronym { get; set; }
    public string? Symbol { get; set; }
    public string? UnderlyingSymbol { get; set; }
    public string? Route { get; set; }
    public int Side { get; set; }
    public string? Destination { get; set; }
    public int PositionEffect { get; set; }
    public string? RoutingSession { get; set; }
    public string? ClearingFirm { get; set; }
    public string? ClearingId { get; set; }
    public string? Source { get; set; }
    public string? Tag { get; set; }
    public int OrderStatus { get; set; }
    public string? ExchangeOrderId { get; set; }
    public string? ExecutingBroker { get; set; }
    public string? ExecutionId { get; set; }
    public string? ExecutionReferenceId { get; set; }
    public string? LastExchange { get; set; }
    public bool MultiLeg { get; set; }
    public int Quantity { get; set; }
    public int AccountId { get; set; }
    public int CumulativeQuantity { get; set; }
    public int LeavesQuantity { get; set; }
    public int LastQuantity { get; set; }
    public DateTime SubmitTime { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public DateTime Timestamp { get; set; }
    public int TransactionId { get; set; }
    public byte TIF { get; set; }
    public byte ExecutionType { get; set; }
    public int UnderBidSize { get; set; }
    public int UnderAskSize { get; set; }
    public int BidSize { get; set; }
    public int AskSize { get; set; }
    public string? TagType { get; set; }
    public string? TagSubType { get; set; }
    public string? TagComment { get; set; }
    public double Price { get; set; }
    public double AveragePrice { get; set; }
    public double LastPrice { get; set; }
    public double Fee1 { get; set; }
    public double Fee2 { get; set; }
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double UnderBid { get; set; }
    public double UnderAsk { get; set; }
    public double Tv { get; set; }
    public double Delta { get; set; }
    public double ExchangeFee1 { get; set; }
    public double ExchangeFee2 { get; set; }
    public double BrokerFee1 { get; set; }
    public double BrokerFee2 { get; set; }
    public double HwDelta { get; set; }
    public double HwTv { get; set; }
    public double HwGamma { get; set; }
    public double HwVega { get; set; }
    public double HwTheta { get; set; }
    public double HwRho { get; set; }
    public double HwIv { get; set; }
    public double HwUnder { get; set; }
    public double HwUBid { get; set; }
    public double HwUAsk { get; set; }
    public double HwBid { get; set; }
    public double HwAsk { get; set; }
    public double DeltaAdjTheo { get; set; }
    public double TimeValue { get; set; }
    public double IntrinsicValue { get; set; }
    public double FVDivs { get; set; }
    public double UFwd { get; set; }
    public double UFwdFactor { get; set; }
    public double BorrowCost { get; set; }
    public double BorrowRate { get; set; }
    public double UTheo { get; set; }
    public double TagEdge { get; set; }
    public double TagEma { get; set; }
    public double TagTv { get; set; }
    public double TagBid { get; set; }
    public double TagAsk { get; set; }
    public string? Exchanges { get; set; }
    public IList<ContraCapacity>? ContraCapacities { get; set; }
    public IList<ContraBrokerName>? ContraBrokerNames { get; set; }
    public IList<ContraCmta>? ContraCmtas { get; set; }
    public IList<ContraTrader>? ContraTraders { get; set; }

    public void CopyFrom(IOrder order)
    {
        MsgSequence = order.MsgSequence;
        PermId = order.PermID;
        OrderId = order.OrderID;
        OriginalOrderId = order.OriginalOrderID;
        Username = order.Username;
        AccountAcronym = order.AccountAcronym;
        Symbol = order.Symbol;
        UnderlyingSymbol = order.UnderlyingSymbol;
        Route = order.Route;
        Side = order.Side.HasValue ? (int)order.Side : -1;
        Destination = order.Destination;
        PositionEffect = (int)order.PositionEffect;
        RoutingSession = order.RoutingSession;
        ClearingFirm = order.ClearingFirm;
        ClearingId = order.ClearingID;
        Source = order.Source;
        Tag = order.Tag;
        OrderStatus = (int)order.OrderStatus;
        ExchangeOrderId = order.ExchangeOrderID;
        ExecutingBroker = order.ExecutingBroker;
        ExecutionId = order.ExecutionID;
        ExecutionReferenceId = order.ExecutionReferenceID;
        LastExchange = order.LastExchange;
        Exchanges = order.Exchanges;
        MultiLeg = order.IsComplexOrder;
        Quantity = order.Quantity;
        AccountId = order.AccountID;
        CumulativeQuantity = order.CumulativeQuantity;
        LeavesQuantity = order.LeavesQuantity;
        LastQuantity = order.LastQuantity;
        TransactionId = order.TransactionID;

        TIF = (byte)order.TimeInForce;
        ExecutionType = (byte)order.ExecutionType;
        UnderBidSize = order.UnderlyingBidSize;
        UnderAskSize = order.UnderlyingAskSize;
        BidSize = order.BidSize;
        AskSize = order.AskSize;
        TagType = order.Type;
        TagSubType = order.SubType?.ToSpacedString();
        TagComment = order.Comment;

        SubmitTime = order.SubmitTime == default ? CheckForDefault(order.LastUpdateTime) : order.SubmitTime;
        LastUpdateTime = CheckForDefault(order.LastUpdateTime);
        Timestamp = CheckForDefault(order.Timestamp);

        Price = CheckForNaN(order.Price);
        AveragePrice = CheckForNaN(order.AveragePrice);
        LastPrice = CheckForNaN(order.LastPrice);
        Fee1 = CheckForNaN(order.Fee1);
        Fee2 = CheckForNaN(order.Fee2);
        Bid = CheckForNaN(order.Bid);
        Ask = CheckForNaN(order.Ask);
        UnderBid = CheckForNaN(order.UnderBid);
        UnderAsk = CheckForNaN(order.UnderAsk);
        Tv = CheckForNaN(order.TV);
        Delta = CheckForNaN(order.Delta);
        ExchangeFee1 = CheckForNaN(order.ExchangeFee1);
        ExchangeFee2 = CheckForNaN(order.ExchangeFee2);
        BrokerFee1 = CheckForNaN(order.BrokerFee1);
        BrokerFee2 = CheckForNaN(order.BrokerFee2);
        HwDelta = CheckForNaN(order.TotalDelta);
        HwTv = CheckForNaN(order.HanweckTotalTheo);
        HwGamma = CheckForNaN(order.HanweckTotalGamma);
        HwVega = CheckForNaN(order.HanweckTotalVega);
        HwTheta = CheckForNaN(order.HanweckTotalTheta);
        HwRho = CheckForNaN(order.HanweckTotalRho);
        HwIv = CheckForNaN(order.HanweckTotalIV);
        HwUnder = CheckForNaN(order.HanweckTotalUnder);
        HwUBid = CheckForNaN(order.HanweckTotalUBid);
        HwUAsk = CheckForNaN(order.HanweckTotalUAsk);
        HwBid = CheckForNaN(order.HanweckTotalBid);
        HwAsk = CheckForNaN(order.HanweckTotalAsk);
        TimeValue = CheckForNaN(order.TimeValue);
        IntrinsicValue = CheckForNaN(order.IntrinsicValue);
        FVDivs = CheckForNaN(order.FVDivs);
        UFwd = CheckForNaN(order.UFwd);
        UFwdFactor = CheckForNaN(order.UFwdFactor);
        BorrowCost = CheckForNaN(order.BorrowCost);
        BorrowRate = CheckForNaN(order.BorrowRate);
        UTheo = CheckForNaN(order.UTheo);
        TagEdge = CheckForNaN(order.TagEdge);
        DeltaAdjTheo = CheckForNaN(order.DeltaAdjustedTheo);
        TagEma = CheckForNaN(order.TagEma);
        TagTv = CheckForNaN(order.TagTheo);
        TagBid = CheckForNaN(order.TagBid);
        TagAsk = CheckForNaN(order.TagAsk);

        ContraBrokerNames = order.ContraBrokerNames;
        ContraCapacities = order.ContraCapacities;
        ContraCmtas = order.ContraCmtas;
        ContraTraders = order.ContraTraders;
    }

    private DateTime CheckForDefault(DateTime value)
    {
        if (value == default || value == DateTime.UnixEpoch)
        {
            return DateTime.Today;
        }

        return value;
    }

    private static double CheckForNaN(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return value;
    }

    public bool TryReset()
    {
        return true;
    }
}