using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock
{
    public class AuctionPrint : ICobData
    {
        public CobDataType DataType { get; } = CobDataType.Auction;
        public bool FromCache { get; set; }
        public string? Underlying { get; set; }
        public string? Symbol { get; set; }
        public BaseStrategy BaseStrategy { get; set; }
        public string? SpreadId { get; set; }
        public string? SpreadDescription { get; set; }
        public string? CustAgentMPID { get; set; }
        public string? Industry { get; set; }
        public uint BidMask { get; set; }
        public uint AskMask { get; set; }
        public int BidSz { get; set; }
        public int AskSz { get; set; }
        public int PrtSize { get; set; }
        public int PrtSize2 { get; set; }
        public int CustQty { get; set; }
        public int NumOptLegs { get; set; }
        public int ExchBidSz { get; set; }
        public int ExchAskSz { get; set; }
        public double BidPrc { get; set; }
        public double AskPrc { get; set; }
        public double BidPrc10M { get; set; }
        public double AskPrc10M { get; set; }
        public double BidPrc1M { get; set; }
        public double AskPrc1M { get; set; }
        public double UAvgDailyVlm { get; set; }
        public double UPrc10M { get; set; }
        public double UPrc1M { get; set; }
        public double PrtSurfPrc { get; set; }
        public double PrtSurfVol { get; set; }
        public double CommEnhancement { get; set; }
        public double NetDe { get; set; }
        public double NetGa { get; set; }
        public double NetTh { get; set; }
        public double NetVe { get; set; }
        public double ExchAskPrc { get; set; }
        public double ExchBidPrc { get; set; }
        public double SurfVol1M { get; set; }
        public double SurfVol10M { get; set; }
        public double SurfPrc1M { get; set; }
        public double SurfPrc10M { get; set; }
        public double PkgAskPrc { get; set; }
        public double PkgBidPrc { get; set; }
        public double PkgSurfPrc { get; set; }
        public double UBid { get; set; }
        public double UAsk { get; set; }
        public double PrtUBid { get; set; }
        public double PrtUAsk { get; set; }
        public double PrtUPrc { get; set; }
        public double PrtPrice { get; set; }
        public double PrtPrice2 { get; set; }
        public double CustPrc { get; set; }
        public PrtType PrtType { get; set; }
        public AuctionSource AuctionSource { get; set; }
        public AuctionType AuctionType { get; set; }
        public FirmType CustFirmType { get; set; }
        public SpreadClass SpreadClass { get; set; }
        public SpreadFlavor SpreadFlavor { get; set; }
        public Side? CustSide { get; set; }
        public bool? ContainsFlex { get; set; }
        public bool? ContainsHedge { get; set; }
        public bool? ContainsMultiHedge { get; set; }
        public bool? HasCustPrc { get; set; }
        public bool? IsTestAuction { get; set; }
        public long Pkey { get; set; }
        public DateTime PrtTime { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime NoticeTime { get; set; }
        public DateTime TradeDate { get; set; }
        public List<AuctionPrintLeg> Legs { get; } = new List<AuctionPrintLeg>();

        public AuctionPrint()
        {

        }

        public AuctionPrint(AuctionPrint other)
        {
            FromCache = other.FromCache;
            Underlying = other.Underlying;
            Symbol = other.Symbol;
            BaseStrategy = other.BaseStrategy;
            SpreadId = other.SpreadId;
            SpreadDescription = other.SpreadDescription;
            CustAgentMPID = other.CustAgentMPID;
            Industry = other.Industry;
            BidMask = other.BidMask;
            AskMask = other.AskMask;
            BidSz = other.BidSz;
            AskSz = other.AskSz;
            PrtSize = other.PrtSize;
            PrtSize2 = other.PrtSize2;
            CustQty = other.CustQty;
            NumOptLegs = other.NumOptLegs;
            ExchBidSz = other.ExchBidSz;
            ExchAskSz = other.ExchAskSz;
            BidPrc = other.BidPrc;
            AskPrc = other.AskPrc;
            BidPrc10M = other.BidPrc10M;
            AskPrc10M = other.AskPrc10M;
            BidPrc1M = other.BidPrc1M;
            AskPrc1M = other.AskPrc1M;
            UAvgDailyVlm = other.UAvgDailyVlm;
            UPrc10M = other.UPrc10M;
            UPrc1M = other.UPrc1M;
            PrtSurfPrc = other.PrtSurfPrc;
            PrtSurfVol = other.PrtSurfVol;
            CommEnhancement = other.CommEnhancement;
            NetDe = other.NetDe;
            NetGa = other.NetGa;
            NetTh = other.NetTh;
            NetVe = other.NetVe;
            ExchAskPrc = other.ExchAskPrc;
            ExchBidPrc = other.ExchBidPrc;
            SurfVol1M = other.SurfVol1M;
            SurfVol10M = other.SurfVol10M;
            SurfPrc1M = other.SurfPrc1M;
            SurfPrc10M = other.SurfPrc10M;
            PkgAskPrc = other.PkgAskPrc;
            PkgBidPrc = other.PkgBidPrc;
            PkgSurfPrc = other.PkgSurfPrc;
            UBid = other.UBid;
            UAsk = other.UAsk;
            PrtUBid = other.PrtUBid;
            PrtUAsk = other.PrtUAsk;
            PrtUPrc = other.PrtUPrc;
            PrtPrice = other.PrtPrice;
            PrtPrice2 = other.PrtPrice2;
            CustPrc = other.CustPrc;
            PrtType = other.PrtType;
            AuctionSource = other.AuctionSource;
            AuctionType = other.AuctionType;
            CustFirmType = other.CustFirmType;
            SpreadClass = other.SpreadClass;
            SpreadFlavor = other.SpreadFlavor;
            CustSide = other.CustSide;
            ContainsFlex = other.ContainsFlex;
            ContainsHedge = other.ContainsHedge;
            ContainsMultiHedge = other.ContainsMultiHedge;
            HasCustPrc = other.HasCustPrc;
            IsTestAuction = other.IsTestAuction;
            Pkey = other.Pkey;
            PrtTime = other.PrtTime;
            Timestamp = other.Timestamp;
            NoticeTime = other.NoticeTime;
            TradeDate = other.TradeDate;

            foreach (var leg in other.Legs)
            {
                Legs.Add(new AuctionPrintLeg(leg));
            }
        }
    }
}
