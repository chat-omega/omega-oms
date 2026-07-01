using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock;

public class AuctionPrintLeg
{
    public string? LegSymbol { get; set; }
    public SpdrKeyType LegSecType { get; set; }
    public Side? LegSide { get; set; }
    public ExpiryType LegExpType { get; set; }
    public uint LegBidMask { get; set; }
    public uint LegAskMask { get; set; }
    public uint LegRatio { get; set; }
    public int LegBidSz { get; set; }
    public int LegAskSz { get; set; }
    public int LegUndPerCn { get; set; }
    public double LegPointValue { get; set; }
    public double LegYears { get; set; }
    public double LegRate { get; set; }
    public double LegAtmVol { get; set; }
    public double LegDdivPv { get; set; }
    public double LegTVol { get; set; }
    public double LegSVol { get; set; }
    public double LegSDiv { get; set; }
    public double LegSPrc { get; set; }
    public double LegDe { get; set; }
    public double LegGa { get; set; }
    public double LegTh { get; set; }
    public double LegVe { get; set; }
    public double LegBid { get; set; }
    public double LegAsk { get; set; }
    public bool? LegSVolOk { get; set; }

    public AuctionPrintLeg()
    {
    }

    public AuctionPrintLeg(AuctionPrintLeg other)
    {
        LegSymbol = other.LegSymbol;
        LegSecType = other.LegSecType;
        LegSide = other.LegSide;
        LegExpType = other.LegExpType;
        LegBidMask = other.LegBidMask;
        LegAskMask = other.LegAskMask;
        LegRatio = other.LegRatio;
        LegBidSz = other.LegBidSz;
        LegAskSz = other.LegAskSz;
        LegUndPerCn = other.LegUndPerCn;
        LegPointValue = other.LegPointValue;
        LegYears = other.LegYears;
        LegRate = other.LegRate;
        LegAtmVol = other.LegAtmVol;
        LegDdivPv = other.LegDdivPv;
        LegTVol = other.LegTVol;
        LegSVol = other.LegSVol;
        LegSDiv = other.LegSDiv;
        LegSPrc = other.LegSPrc;
        LegDe = other.LegDe;
        LegGa = other.LegGa;
        LegTh = other.LegTh;
        LegVe = other.LegVe;
        LegBid = other.LegBid;
        LegAsk = other.LegAsk;
        LegSVolOk = other.LegSVolOk;
    }
}