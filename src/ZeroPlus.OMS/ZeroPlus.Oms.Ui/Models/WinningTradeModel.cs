using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Oms.Clients;

namespace ZeroPlus.Oms.Ui.Models;

public partial class WinningTradeModel : ModelBase, IOmsDataSubscriber
{
    private readonly OmsCore _omsCore;
    private readonly PortfolioManagerModel _portfolioManager;
    private readonly HashSet<string> _tradersSet = new();

    public bool IsDisposed { get; set; }
    [Bindable]
    public partial string Underlying { get; set; }
    [Bindable]
    public partial string SpreadId { get; set; }
    [Bindable]
    public partial string Traders { get; set; }
    [Bindable]
    public partial string Symbol { get; set; }
    [Bindable]
    public partial HardSideKey? HardSideKey { get; set; }
    [Bindable]
    public partial DateTime LastEdgeTradeTime { get; set; }
    [Bindable]
    public partial DateTime LastTradeTime { get; set; }
    [Bindable]
    public partial DateTime LastHardSideAttemptTime { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double HighestEdge { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double BuyEdgeToTheo { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double SellEdgeToTheo { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double BuyPrice { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double SellPrice { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double BuyUnderPrice { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double SellUnderPrice { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double AdjBuyPrice { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double AdjSellPrice { get; set; }
    [Bindable]
    public partial double LastTradeDelta { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double LastTradeUnder { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double UnderlyingChange { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double MinutesSinceEdgeTrade { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double MinutesSinceLastTrade { get; set; }
    [Bindable(Default = double.NaN)]
    public partial double MinutesSinceLastHardSideAttempt { get; set; }
    [Bindable]
    public partial Side? LastTradeSide { get; set; }
    [Bindable]
    public partial Side? HardSide { get; set; }
    [Bindable]
    public partial double UnderlyingMid { get; set; }

    public WinningTradeModel(OmsCore omsCore, PortfolioManagerModel portfolioManager)
    {
        _omsCore = omsCore;
        _portfolioManager = portfolioManager;
    }

    public void Update()
    {
        MinutesSinceEdgeTrade = (DateTime.Now - LastEdgeTradeTime).TotalMinutes;
        MinutesSinceLastTrade = (DateTime.Now - LastTradeTime).TotalMinutes;
        MinutesSinceLastHardSideAttempt = (DateTime.Now - LastHardSideAttemptTime).TotalMinutes;
    }

    private void AdjustPrices()
    {
        AdjBuyPrice = (UnderlyingMid - BuyUnderPrice) * LastTradeDelta + BuyPrice;
        AdjSellPrice = (UnderlyingMid - SellUnderPrice) * LastTradeDelta + SellPrice;
        UnderlyingChange = UnderlyingMid - LastTradeUnder;
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
    {
        switch (key.Type)
        {
            case SubscriptionFieldType.MidPoint when key.Symbol == Underlying && value is double midValue:
                UnderlyingMid = midValue;
                AdjustPrices();
                break;
            case SubscriptionFieldType.HardSide when value is HardSideResult hardSideResult:
                HardSide = hardSideResult.HardSide;
                break;
        }
    }

    public void Subscribe()
    {
        _omsCore.QuoteClient.Subscribe(Underlying, SubscriptionFieldType.MidPoint, this);
        if (HardSideKey != null)
        {
            _portfolioManager.Subscribe(HardSideKey.ToString(), SubscriptionFieldType.HardSide, this);
        }
    }

    public void Unsubscribe()
    {
        _omsCore.QuoteClient.Unsubscribe(Underlying, SubscriptionFieldType.MidPoint, this);
        if (HardSideKey != null)
        {
            _portfolioManager.Unsubscribe(HardSideKey.ToString(), SubscriptionFieldType.HardSide, this);
        }
    }

    public void AddTrader(string trader)
    {
        if (string.IsNullOrWhiteSpace(trader))
        {
            return;
        }

        if (_tradersSet.Add(trader))
        {
            Traders = string.Join(", ", _tradersSet);
        }
    }
}