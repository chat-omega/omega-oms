using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.Models;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class PermCloser : IAutomation
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsCore _omsCore;
        private OrderTicket _ticket;

        public OrderSubType? SubType { get; set; }

        public int Qty { get; set; }
        public double Increment { get; set; }
        public double Interval { get; set; }
        public double SecondaryIncrement { get; set; }
        public double FillPx { get; private set; }

        private List<Walker> Walkers { get; }

        public PermCloser(OrderTicket baseOrder)
        {
            _omsCore = baseOrder.OmsCore;
            _ticket = baseOrder;
            SubType = OrderSubType.ThreeWayCloser;
            Walkers = new List<Walker>();
        }

        public async Task<bool> GeneratePermsAsync(bool includeBaseContra, int maxSpacing = 0, int maxPerms = 4)
        {
            try
            {
                DisposeWalkers();
                Walkers.Clear();

                List<Walker> walkers = new();
                for (int i = 0; i < _ticket.Legs.Count; i++)
                {
                    Walker walker = await GetPermWalker(i, PermutationDirection.Up);
                    if (walker != null)
                    {
                        walkers.Add(walker);
                    }
                    walker = await GetPermWalker(i, PermutationDirection.Down);
                    if (walker != null)
                    {
                        walkers.Add(walker);
                    }
                }

                if (maxSpacing > 0)
                {
                    walkers = walkers.Where(x => x.Spacing <= maxSpacing).ToList();
                }

                if (walkers.Count > maxPerms)
                {
                    walkers = walkers.Take(maxPerms).ToList();
                }

                Walkers.AddRange(walkers.OrderBy(x => x.Spacing));

                if (includeBaseContra)
                {
                    var mainOrder = _ticket.BuildOrder(isContra: true, SubType, Qty);
                    List<TicketLegModel> legs = new();
                    foreach (var leg in _ticket.Legs)
                    {
                        TicketLegModel legClone = new(_omsCore, leg.Underlying, leg.Account, leg.ParentBasket, leg.PortfolioManager);
                        await legClone.LoadFromTemplateAsync(leg);
                        legs.Add(legClone);
                    }
                    PxCalculator mainOrderCalculator = new(_ticket.Lcd, legs, mainOrder);
                    Walkers.Insert(0, new Walker(_ticket, this, _omsCore, SubType, mainOrderCalculator, _ticket.Multiplier));
                }

                for (int i = 0; i < Walkers.Count; i++)
                {
                    Walker walker = Walkers[i];
                    walker.Index = i;
                }

                return !includeBaseContra ? Walkers.Any() : Walkers.Count > 1;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GeneratePermsAsync));
                return false;
            }
        }

        private async Task<Walker> GetPermWalker(int i, PermutationDirection dir)
        {
            try
            {
                List<TicketLegModel> legs = new();
                foreach (TicketLegModel leg in _ticket.Legs)
                {
                    TicketLegModel legClone = new(_omsCore, leg.Underlying, leg.Account, leg.ParentBasket, leg.PortfolioManager);
                    await legClone.LoadFromTemplateAsync(leg);
                    legs.Add(legClone);
                }
                TicketLegModel swapLeg = legs[i];
                TicketLegModel permLeg = new(_omsCore, swapLeg.Underlying, swapLeg.Account, swapLeg.ParentBasket, swapLeg.PortfolioManager);
                await permLeg.LoadFromTemplateAsync(swapLeg);
                switch (dir)
                {
                    case PermutationDirection.Up:
                        await permLeg.StrikeUp(false);
                        break;
                    case PermutationDirection.Down:
                        await permLeg.StrikeDown(false);
                        break;
                }
                legs.Remove(swapLeg);
                legs.Add(permLeg);


                TicketLegModel swapLegClone = await CloneLeg(swapLeg);
                swapLegClone.Reverse();
                TicketLegModel newPermLeg = await CloneLeg(permLeg);

                List<TicketLegModel> verticalLegs =
                [
                    swapLegClone,
                    newPermLeg
                ];

                List<string> symbols =
                [
                    "[" + _ticket.SpreadSymbol + "]",
                    "[" + IAutomation.GetTosSymbol(legs, invert: true) + "]",
                    "[" + IAutomation.GetTosSymbol(verticalLegs) + "]"
                ];

                string payLoad = "3 Way - " + _ticket.SpreadId + " - " + string.Join(", ", symbols);
                var comment = payLoad.CompressString();

                var mainOrder = legs.Count == 1 ?
                    _ticket.BuildSingleLegOrder(isContra: true, leg: legs.First(), SubType, qty: Qty, comment: comment) :
                    _ticket.BuildMultiLegOrder(isContra: true, validLegs: legs, SubType, stampValues: true, overrideTheo: double.NaN, qty: Qty, comment: comment);

                int lcd = _ticket.Lcd;
                PxCalculator mainOrderCalculator = new(lcd, legs, mainOrder);

                var secondaryOrder = _ticket.BuildMultiLegOrder(isContra: false, validLegs: verticalLegs, SubType, stampValues: true, overrideTheo: double.NaN, qty: Qty, comment: comment);
                secondaryOrder.Route = _ticket.ApplyBrokerPrefix(_ticket.GetBestRoute(false, _ticket.InstanceMode, _ticket.IsBasketOrder, _ticket.IsStockTied, _ticket.IsStockTicket, false, _ticket.Underlying));
                PxCalculator secondaryOrderCalculator = new(lcd, verticalLegs, secondaryOrder);

                Walker walker = new(_ticket, this, _omsCore, SubType, mainOrderCalculator, secondaryOrderCalculator, _ticket.Multiplier)
                {
                    Spacing = Math.Abs(swapLegClone.Strike.Strike - newPermLeg.Strike.Strike)
                };
                return walker;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetPermWalker));
                return null;
            }
        }

        private async Task<TicketLegModel> CloneLeg(TicketLegModel swapLeg)
        {
            TicketLegModel swapLegClone = new(_omsCore, swapLeg.Underlying, swapLeg.Account, swapLeg.ParentBasket, swapLeg.PortfolioManager);
            await swapLegClone.LoadFromTemplateAsync(swapLeg);
            return swapLegClone;
        }

        private void DisposeWalkers()
        {
            try
            {
                foreach (var walker in Walkers)
                {
                    walker.Dispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DisposeWalkers));
            }
        }

        public async Task<bool> StartAsync(int qty, double fillPx, double edge, double maxLoss, double increment, double secondaryIncrement = double.NaN, int secondaryMaxResubmit = 3, int interval = 500, bool useRawHwTheo = true)
        {
            Qty = qty;
            Increment = increment;
            Interval = interval;
            SecondaryIncrement = double.IsNaN(secondaryIncrement) ? Convert.ToDouble(_ticket.GetPriceIncrement()) : secondaryIncrement;
            FillPx = fillPx;

            foreach (var walker in Walkers.ToList())
            {
                if (walker.SecondaryOrder == null)
                {
                    if (_ticket.IsSingleLeg)
                    {
                        walker.MainOrder.PriceCalculator.Order.Qty = qty;
                        double startingPx = FillPx;
                        if (_ticket.Side == Side.Buy)
                        {
                            walker.MainOrder.NextPriceCalculator = () => startingPx + edge;
                            walker.MainOrder.StopPriceCalculator = () => startingPx - maxLoss;
                        }
                        else
                        {
                            walker.MainOrder.NextPriceCalculator = () => startingPx - edge;
                            walker.MainOrder.StopPriceCalculator = () => startingPx + maxLoss;
                        }
                    }
                    else
                    {
                        double startingPx = FillPx * -1;
                        walker.MainOrder.PriceCalculator.Order.Qty = qty;
                        walker.MainOrder.NextPriceCalculator = () => startingPx - edge;
                        walker.MainOrder.StopPriceCalculator = () => startingPx + maxLoss;
                    }
                }
                else
                {
                    if (await walker.SecondaryOrder.PriceCalculator.TheoLoadedNotifier.WaitForLoadAsync())
                    {
                        double theo = useRawHwTheo ? walker.SecondaryOrder.PriceCalculator.NetTheo : await CalculateTheoFromMatchingHwUpdates(walker, fillPx);

                        walker.MainOrder.PriceCalculator.Order.Qty = qty;
                        walker.SecondaryOrder.PriceCalculator.Order.Qty = qty;
                        walker.SecondaryOrder.OrderMaxResubmit = secondaryMaxResubmit;
                        if (_ticket.IsSingleLeg)
                        {
                            if (_ticket.Side == Side.Buy)
                            {
                                double startingPx = FillPx + theo;
                                walker.MainOrder.NextPriceCalculator = () => startingPx + edge;
                                walker.MainOrder.StopPriceCalculator = () => startingPx - maxLoss;
                            }
                            else
                            {
                                double startingPx = FillPx - theo;
                                walker.MainOrder.NextPriceCalculator = () => startingPx - edge;
                                walker.MainOrder.StopPriceCalculator = () => startingPx + maxLoss;
                            }
                        }
                        else
                        {
                            double startingPx = FillPx + theo;
                            startingPx *= -1;
                            walker.MainOrder.NextPriceCalculator = () => startingPx - edge;
                            walker.MainOrder.StopPriceCalculator = () => startingPx + maxLoss;
                        }

                        walker.SecondaryOrder.StopPriceCalculator = () => walker.SecondaryOrder.PriceCalculator.NetTheo;
                    }
                    else
                    {
                        walker.Dispose();
                        Walkers.Remove(walker);
                    }
                }
            }

            var started = await RestartAsync();
            return started;
        }

        private async Task<double> CalculateTheoFromMatchingHwUpdates(Walker walker, double fillPx)
        {
            double theo = double.NaN;
            IEnumerable<string> baseSymbols = _ticket.Legs.Select(x => x.Symbol);
            IEnumerable<string> verticalSymbols = walker.SecondaryOrder.PriceCalculator.Legs.Select(x => x.Symbol);
            List<string> allSymbols = verticalSymbols.Union(baseSymbols).Distinct().ToList();
            string symbolsList = string.Join(", ", allSymbols);
            HanweckUpdatesWithMatchingTimestampsResponse matchingUpdatesResponse = await _omsCore.UpdateManager.RequestHanweckUpdatesWithMatchingTimestampsAsync(allSymbols);
            _log.Info($"{nameof(StartAsync)} requesting matching hw updates for [{symbolsList}], Result: {matchingUpdatesResponse.UpdateFound}, Px: {matchingUpdatesResponse.Price}, Orig: {_ticket.GetStats()}");
            if (matchingUpdatesResponse.UpdateFound)
            {
                var theoAtTime = OrderTicket.GetPermAdjPx(matchingUpdatesResponse, _ticket.Legs);
                var edgeToHwTheo = theoAtTime - fillPx;

                bool[] greeksLoaded = await Task.WhenAll(new[] { walker.SecondaryOrder.PriceCalculator.GreeksLoadedNotifier.WaitForLoadAsync(), _ticket.WaitForTheoLoadAsync() });
                if (greeksLoaded.All(x => x))
                {
                    var deltaDiff = walker.SecondaryOrder.PriceCalculator.TotalDelta - _ticket.TotalDelta;
                    var deltaAdjChange = (_ticket.UnderMid - matchingUpdatesResponse.Price) * deltaDiff;
                    if (!double.IsNaN(deltaAdjChange))
                    {
                        edgeToHwTheo += deltaAdjChange;
                    }
                }

                var permTheo = OrderTicket.GetPermAdjPx(matchingUpdatesResponse, walker.SecondaryOrder.PriceCalculator.Legs);
                theo = permTheo - edgeToHwTheo;
            }

            return theo;
        }

        private async Task<bool> RestartAsync()
        {
            bool started = await SendNextWalker(nextWalkerIndex: 0);
            return started;
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
            DisposeWalkers();
        }

        internal void Dispose()
        {
            DisposeWalkers();
            _ticket = null;
        }

        public void ShowStatus(OrderUpdateModel execReport, OrderStatus status)
        {
            OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

            _ticket.ContraOrderStatus = status;
            _ticket.ContraStatus = "[3W] " + orderUpdateValues.Status;
            _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
        }
    }
}
