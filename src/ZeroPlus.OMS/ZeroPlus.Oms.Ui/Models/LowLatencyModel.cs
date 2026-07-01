using DevExpress.Mvvm;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.LowLatency;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LowLatencyModel : BindableBase, ILowLatencyModel, IOmsDataSubscriber
    {
        private readonly ILogger<LowLatencyModel> _logger;
        private readonly OmsCore _omsCore;
        private readonly PortfolioManagerModel _portfolioManager;


        public ILowLatencyInstance LatencyInstance { get; }
        public bool IsDisposed { get; set; }

        [Bindable]
        public partial bool ShowNotification { get; set; }
        [Bindable]
        public partial bool CanStart { get; set; }
        [Bindable]
        public partial bool IsRunning { get; set; }
        [Bindable]
        public partial bool RunInTestMode { get; set; }
        [Bindable]
        public partial bool IsConnected { get; set; }
        [Bindable]
        public partial string Username { get; set; }
        [Bindable]
        public partial int InstanceId { get; set; }
        [Bindable]
        public partial string Name { get; set; }
        [Bindable(Default = 1)]
        public partial int Rank { get; set; }
        [Bindable]
        public partial string Host { get; set; }
        [Bindable]
        public partial int SymbolsCount { get; set; }
        [Bindable]
        public partial HashSet<string> Symbols { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double AdjPnl { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double RealPnl { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial int NetQty { get; set; }
        [Bindable]
        public partial int TotalFills { get; set; }
        [Bindable]
        public partial int TotalSubmissions { get; set; }
        [Bindable]
        public partial ObservableCollection<InitiatorModel> Initiators { get; set; }
        [Bindable]
        public partial ObservableCollection<LoopModel> Loops { get; set; }
        [Bindable]
        public partial ObservableCollection<LiquidatorModel> Liquidators { get; set; }
        [Bindable]
        public partial ObservableCollection<SignalModel> Signals { get; set; }
        [Bindable]
        public partial ObservableCollection<LowLatencyRiskModel> RiskModels { get; set; }
        [Bindable]
        public partial InitiatorModel Initiator { get; set; }
        [Bindable]
        public partial LoopModel Loop { get; set; }
        [Bindable]
        public partial LiquidatorModel Liquidator { get; set; }
        [Bindable]
        public partial SignalModel Signal { get; set; }
        [Bindable]
        public partial LowLatencyRiskModel Risk { get; set; }
        [Bindable]
        public partial ConfigSave WatchlistGenerator { get; set; }
        [Bindable]
        public partial string StatsTotalSent { get; set; }
        [Bindable]
        public partial string AppProcessThread { get; set; }
        [Bindable]
        public partial string LiveStrategies { get; set; }
        [Bindable]
        public partial bool ForceResendWatchlist { get; set; }
        [Bindable]
        public partial string Message { get; set; }

        public LowLatencyModel(ILogger<LowLatencyModel> logger, OmsCore omsCore, ILowLatencyInstance lowLatencyInstance, PortfolioManagerModel portfolioManager)
        {
            _logger = logger;
            _omsCore = omsCore;
            LatencyInstance = lowLatencyInstance;
            _portfolioManager = portfolioManager;
            LatencyInstance.LowLatencyStateChanged += OnInstanceStateChange;
            LatencyInstance.Init(this);

            Initiators = new ObservableCollection<InitiatorModel>();
            Loops = new ObservableCollection<LoopModel>();
            Liquidators = new ObservableCollection<LiquidatorModel>();
            Signals = new ObservableCollection<SignalModel>();
            RiskModels = new ObservableCollection<LowLatencyRiskModel>();
        }

        internal async Task RefreshAsync()
        {
            SymbolsCount = 0;
            Symbols?.Clear();
            if (!string.IsNullOrWhiteSpace(Name))
            {
                var symbols = await _omsCore.QuoteClient.GetSymbolsAsync(Name);
                if (symbols != null)
                {
                    if (WatchlistGenerator == null || WatchlistGenerator.Id == 0)
                    {
                        Symbols = symbols.Select(x => x.OptionSymbol).ToHashSet();
                        SymbolsCount = Symbols.Count;
                    }
                    else
                    {
                        await RunWatchListGenerator(symbols);
                    }

                    if (!IsConnected)
                    {
                        await LatencyInstance.ConnectAsync(RunInTestMode);
                    }
                    CheckForCanStart(out _);
                }
            }
        }

        private async Task RunWatchListGenerator(List<Option> symbols)
        {
            await Task.Run(async () =>
            {
                var fullConfig = await _omsCore.GatewayClient.RequestConfigDataAsync(WatchlistGenerator.Id);
                if (fullConfig != null)
                {
                    string configJson = fullConfig.ConfigJson;
                    SpreadsGeneratorConfig config = JsonConvert.DeserializeObject<SpreadsGeneratorConfig>(configJson, SpreadsGeneratorViewModel.SpreadGeneratorConfigSerializationSettings);
                    if (config != null && config.SingleLegEnabled)
                    {
                        List<ZeroPlus.Models.Data.Securities.Option> filtered = new();
                        if (config.CallsEnabled && config.PutsEnabled)
                        {
                            foreach (var symbol in symbols)
                            {
                                if (_omsCore.SecurityBook.GetSecurity(symbol.OptionSymbol) is ZeroPlus.Models.Data.Securities.Option converted)
                                {
                                    filtered.Add(converted);
                                }
                            }
                        }
                        else if (config.CallsEnabled)
                        {
                            foreach (var symbol in symbols.Where(x => x.Type == OptionType.CALL))
                            {
                                if (_omsCore.SecurityBook.GetSecurity(symbol.OptionSymbol) is ZeroPlus.Models.Data.Securities.Option converted)
                                {
                                    filtered.Add(converted);
                                }
                            }
                        }
                        else if (config.PutsEnabled)
                        {
                            foreach (var symbol in symbols.Where(x => x.Type == OptionType.PUT))
                            {
                                if (_omsCore.SecurityBook.GetSecurity(symbol.OptionSymbol) is ZeroPlus.Models.Data.Securities.Option converted)
                                {
                                    filtered.Add(converted);
                                }
                            }
                        }
                        else
                        {
                            return;
                        }

                        var cpFiltered = await SpreadsGeneratorViewModel.ApplySpreadGeneratorFilters(config, filtered, _portfolioManager, CancellationToken.None);

                        SingleLegSpreadsGenerator generator = new SingleLegSpreadsGenerator(_logger, new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore(), new DataStore());

                        List<Task<SpreadGeneratorResults>> tasks = new();

                        if (config.CallsEnabled)
                        {
                            tasks.Add(Task.Run(() => generator.GenerateAsync(cpFiltered.callOptionsChain,
                                null,
                                false,
                                config.SingleLegSpreadsSettings!,
                                symbols.Count,
                                CancellationToken.None)));
                        }

                        if (config.PutsEnabled)
                        {
                            tasks.Add(Task.Run(() => generator.GenerateAsync(cpFiltered.putOptionsChain,
                                null,
                                false,
                                config.SingleLegSpreadsSettings!,
                                symbols.Count,
                                CancellationToken.None)));
                        }

                        await Task.WhenAll(tasks);

                        if (tasks.All(x => x.IsCompletedSuccessfully))
                        {
                            var result = tasks.Select(x => x.Result);
                            Symbols = result.SelectMany(x => x.Spreads.Select(i => i.Symbol)).ToHashSet();
                            SymbolsCount = Symbols.Count;
                        }
                    }
                }
            });
        }

        public bool CheckForCanStart(out string message)
        {
            var canStart = true;
            message = "";

            if (SymbolsCount == 0)
            {
                message += "Symbol Not Found, ";
                canStart = false;
            }
            if (!IsConnected)
            {
                message += "Not Connected, ";
                canStart = false;
            }
            if (Initiator == null)
            {
                message += "Initiator Not Selected, ";
                canStart = false;
            }
            if (Liquidator == null)
            {
                message += "Liquidator Not Selected, ";
                canStart = false;
            }
            if (Signal == null)
            {
                message += "Signal Not Selected, ";
                canStart = false;
            }
            if (Risk == null)
            {
                message += "Risk Not Selected, ";
                canStart = false;
            }
            VerifyUsername();

            CanStart = canStart;
            SetMessage(canStart ? string.Empty : message.Trim()[..(message.Length - 2)]);

            return canStart;
        }

        internal void Start()
        {
            if (CanStart && !IsRunning)
            {
                VerifyUsername();
                LatencyInstance.Start();
            }
        }

        internal void LoadFromFile(string path)
        {
            LatencyInstance.LoadFromFile(path);
        }

        internal void Stop(bool killAll)
        {
            LatencyInstance.Stop(killAll);
        }

        private void OnInstanceStateChange(bool isConnected, bool isRunning)
        {
            IsConnected = isConnected;
            IsRunning = isRunning;
            CheckForCanStart(out _);
        }

        public void InitiatorChanged()
        {
            if (IsRunning)
            {
                LatencyInstance.UploadInitiatorChanges();
            }
            CheckForCanStart(out _);
        }

        public void LiquidatorChanged()
        {
            if (IsRunning)
            {
                LatencyInstance.UploadLiquidatorChanges();
            }
            CheckForCanStart(out _);
        }

        public void SignalChanged()
        {
            if (IsRunning)
            {
                LatencyInstance.UploadSignalChanges();
            }
            CheckForCanStart(out _);
        }

        public void RiskChanged()
        {
            if (IsRunning)
            {
                LatencyInstance.UploadRiskChanges();
            }
            CheckForCanStart(out _);
        }

        public LowLatencyModelConfig GetConfig()
        {
            return new LowLatencyModelConfig
            {
                Name = Name,
                Username = Username,
                InstanceId = InstanceId,
                Rank = Rank,
                InitiatorId = Initiator?.Id ?? 0,
                LoopId = Loop?.Id ?? 0,
                LiquidatorId = Liquidator?.Id ?? 0,
                SignalId = Signal?.Id ?? 0,
                RiskId = Risk?.Id ?? 0,
                WatchlistGenerator = WatchlistGenerator?.Id ?? 0,
            };
        }

        public void VerifyUsername()
        {
            if (InstanceId == 0 || string.IsNullOrWhiteSpace(Username) || Username != BuildUsername(InstanceId))
            {
                SetUsername();
            }

            SubscribeToInstancePosition();
        }

        private void SubscribeToInstancePosition()
        {
            _portfolioManager.Subscribe(Username, SubscriptionFieldType.FirmInstancePosition, this);
        }

        public void SetUsername()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return;
            }

            var instanceId = LowLatencyInstance.GetNextInstanceId();
            InstanceId = instanceId;
            Username = BuildUsername(instanceId);
        }

        public void SetMessage(string message)
        {
            Message = message;
            ShowNotification = Message.Contains("ERROR: Signal Completed by:") &&
                               !Message.Contains("CancelOrderDueToUser");
        }

        public string BuildUsername(int instanceId)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                return "";
            }

            if (LowLatencyInstance.TryGetSuffix(Name, RunInTestMode, out string suffix))
            {
                return _omsCore.User.Username + suffix + instanceId;
            }

            return "";
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            if (key.Type == SubscriptionFieldType.FirmInstancePosition &&
                key.Symbol == Username &&
                value is IPosition position)
            {
                AdjPnl = position.AdjustedPnl;
                RealPnl = position.RealizedPnl;
                UnrealPnl = position.UnrealizedPnl;
                NetQty = position.NetQty;
                TotalFills = position.TotalFills;
                TotalSubmissions = position.TotalSubmissions;
            }
        }
    }
}
