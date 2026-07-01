using System.Threading.Tasks;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Topics;

namespace ZeroPlus.Models.Data.Models;

public interface IDeltaAdjustedOption
{
    int Index { get; }
    Option? Option { get; }
    DeltaAdjustedUnderlyingModel? DeltaAdjustedUnderlyingModel { get; }

    uint UpdateSequence { get; }
    long SnapshotTicks { get; set; }
    ulong SnapshotTimestamp { get; }
    ulong HanweckTimestamp { get; }
    double Theo { get; set; }
    double Delta { get; set; }
    double Gamma { get; }
    double Vega { get; }
    double WeightedVega { get; }
    double Theta { get; }
    double Implied { get; }
    double SnapshotMidPrice { get; }
    double DeltaAdjustedTheo { get; set; }
    double DaEma { get; }
    double AdjDaEma { get; }
    double VolaEma { get; }
    double AdjVolaEma { get; }

    ICacheStore? MidPriceCacheStore { get; }
    IDeltaAdjustedOption? AtmOption { get; set; }
    TheoUpdateTopics? TheoTopics { get; set; }
    ulong LastUpdateTimestamp { get; }
    double LastUpdateMid { get; }
    bool JumpDetected { get; }
    double SmoothedDeltaAdjustedTheo { get; set; }
    double Multiplier { get; }
    TheoResult VolaTheoResult { get; }
    double VolaReferenceUnderlying { get; }

    void StartLogging();
    bool Adjust(uint sequence, ulong timestamp, double mid);
    Task CleanCacheAsync();
    void InitializeFromModel(Option option, DeltaAdjustedUnderlyingModel deltaAdjustedUnderlyingModel, ICacheStore _midPriceCacheStore);
    void SetVolaValues();
}