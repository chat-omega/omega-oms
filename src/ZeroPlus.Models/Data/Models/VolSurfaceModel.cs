using System;
using System.Collections.Generic;

namespace ZeroPlus.Models.Data.Models
{
    public record VolSurfaceRequestModel
    {
        public int RequestId { get; set; }
        public DateTime RequestTime { get; set; }
        public int SymbolId { get; set; }
        public int TenorIndex { get; set; } = -1;
    }

    public record VolaCurvePointModel
    {
        public double NormalizedStrike { get; set; }
        public double Volatility { get; set; }
        public double Strike { get; set; }
        public double TheoPrice { get; set; }
    }

    public record PutMarketDataModel
    {
        public double NormalizedStrike { get; set; }
        public double BidIV { get; set; }
        public double AskIV { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
    }

    public record CallMarketDataModel
    {
        public double NormalizedStrike { get; set; }
        public double BidIV { get; set; }
        public double AskIV { get; set; }
        public double Bid { get; set; }
        public double Ask { get; set; }
    }

    public record VolSurfacePointModel
    {
        public double Tenor { get; set; }
        public double Price { get; set; }
        public double Strike { get; set; }
        public double NormalizedStrike { get; set; }
    }

    public record VolSurfaceResponseModel
    {
        public int RequestId { get; set; }
        public bool Success { get; set; }
        public required DateTime MarketDataSnapshotTime { get; set; }
        public List<VolaCurvePointModel> VolaCurvePoints { get; set; } = new();
        public List<PutMarketDataModel> PutsMarketData { get; set; } = new();
        public List<CallMarketDataModel> CallsMarketData { get; set; } = new();
        public List<VolSurfacePointModel> SurfacePoints { get; set; } = new();
        public string? VolaJson { get; set; }
    }
}
