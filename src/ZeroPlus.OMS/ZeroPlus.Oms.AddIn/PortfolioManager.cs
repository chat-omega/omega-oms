using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Models.Data.Update.Interfaces;

namespace ZeroPlus.Oms.AddIn;


public delegate void PositionUpdateHandler(SubscriptionFieldType type, IPosition position);
public class PortfolioManager : IPortfolioManager
{
    private readonly ConcurrentDictionary<Tuple<int, PortfolioType>, Portfolio> _idToPortfolioMap = [];

    public PositionUpdateHandler PositionUpdate { get; set; }

    public IPortfolio GetPortfolio(int id, PortfolioType portfolioType, int requestId = 0)
    {
        Tuple<int, PortfolioType> key = Tuple.Create(id, portfolioType);
        if (!_idToPortfolioMap.TryGetValue(key, out Portfolio portfolio))
        {
            portfolio = new Portfolio()
            {
                PortfolioType = portfolioType
            };
            _idToPortfolioMap[key] = portfolio;
        }
        return portfolio;
    }

    public void MultiplePortfoliosAdded(int requestId, HashSet<IPortfolio> portfolios)
    {
        if (requestId == 0)
        {
            foreach (IPortfolio portfolio in portfolios)
            {
                foreach (IPosition position in portfolio.Positions)
                {
                    PositionUpdated(portfolio, position, true);
                }
            }
        }
    }

    public void PortfolioAdded(IPortfolio portfolio)
    {
    }

    public void PositionAdded(IPortfolio portfolio, IPosition position)
    {
        PositionUpdated(portfolio, position, false);
    }

    public void PositionUpdated(IPortfolio portfolio, List<IPosition> positionsList, bool isReplay)
    {
        switch (portfolio.PortfolioType)
        {
            case PortfolioType.Firm when "Firm" == portfolio.Name:
                foreach (IPosition position in positionsList)
                {
                    HandleFirmPositionUpdate(position);
                }
                break;
        }
    }

    public void PositionUpdated(IPortfolio portfolio, IPosition position, bool isFromCache)
    {
        switch (portfolio.PortfolioType)
        {
            case PortfolioType.Firm when "Firm" == portfolio.Name:
                HandleFirmPositionUpdate(position);
                break;
        }
    }

    private void HandleFirmPositionUpdate(IPosition position)
    {
        try
        {
            switch (position.PositionType)
            {
                case PositionType.Symbol:
                    if (!string.IsNullOrWhiteSpace(position.Name))
                    {
                        PositionUpdate?.Invoke(SubscriptionFieldType.FirmSymbolPosition, position);
                    }
                    break;
            }
        }
        catch
        {
            // ignored
        }
    }

    public void SubmissionSummaryUpdate(SubmissionsSummary submissionsSummary)
    {
    }

    public ISymbolStatModel GetSymbolStatModel(int id)
    {
        return default;
    }

    public ISymbolStatModel GetSymbolStatModel(int id, string symbol)
    {
        return default;
    }

    public void SymbolStatModelUpdated(ISymbolStatModel model)
    {
    }

    public ISpreadRiskModel GetSpreadRiskModel(string spreadId)
    {
        return default;
    }

    public void SpreadRiskUpdate(ISpreadRiskModel model)
    {
    }

    public ISelfTradeModel GetSelfTradeWarningModel()
    {
        return default;
    }

    public void SelfTradeWarning(ISelfTradeModel model)
    {
    }

    public void MultiplePositionsAdded(int requestId, IPortfolio portfolio, List<IPosition> positionsList)
    {
    }
}