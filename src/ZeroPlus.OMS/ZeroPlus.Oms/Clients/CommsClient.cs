using Middleware.Communication.Tcp;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Comms.Helper.Concurrency;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Oms.BasketManager;
using ZeroPlus.Comms.Models.Data.Oms.Common;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Data.Requests;
using ZeroPlus.Comms.Models.Data.Responses;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Comms.Models.Protocols.FAST.Codec;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Exceptions;
using TcpClient = Middleware.Communication.Tcp.TcpClient;

namespace ZeroPlus.Oms.Clients
{
    public class CommsClient
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;

        private readonly OmsConfig _config;
        private readonly MessageHandler.HandleMessageHandler _handleMessageDelegate;
        private readonly bool _register;
        private readonly BufferType _bufferType;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private MessageHandler _messageHandler;

        private ClientInfo _clientInfo;
        private TcpClient _tcpClient;

        private bool _isConnected = false;
        private readonly ManualResetEventSlim _connectionNotifier = new(false);

        private int _requestID = 0;
        private string _serverAddress;
        private int _serverPort;
        private readonly int _sendOrderTimeout = 15000;
        private readonly int _shortRequestTimeout = 2000;
        private readonly int _requestTimeout = 15000;
        private readonly int _longRequestTimeout = 60000;
        private readonly int _veryLongRequestTimeout = 3600000;
        private readonly ConcurrentDictionary<int, AsyncResult<DataRequest>> _requestIdToRequestsMap = new();
        public OmsCore OmsCore { get; }

        public CommsClient(Guid guid, OmsConfig config, MessageHandler.HandleMessageHandler handleMessageDelegate, OmsCore omsCore, bool register = true, BufferType bufferType = BufferType.Read)
        {
            OmsCore = omsCore;
            _config = config;
            _handleMessageDelegate = handleMessageDelegate;
            _register = register;
            _bufferType = bufferType;
            SetConfigUpdateHandler();
            SetCommsClient(guid);
        }

        public void UpdateClientId(Guid guid)
        {
            _clientInfo.SetClientInfo(guid);
        }

        public bool Start(string serverAddress, int serverPort)
        {
            try
            {
                serverAddress = GetIp(serverAddress);
                _serverAddress = serverAddress;
                _serverPort = serverPort;
                _connectionNotifier.Reset();
                _messageHandler = new MessageHandler(_handleMessageDelegate);
                _tcpClient.Connect(serverAddress, serverPort, 0x2000000, 0x800000);
                _connectionNotifier.Wait();
                return _isConnected;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Start));
                return false;
            }
        }

        public static bool IsLocalAddress(string address)
        {
            try
            {
                address = GetIp(address);

                if (IPAddress.TryParse(address, out var ipAddress))
                {
                    if (IPAddress.IsLoopback(ipAddress))
                    {
                        return true;
                    }

                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                        {
                            if (ip.Equals(ipAddress))
                            {
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(IsLocalAddress));
                return false;
            }
        }

        public static string GetIp(string address)
        {
            if (!IPAddress.TryParse(address, out _))
            {
                IPHostEntry hostInfo = Dns.GetHostEntry(address);
                address = hostInfo.AddressList.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            }

            return address;
        }

        public void Stop()
        {
            try
            {
                _messageHandler?.Dispose();
                _tcpClient?.Disconnect();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Stop));
            }
        }

        public void ReStart()
        {
            try
            {
                Stop();
                Start(_serverAddress, _serverPort);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ReStart));
            }
        }

        internal void SendMdsManagerUiRegistrationMsg()
        {
            try
            {
                OMSGetHeartbeat getHbMessage = new()
                {
                    Timestamp = DateTime.Now,
                    StringValue = "test"
                };

                if (_tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSGetHeartbeat(getHbMessage)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendMdsManagerUiRegistrationMsg)} -> Exception Sending {nameof(OMSGetHeartbeat)} message");
            }
        }

        internal void SendSetClientNameMsg()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                OMSSetWorkbookName getHbMessage = new()
                {
                    SendTime = DateTime.Now,
                    WorkbookName = $"ZeroPlus OMS - V:{version?.ToString(3)}",
                };

                User user = OmsCore.User;
                if (user != null)
                {
                    getHbMessage.WorkbookName += $" [{user.Username}]";
                }

                if (_tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSSetWorkbookNameMessage(getHbMessage)));
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendSetClientNameMsg)} -> Exception Sending {nameof(OMSSetWorkbookName)} message");
            }
        }

        internal void SendAllPositionsRequestMessage(bool subscribe)
        {
            List<ZPAccount> accounts = new();
            for (int i = 0; i < 2; i++)
            {
                accounts = GetAccounts();
                if (accounts.Count > 0)
                {
                    _log.Info($"{nameof(SendAllPositionsRequestMessage)} Subscribe: {subscribe} -> Accounts request success on {i + 1} attempt. Count: {accounts.Count}. Accounts: {string.Join(",", accounts.Select(x => x.Acronym))}");
                    break;
                }
                else
                {
                    _log.Error($"{nameof(SendAllPositionsRequestMessage)} Subscribe: {subscribe} -> Accounts request attempt {i + 1} failed.");
                }
            }

            IEnumerable<string> accountNames = accounts.Select(x => x.Acronym?.ToUpper()).Where(x => OmsCore.User.Accounts.Contains(x)).Distinct();
            foreach (string account in accountNames)
            {
                try
                {
                    var (message, timestamp) = subscribe ? CreateSubscribeAllPositionsMessage(account) : CreateUnsubscribeAllPositionsMessage(account);
                    if (_tcpClient is { IsConnected: true })
                    {
                        _tcpClient.SendData(FastEncoder.Encode(message));
                        _log.Info($"{nameof(SendAllPositionsRequestMessage)} Subscribe: {subscribe} -> Account: {account}, Time: {timestamp}.");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(SendAllPositionsRequestMessage) + $"  Subscribe: {subscribe}");
                }
            }
        }

        internal (Message, DateTime) CreateSubscribeAllPositionsMessage(string account)
        {
            OMSSubscribePosition omsSubscribePositionRequest = new()
            {
                AccountAcronym = account,
                Symbol = string.Empty,
                Timestamp = DateTime.Now,
            };

            return (MessageFactory.CreateOMSSubscribePosition(omsSubscribePositionRequest), omsSubscribePositionRequest.Timestamp);
        }

        internal (Message, DateTime) CreateUnsubscribeAllPositionsMessage(string account)
        {
            OMSUnsubscribePosition omsUnsubscribePositionRequest = new()
            {
                AccountAcronym = account,
                Symbol = string.Empty,
                Timestamp = DateTime.Now,
            };

            return (MessageFactory.CreateOMSUnsubscribePosition(omsUnsubscribePositionRequest), omsUnsubscribePositionRequest.Timestamp);
        }

        internal List<ZPAccount> GetAccounts()
        {
            DataRequest dataRequest = BeginGetAccountAndRoutes().EndInvoke(_requestTimeout);
            if (dataRequest is RequestAcctsAndRoutes dataRequestResponse && dataRequestResponse.Response != null)
            {
                return ((RequestAcctsAndRoutesResponse)dataRequestResponse.Response).GetAccounts();
            }
            else
            {
                _log.Error(nameof(GetAccounts) + " Getting accounts failed.");
                return new List<ZPAccount>();
            }
        }

        internal AsyncResult<DataRequest> BeginGetAccountAndRoutes()
        {
            RequestAcctsAndRoutes request = new(true, "AAPL", GetNextRequestId());
            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient is { IsConnected: true })
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal void SendConfigShareMessage(ConfigShare config)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateConfigShareMessage(config)));
            }
        }

        internal void SendBasketUpdateMessage(BasketUpdate basketUpdate)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateBasketUpdateMessage(basketUpdate)));
            }
        }

        internal void SendBasketTradeUpdateMessage(BasketTradeUpdate basketTradeUpdate)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateBasketTradeUpdateMessage(basketTradeUpdate)));
            }
        }

        internal void SendBasketCommandMessage(BasketCommand basketCommand)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateBasketCommandMessage(basketCommand)));
            }
        }

        internal void SendDomRegisterMessage(DomRegister domRegister)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomRegisterMessage(domRegister)));
            }
        }

        internal void SendDomMessageMessage(DomMessage domMessage)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomMessageMessage(domMessage)));
            }
        }

        internal void SendDomSetupUpdateMessage(DomSetupUpdate domSetupUpdate)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomSetupUpdateMessage(domSetupUpdate)));
            }
        }

        internal void SendDomListUpdateMessage(DomListUpdate domListUpdate)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomListUpdateMessage(domListUpdate)));
            }
        }

        internal void SendPlaySoundRequestMessage(PlaySoundRequest playSoundRequest)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreatePlaySoundRequestMessage(playSoundRequest)));
            }
        }

        internal void SendVisualNotificationRequestMessage(VisualNotificationRequest visualNotificationRequest)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateVisualNotificationRequestMessage(visualNotificationRequest)));
            }
        }

        internal void SendOpenIvChartMessage(OpenChartRequest openChartRequest)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOpenChartRequestMessage(openChartRequest)));
            }
        }

        internal void SendOpenTicketRequestMessage(OpenTicketRequest request)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOpenTicketRequestMessage(request)));
            }
        }

        internal void SendCloseTicketRequestMessage(CloseTicketRequest request)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateCloseTicketRequestMessage(request)));
            }
        }

        internal void SendOpenBasketRequestMessage(OpenBasketRequest request)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOpenBasketRequestMessage(request)));
            }
        }

        internal void SendDomStateUpdateMessage(DomStateUpdate domStateUpdate)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomStateUpdateMessage(domStateUpdate)));
            }
        }

        internal void SendConfigSaveMessage(ConfigSave config)
        {
            if (_tcpClient is { IsConnected: true })
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateConfigSaveMessage(config)));
            }
        }

        public LoginResponse SendAuthMessage(string username, SecureString securePassword, string appCode)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            string systemInfo = Environment.MachineName;
            DataRequest dataRequest = BeginSendAuthMessage(username, securePassword, version, appCode, systemInfo).EndInvoke(_longRequestTimeout);
            if (dataRequest is LoginRequest loginRequest && loginRequest.Response != null)
            {
                LoginResponse response = (LoginResponse)loginRequest.Response;
                return response;
            }
            else
            {
                return null;
            }
        }

        private AsyncResult<DataRequest> BeginSendAuthMessage(string username, SecureString securePassword, string version, string appCode, string systemInfo)
        {
            LoginRequest loginRequest = new()
            {
                RequestID = GetNextRequestId(),
                Username = username,
                Password = OmsCore.CalculateHash(securePassword, ""),
                Version = version,
                AppCode = appCode,
                IsReauth = false,
                SystemInformation = systemInfo
            };

            AsyncResult<DataRequest> asyncResult = new(null, loginRequest);

            if (_tcpClient is { IsConnected: true })
            {
                _requestIdToRequestsMap[loginRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(loginRequest)));
            }

            return asyncResult;
        }

        public LoginResponse SendAuthMessage(string username, string authCode)
        {
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
            string systemInfo = Environment.MachineName;
            DataRequest dataRequest = BeginSendAuthMessage(username, authCode, version, systemInfo).EndInvoke(_requestTimeout);
            if (dataRequest is LoginRequest loginRequest && loginRequest.Response != null)
            {
                LoginResponse response = (LoginResponse)loginRequest.Response;
                return response;
            }
            else
            {
                return null;
            }
        }

        private AsyncResult<DataRequest> BeginSendAuthMessage(string username, string authCode, string version, string systemInfo)
        {
            LoginRequest loginRequest = new()
            {
                RequestID = GetNextRequestId(),
                IsReauth = true,
                Username = username,
                AuthCode = authCode,
                Version = version,
                SystemInformation = systemInfo
            };

            AsyncResult<DataRequest> asyncResult = new(null, loginRequest);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[loginRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(loginRequest)));
            }

            return asyncResult;
        }

        internal string SendConfigDeleteMessage(int id)
        {
            DataRequest dataRequest = BeginSendConfigDeleteMessage(id).EndInvoke(_requestTimeout);
            if (dataRequest is DeleteConfigRequest request && request.Response != null)
            {
                DeleteConfigResponse response = (DeleteConfigResponse)request.Response;
                return response.Message;
            }
            else
            {
                return "Request timed out.";
            }
        }

        public AsyncResult<DataRequest> BeginSendConfigDeleteMessage(int id)
        {
            DeleteConfigRequest request = new()
            {
                RequestID = GetNextRequestId(),
                ConfigId = id,
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal List<User> SendRequestUsersMessage()
        {
            DataRequest dataRequest = BeginSendRequestUsersMessage().EndInvoke(_longRequestTimeout);
            if (dataRequest is GetUsersRequest request && request.Response != null)
            {
                GetUsersResponse response = (GetUsersResponse)request.Response;
                return response.Users;
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestUsersMessage()
        {
            GetUsersRequest request = new()
            {
                RequestID = GetNextRequestId(),
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal List<DomListInfo> SendRequestDomListInfosMessage()
        {
            DataRequest dataRequest = BeginSendRequestDomListInfosMessage().EndInvoke(_longRequestTimeout);
            if (dataRequest is GetDomListInfosRequest request && request.Response != null)
            {
                GetDomListInfosResponse response = (GetDomListInfosResponse)request.Response;
                return response.DomListInfos;
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestDomListInfosMessage()
        {
            GetDomListInfosRequest request = new()
            {
                RequestID = GetNextRequestId(),
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal GetCommissionsResponse SendGetCommissionsMessage()
        {
            DataRequest dataRequest = BeginSendGetCommissionsMessage().EndInvoke(_longRequestTimeout);
            if (dataRequest is GetCommissionsRequest request && request.Response != null)
            {
                GetCommissionsResponse response = (GetCommissionsResponse)request.Response;
                return response;
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendGetCommissionsMessage()
        {
            GetCommissionsRequest request = new()
            {
                RequestID = GetNextRequestId(),
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        public bool SendRequestPasswordChangeMessage(SecureString currentPassword, SecureString newPassword)
        {
            DataRequest dataRequest = BeginSendRequestPasswordChangeMessage(currentPassword, newPassword).EndInvoke(_requestTimeout);
            if (dataRequest is UpdatePasswordRequest updatePasswordRequest && updatePasswordRequest.Response != null)
            {
                UpdatePasswordResponse response = (UpdatePasswordResponse)updatePasswordRequest.Response;

                if (response.IsSuccess)
                {
                    return true;
                }
                else
                {
                    throw new SlimException(response.Comment);
                }
            }
            else
            {
                return false;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestPasswordChangeMessage(SecureString currentPassword, SecureString newPassword)
        {
            UpdatePasswordRequest updatePasswordRequest = new()
            {
                RequestID = GetNextRequestId(),
                OldPassword = OmsCore.CalculateHash(currentPassword, ""),
                NewPassword = OmsCore.CalculateHash(newPassword, ""),
            };

            AsyncResult<DataRequest> asyncResult = new(null, updatePasswordRequest);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[updatePasswordRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(updatePasswordRequest)));
            }

            return asyncResult;
        }

        public string SendRequestPnlReportMessage(string format, DateTime start, DateTime end, List<string> usernames, List<string> tags, List<string> symbols, List<string> underlyings)
        {
            DataRequest dataRequest = BeginSendRequestPnlReportMessage(format, start, end, usernames, tags, symbols, underlyings).EndInvoke(_longRequestTimeout);
            if (dataRequest is PnlReportRequest request && request.Response != null)
            {
                PnlReportResponse response = (PnlReportResponse)request.Response;
                return Encoding.ASCII.GetString(response.ReportDocument);
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestPnlReportMessage(string format, DateTime start, DateTime end, List<string> usernames, List<string> tags, List<string> symbols, List<string> underlyings)
        {
            PnlReportRequest pnlReportRequest = new()
            {
                RequestID = GetNextRequestId(),
                Format = format,
                Start = start,
                End = end,
                ApiUsernames = usernames,
                Tags = tags,
                Symbols = symbols,
                Underlyings = underlyings,
            };

            AsyncResult<DataRequest> asyncResult = new(null, pnlReportRequest);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[pnlReportRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(pnlReportRequest)));
            }

            return asyncResult;
        }

        public List<OptionSnapshot> SendRequestOptionSnapshotsMessage(string symbol, DateTime expiration, double delta, DateTime start, DateTime end)
        {
            DataRequest dataRequest = BeginSendRequestOptionSnapshotsMessage(symbol, expiration, delta, start, end).EndInvoke(_veryLongRequestTimeout);
            if (dataRequest is GetOptionSnapshotsRequest request && request.Response != null)
            {
                GetOptionSnapshotsResponse response = (GetOptionSnapshotsResponse)request.Response;
                return response.Snapshots.ToList();
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestOptionSnapshotsMessage(string symbol, DateTime expiration, double delta, DateTime start, DateTime end)
        {
            GetOptionSnapshotsRequest request = new()
            {
                RequestID = GetNextRequestId(),
                Symbol = symbol,
                Expiration = expiration,
                Delta = delta,
                Start = start,
                End = end,
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal List<ConfigSave> SendRequestConfigsMessage(int module)
        {

            DataRequest dataRequest = BeginSendRequestConfigsMessage(module).EndInvoke(_requestTimeout);
            if (dataRequest is GetConfigsRequest request && request.Response != null)
            {
                GetConfigsResponse response = (GetConfigsResponse)request.Response;
                return response.Configs;
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestConfigsMessage(int moduleId)
        {
            GetConfigsRequest request = new()
            {
                RequestID = GetNextRequestId(),
                ModuleId = moduleId,
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal ConfigSave SendRequestConfigDataAsyncMessage(int id)
        {
            DataRequest dataRequest = BeginSendRequestConfigDataAsyncMessage(id).EndInvoke(_requestTimeout);
            if (dataRequest is GetConfigDataRequest request && request.Response != null)
            {
                GetConfigDataResponse response = (GetConfigDataResponse)request.Response;
                return response.Config;
            }
            else
            {
                return null;
            }
        }

        public AsyncResult<DataRequest> BeginSendRequestConfigDataAsyncMessage(int id)
        {
            GetConfigDataRequest request = new()
            {
                RequestID = GetNextRequestId(),
                Id = id,
            };

            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        internal void SubscribeServerCreep()
        {
            try
            {
                MDSubscribeAlerts subscribeAlertRequest = new()
                {
                    Type = Comms.Models.Data.MarketData.AlertType.MDTronCreep,
                    SendTime = DateTime.Now
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeAlertsMessage(subscribeAlertRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug(nameof(SubscribeServerCreep));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeServerCreep));
            }
        }

        internal void SubscribeServerTimeUpdate()
        {
            try
            {
                MDSubscribeAlerts subscribeAlertRequest = new()
                {
                    Type = Comms.Models.Data.MarketData.AlertType.MDTronClock,
                    SendTime = DateTime.Now,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeAlertsMessage(subscribeAlertRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug(nameof(SubscribeServerCreep));
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeServerTimeUpdate));
            }
        }

        public void SubscribeSilexxMarketData(string symbol, SubscriptionFieldType type)
        {
            try
            {
                MDSubscribeMarketData subscribeMarketDataRequest = new()
                {
                    RequestType = (short)type,
                    Symbol = symbol,
                    SendTime = DateTime.Now
                };

                if (type > SubscriptionFieldType.Trade)
                {
                    return;
                }

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeMarketDataMessage(subscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribeSilexxMarketData)} -> SubscribeRequest Symbol: {subscribeMarketDataRequest.Symbol}, Type: {type}, Time: {subscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscribeSilexxMarketData)} -> Exception subscribing to data.");
            }
        }

        public void UnsubscribeSilexxMarketData(string symbol, SubscriptionFieldType type)
        {
            try
            {
                MDUnsubscribeMarketData unsubscribeMarketDataRequest = new()
                {
                    RequestType = (short)type,
                    Symbol = symbol,
                    SendTime = DateTime.Now,
                };

                if (type > SubscriptionFieldType.Trade)
                {
                    return;
                }

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDUnsubscribeMarketDataMessage(unsubscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribeSilexxMarketData)} -> UnsubscribeRequest Symbol: {unsubscribeMarketDataRequest.Symbol}, Type: {type}, Time: {unsubscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeSilexxMarketData)} -> Exception unsubscribing to data.");
            }
        }

        internal void SubscribeSilexxDepth(string symbol)
        {
            try
            {
                MDSubscribeDepth subscribeMarketDataRequest = new()
                {
                    Symbol = symbol,
                    SendTime = DateTime.Now
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeDepthMessage(subscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribeSilexxDepth)} -> SubscribeRequest Symbol: {subscribeMarketDataRequest.Symbol}, Type: Depth, Time: {subscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscribeSilexxDepth)} -> Exception subscribing to data.");
            }
        }

        public void UnsubscribeSilexxDepth(string symbol)
        {
            try
            {
                MDUnsubscribeDepth unsubscribeMarketDataRequest = new()
                {
                    Symbol = symbol,
                    SendTime = DateTime.Now,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDUnsubscribeDepthMessage(unsubscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribeSilexxDepth)} -> UnsubscribeRequest Symbol: {unsubscribeMarketDataRequest.Symbol}, Type: Depth, Time: {unsubscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeSilexxDepth)} -> Exception unsubscribing to data.");
            }
        }

        public void SubscribeTronMarketData(string symbol, SubscriptionFieldType type, MdsDataSource mdsDataSource = MdsDataSource.None)
        {
            try
            {
                MDSubscribeDmitryMarketData subscribeMarketDataRequest = new()
                {
                    Symbol = symbol,
                    RequestType = (short)type,
                    Source = (byte)mdsDataSource,
                    SendTime = DateTime.Now
                };

                if (type > SubscriptionFieldType.Trade)
                {
                    return;
                }

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeDmitryMarketDataMessage(subscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribeTronMarketData)} -> SubscribeRequest Symbol: {subscribeMarketDataRequest.Symbol}, Type: {type}, Time: {subscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeTronMarketData));
            }
        }

        public void UnsubscribeTronMarketData(string symbol, SubscriptionFieldType type, MdsDataSource source = MdsDataSource.None)
        {
            try
            {
                MDUnsubscribeDmitryMarketData unsubscribeMarketDataRequest = new()
                {
                    Symbol = symbol,
                    Source = (byte)source,
                    RequestType = (short)type,
                    SendTime = DateTime.Now,
                };

                if (type > SubscriptionFieldType.Trade)
                {
                    return;
                }

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDUnsubscribeDmitryMarketDataMessage(unsubscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribeTronMarketData)} -> UnsubscribeRequest Symbol: {unsubscribeMarketDataRequest.Symbol}, Type: {type}, Time: {unsubscribeMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeTronMarketData));
            }
        }

        public void ThrottleMarketData(bool isPerformanceMode)
        {
            try
            {
                MDThrottleDmitryMarketData throttleMarketDataRequest = new()
                {
                    ThrottleMs = isPerformanceMode ? _config.PerformanceModeMarketDataThrottleMs : 0,
                    SendTime = DateTime.Now
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDThrottleDmitryMarketDataMessage(throttleMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(ThrottleMarketData)} -> ThrottleSeconds: {throttleMarketDataRequest.ThrottleMs}, Time: {throttleMarketDataRequest.SendTime}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ThrottleMarketData));
            }
        }

        public void SubscribeHanweckData(string symbol, SubscriptionFieldType type)
        {
            try
            {
                HWSubscribeMarketData hwSubscribeMarketDataRequest = new()
                {
                    RequestType = (short)type,
                    Symbol = symbol,
                    SendTime = DateTime.Now,
                    SourceType = hanweckSourceType.vbin_risk,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateHWSubscribeMarketDataMessage(hwSubscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribeHanweckData)} -> Symbol: {hwSubscribeMarketDataRequest.Symbol}, Type: {type}, Time: {hwSubscribeMarketDataRequest.SendTime}, Source: {hwSubscribeMarketDataRequest.SourceType}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeHanweckData));
            }
        }

        public void UnsubscribeHanweckData(string symbol, SubscriptionFieldType type)
        {
            try
            {
                HWUnsubscribeMarketData hwUnsubscribeMarketDataRequest = new()
                {
                    RequestType = (short)type,
                    Symbol = symbol,
                    SendTime = DateTime.Now,
                    SourceType = hanweckSourceType.vbin_risk,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateHWUnsubscribeMarketDataMessage(hwUnsubscribeMarketDataRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribeHanweckData)} -> Symbol: {hwUnsubscribeMarketDataRequest.Symbol}, Type: {type}, Time: {hwUnsubscribeMarketDataRequest.SendTime}, Source: {hwUnsubscribeMarketDataRequest.SourceType}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeHanweckData));
            }
        }

        internal void SubscribeEmaData(string symbol, string underlying, string captureUnderlying, double smoothing, int interval, int period, int type)
        {
            try
            {
                EMAAddToCapture captureRequest = new()
                {
                    Symbol = symbol,
                    UnderlyingSymbol = captureUnderlying,
                    InputThreshold = 9999,
                    OutputThreshold = 9999,
                    IntervalMS = interval,
                    N = period,
                    Type = type,
                    Smoothing = smoothing
                };

                EMASubscribeEMAData emaRequest = new()
                {
                    Symbol = symbol,
                    Type = type,
                    UnderSymbol = underlying,
                    IntervalMS = interval
                };

                if (_tcpClient is { IsConnected: true })
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateEMAAddToCaptureMessage(captureRequest)));
                    Task.Delay(100).ContinueWith(t => _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateEMASubscribeEMADataMessage(emaRequest))));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribeEmaData)} -> Symbol: {emaRequest.Symbol}, Type: {emaRequest.Type}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribeHanweckData));
            }
        }

        public void UnsubscribeEmaData(string symbol, string underlying, int interval, int type)
        {
            try
            {
                EMAUnsubscribeEMAData emaUnsubscribeRequest = new()
                {
                    Symbol = symbol,
                    Type = type,
                    UnderSymbol = underlying,
                    IntervalMS = interval
                };

                if (_tcpClient is { IsConnected: true })
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateEMAUnsubscribeEMADataMessage(emaUnsubscribeRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribeEmaData)} -> Symbol: {emaUnsubscribeRequest.Symbol}, Type: {emaUnsubscribeRequest.Type}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribeEmaData));
            }
        }

        internal void SubscribePosition(string symbol, string account)
        {
            try
            {
                OMSSubscribePosition omsSubscribePositionRequest = new()
                {
                    AccountAcronym = account,
                    Symbol = symbol,
                    Timestamp = DateTime.Now,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSSubscribePosition(omsSubscribePositionRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(SubscribePosition)} -> Symbol: {omsSubscribePositionRequest.Symbol}, Account: {omsSubscribePositionRequest.AccountAcronym}, Time: {omsSubscribePositionRequest.Timestamp}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribePosition));
            }
        }

        internal void UnsubscribePosition(string symbol, string account)
        {
            try
            {
                OMSUnsubscribePosition omsUnsubscribePositionRequest = new()
                {
                    AccountAcronym = account,
                    Symbol = symbol,
                    Timestamp = DateTime.Now,
                };

                if (_tcpClient != null && _tcpClient.IsConnected)
                {
                    _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSUnsubscribePosition(omsUnsubscribePositionRequest)));
                    if (_log.IsDebugEnabled)
                    {
                        _log.Debug($"{nameof(UnsubscribePosition)} -> Symbol: {omsUnsubscribePositionRequest.Symbol}, Account: {omsUnsubscribePositionRequest.AccountAcronym}, Time: {omsUnsubscribePositionRequest.Timestamp}.");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UnsubscribePosition));
            }
        }

        public async Task<string> SendOrderAsync(OpsOrderModel omsOrder)
        {
            DataRequest dataRequest = await BeginSendOrder(omsOrder).EndInvokeAsync(_sendOrderTimeout);
            if (dataRequest is SendOrder dataRequestResponse && dataRequestResponse.Response != null)
            {
                SendOrderResponse response = (SendOrderResponse)dataRequestResponse.Response;
                if (response.ErrorCode != 0)
                {
                    throw new SendOrderServerException(response.ErrorMessage)
                    {
                        OmsOrder = omsOrder
                    };
                }
                else
                {
                    return response.InitialOrder;
                }
            }
            else
            {
                throw new SlimException($"Sending order failed for {omsOrder}.");
            }
        }

        public string SendOrder(OpsOrderModel omsOrder)
        {
            DataRequest dataRequest = BeginSendOrder(omsOrder).EndInvoke(_sendOrderTimeout);
            if (dataRequest is SendOrder dataRequestResponse && dataRequestResponse.Response != null)
            {
                SendOrderResponse response = (SendOrderResponse)dataRequestResponse.Response;
                if (response.ErrorCode != 0)
                {
                    throw new SendOrderServerException(response.ErrorMessage)
                    {
                        OmsOrder = omsOrder
                    };
                }
                else
                {
                    return response.InitialOrder;
                }
            }
            else
            {
                throw new SlimException($"Sending order failed for {omsOrder}.");
            }
        }

        public AsyncResult<DataRequest> BeginSendOrder(OpsOrderModel orderInfo)
        {
            SendOrder sendOrderRequest = new(orderInfo)
            {
                RequestID = GetNextRequestId(),
                UserId = OmsCore.User.ID
            };
            AsyncResult<DataRequest> asyncResult = new(null, sendOrderRequest);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[sendOrderRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(sendOrderRequest)));
            }

            return asyncResult;
        }

        public async Task<string> SendOrderFixAsync(OpsOrderModel omsOrder, bool useZpFix)
        {
            var omsOrderExt = MakeFASTOrderMessage(omsOrder, useZpFix);
            DataRequest dataRequest = await BeginSendOrderFix(omsOrderExt).EndInvokeAsync(_sendOrderTimeout);
            if (dataRequest is SendOrderExt dataRequestResponse && dataRequestResponse.Response != null)
            {
                SendOrderResponse response = (SendOrderResponse)dataRequestResponse.Response;
                if (response.ErrorCode != 0)
                {
                    throw new SendOrderServerException(response.ErrorMessage)
                    {
                        OmsOrder = omsOrder
                    };
                }
                else
                {
                    return response.InitialOrder;
                }
            }
            else
            {
                throw new SlimException($"Sending order failed for {omsOrder}.");
            }
        }

        private static OMSOrderExt MakeFASTOrderMessage(OpsOrderModel omsOrder, bool useZpFix = false)
        {
            OMSOrderExt omsOrderExt = new()
            {
                Account = omsOrder.Account,
                Ask = omsOrder.Ask,
                Bid = omsOrder.Bid,
                OMSGetImplied = omsOrder.OMSGetImplied,
                Symbol = omsOrder.Symbol,
                Timestamp = omsOrder.Timestamp,
                Tif = omsOrder.Tif,
                OpenClose = omsOrder.OpenClose,
                Qty = omsOrder.Qty,
                Price = omsOrder.Price,
                Route = omsOrder.Route,
                OMSOrderType = omsOrder.OMSOrderType,
                OMSSide = omsOrder.OMSSide,
                Tag = omsOrder.Tag,
                LocalID = omsOrder.LocalID,
                UnderlyingSymbol = omsOrder.UnderlyingSymbol,
                MinUnderBid = omsOrder.MinUnderBid,
                MaxUnderAsk = omsOrder.MaxUnderAsk,
                CancelDelay = omsOrder.CancelDelay,
                //RequestType = omsOrder.RequestType,
                //StopPrice = omsOrder.StopPrice,
                //PreviousOrderID = omsOrder.PreviousOrderID,
                //FDID = omsOrder.FDID,
                //FDIDAccountHolderType = omsOrder.FDIDAccountHolderType,
                //RepresentativeIndicator = omsOrder.RepresentativeIndicator,
                Venue = useZpFix ? Comms.Models.Data.Venue.ZP : Comms.Models.Data.Venue.TB,
                Legs = omsOrder.Legs,
            };
            return omsOrderExt;
        }

        internal string SendOrderFix(OpsOrderModel omsOrder, Models.Data.Enums.Venue venue)
        {
            bool useZpFix = venue == Models.Data.Enums.Venue.ZpFix;
            OMSOrderExt omsOrderExt = MakeFASTOrderMessage(omsOrder, useZpFix);
            DataRequest dataRequest = BeginSendOrderFix(omsOrderExt).EndInvoke(_sendOrderTimeout);
            if (dataRequest is SendOrderExt dataRequestResponse && dataRequestResponse.Response != null)
            {
                SendOrderResponse response = (SendOrderResponse)dataRequestResponse.Response;
                if (response.ErrorCode != 0)
                {
                    throw new SendOrderServerException(response.ErrorMessage)
                    {
                        OmsOrder = omsOrder
                    };
                }
                else
                {
                    return response.InitialOrder;
                }
            }
            else
            {
                throw new SlimException($"Sending order failed for {omsOrder}.");
            }
        }

        private AsyncResult<DataRequest> BeginSendOrderFix(OMSOrderExt orderInfo)
        {
            SendOrderExt sendOrderRequest = new(orderInfo)
            {
                RequestID = GetNextRequestId(),
                UserId = OmsCore.User.ID
            };
            AsyncResult<DataRequest> asyncResult = new(null, sendOrderRequest);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[sendOrderRequest.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(sendOrderRequest)));
            }

            return asyncResult;
        }

        public void CancelReplaceOrder(string orderId, double price, int quantity)
        {
            OMSOrderCancelReplaceRequest cancelReplaceRequest = new()
            {
                OrderID = orderId,
                Price = price,
                Qty = quantity,
                Timestamp = DateTime.Now,
            };

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSOrderCancelReplaceRequestMessage(cancelReplaceRequest)));
            }
        }

        public void CancelOrder(string orderId)
        {
            OMSOrderCancelRequest cancelRequest = new()
            {
                OrderID = orderId,
                Timestamp = DateTime.Now
            };

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSOrderCancelRequestMessage(cancelRequest)));
            }
        }

        public List<MDUnderlying> GetUnderlyingDetails(string symbol)
        {
            DataRequest dataRequest = BeginGetUnderlyingDetails(symbol).EndInvoke(_requestTimeout);
            if (dataRequest is GetUnderDataRequest dataRequestResponse && dataRequestResponse.Response != null)
            {
                return ((GetUnderDataResponse)dataRequestResponse.Response).Underlyings;
            }
            else
            {
                throw new SlimException($"Getting underlying data failed for {symbol}, request timed out.");
            }
        }

        private AsyncResult<DataRequest> BeginGetUnderlyingDetails(string symbol)
        {
            GetUnderDataRequest request = new();
            request.AddSymbol(symbol);
            request.RequestID = GetNextRequestId();

            return SendRequest(request);
        }

        public List<MDOption> GetSymbols(string symbol)
        {
            DataRequest dataRequest = BeginGetSymbols(symbol).EndInvoke(_requestTimeout);
            if (dataRequest is RequestSymbols dataRequestResponse && dataRequestResponse.Response != null)
            {
                return ((RequestSymbolsResponse)dataRequestResponse.Response).GetSymbols();
            }
            else
            {
                throw new SlimException($"Getting symbols failed for {symbol}, request timed out.");
            }
        }

        private AsyncResult<DataRequest> BeginGetSymbols(string symbol)
        {
            RequestSymbols request = new(symbol, true, GetNextRequestId());

            return SendRequest(request);
        }

        public async Task<List<MDOptionExt>> GetSymbolsExtAsync(string symbol)
        {
            DataRequest dataRequest = await BeginGetSymbolsExt(symbol).EndInvokeAsync(_requestTimeout);
            if (dataRequest is GetSymbolsRequest { Response: not null } dataRequestResponse)
            {
                var symbols = ((GetSymbolsResponse)dataRequestResponse.Response);
                return symbols.GetSymbols();
            }

            return null;
        }

        private AsyncResult<DataRequest> BeginGetSymbolsExt(string symbol)
        {
            GetSymbolsRequest request = new()
            {
                RequestID = GetNextRequestId()
            };
            request.AddSymbol(symbol);
            return SendRequest(request);
        }

        private AsyncResult<DataRequest> SendRequest(DataRequest request)
        {
            AsyncResult<DataRequest> asyncResult = new(null, request);
            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        public List<ZPAccount> GetAccountAndRoutes(string symbol)
        {
            DataRequest dataRequest = BeginGetAccountAndRoutes(symbol).EndInvoke(_requestTimeout);
            if (dataRequest is RequestAcctsAndRoutes dataRequestResponse && dataRequestResponse.Response != null)
            {
                return ((RequestAcctsAndRoutesResponse)dataRequestResponse.Response).GetAccounts();
            }
            else
            {
                throw new SlimException($"Getting accounts failed for {symbol}, request timed out.");
            }
        }

        private AsyncResult<DataRequest> BeginGetAccountAndRoutes(string symbol)
        {
            RequestAcctsAndRoutes request = new(true, symbol, GetNextRequestId());
            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient != null && _tcpClient.IsConnected)
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        public double TryGetQuoteSnapshot(string symbol, SubscriptionFieldType quoteType, QuoteSource source)
        {
            double value = double.NaN;
            DataRequest dataRequest = BeginGetQuoteSnapshot(symbol, quoteType, source).EndInvoke(_shortRequestTimeout);
            if (dataRequest is MarketDataRequest { Response: not null } request)
            {
                MarketDataResponse response = (MarketDataResponse)request.Response;
                if (response.ErrorCode == 0)
                {
                    value = response.DoubleValue;
                    return value;
                }
                else
                {
                    _log.Error(nameof(TryGetQuoteSnapshot) + " Code: " + response.ErrorCode + ", Message: " + response.ErrorMessage);
                    value = double.NaN;
                    return value;
                }
            }
            else
            {
                value = double.NaN;
                return value;
            }
        }

        private AsyncResult<DataRequest> BeginGetQuoteSnapshot(string symbol, SubscriptionFieldType quoteType, QuoteSource source)
        {
            MarketDataRequest request = new()
            {
                Symbol = symbol,
                Type = (short)quoteType,
                SendTime = DateTime.Now,
                Source = (byte)source,
                RequestID = GetNextRequestId()
            };
            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient is { IsConnected: true })
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        public AdjustPositionResponse SendPositionAdjustmentRequest(string account, string symbol, int positionDelta, double openingPrice)
        {
            DataRequest dataRequest = BeginSendPositionAdjustmentRequest(account, symbol, positionDelta, openingPrice).EndInvoke(_requestTimeout);
            if (dataRequest is AdjustPositionRequest dataRequestResponse && dataRequestResponse.Response != null)
            {
                return dataRequestResponse.Response as AdjustPositionResponse;
            }
            else
            {
                throw new SlimException($"Getting accounts failed for {symbol}, request timed out.");
            }
        }

        private AsyncResult<DataRequest> BeginSendPositionAdjustmentRequest(string account, string symbol, int positionDelta, double openingPrice)
        {
            AdjustPositionRequest request = new()
            {
                Account = account,
                Symbol = symbol,
                PositionDelta = positionDelta,
                OpeningPrice = openingPrice,
                RequestID = GetNextRequestId(),
            };
            AsyncResult<DataRequest> asyncResult = new(null, request);

            if (_tcpClient is { IsConnected: true })
            {
                _requestIdToRequestsMap[request.RequestID] = asyncResult;
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(request)));
            }

            return asyncResult;
        }

        private int GetNextRequestId()
        {
            return Interlocked.Increment(ref _requestID);
        }

        private void SetConfigUpdateHandler()
        {
            if (_config != null)
            {
                _config.ConfigChangedEvent -= OnConfig_ConfigChangedEvent;
            }

            _config.ConfigChangedEvent += OnConfig_ConfigChangedEvent;
        }

        private void OnConfig_ConfigChangedEvent(OmsConfig config, bool requiresRestart)
        {
            SetupIntervalsFromConfig();
        }

        private void SetCommsClient(Guid guid)
        {
            SetClientInfo(guid);
            SetTcpClient();
            SetupIntervalsFromConfig();
        }

        private void SetClientInfo(Guid guid)
        {
            try
            {
                _clientInfo = new ClientInfo(_config);
                _clientInfo.SetClientInfo(guid);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SetClientInfo)}");
            }
        }

        private void SetTcpClient()
        {
            try
            {
                if (_tcpClient == null)
                {
                    _tcpClient = new TcpClient(_bufferType)
                    {
                        MessageParser = new TcpFastMessageParser(),
                        ConnectTimeout = 5,
                    };

                    _tcpClient.Connected += OnConnected;
                    _tcpClient.Disconnected += OnConnectionFailedOrDisconnected;
                    _tcpClient.ConnectionFailed += OnConnectionFailedOrDisconnected;
                    (_tcpClient.MessageParser as TcpFastMessageParser).Message += new TcpFastMessageParser.MessageEventHandler(OnMessage);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SetTcpClient)}");
            }
        }

        private void SetupIntervalsFromConfig()
        {
            if (_tcpClient != null)
            {
                _tcpClient.SetHeartbeatInterval(_config.ClientHbInterval);
                _tcpClient.SetReconnectInterval(_config.ClientReconInterval);
            }
        }

        private async void OnConnected(TcpSocket tcpSocket)
        {
            if (tcpSocket.TcpSocketStatus == TcpSocketStatus.Connected && !tcpSocket.IsShutdown)
            {
                _isConnected = true;
                _connectionNotifier.Set();
                if (!_register)
                {
                    Start();
                }
                else
                {
                    RegisterResponse registerResponse = Register();
                    if (registerResponse != null)
                    {
                        Start();
                    }
                    else
                    {
                        await Task.Delay(1000).ContinueWith(t => ReStart());
                    }
                }
            }
        }

        private void Start()
        {
            ConnectionStatusChangedEvent?.Invoke(_isConnected);
            if (_isConnected)
            {
                SendManagerUiRegistrationMsg();
            }
        }

        private void OnConnectionFailedOrDisconnected(TcpSocket tcpSocket)
        {
            _isConnected = false;
            _connectionNotifier.Set();
            ConnectionStatusChangedEvent?.Invoke(_isConnected);
        }

        private void OnMessage(Message message, TcpSocket tcpSocket)
        {
            try
            {
                switch (message.Template.TemplateType)
                {
                    case TemplateType.Heartbeat:
                    case TemplateType.MDSendHeartbeat:
                        break;
                    case TemplateType.DataRequest:
                        DataRequest request = MessageFactory.DecodeDataRequestMessage(message);
                        if (request.IsResponse
                            && request.DataRequestType != DataRequestType.TradesRequest
                            && request.DataRequestType != DataRequestType.TradesExtRequest)
                        {
                            if (_requestIdToRequestsMap.TryRemove(request.RequestID, out AsyncResult<DataRequest> theRequest))
                            {
                                theRequest.SetAsCompleted(request, false);
                            }
                            else
                            {
                                _messageHandler.AddMessage(message);
                            }
                        }
                        else
                        {
                            _messageHandler.AddMessage(message);
                        }
                        break;
                    default:
                        _messageHandler.AddMessage(message);
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(OnMessage)}");
            }
        }

        private RegisterResponse Register()
        {
            DataRequest dataRequest = BeginRegister().EndInvoke(_shortRequestTimeout);
            if (dataRequest is Register dataRequestResponse && dataRequestResponse.Response != null)
            {
                return (RegisterResponse)dataRequestResponse.Response;
            }
            else
            {
                return default;
            }
        }

        private AsyncResult<DataRequest> BeginRegister()
        {
            Register registerReq = new()
            {
                RequestID = GetNextRequestId(),
                Id = _clientInfo.GetRegistrationString(),
            };

            AsyncResult<DataRequest> asyncResult = new(null, registerReq);

            _requestIdToRequestsMap[registerReq.RequestID] = asyncResult;
            _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(registerReq)));

            return asyncResult;
        }

        private void SendManagerUiRegistrationMsg()
        {
            try
            {
                MDGetHeartbeat getHbMessage = new()
                {
                    Timestamp = DateTime.Now,
                    StringValue = "NA"
                };

                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDGetHeartbeatMessage(getHbMessage)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendManagerUiRegistrationMsg)} -> Exception Sending {nameof(MDGetHeartbeat)} message");
            }
        }

        internal void SendSetMdClientNameMsg()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                MDSetWorkbookName setworkbookNameMsg = new()
                {
                    SendTime = DateTime.Now,
                    WorkbookName = $"{_config.AppId} - V:{version.ToString(3)}",
                };

                User user = OmsCore.User;
                if (user != null)
                {
                    setworkbookNameMsg.WorkbookName += $" [{user.Username}]";
                }

                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSetWorkbookNameMessage(setworkbookNameMsg)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendSetMdClientNameMsg)} -> Exception Sending {nameof(MDSetWorkbookName)} message");
            }
        }

        internal void SendSetEmaClientNameMsg()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                EMASetWorkbookName setworkbookNameMsg = new()
                {
                    SendTime = DateTime.Now,
                    WorkbookName = $"{_config.AppId} - V:{version.ToString(3)}",
                };

                User user = OmsCore.User;
                if (user != null)
                {
                    setworkbookNameMsg.WorkbookName += $" [{user.Username}]";
                }

                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateEMASetWorkbookNameMessage(setworkbookNameMsg)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendSetMdClientNameMsg)} -> Exception Sending {nameof(EMASetWorkbookName)} message");
            }
        }

        internal void SendMdsClientRegistrationMsg()
        {
            try
            {
                Register registerReq = new()
                {
                    Id = _clientInfo.GetRegistrationString(),
                };
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDataRequestMessage(registerReq)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendMdsClientRegistrationMsg)} -> Exception Sending {nameof(Register)} message");
            }
        }

        internal void SendSetOmsClientNameMsg()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                OMSSetWorkbookName getHbMessage = new()
                {
                    SendTime = DateTime.Now,
                    WorkbookName = $"{_config.AppId} - V:{version.ToString(3)}",
                };

                User user = OmsCore.User;
                if (user != null)
                {
                    getHbMessage.WorkbookName += $" [{user.Username}]";
                }

                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateOMSSetWorkbookNameMessage(getHbMessage)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendSetOmsClientNameMsg)} -> Exception Sending {nameof(OMSSetWorkbookName)} message");
            }
        }

        private void SendRequestSymbolsMsg(string symbol)
        {
            try
            {
                MDRequestSymbols mdRequestSymbols = new()
                {
                    Symbol = symbol,
                };

                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDRequestSymbolsMessage(mdRequestSymbols)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendRequestSymbolsMsg)} -> Exception Sending {nameof(MDRequestSymbols)} message");
            }
        }

        internal void SendDomCommand(DomCommand domCommand)
        {
            try
            {
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateDomCommandMessage(domCommand)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendDomCommand)} -> Exception Sending {nameof(DomCommand)} message");
            }
        }

        internal void SendUserFeedback(string moduleType, string type, string level, string subject, string description)
        {
            try
            {
                UserFeedback userFeedback = new()
                {
                    ModuleType = moduleType,
                    Type = type,
                    Level = level,
                    Subject = subject,
                    Description = description
                };
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateUserFeedbackMessage(userFeedback)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SendUserFeedback)} -> Exception Sending {nameof(UserFeedback)} message");
            }
        }

        internal void SubscribeTronTrades(string symbol)
        {
            try
            {
                MDSubscribeDmitryTrades tradeSubscriptionRequest = new()
                {
                    Symbol = symbol,
                    SendTime = DateTime.Now
                };
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDSubscribeDmitryTradesMessage(tradeSubscriptionRequest)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SubscribeTronTrades)} -> Exception Sending {nameof(UserFeedback)} message");
            }
        }

        internal void UnsubscribeTronTrades(string symbol)
        {
            try
            {
                MDUnsubscribeDmitryTrades tradeUnsubscribeRequest = new()
                {
                    Symbol = symbol,
                    SendTime = DateTime.Now
                };
                _tcpClient.SendData(FastEncoder.Encode(MessageFactory.CreateMDUnsubscribeDmitryTradesMessage(tradeUnsubscribeRequest)));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(UnsubscribeTronTrades)} -> Exception Sending {nameof(UserFeedback)} message");
            }
        }
    }
}
