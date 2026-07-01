using NLog;
using System;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;
using Timer = System.Timers.Timer;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class CxlReplaceCloser
    {
        private const int ROUND_TRIP_ESTIMATE = 500;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly Timer _overwatchTimer;
        private OrderTicket _ticket;
        private AutomationConfigModel _automationConfig;
        private bool _overwatchStarted;
        private int _closeQty;
        private double _closeStopPx;
        private int _closeInterval;
        private double _closingEdge;
        private double _closeMaxLoss;
        private double _priceIncrement;
        private OrderSubType _type = OrderSubType.AutoClose;

        public bool Enabled
        {
            get
            {
                try
                {
                    if (_ticket.IsBasketOrder)
                    {
                        return _automationConfig is { LoopingEnabled: false, ClosingMode: Oms.Enums.ClosingTypes.CxlReplace, GoFishAutoCloseEnabled: true } || Manual;
                    }
                    else
                    {
                        return Manual;
                    }
                }
                catch { return false; }
            }
        }

        public bool Manual { get; set; }

        public CxlReplaceCloser(OrderTicket orderTicketViewModelBase)
        {
            _ticket = orderTicketViewModelBase;
            _overwatchTimer = new()
            {
                AutoReset = false
            };
            _overwatchTimer.Elapsed += OverwatchTimer_Ellapsed;
        }

        internal void StartCloser(double lastFillPx,
                                  int qty,
                                  double closingEdge,
                                  double closeMaxLoss,
                                  double priceIncrement,
                                  int closeInterval,
                                  bool manualClose = false,
                                  OrderSubType? type = null)
        {
            _closeQty = qty;
            _closingEdge = closingEdge;
            _closeMaxLoss = closeMaxLoss;
            _priceIncrement = priceIncrement;
            _closeInterval = Math.Max(1, closeInterval);
            Manual = manualClose;
            _overwatchStarted = false;
            if (type.HasValue)
            {
                _type = type.Value;
            }
            _automationConfig = _ticket.IsBasketOrder ? _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement) : null;

            _ticket.IsLooping = true;
            if (_ticket.IsDisposed ||
                !Enabled)
            {
                Stop();
                return;
            }

            double fillPx = lastFillPx;
            if (_ticket.IsSingleLeg &&
                _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                fillPx = lastFillPx;
            }

            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    _closeStopPx = Math.Round(fillPx - _closeMaxLoss, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    _closeStopPx = Math.Round(fillPx + _closeMaxLoss, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                _closeStopPx = Math.Round((fillPx * -1.0) + _closeMaxLoss, 2, MidpointRounding.AwayFromZero);
            }

            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    _ticket.LastContraFillPx = Math.Round(lastFillPx + _closingEdge, 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    _ticket.LastContraFillPx = Math.Round(lastFillPx - _closingEdge, 2, MidpointRounding.AwayFromZero);
                }
            }
            else
            {
                _ticket.LastContraFillPx = Math.Round((lastFillPx * -1.0) - _closingEdge, 2, MidpointRounding.AwayFromZero);
            }

            SubmitClosingOrderAsync(_ticket.LastContraFillPx, _type);
        }

        internal async Task<bool> ContClose()
        {
            if (_ticket.IsDisposed ||
                !Enabled)
            {
                Stop();
                return false;
            }

            if (_closeInterval > 0)
            {
                await Task.Delay(_closeInterval);
            }

            if (_ticket.IsSingleLeg &&
                _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                _ticket.LastContraFillPx -= GetPriceIncrement(_ticket.LastContraFillPx, IncrementDirection.Down);
            }
            else
            {
                _ticket.LastContraFillPx += GetPriceIncrement(_ticket.LastContraFillPx, IncrementDirection.Up);
            }

            if (_ticket.IsSingleLeg &&
                _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                if (_ticket.LastContraFillPx < _closeStopPx)
                {
                    Stop();
                    return false;
                }
            }
            else
            {
                if (_ticket.LastContraFillPx > _closeStopPx)
                {
                    Stop();
                    return false;
                }
            }

            ModifyClosingOrderAsync(_ticket.LastContraFillPx);

            if (!_overwatchStarted)
            {
                _overwatchStarted = true;
                StartOverwatchTimer();
            }
            return true;
        }

        private double GetPriceIncrement(double price, IncrementDirection direction)
        {
            return Math.Max(_priceIncrement, (double)_ticket.GetPriceIncrement(price, direction));
        }

        private void SubmitClosingOrderAsync(double price, OrderSubType? type)
        {
            Task.Run(() => SubmitClosingOrder(price, type));
        }

        private void SubmitClosingOrder(double price, OrderSubType? type)
        {
            try
            {
                if (_ticket.IsDisposed ||
                    !Enabled)
                {
                    Stop();
                    return;
                }

                var orderInfo = _ticket.BuildOrder(true, type, Math.Abs(_closeQty));
                orderInfo.Price = _ticket.PriceNeedsPadding(price) ? _ticket.PadForNickelOrDime(price, true) : price;
                double cancelDelay = (_closeInterval + ROUND_TRIP_ESTIMATE) * ((_closingEdge + _closeMaxLoss) / _priceIncrement);
                cancelDelay = cancelDelay > 0 && (!_ticket.IsBasketOrder || !_ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement).LeaveAutoCloseResting) ? cancelDelay : 0;

                if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                orderInfo.SetCancelDelay(cancelDelay);

                _ticket.SubmitContraOrder(orderInfo);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
            }
        }

        private void ModifyClosingOrderAsync(double price)
        {
            Task.Run(() => ModifyClosingOrder(price));
        }

        private void ModifyClosingOrder(double price)
        {
            try
            {
                if (_ticket.IsDisposed ||
                    !_ticket.ContraNotFilled ||
                    !Enabled)
                {
                    Stop();
                    return;
                }

                _ticket.ModifyContraOrder(price, _closeQty);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
            }
        }

        private void StartOverwatchTimer()
        {
            if (_ticket.IsBasketOrder &&
                _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement).LeaveAutoCloseResting)
            {
                return;
            }
            if (_ticket.IsDisposed ||
                !Enabled)
            {
                Stop();
                return;
            }
            _overwatchTimer.Stop();
            double interval = (_closeInterval + ROUND_TRIP_ESTIMATE) * (((_closingEdge + _closeMaxLoss) / _priceIncrement) - 1);
            if (interval > 0)
            {
                _overwatchTimer.Interval = interval;
                _overwatchTimer.Start();
            }
        }

        public void Dispose()
        {
            try
            {
                if (Enabled)
                {
                    Stop();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
            _ticket = null;
        }

        internal void Stop()
        {
            if (_ticket.IsBasketOrder &&
                _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement).LeaveAutoCloseResting)
            {
                return;
            }
            if (_ticket.IsLooping)
            {
                _ticket.CancelContra();
            }
            _ticket.IsLooping = false;
            Manual = false;
            _overwatchTimer.Stop();
        }

        private void OverwatchTimer_Ellapsed(object sender, ElapsedEventArgs e)
        {
            Stop();
        }
    }
}