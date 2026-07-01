using Middleware.Communication.Tcp;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Data.Trading;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.LowLatency.Ext;
using ZeroPlus.Oms.Ui.Models;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.Helpers;
using static ZeroPlus.Oms.Ui.LowLatency.Ext.MsgRequests;
using TcpClient = Middleware.Communication.Tcp.TcpClient;

namespace ZeroPlus.Oms.Ui.LowLatency
{
    public class LowLatencyInstance : ILowLatencyInstance, ITcpMessageParser
    {
        public event LowLatencyStateChangedHandler LowLatencyStateChanged;

        private const string SPLIT = "NVDA";
        private const string LOG_LOC = "LoLa-Logs";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private static int _instanceId;

        private readonly LowLatencyTransactionsProcessor _loLaTransactionsProcessor;
        private readonly byte[] _heartbeatPacket;
        private readonly StringBuilder _messageBuffer;
        private readonly OmsCore _omsCore;
        private ILowLatencyModel _model;
        private TcpClient _tcpClient;
        private string _logKey;
        private bool _loginSent;

        public static readonly JsonSerializerSettings NoJsonNulls = new()
        { NullValueHandling = NullValueHandling.Ignore };

        public byte[] GetHeartbeatPacket() => _heartbeatPacket;
        private string BasketName() => $"basket_{_model.Username}_{_model.Name}";
        private string WatchlistName() => $"watchlist001_{_model.Username}";
        private string TradeName() => $"trade001_{_model.Username}";

        public string SentWatchlistJsonString { get; set; }

        public LowLatencyInstance(LowLatencyTransactionsProcessor loLaTransactionsProcessor, OmsCore omsCore)
        {
            _loLaTransactionsProcessor = loLaTransactionsProcessor;
            _omsCore = omsCore;
            _heartbeatPacket = [];
            _messageBuffer = new StringBuilder();
        }

        public static int GetNextInstanceId()
        {
            return Interlocked.Increment(ref _instanceId);
        }

        public static void UpdateNextInstanceId(int id)
        {
            if (id > _instanceId)
            {
                _instanceId = id;
            }
        }

        public void Init(ILowLatencyModel lowLatencyModel)
        {
            _model = lowLatencyModel;
        }

        public void Parse(TcpSocket tcpSocket, IReadBuffer readBuffer)
        {
            try
            {
                if (readBuffer is { Length: > 0 })
                {
                    readBuffer.SeekOrigin();
                    long length = readBuffer.Length;
                    byte[] buffer = new byte[length];
                    readBuffer.Read(buffer, 0, 0, (int)length);
                    string jsonData = Encoding.UTF8.GetString(buffer);
                    _messageBuffer.Append(jsonData);
                    readBuffer.Remove((int)length);
                    ProcessLines();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Parse));
            }
        }

        private void ProcessLines()
        {
            while (_messageBuffer.Length > 0)
            {
                int index = _messageBuffer.ToString().IndexOf('\n');
                if (index < 0)
                {
                    break;
                }

                var jsonString = _messageBuffer.ToString(0, index).TrimEnd();

                _messageBuffer.Remove(0, index + 1);

                if (!string.IsNullOrWhiteSpace(jsonString))
                {
                    ParseJson(jsonString);
                }
            }
        }

        private void OnClientConnected(TcpSocket tcpSocket)
        {
            LowLatencyStateChanged?.Invoke(_tcpClient?.IsConnected ?? false, false);
        }

        private void OnClientDisconnected(TcpSocket tcpSocket)
        {
            Disconnect();
            SentWatchlistJsonString = "";
            _loginSent = false;
            LowLatencyStateChanged?.Invoke(_tcpClient?.IsConnected ?? false, false);
        }

        private void OnClientConnectionFailed(TcpSocket tcpSocket)
        {
            _loginSent = false;
            LowLatencyStateChanged?.Invoke(_tcpClient?.IsConnected ?? false, false);
        }

        public async Task<bool> ConnectAsync(bool testMode)
        {
            await SetupLogger();
            return await Task.Run(() => Connect(testMode));
        }

        private async Task SetupLogger()
        {
            string parent = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), LOG_LOC);
            if (!Directory.Exists(parent))
            {
                Directory.CreateDirectory(parent);
            }
            _logKey = Path.Combine(parent, _model.Name + "-" + DateTime.Today.ToString("yy-MM-dd") + ".log");
            if (File.Exists(_logKey))
            {
                await LoadFromFile(_logKey);
            }
            else
            {
                File.Create(_logKey);
            }
        }

        public bool Connect(bool testMode)
        {
            try
            {
                Disconnect();

                _tcpClient = new TcpClient()
                {
                    MessageParser = this,
                    ConnectTimeout = 5,
                };
                _tcpClient.Connected += OnClientConnected;
                _tcpClient.Disconnected += OnClientDisconnected;
                _tcpClient.ConnectionFailed += OnClientConnectionFailed;

                if (TryGetAddress(_model.Name, testMode, out string address, out string host, out int port))
                {
                    if (string.IsNullOrWhiteSpace(_model.Username))
                    {
                        _model.SetUsername();
                    }

                    _tcpClient.Connect(address, port, 256 * 1024, 256 * 1024);
                    _model.Host = host;
                    return _tcpClient.IsConnected;
                }

                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Connect));
                return false;
            }
        }

        private void Disconnect()
        {
            try
            {
                SentWatchlistJsonString = "";
                _tcpClient?.Disconnect();
                _loginSent = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Disconnect));
            }
        }

        public static bool TryGetSuffix(string modelName, bool testMode, out string suffix)
        {
            try
            {
                suffix = "";
                var length = modelName.Length;

                if (string.IsNullOrEmpty(modelName) || length < 2)
                {
                    return false;
                }

                if (testMode)
                {
                    suffix = "T";
                    return true;
                }
                else
                {
                    var index = GetServerIndex(modelName);

                    var addresses = OmsCore.Config.LowLatencyAddressV1.Split(";");

                    if (index >= addresses.Length)
                    {
                        return false;
                    }

                    suffix = ((char)('A' + index)).ToString();
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetAddress));
                suffix = "";
                return false;
            }
        }

        public static bool TryGetAddress(string modelName, bool testMode, out string address, out string host, out int port)
        {
            try
            {
                address = "";
                host = "";
                port = 0;
                var length = modelName.Length;

                if (string.IsNullOrEmpty(modelName) || length < 2)
                {
                    return false;
                }

                if (testMode)
                {
                    address = OmsCore.Config.LowLatencyTestAddressV1;
                    port = OmsCore.Config.LowLatencyTestPort;
                    host = GetHostName(address);
                    return true;
                }
                else
                {
                    var index = GetServerIndex(modelName);

                    var addresses = OmsCore.Config.LowLatencyAddressV1.Split(";");

                    if (index >= addresses.Length)
                    {
                        return false;
                    }

                    address = addresses[index];
                    port = OmsCore.Config.LowLatencyPort;
                    host = GetHostName(address);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryGetAddress));
                address = "";
                host = "";
                port = 0;
                return false;
            }
        }

        private static int GetServerIndex(string modelName)
        {
            int length = modelName.Length;
            modelName = modelName.ToUpper();

            int index;
            if (modelName[0] < SPLIT[0])
            {
                index = 0;
            }
            else if (modelName[0] > SPLIT[0])
            {
                index = 1;
            }
            else if (modelName[1] < SPLIT[1])
            {
                index = 0;
            }
            else if (modelName[1] > SPLIT[1])
            {
                index = 1;
            }
            else if (length < 3 || modelName[2] < SPLIT[2])
            {
                index = 0;
            }
            else if (modelName[2] > SPLIT[2])
            {
                index = 1;
            }
            else if (length < 4 || modelName[3] < SPLIT[3])
            {
                index = 0;
            }
            else
            {
                index = 1;
            }

            return index;
        }

        public static string GetHostName(string ipAddress)
        {
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(ipAddress);
                return entry.HostName;
            }
            catch
            {
                return ipAddress;
            }
        }

        public void Start()
        {
            _log.Info($"{DateTime.Now:yyyyMMdd HH:mm:ss} START");
            if (!_tcpClient.IsConnected)
            {
                _log.Info($"{DateTime.Now:yyyyMMdd HH:mm:ss} Not Connected!");
                return;
            }

            EnsureLoggedIn();

            var jsonRequestBuy = new jsonRequest
            {
                RunTrade = MakeRunTrade(),
                ParamTrades = MakeParamTrades(),
                ParamBasket = MakeBasketParam(),
                RiskParams = MakeRiskParams(),
            };

            jsonRequestParamWatchlist watchlist = MakeSignalWatchlist();

            if (watchlist != null)
            {
                var jsonRequestWatchlist = new jsonRequest
                {
                    ParamWatchlist = watchlist,
                };

                string watchlistJsonString = $"{JsonConvert.SerializeObject(jsonRequestWatchlist, Formatting.None, NoJsonNulls)}";
                bool same = SentWatchlistJsonString == watchlistJsonString;

                if (!same || _model.ForceResendWatchlist)
                {
                    SentWatchlistJsonString = watchlistJsonString;
                    SendToTrade(jsonRequestWatchlist);
                }

                SendToTrade(jsonRequestBuy);
                LowLatencyStateChanged?.Invoke(_tcpClient.IsConnected, true);
            }
        }

        public void Stop(bool killAll)
        {
            int abortWhat = 0;
            if (killAll)
            {
                abortWhat |= 1;
                abortWhat |= 2;
            }
            abortWhat |= 4;

            jsonRequestAbortTrade abortTrade = new jsonRequestAbortTrade
            {
                UserName = _model.Username,
                AbortWhat = abortWhat,
            };

            jsonRequest jsonRequestAbort = new jsonRequest
            {
                AbortTrade = abortTrade
            };

            EnsureLoggedIn();

            SendToTrade(jsonRequestAbort);

            LowLatencyStateChanged?.Invoke(_tcpClient?.IsConnected ?? false, false);
        }

        private void EnsureLoggedIn()
        {
            if (_loginSent)
            {
                return;
            }

            jsonRequest jsonRequestLogin = new jsonRequest
            {
                Login = CreateLogin(),
            };

            _loginSent = true;

            SendToTrade(jsonRequestLogin);
        }

        private jsonRequestLogin CreateLogin()
        {
            jsonRequestLogin login = new jsonRequestLogin
            {
                UserName = _model.Username,
                Account = OmsCore.Config.LoLaAccount,
                FDID = OmsCore.Config.LoLaFdid,
            };
            return login;
        }

        public void UploadInitiatorChanges()
        {
            Upload(null, _model.Initiator?.GetParams(_model.Loop), null);
        }

        public void UploadLiquidatorChanges()
        {
            Upload(null, null, _model.Liquidator?.GetParams());
        }

        public void UploadSignalChanges()
        {
            foreach (var signal in _model.Signal.Signals)
            {
                Upload(_model.Signal?.GetParams(signal.Tag), null, null);
            }
        }

        public void UploadAllChanges()
        {
            foreach (var signal in _model.Signal.Signals)
            {
                Upload(_model.Signal?.GetParams(signal.Tag), _model.Initiator?.GetParams(_model.Loop), _model.Liquidator?.GetParams());
            }
        }

        public void UploadRiskChanges()
        {
            EnsureLoggedIn();

            var jsonRequestRisk = new jsonRequest
            {
                RiskParams = MakeRiskParams(),
            };

            SendToTrade(jsonRequestRisk);
        }

        public void UploadManualAdjustment(LowLatencyOrderModel orderModel, IOmsOrder trade)
        {
            var username = orderModel.UserName;
            var symbol = orderModel.Symbol;
            var side = orderModel.Side;
            var clOrdId = orderModel.ClOrdId;
            var who = orderModel.Who;
            var averagePrice = trade.AveragePrice;
            var cumulativeQuantity = trade.CumulativeQuantity;

            RequestManualAdj(username, symbol, averagePrice, cumulativeQuantity, side, clOrdId, who, false, true);
        }

        public void RequestManualAdj(string userName,
            string symbol,
            double averagePrice,
            int cumulativeQuantity,
            Side side,
            string clOrdId,
            string who,
            bool doNothing,
            bool bypassRisk)
        {
            jsonRequestExecutionManualParams requestExecutionManualParams = new jsonRequestExecutionManualParams
            {
                UserName = userName,
                FillSymbol = symbol,
                FillPrice = $"{averagePrice:N4}",
                FillQty = cumulativeQuantity,
                FillSideSell = side == Side.Sell ? 1 : 0,
                DoNothing = doNothing ? 1 : 0,
                BypassRisk = bypassRisk ? 1 : 0,
                LogWho = !string.IsNullOrWhiteSpace(who) ? GetManualAdjWho(userName, clOrdId, who) : "",
                FillClOrdId = clOrdId,
            };

            jsonRequestParamWatchlist paramWatchList = new jsonRequestParamWatchlist
            {
                WatchlistName = $"watchlistManual{DateTime.Now:yyyyMMddHHmmssfff}_{_model.Username}_{side.ToString()}",
                WatchlistSymbols = [symbol],
            };

            jsonRequest jsonManualAdjust = new jsonRequest
            {
                ParamWatchlist = paramWatchList,
                ManualAdjust = requestExecutionManualParams,
            };

            EnsureLoggedIn();

            SendToTrade(jsonManualAdjust);
        }

        private string GetManualAdjWho(string currentUserName, string fillClOrdId, string who)
        {
            if (!string.IsNullOrWhiteSpace(who))
            {
                StringBuilder sb = new StringBuilder();

                var ss0 = who.Split()[0];
                var ss1 = who.Split()[1];

                var ss = ss1.Split([':'], StringSplitOptions.RemoveEmptyEntries);

                if (!string.IsNullOrWhiteSpace(fillClOrdId))
                {
                    char stratType = fillClOrdId.Split(':')[3][0];
                    sb.Append($"/StratType/:/{stratType}/,");
                }

                var signalInstance = ss0[1];
                var stratName = ss[0];
                var stratId = ss[1];
                string stratIdInResponseTo = null;

                if (ss.Length > 2)
                {
                    stratIdInResponseTo = ss[2];
                }

                sb.Append($"/StratName/:/{stratName}/,");

                sb.Append($"/StratNameWithId/:/e{stratName}:{stratId}");
                if (stratIdInResponseTo != null)
                {
                    sb.Append("::").Append(stratIdInResponseTo);
                }

                sb.Append($":::{currentUserName}[{signalInstance}]/,");

                sb.Append($"/StratId/:/{stratId}/,");
                sb.Append($"/SignalInstance/:/{signalInstance}/,");

                if (stratIdInResponseTo != null)
                {
                    sb.Append($"/StratIdInResponseTo/:/{stratIdInResponseTo}/,");
                }

                return sb.ToString();
            }

            return who;
        }

        public void Upload(jsonRequestSignalController signalController, jsonRequestInitiatorController initiatorController, jsonRequestLiquidatorController liquidatorController)
        {
            jsonRequestParamsBasket basketParams = new jsonRequestParamsBasket
            {
                ParamBasketName = BasketName(),
                InitiatorController = initiatorController ?? _model.Initiator?.GetParams(_model.Loop),
                LiquidatorController = liquidatorController ?? _model.Liquidator?.GetParams(),
                SignalController = signalController ?? _model.Signal?.GetParams(),
            };

            if (liquidatorController?.Chaser != null)
            {
                liquidatorController.Chaser.RollMode = _model.Liquidator.Route != null && _model.Liquidator.Route.Contains("ROLL") ? 1 : 0;
            }

            jsonRequest jsonRequestBuySell = new jsonRequest
            {
                ParamBasket = basketParams,
            };

            SendToTrade(jsonRequestBuySell);
        }

        private jsonRequestRunTrade MakeRunTrade()
        {
            jsonRequestRunTrade runTrade = new jsonRequestRunTrade
            {
                ParamTradeName = TradeName(),
                InitiatorExchange = ApplyBrokerPrefix(_omsCore.AutoTraderClient.GetLoLaRoute(_model.Initiator.Route)),
                LiquidatorExchange = ApplyBrokerPrefix(_omsCore.AutoTraderClient.GetLoLaRoute(_model.Liquidator.Route)),
            };

            return runTrade;
        }

        private string ApplyBrokerPrefix(string route)
        {
            if (string.IsNullOrWhiteSpace(route) || route.Contains('-'))
            {
                return route;
            }
            var broker = OmsCore.Config.DefaultBroker;
            if (string.IsNullOrWhiteSpace(broker))
            {
                return route;
            }
            return $"{broker}-{route}";
        }

        private jsonRequestRiskParams MakeRiskParams()
        {
            if (_model.Risk == null)
            {
                return null;
            }

            jsonRequestRiskParams risk = new jsonRequestRiskParams
            {
                MaxLossInDollars = _model.Risk.MaxLossInitiator,
                MaxLossInDollarsLiq = _model.Risk.MaxLossLiquidator,
                MaxOpenPos = _model.Risk.MaxOpenPosition,
                MaxOpenSymbols = _model.Risk.MaxOpenSymbols,
            };

            return risk;
        }

        private jsonRequestParamTrades MakeParamTrades()
        {
            jsonRequestParamTrades paramTrades = new jsonRequestParamTrades
            {
                ParamTradeName = TradeName(),
                ParamBasketName = BasketName(),
                ParamWatchlistName = WatchlistName(),
            };

            return paramTrades;
        }

        private jsonRequestParamsBasket MakeBasketParam()
        {
            jsonRequestParamsBasket basketParams = new jsonRequestParamsBasket
            {
                ParamBasketName = BasketName(),
                InitiatorController = new jsonRequestInitiatorController(),
                LiquidatorController = new jsonRequestLiquidatorController(),
                SignalController = new jsonRequestSignalController()
            };

            var initFound = SetInitController(basketParams);
            if (!initFound)
            {
                basketParams.InitiatorController = null;
            }

            bool liqFound = SetLiqController(basketParams);
            if (!liqFound)
            {
                basketParams.LiquidatorController = null;
            }

            bool sigFound = SetSigController(basketParams);
            if (!sigFound)
            {
                basketParams.SignalController = null;
            }

            return basketParams;
        }

        private bool SetInitController(jsonRequestParamsBasket basketParams)
        {
            bool initFound = false;

            jsonRequestInitiatorController initController = basketParams.InitiatorController;

            if (_model.Initiator.Type == InitiatorType.Hunter)
            {
                initFound = true;
                var executionHunterParams = _model.Initiator.HunterModel.JsonRequestExecutionHunterParams(_model.Loop);
                initController.Hunter = executionHunterParams;
            }

            return initFound;
        }

        private bool SetLiqController(jsonRequestParamsBasket basketParams)
        {
            bool liqFound = false;
            jsonRequestLiquidatorController liqController = basketParams.LiquidatorController;

            if (_model.Liquidator.Type == LiquidatorType.Chaser)
            {
                liqFound = true;
                liqController.Chaser = _model.Liquidator.ChaserModel.JsonRequestExecutionChaserParams;
                liqController.Chaser.RollMode = _model.Liquidator.Route != null && _model.Liquidator.Route.Contains("ROLL") ? 1 : 0;
            }

            return liqFound;
        }

        private bool SetSigController(jsonRequestParamsBasket basketParams)
        {
            bool sigFound = false;
            jsonRequestSignalController sigController = basketParams.SignalController;

            if (_model.Signal.Type == SignalType.TradeWatcher)
            {
                sigFound = true;
                sigController.TradeWatcher = _model.Signal.JsonRequestSignalTradeWatcherParams(true);
            }

            return sigFound;
        }

        private void SendToTrade(jsonRequest jsonRequest)
        {
            try
            {
                var connected = _tcpClient is { IsConnected: true };
                string jsonString = $"{JsonConvert.SerializeObject(jsonRequest, Formatting.None, NoJsonNulls)}";
                _log.Info($"{DateTime.Now:yyyyMMdd HH:mm:ss} NumBytes:{jsonString.Length:N0} Stat: {connected} To: {jsonString}");
                jsonString = $"{jsonString}{Environment.NewLine}";
                if (connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(jsonString);
                    _tcpClient.SendData(data);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendToTrade));
            }
        }

        private jsonRequestParamWatchlist MakeSignalWatchlist()
        {
            List<string> symbols = _model.Symbols.Select(SymbolNamer.PatchTOS2TB).ToList();

            jsonRequestParamWatchlist paramWatchlist = new jsonRequestParamWatchlist
            {
                WatchlistName = WatchlistName(),
                WatchlistSymbols = symbols.ToList(),
                WatchlistSymbolsCount = symbols.Count,
            };

            return paramWatchlist;
        }

        private void ParseJson(string jsonString, bool log = true)
        {
            try
            {
                MsgPackerResponses.jsonResponse resp = JsonConvert.DeserializeObject<MsgPackerResponses.jsonResponse>(jsonString);
                if (resp != null)
                {
                    resp.LowLatencyInstance = this;
                    AddResponse(resp);
                }

                if (log)
                {
                    _log.Info($"{DateTime.Now:yyyyMMdd HH:mm:ss} Fr: {jsonString}");
                    if (_logKey != null)
                    {
                        using var writer = File.AppendText(_logKey);
                        writer.WriteLine(jsonString);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        protected void AddResponse(MsgPackerResponses.jsonResponse resp)
        {
            _loLaTransactionsProcessor.Add(resp);

            MsgPackerResponses.msgResponseStratStats respStratStats = resp.StratStats;
            HandleStratStats(respStratStats);

            var error = resp.Error;
            if (!string.IsNullOrWhiteSpace(error))
            {
                _model.SetMessage(error);
                if (error.StartsWith("Signal Completed by: CancelOrder"))
                {
                    LowLatencyStateChanged?.Invoke(_tcpClient?.IsConnected ?? false, false);
                }
            }

            if (!string.IsNullOrWhiteSpace(resp.AppProcess) && !string.IsNullOrWhiteSpace(resp.AppThread))
            {
                string appProcessThread = $"App:[{resp.AppProcess}{resp.AppThread}]";
                _model.AppProcessThread = appProcessThread;
            }
        }

        private void HandleStratStats(MsgPackerResponses.msgResponseStratStats respStratStats)
        {
            if (respStratStats == null)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(respStratStats.Total))
            {
                sb.Append($"Total:{respStratStats.Total}");
            }

            if (respStratStats.Signals != null)
            {
                string s = "";
                foreach (var kv in respStratStats.Signals)
                {
                    if (s.Length == 0)
                    {
                        sb.Append(" Signal[");
                    }

                    sb.Append($"{s}{kv.Key}:{kv.Value}");
                    s = " ";
                }

                if (s.Length > 0)
                    sb.Append("]");

            }

            if (respStratStats.Initiators != null)
            {
                string s = "";
                foreach (var kv in respStratStats.Initiators)
                {
                    if (s.Length == 0)
                    {
                        sb.Append(" Initiators[");
                    }

                    sb.Append($"{s}{kv.Key}:{kv.Value}");
                    s = " ";
                }

                if (s.Length > 0)
                    sb.Append("]");

            }

            if (respStratStats.Liquidators != null)
            {
                string s = "";
                foreach (var kv in respStratStats.Liquidators)
                {
                    if (s.Length == 0)
                    {
                        sb.Append(" Liquidators[");
                    }

                    sb.Append($"{s}{kv.Key}:{kv.Value}");
                    s = " ";
                }

                if (s.Length > 0)
                    sb.Append("]");
            }

            var ssb = $"{sb}";
            _model.LiveStrategies = ssb;
        }

        public async Task LoadFromFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }
            await Task.Run(() =>
            {
                FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var logFileReader = new StreamReader(fileStream);
                string line;
                while ((line = logFileReader.ReadLine()) != null)
                {
                    if (line.Contains("{"))
                    {
                        string jsonString = line.Substring(line.IndexOf('{'));
                        ParseJson(jsonString, false);
                    }
                }
            });
        }
    }
}
