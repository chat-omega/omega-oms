using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Enums;
namespace ZeroPlus.Oms.Managers
{
    public delegate void MacroTriggerRequestEventHandler(string macro, object[] args, DateTime timestamp);
    public delegate void CommandRequestEventHandler(Command command, string[] args, DateTime timestamp);

    public class DominatorClient
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        public event MacroTriggerRequestEventHandler MacroTriggerRequestEvent;
        public event CommandRequestEventHandler CommandRequestEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsConfig _config;
        private readonly CommsClient _commsClient;
        private readonly Dominator _dominator;
        public bool IsConnected { get; set; } = false;

        public DominatorClient(OmsConfig config, OmsCore omsCore)
        {
            _dominator = new(omsCore);
            _config = config;
            _commsClient = new CommsClient(OmsConfig.ManagerGuid, config, HandleMessage, omsCore, register: false);
            _commsClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
        }

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            _log.Info(nameof(StartAsync));
            return await Task.Run(() => _commsClient.Start(_config.DominatorsManagerOmsAddress, _config.DominatorsManagerOmsPort));
        }

        public async Task StopAsync()
        {
            _log.Info(nameof(StopAsync));
            await Task.Run(() => _commsClient.Stop());
        }

        public void RegisterDom(string workbookName, string username, List<string> domSetups, List<string> fullAutoSetups)
        {
            _dominator.Source = workbookName;
            _dominator.Username = username;
            _dominator.Host = Environment.MachineName;
            _dominator.DominatorSetups = domSetups;
            _dominator.FullAutoSetups = fullAutoSetups;
            _dominator.IsInitialized = true;
            SendRegisterMessage();
        }

        public void UpdateStatus(string status)
        {
            DomCommand domCommand = new()
            {
                Command = Command.StatusUpdate,
                Argument = status,
                Timestamp = DateTime.Now,
            };

            _commsClient.SendDomCommand(domCommand);
        }

        public void UpdateDomSetup(string setup, string list)
        {
            _dominator.Setup = setup;
            _dominator.Configs = list;
            SendDomSetupMessage();
        }

        public void UpdateDomList(string product, string type, string listDate, string listCreator, string fullName, string subName, int listCount)
        {
            _dominator.Product = product;
            _dominator.Type = type;
            _dominator.ListDate = listDate;
            _dominator.ListCreator = listCreator;
            _dominator.FullName = fullName;
            _dominator.SubName = subName;
            _dominator.ListCount = listCount;
            SendDomListMessage();
        }

        public void UpdateDomState(string state, int domCount, int fills, double edgeMultiplier, double deltaMax)
        {
            if (Enum.TryParse(state, true, out DomState domState))
            {
                _dominator.State = domState;
                _dominator.DomCount = domCount;
                _dominator.Fills = fills;
                _dominator.EdgeMultiplier = edgeMultiplier;
                _dominator.DeltaMax = deltaMax;
                SendDomUpdateMessage();
            }
        }

        public void UpdateUniqueSubmissionsState(bool uniqueSubmissionsOn)
        {
            _dominator.UniqueSubmissionsOn = uniqueSubmissionsOn;
            SendDomUpdateMessage();
        }

        public void UpdateLoopSize(int loopSize)
        {
            _dominator.LoopSize = loopSize;
            SendDomUpdateMessage();
        }

        public void UpdateCalendarEdge(Dictionary<int, double> dayToEdgeMap)
        {
            _dominator.CalendarEdge = dayToEdgeMap;
            SendDomUpdateMessage();
        }

        public void UpdateDeltaEdge(Dictionary<double, double> deltaToEdgeMap)
        {
            _dominator.DeltaEdge = deltaToEdgeMap;
            SendDomUpdateMessage();
        }

        public void ShowMessage(string message, string title, bool silent)
        {
            DomMessage domMessage = new()
            {
                Message = message,
                Title = title,
                Silent = silent,
            };

            Task.Run(() => _commsClient.SendDomMessageMessage(domMessage));
        }

        public void PlaySound(int id)
        {
            PlaySoundRequest playSoundRequest = new()
            {
                Id = id,
                Name = String.Empty,
            };

            Task.Run(() => _commsClient.SendPlaySoundRequestMessage(playSoundRequest));
        }

        public void PlaySound(string name)
        {
            PlaySoundRequest playSoundRequest = new()
            {
                Id = 0,
                Name = name,
            };

            Task.Run(() => _commsClient.SendPlaySoundRequestMessage(playSoundRequest));
        }

        public void ShowVisualNotification(int timeout)
        {
            VisualNotificationRequest visualNotificationRequest = new()
            {
                Timeout = timeout,
            };

            Task.Run(() => _commsClient.SendVisualNotificationRequestMessage(visualNotificationRequest));
        }

        public void OpenIvChart(string symbol, int days, int mins, double underlyingPrice, Enums.UnderPriceSource source, IvChartType type)
        {
            OpenChartRequest openChartRequest = new()
            {
                Symbol = symbol,
                Days = days,
                Mins = mins,
                UnderPrice = underlyingPrice,
                UnderPriceSource = (Comms.Models.Data.Oms.Common.UnderPriceSource)source,
                ChartType = (ChartType)type,
            };

            Task.Run(() => _commsClient.SendOpenIvChartMessage(openChartRequest));
        }

        public void OpenTicket(string symbol, string route, string closingRoute, double price, double underPrice, double contraPrice, double contraUnderPrice, double edge = double.NaN, bool withContra = false, double left = -1, double top = -1, bool hedge = false, bool autoHedgeEnabled = false, double hedgePercentage = 0.0, string side = "")
        {
            OpenTicketRequest request = new()
            {
                TicketType = TicketType.Single,
                Symbol = symbol,
                Side = side,
                Price = price == 0 ? double.NaN : price,
                UnderPrice = underPrice == 0 ? double.NaN : underPrice,
                Route = route,
                ClosingRoute = closingRoute,
                WithContra = withContra,
                Left = left,
                Top = top,
                Hedge = hedge,
                AutoHedgeEnabled = autoHedgeEnabled,
                HedgePercentage = hedgePercentage,
                ContraPrice = contraPrice == 0 ? double.NaN : contraPrice,
                ContraUnderPrice = contraUnderPrice == 0 ? double.NaN : contraUnderPrice,
                Edge = edge == 0 ? double.NaN : edge,
            };

            Task.Run(() => _commsClient.SendOpenTicketRequestMessage(request));
        }

        public void OpenTicket(OpenTicketRequest request)
        {
            Task.Run(() => _commsClient.SendOpenTicketRequestMessage(request));
        }

        public void Open3WayTicket(string symbol, string side, double price, double underPrice, double contraPrice, double contraUnderPrice, double edge, string route, string closingRoute, string finishingRoute, double left = -1, double top = -1, double startingEdge = 0, double increment = 0, double interval = 0)
        {
            OpenTicketRequest request = new()
            {
                TicketType = TicketType.ThreeWay,
                Symbol = symbol,
                Side = side,
                Price = price == 0 ? double.NaN : price,
                UnderPrice = underPrice == 0 ? double.NaN : underPrice,
                Edge = edge,
                Route = route,
                ClosingRoute = closingRoute,
                FinishingRoute = finishingRoute,
                Left = left,
                Top = top,
                FishStartEdge = startingEdge,
                Increment = increment,
                Interval = interval,
                ContraPrice = contraPrice,
                ContraUnderPrice = contraUnderPrice,
            };

            Task.Run(() => _commsClient.SendOpenTicketRequestMessage(request));
        }

        public void CloseTicket(string symbol)
        {
            CloseTicketRequest request = new()
            {
                Symbol = symbol,
                IsBasket = false,
            };

            Task.Run(() => _commsClient.SendCloseTicketRequestMessage(request));
        }

        public void CloseBasket(string id)
        {
            CloseTicketRequest request = new()
            {
                Id = id,
                IsBasket = true,
            };

            Task.Run(() => _commsClient.SendCloseTicketRequestMessage(request));
        }

        public void OpenBasket(string mode, string id, string settings, object[] symbols)
        {
            if (Enum.TryParse(mode, true, out BasketMode basketMode))
            {
                OpenBasketRequest request = new()
                {
                    BasketMode = basketMode,
                    Id = string.IsNullOrEmpty(id) ? "" : id,
                    Settings = string.IsNullOrEmpty(settings) ? "" : settings,
                };

                foreach (object[] symbol in symbols)
                {
                    request.Tickets.Add(new OpenTicketRequest()
                    {
                        Symbol = (string)symbol[0],
                        Route = (string)symbol[1],
                        Price = (double)symbol[2],
                    });
                }
                SendOpenTicketRequest(request);
            }
        }

        public void OpenPermBasket(string id,
                                   List<OpenTicketRequest> symbols,
                                   string buyRoute = "",
                                   string sellRoute = "",
                                   double closeEdge = double.NaN,
                                   double closeInterval = double.NaN,
                                   double closeIncrement = double.NaN,
                                   double maxLoss = double.NaN,
                                   double loopInterval = double.NaN,
                                   double minEdge = double.NaN,
                                   int sizeUpQty = 0,
                                   int loopBeforeSizeup = 0)
        {
            OpenBasketRequest request = new()
            {
                BasketMode = BasketMode.PermBasket,
                Id = string.IsNullOrEmpty(id) ? "" : id,
                Tickets = symbols,
                BuyRoute = buyRoute,
                SellRoute = sellRoute,
                CloseEdge = Math.Abs(closeEdge),
                CloseInterval = Math.Abs(closeInterval),
                CloseIncrement = Math.Abs(closeIncrement),
                MaxLoss = Math.Abs(maxLoss),
                LoopInterval = Math.Abs(loopInterval),
                MinEdge = Math.Abs(minEdge),
                SizeUpQty = Math.Abs(sizeUpQty),
                LoopBeforeSizeup = Math.Abs(loopBeforeSizeup),
            };

            SendOpenTicketRequest(request);
        }

        private void SendOpenTicketRequest(OpenBasketRequest request)
        {
            Task.Run(() => _commsClient.SendOpenBasketRequestMessage(request));
        }

        private void OnConnectionStatusChangedEvent(bool connected)
        {
            _log.Info($"Connection status changed. Connected: {connected}");
            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected && _dominator.IsInitialized)
            {
                SendRegisterMessage();
                Task.Delay(500).ContinueWith(t =>
                {
                    SendDomSetupMessage();
                    SendDomListMessage();
                    SendDomUpdateMessage();
                });
            }
        }

        private void HandleMessage(Message message)
        {
            try
            {
                switch (message.Template.TemplateType)
                {
                    case TemplateType.DomCommand:
                        DomCommand domCommand = MessageFactory.DecodeDomCommandMessage(message);
                        HandleDomCommand(domCommand);
                        break;
                    case TemplateType.DomSettings:
                        DomSettings domSettings = MessageFactory.DecodeDomSettingsMessage(message);
                        HandleDomSettings(domSettings);
                        break;
                    case TemplateType.TradeForDom:
                        TradeForDom tradeForDom = MessageFactory.DecodeTradeForDomMessage(message);
                        HandleTradeForDom(tradeForDom);
                        break;
                    case TemplateType.Transaction:
                        Transaction transaction = MessageFactory.DecodeTransactionMessage(message);
                        HandleTransaction(transaction);
                        break;
                    case TemplateType.AutoTraderExecReport:
                        AutoTraderExecReport report = MessageFactory.DecodeAutoTraderExecReportMessage(message);
                        HandleAutoTraderExecReport(report);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleMessage));
            }
        }

        private void HandleAutoTraderExecReport(AutoTraderExecReport report)
        {
            string id, status, symbol, orderId;
            int quantity = report.Qty;
            double buyUnderPx = default, buyPrice = default, sellUnderPx = default, sellPrice = default;
            DateTime sellTime = default, buyTime = default;

            id = report.LocalId;
            orderId = report.OriginalOrderId;
            symbol = report.Symbol;

            int sideCode = 0;

            if (report.Side == Side.Buy)
            {
                sideCode = 1;

                buyPrice = report.Price;
                buyUnderPx = report.UnderPrice;
                buyTime = report.TradeTime;

                sellPrice = report.ContraPrice;
                sellUnderPx = report.ContraUnderPrice;
                sellTime = report.LastUpdateTime;
            }
            else if (report.Side == Side.Sell)
            {
                sideCode = 2;

                sellPrice = report.Price;
                sellUnderPx = report.UnderPrice;
                sellTime = report.TradeTime;

                buyPrice = report.ContraPrice;
                buyUnderPx = report.ContraUnderPrice;
                buyTime = report.LastUpdateTime;
            }

            switch (report.AutoTraderStatus)
            {
                case AutoTraderStatus.NoPositionTaken:
                    status = "FISH_NO_FILL";
                    break;
                case AutoTraderStatus.PositionClosed:
                    status = "FILLED";
                    break;
                case AutoTraderStatus.PositionOpened:
                    status = report.Side == Side.Buy ? "BUY_OPEN_MAX_LOSS" : "SELL_OPEN_MAX_LOSS";
                    break;
                case AutoTraderStatus.IncompletePositionClosed:
                    status = report.Side == Side.Buy ? "BUY_OPEN_MAX_LOSS" : "SELL_OPEN_MAX_LOSS";
                    break;
                default:
                    status = "FAILED";
                    break;
            }

            MacroTriggerRequestEvent?.Invoke("OmsQueueCallbackMacro", new object[]
            {
                id, // The guid received upon submission
                sideCode, // side code sent from Excel
                status, // Possible values: FAILED, FILLED, FISH_NO_FILL, BUY_OPEN_MAX_LOSS, SELL_OPEN_MAX_LOSS
                symbol, // For additional validation, the TOS-Id is sent back with the guid
                orderId, // The identifier of the submitted order, otherwise 'empty'
                quantity, // The quantity of the submitted order, otherwise '0'
                buyTime.ToString("HH:mm:ss.fff"), // The time where the buy side was filled or the highest buy price was submitted, otherwise 'empty'
                buyUnderPx, // The underlying price at the time where the buy side was filled or the highest buy price was submitted, otherwise '0'
                buyPrice, // The buy price filled or the highest buy price submitted, otherwise '0'
                sellTime.ToString("HH:mm:ss.fff"), // The time where the sell side was filled or the lowest sell price was submitted, otherwise 'empty'
                sellUnderPx, // The underlying price at the time where the sell side was filled or the lowest sell price was submitted, otherwise '0'
                sellPrice, // The sell price filled or the lowest sell price submitted, otherwise '0'
            }, DateTime.Now);
        }

        private void HandleDomCommand(DomCommand domCommand)
        {
            switch (domCommand.Command)
            {
                case Command.Start:
                    MacroTriggerRequestEvent?.Invoke("StartDominators", null, DateTime.Now);
                    break;
                case Command.Stop:
                    MacroTriggerRequestEvent?.Invoke("StopDominators", null, DateTime.Now);
                    break;
                case Command.Close:
                    CommandRequestEvent?.Invoke(Command.Close, null, DateTime.Now);
                    break;
                case Command.AllowUniqueSubmissions:
                    MacroTriggerRequestEvent?.Invoke("AllowUniqueSubmissions", null, DateTime.Now);
                    break;
                case Command.BlockUniqueSubmissions:
                    MacroTriggerRequestEvent?.Invoke("BlockUniqueSubmissions", null, DateTime.Now);
                    break;
                case Command.SaveLog:
                    MacroTriggerRequestEvent?.Invoke("SaveLogs", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
                case Command.EnableLoadPriceChain:
                    MacroTriggerRequestEvent?.Invoke("LOAD_OMS_PRICE_CHAIN_RTD_FORMULAS", null, DateTime.Now);
                    break;
                case Command.DisableLoadPriceChain:
                    MacroTriggerRequestEvent?.Invoke("clear_price_chain_formulas", null, DateTime.Now);
                    break;
                case Command.EnableLeastDataPossible:
                    MacroTriggerRequestEvent?.Invoke("LEAST_DATA_POSSIBLE", null, DateTime.Now);
                    break;
                case Command.DisableLeastDataPossible:
                    MacroTriggerRequestEvent?.Invoke("Clear_Neighbor_strike_Px_comparison_MDS_GREEKS", null, DateTime.Now);
                    MacroTriggerRequestEvent?.Invoke("CLEAR_NEIGHBIOR_EXPIRY", null, DateTime.Now);
                    break;
                case Command.SelectChannel:
                    switch (domCommand.Argument)
                    {
                        case "1":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_1", null, DateTime.Now);
                            break;
                        case "2":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_2", null, DateTime.Now);
                            break;
                        case "3":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_3", null, DateTime.Now);
                            break;
                        case "4":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_4", null, DateTime.Now);
                            break;
                        case "5":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_5", null, DateTime.Now);
                            break;
                        case "6":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_6", null, DateTime.Now);
                            break;
                        case "7":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_7", null, DateTime.Now);
                            break;
                        case "8":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_8", null, DateTime.Now);
                            break;
                        case "9":
                            MacroTriggerRequestEvent?.Invoke("lOAD_FA_2_Channel_9", null, DateTime.Now);
                            break;
                    }
                    break;
                case Command.RemoveHighDeltaSpreadsAndStart:
                    MacroTriggerRequestEvent?.Invoke("LOAD_DOMINATOR_HW_DELTAS_REMOVE_SPREADS_START_DOM", null, DateTime.Now);
                    break;
                case Command.LoadEmaCapture:
                    MacroTriggerRequestEvent?.Invoke("LOAD_EMA_Capture", null, DateTime.Now);
                    break;
                case Command.DisplayFirmTradeActivity:
                    MacroTriggerRequestEvent?.Invoke("LOAD_FORMULAS_TO_DISPLAY_ZP_TRADED_SPREADS_BUTTON", null, DateTime.Now);
                    break;
                case Command.ChangeRoute:
                    MacroTriggerRequestEvent?.Invoke("Route_SPX_TO_" + domCommand.Argument?.ToUpper(), null, DateTime.Now);
                    break;
                case Command.BlockUnderlyings:
                    MacroTriggerRequestEvent?.Invoke("BlockUnderlyings", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
                case Command.BlockSymbols:
                    MacroTriggerRequestEvent?.Invoke("BlockSymbols", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
                case Command.BlockExpirations:
                    MacroTriggerRequestEvent?.Invoke("BlockExpirations", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
                case Command.NewDomEdgeCalc:
                    MacroTriggerRequestEvent?.Invoke("ReturnTargetEdgeWithParameters", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
                case Command.NewDomSpreadFilter:
                    MacroTriggerRequestEvent?.Invoke("ReturnFiltersResultWithParameters", new string[] { domCommand.Argument }, DateTime.Now);
                    break;
            }
        }

        private void HandleTransaction(Transaction transaction)
        {
            string[] updates = new string[7]
            {
                transaction.Symbol,
                transaction.OrdStatus.ToString(),
                transaction.AvgPx.ToString(),
                transaction.LastQty.ToString(),
                transaction.ParentRefID,
                transaction.Fee1.ToString(),
                transaction.Fee2.ToString(),
            };
            MacroTriggerRequestEvent?.Invoke("HandleOrderUpdate", updates, DateTime.Now);
        }

        private void HandleTradeForDom(TradeForDom tradeForDom)
        {
            string[] args = new string[]
            {
                tradeForDom.UnderSymbol,
                tradeForDom.Symbol,
                tradeForDom.Exchange,
                tradeForDom.SpreadType,
                tradeForDom.Bid.ToString(),
                tradeForDom.Ask.ToString(),
                tradeForDom.Price.ToString(),
                tradeForDom.UnderPrice.ToString(),
                tradeForDom.MidMarket.ToString(),
                tradeForDom.TradeDelta.ToString(),
                tradeForDom.Quantity.ToString(),
            };
            MacroTriggerRequestEvent?.Invoke("HandleTradeRequest", args, DateTime.Now);
        }

        private void HandleDomSettings(DomSettings domSettings)
        {
            MacroTriggerRequestEvent?.Invoke("LoadDominatorSettings", new string[] { domSettings.Settings }, DateTime.Now);
        }

        private void SendRegisterMessage()
        {
            DomRegister domRegister = new()
            {
                Source = _dominator.Source,
                Username = _dominator.Username,
                Host = _dominator.Host,
                DominatorSetups = _dominator.DominatorSetups,
                FullAutoSetups = _dominator.FullAutoSetups,
            };
            Task.Run(() => _commsClient.SendDomRegisterMessage(domRegister));
        }

        private void SendDomUpdateMessage()
        {
            DomStateUpdate domStateUpdate = new()
            {
                State = _dominator.State,
                DomCount = _dominator.DomCount,
                Fills = _dominator.Fills,
                EdgeMultiplier = _dominator.EdgeMultiplier,
                DeltaMax = _dominator.DeltaMax,
                LoopSize = _dominator.LoopSize,
                CalendarEdge = _dominator.CalendarEdge,
                DeltaEdge = _dominator.DeltaEdge,
                UniqeSpreadsOn = _dominator.UniqueSubmissionsOn,
                Timestamp = DateTime.Now,
            };

            Task.Run(() => _commsClient.SendDomStateUpdateMessage(domStateUpdate));
        }

        private void SendDomSetupMessage()
        {
            DomSetupUpdate domSetupUpdate = new()
            {
                Setup = _dominator.Setup,
                List = _dominator.Configs,
            };

            Task.Run(() => _commsClient.SendDomSetupUpdateMessage(domSetupUpdate));
        }

        private void SendDomListMessage()
        {
            DomListUpdate domListUpdate = new()
            {
                Product = _dominator.Product,
                Type = _dominator.Type,
                ListDate = _dominator.ListDate,
                ListCreator = _dominator.ListCreator,
                FullName = _dominator.FullName,
                SubName = _dominator.SubName,
                ListCount = _dominator.ListCount,
            };

            Task.Run(() => _commsClient.SendDomListUpdateMessage(domListUpdate));
        }

        public void StartDoms(string argument)
        {
            DomCommand domCommand = new()
            {
                Command = Command.Start,
                Argument = argument,
                Timestamp = DateTime.Now,
            };

            Task.Run(() => _commsClient.SendDomCommand(domCommand));
        }

        public void StopDoms(string argument)
        {
            DomCommand domCommand = new()
            {
                Command = Command.Stop,
                Argument = argument,
                Timestamp = DateTime.Now,
            };

            Task.Run(() => _commsClient.SendDomCommand(domCommand));
        }
        public void SendCommand(string commmadCode, string argument)
        {
            DomCommand domCommand = new()
            {
                Command = Enum.Parse<Command>(commmadCode),
                Argument = argument,
                Timestamp = DateTime.Now,
            };
            Task.Run(() => _commsClient.SendDomCommand(domCommand));
        }
        public void SendEdge(string symbol, double edge, string argsJson)
        {
            string argument = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                Symbol = symbol,
                Edge = edge,
                DomParams = argsJson
            });
            DomCommand domCommand = new()
            {
                Command = Command.NewDomEdgeCalc,
                Argument = argument,
                Timestamp = DateTime.Now,
            };
            Task.Run(() => _commsClient.SendDomCommand(domCommand));
        }
        public void SendFilter(string symbol, bool approvedSpread, string argsJson)
        {
            string argument = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                Symbol = symbol,
                Filter = approvedSpread,
                DomParams = argsJson
            });
            DomCommand domCommand = new()
            {
                Command = Command.NewDomSpreadFilter,
                Argument = argument,
                Timestamp = DateTime.Now,
            };
            Task.Run(() => _commsClient.SendDomCommand(domCommand));
        }
        public Guid SendAutoTraderOrder(string symbol, Side side, long price, long underPrice, string argsJson, Guid? cancelSendParent = null)
        {
            var id = Guid.NewGuid();
            string argument = Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                Id = id,
                Symbol = symbol,
                Side = side,
                Price = price,
                UnderPrice = underPrice,
                CancelSendParentId = cancelSendParent,
                DomParams = argsJson
            });
            DomCommand domCommand = new()
            {
                Command = Command.DomAutoTraderInfo,
                Argument = argument,
                Timestamp = DateTime.Now,
            };
            Task.Run(() => _commsClient.SendDomCommand(domCommand));
            return id;
        }
    }
}
