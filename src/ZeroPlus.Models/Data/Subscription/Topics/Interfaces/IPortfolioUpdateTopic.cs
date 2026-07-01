using System.Collections.Generic;
using ZeroPlus.Models.Data.Portfolio.Interfaces;

namespace ZeroPlus.Models.Data.Subscription.Topics.Interfaces
{
    public interface IPortfolioUpdateTopic : ITopic
    {
        int RequestId { get; }
        bool OneTimeUse { get; set; }
        bool IsUseSlim { get; set; }
        bool IgnoreBreakdownPositions { get; set; }

        void PositionAdded(IPortfolio portfolio, IPosition position);
        void PositionUpdate(IPortfolio portfolio, ICollection<IPosition> positions);
        void MultiplePortfoliosAdded(int requestId, ICollection<IPortfolio> portfolios);
        void MultiplePositionsAdded(int requestId, IPortfolio portfolio, List<IPosition> positions);
        void Clear();
    }
}