using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Models
{
    public record PricingRequestLeg
    {
        public int TickerId { get; set; }
        public Side Side { get; set; }
        public uint Ratio { get; set; }
    }

    public record PricingRequestModel
    {
        public uint RequestId { get; set; }
        public List<PricingRequestLeg> Legs { get; } = new();
    }

    public record PricingResponseModel
    {
        public uint RequestId { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
        public double HwTheo { get; set; }
        public double HwAdjTheo { get; set; }
        public double HwDelta { get; set; }
        public double VolaTheo { get; set; }
        public double VolaAdjTheo { get; set; }
        public double AdjVolaEma { get; set; }
        public double AdjDaEma { get; set; }
        public double UnderBid { get; set; }
        public double UnderAsk { get; set; }
    }

}
