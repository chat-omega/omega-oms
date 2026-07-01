using Middleware.Communication.Tcp;
using NLog;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Comms.Models.Protocols.FAST.Codec;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Excel;
using ZeroPlus.Oms.Data.Trading;
using OrderStatus = ZeroPlus.Models.Data.Enums.OrderStatus;
using Side = ZeroPlus.Comms.Models.Data.Trading.Side;

namespace ZeroPlus.Oms.Managers
{
    public class Dominator : INotifyPropertyChanged, IOrderInfoUpdateHandler
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        internal TcpSocket Socket;

        public string Id;
        public string Source { get; set; }
        public string Username { get; set; }
        public string Host { get; set; }
        public List<string> DominatorSetups { get; set; }
        public List<string> FullAutoSetups { get; set; }
        public Dictionary<string, List<string>> CustomSetups { get; set; }
        public string Setup { get; set; }
        public string Configs { get; set; }
        public DomState State { get; set; }
        public int DomCount { get; set; }
        public int Fills { get; set; }
        public double DeltaMax { get; set; }
        public double EdgeMultiplier { get; set; }
        public int LoopSize { get; set; }
        public bool IsInitialized { get; set; }
        public string Product { get; set; }
        public string Type { get; set; }
        public string ListDate { get; set; }
        public string ListCreator { get; set; }
        public string FullName { get; set; }
        public string SubName { get; set; }
        public int ListCount { get; set; }
        public bool ShowNotification { get; set; }
        public int NotificationTimeout { get; set; }
        public bool UniqueSubmissionsOn { get; set; }
        public Dictionary<int, double> CalendarEdge { get; set; } = new Dictionary<int, double>();
        public Dictionary<double, double> DeltaEdge { get; set; } = new Dictionary<double, double>();
        public ConcurrentDictionary<string, string> StatusUpdateMap { get; set; } = new ConcurrentDictionary<string, string>();
        public string AutoTraderConfigId;
        readonly IOmsCore _omsCore;
        public Dominator(IOmsCore omsCore)
        {
            Id = Guid.NewGuid().ToString();
            _omsCore = omsCore;
        }

        public void Start()
        {
            try
            {
                DomCommand domStart = new()
                {
                    Command = Command.Start
                };
                SendDomCommand(domStart);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Start));
            }
        }

        public void Stop()
        {
            try
            {
                DomCommand domStop = new()
                {
                    Command = Command.Stop
                };
                SendDomCommand(domStop);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Stop));
            }
        }

        public void AllowUniqueSubmissions()
        {
            try
            {
                DomCommand domStart = new()
                {
                    Command = Command.AllowUniqueSubmissions
                };
                SendDomCommand(domStart);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Start));
            }
        }

        public void BlockUniqueSubmissions()
        {
            try
            {
                DomCommand domStop = new()
                {
                    Command = Command.BlockUniqueSubmissions
                };
                SendDomCommand(domStop);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Stop));
            }
        }

        public void RequestEdgeCalculation(string Symbol)
        {
            try
            {
                DomCommand domEdgeCalc = new()
                {
                    Command = Command.NewDomEdgeCalc,
                    Argument = Symbol
                };
                SendDomCommand(domEdgeCalc);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestEdgeCalculation));
            }
        }

        public void RequestFilterResult(string Symbol)
        {
            try
            {
                DomCommand domFilterResult = new()
                {
                    Command = Command.NewDomSpreadFilter,
                    Argument = Symbol
                };
                SendDomCommand(domFilterResult);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RequestFilterResult));
            }
        }

        public void SendDomCommand(DomCommand domStart)
        {
            try
            {
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendDomCommand));
            }
        }

        public void SaveLog(int delay)
        {
            try
            {
                TimeSpan span = TimeSpan.FromSeconds(delay);
                DomCommand command = new()
                {
                    Command = Command.SaveLog,
                    Argument = span.ToString(),
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        public void ChangeLoadPriceChain(bool enable)
        {
            try
            {
                DomCommand command = new()
                {
                    Command = enable ? Command.EnableLoadPriceChain : Command.DisableLoadPriceChain,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        public void RemoveHighDeltaSpreadsAndStart()
        {
            try
            {
                DomCommand command = new()
                {
                    Command = Command.RemoveHighDeltaSpreadsAndStart,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RemoveHighDeltaSpreadsAndStart));
            }
        }

        public void LoadEmaCapture()
        {
            try
            {
                DomCommand command = new()
                {
                    Command = Command.LoadEmaCapture,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadEmaCapture));
            }
        }

        public void DisplayFirmTradeActivity()
        {
            try
            {
                DomCommand command = new()
                {
                    Command = Command.DisplayFirmTradeActivity,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadEmaCapture));
            }
        }

        public void ChangeLeastDataPossible(bool enable)
        {
            try
            {
                DomCommand command = new()
                {
                    Command = enable ? Command.EnableLeastDataPossible : Command.DisableLeastDataPossible,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        public void SelectChannel(string channel)
        {
            try
            {
                DomCommand command = new()
                {
                    Command = Command.SelectChannel,
                    Argument = channel,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        public void ChangeRoute(string route)
        {
            try
            {
                DomCommand command = new()
                {
                    Command = Command.ChangeRoute,
                    Argument = route,
                };
                SendDomCommand(command);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveLog));
            }
        }

        public void UpdateSettings(string settings)
        {
            try
            {
                DomSettings domSettings = new()
                {
                    Settings = settings
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomSettingsMessage(domSettings)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateSettings));
            }
        }

        public void LoadFullAutoSetup(string setup)
        {
            try
            {
                DomSettings domSettings = new()
                {
                    Settings = "FullAutoSetup:" + setup + ";"
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomSettingsMessage(domSettings)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadFullAutoSetup));
            }
        }

        public void LoadDominatorSetup(string setup)
        {
            try
            {
                DomSettings domSettings = new()
                {
                    Settings = "DominatorSetup:" + setup + ";"
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomSettingsMessage(domSettings)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadDominatorSetup));
            }
        }

        public void LoadCustomSetup(string title, string setup)
        {
            try
            {
                DomSettings domSettings = new()
                {
                    Settings = title + ":" + setup + ";"
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomSettingsMessage(domSettings)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadDominatorSetup));
            }
        }

        public void LoadList(string list, string sublist)
        {
            try
            {
                DomSettings domSettings = new()
                {
                    Settings = "List:" + list + ";" + "Sheet:" + sublist + ";"
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomSettingsMessage(domSettings)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadList));
            }
        }

        public void SendTradeToDominator(TradeForDom trade)
        {
            try
            {
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateTradeForDomMessage(trade)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendTradeToDominator));
            }
        }

        public void CloseInstance()
        {
            try
            {
                DomCommand domStart = new()
                {
                    Command = Command.Close
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloseInstance));
            }
        }

        public void HandleOrderUpdate(string requestSymbol, IOmsOrder order, Models.Data.Enums.OrderStatus orderStatus)
        {
            try
            {
                Comms.Models.Data.Trading.Transaction transaction = new()
                {
                    Symbol = order.Symbol,
                    Side = order.Side == Models.Data.Enums.Side.Buy ? Side.Buy : Side.Sell,
                    OrdStatus = Enum.Parse<OrdStatus>(orderStatus.ToString()),
                    AvgPx = order.AveragePrice,
                    LastQty = order.LastQuantity,
                    CumQty = order.FilledQty,
                    ParentRefID = requestSymbol,
                    Fee1 = order.Bid,
                    Fee2 = order.Ask,
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateTransactionMessage(transaction)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOrderUpdate));
            }
        }

        public void BlockUnderlyings(string underlyings)
        {
            try
            {
                DomCommand domStart = new()
                {
                    Command = Command.BlockUnderlyings,
                    Argument = underlyings,
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BlockUnderlyings));
            }
        }

        public void BlockSymbols(string symbols)
        {
            try
            {
                DomCommand domStart = new()
                {
                    Command = Command.BlockSymbols,
                    Argument = symbols,
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BlockUnderlyings));
            }
        }

        public void BlockExpirations(Dictionary<string, HashSet<DateTime>> underToExpirationsMap)
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, HashSet<DateTime>> kvp in underToExpirationsMap.OrderBy(x => x.Key))
                {
                    sb.Append(kvp.Key).Append(';').Append(string.Join(";", kvp.Value.Select(x => x.ToString("yy-MMM-dd")))).Append(',');
                }
                string expirations = sb.ToString();
                DomCommand domStart = new()
                {
                    Command = Command.BlockExpirations,
                    Argument = expirations,
                };
                Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domStart)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BlockUnderlyings));
            }
        }
        public bool NegativeTestEdge = false;
        public void SendAutoTraderOrder(
            Guid orderId,
            string symbol,
            Side side,
            long price,
            long underPrice,
            bool isDomContra,
            int quantity,
            Models.Data.Enums.MinimumTickStyle minimumTickStyle,
            AutoTraderClient orderGateway)
        {
            if (Models.Utils.OptionStrategy.TryIdentify(symbol,
                out Models.Data.Enums.BaseStrategy baseStrategy,
                out string spreadType,
                out string spreadDescription))
            {
                double avgPrice = (double)price / 100.0;
                var side_m = side switch
                {
                    Side.Buy => Models.Data.Enums.Side.Buy,
                    Side.Sell => Models.Data.Enums.Side.Sell,
                    _ => throw new SlimException("Unrecognised side")
                };
                bool isComplexOrder = !(baseStrategy == Models.Data.Enums.BaseStrategy.PUT || baseStrategy == Models.Data.Enums.BaseStrategy.CALL);
                var codec = new SymbolCodec(symbol);
                string underS = codec.UnderlyingSymbol();
                Models.Data.Securities.Security security = (isComplexOrder ? _omsCore.SecurityBook.GetSecurity(underS)
                    : _omsCore.SecurityBook.GetSecurity(symbol))
                    ?? throw new SlimException("No security");

                var multiplier = isComplexOrder ? _omsCore.SecurityBook.GetSecurity(codec.GetLeg(0).symbol).Multiplier : security.Multiplier;
                // check all legs

                ExcelAutoTraderOrder order = new(_omsCore.SecurityBook, symbol)
                {
                    // unique global id for order is only used to send updates to the right client
                    LocalID = orderId.ToString(),
                    //SpreadId = orderId.ToString(),
                    Comment = Id,
                    SubType = OrderSubType.Fish, // info

                    IsComplexOrder = isComplexOrder,
                    // get security from security book
                    Security = security,
                    Multiplier = multiplier,
                    MinimumTickStyle = minimumTickStyle,
                    PositionEffect = Models.Data.Enums.PositionEffect.Open,

                    // Route = Route.Exrolls // if not set in the autotrader config must be set


                    // IMPORTANT Add username from oms core (P/L)
                    Tag = "DOM "+ _omsCore.User.Username,
                    NewToCancelTime = 0,
                    Destination = AutoTraderConfigId,
                    TimeInForce = Models.Data.Enums.TimeInForce.DAY,
                    AccountAcronym = OmsCore.Config.DefaultAccount,

                    // fish order always has quantity 1
                    Quantity = quantity,

                    Side = side_m
                };
                if (NegativeTestEdge)
                {
                    order.EdgeOverride = -0.05;
                    order.CloseUnderBid = underPrice / 100.0;
                    order.CloseUnderAsk = underPrice / 100.0;
                    _log.Info("Negative Edge Override");
                }
                else
                {
                    order.CloseUnderBid = underPrice / 100.0;
                    order.CloseUnderAsk = underPrice / 100.0;
                    // AveragePrice will use delta adjusted price, Price uses exact prce
                    order.AveragePrice = avgPrice;
                }

                orderLoopStatusDict.TryAdd(orderId, new OrderState(false, isDomContra, side, symbol));
                orderGateway.SendSlimOrder(order, this);
                _log.Info("Send GOOD");
            }
            else throw new SlimException($"Invalid spread sent from Excel: {symbol}");
        }

        #region AutoTrader Order update callbacks
        private sealed record OrderState(bool IsLooping, bool IsDomContra, Side Side, string Symbol)
        {
            public List<OrderUpdateValues> Updates { get; } = new();
        }
        private readonly ConcurrentDictionary<Guid, OrderState> orderLoopStatusDict = new();
        public event EventHandler<OrderUpdateValues> OrderClose;
        public event EventHandler<(string symbol, int quantity, OrderUpdateValues update)> ManualIntervention;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Callback on status update from autotrader
        /// see also: OrderTicketViewModelBase.UpdateOrderStatusAsync(orderUpdate, orderUpdate.OrderStatus);
        /// </summary>
        /// <param name="orderUpdate"></param>
        public void OrderUpdated(OrderUpdateValues orderUpdate)
        {
            Guid originalIdGuid = new(orderUpdate.LocalOrderId);
            bool present = orderLoopStatusDict.TryGetValue(originalIdGuid, out var orderLoopStatus);
            if (!present) throw new SlimException($"Order Not Tracked on Current Dominator Session: {Id}, {orderUpdate.LocalOrderId}");
            orderLoopStatusDict[originalIdGuid] = orderLoopStatus with { IsLooping = orderUpdate.IsLooping };

            try
            {
                HandleEntryOrderUpdate(orderUpdate, originalIdGuid);
                HandleExitOrderUpdate(orderUpdate, originalIdGuid);
                HandleHedgeOrderUpdate(orderUpdate, originalIdGuid);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(orderUpdate));
            }
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
        }

        public void AutomationStateChanged(bool running)
        {
        }

        /// <summary>
        /// Dominator Needs to be notified if fishing a spread on both long and short side failed
        /// </summary>
        /// <param name="orderUpdate">autotrader opening a postion</param>
        void HandleEntryOrderUpdate(OrderUpdateValues orderUpdate, Guid id)
        {
            if (orderUpdate.IsMainOrder)
            {
                // looper opening a postion
                // handling main order
                // main order updates aren't reported to excel
                // UNLESS fishing the entry fails for both the long and short side
                switch (orderUpdate.OrderStatus)
                {
                    case OrderStatus.Rejected:
                    case OrderStatus.Canceled:
                        orderLoopStatusDict[id].Updates.Add(orderUpdate);
                        SendFishFailedToExcel(orderUpdate, orderLoopStatusDict[id].Symbol);
                        orderLoopStatusDict.Remove(id, out _);
                        break;
                    // case OrderStatus.Replaced: // autotrader does cancel resubmit
                    case OrderStatus.PartiallyFilled:
                    case OrderStatus.Filled:
                        orderLoopStatusDict[id].Updates.Add(orderUpdate);
                        break;
                    case OrderStatus.Stopped:
                    case OrderStatus.Suspended:
                    case OrderStatus.DoneForDay:
                    case OrderStatus.Expired:
                        SendFishErrorToExcel(orderUpdate);
                        orderLoopStatusDict.Remove(id, out _);
                        break;
                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// Dominator isn't setup for processing hedge order data
        /// </summary>
        /// <param name="orderUpdate"></param>
        void HandleHedgeOrderUpdate(OrderUpdateValues orderUpdate, Guid id)
        {
            if (orderUpdate.IsHedgeOrder)
            {
                SendFishErrorToExcel(orderUpdate);
            }
        }
        /// <summary>
        /// closing a contra order with fill completes the transaction
        /// </summary>
        /// <param name="orderUpdate">looper closing transaction</param>
        void HandleExitOrderUpdate(OrderUpdateValues orderUpdate, Guid id)
        {
            if (orderUpdate.IsContraOrder)
            {
                switch (orderUpdate.OrderStatus)
                {
                    case OrderStatus.Rejected:
                    case OrderStatus.Canceled:
                        if (orderUpdate.RequiresManualIntervention) SendTransactionUnfinishedToExcel(orderUpdate);
                        break;
                    case OrderStatus.New:
                    case OrderStatus.PartiallyFilled:
                        orderLoopStatusDict[id].Updates.Add(orderUpdate);
                        break;
                    case OrderStatus.Filled:
                        orderLoopStatusDict[id].Updates.Add(orderUpdate);
                        SendTransactionToExcel(orderUpdate);
                        OrderClose?.Invoke(this, orderUpdate);
                        break;
                    case OrderStatus.Stopped:
                    case OrderStatus.Suspended:
                    case OrderStatus.DoneForDay:
                    case OrderStatus.Expired:
                        SendFishErrorToExcel(orderUpdate);
                        orderLoopStatusDict.Remove(id, out _);
                        break;
                    default:
                        return;
                }
            }
        }
        AutoTraderExecReport PopulateReport(OrderUpdateValues orderUpdate, AutoTraderStatus status)
        {
            var orderLoopStatus = orderLoopStatusDict[new Guid(orderUpdate.LocalOrderId)];
            var openPositionUpdate = orderLoopStatus.Updates.FirstOrDefault();
            var closePositionUpdate = orderLoopStatus.Updates.Skip(1).LastOrDefault();

            return new AutoTraderExecReport()
            {
                LocalId = orderUpdate.LocalOrderId,
                AutoTraderStatus = status,
                Side = orderLoopStatus.Side,
                Symbol = orderLoopStatus.Symbol,
                OriginalOrderId = orderUpdate.OrderId,
                Qty = orderUpdate.LastQuantity,
                Price = openPositionUpdate?.Price ?? 0,
                ContraPrice = closePositionUpdate?.Price ?? 0,
                UnderPrice = openPositionUpdate?.UnderlyingMidPrice ?? 0,
                ContraUnderPrice = closePositionUpdate?.UnderlyingMidPrice ?? 0,
                TradeTime = openPositionUpdate?.LastUpdateTime.ToLocalTime() ?? DateTime.Now.ToLocalTime(),
                LastUpdateTime = closePositionUpdate?.LastUpdateTime.ToLocalTime() ?? DateTime.Now.ToLocalTime(),
                LeavesQuantity = orderUpdate.LeavesQuantity
            };
        }
        void SendFishFailedToExcel(OrderUpdateValues orderUpdate, string symbol)
        {
            var report = PopulateReport(orderUpdate, AutoTraderStatus.NoPositionTaken);
#if DEBUG
            report.AutoTraderStatus = AutoTraderStatus.PositionClosed;
            report.LeavesQuantity = 0;
            report.ContraUnderPrice = report.UnderPrice = 1000.0;
            report.ContraPrice = report.Price = 10.0;
            report.Qty = 1;
#endif
            Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateAutoTraderExecReportMessage(report)));
            _log.Info($"{nameof(SendFishFailedToExcel)} for {symbol} with {report.LocalId}");
        }
        void SendTransactionUnfinishedToExcel(OrderUpdateValues orderUpdate)
        {
            var report = PopulateReport(orderUpdate, AutoTraderStatus.PositionOpened);
            Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateAutoTraderExecReportMessage(report)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LoopingStatus"));
            ManualIntervention.Invoke(this, (report.Symbol, orderUpdate.Filled, orderUpdate));
            _log.Info($"{nameof(SendTransactionUnfinishedToExcel)} for {report.Symbol} with {report.LocalId}");
        }
        void SendTransactionToExcel(OrderUpdateValues orderUpdate)
        {
            var report = PopulateReport(orderUpdate, AutoTraderStatus.PositionClosed);
            Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateAutoTraderExecReportMessage(report)));
            _log.Info($"{nameof(SendTransactionToExcel)} for {report.Symbol} with {report.LocalId}");
        }
        void SendFishErrorToExcel(OrderUpdateValues orderUpdate, Exception ex = null)
        {
            var report = PopulateReport(orderUpdate, AutoTraderStatus.ERROR);
            Socket.SendAsync(FastEncoder.Encode(MessageFactory.CreateAutoTraderExecReportMessage(report)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LoopingStatus"));
            _log.Error(ex, $"{nameof(SendTransactionToExcel)} for {report.Symbol} with {report.LocalId}");
        }

        void IOrderInfoUpdateHandler.OrderInfoUpdated(OrderInfoUpdate update) { }

        static string DomMainId_TEST { get; set; } = Guid.NewGuid().ToString();
        public void TestDummyReport() => TestDummyReport(DomMainId_TEST);
        public void TestDummyReport(string DomMainId_TEST)
        {
            var report = new AutoTraderExecReport()
            {
                AutoTraderStatus = AutoTraderStatus.PositionClosed,
                Symbol = "SPX",
                OriginalOrderId = DomMainId_TEST,
                Qty = 1
            };
            var m = MessageFactory.CreateAutoTraderExecReportMessage(report);
            var bits = FastEncoder.Encode(m);
            Socket.SendAsync(bits);
            _log.Info("Dummy Report Sent to Excel");
#if DEBUG
            try
            {
                var z = MessageFactory.DecodeAutoTraderExecReportMessage(m);
                _log.Info(z.Symbol);
            }
            catch
            {
                _log.Warn("FAIL to send");
            }
#endif
        }

        #endregion

        internal void ParseStatusUpdate(string argument)
        {
            try
            {
                if (string.IsNullOrEmpty(argument))
                {
                    return;
                }
                string[] messages = argument.Split(",");
                foreach (string message in messages)
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        string[] kvp = message.Split(":");
                        if (kvp.Length == 2)
                        {
                            string key = kvp[0]?.Trim();
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                string value = kvp[1]?.Trim();
                                if (!string.IsNullOrWhiteSpace(value))
                                {
                                    StatusUpdateMap[key] = value;
                                }
                                else
                                {
                                    StatusUpdateMap.TryRemove(key, out _);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ParseStatusUpdate));
            }
        }
    }
}
