using Newtonsoft.Json;

namespace ZeroPlus.Models.Data.EdgeScanner;

public interface IEdgeScanFeedTraderSettings
{
    [JsonProperty]
    bool PreviousAttemptCrossCheckEnabled { get; set; }
    [JsonProperty]
    bool MinEdgeToPreviousAttemptCheckEnabled { get; set; }
    [JsonProperty]
    double MinEdgeToMarketCheckEdge { get; set; }
    [JsonProperty]
    bool MinEdgeToMarketCheckEnabled { get; set; }
    [JsonProperty]
    double CancelWithTimer { get; set; }
    [JsonProperty]
    bool MinTimeToPermLoserCheckEnabled { get; set; }
    [JsonProperty]
    bool MinTimeToPreviousAttemptCheckEnabled { get; set; }
    [JsonProperty]
    bool MinBidCheckEnabled { get; set; }
    [JsonProperty]
    double MinBidCheckBidValue { get; set; }
    [JsonProperty]
    double MinTimeToPreviousAttemptIntervalSeconds { get; set; }
    [JsonProperty]
    double MinTimeToPermLoserIntervalSeconds { get; set; }
}