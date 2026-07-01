using Microsoft.Extensions.ObjectPool;
using System;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Data.Models;

public class LegTransactionModel : IResettable
{
    public string? PermId { get; set; }
    public string? OrderId { get; set; }
    public string? ExecutionId { get; set; }
    public string? LegId { get; set; }
    public string? Symbol { get; set; }
    public string? LastExchange { get; set; }
    public int ParentOrderId { get; set; }
    public int PositionEffect { get; set; }
    public int Side { get; set; }
    public int OrderStatus { get; set; }
    public int TransactionId { get; set; }
    public int Ratio { get; set; }
    public int Quantity { get; set; }
    public int LeavesQuantity { get; set; }
    public int LastQuantity { get; set; }
    public int CumulativeQuantity { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime HwTimestamp { get; set; }
    public DateTime HwBidTime { get; set; }
    public DateTime HwAskTime { get; set; }
    public int BidSize { get; set; }
    public int AskSize { get; set; }
    public double LastPrice { get; set; }
    public double AveragePrice { get; set; }
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Tv { get; set; }
    public double Delta { get; set; }
    public double Fee1 { get; set; }
    public double Fee2 { get; set; }
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

    public void CopyFrom(IComplexOrderLeg leg)
    {
        PermId = leg.PermID;
        OrderId = leg.OrderID;
        ExecutionId = leg.ExecutionID;
        LegId = leg.LegID;
        Symbol = leg.Symbol;
        PositionEffect = (int)leg.PositionEffect;
        Side = leg.Side.HasValue ? (int)leg.Side : -1;
        OrderStatus = (int)leg.OrderStatus;
        TransactionId = leg.TransactionID;
        Ratio = leg.Ratio;
        Quantity = leg.Quantity;
        LeavesQuantity = leg.LeavesQuantity;
        LastQuantity = leg.LastQuantity;
        CumulativeQuantity = leg.CumulativeQuantity;
        BidSize = leg.BidSize;
        AskSize = leg.AskSize;

        LastUpdateTime = CheckForDefault(leg.LastUpdateTime);
        Timestamp = CheckForDefault(leg.Timestamp);
        HwTimestamp = CheckForDefault(leg.HanweckTimestamp);
        HwBidTime = CheckForDefault(leg.HanweckBidTime);
        HwAskTime = CheckForDefault(leg.HanweckAskTime);

        LastPrice = CheckForNaN(leg.LastPrice);
        AveragePrice = CheckForNaN(leg.AveragePrice);
        Bid = CheckForNaN(leg.Bid);
        Ask = CheckForNaN(leg.Ask);
        Tv = CheckForNaN(leg.TV);
        Delta = CheckForNaN(leg.Delta);
        Fee1 = CheckForNaN(leg.Fee1);
        Fee2 = CheckForNaN(leg.Fee2);
        ExchangeFee1 = CheckForNaN(leg.ExchangeFee1);
        ExchangeFee2 = CheckForNaN(leg.ExchangeFee2);
        BrokerFee1 = CheckForNaN(leg.BrokerFee1);
        BrokerFee2 = CheckForNaN(leg.BrokerFee2);
        HwDelta = CheckForNaN(leg.Delta);
        HwTv = CheckForNaN(leg.TV);
        HwGamma = CheckForNaN(leg.HanweckGamma);
        HwVega = CheckForNaN(leg.HanweckVega);
        HwTheta = CheckForNaN(leg.HanweckTheta);
        HwRho = CheckForNaN(leg.HanweckRho);
        HwIv = CheckForNaN(leg.HanweckIV);
        HwUnder = CheckForNaN(leg.HanweckUnder);
        HwUBid = CheckForNaN(leg.HanweckUnderBid);
        HwUAsk = CheckForNaN(leg.HanweckUnderAsk);
        HwBid = CheckForNaN(leg.HanweckBid);
        HwAsk = CheckForNaN(leg.HanweckAsk);
        DeltaAdjTheo = CheckForNaN(leg.DeltaAdjustedTheo);
        TimeValue = CheckForNaN(leg.TimeValue);
        IntrinsicValue = CheckForNaN(leg.IntrinsicValue);
        FVDivs = CheckForNaN(leg.FVDivs);
        UFwd = CheckForNaN(leg.UFwd);
        UFwdFactor = CheckForNaN(leg.UFwdFactor);
        BorrowCost = CheckForNaN(leg.BorrowCost);
        BorrowRate = CheckForNaN(leg.BorrowRate);
        UTheo = CheckForNaN(leg.UTheo);
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