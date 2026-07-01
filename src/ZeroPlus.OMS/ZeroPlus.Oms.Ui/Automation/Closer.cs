using NLog;
using System;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    /// <summary>
    /// Implements closing strategy execution for trading positions with configurable price adjustments and timing controls.
    /// </summary>
    /// <remarks>
    /// The Closer handles automatic order submission and price adjustments for closing positions, supporting both
    /// single-leg and basket orders. It implements fail-safes for maximum loss and provides manual override capabilities.
    /// Price adjustments follow instrument-specific increment rules and respect exchange-mandated minimums.
    /// </remarks>
    public class Closer : ICloser
    {
        private const int ROUND_TRIP_ESTIMATE = 500;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private OrderTicket _ticket;
        private AutomationConfigModel _automationConfig;
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
                        return (_automationConfig != null && !_automationConfig.LoopingEnabled && _automationConfig.ClosingMode == Oms.Enums.ClosingTypes.CxlResubmit && _automationConfig.GoFishAutoCloseEnabled) || Manual;
                    }
                    else
                    {
                        return _ticket.SpeedTraderClosingType == SpeedTraderClosingType.Close || Manual;
                    }
                }
                catch { return false; }
            }
        }

        public bool Manual { get; set; }

        public Closer(OrderTicket orderTicketViewModelBase)
        {
            _ticket = orderTicketViewModelBase;
        }
        /// <summary>
        /// Initiates a position closing sequence with specified parameters and price controls.
        /// </summary>
        /// <remarks>
        /// This method calculates initial stop prices and contra-side order prices based on the last fill.
        /// For single-leg orders, it adjusts prices based on the original order side (BUY/SELL).
        /// For multi-leg orders, it performs additional price normalization.
        /// </remarks>
        public void StartCloser(double lastFillPx,
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
            if (type != null)
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

        /// <summary>
        /// Attempts to continue the closing process by adjusting the order price.
        /// </summary>
        /// <remarks>
        /// Implements the core price adjustment logic:
        /// - Decrements price for long positions
        /// - Increments price for short positions
        /// - Respects maximum loss thresholds
        /// - Maintains minimum price increment rules
        /// </remarks>
        public bool ContClose(int qty = 0)
        {
            if (_ticket.IsDisposed ||
                !Enabled)
            {
                Stop();
                return false;
            }

            if (qty > 0)
            {
                _closeQty = Math.Abs(qty);
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

            SubmitClosingOrderAsync(_ticket.LastContraFillPx, _type);

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
        /// <summary>
        /// Submits a closing order with the specified price and type identifier.
        /// </summary>
        /// <remarks>
        /// Handles price padding for instruments requiring nickel/dime increments.
        /// Sets appropriate cancel delays based on configuration and remaining price steps to stop level.
        /// </remarks>
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
                orderInfo.SetCancelDelay(GetCancelDelay(price));
                _ticket.SubmitContraOrder(orderInfo);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitClosingOrder));
            }
        }
        /// <summary>
        /// Calculates the appropriate cancel delay for a closing order at the specified price.
        /// </summary>
        /// <remarks>
        /// Determines whether to let orders rest based on:
        /// - Distance to stop price
        /// - Basket order configuration
        /// - Exchange-specific timing requirements
        /// </remarks>
        private double GetCancelDelay(double price)
        {
            double cancelDelay = _closeInterval;

            if (cancelDelay <= 0 || _ticket.IsBasketOrder && _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying, (double)_ticket.PriceIncrement).LeaveAutoCloseResting)
            {
                var nxtPrice = price;

                if (_ticket.IsSingleLeg &&
                    _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    nxtPrice -= GetPriceIncrement(nxtPrice, IncrementDirection.Down);
                }
                else
                {
                    nxtPrice += GetPriceIncrement(nxtPrice, IncrementDirection.Up);
                }

                if (_ticket.IsSingleLeg &&
                    _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    if (nxtPrice < _closeStopPx)
                    {
                        cancelDelay = 0;
                    }
                }
                else
                {
                    if (nxtPrice > _closeStopPx)
                    {
                        cancelDelay = 0;
                    }
                }
            }


            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            return cancelDelay;
        }
        /// <summary>
        /// Performs cleanup of closing process resources.
        /// </summary>
        /// <remarks>
        /// Ensures orderly shutdown of closing process by:
        /// - Cancelling active orders if enabled
        /// - Logging any cleanup failures
        /// - Maintaining state consistency
        /// </remarks>
        public void Dispose()
        {
            try
            {
                if (Enabled)
                {
                    Stop();
                }
                _ticket = null;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }

        public void Stop()
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
        }

        private void OverwatchTimer_Ellapsed(object sender, ElapsedEventArgs e)
        {
            Stop();
        }
    }
}