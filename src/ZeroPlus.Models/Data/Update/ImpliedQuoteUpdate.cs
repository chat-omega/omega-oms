using System;

namespace ZeroPlus.Models.Data.Update;

public record ImpliedQuoteUpdate()
{
    public int Index { get; set; }
    public string? Underlying { get; set; }
    public string? Symbol { get; set; }
    public double Bid { get; set; }
    public double Ask { get; set; }
    public double Theo { get; set; }
    public double UnderBid { get; set; }
    public double UnderAsk { get; set; }
    public double ImpliedBid { get; set; }
    public double ImpliedAsk { get; set; }
    public double ImpliedBidRecordPrice { get; set; }
    public double ImpliedBidRecordTheo { get; set; }
    public double ImpliedBidRecordMovement { get; set; }
    public double ImpliedBidRecordNonDeltaMovement { get; set; }
    public DateTime ImpliedBidRecordTime { get; set; }
    public double ImpliedAskRecordPrice { get; set; }
    public double ImpliedAskRecordTheo { get; set; }
    public double ImpliedAskRecordMovement { get; set; }
    public double ImpliedAskRecordNonDeltaMovement { get; set; }
    public DateTime ImpliedAskRecordTime { get; set; }
}