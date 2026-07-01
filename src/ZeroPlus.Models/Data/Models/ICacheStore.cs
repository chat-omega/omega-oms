using System.Threading.Tasks;
using ZeroPlus.Models.Data.Securities;

namespace ZeroPlus.Models.Data.Models;

public interface ICacheStore
{
    QuoteUpdateModel? QuoteUpdateModel { get; }

    Task CleanupCacheAsync();
    void SubscribeToData();
    Task InitializeAsync(Security security, DeltaAdjustedUnderlyingModel underlyingModel);
    bool TryGetClosestMatch(ulong reqTime, out ulong snapshotTime, out double update);
}