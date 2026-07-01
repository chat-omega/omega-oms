using NLog;
using System;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public interface IFisher : IDisposable
    {
        /// <summary>
        /// Gets or sets whether the fishing algorithm is currently active.
        /// </summary>
        bool IsRunning { get; set; }
        /// <summary>
        /// Gets or sets whether the fisher is operating in manual mode.
        /// </summary>
        bool Manual { get; set; }

        /// <summary>
        /// Continues the fishing strategy by adjusting the order price based on the last fill.
        /// </summary>
        /// <param name="receiveTime">The timestamp when the continuation was triggered.</param>
        /// <param name="qty">Optional new quantity to use for subsequent orders. If 0, maintains existing quantity.</param> 
        void ContFish(DateTime receiveTime, int qty = 0);

        /// <summary>
        /// Initializes and starts the fishing algorithm with specified parameters.
        /// </summary>
        /// <param name="basePrice">The initial reference price for the fishing strategy.</param>
        /// <param name="underlyingAtBase">The underlying asset's price when the base price was set.</param>
        /// <param name="qty">The quantity to trade in each order.</param>
        /// <param name="fishEdge">The price improvement to target relative to the base price.</param>
        /// <param name="fishMaxLoss">The maximum adverse price movement allowed before stopping.</param>
        /// <param name="priceIncrement">The minimum price increment for order adjustments.</param>
        /// <param name="interval">The time interval between order adjustments in milliseconds.</param>
        /// <param name="manual">Whether to operate in manual mode.</param>
        /// <param name="type">The type identifier for the fishing strategy.</param>
        void StartFisher(
            double basePrice,
            double underlyingAtBase,
            int qty,
            double fishEdge,
            double fishMaxLoss,
            double priceIncrement,
            int interval,
            bool manual = false,
            OrderSubType? type = null);

        /// <summary>
        /// Stops the fishing algorithm and cancels any active orders.
        /// </summary>
        void Stop();
    }

    /// <summary>
    /// Implements a fishing algorithm for automated order execution that attempts to capture 
    /// favorable prices by systematically adjusting order prices within specified boundaries.
    /// This implementation supports both single-leg and basket orders with customizable parameters
    /// for price improvements and risk management.
    /// </summary>
    public class Fisher : IFisher
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private OrderTicket _ticket;
        private double _basePrice = double.NaN;
        private double _underlyingAtBase = double.NaN;
        private int _fishQty;
        private int _fishInterval;
        private double _fishStopPx;
        private double _fishEdge;
        private double _fishMaxLoss;
        private double _priceIncrement;
        /// <summary>
        /// The type identifier for the fishing strategy (e.g., "Fish", "Fish - C.O.T", "Slooper").
        /// </summary>
        private OrderSubType? _type = OrderSubType.Fish;

        public bool IsRunning { get; set; }
        public bool Manual { get; set; }

        public Fisher(OrderTicket orderTicketViewModelBase)
        {
            _ticket = orderTicketViewModelBase;
        }

        public void StartFisher(double basePrice,
                                  double underlyingAtBase,
                                  int qty,
                                  double fishEdge,
                                  double fishMaxLoss,
                                  double priceIncrement,
                                  int interval,
                                  bool manual = false,
                                  OrderSubType? type = null)
        {
            _basePrice = basePrice;
            _underlyingAtBase = underlyingAtBase;
            _fishQty = qty;
            _fishEdge = fishEdge;
            _fishMaxLoss = fishMaxLoss;
            _fishInterval = Math.Max(1, interval);
            _priceIncrement = priceIncrement;
            Manual = manual;
            if (type.HasValue)
            {
                _type = type.Value;
            }

            _ticket.IsLooping = true;
            IsRunning = true;
            if (_ticket.IsDisposed)
            {
                Stop();
                return;
            }

            if (_ticket.IsSingleLegSell)
            {
                _fishStopPx = Math.Round(basePrice - _fishMaxLoss, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                _fishStopPx = Math.Round(basePrice + _fishMaxLoss, 2, MidpointRounding.AwayFromZero);
            }

            if (_ticket.IsSingleLegSell)
            {
                _ticket.LastFillPx = Math.Round(basePrice + _fishEdge, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                _ticket.LastFillPx = Math.Round(basePrice - _fishEdge, 2, MidpointRounding.AwayFromZero);
            }

            SubmitFishOrderAsync(_ticket.LastFillPx, _type);
        }

        public void ContFish(DateTime receiveTime, int qty = 0)
        {
            if (_ticket.IsDisposed)
            {
                Stop();
                return;
            }

            if (qty > 0)
            {
                _fishQty = Math.Abs(qty);
            }

            if (_ticket.IsSingleLeg &&
                _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                _ticket.LastFillPx -= GetPriceIncrement(_ticket.LastFillPx, IncrementDirection.Down);
            }
            else
            {
                _ticket.LastFillPx += GetPriceIncrement(_ticket.LastFillPx, IncrementDirection.Up);
            }

            _ticket.LastFillPx = Math.Round(_ticket.LastFillPx, 2);

            if (_ticket.IsSingleLeg &&
                _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                if (_ticket.LastFillPx < _fishStopPx)
                {
                    Stop();
                    ResumeLooper(receiveTime);
                    return;
                }
            }
            else
            {
                if (_ticket.LastFillPx > _fishStopPx)
                {
                    Stop();
                    ResumeLooper(receiveTime);
                    return;
                }
            }

            SubmitFishOrderAsync(_ticket.LastFillPx, _type);
        }

        private void ResumeLooper(DateTime receiveTime)
        {
            if (_type == OrderSubType.Slooper)
            {
                int prevQty = _ticket.PrevQty;
                _ticket.Reverse();
                _ticket.UpdateQty(prevQty);
                _ticket.IsFreeLooking = false;
                _ticket.LastFillPx = _ticket.IsSingleLeg ? _basePrice : -_basePrice;
                _ticket.LastFillUnderBidPx = _ticket.UnderBid;
                _ticket.LastFillUnderPx = _ticket.UnderMid;
                _ticket.LastFillUnderAskPx = _ticket.UnderAsk;
                _ticket.LastFillAdjTheo = _ticket.NetDeltaAdjTheo;

                _ticket.Looper.StartClosingLoop(receiveTime);
            }
        }

        private double GetPriceIncrement(double price, IncrementDirection direction)
        {
            return Math.Max(_priceIncrement, (double)_ticket.GetPriceIncrement(price, direction));
        }

        private void SubmitFishOrderAsync(double price, OrderSubType? type)
        {
            Task.Run(() => SubmitFishOrder(price, type));
        }

        private void SubmitFishOrder(double price, OrderSubType? type)
        {
            try
            {
                if (_ticket.IsDisposed)
                {
                    Stop();
                    return;
                }

                var orderInfo = _ticket.BuildOrder(isContra: false, type, Math.Abs(_fishQty));
                orderInfo.Price = _ticket.PriceNeedsPadding(price) ? _ticket.PadForNickelOrDime(price, true) : price;
                double cancelDelay = _fishInterval;

                if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                orderInfo.SetCancelDelay(cancelDelay);

                _ticket.SubmitMainOrder(orderInfo);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubmitFishOrder));
            }
        }

        public void Dispose()
        {
            try
            {
                if (IsRunning)
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

        public void Stop()
        {
            var wasRunning = IsRunning;
            IsRunning = false;
            _ticket.IsLooping = false;

            if (Manual && _ticket.IsBasketOrder)
            {
                AutomationConfigModel automationConfigModel = _ticket.GetAutomationConfig();
                if (automationConfigModel != null && automationConfigModel.LeaveAutoCloseResting)
                {
                    return;
                }
            }

            Manual = false;
            if (wasRunning)
            {
                _ticket.CancelMain();
            }
        }
    }
}