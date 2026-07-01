using DevExpress.Mvvm;
using NLog;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.LowLatency.Ext;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Notifications;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.Helpers;

namespace ZeroPlus.Oms.Ui.LowLatency
{
    public partial class LowLatencyTransactionsProcessor : BindableBase
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        private readonly OmsCore _omsCore;

        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly BlockingCollection<MsgPackerResponses.jsonResponse> _jsonResponseQueue;
        private readonly Dictionary<string, LowLatencyOrderModel> _orderIdToOrderModelMap;
        private readonly ConcurrentDictionary<string, LowLatencyInstanceModel> _instanceIdToModelMap;
        private readonly ConcurrentDictionary<ILowLatencyInstance, List<LowLatencyOrderModel>> _instanceIdToHangedOrdersMap;

        public static readonly Regex SigEdgeToTheoRegex = new Regex(@".* sigEdgeToTheo:\[(.*?)\] sigPctBid:\[(.*?)\]", RegexOptions.Compiled);

        public Dispatcher Dispatcher { get; }
        public bool PlayScratch { get; set; }
        public bool PlayProfit { get; set; }
        public bool PlayLoss { get; set; }
        public string ScratchSound { get; set; } = "scratch";
        public string ProfitSound { get; set; } = "profit";
        public string LossSound { get; set; } = "loss";
        public LowLatencyInstanceModel AllModel { get; set; }
        public FastObservableCollection<LowLatencyInstanceModel> LatencyInstanceModels { get; } = new();

        [Bindable]
        public partial bool AudioMuted { get; set; }
        [Bindable]
        public partial bool DisableOpenTicket { get; set; }

        public LowLatencyTransactionsProcessor(DispatcherStore dispatcherStore, OmsCore omsCore)
        {
            _omsCore = omsCore;
            _jsonResponseQueue = new BlockingCollection<MsgPackerResponses.jsonResponse>();
            _cancellationTokenSource = new CancellationTokenSource();
            _orderIdToOrderModelMap = new Dictionary<string, LowLatencyOrderModel>();
            _instanceIdToModelMap = new ConcurrentDictionary<string, LowLatencyInstanceModel>();
            _instanceIdToHangedOrdersMap = new ConcurrentDictionary<ILowLatencyInstance, List<LowLatencyOrderModel>>();
            Dispatcher = dispatcherStore.GetDispatcherForModule(Module.OrderBook);

            CreateBaseInstance();

            Task.Factory.StartNew(RunProcessor, TaskCreationOptions.LongRunning);
        }

        private void CreateBaseInstance()
        {
            AllModel = new LowLatencyInstanceModel()
            {
                Title = "All",
            };
            LatencyInstanceModels.Add(AllModel);
        }

        private LowLatencyInstanceModel GetInstanceModel(string instanceId)
        {
            if (!_instanceIdToModelMap.TryGetValue(instanceId, out var model))
            {
                model = new LowLatencyInstanceModel()
                {
                    Title = instanceId,
                };
                _instanceIdToModelMap[instanceId] = model;
                Dispatcher.BeginInvoke(() => LatencyInstanceModels.Add(model));
            }
            return model;
        }

        public void Add(MsgPackerResponses.jsonResponse resp)
        {
            _jsonResponseQueue.Add(resp);
        }

        public void ClearTransactions()
        {
            try
            {
                Dispatcher?.BeginInvoke(() =>
                {
                    foreach (var instance in LatencyInstanceModels)
                    {
                        instance.Orders.Clear();
                        instance.WorkingOrders.Clear();
                    }
                    LatencyInstanceModels.Clear();
                    LatencyInstanceModels.Add(AllModel);
                });
                _instanceIdToModelMap.Clear();
                _orderIdToOrderModelMap.Clear();
            }
            catch { /* ignored */ }
        }

        private async Task RunProcessor()
        {
            var token = _cancellationTokenSource.Token;
            int buffer = 0;
            while (!token.IsCancellationRequested)
            {
                var found = _jsonResponseQueue.TryTake(out var resp, 1000, token);
                if (found)
                {
                    buffer = ProcessUpdate(resp, buffer);
                }

                if (buffer > 100 || (!found && buffer > 0))
                {
                    await Dispatcher.BeginInvoke(() =>
                    {
                        foreach (LowLatencyInstanceModel instance in LatencyInstanceModels)
                        {
                            if (instance.Buffer.Any())
                            {
                                instance.Orders.AddRange(instance.Buffer);
                                instance.Buffer.Clear();
                            }
                        }

                        buffer = 0;
                    });

                    if (!AudioMuted)
                    {
                        if (PlayScratch)
                        {
                            PlayScratch = false;
                            SoundManager.Play(ScratchSound);
                        }
                        if (PlayProfit)
                        {
                            PlayProfit = false;
                            SoundManager.Play(ProfitSound);
                        }
                        if (PlayLoss)
                        {
                            PlayLoss = false;
                            SoundManager.Play(LossSound);
                        }
                    }
                }
            }
        }

        private int ProcessUpdate(MsgPackerResponses.jsonResponse resp, int buffer)
        {
            try
            {
                var respOrder = resp.Order;
                if (respOrder != null)
                {
                    var respNbbo = resp.Nbbo;
                    if (!_orderIdToOrderModelMap.TryGetValue(respOrder.ClOrdId, out var model))
                    {
                        string who = "";
                        string what = "";
                        Decode(respOrder.ClOrdId, respOrder.Action, ref who, ref what);
                        model = new LowLatencyOrderModel
                        {
                            Side = what.Contains(Side.Buy.ToString()) ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                            Symbol = respOrder.Symbol,
                            Underlying = GetUnderlying(respOrder.Symbol),
                            UserName = respOrder.UserName,
                            StratIdInResponseTo = respOrder.StratIdInResponseTo,
                            SignalInstance = respOrder.SignalInstance,
                            What = what.FromCamelCase(),
                            Who = who,
                            UserId = respOrder.UserId,
                            StratId = respOrder.StratId,
                            StratName = respOrder.StratName,
                            StratType = respOrder.StratType,
                        };
                        _orderIdToOrderModelMap[respOrder.ClOrdId] = model;
                        var instanceModel = GetInstanceModel(respOrder.UserName);
                        instanceModel.Buffer.Add(model);
                        AllModel.Buffer.Add(model);
                        buffer++;
                    }

                    if (respNbbo != null && respNbbo.Count > 0)
                    {
                        string nbbo = respNbbo.Where(x => x.Symbol == respOrder.Symbol)
                            .Select(x => $"{x.Bid} x {x.Ask}  ({XConverter.asDecimal(x.Ask) - XConverter.asDecimal(x.Bid)})   [{x.NbboT}]")
                            .FirstOrDefault() ?? "";
                        model.Nbbo = nbbo;
                    }

                    if (!string.IsNullOrWhiteSpace(resp.Error))
                    {

                        model.LastUpdateTime = resp.Timestamp;
                        model.Symbol = " - error -";
                        model.Who = " - error -";
                        model.What = " - error -";
                        model.Error = resp.Error;
                    }

                    if (respOrder != null)
                    {
                        var action = $"{respOrder.Action}";

                        if (string.IsNullOrWhiteSpace(respOrder.Symbol))
                        {
                            if (!string.IsNullOrWhiteSpace(respOrder.Error))
                            {

                                model.LastUpdateTime = resp.Timestamp;
                                model.Symbol = " - error -";
                                model.Who = " - error -";
                                model.What = " - error -";
                                model.Error = resp.Error;

                            }
                        }
                        else if (!action.Contains("Complete") ||
                                 !string.IsNullOrWhiteSpace(respOrder.Error))
                        {
                            string who = "?", what = "?";

                            Decode(respOrder.ClOrdId, respOrder.Action, ref who, ref what);

                            string nbbo = "";
                            if (respNbbo != null && respNbbo.Count > 0)
                            {
                                nbbo = respNbbo.Where(x => x.Symbol == respOrder.Symbol)
                                    .Select(x => $"{x.Bid} x {x.Ask}  ({XConverter.asDecimal(x.Ask) - XConverter.asDecimal(x.Bid)})   [{x.NbboT}]")
                                    .FirstOrDefault() ?? "";
                                model.Nbbo = nbbo;
                            }


                            model.LastUpdateTime = resp.Timestamp;
                            model.Symbol = respOrder.Symbol;

                            model.Dte = ExpiredOptionHelper.DaysToExpiration(respOrder.Symbol);

                            if (string.IsNullOrWhiteSpace(respOrder.StratId))
                            {
                                model.Who = respOrder.StratName;
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(respOrder.StratIdInResponseTo))
                                {
                                    model.Who = $"{respOrder.StratName}:{respOrder.StratId}";
                                }
                                else
                                {
                                    model.Who = $"{respOrder.StratName}:{respOrder.StratId}::{respOrder.StratIdInResponseTo}";
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(respOrder.SignalInstance))
                            {
                                model.Who = $"[{respOrder.SignalInstance}] [{model.Who}]";
                            }

                            model.What = what?.FromCamelCase();
                            if (action.Contains("Complet") &&
                                respOrder.RemOrderQty != "0" &&
                                respOrder.StratType == "L")
                            {
                                model.Status = "H U N G";
                                OpenInComplexTicket(model, resp.LowLatencyInstance);
                                lock (resp.LowLatencyInstance)
                                {
                                    if (!_instanceIdToHangedOrdersMap.TryGetValue(resp.LowLatencyInstance,
                                            out var list))
                                    {
                                        list = new List<LowLatencyOrderModel>();
                                        _instanceIdToHangedOrdersMap[resp.LowLatencyInstance] = list;
                                    }
                                    list.Add(model);
                                }
                            }
                            else
                            {
                                model.Status = action;
                            }

                            model.Side = what.Contains("Buy") ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell;

                            int.TryParse(respOrder.FillQty, out var filled);
                            int.TryParse(respOrder.RemOrderQty, out var leaves);

                            var orderStatus = OrderStatus.New;

                            if (filled > 0)
                            {
                                if (leaves == 0)
                                {
                                    orderStatus = OrderStatus.Filled;
                                }
                                else
                                {
                                    orderStatus = OrderStatus.PartiallyFilled;
                                }
                            }
                            else if (action == "Cancelled")
                            {
                                orderStatus = OrderStatus.Canceled;
                            }

                            model.OrderUpdateModel = new OmsOrderUpdateModel
                            {
                                Side = model.Side,
                                Filled = filled,
                                OrderStatus = orderStatus,
                            };

                            if (double.TryParse(respOrder.OrderPrice, out var orderPrice))
                            {
                                model.OrderPrice = orderPrice;
                            }

                            if (string.IsNullOrWhiteSpace(respOrder.DiffMillis))
                            {
                                model.FillPrice = respOrder.FillPrice;
                            }
                            else
                            {
                                if (!string.IsNullOrWhiteSpace(respOrder.FillPrice))
                                {
                                    model.FillPrice = $"{respOrder.FillPrice}    {respOrder.DiffMillis} ms";

                                }
                                else
                                {
                                    model.FillPrice = $"{respOrder.DiffMillis} ms";
                                }
                            }

                            if (int.TryParse(respOrder.FillQty, out var filledQty))
                            {
                                model.FillQty = filledQty;
                            }

                            model.Nbbo = nbbo;
                            model.ExecutedExchange = respOrder.ExecutedExchange;
                            if (!string.IsNullOrWhiteSpace(respOrder.Error))
                            {
                                model.Error = respOrder.Error;
                            }
                            else
                            {
                                model.Error = "";
                            }

                            if (int.TryParse(respOrder.RemOrderQty, out var remOrderQty))
                            {
                                model.RemOrderQty = remOrderQty;
                            }

                            model.Qty = model.FillQty + model.RemOrderQty;

                            model.ClOrdId = respOrder.ClOrdId;

                            if (!string.IsNullOrEmpty(respOrder.ResponseToPrice))
                            {
                                if (decimal.TryParse(respOrder.FillPrice, out var fillPrice) &&
                                    decimal.TryParse(respOrder.ResponseToPrice, out var origPrice) &&
                                    int.TryParse(respOrder.FillQty, out var fillQty) &&
                                    fillPrice != 0 && origPrice != 0 && fillQty != 0)
                                {
                                    //                           1.60     - 1.58 = +0.02, so profit
                                    decimal cost = fillQty * (fillPrice - origPrice);
                                    if (model.Side == ZeroPlus.Models.Data.Enums.Side.Sell) // if we bought first, and now sold
                                    {
                                        if (cost < 0)
                                        {
                                            PlayLoss = !AudioMuted;
                                            model.Error = $"-{cost:N2}   -${-cost * 100:N2}";
                                            model.Pnl = Convert.ToDouble(-cost * 100);
                                        }
                                        else if (cost > 0)
                                        {
                                            PlayProfit = !AudioMuted;
                                            model.Error = $"+{cost:N2}   +${cost * 100:N2}";
                                            model.Pnl = Convert.ToDouble(cost * 100);
                                        }
                                        else
                                        {
                                            PlayScratch = !AudioMuted;
                                            model.Error = "scratch";
                                            model.Pnl = 0.00;
                                        }
                                    }
                                    else if (model.Side == ZeroPlus.Models.Data.Enums.Side.Buy) // if we sold first, and now bought
                                    {
                                        if (cost < 0)
                                        {
                                            PlayProfit = !AudioMuted;
                                            model.Error = $"+{cost:N2}   +${-cost * 100:N2}";
                                            model.Pnl = Convert.ToDouble(-cost * 100);
                                        }
                                        else if (cost > 0)
                                        {
                                            PlayLoss = !AudioMuted;
                                            model.Error = $"-{cost:N2}   -${cost * 100:N2}";
                                            model.Pnl = Convert.ToDouble(cost * 100);
                                        }
                                        else
                                        {
                                            PlayScratch = !AudioMuted;
                                            model.Error = "scratch";
                                            model.Pnl = 0.00;
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(respOrder.OrderExtra))
                            {
                                model.OrderExtra = respOrder.OrderExtra;

                                Match match = SigEdgeToTheoRegex.Match(respOrder.OrderExtra);
                                if (match.Success)
                                {
                                    var y = match.Groups[1].Value.Trim();
                                    double edgeToTheo = double.NaN;
                                    if (y == "-214748.3648")
                                    {
                                        edgeToTheo = double.NaN;
                                    }
                                    else
                                    {
                                        if (double.TryParse(y, out var ett))
                                        {
                                            edgeToTheo = ett;
                                        }
                                    }

                                    model.EdgeToTheo = edgeToTheo;

                                    if (double.TryParse(match.Groups[2].Value.Trim(), out var bidPercent))
                                    {
                                        model.PctBid = bidPercent;
                                    }
                                    else
                                    {
                                        model.PctBid = double.NaN;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessUpdate));
            }

            return buffer;
        }

        public void OpenHangsForInstance(ILowLatencyInstance model)
        {
            List<LowLatencyOrderModel> copy = null;

            lock (model)
            {
                if (_instanceIdToHangedOrdersMap.TryGetValue(model, out var list))
                {
                    copy = list.ToList();
                }
            }

            if (copy != null)
            {
                foreach (var orderModel in copy)
                {
                    OpenInComplexTicket(orderModel, model);
                }
            }
        }

        public void OpenInComplexTicket(LowLatencyOrderModel orderModel, ILowLatencyInstance instance)
        {
            if (DisableOpenTicket)
            {
                return;
            }

            if (_omsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                Thread newWindowThread = new(() =>
                {
                    SynchronizationContext.SetSynchronizationContext(
                    new DispatcherSynchronizationContext(
                    Dispatcher.CurrentDispatcher));

                    (Window view, ComplexOrderTicketViewModel viewModel) = CreateWindow(true);

                    view.Loaded += async (_, _) => await LoadTicketFromModel(viewModel, orderModel, instance);

                    view.Show();
                    Dispatcher.Run();
                });
                newWindowThread.SetApartmentState(ApartmentState.STA);
                newWindowThread.Start();
            }
        }

        private async Task LoadTicketFromModel(ComplexOrderTicketViewModel viewModel, LowLatencyOrderModel orderModel, ILowLatencyInstance instance)
        {
            var instrument = new Instrument();
            if (instrument.FromMDTron(orderModel.Symbol))
            {
                string symbol = instrument.ToTOS();
                await viewModel.LoadLegsFromTosAsync(symbol, orderModel.Side, true);

                string account = OmsCore.Config.LowLatencyAccounts.FirstOrDefault();
                if (!viewModel.AccountsList.Contains(account))
                {
                    await viewModel.Dispatcher.BeginInvoke(() => viewModel.AccountsList.Add(account));
                }

                viewModel.InstanceMode = InstanceMode.AT_ZPFIX;
                viewModel.Venue = Venue.ZpFix;

                viewModel.Account = account;
                viewModel.AccountLocked = false;

                string route = OmsCore.Config.LowLatencyHungRoute;
                viewModel.Route = route;
                viewModel.ContraRoute = route;

                foreach (TicketLegModel leg in viewModel.Legs)
                {
                    leg.Position = Positions.CLOSE.ToString();
                }

                viewModel.IsLowLatencyHangManager = true;
                viewModel.Username = orderModel.UserName;

                double leaves = orderModel.RemOrderQty;

                viewModel.TradeEvent += (_, order) =>
                {
                    if (leaves > 0)
                    {
                        leaves -= order.CumulativeQuantity;
                        string message = "Manual Fill! Fill adjustment request sent!\n\n" +
                                         $"{orderModel.Side} [{orderModel.Symbol}]\n" +
                                         $"Filled: {order.CumulativeQuantity}@{order.AveragePrice:N2}";
                        _log.Info(message);
                        instance.UploadManualAdjustment(orderModel, order);
                        lock (instance)
                        {
                            if (_instanceIdToHangedOrdersMap.TryGetValue(instance, out var hangs))
                            {
                                hangs.Remove(orderModel);
                            }
                        }
                    }
                };
            }
        }

        private static (Window window, ComplexOrderTicketViewModel) CreateWindow(bool isSingleLeg = false)
        {
            Window window = null;
            if (isSingleLeg && OmsCore.Config.UseOrderTicketForSingleLegOrders)
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
            viewModel.SetDispatcher(window.Dispatcher);
            window.Dispatcher.UnhandledException += (_, e) =>
            {
                _log.Error(e.Exception, "DispatcherUnhandledException");
                e.Handled = true;
            };
            window.Closed += (_, _) => window.Dispatcher.InvokeShutdown();
            return (window, viewModel);
        }

        private string GetUnderlying(string symbol)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(symbol) || !symbol.StartsWith("."))
                {
                    return "";
                }

                var instrument = new Instrument(symbol);
                return instrument.valid ? instrument.underlyingSymbol : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public List<LowLatencyOrderModel> GetHangs(ILowLatencyInstance instance)
        {
            lock (instance)
            {
                if (_instanceIdToHangedOrdersMap.TryGetValue(instance, out var hangs))
                {
                    return hangs.ToList();
                }
            }

            return null;
        }
    }
}
