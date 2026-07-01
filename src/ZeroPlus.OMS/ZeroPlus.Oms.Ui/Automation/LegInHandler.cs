using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Automation;

public class LegInHandler : IAutomation
{
    private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

    private BasketTraderItemModel _ticket;

    private double _fillPx;
    private double _fillUnderlying;
    private int _initialQty;
    private bool _priceLocked;
    private readonly SpreadsGeneratorConfig _generatorConfig;
    private bool _spreadsLoaded;
    private AutomationConfigModel _automationConfig;

    public OrderSubType? SubType { get; }
    private List<Walker> Walkers { get; }
    public double Increment => Math.Max(_automationConfig.ContraFishPriceIncrement, (double)_ticket.GetPriceIncrement());
    public double SecondaryIncrement => Increment;
    public double Interval => _automationConfig.ContraFishIntervalMax > _automationConfig.ContraFishInterval
        ? Random.Shared.Next(_automationConfig.ContraFishInterval, _automationConfig.ContraFishIntervalMax + 1)
        : _automationConfig.ContraFishInterval;

    public LegInHandler(BasketTraderItemModel ticket)
    {
        _ticket = ticket;

        _generatorConfig = new SpreadsGeneratorConfig
        {
            WholeStrikes = true,
            DecimalStrikes = true,
            Regulars = true,
            NonRegulars = true,
            Quarterlies = true,
            DiagonalEnabled = true,
            DiagonalSpreadsSettings = new DiagonalSpreadsGeneratorSettingsModel(),
        };

        SubType = OrderSubType.ThreeWayCloser;
        Walkers = new List<Walker>();
    }

    public void LockPrices(double fillPx, double fillUnderlying, int qty)
    {
        _fillPx = fillPx;
        _fillUnderlying = fillUnderlying;
        _initialQty = qty;
        _priceLocked = true;
    }

    public async Task<bool> Start(OrderSubType? subType = null, double restOverride = double.NaN)
    {
        if (_ticket.IsActive)
        {
            return false;
        }

        if (!_ticket.IsSingleLeg)
        {
            _ticket.Status = "Leg In not supported on spreads!";
            return false;
        }

        _automationConfig =
            _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying,
                (double)_ticket.PriceIncrement);

        if (!_spreadsLoaded)
        {
            await GenerateSpreads();
        }

        if (_priceLocked)
        {
            StartSendingClosers();
        }
        else if (_ticket.BasketSettings.LegInSettings.FishForLiquidityOnStart)
        {
            return await Task.Run(function: () =>
                _ticket.SubmitOrderAsync(isContra: false, resting: false, subType, cancelDelay: restOverride));
        }
        else
        {
            _ticket.Status = "Fish disabled and no price locked!";
        }

        return false;
    }

    private void StartSendingClosers()
    {
        if (_spreadsLoaded)
        {
            _ticket.IsLooping = true;
            RestartAsync();
        }
    }

    private async Task GenerateSpreads()
    {
        DisposeWalkers();
        Walkers.Clear();

        var targetLeg = _ticket.Legs[0];
        var targetSymbol = targetLeg.Symbol;

        var legInSettings = _ticket.BasketSettings.LegInSettings;
        _generatorConfig.MaxCountEnabled = true;
        _generatorConfig.UnderlyingQuery = _ticket.Underlying;
        _generatorConfig.MaxCount = legInSettings.SpreadsCount;
        _generatorConfig.CallsEnabled = _ticket.PutCall == PutCall.Call;
        _generatorConfig.PutsEnabled = _ticket.PutCall == PutCall.Put;
        _generatorConfig.Leg2LockEnabled = true;
        _generatorConfig.Leg2LockOptions = targetSymbol;

        var diagonalGeneratorSettings = _generatorConfig.DiagonalSpreadsSettings;
        diagonalGeneratorSettings!.Leg1DeltaRangeEnabled = true;
        diagonalGeneratorSettings.Leg1DeltaRangeFloor = legInSettings.CheapoDeltaRangeMin;
        diagonalGeneratorSettings.Leg1DeltaRangeCeil = legInSettings.CheapoDeltaRangeMax;
        diagonalGeneratorSettings.Leg1WidthRangeFloor = legInSettings.CheapoWidthRangeMin;
        diagonalGeneratorSettings.Leg1WidthRangeCeil = legInSettings.CheapoWidthRangeMax;
        diagonalGeneratorSettings.Leg1Ratio = legInSettings.Ratio1;
        diagonalGeneratorSettings.Leg2Ratio = legInSettings.Ratio2;

        if (_ticket.IsSingleLeg)
        {
            var spreads = await _ticket.OmsCore.SymbolMapClient.Client.GenerateSpreadsAsync(_generatorConfig, CancellationToken.None);
            var symbols = spreads.SelectMany(x => x.Spreads).Select(x => x.Symbol).ToList();

            var edge = Math.Max(_automationConfig.ContraFishEdge, legInSettings.CushionValue);
            var maxLoss = _ticket.GetLoopMaxLoss();
            var minEdge = Math.Max(_ticket.GetLoopMinEdge(), legInSettings.MinEdge);

            var tasks = new List<Task<Walker>>();
            for (var index = 0; index < symbols.Count; index++)
            {
                var spreadSymbol = symbols[index];
                var walker = CreateLegInWalker(index, spreadSymbol, edge, minEdge);
                tasks.Add(walker);
            }

            await Task.WhenAll(tasks);
            Walkers.AddRange(tasks.Select(x => x.Result));

            var directWalker = await CreateDirectWalker(edge, maxLoss);
            Walkers.Add(directWalker);

            _spreadsLoaded = Walkers.Any();
        }
    }

    private async Task<Walker> CreateLegInWalker(int index, string spreadSymbol, double edge, double minEdge)
    {
        var targetLeg = _ticket.Legs[0];
        var targetSymbol = targetLeg.Symbol;

        var legInSettings = _ticket.BasketSettings.LegInSettings;
        var spreadLegs = _ticket.ParseFromTos(spreadSymbol);
        var secondaryLegs = _ticket.ParseFromTos(targetSymbol);

        var comment = BuildTrackerTag(spreadLegs, secondaryLegs);

        var initialQty = _priceLocked ? _initialQty : 1;

        var matchingLeg = spreadLegs.FirstOrDefault(x => x.Symbol == targetSymbol);
        var cheapoLeg = spreadLegs.FirstOrDefault(x => x.Symbol != targetSymbol);
        var reverseSide = matchingLeg!.Side == targetLeg.Side;
        BuildOrder(spreadLegs, reverseSide, initialQty, comment, out var mainOrderCalculator);

        var cheapoLegQty = cheapoLeg!.Ratio * initialQty;
        BuildOrder([cheapoLeg], !reverseSide, cheapoLegQty, comment, out var legOutCheapoCalculator);

        var secondaryMatchingLeg = secondaryLegs.FirstOrDefault(x => x.Symbol == targetSymbol);
        var reverseSecondarySide = secondaryMatchingLeg?.Side != _ticket.Side;
        var closeQty = matchingLeg!.Quantity - initialQty;
        BuildOrder(secondaryLegs, reverseSecondarySide, closeQty, comment, out var legOutPosCalculator);

        Walker walker = new(_ticket, this, _ticket.OmsCore, SubType, mainOrderCalculator, legOutPosCalculator, legOutCheapoCalculator, _ticket.Multiplier)
        {
            Index = index,
            Spacing = spreadLegs.Count > 1 ? Math.Abs(spreadLegs[0].Strike.Strike - spreadLegs[1].Strike.Strike) : 0
        };

        if (!walker.SecondaryOrder.PriceCalculator.QuoteLoadedNotifier.IsSet)
        {
            await walker.SecondaryOrder.PriceCalculator.QuoteLoadedNotifier.WaitForLoadAsync();
        }
        if (!walker.MainOrder.PriceCalculator.QuoteLoadedNotifier.IsSet)
        {
            await walker.MainOrder.PriceCalculator.QuoteLoadedNotifier.WaitForLoadAsync();
        }

        if (_ticket.Side == Side.Buy)
        {
            walker.MainOrder.NextPriceCalculator = () =>
            {
                var adjFill = ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
                var cheapoWidth = legInSettings.AdjustForCheapoWidth
                    ? cheapoLeg!.Ask - cheapoLeg.Bid
                    : 0;
                return (-1 * (adjFill + edge + cheapoWidth) * matchingLeg.Ratio) +
                       (cheapoLeg!.Ask * cheapoLeg.Ratio);
            };
            walker.MainOrder.StopPriceCalculator = () =>
            {
                var adjFill = ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
                var cheapoWidth = legInSettings.AdjustForCheapoWidth
                    ? cheapoLeg!.Ask - cheapoLeg.Bid
                    : 0;
                return (-1 * (adjFill + minEdge + cheapoWidth) * matchingLeg.Ratio) +
                       (cheapoLeg!.Ask * cheapoLeg.Ratio);
            };
            walker.SecondaryOrder.NextPriceCalculator = () => ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
            walker.SecondaryOrder.StopPriceCalculator = () => _fillPx + _automationConfig.LoopMaxLoss;
            var cheapoLegLockedBid = cheapoLeg.Bid;
            walker.TertiaryOrder.NextPriceCalculator = () => cheapoLegLockedBid;
            walker.TertiaryOrder.OrderMaxResubmit = legInSettings.CheapoMaxResubmit;
        }
        else
        {
            walker.MainOrder.NextPriceCalculator = () =>
            {
                var adjFill = ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
                var cheapoWidth = legInSettings.AdjustForCheapoWidth
                    ? cheapoLeg!.Ask - cheapoLeg.Bid
                    : 0;
                return ((adjFill - edge - cheapoWidth) * matchingLeg.Ratio) -
                       (cheapoLeg!.Bid * cheapoLeg.Ratio);
            };
            walker.MainOrder.StopPriceCalculator = () =>
            {
                var adjFill = ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
                var cheapoWidth = legInSettings.AdjustForCheapoWidth
                    ? cheapoLeg!.Ask - cheapoLeg.Bid
                    : 0;
                return ((adjFill - minEdge - cheapoWidth) * matchingLeg.Ratio) -
                       (cheapoLeg!.Bid * cheapoLeg.Ratio);
            };
            walker.SecondaryOrder.NextPriceCalculator = () => ((_ticket.UnderMid - _fillUnderlying) * _ticket.TotalDelta) + _fillPx;
            walker.SecondaryOrder.StopPriceCalculator = () => _fillPx - _automationConfig.LoopMaxLoss;
            var cheapoLegLockedAsk = cheapoLeg.Ask;
            walker.TertiaryOrder.NextPriceCalculator = () => cheapoLegLockedAsk;
            walker.TertiaryOrder.OrderMaxResubmit = legInSettings.CheapoMaxResubmit;
        }

        return walker;
    }

    private async Task<Walker> CreateDirectWalker(double edge, double maxLoss)
    {
        var qty = _priceLocked ? _initialQty : _ticket.Lcd;
        var mainOrder = _ticket.BuildOrder(isContra: true, SubType, qty);
        var tasks = new List<Task<TicketLegModel>>();
        foreach (var leg in _ticket.Legs)
        {
            TicketLegModel legClone = new(_ticket.OmsCore, leg.Underlying, leg.Account, leg.ParentBasket, leg.PortfolioManager);
            var load = legClone.LoadFromTemplateAsync(leg);
            tasks.Add(load);
        }
        await Task.WhenAll(tasks);
        List<TicketLegModel> legs = tasks.Select(x => x.Result).ToList();
        PxCalculator calc = new(_ticket.Lcd, legs, mainOrder);
        var directWalker = new Walker(_ticket, this, _ticket.OmsCore, SubType, calc, _ticket.Multiplier)
        {
            Index = Walkers.Count
        };
        directWalker.MainOrder.PriceCalculator.Order.Qty = qty;
        if (_ticket.Side == Side.Buy)
        {
            directWalker.MainOrder.NextPriceCalculator = () => _fillPx + edge;
            directWalker.MainOrder.StopPriceCalculator = () => _fillPx - maxLoss;
        }
        else
        {
            directWalker.MainOrder.NextPriceCalculator = () => _fillPx - edge;
            directWalker.MainOrder.StopPriceCalculator = () => _fillPx + maxLoss;
        }

        return directWalker;
    }

    private void BuildOrder(List<TicketLegModel> spreadLegs, bool reverseSide, int initialQty, string comment, out PxCalculator pxCalculator)
    {
        var order = spreadLegs.Count == 1
            ? _ticket.BuildSingleLegOrder(isContra: reverseSide, leg: spreadLegs.First(),
                SubType, qty: initialQty, comment: comment)
            : _ticket.BuildMultiLegOrder(isContra: reverseSide, validLegs: spreadLegs,
                SubType, stampValues: true, overrideTheo: double.NaN, qty: initialQty,
                comment: comment);
        order.Route = _ticket.ApplyBrokerPrefix(_ticket.GetBestRoute(false, _ticket.InstanceMode, _ticket.IsBasketOrder,
            _ticket.IsStockTied, _ticket.IsStockTicket, spreadLegs.Count == 1, _ticket.Underlying));
        order.SetCancelDelay(Interval);
        pxCalculator = new(initialQty, spreadLegs, order);
    }

    private string BuildTrackerTag(List<TicketLegModel> spreadLegs, List<TicketLegModel> secondaryLegs)
    {
        List<string> symbolsKey =
        [
            "[" + _ticket.SpreadSymbol + "]",
            "[" + IAutomation.GetTosSymbol(spreadLegs, invert: true) + "]",
            "[" + IAutomation.GetTosSymbol(secondaryLegs) + "]"
        ];

        string payLoad = "3 Way - " + _ticket.SpreadId + " - " + string.Join(", ", symbolsKey);
        var comment = payLoad.CompressString();
        return comment;
    }

    public void ShowStatus(OrderUpdateModel execReport, OrderStatus status)
    {
        OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

        _ticket.ContraOrderStatus = status;
        _ticket.ContraStatus = "[LI] " + orderUpdateValues.Status;
        _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
    }

    public async Task ContinueAsync(Walker walker)
    {
        int nextWalkerIndex = walker.Index + 1;
        if (nextWalkerIndex < Walkers.Count)
        {
            await SendNextWalker(nextWalkerIndex);
        }
        else
        {
            await RestartAsync();
        }
    }

    private async Task<bool> RestartAsync()
    {
        bool started = await SendNextWalker(nextWalkerIndex: 0);
        return started;
    }

    private async Task<bool> SendNextWalker(int nextWalkerIndex)
    {
        var started = false;
        for (int i = nextWalkerIndex; i < Walkers.Count; i++)
        {
            started = await StartWalkerAsync(i);
            if (started)
            {
                break;
            }
        }
        return started;
    }

    private async Task<bool> StartWalkerAsync(int nextWalkerIndex)
    {
        if (Walkers.Count > nextWalkerIndex)
        {
            var nextWalker = Walkers[nextWalkerIndex];
            bool started = await nextWalker.SendPrimaryOrder();
            return started;
        }
        else
        {
            return false;
        }
    }

    public void Stop()
    {
        _priceLocked = false;
        _spreadsLoaded = false;
        DisposeWalkers();
    }

    internal void Dispose()
    {
        DisposeWalkers();
        _ticket = null;
    }

    private void DisposeWalkers()
    {
        try
        {
            foreach (var walker in Walkers)
            {
                walker.Dispose();
            }
            Walkers.Clear();
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(DisposeWalkers));
        }
    }
}