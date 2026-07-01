using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Models.Data.Trading.Interfaces;

public interface IStatsProcessor
{
    void EdgeScanFeedStatsUpdate(IEdgeScanFeedStatisticsSummary model);
    IEdgeScanFeedStatisticsSummary? GetEdgeScanFeedStatisticsModel(string instanceId);
    void HandleUpdate(string id, SubscriptionFieldType type, List<ChartValueModel> updatesList);
}