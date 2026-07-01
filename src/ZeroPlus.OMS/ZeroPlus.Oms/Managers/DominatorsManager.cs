using Middleware.Communication.Tcp;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Excel;
using MinimumTickStyle = ZeroPlus.Models.Data.Enums.MinimumTickStyle;
using Security = ZeroPlus.Models.Data.Securities.Security;

namespace ZeroPlus.Oms.Managers
{
    public delegate void DominatorUpdatedEventHandler(Dominator dominator);
    public delegate void ExcelManagerUpdatedEventHandler(ExcelManager excelManager);
    public delegate void DominatorMessageEventHandler(string message, string title, bool silent);
    public delegate void DominatorPlaySoundRequestEventHandler(int id, string name);
    public delegate void OpenTicketRequestEventHandler(OpenTicketRequest openTicketRequest, Dominator dominator);
    public delegate void CloseTicketRequestEventHandler(CloseTicketRequest closeTicketRequest);
    public delegate void OpenBasketRequestEventHandler(OpenBasketRequest openBasketRequest);
    public delegate void OpenChartRequestEventHandler(OpenChartRequest openChartRequest);

    public class DominatorsManager
    {
        public event ServerStatusChangedEventHandler ServerStatusChangedEvent;
        public event DominatorUpdatedEventHandler DominatorUpdatedEvent;
        public event DominatorUpdatedEventHandler DominatorDisconnectedEvent;
        public event DominatorMessageEventHandler DominatorMessageEvent;
        public event DominatorPlaySoundRequestEventHandler DominatorPlaySoundRequestEvent;
        public event ExcelManagerUpdatedEventHandler ExcelManagerConnectedEvent;
        public event ExcelManagerUpdatedEventHandler ExcelManagerDisconnectedEvent;
        public event OpenTicketRequestEventHandler OpenTicketRequestEvent;
        public event CloseTicketRequestEventHandler CloseTicketRequestEvent;
        public event OpenBasketRequestEventHandler OpenBasketRequestEvent;
        public event OpenChartRequestEventHandler OpenChartRequestEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<TcpSocket, HashSet<Dominator>> _stopRequestToStoppedDomsMap = new();

        private readonly OmsConfig _config;
        private readonly ConcurrentDictionary<TcpSocket, Dominator> _socketToDomMap = new();
        private readonly ConcurrentDictionary<TcpSocket, ExcelManager> _socketToExcelManagerMap = new();
        public dynamic DominatorTraderModel { get; set; }
        public Dominator AnyDominator => _socketToDomMap.Values.FirstOrDefault();
        public readonly IOmsCore OmsCore;
        private TcpServer _tcpServer;
        public bool Listening { get; set; }

        public DominatorsManager(OmsConfig config, IOmsCore omsCore)
        {
            _config = config;
            OmsCore = omsCore;
        }

        public async Task StartServerAsync()
        {
            await Task.Run(() => StartServer());
        }

        public void StartServer()
        {
            SetupServer();
            _tcpServer.Start();
        }

        public async Task StopServerAsync()
        {
            await Task.Run(() => StopServer());
        }

        public void StopServer()
        {
            _tcpServer?.Stop();
        }

        private void SetupServer()
        {
            IPAddress address = IPAddress.Parse(_config.DominatorsManagerListenerAddress);
            IPEndPoint localEndPoint = new(address, _config.DominatorsManagerListenerPort);

            _tcpServer = new TcpServer(localEndPoint)
            {
                TcpMessageParser = new TcpFastMessageParser()
            };

            _tcpServer.TcpServerStarted += OnTcpServerStarted;
            _tcpServer.TcpServerStopped += OnTcpServerStopped;
            _tcpServer.TcpSocketConnected += OnClientConnected;
            _tcpServer.TcpSocketDisconnected += OnClientDisconnected;
            (_tcpServer.TcpMessageParser as TcpFastMessageParser).Message += new TcpFastMessageParser.MessageEventHandler(OnMessageHandler);
        }

        private void OnTcpServerStarted(TcpServer tcpServer)
        {
            Listening = true;
            ServerStatusChangedEvent?.Invoke(true);
            _socketToDomMap.Clear();
            _log.Info($"{nameof(OnTcpServerStarted)} -> Server started. " +
                      $"Address: {tcpServer.EndPoint.Address}, " +
                      $"Port: {tcpServer.EndPoint.Port}");
        }

        public void DispatchMessage(string message, string title)
        {
            DominatorMessageEvent?.Invoke(message, title, true);
        }

        private void OnTcpServerStopped(TcpServer tcpServer)
        {
            Listening = false;
            ServerStatusChangedEvent?.Invoke(false);
            _socketToDomMap.Clear();
            _log.Info($"{nameof(OnTcpServerStopped)} -> Server stopped. " +
                      $"Address: {tcpServer.EndPoint.Address}, " +
                      $"Port: {tcpServer.EndPoint.Port}");
        }

        private void OnClientConnected(TcpSocket tcpSocket)
        {
            if (tcpSocket.TcpSocketStatus == TcpSocketStatus.Connected && !tcpSocket.IsShutdown)
            {
                _log.Info($"{nameof(OnClientConnected)} -> Client connected. " +
                      $"Address: {tcpSocket.RemoteEndPoint.Address}, " +
                      $"Port: {tcpSocket.RemoteEndPoint.Port}, " +
                      $"Connect time: {tcpSocket.ConnectTime}");
            }
        }

        private void OnClientDisconnected(TcpSocket tcpSocket)
        {
            if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator oldDom))
            {
                oldDom.State = DomState.Stopped;
                DominatorDisconnectedEvent?.Invoke(oldDom);
            }

            if (_socketToExcelManagerMap.TryGetValue(tcpSocket, out ExcelManager oldManager))
            {
                ExcelManagerDisconnectedEvent?.Invoke(oldManager);
            }

            _log.Info($"{nameof(OnClientDisconnected)} -> Client disconnected. " +
                      $"Address: {tcpSocket.RemoteEndPoint.Address}, " +
                      $"Port: {tcpSocket.RemoteEndPoint.Port}, " +
                      $"Connect time: {tcpSocket.ConnectTime}, " +
                      $"Disconnect time: {tcpSocket.DisconnectTime}");
        }

        private void OnMessageHandler(Message message, TcpSocket tcpSocket)
        {
            try
            {
                switch (message.Template.TemplateType)
                {
                    case TemplateType.DomRegister:
                        DomRegister domRegister = MessageFactory.DecodeDomRegisterMessage(message);
                        HandleDomRegisterMessage(domRegister, tcpSocket);
                        break;
                    case TemplateType.DomMessage:
                        DomMessage domMessage = MessageFactory.DecodeDomMessageMessage(message);
                        HandleDomMessageMessage(domMessage, tcpSocket);
                        break;
                    case TemplateType.DomSetupUpdate:
                        DomSetupUpdate domSetupUpdate = MessageFactory.DecodeDomSetupUpdateMessage(message);
                        HandleDomSetupUpdateMessage(domSetupUpdate, tcpSocket);
                        break;
                    case TemplateType.DomListUpdate:
                        DomListUpdate domListUpdate = MessageFactory.DecodeDomListUpdateMessage(message);
                        HandleDomListUpdateMessage(domListUpdate, tcpSocket);
                        break;
                    case TemplateType.DomStateUpdate:
                        DomStateUpdate domStateUpdate = MessageFactory.DecodeDomStateUpdateMessage(message);
                        HandleDomStateUpdateMessage(domStateUpdate, tcpSocket);
                        break;
                    case TemplateType.PlaySoundRequest:
                        PlaySoundRequest playSoundRequest = MessageFactory.DecodePlaySoundRequestMessage(message);
                        HandlePlaySoundRequestMessage(playSoundRequest, tcpSocket);
                        break;
                    case TemplateType.VisualNotificationRequest:
                        VisualNotificationRequest visualNotificationRequest = MessageFactory.DecodeVisualNotificationRequestMessage(message);
                        HandleVisualNotificationRequest(visualNotificationRequest, tcpSocket);
                        break;
                    case TemplateType.OpenTicketRequest:
                        OpenTicketRequest openTicketRequest = MessageFactory.DecodeOpenTicketRequestMessage(message);
                        HandleOpenTicketRequest(openTicketRequest, tcpSocket);
                        break;
                    case TemplateType.OpenBasketRequest:
                        OpenBasketRequest openBasketRequest = MessageFactory.DecodeOpenBasketRequestMessage(message);
                        HandleOpenBasketRequest(openBasketRequest, tcpSocket);
                        break;
                    case TemplateType.CloseTicketRequest:
                        CloseTicketRequest closeTicketRequest = MessageFactory.DecodeCloseTicketRequestMessage(message);
                        HandleCloseTicketRequest(closeTicketRequest, tcpSocket);
                        break;
                    case TemplateType.DomCommand:
                        DomCommand domCommand = MessageFactory.DecodeDomCommandMessage(message);
                        HandleDomCommand(domCommand, tcpSocket);
                        break;
                    case TemplateType.OpenChartRequest:
                        OpenChartRequest openChartRequest = MessageFactory.DecodeOpenChartRequestMessage(message);
                        HandleOpenChartRequest(openChartRequest, tcpSocket);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnMessageHandler));
            }
        }

        private void HandleDomRegisterMessage(DomRegister domRegister, TcpSocket tcpSocket)
        {
            if (domRegister.Source == "ZeroPlus.Excel.Manager")
            {
                ExcelManager excelManager = new()
                {
                    Socket = tcpSocket,
                    Host = domRegister.Host,
                };

                if (_socketToExcelManagerMap.TryGetValue(tcpSocket, out ExcelManager oldManager))
                {
                    ExcelManagerDisconnectedEvent?.Invoke(oldManager);
                }

                _socketToExcelManagerMap[tcpSocket] = excelManager;
                ExcelManagerConnectedEvent?.Invoke(excelManager);
            }
            else
            {
                Dominator dominator = new(OmsCore)
                {
                    Username = domRegister.Username,
                    Source = domRegister.Source,
                    Socket = tcpSocket,
                    State = DomState.Stopped,
                    Host = domRegister.Host,
                    FullAutoSetups = domRegister.FullAutoSetups,
                    DominatorSetups = new List<string>(),
                    CustomSetups = new Dictionary<string, List<string>>(),
                };

                foreach (string setup in domRegister.DominatorSetups)
                {
                    if (!string.IsNullOrWhiteSpace(setup))
                    {
                        if (setup.Contains(':'))
                        {
                            string[] parts = setup.Split(':');
                            if (parts.Length == 1)
                            {
                                dominator.DominatorSetups.Add(parts[0]);
                            }
                            else if (parts.Length > 1)
                            {
                                string title = parts[0];
                                string setupName = parts[1];
                                if (!string.IsNullOrWhiteSpace(setupName))
                                {
                                    if (string.IsNullOrWhiteSpace(title) ||
                                        title.Equals("dominator", StringComparison.OrdinalIgnoreCase))
                                    {
                                        dominator.DominatorSetups.Add(setupName);
                                    }
                                    else
                                    {
                                        if (!dominator.CustomSetups.TryGetValue(title, out List<string> customSetup))
                                        {
                                            customSetup = new List<string>();
                                            dominator.CustomSetups[title] = customSetup;
                                        }
                                        customSetup.Add(setupName);
                                    }
                                }
                            }
                        }
                        else
                        {
                            dominator.DominatorSetups.Add(setup);
                        }
                    }
                }

                if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator oldDom))
                {
                    oldDom.State = DomState.Stopped;
                    DominatorDisconnectedEvent?.Invoke(oldDom);
                }

                _socketToDomMap[tcpSocket] = dominator;
                DominatorUpdatedEvent?.Invoke(dominator);
            }
        }

        private void HandleDomMessageMessage(DomMessage domMessage, TcpSocket tcpSocket)
        {
            DominatorMessageEvent?.Invoke(domMessage.Message, domMessage.Title, domMessage.Silent);
        }

        private void HandleDomSetupUpdateMessage(DomSetupUpdate domSetupUpdate, TcpSocket tcpSocket)
        {
            if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
            {
                dominator.Setup = domSetupUpdate.Setup;
                dominator.Configs = domSetupUpdate.List;
                DominatorUpdatedEvent?.Invoke(dominator);
            }
        }

        private void HandleDomListUpdateMessage(DomListUpdate domListUpdate, TcpSocket tcpSocket)
        {
            if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
            {
                dominator.Product = domListUpdate.Product;
                dominator.Type = domListUpdate.Type;
                dominator.ListDate = domListUpdate.ListDate;
                dominator.ListCreator = domListUpdate.ListCreator;
                dominator.FullName = domListUpdate.FullName;
                dominator.SubName = domListUpdate.SubName;
                dominator.ListCount = domListUpdate.ListCount;
                DominatorUpdatedEvent?.Invoke(dominator);
            }
        }

        private void HandleDomStateUpdateMessage(DomStateUpdate domStateUpdate, TcpSocket tcpSocket)
        {
            if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
            {
                dominator.State = domStateUpdate.State;
                dominator.DomCount = domStateUpdate.DomCount;
                dominator.Fills = domStateUpdate.Fills;
                dominator.EdgeMultiplier = domStateUpdate.EdgeMultiplier;
                dominator.DeltaMax = domStateUpdate.DeltaMax;
                dominator.LoopSize = domStateUpdate.LoopSize;
                dominator.CalendarEdge = domStateUpdate.CalendarEdge;
                dominator.DeltaEdge = domStateUpdate.DeltaEdge;
                dominator.UniqueSubmissionsOn = domStateUpdate.UniqeSpreadsOn;
                DominatorUpdatedEvent?.Invoke(dominator);
            }
        }

        private void HandlePlaySoundRequestMessage(PlaySoundRequest playSoundRequest, TcpSocket tcpSocket)
        {
            try
            {
                Task.Run(() => DominatorPlaySoundRequestEvent?.Invoke(playSoundRequest.Id, playSoundRequest.Name));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandlePlaySoundRequestMessage));
            }
        }

        private void HandleVisualNotificationRequest(VisualNotificationRequest visualNotificationRequest, TcpSocket tcpSocket)
        {
            if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
            {
                dominator.ShowNotification = true;
                dominator.NotificationTimeout = visualNotificationRequest.Timeout;
                Task.Run(() => DominatorUpdatedEvent?.Invoke(dominator));
            }
        }

        private void HandleOpenTicketRequest(OpenTicketRequest openTicketRequest, TcpSocket tcpSocket)
        {
            try
            {
                if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
                {
                    Task.Run(() => OpenTicketRequestEvent?.Invoke(openTicketRequest, dominator));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOpenTicketRequest));
            }
        }

        private void HandleOpenChartRequest(OpenChartRequest openChartRequest, TcpSocket tcpSocket)
        {
            try
            {
                Task.Run(() => OpenChartRequestEvent?.Invoke(openChartRequest));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOpenChartRequest));
            }
        }

        private void HandleOpenBasketRequest(OpenBasketRequest openBasketRequest, TcpSocket tcpSocket)
        {
            try
            {
                Task.Run(() => OpenBasketRequestEvent?.Invoke(openBasketRequest));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOpenBasketRequest));
            }
        }

        private void HandleCloseTicketRequest(CloseTicketRequest closeTicketRequest, TcpSocket tcpSocket)
        {
            try
            {
                Task.Run(() => CloseTicketRequestEvent?.Invoke(closeTicketRequest));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleCloseTicketRequest));
            }
        }

        private void HandleDomCommand(DomCommand domCommand, TcpSocket tcpSocket)
        {
            try
            {
                List<Dominator> dominators = _socketToDomMap.Values.ToList();
                if (domCommand.Argument.StartsWith("Setup:") && (domCommand.Command == Command.Start || domCommand.Command == Command.Stop))
                {
                    string setups = domCommand.Argument.Replace("Setup:", "");
                    string[] args = setups.Split(",");
                    foreach (string arg in args)
                    {
                        string keyword = arg.Trim();
                        IEnumerable<Dominator> doms = dominators.Where(x => x.Setup.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                        switch (domCommand.Command)
                        {
                            case Command.Stop:
                                StopDoms(domCommand, tcpSocket, doms);
                                break;
                            case Command.Start:
                                StartDoms(domCommand, tcpSocket, doms);
                                break;
                        }
                    }
                }
                else if (domCommand.Command is Command.Start or Command.Stop)
                {
                    string[] args = domCommand.Argument.Replace(",", " ").Split(" ");
                    foreach (string arg in args)
                    {
                        string keyword = arg.Trim();
                        IEnumerable<Dominator> doms = dominators.Where(x => x.Username.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                        switch (domCommand.Command)
                        {
                            case Command.Stop:
                                StopDoms(domCommand, tcpSocket, doms);
                                break;
                            case Command.Start:
                                StartDoms(domCommand, tcpSocket, doms);
                                break;
                        }
                    }
                }
                else if (domCommand.Command == Command.DomAutoTraderInfo)
                {
                    if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
                    {
                        HandleDomAutoTraderCommand(dominator, domCommand.Argument);
                    }
                }
                else if (domCommand.Command == Command.StatusUpdate)
                {
                    if (_socketToDomMap.TryGetValue(tcpSocket, out Dominator dominator))
                    {
                        dominator.ParseStatusUpdate(domCommand.Argument);
                    }
                }
                else if (domCommand.Command == Command.NewDomEdgeCalc)
                {
                    DominatorTraderModel.ProcessEdgeUpdate(domCommand.Argument);
                }
                else if (domCommand.Command == Command.NewDomSpreadFilter)
                {
                    DominatorTraderModel.ProcessFilterUpdate(domCommand.Argument);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleDomCommand));
            }
        }
        /// <summary>
        /// If the symbol is not being fished send to autotrader and remove from map on close
        /// if the symbol is being fished, try again after dom autotrader finishes current symbol
        /// </summary>
        private void HandleDomAutoTraderCommand(Dominator dominator, string argumentsJson)
        {
            try
            {
                dynamic argument = Newtonsoft.Json.JsonConvert.DeserializeObject(argumentsJson);
                Guid orderId = argument.Id;
                string symbol = argument.Symbol;
                Side side = argument.Side;
                long price = argument.Price;
                long underPrice = argument.UnderPrice;
                Guid? cancelSendParent = argument.CancelSendParentId;

                AutoTraderSender(dominator, orderId, symbol, side, price, underPrice, cancelSendParent ?? default);
                _log.Info(argumentsJson);
            }
            catch
            {
                _log.Error("Failed to parse and send autotrader orders");
            }
        }

        private void AutoTraderSender(
            Dominator dominator,
            Guid orderId,
            string symbol,
            Side side,
            long price,
            long underPrice,
            Guid DomContraParentId)
        {
            bool isDomContra = DomContraParentId != default;
            int quantity = 1;
            // requests all options for the symbol to get the MinimumTickStyle
            MinimumTickStyle minimumTickStyle = GetMinimumTickStyle(symbol, OmsCore.QuoteClient, OmsCore.SecurityBook).Result;
            dominator.SendAutoTraderOrder(orderId, symbol, side, price, underPrice, isDomContra, quantity, minimumTickStyle, OmsCore.AutoTraderClient);
            _log.Info("DOM Autotrader Main: order sent");
        }

        private static async Task<MinimumTickStyle> GetMinimumTickStyle(string symbol, Clients.QuoteClient quoteClient, ISecurityBook securityBook)
        {
            try
            {
                string underlyingTicker = new SymbolLib.SymbolCodec(symbol).UnderlyingSymbol();
                Security underlyingSecurity = securityBook.GetSecurity(underlyingTicker);
                List<Data.Securities.Option> options = await quoteClient.GetSymbolsAsync(symbol);
                Data.Securities.Option option = options.First();
                return (MinimumTickStyle)option.TickType;
            }
            catch { return MinimumTickStyle.AllPenny; }
        }

#if DEBUG
        public void SendTestOrder(Dominator dominator)
        {
            dominator.SendAutoTraderOrder(Guid.NewGuid(), ".SPY250801C630", Side.Buy, 600, 622_90, false, 1, Models.Data.Enums.MinimumTickStyle.AllPenny, OmsCore.AutoTraderClient);
            _log.Info("Test Order Send is {0}", true);
        }
#endif
        private void StartDoms(DomCommand domCommand, TcpSocket tcpSocket, IEnumerable<Dominator> doms)
        {
            try
            {
                if (_stopRequestToStoppedDomsMap.TryGetValue(tcpSocket, out HashSet<Dominator> list))
                {
                    foreach (Dominator dom in doms)
                    {
                        if (list.Contains(dom))
                        {
                            dom.SendDomCommand(domCommand);
                            list.Remove(dom);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StartDoms));
            }
        }

        private static void StopDoms(DomCommand domCommand, TcpSocket tcpSocket, IEnumerable<Dominator> doms)
        {
            try
            {
                if (!_stopRequestToStoppedDomsMap.TryGetValue(tcpSocket, out HashSet<Dominator> list))
                {
                    list = new HashSet<Dominator>();
                    _stopRequestToStoppedDomsMap[tcpSocket] = list;
                }
                foreach (Dominator dom in doms)
                {
                    dom.SendDomCommand(domCommand);
                    list.Add(dom);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(StopDoms));
            }
        }
    }
}
