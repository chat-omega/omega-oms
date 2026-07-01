using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class Looper
    {
        private const double PENNY_TOLERANCE = .01;

        private const int MAX_RESUB = 50;
        private const OrderSubType LOOPER_TYPE = OrderSubType.Looper;
        private const OrderSubType FREELOOK_TYPE = OrderSubType.FreeLook;
        private const OrderSubType FREELOOK_ALL_TYPE = OrderSubType.FreeLookAll;
        private const OrderSubType AUTO_AGGRESSOR_TYPE = OrderSubType.AutoAggressor;
        private const OrderSubType ICEBERG_TYPE = OrderSubType.IceBerg;

        private const double PX_TOLERANCE = .01;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private static int looperIndex;
        private static readonly object looperLock = new();
        private static readonly ConcurrentDictionary<string, Looper> spreadIdToActiveLooperMap = new();
        private static readonly HashSet<int> loopInstancesCounter = new();

        private OrderTicket _ticket;
        private readonly int _looperId;
        private bool _closing;
        private int _resubmitLastFilledCounter;
        private int _resubmitAllAttemptsCounter;
        private double _loopStopPx;
        private double _loopCloseStopPx;
        private bool _usingFreeLookIncrement;
        private bool _isFreeLookingOnAll;

        private double _prevPriceBeforeDynamicRouting = double.NaN;
        private double _prevContraPriceBeforeDynamicRouting = double.NaN;

        public int EstimatedResubmit { get; set; }
        public int ResubmitCounter { get; set; }
        public int LoopResubmitCounter { get; set; }
        public ResubmitSizeOption LoopResubmitWithPrevSize { get; set; }
        public bool SizeUpLocked { get; set; }
        public bool FirstSizeAggressorApplied { get; set; }

        public bool IcebergRunning { get; set; }
        public int IcebergTotalQty { get; set; }

        public OmsCore OmsCore { get; }
        private AutomationConfigModel AutomationConfig { get; set; }
        public bool IsActive => _ticket.IsActive;
        public ILoopSettings Settings => _ticket.IsBasketOrder ? AutomationConfig : _ticket;
        public int LoopDelay => _ticket.IsBasketOrder ? Settings.LoopIntervalMax > Settings.LoopInterval ? Random.Shared.Next(Settings.LoopInterval, Settings.LoopIntervalMax + 1) : Settings.LoopInterval : 0;
        public int LoopCloseInterval
        {
            get
            {
                int interval;
                if (_ticket.IsBasketOrder)
                {
                    interval = Settings.ContraFishIntervalMax > Settings.ContraFishInterval
                        ? Random.Shared.Next(Settings.ContraFishInterval, Settings.ContraFishIntervalMax + 1)
                        : Settings.ContraFishInterval;
                }
                else
                {
                    interval = _ticket.GetLoopCloseInterval();
                }
                return Math.Max(1, interval);
            }
        }

        public int LoopAttemptResubmit => _ticket.IsBasketOrder ? IcebergRunning ? AutomationConfig.IcebergMaxResubmit : Settings.AttemptResubmit : 0;
        public int LoopResubmit => _ticket.IsBasketOrder ? Settings.LoopResubmit : _ticket.LoopResubmit;
        public bool LoopingEnabled => _ticket.IsBasketOrder ? Settings.LoopingEnabled : _ticket.SpeedTraderClosingType == SpeedTraderClosingType.Loop;
        public double ClosingEdge => _ticket.GetClosingEdge();

        public Looper(OrderTicket orderTicketViewModelBase)
        {
            _ticket = orderTicketViewModelBase;
            _looperId = Interlocked.Increment(ref looperIndex);
            OmsCore = orderTicketViewModelBase.OmsCore;
            _ticket.LoopCommandEvent += OnBasketSettingsLoopCommandEvent;
        }

        internal void StartLoop(DateTime receiveTime, bool isRecon = false, bool skipFreeLookAll = false)
        {
            _log.Info($"{nameof(StartLoop)} Start Loop. Id: {_ticket.SpreadId}, Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}");

            if (_ticket.IsBasketOrder &&
                _ticket.BasketSettings.BasketLoopBlockList != null &&
                _ticket.BasketSettings.BasketLoopBlockList.Items.Contains(_ticket.Underlying))
            {
                _log.Info(nameof(StartLoop) + " Symbol Blocked From Looping. " + _ticket.SpreadId);
                Disable();
                return;
            }

            _ticket.SubTypeId = SubType.LoopOpen;
            _ticket.SubTypeSequence++;
            _ticket.IsLooping = true;
            _ticket.PartiallyFilled = false;
            _usingFreeLookIncrement = false;
            _prevPriceBeforeDynamicRouting = double.NaN;
            _ticket.LeavesQty = 0;
            _ticket.CumulativeQty = 0;
            IcebergRunning = false;

            if (!CheckForLoopInstances(isRecon))
            {
                return;
            }

            _closing = false;
            _resubmitLastFilledCounter = 0;
            _resubmitAllAttemptsCounter = 0;
            ResubmitCounter = 0;
            LoopResubmitCounter = 0;

            if (!_ticket.TrySelectRoute(isContra: true, lookupOnly: true, out string route, out _))
            {
                route = string.IsNullOrWhiteSpace(_ticket.ContraRoute) ? _ticket.Route : _ticket.ContraRoute;
            }

            double fees = _ticket.GetTotalFeesForTicket(route, reverse: true);
            double lastContraFillPx = _ticket.LastContraFillPx + fees;
            if (_ticket.IsSingleLeg && _ticket.Side == Side.Buy)
            {
                lastContraFillPx = _ticket.LastContraFillPx - fees;
            }

            _ticket.SetOrderDetailTag("Last Close Fill Px", lastContraFillPx.ToString());
            _ticket.SetOrderDetailTag("Fees", fees.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());

            double stopPx = double.NaN;
            double minEdge = _ticket.GetLoopMinEdge();
            if (_ticket.LastEdge > 0 && Settings.LastEdgeTightenPercent > 0)
            {
                double change = _ticket.LastEdge * Settings.LastEdgeTightenPercent;
                if (minEdge - change > 0)
                {
                    minEdge -= change;
                }
            }
            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == Side.Sell)
                {
                    stopPx = lastContraFillPx + minEdge;
                }
                else
                {
                    stopPx = lastContraFillPx - minEdge;
                }
            }
            else
            {
                stopPx = (lastContraFillPx * -1.0) - minEdge;
            }
            _loopStopPx = Math.Round(stopPx, 2, MidpointRounding.AwayFromZero);
            _ticket.SetOrderDetailTag("Loop Min Edge", minEdge.ToString());
            _ticket.SetOrderDetailTag("Loop Stop Px", _loopStopPx.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());

            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(StartLoop) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(StartLoop) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!isRecon)
            {
                OrderSubType type = LOOPER_TYPE;
                if (_ticket.IsBasketOrder)
                {
                    if ((Settings.LoopFreeLookOnAll && (!Settings.FreeLookRequireMinFillTime || _ticket.FillTime > Settings.FreeLookMinFillTime)) || (Settings.FreeLookWhenGettingCloseEdge && Math.Abs(_ticket.LastEdge - ClosingEdge) < PENNY_TOLERANCE))
                    {
                        double priceBackup = 0;
                        double prevPrice = _ticket.LastFillPx;
                        if (_ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            var priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastFillPx, IncrementDirection.Up);

                            var settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks ?
                                Settings.FreeLookOnAllIncrementTicks * priceIncrement :
                                Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);


                            _ticket.LastFillPx += priceBackup;
                        }
                        else
                        {
                            var priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastFillPx, IncrementDirection.Down);

                            var settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks ?
                                Settings.FreeLookOnAllIncrementTicks * priceIncrement :
                                Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);

                            _ticket.LastFillPx -= priceBackup;
                        }

                        _isFreeLookingOnAll = priceBackup > 0;

                        type = FREELOOK_ALL_TYPE;
                        _log.Info("Price backup applied." +
                                  "Id: " + _ticket.SpreadId + ", " +
                                  "Prev Price: " + prevPrice + ", " +
                                  "Price Backup: " + priceBackup + ", " +
                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                  "Price: " + _ticket.LastFillPx);
                    }
                    else
                    {
                        _isFreeLookingOnAll = false;
                    }
                }
                else
                {
                    _isFreeLookingOnAll = false;
                }

                double lastFillPx = GetLastFillPx();

                SetAttemptedEdgeOnOpen(lastFillPx);

                SubmitOrderAsync(lastFillPx, type, receiveTime);
            }
            else
            {
                double loopMinEdge = _ticket.GetLoopMinEdge();
                _ticket.LastEdge = loopMinEdge;
                _ticket.DeltaAdjLastEdge = loopMinEdge;
                OrderSubType type = FREELOOK_TYPE;
                double freeLookPx;
                if (_ticket.IsSingleLeg)
                {
                    if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        var priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastContraFillPx, IncrementDirection.Up);
                        var priceBackup = Math.Round(Math.Max(loopMinEdge, priceIncrement), 2);
                        freeLookPx = _ticket.LastContraFillPx + priceBackup;
                    }
                    else
                    {
                        var priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastContraFillPx, IncrementDirection.Down);
                        var priceBackup = Math.Round(Math.Max(loopMinEdge, priceIncrement), 2);
                        freeLookPx = _ticket.LastContraFillPx - priceBackup;
                    }
                }
                else
                {
                    var priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastContraFillPx, IncrementDirection.Down);
                    var priceBackup = Math.Round(Math.Max(loopMinEdge, priceIncrement), 2);
                    freeLookPx = (_ticket.LastContraFillPx * -1.0) - priceBackup;
                }

                bool doFreeLooking = false;
                if (_ticket.IsFreeLooking)
                {
                    _ticket.IsFreeLooking = false;
                }
                else
                {
                    if (_ticket.IsBasketOrder && Settings.LoopFreeLookOnAll && !skipFreeLookAll && (!Settings.FreeLookRequireMinFillTime || _ticket.FillTime > Settings.FreeLookMinFillTime))
                    {
                        double priceBackup = 0;
                        double prevPrice = freeLookPx;
                        if (_ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                        {
                            var priceIncrement = (double)_ticket.GetPriceIncrement(freeLookPx, IncrementDirection.Up);

                            double settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks ?
                                Settings.FreeLookOnAllIncrementTicks * priceIncrement :
                                Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);


                            freeLookPx += priceBackup;
                        }
                        else
                        {
                            var priceIncrement = (double)_ticket.GetPriceIncrement(freeLookPx, IncrementDirection.Down);

                            double settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks ?
                                Settings.FreeLookOnAllIncrementTicks * priceIncrement :
                                Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);

                            freeLookPx -= priceBackup;
                        }
                        type = FREELOOK_ALL_TYPE;
                        doFreeLooking = true;
                        _log.Info("Price backup applied for free look." +
                                  "Id: " + _ticket.SpreadId +
                                  "Prev Price: " + prevPrice + ", " +
                                  "Price Backup: " + priceBackup + ", " +
                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                  "Price: " + freeLookPx);
                    }
                }

                SetAttemptedEdgeOnOpen(freeLookPx);
                Disable(resetSize: false);
                _ticket.IsFreeLooking = doFreeLooking;
                SubmitOrderAsync(freeLookPx, type, receiveTime);
            }
        }

        internal async Task<bool> ContLoopAsync(DateTime receiveTime)
        {
            _log.Info($"{nameof(ContLoopAsync)} Cont Loop. Id: {_ticket.SpreadId}, Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}");
            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(ContLoopAsync) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(ContLoopAsync) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            _closing = false;

            bool doNotResubmitLastFill = true;
            if (Math.Abs(_ticket.LastFillPx - _ticket.AveragePrice) < PX_TOLERANCE)
            {
                _resubmitLastFilledCounter++;
                doNotResubmitLastFill = _resubmitLastFilledCounter > LoopResubmit;
            }

            double price = double.NaN;
            bool disableRounding = false;
            string dynamicRoute = null;

            if (_ticket.IsBasketOrder &&
                AutomationConfig.LooperDynamicRouting &&
                !AutomationConfig.AttemptIncrementUsingDynamicRoute &&
                !string.IsNullOrWhiteSpace(_ticket.LastLoopRoute))
            {
                _ticket.LastLoopRoute = null;
                price = _ticket.LastFillPx;
            }
            else if (_ticket.IsBasketOrder &&
                     AutomationConfig.LooperDynamicRouting &&
                     AutomationConfig.AttemptIncrementUsingDynamicRoute &&
                     !_isFreeLookingOnAll &&
                     !double.IsNaN(_prevPriceBeforeDynamicRouting) &&
                     !string.IsNullOrWhiteSpace(_ticket.LastLoopRoute))
            {
                _ticket.LastLoopRoute = null;
                price = _prevPriceBeforeDynamicRouting;
            }
            else if (++_resubmitAllAttemptsCounter > LoopAttemptResubmit && doNotResubmitLastFill)
            {
                _resubmitAllAttemptsCounter = 0;

                var curSize = _ticket.Lcd;
                var preSize = LoopResubmitWithPrevSize == ResubmitSizeOption.OneLot ? 1 : Math.Max(1, _ticket.PrevQty);
                if (LoopResubmitWithPrevSize != ResubmitSizeOption.Off && curSize > preSize)
                {
                    LoopResubmitWithPrevSize = ResubmitSizeOption.Off;
                    price = _ticket.LastFillPx;
                    SizeUpLocked = true;
                    _ticket.UpdateQty(preSize);
                    _log.Info(nameof(CheckForResubmit) + " Resubmit with prev size." +
                                                         " Id: " + _ticket.SpreadId + "," +
                                                         " Cur Size: " + curSize + "," +
                                                         " Pre Size: " + preSize + "," +
                                                         " Loop Resub With No Size: " + LoopResubmitWithPrevSize + "," +
                                                         " Loop Resubmit: " + LoopResubmitCounter + "," +
                                                         " Resubmit Count: " + ResubmitCounter + ".");
                }
                else
                {
                    double increment;
                    double nextPrice;
                    if (_ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                    {
                        var incrementDirection = IncrementDirection.Down;
                        var loopIncrement = GetLoopIncrement(_ticket.LastFillPx, incrementDirection, _ticket.Width);
                        var lastPrice = _ticket.LastFillPx;

                        GetNextIncrementRoundingAndRoute(lastPrice,
                            loopIncrement,
                            incrementDirection,
                            out disableRounding,
                            out increment,
                            out dynamicRoute);

                        if (!_isFreeLookingOnAll)
                        {
                            _prevPriceBeforeDynamicRouting = _ticket.LastFillPx;
                        }

                        if (_ticket.LastFillPx > _ticket.AveragePrice && _ticket.LastFillPx - increment < _ticket.AveragePrice)
                        {
                            _ticket.LastFillPx = _ticket.AveragePrice;
                            nextPrice = _ticket.LastFillPx;
                        }
                        else
                        {
                            nextPrice = _ticket.LastFillPx - increment;
                            if (!_usingFreeLookIncrement)
                            {
                                _ticket.LastFillPx = nextPrice;
                            }
                        }
                    }
                    else
                    {
                        var incrementDirection = IncrementDirection.Up;
                        var loopIncrement = GetLoopIncrement(_ticket.LastFillPx, incrementDirection, _ticket.Width);
                        var lastPrice = _ticket.LastFillPx;

                        GetNextIncrementRoundingAndRoute(lastPrice,
                            loopIncrement,
                            incrementDirection,
                            out disableRounding,
                            out increment,
                            out dynamicRoute);

                        if (!_isFreeLookingOnAll)
                        {
                            _prevPriceBeforeDynamicRouting = _ticket.LastFillPx;
                        }

                        if (_ticket.LastFillPx < _ticket.AveragePrice && _ticket.LastFillPx + increment > _ticket.AveragePrice)
                        {
                            _ticket.LastFillPx = _ticket.AveragePrice;
                            nextPrice = _ticket.LastFillPx;
                        }
                        else
                        {
                            nextPrice = _ticket.LastFillPx + increment;
                            if (!_usingFreeLookIncrement)
                            {
                                _ticket.LastFillPx = nextPrice;
                            }
                        }
                    }

                    if (!_ticket.IsBasketOrder)
                    {
                        price = nextPrice;
                    }
                    else
                    {
                        double hedgeRevDelta = _ticket.TotalDelta;
                        if (_ticket.IsSingleLegSell)
                        {
                            hedgeRevDelta *= -1;
                        }

                        double change = hedgeRevDelta > 0
                            ? (_ticket.UnderAsk - _ticket.LastFillUnderAskPx) * _ticket.TotalDelta
                            : (_ticket.UnderBid - _ticket.LastFillUnderBidPx) * _ticket.TotalDelta;

                        switch (Settings.LoopPricingMode)
                        {
                            case LoopPricingMode.PriceIncrement:
                                price = nextPrice;
                                break;
                            case LoopPricingMode.DeltaAdjustedLastFillPrice:
                                price = nextPrice + change;
                                break;
                            case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                                price = _ticket.IsSingleLegSell ? Math.Max(nextPrice, nextPrice + change) : Math.Min(nextPrice, nextPrice + change);
                                break;
                            case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                                price = _ticket.IsSingleLegSell ? Math.Min(nextPrice, nextPrice + change) : Math.Max(nextPrice, nextPrice + change);
                                break;
                            case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                                change = _ticket.NetDeltaAdjTheo - _ticket.LastFillAdjTheo;
                                price = nextPrice + change;
                                break;
                        }

                        TagOrderDetails(receiveTime, nextPrice, change, price, increment, hedgeRevDelta);
                    }
                    ResubmitCounter++;
                }
            }
            else
            {
                price = _ticket.LastFillPx;
            }

            if (_ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
            {
                if (price < _loopStopPx || double.IsNaN(_loopStopPx))
                {
                    _log.Info(nameof(ContLoopAsync) + " Stop price reached." +
                                                 " Id: " + _ticket.SpreadId + "," +
                                                 " Price: " + price + "," +
                                                 " Stop price: " + _loopStopPx + "," +
                                                 " Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                                 " Resubmit Count: " + ResubmitCounter + ".");
                    CheckForResubmit(receiveTime);
                    return false;
                }
            }
            else
            {
                if (price > _loopStopPx || double.IsNaN(_loopStopPx))
                {
                    _log.Info(nameof(ContLoopAsync) + " Stop price reached." +
                                                 " Id: " + _ticket.SpreadId + "," +
                                                 " Price: " + price + "," +
                                                 " Stop price: " + _loopStopPx + "," +
                                                 " Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                                 " Resubmit Count: " + ResubmitCounter + ".");
                    CheckForResubmit(receiveTime);
                    return false;
                }
            }

            if (ResubmitCounter >= 50)
            {
                _log.Info(nameof(ContLoopAsync) + " Max risk resubmit count reached." +
                                             " Id: " + _ticket.SpreadId + "," +
                                             " Resubmit Count: " + ResubmitCounter + ".");
                _ticket.ShowMessage($"Loop failed after {ResubmitCounter} attempts.", _ticket.SpreadId, canBeSilended: false);
                return false;
            }

            SetAttemptedEdgeOnOpen(price);

            if (!SizeUpLocked)
            {
                LoopResubmitWithPrevSize = await _ticket.CheckLoopSizeUpAsync(_ticket.AttemptedEdge, savePrevSize: false, allowReverse: false);
            }

            if (_ticket.MainNotFilled)
            {
                SubmitOrderAsync(price, LOOPER_TYPE, receiveTime, dynamicRoute, disableRounding);
                return true;
            }

            return false;
        }

        private void TagOrderDetails(DateTime receiveTime, double nextPrice, double change, double price, double increment,
            double hedgeRevDelta)
        {
            _log.Info(nameof(ContLoopAsync) + " Using: " + Settings.LoopPricingMode + ", " +
                      "Id: " + _ticket.SpreadId + ", " +
                      "Next Price: " + nextPrice + ", " +
                      "Delta: " + _ticket.TotalDelta + ", " +
                      "Change: " + change + ", " +
                      "Last Fill: " + _ticket.LastFillPx + ", " +
                      "Last Fill Under Bid: " + _ticket.LastFillUnderBidPx + ", " +
                      "Last Fill Under: " + _ticket.LastFillUnderPx + ", " +
                      "Last Fill Under Ask: " + _ticket.LastFillUnderAskPx + ", " +
                      "Last Under: " + _ticket.UnderMid + ", " +
                      "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                      "Price: " + price + ", " +
                      "Increment: " + increment + ".");

            _ticket.SetOrderDetailTag("Delta", _ticket.TotalDelta.ToString());
            _ticket.SetOrderDetailTag("Px Increment", increment.ToString());
            _ticket.SetOrderDetailTag("Rev Delta", hedgeRevDelta.ToString());
            _ticket.SetOrderDetailTag("Delta Adj Change", change.ToString());
            _ticket.SetOrderDetailTag("Last Fill Px", _ticket.LastFillPx.ToString());
            _ticket.SetOrderDetailTag("Next Price Px", price.ToString());
            _ticket.SetOrderDetailTag("Under Bid", _ticket.UnderBid.ToString());
            _ticket.SetOrderDetailTag("Under Ask", _ticket.UnderAsk.ToString());
            _ticket.SetOrderDetailTag("Last Fill Under Bid", _ticket.LastFillUnderBidPx.ToString());
            _ticket.SetOrderDetailTag("Last Fill Under Ask", _ticket.LastFillUnderAskPx.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());
        }

        internal async Task<bool> StartClosingLoop(DateTime receiveTime, bool checkForIceberg = true)
        {
            _log.Info($"{nameof(StartClosingLoop)} Start Closing Loop. Id: {_ticket.SpreadId}, Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}");
            OrderSubType subType;

            if (_ticket.IsBasketOrder)
            {
                subType = LOOPER_TYPE;
                AutomationConfig =
                    _ticket.BasketTraderViewModel.GetAutomationConfig(_ticket.Underlying,
                        (double)_ticket.PriceIncrement);
            }
            else
            {
                subType = OrderSubType.Looper;
            }

            _ticket.IsLooping = true;
            _closing = true;
            _resubmitLastFilledCounter = 0;
            _resubmitAllAttemptsCounter = 0;
            _ticket.ContraPartiallyFilled = false;
            _usingFreeLookIncrement = false;
            _ticket.ContraLeavesQty = 0;
            _ticket.ContraCumulativeQty = 0;
            _prevContraPriceBeforeDynamicRouting = double.NaN;
            ResubmitCounter = 0;
            LoopResubmitCounter = 0;
            LoopResubmitWithPrevSize = ResubmitSizeOption.Off;

            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(StartLoop) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(StartLoop) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            if (_ticket.IsBasketOrder && checkForIceberg)
            {
                if (AutomationConfig.IcebergCloserEnabled)
                {
                    if (_ticket.Lcd >= AutomationConfig.IcebergTotalSize && AutomationConfig.IcebergTotalSize > AutomationConfig.IcebergDisplaySize)
                    {
                        IcebergRunning = true;
                        IcebergTotalQty = _ticket.Lcd;
                        _ticket.UpdateQty(AutomationConfig.IcebergDisplaySize);
                    }
                }
            }

            double fillPx = _ticket.LastFillPx;
            double loopMaxLoss = GetLoopMaxLoss();
            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    _loopCloseStopPx = fillPx - loopMaxLoss;
                }
                else
                {
                    _loopCloseStopPx = fillPx + loopMaxLoss;
                }
            }
            else
            {
                _loopCloseStopPx = (fillPx * -1.0) + loopMaxLoss;
            }
            _loopCloseStopPx = Math.Round(_loopCloseStopPx, 2, MidpointRounding.AwayFromZero);
            _ticket.SetOrderDetailTag("Fill Px", fillPx.ToString());
            _ticket.SetOrderDetailTag("Max Loss", loopMaxLoss.ToString());
            _ticket.SetOrderDetailTag("Loop Close Stop Px", _loopCloseStopPx.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
            _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());

            double lastFillPx = GetLastFillPx();

            if (double.IsNaN(_ticket.LastEdge))
            {
                _resubmitLastFilledCounter = LoopResubmit;
                double edge = ClosingEdge;
                if (_ticket.IsSingleLeg)
                {
                    if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        _ticket.LastContraFillPx = lastFillPx + edge;
                    }
                    else
                    {
                        _ticket.LastContraFillPx = lastFillPx - edge;
                    }
                }
                else
                {
                    _ticket.LastContraFillPx = (lastFillPx * -1.0) - edge;
                }
                _ticket.SetOrderDetailTag("Edge", edge.ToString());
                _ticket.SetOrderDetailTag("Last Fill Px", lastFillPx.ToString());
                _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
                _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());
            }
            else
            {
                double lastEdge = _ticket.LastEdge;
                if (Settings.MaintainLastEdge)
                {
                    double priceWhenUsingLastEdge;
                    if (_ticket.IsSingleLeg)
                    {
                        if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                        {
                            priceWhenUsingLastEdge = lastFillPx + lastEdge;

                            if (priceWhenUsingLastEdge > _ticket.LastContraFillPx)
                            {
                                _ticket.LastContraFillPx = priceWhenUsingLastEdge;
                            }
                        }
                        else
                        {
                            priceWhenUsingLastEdge = lastFillPx - lastEdge;

                            if (priceWhenUsingLastEdge < _ticket.LastContraFillPx)
                            {
                                _ticket.LastContraFillPx = priceWhenUsingLastEdge;
                            }
                        }
                    }
                    else
                    {
                        priceWhenUsingLastEdge = (lastFillPx * -1.0) - lastEdge;

                        if (priceWhenUsingLastEdge < _ticket.LastContraFillPx)
                        {
                            _ticket.LastContraFillPx = priceWhenUsingLastEdge;
                        }
                    }


                    _ticket.SetOrderDetailTag("Maintained Edge", lastEdge.ToString());
                    _ticket.SetOrderDetailTag("Last Fill Px", lastFillPx.ToString());
                    _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
                    _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());
                }

                if (!_ticket.IsBasketOrder)
                {
                    _isFreeLookingOnAll = false;
                }
                else
                {
                    double prevPrice = Math.Round(_ticket.LastContraFillPx, 2);
                    bool autoAggressorApplied = false;
                    bool isSingleLegBuy = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
                    IncrementDirection incrementDirection = isSingleLegBuy ? IncrementDirection.Up : IncrementDirection.Down;
                    double priceIncrement = (double)_ticket.GetPriceIncrement(_ticket.LastContraFillPx, incrementDirection);

                    double priceBackup = AutomationConfig.AutoAggressorEdgeTightenMode == AutoAggressorEdgeTightenMode.Percentage ? lastEdge * AutomationConfig.AutoAggressorEdgeTightenPercentage : priceIncrement;

                    if (AutomationConfig.AutoAggressorEnabled && lastEdge >= priceBackup + _ticket.GetLoopMinEdge())
                    {
                        switch (AutomationConfig.AutoAggressorMode)
                        {
                            case AutoAggressorMode.OnFirstSize:
                                autoAggressorApplied = _ticket.Lcd > 1 && !FirstSizeAggressorApplied;
                                FirstSizeAggressorApplied = true;
                                break;
                            case AutoAggressorMode.OnAllSize:
                                autoAggressorApplied = _ticket.Lcd > 1;
                                break;
                            case AutoAggressorMode.OnAll:
                                autoAggressorApplied = true;
                                break;
                        }
                    }

                    if (autoAggressorApplied)
                    {
                        if (isSingleLegBuy)
                        {
                            _ticket.LastContraFillPx -= priceBackup;
                        }
                        else
                        {
                            _ticket.LastContraFillPx += priceBackup;
                        }

                        _ticket.LastContraFillPx = Math.Round(_ticket.LastContraFillPx, 2);
                        _isFreeLookingOnAll = false;
                        subType = AUTO_AGGRESSOR_TYPE;

                        _ticket.SetOrderDetailTag(subType.ToString(), priceBackup.ToString());
                        _log.Info($"{subType} applied." +
                                  "Id: " + _ticket.SpreadId +
                                  "Prev Price: " + prevPrice + ", " +
                                  "Price Backup: " + priceBackup + ", " +
                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                  "Price: " + _ticket.LastContraFillPx);

                    }
                    else if (Settings.LoopFreeLookOnAll && (!Settings.FreeLookRequireMinFillTime || _ticket.FillTime > Settings.FreeLookMinFillTime))
                    {
                        if (isSingleLegBuy)
                        {
                            double settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks
                                ? Settings.FreeLookOnAllIncrementTicks * priceIncrement
                                : Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);

                            _ticket.LastContraFillPx += priceBackup;
                        }
                        else
                        {
                            double settingsIncrement = Settings.LoopFreeLookOnAllUsingTicks
                                ? Settings.FreeLookOnAllIncrementTicks * priceIncrement
                                : Settings.FreeLookOnAllIncrement;
                            priceBackup = Math.Round(Math.Max(settingsIncrement, priceIncrement), 2);

                            _ticket.LastContraFillPx -= priceBackup;
                        }

                        _ticket.LastContraFillPx = Math.Round(_ticket.LastContraFillPx, 2);
                        _isFreeLookingOnAll = priceBackup > 0;
                        subType = FREELOOK_ALL_TYPE;

                        _ticket.SetOrderDetailTag("Loop Px Backup", priceBackup.ToString());
                        _log.Info($"{subType} applied." +
                                  "Id: " + _ticket.SpreadId +
                                  "Prev Price: " + prevPrice + ", " +
                                  "Price Backup: " + priceBackup + ", " +
                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                  "Price: " + _ticket.LastContraFillPx);
                    }
                    else
                    {
                        _isFreeLookingOnAll = false;
                    }
                }
            }

            if (IcebergRunning)
            {
                subType = ICEBERG_TYPE;
            }

            if (Settings.AdjustClosingPriceToMarket && !_ticket.IsStockTied)
            {
                ;
                double bid = _ticket.Low;
                double ask = _ticket.High;
                if (_ticket.IsSingleLeg)
                {
                    if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        if (_ticket.LastContraFillPx > ask)
                        {
                            _log.Info("Adjusting Closing Price To MKT" + ", " +
                                      "Id: " + _ticket.SpreadId + ", " +
                                      "Last Price: " + lastFillPx + ", " +
                                      "Prev Price: " + _ticket.LastContraFillPx + ", " +
                                      "Winner Only: " + Settings.AdjustClosingPriceToMarketWinnersOnly + ", " +
                                      "Mkt: [" + bid + "X" + ask + "] " +
                                      "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                      "Price: " + ask);

                            if (!Settings.AdjustClosingPriceToMarketWinnersOnly || lastFillPx < ask)
                            {
                                _ticket.LastContraFillPx = ask;
                            }
                        }
                    }
                    else
                    {
                        if (_ticket.LastContraFillPx < bid)
                        {
                            _log.Info("Adjusting Closing Price To MKT" + ", " +
                                      "Id: " + _ticket.SpreadId + ", " +
                                      "Last Price: " + lastFillPx + ", " +
                                      "Prev Price: " + _ticket.LastContraFillPx + ", " +
                                      "Winner Only: " + Settings.AdjustClosingPriceToMarketWinnersOnly + ", " +
                                      "Mkt: [" + bid + "X" + ask + "] " +
                                      "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                      "Price: " + bid);

                            if (!Settings.AdjustClosingPriceToMarketWinnersOnly || lastFillPx > bid)
                            {
                                _ticket.LastContraFillPx = bid;
                            }
                        }
                    }
                }
                else
                {
                    if (_ticket.LastContraFillPx < -ask)
                    {
                        _log.Info("Adjusting Closing Price To MKT" + ", " +
                                  "Id: " + _ticket.SpreadId + ", " +
                                  "Last Price: " + lastFillPx + ", " +
                                  "Prev Price: " + _ticket.LastContraFillPx + ", " +
                                  "Winner Only: " + Settings.AdjustClosingPriceToMarketWinnersOnly + ", " +
                                  "Mkt: [" + bid + "X" + ask + "] " +
                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                  "Price: " + -ask);
                        if (!Settings.AdjustClosingPriceToMarketWinnersOnly || lastFillPx < ask)
                        {
                            _ticket.LastContraFillPx = -ask;
                        }
                    }
                }
            }

            bool proceed = await CheckForResubmitLimit();

            if (!proceed)
            {
                _log.Warn($"Loop Close disabled for possible too many resubmit attempts. " +
                           "Id: " + _ticket.SpreadId + ", " +
                           "Price: " + _ticket.LastContraFillPx);
                Disable();
                return false;
            }

            double attemptedEdge = SetAttemptedEdgeOnClose(_ticket.LastContraFillPx);

            bool hedged = _ticket.CheckForAutoHedge(attemptedEdge);
            if (hedged)
            {
                Disable();
                return !Settings.AutoHedgeOpenTicket;
            }

            double? interval = null;
            if (TryGetCloseDynamicIntervalAndResubmit(attemptedEdge, _ticket.Lcd, out double attemptInterval, out _, out string dynamicRoute, out bool disableRounding) && attemptInterval > 0)
            {
                interval = attemptInterval;
            }
            SubmitClosingOrderAsync(_ticket.LastContraFillPx, subType, receiveTime, interval, dynamicRoute, disableRounding);
            return true;
        }

        internal async Task<bool> ContClose(DateTime receiveTime)
        {
            _log.Info($"{nameof(ContClose)} Cont Close. Id: {_ticket.SpreadId}, Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}");
            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(ContClose) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(ContClose) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return false;
            }

            _closing = true;

            bool doNotResubmitLastFill = true;
            if (Math.Abs(_ticket.LastContraFillPx - _ticket.ContraAveragePrice) < PX_TOLERANCE)
            {
                _resubmitLastFilledCounter++;
                doNotResubmitLastFill = _resubmitLastFilledCounter > LoopResubmit;
            }

            double? interval = null;
            double contraPrice = double.NaN;
            _resubmitAllAttemptsCounter++;

            int loopAttemptResubmit = LoopAttemptResubmit;
            if (TryGetCloseDynamicIntervalAndResubmit(_ticket.AttemptedEdge, _ticket.Lcd, out double attemptInterval, out int resubmitCount, out string dynamicRoute, out var disableRounding))
            {
                loopAttemptResubmit = resubmitCount;
                if (attemptInterval > 0)
                {
                    interval = attemptInterval;
                }
                _log.Info($"{nameof(ContClose)} Resubmit count override. Id: {_ticket.SpreadId}, Attempt: {LoopAttemptResubmit}, Override: {loopAttemptResubmit}, Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}");
            }

            if (_ticket.IsBasketOrder &&
                AutomationConfig.LooperDynamicRouting &&
                !AutomationConfig.AttemptIncrementUsingDynamicRoute &&
                !string.IsNullOrWhiteSpace(_ticket.LastLoopContraRoute))
            {
                _ticket.LastLoopContraRoute = null;
                contraPrice = _ticket.LastContraFillPx;
            }
            else if (_ticket.IsBasketOrder &&
                     AutomationConfig.LooperDynamicRouting &&
                     AutomationConfig.AttemptIncrementUsingDynamicRoute &&
                     !_isFreeLookingOnAll &&
                     !double.IsNaN(_prevContraPriceBeforeDynamicRouting) &&
                     !string.IsNullOrWhiteSpace(_ticket.LastLoopContraRoute))
            {
                _ticket.LastLoopContraRoute = null;
                contraPrice = _prevContraPriceBeforeDynamicRouting;
            }
            else if (_resubmitAllAttemptsCounter > loopAttemptResubmit &&
                doNotResubmitLastFill)
            {
                if (IcebergRunning)
                {
                    IcebergRunning = false;
                    _ticket.UpdateQty(IcebergTotalQty);
                    return await StartClosingLoop(receiveTime, false);
                }

                _resubmitAllAttemptsCounter = 0;
                double increment;
                double nextPrice;
                if (_ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
                {
                    var incrementDirection = IncrementDirection.Down;
                    var loopIncrement = GetLoopIncrement(_ticket.LastContraFillPx, incrementDirection, _ticket.Width, log: true, !disableRounding);
                    var lastPrice = _ticket.LastContraFillPx;

                    GetNextIncrementRoundingAndRoute(lastPrice,
                        loopIncrement,
                        incrementDirection,
                        out disableRounding,
                        out increment,
                        out dynamicRoute);

                    if (!_isFreeLookingOnAll)
                    {
                        _prevContraPriceBeforeDynamicRouting = _ticket.LastContraFillPx;
                    }

                    if (_ticket.LastContraFillPx > _ticket.ContraAveragePrice && _ticket.LastContraFillPx - increment < _ticket.ContraAveragePrice)
                    {
                        _ticket.LastContraFillPx = _ticket.ContraAveragePrice;
                        nextPrice = _ticket.LastContraFillPx;
                    }
                    else
                    {
                        nextPrice = _ticket.LastContraFillPx - increment;

                        if (!_usingFreeLookIncrement)
                        {
                            _ticket.LastContraFillPx = nextPrice;
                        }
                    }
                }
                else
                {
                    var incrementDirection = IncrementDirection.Up;
                    var loopIncrement = GetLoopIncrement(_ticket.LastContraFillPx, incrementDirection, _ticket.Width, log: true, !disableRounding);
                    var lastPrice = _ticket.LastContraFillPx;

                    GetNextIncrementRoundingAndRoute(lastPrice,
                        loopIncrement,
                        incrementDirection,
                        out disableRounding,
                        out increment,
                        out dynamicRoute);

                    if (!_isFreeLookingOnAll)
                    {
                        _prevContraPriceBeforeDynamicRouting = _ticket.LastContraFillPx;
                    }

                    if (_ticket.LastContraFillPx < _ticket.ContraAveragePrice && _ticket.LastContraFillPx + increment > _ticket.ContraAveragePrice)
                    {
                        _ticket.LastContraFillPx = _ticket.ContraAveragePrice;
                        nextPrice = _ticket.LastContraFillPx;
                    }
                    else
                    {
                        nextPrice = _ticket.LastContraFillPx + increment;
                        if (!_usingFreeLookIncrement)
                        {
                            _ticket.LastContraFillPx = nextPrice;
                        }
                    }
                }

                if (!_ticket.IsBasketOrder ||
                    Settings.LoopPricingMode == LoopPricingMode.PriceIncrement ||
                    double.IsNaN(_ticket.LastFillUnderPx))
                {
                    contraPrice = nextPrice;
                    _log.Info(nameof(ContClose) + " Using price increment. " +
                                                  "Id: " + _ticket.SpreadId + ", " +
                                                  "Last Fill: " + _ticket.LastContraFillPx + ", " +
                                                  "Next Px: " + nextPrice + ", " +
                                                  "Last Fill Under Bid: " + _ticket.LastFillUnderBidPx + ", " +
                                                  "Last Fill Under Mid: " + _ticket.LastFillUnderPx + ", " +
                                                  "Last Fill Under Ask: " + _ticket.LastFillUnderAskPx + ", " +
                                                  "Price: " + contraPrice + ", " +
                                                  "Increment: " + increment + ", " +
                                                  "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                                  "Resubmit Count: " + ResubmitCounter + ".");
                }
                else
                {
                    double contraDelta = _ticket.IsSingleLeg ? _ticket.TotalDelta : -_ticket.TotalDelta;

                    double hedgeRevDelta = contraDelta;
                    if (!_ticket.IsSingleLegSell)
                    {
                        hedgeRevDelta *= -1;
                    }

                    double change = hedgeRevDelta > 0
                        ? (_ticket.UnderAsk - _ticket.LastFillUnderAskPx) * contraDelta
                        : (_ticket.UnderBid - _ticket.LastFillUnderBidPx) * contraDelta;


                    switch (Settings.LoopPricingMode)
                    {
                        case LoopPricingMode.DeltaAdjustedLastFillPrice:
                            contraPrice = nextPrice + change;
                            break;
                        case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                            contraPrice = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                                ? Math.Max(nextPrice, nextPrice + change)
                                : Math.Min(nextPrice, nextPrice + change);
                            break;
                        case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                            contraPrice = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                                ? Math.Min(nextPrice, nextPrice + change)
                                : Math.Max(nextPrice, nextPrice + change);
                            break;
                        case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                            change = _ticket.IsSingleLeg
                                ? _ticket.NetDeltaAdjTheo - _ticket.LastContraFillAdjTheo
                                : -(_ticket.NetDeltaAdjTheo - _ticket.LastContraFillAdjTheo);
                            contraPrice = nextPrice + change;
                            break;
                    }

                    _log.Info(nameof(ContClose) + " Using: " + Settings.LoopPricingMode + ", " +
                              "Id: " + _ticket.SpreadId + ", " +
                              "Delta: " + contraDelta + ", " +
                              "Change: " + change + ", " +
                              "Next Px: " + nextPrice + ", " +
                              "Last Fill: " + _ticket.LastContraFillPx + ", " +
                              "Last Fill Under Bid: " + _ticket.LastFillUnderBidPx + ", " +
                              "Last Fill Under Mid: " + _ticket.LastFillUnderPx + ", " +
                              "Last Fill Under Ask: " + _ticket.LastFillUnderAskPx + ", " +
                              "Last Under: " + _ticket.UnderMid + ", " +
                              "Price: " + contraPrice + ", " +
                              "Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                              "Resubmit Count: " + ResubmitCounter + ".");

                    _ticket.SetOrderDetailTag("Delta", contraDelta.ToString());
                    _ticket.SetOrderDetailTag("Rev Delta", hedgeRevDelta.ToString());
                    _ticket.SetOrderDetailTag("Delta Adj Change", change.ToString());
                    _ticket.SetOrderDetailTag("Last Fill Px", _ticket.LastContraFillPx.ToString());
                    _ticket.SetOrderDetailTag("Next Price Px", contraPrice.ToString());
                    _ticket.SetOrderDetailTag("Under Bid", _ticket.UnderBid.ToString());
                    _ticket.SetOrderDetailTag("Under Ask", _ticket.UnderAsk.ToString());
                    _ticket.SetOrderDetailTag("Last Fill Under Bid", _ticket.LastFillUnderBidPx.ToString());
                    _ticket.SetOrderDetailTag("Last Fill Under Ask", _ticket.LastFillUnderAskPx.ToString());
                    _ticket.SetOrderDetailTag("HardSide At Trade", _ticket.HardSide.ToString());
                    _ticket.SetOrderDetailTag("HardSide At Trade Time", _ticket.HardSideDesignationTime.ToString());
                }
                ResubmitCounter++;
            }
            else
            {
                contraPrice = _ticket.LastContraFillPx;
            }
            contraPrice = Math.Round(contraPrice, 2, MidpointRounding.AwayFromZero);


            if (_ticket.Legs.Count == 1 && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy)
            {
                if (contraPrice < _loopCloseStopPx || double.IsNaN(_loopCloseStopPx))
                {
                    _log.Info(nameof(ContClose) + " Closing stop price reached." +
                                                  " Id: " + _ticket.SpreadId + "," +
                                                  " Price: " + contraPrice + "," +
                                                  " Stop price: " + _loopCloseStopPx + "," +
                                                  " Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                                  " Resubmit Count: " + ResubmitCounter + ".");

                    if (_ticket.CheckForAutoHedge(_ticket.CalculateAttemptedEdgeOnClose(contraPrice), true))
                    {
                        Disable();
                        return !Settings.AutoHedgeOpenTicket;
                    }

                    Disable();
                    return false;
                }
            }
            else
            {
                if (contraPrice > _loopCloseStopPx || double.IsNaN(_loopCloseStopPx))
                {
                    _log.Info(nameof(ContClose) + " Closing stop price reached." +
                                                  " Id: " + _ticket.SpreadId + "," +
                                                  " Price: " + contraPrice + "," +
                                                  " Stop price: " + _loopCloseStopPx + "," +
                                                  " Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                                  " Resubmit Count: " + ResubmitCounter + ".");

                    if (_ticket.CheckForAutoHedge(_ticket.CalculateAttemptedEdgeOnClose(contraPrice), true))
                    {
                        Disable();
                        return !Settings.AutoHedgeOpenTicket;
                    }

                    Disable();
                    return false;
                }
            }

            if (ResubmitCounter >= MAX_RESUB)
            {
                _log.Info(nameof(ContClose) + " Max risk resubmit count reached." +
                                              " Id: " + _ticket.SpreadId + "," +
                                              " Latency: " + (DateTime.Now - receiveTime).TotalMilliseconds + ", " +
                                              " Resubmit Count: " + ResubmitCounter + ".");


                if (_ticket.CheckForAutoHedge(_ticket.CalculateAttemptedEdgeOnClose(contraPrice), true))
                {
                    Disable();
                    return !Settings.AutoHedgeOpenTicket;
                }

                Disable();
                _ticket.ShowMessage($"Loop Closer failed after {ResubmitCounter} attempts.\nManual intervention required to close.", _ticket.SpreadId, canBeSilended: false);
                return false;
            }

            double attemptedEdge = SetAttemptedEdgeOnClose(_ticket.LastContraFillPx);

            if (_ticket.CheckForAutoHedge(attemptedEdge))
            {
                Disable();
                return !Settings.AutoHedgeOpenTicket;
            }

            if (_ticket.ContraNotFilled)
            {
                SubmitClosingOrderAsync(contraPrice, IcebergRunning ? ICEBERG_TYPE : LOOPER_TYPE, receiveTime, interval, dynamicRoute, disableRounding);
                return true;
            }

            return false;
        }

        private void GetNextIncrementRoundingAndRoute(
            double lastPrice,
            double loopIncrement,
            IncrementDirection incrementDirection,
            out bool disableRounding,
            out double increment,
            out string routeOverride)
        {
            if (_ticket.IsBasketOrder && _isFreeLookingOnAll &&
                ((AutomationConfig.LoopFreeLookOnAllUsingTicks && AutomationConfig.FreeLookOnAllWalkBackIncrementTicks > 0) ||
                 (!AutomationConfig.LoopFreeLookOnAllUsingTicks && AutomationConfig.FreeLookOnAllWalkBackIncrement > 0)))
            {
                double defaultIncrement = (double)_ticket.GetPriceIncrement(lastPrice, incrementDirection);
                _isFreeLookingOnAll = false;
                _usingFreeLookIncrement = false;
                disableRounding = false;

                var walkBackIncrement = AutomationConfig.LoopFreeLookOnAllUsingTicks ?
                    Math.Round(AutomationConfig.FreeLookOnAllWalkBackIncrementTicks * defaultIncrement, 2) :
                    AutomationConfig.FreeLookOnAllWalkBackIncrement;
                increment = Math.Max(defaultIncrement, walkBackIncrement);

                routeOverride = null;
                return;
            }

            if (!_usingFreeLookIncrement &&
                IsValidForNickelIncrementOverride(lastPrice, loopIncrement, incrementDirection))
            {
                _usingFreeLookIncrement = true;
                disableRounding = true;
                increment = AutomationConfig.LoopFreeLookOnNickelNamesIncrement;
                routeOverride = AutomationConfig.LoopFreeLookOnNickelNamesRoute;
            }
            else if (!_usingFreeLookIncrement &&
                     IsValidForDimeIncrementOverride(lastPrice, loopIncrement, incrementDirection))
            {
                _usingFreeLookIncrement = true;
                disableRounding = true;
                increment = AutomationConfig.LoopFreeLookOnDimeNamesIncrement;
                routeOverride = AutomationConfig.LoopFreeLookOnDimeNamesRoute;
            }
            else
            {
                _usingFreeLookIncrement = false;
                disableRounding = false;
                increment = loopIncrement;
                routeOverride = null;
            }
        }

        private void CheckForResubmit(DateTime receiveTime)
        {
            _log.Info(nameof(CheckForResubmit) + " Cont Loop. Id: " + _ticket.SpreadId);
            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(CheckForResubmit) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(CheckForResubmit) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!_ticket.IsBasketOrder)
            {
                _log.Info(nameof(CheckForResubmit) + " Resubmit not valid for tickets. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            AutomationConfigModel automationConfig = AutomationConfig;

            if (automationConfig == null)
            {
                _log.Info(nameof(CheckForResubmit) + " Resubmit not valid no config. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (automationConfig.LoopSizeupType != LoopSizeupType.Dynamic)
            {
                _log.Info(nameof(CheckForResubmit) + " Resubmit not valid for current size-up mode. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            var curSize = _ticket.Lcd;
            var preSize = LoopResubmitWithPrevSize == ResubmitSizeOption.OneLot ? 1 : Math.Max(1, _ticket.PrevQty);
            if (LoopResubmitWithPrevSize != ResubmitSizeOption.Off && curSize > preSize)
            {
                LoopResubmitWithPrevSize = ResubmitSizeOption.Off;
                _ticket.UpdateQty(preSize);
                _log.Info(nameof(CheckForResubmit) + " Resubmit with no size." +
                                                     " Id: " + _ticket.SpreadId + "," +
                                                     " Loop Resub With No Size: " + LoopResubmitWithPrevSize + "," +
                                                     " Loop Resubmit: " + LoopResubmitCounter + "," +
                                                     " Resubmit Count: " + ResubmitCounter + ".");
            }
            else if (LoopResubmitCounter++ >= _ticket.ResubmitAfterLastLoopCount)
            {
                _log.Info(nameof(CheckForResubmit) + " Max loop resubmit count reached." +
                                                     " Id: " + _ticket.SpreadId + "," +
                                                     " Loop Resub With No Size: " + LoopResubmitWithPrevSize + "," +
                                                     " Loop Resubmit: " + LoopResubmitCounter + "," +
                                                     " Resubmit Count: " + ResubmitCounter + ".");
                Disable();
                return;
            }

            _closing = false;

            double price = double.NaN;

            _resubmitAllAttemptsCounter = 0;

            if (!_ticket.IsBasketOrder)
            {
                price = _ticket.LastFillPx;
            }
            else
            {
                double hedgeRevDelta = _ticket.TotalDelta;
                if (_ticket.IsSingleLegSell)
                {
                    hedgeRevDelta *= -1;
                }

                double change = hedgeRevDelta > 0
                    ? (_ticket.UnderAsk - _ticket.LastFillUnderAskPx) * _ticket.TotalDelta
                    : (_ticket.UnderBid - _ticket.LastFillUnderBidPx) * _ticket.TotalDelta;

                switch (Settings.LoopPricingMode)
                {
                    case LoopPricingMode.PriceIncrement:
                        price = _ticket.LastFillPx;
                        break;
                    case LoopPricingMode.DeltaAdjustedLastFillPrice:
                        price = _ticket.LastFillPx + change;
                        break;
                    case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                        price = _ticket.IsSingleLegSell
                            ? Math.Max(_ticket.LastFillPx, _ticket.LastFillPx + change)
                            : Math.Min(_ticket.LastFillPx, _ticket.LastFillPx + change);
                        break;
                    case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                        price = _ticket.IsSingleLegSell
                            ? Math.Min(_ticket.LastFillPx, _ticket.LastFillPx + change)
                            : Math.Max(_ticket.LastFillPx, _ticket.LastFillPx + change);
                        break;
                    case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                        change = _ticket.NetDeltaAdjTheo - _ticket.LastFillAdjTheo;
                        price = _ticket.LastFillPx + change;
                        break;
                }
            }

            if (_ticket.IsSingleLegSell)
            {
                if (price < _loopStopPx || double.IsNaN(_loopStopPx))
                {
                    _log.Info(nameof(CheckForResubmit) + " Stop price reached." +
                                                 " Id: " + _ticket.SpreadId + "," +
                                                 " Price: " + price + "," +
                                                 " Stop price: " + _loopStopPx + "," +
                                                 " Loop Resub With No Size: " + LoopResubmitWithPrevSize + "," +
                                                 " Loop Resubmit: " + LoopResubmitCounter + "," +
                                                 " Resubmit Count: " + ResubmitCounter + ".");
                    Disable();
                    return;
                }
            }
            else
            {
                if (price > _loopStopPx || double.IsNaN(_loopStopPx))
                {
                    _log.Info(nameof(CheckForResubmit) + " Stop price reached." +
                                                 " Id: " + _ticket.SpreadId + "," +
                                                 " Price: " + price + "," +
                                                 " Stop price: " + _loopStopPx + "," +
                                                 " Loop Resub With No Size: " + LoopResubmitWithPrevSize + "," +
                                                 " Loop Resubmit: " + LoopResubmitCounter + "," +
                                                 " Resubmit Count: " + ResubmitCounter + ".");
                    Disable();
                    return;
                }
            }

            SetAttemptedEdgeOnOpen(price);

            if (_ticket.MainNotFilled)
            {
                SubmitOrderAsync(price, OrderSubType.LooperResubmit, receiveTime);
            }
        }

        private async Task<bool> CheckForResubmitLimit()
        {
            double startingPrice = _ticket.LastContraFillPx;
            double stopPrice = _loopCloseStopPx;
            bool isSingleLegBuy = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy;
            int count = 0;

            while (true)
            {
                if (isSingleLegBuy)
                {
                    startingPrice -= GetLoopIncrement(startingPrice, IncrementDirection.Down, _ticket.Width, log: false);
                    if (startingPrice < stopPrice)
                    {
                        break;
                    }
                }
                else
                {
                    startingPrice += GetLoopIncrement(startingPrice, IncrementDirection.Up, _ticket.Width, log: false);
                    if (startingPrice > stopPrice)
                    {
                        break;
                    }
                }
                count++;
            }

            int attemptResubmit = Settings.AttemptResubmit;
            if (attemptResubmit > 0)
            {
                count *= Settings.AttemptResubmit;
            }

            EstimatedResubmit = count;

            bool proceed = true;
            if (OmsCore.Config.LoopMaxResubmitWithIncrementCheckEnabled)
            {
                if (count > OmsCore.Config.LoopMaxResubmitWithIncrement)
                {
                    _log.Warn($"Attempted edge may require more than {count} increments. " +
                             "Id: " + _ticket.SpreadId + ", " +
                             "Start Price: " + startingPrice + ", " +
                             "Stop Price: " + stopPrice + ", " +
                             "Price: " + _ticket.LastContraFillPx);

                    proceed = await _ticket.GetVerificationAsync($"Loop Closer may require {count} attempts.\nAre you sure you want to proceed?", _ticket.SpreadId);
                }
            }
            return proceed;
        }

        private bool IsValidForNickelIncrementOverride(double lastPrice, double nextIncrement, IncrementDirection direction)
        {
            return _ticket.IsBasketOrder &&
                   _ticket.IsSingleLeg &&
                   AutomationConfig.LoopFreeLookOnNickelNames &&
                   AutomationConfig.LoopFreeLookOnNickelNamesIncrement > 0 &&
                   AutomationConfig.LoopFreeLookOnNickelNamesIncrement < nextIncrement &&
                   _ticket.GetPriceIncrement(lastPrice, direction) == .05M;
        }

        private bool IsValidForDimeIncrementOverride(double lastPrice, double nextIncrement, IncrementDirection direction)
        {
            return _ticket.IsBasketOrder &&
                   _ticket.IsSingleLeg &&
                   AutomationConfig.LoopFreeLookOnDimeNames &&
                   AutomationConfig.LoopFreeLookOnDimeNamesIncrement > 0 &&
                   AutomationConfig.LoopFreeLookOnDimeNamesIncrement < nextIncrement &&
                   _ticket.GetPriceIncrement(lastPrice, direction) == .10M;
        }

        private void SetAttemptedEdgeOnOpen(double price)
        {
            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    _ticket.AttemptedEdge = price - _ticket.LastContraFillPx;
                }
                else
                {
                    _ticket.AttemptedEdge = _ticket.LastContraFillPx - price;
                }
            }
            else
            {
                if (_ticket.LastContraFillPx < 0 && price > 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(_ticket.LastContraFillPx) - price;
                }
                else if (price < 0 && _ticket.LastContraFillPx > 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(price) - _ticket.LastContraFillPx;
                }
                else if (price < 0 && _ticket.LastContraFillPx < 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(price + _ticket.LastContraFillPx);
                }
            }

            _ticket.SetOrderDetailTag("Attempted Edge Open", _ticket.AttemptedEdge.ToString());
        }

        private double SetAttemptedEdgeOnClose(double contraPrice)
        {
            if (_ticket.IsSingleLeg)
            {
                if (_ticket.Side == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    _ticket.AttemptedEdge = _ticket.LastFillPx - contraPrice;
                }
                else
                {
                    _ticket.AttemptedEdge = contraPrice - _ticket.LastFillPx;
                }
            }
            else
            {
                if (_ticket.LastFillPx < 0 && contraPrice > 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(_ticket.LastFillPx) - contraPrice;
                }
                else if (contraPrice < 0 && _ticket.LastFillPx > 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(contraPrice) - _ticket.LastFillPx;
                }
                else if (contraPrice < 0 && _ticket.LastFillPx < 0)
                {
                    _ticket.AttemptedEdge = Math.Abs(contraPrice + _ticket.LastFillPx);
                }
            }

            _ticket.SetOrderDetailTag("Attempted Edge Close", _ticket.AttemptedEdge.ToString());
            return _ticket.AttemptedEdge;
        }

        public double GetLastFillPx()
        {
            double lastFillPx = double.NaN;
            if (!_ticket.IsBasketOrder)
            {
                lastFillPx = _ticket.LastFillPx;
            }
            else
            {
                double hedgeRevDelta = _ticket.TotalDelta;
                if (_ticket.IsSingleLegSell)
                {
                    hedgeRevDelta *= -1;
                }

                double change = hedgeRevDelta > 0
                    ? (_ticket.UnderAsk - _ticket.LastFillUnderAskPx) * _ticket.TotalDelta
                    : (_ticket.UnderBid - _ticket.LastFillUnderBidPx) * _ticket.TotalDelta;

                _log.Info($"{nameof(GetLastFillPx)} ^ Adj, Last Fill: {_ticket.LastFillPx}, Hedge Delta: {hedgeRevDelta:F4}, Total Delta: {_ticket.TotalDelta:F4}, Under: {_ticket.UnderBid:F2}X{_ticket.UnderAsk:F2}, Fill Under: {_ticket.LastFillUnderBidPx:F2}X{_ticket.LastFillUnderAskPx:F2}, Change: {change:F2}");

                switch (Settings.LoopPricingMode)
                {
                    case LoopPricingMode.PriceIncrement:
                        lastFillPx = _ticket.LastFillPx;
                        break;
                    case LoopPricingMode.DeltaAdjustedLastFillPrice:
                    case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx: // Allow first order to delta adj in either direction. Msg: WA-Ian 11/11/24
                        lastFillPx = _ticket.LastFillPx + change;
                        break;
                    case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                        lastFillPx = _ticket.IsSingleLegSell
                            ? Math.Max(_ticket.LastFillPx, _ticket.LastFillPx + change)
                            : Math.Min(_ticket.LastFillPx, _ticket.LastFillPx + change);
                        break;
                    case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                        change = _ticket.NetDeltaAdjTheo - _ticket.LastFillAdjTheo;
                        lastFillPx = _ticket.LastFillPx + change;
                        break;
                }
            }

            return lastFillPx;
        }

        private double GetLastContraFillPx()
        {
            double lastContraFillPx = double.NaN;
            if (!_ticket.IsBasketOrder)
            {
                lastContraFillPx = _ticket.LastContraFillPx;
            }
            else
            {
                double contraDelta = _ticket.IsSingleLeg ? _ticket.TotalDelta : -_ticket.TotalDelta;

                double hedgeRevDelta = contraDelta;
                if (!_ticket.IsSingleLegSell)
                {
                    hedgeRevDelta *= -1;
                }

                double change = hedgeRevDelta > 0
                    ? (_ticket.UnderAsk - _ticket.LastFillUnderAskPx) * contraDelta
                    : (_ticket.UnderBid - _ticket.LastFillUnderBidPx) * contraDelta;

                _log.Info($"{nameof(GetLastContraFillPx)} ^ Adj Con, Last Fill: {_ticket.LastContraFillPx}, Hedge Delta: {hedgeRevDelta:F4}, Total Delta: {contraDelta:F4}, Under: {_ticket.UnderBid:F2}X{_ticket.UnderAsk:F2}, Fill Under: {_ticket.LastFillUnderBidPx:F2}X{_ticket.LastFillUnderAskPx:F2}, Change: {change:F2}");

                switch (Settings.LoopPricingMode)
                {
                    case LoopPricingMode.PriceIncrement:
                        lastContraFillPx = _ticket.LastContraFillPx;
                        break;
                    case LoopPricingMode.DeltaAdjustedLastFillPrice:
                        lastContraFillPx = _ticket.LastContraFillPx + change;
                        break;
                    case LoopPricingMode.LimitedDeltaAdjustedLastFillPrice:
                        lastContraFillPx = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? Math.Max(_ticket.LastContraFillPx, _ticket.LastContraFillPx + change)
                            : Math.Min(_ticket.LastContraFillPx, _ticket.LastContraFillPx + change);
                        break;
                    case LoopPricingMode.BadMarketLimitedDeltaAdjLastFillPx:
                        lastContraFillPx = _ticket.IsSingleLeg && _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy
                            ? Math.Min(_ticket.LastContraFillPx, _ticket.LastContraFillPx + change)
                            : Math.Max(_ticket.LastContraFillPx, _ticket.LastContraFillPx + change);
                        break;
                    case LoopPricingMode.AdjustedTheoPeggedLastFillPrice:
                        change = _ticket.IsSingleLeg ? _ticket.NetDeltaAdjTheo - _ticket.LastContraFillAdjTheo : -(_ticket.NetDeltaAdjTheo - _ticket.LastContraFillAdjTheo);
                        lastContraFillPx = _ticket.LastContraFillPx + change;
                        break;
                }
            }

            return lastContraFillPx;
        }

        public double GetLoopMaxLoss()
        {
            if (_ticket.IsBasketOrder)
            {
                if (!_ticket.TryGetDynamicEdge(out _, out _, out double loopMaxLoss, out _))
                {
                    loopMaxLoss = Settings.LoopMaxLoss;
                }

                if (Settings.ScratchOnLowDeltaSize && Math.Abs(_ticket.TotalDelta) < Settings.ScratchOnLowDeltaMax && _ticket.Lcd > Settings.ScratchOnLowDeltaMinSize)
                {
                    loopMaxLoss = Settings.ScratchOnLowDeltaMaxLoss;
                }

                return loopMaxLoss;
            }
            else
            {
                return _ticket.LoopMaxLoss;
            }
        }

        private void SubmitOrderAsync(double price, OrderSubType? type, DateTime receiveTime, string dynamicRoute = null, bool disableRounding = false)
        {
            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(SubmitOrderAsync) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(SubmitOrderAsync) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            int delay = LoopDelay;
            if (OmsCore.Config.LoopDelayMin > 0 || OmsCore.Config.LoopDelayMax > 0)
            {
                int configDelay = OmsCore.Config.LoopDelayMin >= OmsCore.Config.LoopDelayMax
                                ? OmsCore.Config.LoopDelayMin
                                : Random.Shared.Next(OmsCore.Config.LoopDelayMin, OmsCore.Config.LoopDelayMax);
                if (configDelay > 0 && configDelay > delay)
                {
                    delay = configDelay;
                }
            }

            if (delay > 0)
            {
                Task.Delay(delay).ContinueWith(_ =>
                    Task.Run(() => LooperSubmitOrder(price, isContra: false, type, receiveTime, null, dynamicRoute, disableRounding)));
                _log.Info($"{nameof(SubmitOrderAsync)} Id: {_ticket.SpreadId}, Delay b/n loops: {delay}");
            }
            else
            {
                Task.Run(() =>
                    LooperSubmitOrder(price, isContra: false, type, receiveTime, null, dynamicRoute, disableRounding));
            }
        }

        private void SubmitClosingOrderAsync(double price, OrderSubType? type, DateTime receiveTime, double? cancelDelay = null, string dynamicRoute = null, bool disableRounding = false)
        {
            if (_ticket.IsDisposed)
            {
                _log.Info(nameof(SubmitClosingOrderAsync) + " Looper disposed. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (!LoopingEnabled)
            {
                _log.Info(nameof(SubmitClosingOrderAsync) + " Looper disabled. Id: " + _ticket.SpreadId);
                Disable();
                return;
            }

            if (cancelDelay.HasValue && cancelDelay.Value <= 0)
            {
                cancelDelay = null;
            }

            Task.Run(() => LooperSubmitOrder(price, isContra: true, type, receiveTime, cancelDelay, dynamicRoute, disableRounding));
        }

        private void LooperSubmitOrder(double price, bool isContra, OrderSubType? type, DateTime receiveTime, double? cancelDelay = null, string dynamicRoute = null, bool disableRounding = false)
        {
            try
            {
                if (_ticket.IsDisposed)
                {
                    _log.Info(nameof(LooperSubmitOrder) + " Looper disposed. Id: " + _ticket.SpreadId);
                    Disable();
                    return;
                }

                if (!LoopingEnabled)
                {
                    _log.Info(nameof(LooperSubmitOrder) + " Looper disabled. Id: " + _ticket.SpreadId);
                    Disable();
                    return;
                }

                var orderInfo = _ticket.BuildOrder(isContra, type);
                bool floorPrice = !_ticket.IsSingleLeg || (isContra ^ _ticket.Side == ZeroPlus.Models.Data.Enums.Side.Buy);
                orderInfo.Price = !disableRounding && _ticket.PriceNeedsPadding(price) ? _ticket.PadForNickelOrDime(price, floorPrice) : Math.Round(price, 2, MidpointRounding.AwayFromZero);
                cancelDelay ??= LoopCloseInterval;

                if (!_ticket.IsValidCancelDelay(cancelDelay.Value, out double newDelay))
                {
                    cancelDelay = newDelay;
                }

                orderInfo.SetCancelDelay(cancelDelay.Value);

                if (!string.IsNullOrWhiteSpace(dynamicRoute))
                {
                    orderInfo.Route = dynamicRoute;
                }

                if (TryCheckForMarketCrossRoute(_ticket.Low, _ticket.High, orderInfo.Price, isContra, out var sweepRoute))
                {
                    orderInfo.Route = sweepRoute;
                }

                double presubmitLatency = (DateTime.Now - receiveTime).TotalMilliseconds;
                if (!isContra)
                {
                    _ticket.SubmitMainOrder(orderInfo);
                }
                else
                {
                    _ticket.SubmitContraOrder(orderInfo);
                }

                _log.Info($"{nameof(LooperSubmitOrder)} Submitted. Id: {_ticket.SpreadId}, Main: {!isContra}, Type: {type}, Price: {price}, Dynamic Route: {dynamicRoute}, Pre Submit Latency: {presubmitLatency}, Total Latency: {(DateTime.Now - receiveTime).TotalMilliseconds}, Receive Time: {receiveTime:hh:mm:ss.fffff}, Resubmit Count: {ResubmitCounter}.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LooperSubmitOrder));
            }
        }

        private bool TryCheckForMarketCrossRoute(double bid, double ask, double price, bool isContra, out string sweepRoute)
        {
            sweepRoute = OmsCore.Config.DefaultSweepRoute(_ticket.InstanceMode);
            bool sweepRouteEnabled = OmsCore.Config.ForCrossPriceUseRouteEnabled;
            if (sweepRouteEnabled && !string.IsNullOrWhiteSpace(sweepRoute))
            {
                if (_ticket.IsSingleLegSell)
                {
                    if (!isContra)
                    {
                        if (price < bid)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (price > ask)
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    if (_ticket.IsSingleLeg)
                    {
                        if (!isContra)
                        {
                            if (price > ask)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (price < bid)
                            {
                                return true;
                            }
                        }
                    }
                    else
                    {
                        if (!isContra)
                        {
                            if (price > ask)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            if (price > -bid)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            try
            {
                if (_ticket is not null)
                {
                    _ticket.LoopCommandEvent -= OnBasketSettingsLoopCommandEvent;
                    OnBasketSettingsLoopCommandEvent(start: false);
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
            if (_ticket.IsActive)
            {
                if (!_closing)
                {
                    _ticket.CancelMain();
                }
                else
                {
                    _ticket.CancelContra();
                }
            }
            Disable();
        }

        public double GetLoopIncrement(double price, IncrementDirection direction, double marketWidth, bool log = true, bool overrideWithDefaultIncrement = true)
        {
            double defaultIncrement = (double)_ticket.GetPriceIncrement(price, direction);
            double increment = defaultIncrement;

            if (!_ticket.IsBasketOrder)
            {
                increment = _ticket.ContraFishPriceIncrement;
            }
            else
            {
                AutomationConfigModel automationConfig = AutomationConfig;
                switch (automationConfig.LoopIncrementType)
                {
                    case LoopIncrementType.Static:
                        increment = automationConfig.ContraFishPriceIncrement;
                        break;
                    case LoopIncrementType.Dynamic:
                        string message = "Loop dynamic increment. Order: " + _ticket.SpreadId + ", " +
                                         "Attempted Edge: " + _ticket.AttemptedEdge + ", " +
                                         "Static Inc: " + Settings.ContraFishPriceIncrement + ", " +
                                         "Default Inc: " + defaultIncrement + ", " +
                                         "Market Width: " + marketWidth + ", " +
                                         "Override with min tick: " + overrideWithDefaultIncrement + ", ";

                        LoopIncrementConfigModel loopIncrementConfigModel = automationConfig.LoopIncrementConfigModel;
                        if (loopIncrementConfigModel?.DynamicIncrementConfigs == null)
                        {
                            increment = automationConfig.ContraFishPriceIncrement;
                        }
                        else
                        {
                            List<DynamicIncrementConfigModel> incrementConfigs = loopIncrementConfigModel.DynamicIncrementConfigs;
                            var incrementModel =
                                incrementConfigs.FirstOrDefault(x => x.Edge <= _ticket.AttemptedEdge && (!x.MinTickEnabled || Math.Round(x.MinTick, 2) <= Math.Round(defaultIncrement, 2))) ??
                                incrementConfigs.FirstOrDefault(x => x.Default);

                            if (incrementModel == null)
                            {
                                increment = automationConfig.ContraFishPriceIncrement;
                                message += "Selected Inc: none, ";
                            }
                            else
                            {
                                message += "Selected Inc: " + incrementModel.Increment + ", " +
                                           "Selected Min Tick" + incrementModel.MinTick + ", " +
                                           "Selected Edge: " + incrementModel.Edge + ", ";

                                increment = incrementModel.Increment;

                                var nextIndex = incrementConfigs.IndexOf(incrementModel) + 1;
                                if (incrementConfigs.Count > nextIndex)
                                {
                                    var nextModel = incrementConfigs[nextIndex];
                                    if (incrementModel.Edge >= nextModel.Edge)
                                    {
                                        if (_ticket.AttemptedEdge - incrementModel.Increment < nextModel.Edge)
                                        {
                                            increment = Math.Round(_ticket.AttemptedEdge - nextModel.Edge, 2);
                                        }

                                        message += "Next Inc: " + incrementModel.Increment + ", " +
                                                   "Next Edge: " + incrementModel.Edge + ", ";
                                    }
                                }
                            }

                            if (!double.IsNaN(marketWidth))
                            {
                                double max = marketWidth * loopIncrementConfigModel.MaxPercentOfMarketWidth;
                                if (increment > max)
                                {
                                    increment = max;
                                }

                                message += "Max Width Inc: " + max + ", ";
                            }
                        }

                        if (log)
                        {
                            message += "Selected Inc: " + increment;
                            _log.Info(message);
                        }

                        break;
                }
            }

            var finalInc = overrideWithDefaultIncrement ? Math.Max(increment, defaultIncrement) : increment;
            return Math.Round(finalInc, 2);
        }

        private bool TryGetCloseDynamicIntervalAndResubmit(double attemptedEdge, int size, out double interval, out int resubmitCount, out string route, out bool disableRounding)
        {
            interval = LoopCloseInterval;
            resubmitCount = LoopAttemptResubmit;
            route = null;
            disableRounding = false;
            try
            {
                if (_ticket.IsBasketOrder)
                {
                    IAutomationConfig automationConfig = (IAutomationConfig)Settings;
                    if (automationConfig.LoopIntervalType == LoopIntervalType.Dynamic &&
                        automationConfig.DynamicIntervalModel != null &&
                        automationConfig.DynamicIntervalModel.TryGetInterval(_ticket.TotalDelta, attemptedEdge, size, out interval, out resubmitCount, out route, out disableRounding))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetCloseDynamicIntervalAndResubmit));
                return false;
            }
        }

        private void Disable(bool resetSize = true)
        {
            _ticket.IsLooping = false;
            _ticket.IsFreeLooking = false;
            _usingFreeLookIncrement = false;
            SizeUpLocked = false;
            IcebergRunning = false;
            FirstSizeAggressorApplied = false;
            RemoveFromLoopInstances();
            if (resetSize)
            {
                _ticket.ResetLoopSize();
            }
        }

        private bool CheckForLoopInstances(bool isRecon)
        {
            _log.Info(nameof(CheckForLoopInstances) + "Check For Loop Instances. Order: " + _ticket.SpreadId);
            DateTime time = DateTime.Now;
            lock (looperLock)
            {
                if (!isRecon)
                {
                    if (loopInstancesCounter.Count >= OmsCore.Config.MaxSimultaneousLoopsV2)
                    {
                        _ticket.IsLooping = false;
                        IcebergRunning = false;
                        _ticket.ResetLoopSize();
                        _log.Info(nameof(CheckForLoopInstances) + " Max simultaneous loops count reached. Count: " + OmsCore.Config.MaxSimultaneousLoopsV2 + " Order: " + _ticket.SpreadId + ", Took: " + (DateTime.Now - time).TotalMilliseconds);
                        RemoveFromLoopInstances();
                        return false;
                    }
                    else
                    {
                        loopInstancesCounter.Add(_looperId);
                    }
                }
            }
            if (spreadIdToActiveLooperMap.TryGetValue(_ticket.SpreadId, out Looper activeLooper))
            {
                if (activeLooper != this && activeLooper.IsActive)
                {
                    _log.Info(nameof(StartLoop) + " Another looper is active on " + _ticket.SpreadId + ", Took: " + (DateTime.Now - time).TotalMilliseconds);
                    Disable();
                    return false;
                }
            }
            spreadIdToActiveLooperMap[_ticket.SpreadId] = this;

            _log.Info(nameof(CheckForLoopInstances) + "Check For Loop Instances finished. Order: " + _ticket.SpreadId + ", Took: " + (DateTime.Now - time).TotalMilliseconds);
            return true;
        }

        public void RemoveFromLoopInstances()
        {
            _log.Info($"Removing Looper instance. Order: {_ticket.SpreadId}, ID: {_looperId}");
            lock (looperLock)
            {
                loopInstancesCounter.Remove(_looperId);
            }
            _log.Info($"Looper instance removed. Order: {_ticket.SpreadId}, ID: {_looperId}");
        }

        private void OnBasketSettingsLoopCommandEvent(bool start)
        {
            if (!start)
            {
                Stop();
            }
        }
    }
}