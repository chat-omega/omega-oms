using Middleware.Communication.Tcp;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.BasketManager;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Managers
{
    public delegate void ServerStatusChangedEventHandler(bool serverUp);
    public delegate void BasketUpdatedEventHandler(IBasket basket);
    public delegate void BasketTradeUpdateEventHandler(BasketTradeUpdate basketTradeUpdate);
    public delegate void MessageEventHandler(string message, string title, bool silent);

    public class BasketManager
    {
        public event ServerStatusChangedEventHandler ServerStatusChangedEvent;
        public event BasketUpdatedEventHandler BasketUpdatedEvent;
        public event BasketUpdatedEventHandler BasketDisconnectedEvent;
        public event OpenTicketRequestEventHandler OpenTicketRequestEvent;
        public event BasketTradeUpdateEventHandler BasketTradeUpdateEvent;
        public event MessageEventHandler MessageEvent;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly OmsConfig _config;
        private TcpServer _tcpServer;
        private readonly ConcurrentDictionary<string, IBasket> _idToBasketMap = new();

        public bool Listening { get; set; }

        public BasketManager(OmsConfig config)
        {
            _config = config;
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
            IPAddress address = IPAddress.Parse(_config.BasketManagerListenerAddress);
            IPEndPoint localEndPoint = new(address, _config.BasketManagerListenerPort);

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
            _idToBasketMap.Clear();
            _log.Info($"{nameof(OnTcpServerStarted)} -> Server started. " +
                      $"Address: {tcpServer.EndPoint.Address}, " +
                      $"Port: {tcpServer.EndPoint.Port}");
        }

        public void DispatchMessage(string message, string title)
        {
            MessageEvent?.Invoke(message, title, true);
        }

        private void OnTcpServerStopped(TcpServer tcpServer)
        {
            Listening = false;
            ServerStatusChangedEvent?.Invoke(false);
            _idToBasketMap.Clear();
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
                    case TemplateType.BasketUpdate:
                        BasketUpdate basketUpdate = MessageFactory.DecodeBasketUpdateMessage(message);
                        HandleBasketUpdate(basketUpdate, tcpSocket);
                        break;
                    case TemplateType.BasketTradeUpdate:
                        BasketTradeUpdate basketTradeUpdate = MessageFactory.DecodeBasketTradeUpdateMessage(message);
                        HandleBasketTradeUpdate(basketTradeUpdate, tcpSocket);
                        break;
                    case TemplateType.BasketCommand:
                        BasketCommand basketCommand = MessageFactory.DecodeBasketCommandMessage(message);
                        HandleBasketCommand(basketCommand, tcpSocket);
                        break;
                    case TemplateType.OpenTicketRequest:
                        OpenTicketRequest openTicketRequest = MessageFactory.DecodeOpenTicketRequestMessage(message);
                        HandleOpenTicketRequest(openTicketRequest, tcpSocket);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnMessageHandler));
            }
        }

        private void HandleOpenTicketRequest(OpenTicketRequest openTicketRequest, TcpSocket tcpSocket)
        {
            try
            {
                Task.Run(() => OpenTicketRequestEvent?.Invoke(openTicketRequest, null));
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleOpenTicketRequest));
            }
        }

        private void HandleBasketUpdate(BasketUpdate basketUpdate, TcpSocket tcpSocket)
        {
            Basket basket = new()
            {
                Uid = basketUpdate.Uid,
                InstanceId = basketUpdate.InstanceId,
                Username = basketUpdate.Username,
                ModuleTitle = basketUpdate.Title,
                TcpSocket = tcpSocket,
                Host = basketUpdate.Host,
                RowCount = basketUpdate.RowCount,
                Fills = basketUpdate.Fills,
                Edge = basketUpdate.Edge,
                EdgeType = basketUpdate.EdgeType,
                ResubmitCountDown = TimeSpan.FromSeconds(basketUpdate.ResubmitTimer),
                ResubmitIntervalSec = basketUpdate.ResubmitTimerInterval,
                ResubmitOnTimer = basketUpdate.ResubmitTimerOn,
                OpenTicket = basketUpdate.OpenTicketEnabled,
                SampleDescription = basketUpdate.SampleDescription,
                Tag = basketUpdate.Tag,
            };

            _idToBasketMap[basket.Uid] = basket;
            BasketUpdatedEvent?.Invoke(basket);
        }

        private void HandleBasketTradeUpdate(BasketTradeUpdate basketTradeUpdate, TcpSocket tcpSocket)
        {
            BasketTradeUpdateEvent?.Invoke(basketTradeUpdate);
        }

        private void HandleBasketCommand(BasketCommand basketCommand, TcpSocket tcpSocket)
        {
            if (basketCommand.Command == BasketCommands.Disconnected)
            {
                if (_idToBasketMap.TryGetValue(basketCommand.Id, out IBasket basket))
                {
                    BasketDisconnectedEvent?.Invoke(basket);
                }
                return;
            }
        }
    }
}
