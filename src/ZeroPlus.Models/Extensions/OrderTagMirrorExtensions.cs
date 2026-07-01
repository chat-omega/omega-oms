using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Extensions
{
    public static class OrderTagMirrorExtensions
    {
        public static void MirrorEnrichmentToTag(this IOrderSlim order)
        {
            var tag = order.OrderTag;
            if (tag == null)
            {
                return;
            }

            tag.Bid = order.Bid;
            tag.Ask = order.Ask;
            tag.Theo = order.DeltaAdjustedTheo;
            tag.Ema = order.Ema;
            tag.Edge = order.TagEdge;
            tag.EdgeType = order.EdgeType;
            tag.VolaTheo = order.VolaTheo;
            tag.VolaTheoAdj = order.VolaTheoAdj;
            tag.VolaIv = order.VolaIv;
            tag.TheoBid = order.TheoBid;
            tag.TheoAsk = order.TheoAsk;
            tag.UnderBid = order.UnderBid;
            tag.UnderAsk = order.UnderAsk;
            tag.DigBid = order.DigBid;
            tag.DigAsk = order.DigAsk;
            tag.DigBidSize = order.DigBidSize;
            tag.DigAskSize = order.DigAskSize;
            tag.WeightedVega = order.WeightedVega;
        }
    }
}
