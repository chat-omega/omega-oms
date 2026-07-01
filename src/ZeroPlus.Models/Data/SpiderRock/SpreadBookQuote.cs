using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.SpiderRock
{
    public class SpreadBookQuote : ICobData
    {
        public CobDataType DataType { get; } = CobDataType.Quote;

        public bool FromCache { get; set; }
        public bool IsBidPrice1Valid { get; set; }
        public bool IsAskPrice1Valid { get; set; }
        public bool IsBidPrice2Valid { get; set; }
        public bool IsAskPrice2Valid { get; set; }

        public SrExch BidExch1 { get; set; }
        public SrExch AskExch1 { get; set; }
        public SrUpdateType UpdateType { get; set; }

        public uint BidMask1 { get; set; }
        public uint AskMask1 { get; set; }

        public int BidSize1 { get; set; }
        public int AskSize1 { get; set; }
        public int BidSize2 { get; set; }
        public int AskSize2 { get; set; }
        public int PrintVolume { get; set; }

        public double BidPrice1 { get; set; }
        public double AskPrice1 { get; set; }
        public double BidPrice2 { get; set; }
        public double AskPrice2 { get; set; }

        public DateTime BidTime { get; set; }
        public DateTime AskTime { get; set; }
        public DateTime Timestamp { get; set; }

        public long SrcTimestamp { get; set; }
        public long NetTimestamp { get; set; }
        public long SpreadKey { get; set; }

        public string? Underlying { get; set; }
        public string? Symbol { get; set; }

        public BaseStrategy BaseStrategy { get; set; }
        public string? SpreadId { get; set; }
        public string? SpreadDescription { get; set; }

        public SpreadBookQuote()
        {
        }

        public SpreadBookQuote(SpreadBookQuote other)
        {
            FromCache = other.FromCache;
            IsBidPrice1Valid = other.IsBidPrice1Valid;
            IsAskPrice1Valid = other.IsAskPrice1Valid;
            IsBidPrice2Valid = other.IsBidPrice2Valid;
            IsAskPrice2Valid = other.IsAskPrice2Valid;
            BidExch1 = other.BidExch1;
            AskExch1 = other.AskExch1;
            UpdateType = other.UpdateType;
            BidMask1 = other.BidMask1;
            AskMask1 = other.AskMask1;
            BidSize1 = other.BidSize1;
            AskSize1 = other.AskSize1;
            BidSize2 = other.BidSize2;
            AskSize2 = other.AskSize2;
            PrintVolume = other.PrintVolume;
            BidPrice1 = other.BidPrice1;
            AskPrice1 = other.AskPrice1;
            BidPrice2 = other.BidPrice2;
            AskPrice2 = other.AskPrice2;
            BidTime = other.BidTime;
            AskTime = other.AskTime;
            Timestamp = other.Timestamp;
            SrcTimestamp = other.SrcTimestamp;
            NetTimestamp = other.NetTimestamp;
            SpreadKey = other.SpreadKey;
            Underlying = other.Underlying;
            Symbol = other.Symbol;
            BaseStrategy = other.BaseStrategy;
            SpreadId = other.SpreadId;
            SpreadDescription = other.SpreadDescription;
        }
    }
}
