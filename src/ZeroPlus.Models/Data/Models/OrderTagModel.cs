using System;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public class OrderTagModel
    {
        public DateTime OrderDate { get; set; }
        public string? PermId { get; set; }
        public string? Trader { get; set; }
        public string? Instance { get; set; }
        public string? ParentSpreadHash { get; set; }

        public double Bid { get; set; } = double.NaN;
        public double Ask { get; set; } = double.NaN;
        public uint BidSize { get; set; }
        public uint AskSize { get; set; }

        public double Theo { get; set; } = double.NaN;
        public double Ema { get; set; } = double.NaN;
        public double Edge { get; set; } = double.NaN;
        public EdgeType EdgeType { get; set; }

        public double VolaTheo { get; set; } = double.NaN;
        public double VolaTheoAdj { get; set; } = double.NaN;
        public double VolaIv { get; set; } = double.NaN;
        public double TheoBid { get; set; } = double.NaN;
        public double TheoAsk { get; set; } = double.NaN;

        public double UnderBid { get; set; } = double.NaN;
        public double UnderAsk { get; set; } = double.NaN;
        public uint UnderBidSize { get; set; }
        public uint UnderAskSize { get; set; }

        public double DigBid { get; set; } = double.NaN;
        public double DigAsk { get; set; } = double.NaN;
        public uint DigBidSize { get; set; }
        public uint DigAskSize { get; set; }
        public double WeightedVega { get; set; } = double.NaN;

        public OrderSource OrderSource { get; set; }
        public ModuleType ModuleType { get; set; }
        public SubType SubType { get; set; }
        public ulong SharedId { get; set; }
        public ushort Sequence { get; set; }
        public ushort SubTypeSequence { get; set; }

        public OrderSubType OrderSubType { get; set; }

        public uint ResubmitCount { get; set; }
        public uint TotalEstimatedResubmit { get; set; }

        public ushort SessionId { get; set; }
    }
}
