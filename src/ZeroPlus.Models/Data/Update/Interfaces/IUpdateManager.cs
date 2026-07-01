using Generated;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;

namespace ZeroPlus.Models.Data.Update.Interfaces
{
    public interface IUpdateManager
    {
        UnderFitResult? GetUnderFitResultModel(uint index);
        DerivedValueUpdateModel? GetDerivedValueUpdateModel(int tickerId);
        void HandleUpdate(UnderFitResult model);
        void HandleUpdate(DerivedValueUpdateModel model);
        void HandleUpdate(GreekUpdateModel model);
        void HandleUpdate(SlimGreekUpdateModel model);
        void HandleUpdate(ref AdjTheoUpdate theoUpdate);
        void HandleUpdate(ref TradeUpdateModel tradeUpdate);
        void HandleUpdate(ref DeltaAdjustedTheoDetailsModel deltaAdjustedTheoDetails);
        void HandleUpdate(TimeFeedType timeFeedType,
                          DateTime timeUpdate);
        void HandleUpdate(string symbol,
                          SubscriptionFieldType updateType,
                          double update,
                          double bidUpdate,
                          double askUpdate);
        void HandleUpdate(SubscriptionFieldType updateType,
                          Dictionary<int, (double update, double bidUpdate, double askUpdate)> indexToUpdateMap);
        void HandleUpdate(int tickerId,
                          SubscriptionFieldType fieldType,
                          uint updateSequence,
                          ulong underlyingTimestamp,
                          ulong snapshotTimestamp,
                          ulong hanweckTimestamp,
                          double theo,
                          double delta,
                          double gamma,
                          double vega,
                          double theta,
                          double rho,
                          double implied,
                          double latestMidPrice,
                          double snapshotMidPrice,
                          double deltaAdjustedTheo,
                          bool jumpDetected);
        void HandleUpdate(int tickerId,
                          SubscriptionFieldType updateType,
                          double bidUpdate,
                          double askUpdate,
                          DateTime timestamp,
                          QuoteChangeType bidChange,
                          QuoteChangeType askChange,
                          int bidSize,
                          int askSize,
                          double lastPrice,
                          double latencyMs = 0);
        void HandleUpdate(int tickerId,
                          SubscriptionFieldType updateType,
                          EmaUpdateModel emaUpdate);
        void HandleUpdate(int tickerId,
                          SubscriptionFieldType updateType,
                          double value);
    }
}