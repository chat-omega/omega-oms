using System;

namespace ZeroPlus.Models.Data.Models;

public partial class EdgeCalcLogModel
{
    public DateTime TimestampUtc { get; set; }

    public int EdgeType { get; set; }

    public string? ConfigId { get; set; }

    public string? SpreadId { get; set; }

    public string? OrderId { get; set; }

    public short Result { get; set; }

    public double PreBid { get; set; }

    public double PreAsk { get; set; }

    public double PreUnderBid { get; set; }

    public double PreUnderAsk { get; set; }

    public double PreDelta { get; set; }

    public double PreTheo { get; set; }

    public double PreAdjTheo { get; set; }

    public double PreVolaTheo { get; set; }

    public double PreVolaTheoAdj { get; set; }

    public double PreVolaIv { get; set; }

    public double PreTheoBid { get; set; }

    public double PreTheoAsk { get; set; }

    public double PreEma { get; set; }

    public double PostBid { get; set; }

    public double PostAsk { get; set; }

    public double PostUnderBid { get; set; }

    public double PostUnderAsk { get; set; }

    public double PostDelta { get; set; }

    public double PostTheo { get; set; }

    public double PostAdjTheo { get; set; }

    public double PostVolaTheo { get; set; }

    public double PostVolaTheoAdj { get; set; }

    public double PostVolaIv { get; set; }

    public double PostTheoBid { get; set; }

    public double PostTheoAsk { get; set; }

    public double PostEma { get; set; }

    public double AveragePrice { get; set; }

    public double CloseUnderBid { get; set; }

    public double CloseUnderAsk { get; set; }

    public double Price { get; set; }
}
