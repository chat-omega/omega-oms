using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;

namespace ZeroPlus.Models.Data.Portfolio.Interfaces
{
    public interface IPortfolioManager
    {
        IPortfolio GetPortfolio(int id, PortfolioType portfolioType, int requestId = 0);
        void MultiplePortfoliosAdded(int requestId, HashSet<IPortfolio> portfolios);
        void PortfolioAdded(IPortfolio portfolio);
        void PositionAdded(IPortfolio portfolio, IPosition position);
        void PositionUpdated(IPortfolio portfolio, List<IPosition> positionsList, bool isReplay);
        void SubmissionSummaryUpdate(SubmissionsSummary submissionsSummary);
        
        ISymbolStatModel? GetSymbolStatModel(int id);
        ISymbolStatModel? GetSymbolStatModel(int id, string symbol);
        void SymbolStatModelUpdated(ISymbolStatModel model);
        
        ISpreadRiskModel? GetSpreadRiskModel(string spreadId);
        void SpreadRiskUpdate(ISpreadRiskModel model);
        ISelfTradeModel? GetSelfTradeWarningModel();
        void SelfTradeWarning(ISelfTradeModel model);
        void MultiplePositionsAdded(int requestId, IPortfolio portfolio, List<IPosition> positionsList);
    }
}
