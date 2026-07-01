using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Automation
{
    public class ThreeWayCloser : OrderUpdateHandler
    {
        private const int ReplaceInterval = 250;
        private const int ReplaceIntervalSpread = 1000;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly object _lock = new();
        private readonly OmsCore _omsCore;
        private readonly IAbstractFactory<ComplexOrderTicketViewModel> _ticketFactory;
        private OptionType _side;
        private bool _initialized;
        private bool _inverseStarted;
        private OrderTicket _ticket;
        private ComplexOrderTicketViewModel _permTicket;
        private ComplexOrderTicketViewModel _verticalTicket;
        private string _permTicketId;
        private string _verticalTicketId;
        private double _startingPrice = double.NaN;
        private double _verticalStartingPrice = double.NaN;
        private PermSide _lastSide;
        private PermMode _lastPerm;

        public override OrderSubType? SubType { get; set; } = OrderSubType.ThreeWayCloser;

        public bool IsRunning { get; set; }
        public bool IsDisposed { get; set; }

        public ThreeWayCloser(OmsCore omsCore, IAbstractFactory<ComplexOrderTicketViewModel> ticketFactory)
        {
            _omsCore = omsCore;
            _ticketFactory = ticketFactory;
        }

        public void Initialize(OrderTicket orderTicketBase)
        {
            if (!_initialized)
            {
                _initialized = true;
                _permTicket = _ticketFactory.Create();
                _verticalTicket = _ticketFactory.Create();
                _ticket = orderTicketBase;
            }
        }

        public async void Close()
        {
            if (_ticket.IsActive)
            {
                _log.Warn(nameof(Close) + " Automation already running. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Automation already running.", "Three way close");
                return;
            }

            if (IsRunning)
            {
                _log.Warn(nameof(Close) + " Closer already running. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Closer already running.", "Three way close");
                return;
            }

            if (!_ticket.IsSingleLeg)
            {
                _log.Warn(nameof(Close) + " Threeway close not supported for complex orders. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Three way close not supported for spreads.", "Three way close");
                return;
            }

            _startingPrice = double.NaN;
            _verticalStartingPrice = double.NaN;

            _inverseStarted = false;
            _verticalTicket.SetDispatcher(_ticket.Dispatcher);

            TicketLegModel ticketLegModel = _ticket.Legs.FirstOrDefault();
            if (ticketLegModel == null)
            {
                _log.Warn(nameof(Close) + " Threeway close invalid leg. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Invalid leg found.", "Three way close");
                return;
            }

            string type = ticketLegModel.Type;
            if (string.IsNullOrWhiteSpace(type))
            {
                _log.Warn(nameof(Close) + " Threeway close invalid leg type. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Invalid leg type found.", "Three way close");
                return;
            }

            _side = type == "CALL" ? OptionType.CALL : OptionType.PUT;

            await StartMainPermCloser();
        }

        public void Stop()
        {
            IsRunning = false;
        }

        internal void Dispose()
        {
            IsRunning = false;
            _ticket = null;
            IsDisposed = true;
        }

        private async Task StartMainPermCloser()
        {
            _startingPrice = double.NaN;
            _verticalStartingPrice = double.NaN;

            _permTicket.SetDispatcher(_ticket.Dispatcher);
            await _permTicket.LoadFromTicketAsync(_ticket);
            if (_permTicket.Side != _ticket.Side)
            {
                _permTicket.Reverse();
            }
            switch (_side)
            {
                case OptionType.PUT:
                    if (await _permTicket.StrikeDownAsync(PermSide.High))
                    {
                        _lastSide = PermSide.High;
                        _lastPerm = PermMode.StrikeDown;
                        StartPermCloser();
                    }
                    else if (await _permTicket.StrikeUpAsync(PermSide.High))
                    {
                        _lastSide = PermSide.High;
                        _lastPerm = PermMode.StrikeUp;
                        StartPermCloser();
                    }
                    else
                    {
                        _log.Warn(nameof(Close) + " Next perm not found. Id: " + _ticket.SpreadId);
                        _ticket.ShowMessage("Next perm not found.", "Three way close");
                    }
                    break;
                case OptionType.CALL:
                    if (await _permTicket.StrikeUpAsync(PermSide.Low))
                    {
                        _lastSide = PermSide.Low;
                        _lastPerm = PermMode.StrikeUp;
                        StartPermCloser();
                    }
                    else if (await _permTicket.StrikeDownAsync(PermSide.Low))
                    {
                        _lastSide = PermSide.Low;
                        _lastPerm = PermMode.StrikeDown;
                        StartPermCloser();
                    }
                    else
                    {
                        _log.Warn(nameof(Close) + " Next perm not found. Id: " + _ticket.SpreadId);
                        _ticket.ShowMessage("Next perm not found.", "Three way close");
                    }
                    break;
            }
        }

        private async Task StartInversePermSide()
        {
            if (_inverseStarted)
            {
                Stop();
                return;
            }

            _inverseStarted = true;

            _startingPrice = double.NaN;
            _verticalStartingPrice = double.NaN;

            _permTicket.SetDispatcher(_ticket.Dispatcher);
            await _permTicket.LoadFromTicketAsync(_ticket);
            if (_permTicket.Side != _ticket.Side)
            {
                _permTicket.Reverse();
            }

            _lastSide = _lastSide == PermSide.Low ? PermSide.High : PermSide.Low;
            _lastPerm = _lastPerm == PermMode.StrikeUp ? PermMode.StrikeDown : PermMode.StrikeUp;

            switch (_lastPerm)
            {
                case PermMode.StrikeUp:
                    if (await _permTicket.StrikeUpAsync(_lastSide))
                    {
                        StartPermCloser();
                    }
                    else
                    {
                        _log.Warn(nameof(Close) + " Next perm not found. Id: " + _ticket.SpreadId);
                        _ticket.ShowMessage("Next perm not found.", "Three way close");
                    }
                    break;
                case PermMode.StrikeDown:
                    if (await _permTicket.StrikeDownAsync(_lastSide))
                    {
                        StartPermCloser();
                    }
                    else
                    {
                        _log.Warn(nameof(Close) + " Next perm not found. Id: " + _ticket.SpreadId);
                        _ticket.ShowMessage("Next perm not found.", "Three way close");
                    }
                    break;
            }
        }

        private async void StartPermCloser()
        {
            await _permTicket.CalculatePermAdjPxUsingMatchingHwAsync(_ticket);

            IsRunning = true;

            TicketLegModel ticketLegModel = _ticket.Legs.FirstOrDefault();
            bool isContra = (ticketLegModel.NetQty > 0 && _ticket.Side == Side.Buy) ||
                           (ticketLegModel.NetQty < 0 && _ticket.Side == Side.Sell);
            var orderInfo = _permTicket.BuildOrder(isContra, OrderSubType.ThreeWayCloser);
            _permTicketId = _omsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _permTicketId;
            double cancelDelay = ReplaceInterval;

            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            orderInfo.SetCancelDelay(cancelDelay);
            if (await _permTicket.WaitForMarkLoad())
            {
                if (_permTicket.PermAdjPxLoaded)
                {
                    _log.Warn(nameof(StartPermCloser) + " Perm Adj Px loaded. Id: " + _ticket.SpreadId);
                    _permTicket.PermAdjPxAsync();
                    _startingPrice = isContra ? _permTicket.PermAdjContraPx : _permTicket.PermAdjPx;
                }
                else
                {
                    _log.Warn(nameof(StartPermCloser) + " Perm Adj Px not loaded. Id: " + _ticket.SpreadId);
                    _startingPrice = isContra ? _permTicket.High : _permTicket.Low;
                }

                orderInfo.Price = _ticket.PriceNeedsPadding(_startingPrice) ? _ticket.PadForNickelOrDime(_startingPrice, isContra) : Math.Round(_startingPrice, 2, MidpointRounding.AwayFromZero);
                await _omsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
            }
            else
            {
                _log.Warn(nameof(StartPermCloser) + " Data load Failed. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Wait for data timeout.", "Three way close");
                return;
            }
        }

        private async void StartVerticalCloser()
        {
            if (!IsRunning)
            {
                _log.Warn(nameof(StartVerticalCloser) + " Closer not running. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Closer not running.", "Three way close");
                return;
            }

            TicketLegModel ticketLegModel = _ticket.Legs.FirstOrDefault();
            TicketLegModel ticketLegClone = new(_omsCore, ticketLegModel.Underlying, ticketLegModel.Account, ticketLegModel.ParentBasket, ticketLegModel.PortfolioManager);
            await ticketLegClone.LoadFromTemplateAsync(ticketLegModel);
            TicketLegModel permLegModel = _permTicket.Legs.FirstOrDefault();
            TicketLegModel permLegClone = new(_omsCore, permLegModel.Underlying, permLegModel.Account, permLegModel.ParentBasket, permLegModel.PortfolioManager);
            await permLegClone.LoadFromTemplateAsync(permLegModel);
            permLegClone.Reverse();
            List<TicketLegModel> legs = new()
            {
                ticketLegClone,
                permLegClone
            };
            await _verticalTicket.LoadFromLegsAsync(legs);
            bool isContra = (ticketLegModel.NetQty > 0 && _ticket.Side == Side.Buy) ||
                           (ticketLegModel.NetQty < 0 && _ticket.Side == Side.Sell);
            var orderInfo = _verticalTicket.BuildOrder(isContra, OrderSubType.ThreeWayCloser);
            _verticalTicketId = _omsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _verticalTicketId;
            double cancelDelay = ReplaceIntervalSpread;

            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            orderInfo.SetCancelDelay(cancelDelay);
            if (await _verticalTicket.WaitForMarkLoad())
            {
                _verticalStartingPrice = isContra ? -_verticalTicket.Low : _verticalTicket.High;
                orderInfo.Price = _ticket.PriceNeedsPadding(_verticalStartingPrice) ? _ticket.PadForNickelOrDime(_verticalStartingPrice, isContra) : Math.Round(_verticalStartingPrice, 2, MidpointRounding.AwayFromZero);
                await _omsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
            }
            else
            {
                _log.Warn(nameof(StartPermCloser) + " Data load Failed. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Wait for data timeout.", "Three way close");
                return;
            }
        }

        private async void ContinuePermCloser()
        {
            if (!IsRunning)
            {
                _log.Warn(nameof(ContinuePermCloser) + " Closer not running. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Closer not running.", "Three way close");
                return;
            }

            TicketLegModel ticketLegModel = _ticket.Legs.FirstOrDefault();
            bool isContra = (ticketLegModel.NetQty > 0 && _ticket.Side == Side.Buy) ||
                           (ticketLegModel.NetQty < 0 && _ticket.Side == Side.Sell);
            var orderInfo = _permTicket.BuildOrder(isContra, OrderSubType.ThreeWayCloser);
            _permTicketId = _omsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _permTicketId;
            double cancelDelay = ReplaceInterval;

            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            orderInfo.SetCancelDelay(cancelDelay);
            if (await _permTicket.WaitForMarkLoad())
            {
                if (_permTicket.PermAdjPxLoaded)
                {
                    _log.Warn(nameof(StartPermCloser) + " Perm Adj Px loaded. Id: " + _ticket.SpreadId);
                    switch (_permTicket.Side)
                    {
                        case Side.Sell when !isContra:
                            _permTicket.PermAdjPxBase -= (double)_permTicket.GetPriceIncrement(_permTicket.PermAdjPxBase, IncrementDirection.Down);
                            break;
                        case Side.Buy when !isContra:
                            _permTicket.PermAdjPxBase += (double)_permTicket.GetPriceIncrement(_permTicket.PermAdjPxBase, IncrementDirection.Up);
                            break;
                        case Side.Buy when isContra:
                            _permTicket.PermAdjContraPxBase -= (double)_permTicket.GetPriceIncrement(_permTicket.PermAdjContraPxBase, IncrementDirection.Down);
                            break;
                        case Side.Sell when isContra:
                            _permTicket.PermAdjContraPxBase += (double)_permTicket.GetPriceIncrement(_permTicket.PermAdjContraPxBase, IncrementDirection.Up);
                            break;
                    }
                    _permTicket.PermAdjPxAsync();
                    _startingPrice = isContra ? _permTicket.PermAdjContraPx : _permTicket.PermAdjPx;
                }
                else
                {
                    switch (_permTicket.Side)
                    {
                        case Side.Buy when isContra:
                        case Side.Sell when !isContra:
                            _startingPrice -= (double)_permTicket.GetPriceIncrement(_startingPrice, IncrementDirection.Down);
                            break;
                        case Side.Buy when !isContra:
                        case Side.Sell when isContra:
                            _startingPrice += (double)_permTicket.GetPriceIncrement(_startingPrice, IncrementDirection.Up);
                            break;
                    }
                }

                if ((isContra && _startingPrice < _permTicket.Mid) ||
                    (!isContra && _startingPrice > _permTicket.Mid))
                {
                    await StartInversePermSide();
                    return;
                }
                else
                {
                    orderInfo.Price = _ticket.PriceNeedsPadding(_startingPrice) ? _ticket.PadForNickelOrDime(_startingPrice, isContra) : Math.Round(_startingPrice, 2, MidpointRounding.AwayFromZero);
                    await _omsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
                }
            }
            else
            {
                _log.Warn(nameof(ContinuePermCloser) + " Data load Failed. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Wait for data timeout.", "Three way close");
                return;
            }
        }

        private async void ContinueVerticalCloser()
        {
            if (!IsRunning)
            {
                _log.Warn(nameof(ContinueVerticalCloser) + " Closer not running. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Closer not running.", "Three way close");
                return;
            }

            TicketLegModel ticketLegModel = _ticket.Legs.FirstOrDefault();
            bool isContra = (ticketLegModel.NetQty > 0 && _ticket.Side == Side.Buy) ||
                           (ticketLegModel.NetQty < 0 && _ticket.Side == Side.Sell);
            var orderInfo = _verticalTicket.BuildOrder(isContra, OrderSubType.ThreeWayCloser);
            _verticalTicketId = _omsCore.OrderClient.GetNextOrderId();
            orderInfo.LocalID = _verticalTicketId;
            double cancelDelay = ReplaceIntervalSpread;

            if (!_ticket.IsValidCancelDelay(cancelDelay, out double newDelay))
            {
                cancelDelay = newDelay;
            }

            orderInfo.SetCancelDelay(cancelDelay);
            if (await _verticalTicket.WaitForMarkLoad())
            {
                _verticalStartingPrice += (double)_verticalTicket.GetPriceIncrement(_verticalStartingPrice, IncrementDirection.Up);

                if ((isContra && _verticalStartingPrice > -_verticalTicket.Mid) ||
                    (!isContra && _verticalStartingPrice > _verticalTicket.Mid))
                {
                    Stop();
                    CreateComplexOrderTicket(_verticalTicket);
                    return;
                }
                else
                {
                    orderInfo.Price = _ticket.PriceNeedsPadding(_verticalStartingPrice) ? _ticket.PadForNickelOrDime(_verticalStartingPrice, isContra) : Math.Round(_verticalStartingPrice, 2, MidpointRounding.AwayFromZero);
                    await _omsCore.OrderClient.SendOrderAsync(orderInfo, _ticket.GetInstanceMode(), this, false, _ticket.Multiplier, false);
                }
            }
            else
            {
                _log.Warn(nameof(ContinuePermCloser) + " Data load Failed. Id: " + _ticket.SpreadId);
                _ticket.ShowMessage("Wait for data timeout.", "Three way close");
                return;
            }
        }

        public override void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            try
            {
                OrderStatus? orderStatus = execReport.OrderStatus;
                ExecutionType? executionType = execReport.ExecutionType;

                if (!IsRunning || IsDisposed)
                {
                    return;
                }

                if (execReport.ClientOrderId == _permTicketId)
                {
                    HandlePermExecutionReport(execReport, orderStatus, executionType);
                }
                else if (execReport.ClientOrderId == _verticalTicketId)
                {
                    HandleVerticalExecutionReport(execReport, orderStatus, executionType);
                }
            }
            catch (Exception) { }
        }

        private void HandlePermExecutionReport(OrderUpdateModel execReport, OrderStatus? orderStatus, ExecutionType? executionType)
        {
            if (executionType != null && executionType.Value.IsFilled())
            {
                _permTicket.Lcd = execReport.LeavesQty;
            }

            switch (orderStatus)
            {
                case OrderStatus.Canceled:
                    ContinuePermCloser();
                    break;
                case OrderStatus.Filled:
                    StartVerticalCloser();
                    break;
            }

            OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

            _ticket.ContraOrderStatus = orderStatus;
            _ticket.ContraStatus = "Perm " + orderUpdateValues.Status;
            _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
        }

        private void HandleVerticalExecutionReport(OrderUpdateModel execReport, OrderStatus? orderStatus, ExecutionType? executionType)
        {
            if (executionType != null && executionType.Value.IsFilled())
            {
                _verticalTicket.Lcd = execReport.LeavesQty;
            }

            switch (orderStatus)
            {
                case OrderStatus.Canceled:
                    ContinueVerticalCloser();
                    break;
            }

            OrderUpdateValues orderUpdateValues = _ticket.ParseOrderUpdate(execReport);

            _ticket.ContraOrderStatus = orderStatus;
            _ticket.ContraStatus = "Vertical " + orderUpdateValues.Status;
            _ticket.ContraStatusMode = orderUpdateValues.StatusMode;
        }

        internal void CreateComplexOrderTicket(OrderTicket orderModel)
        {
            if (_omsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                        new DispatcherSynchronizationContext(
                            Dispatcher.CurrentDispatcher));

                    Window window = null;
                    if (orderModel.Legs.Count <= 1 && OmsCore.Config.UseOrderTicketForSingleLegOrders)
                    {
                        window = new OrderTicketView();
                    }
                    else
                    {
                        switch (OmsCore.Config.DefaultOrderTicketStyle)
                        {
                            case OrderTicketStyle.Complex:
                                window = new ComplexOrderTicketView()
                                {
                                    Manual = false,
                                };
                                break;
                            case OrderTicketStyle.Combined:
                                window = new CombinedOrderTicketView()
                                {
                                    Manual = false,
                                };
                                break;
                        }
                    }

                    ComplexOrderTicketViewModel viewModel = (ComplexOrderTicketViewModel)window.DataContext;
                    viewModel.InstanceMode = orderModel.InstanceMode;
                    viewModel.BrokerOverride = orderModel.BrokerOverride;
                    viewModel.SetDispatcher(window.Dispatcher);

                    window.Dispatcher.UnhandledException += (s, e) =>
                    {
                        _log.Error(e.Exception, "DispatcherUnhandledException");
                        e.Handled = true;
                    };
                    string spreadId = orderModel.SpreadId;
                    window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                    window.Loaded += (s, e) => _ = viewModel.LoadFromTicketAsync(orderModel);

                    window.Show();

                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }
    }
}
