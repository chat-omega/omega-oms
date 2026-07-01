using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public delegate void EdgeChangedHandler(bool showEdgeIndicators, double acquiredEdge, double projectedEdge, double deltaAdjEdge, double realizedPnl, double adjustedPnl);

    public enum Ticket : int
    {
        First = 1,
        Second = 2,
        Third = 3,
    }

    public class EdgeProjectorModel : IOmsDataSubscriber
    {
        private readonly object _ticketsLock = new();
        private readonly HashSet<Tuple<OrderTicket, Ticket, int, double>> _tickets = new();
        private readonly Dictionary<OrderTicket, bool> _ticketDirectionMap = new();
        private readonly PortfolioManagerModel _portfolioManagerModel;
        private string _mainTicketSpreadId;
        private bool _cleared;
        private bool _calculating;
        private double _multiplier = double.NaN;

        public double AcquiredEdge { get; private set; }
        public double ProjectedEdge { get; private set; }
        public double DeltaAdjEdge { get; private set; }
        public double RealizedPnl { get; private set; }
        public double AdjustedPnl { get; private set; }

        public event EdgeChangedHandler EdgeChanged;
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public string InstanceId { get; private set; }
        public bool Disposed { get; private set; }
        public bool IsDisposed { get; set; }
        public bool IsThreeWay { get; internal set; }
        public int TicketsCount { get; internal set; }

        public EdgeProjectorModel(PortfolioManagerModel portfolioManagerModel)
        {
            _portfolioManagerModel = portfolioManagerModel;
            IsThreeWay = true;
            TicketsCount = 3;
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (key.Type == SubscriptionFieldType.FirmInstancePosition)
                {
                    if (value is IPosition position)
                    {
                        AdjustedPnl = position.AdjustedPnl;
                        RealizedPnl = position.RealizedPnl;
                    }
                }
                EdgeChanged?.Invoke(true, AcquiredEdge, ProjectedEdge, DeltaAdjEdge, RealizedPnl, AdjustedPnl);
            }
            catch (Exception)
            {
            }
        }

        internal void AddTicket(OrderTicket complexOrderTicketViewModel, Ticket ticket, bool reverse = false)
        {
            if (Disposed)
            {
                complexOrderTicketViewModel.Close();
                return;
            }
            lock (_ticketsLock)
            {
                var qty = ticket == Ticket.First ? 1 : complexOrderTicketViewModel.Lcd;
                var key = Tuple.Create(complexOrderTicketViewModel, ticket, qty, complexOrderTicketViewModel.Multiplier);
                _tickets.Add(key);
                _ticketDirectionMap[complexOrderTicketViewModel] = reverse;
            }
            if (ticket == Ticket.First)
            {
                _mainTicketSpreadId = complexOrderTicketViewModel.SpreadId;
                _multiplier = complexOrderTicketViewModel.Multiplier;
            }
            CalculateEdge();
        }

        internal void RemoveTicket(OrderTicket complexOrderTicketViewModel)
        {
            lock (_ticketsLock)
            {
                try
                {
                    foreach (OrderTicket ticket in _tickets.OrderBy(x => x.Item2).Select(x => x.Item1))
                    {
                        ticket.SuggestTradingMain = false;
                        ticket.SuggestTradingContra = false;
                    }

                    _tickets.RemoveWhere(x => x.Item1 == complexOrderTicketViewModel);
                    _ticketDirectionMap.Remove(complexOrderTicketViewModel);
                }
                catch (Exception)
                {
                }
            }
            CalculateEdge();
        }

        internal void CalculateEdge()
        {
            try
            {
                if (!_calculating)
                {
                    _calculating = true;
                    if (_tickets.Count < TicketsCount)
                    {
                        Clear(showIndicators: false);
                    }
                    else if (!IsValid())
                    {
                        Clear(showIndicators: true);
                    }
                    else
                    {
                        try
                        {
                            double acquiredTotal = 0.0;
                            double projectedTotal = 0.0;
                            double deltaAdjTotal = 0.0;

                            foreach (var tuple in _tickets.OrderBy(x => x.Item2))
                            {
                                var ticket = tuple.Item1;
                                var ticketLcd = tuple.Item3;
                                var multiplier = tuple.Item4;
                                if (_ticketDirectionMap[ticket])
                                {
                                    if (ticket.TicketStyle == OrderTicketStyle.Combined)
                                    {
                                        if (OmsCore.Config.PriceEvaluationStyle == PriceEvaluationStyle.Identical)
                                        {
                                            acquiredTotal += ticketLcd * (ticket.ContraAveragePrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                            projectedTotal += ticketLcd * (-ticket.ContraPrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                            deltaAdjTotal += ticketLcd * (-ticket.DeltaAdjContraPx) * multiplier;
                                        }
                                        else
                                        {
                                            acquiredTotal += ticketLcd * (ticket.ContraAveragePrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                            projectedTotal += ticketLcd * (ticket.ContraPrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                            deltaAdjTotal += ticketLcd * (ticket.DeltaAdjContraPx) * multiplier;
                                        }
                                    }
                                    else
                                    {
                                        acquiredTotal -= ticketLcd * (ticket.AveragePrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                        projectedTotal -= ticketLcd * (ticket.Price + ticket.GetTotalFeesForTicket()) * multiplier;
                                        deltaAdjTotal -= ticketLcd * (ticket.DeltaAdjPx) * multiplier;
                                    }
                                }
                                else
                                {
                                    acquiredTotal += ticketLcd * (ticket.AveragePrice + ticket.GetTotalFeesForTicket()) * multiplier;
                                    projectedTotal += ticketLcd * (ticket.Price + ticket.GetTotalFeesForTicket()) * multiplier;
                                    deltaAdjTotal += ticketLcd * (ticket.DeltaAdjPx) * multiplier;
                                }
                            }

                            AcquiredEdge = -acquiredTotal / _multiplier;
                            ProjectedEdge = -projectedTotal / _multiplier;
                            DeltaAdjEdge = -deltaAdjTotal / _multiplier;
                            EdgeChanged?.Invoke(true, AcquiredEdge, ProjectedEdge, DeltaAdjEdge, RealizedPnl, AdjustedPnl);
                            _cleared = false;
                        }
                        catch (Exception)
                        {
                            Clear(showIndicators: true);
                        }
                    }
                }
            }
            finally
            {
                _calculating = false;
            }
        }

        private void Clear(bool showIndicators)
        {
            if (!_cleared)
            {
                AcquiredEdge = double.NaN;
                ProjectedEdge = double.NaN;
                DeltaAdjEdge = double.NaN;
                RealizedPnl = double.NaN;
                AdjustedPnl = double.NaN;
                _portfolioManagerModel.UnsubscribeAllAsync(this);
                InstanceId = "";
                EdgeChanged?.Invoke(showIndicators, AcquiredEdge, ProjectedEdge, DeltaAdjEdge, RealizedPnl, AdjustedPnl);
                _cleared = true;
            }
            return;
        }

        internal string GetComment()
        {
            InstanceId = BuildComment();
            return InstanceId;
        }

        internal string BuildComment()
        {
            if (_tickets.Count < TicketsCount)
            {
                return "";
            }
            else
            {
                List<string> symbols = new();
                foreach (OrderTicket ticket in _tickets.OrderBy(x => x.Item2).Select(x => x.Item1))
                {
                    if (_ticketDirectionMap[ticket])
                    {
                        symbols.Add("[" + ticket.ContraSpreadSymbol + "]");
                    }
                    else
                    {
                        symbols.Add("[" + ticket.SpreadSymbol + "]");
                    }
                }

                string payLoad = "3 Way - " + _mainTicketSpreadId + " - " + string.Join(", ", symbols);
                string comment = payLoad.CompressString();
                _portfolioManagerModel.Subscribe(payLoad, SubscriptionFieldType.FirmInstancePosition, this);
                return comment;
            }
        }

        private bool IsValid()
        {
            return !_tickets.Any(x => _ticketDirectionMap[x.Item1] ?
                                      x.Item1.TicketStyle == OrderTicketStyle.Combined ?
                                        double.IsNaN(x.Item1.ContraAveragePrice) && double.IsNaN(x.Item1.ContraPrice) :
                                        double.IsNaN(x.Item1.AveragePrice) && double.IsNaN(x.Item1.Price) :
                                      double.IsNaN(x.Item1.AveragePrice) && double.IsNaN(x.Item1.Price));
        }

        internal void CloseAll()
        {
            Disposed = true;
            foreach (OrderTicket ticket in _tickets.Select(x => x.Item1).ToList())
            {
                ticket.Dispatcher.Invoke(new Action(() =>
                {
                    ticket.Close();
                }));
            }
        }
    }
}