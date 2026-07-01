using System.Collections.Generic;
using ZeroPlus.Models.Data.Edge;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces;

public interface ISymbolPermEdgeToTheoMappingUpdateTopic : ITopic
{
    void Init(string symbol);
    void Update(List<EdgeToTheoTrackerModel> models);
}