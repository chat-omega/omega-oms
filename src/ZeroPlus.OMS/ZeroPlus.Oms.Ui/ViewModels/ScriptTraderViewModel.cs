using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using ICSharpCode.AvalonEdit.Document;
using Python.Runtime;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Requests;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ScriptTraderViewModel : ModuleViewModelBase, IOrderInfoUpdateHandler
    {
        private const string ENTRY_FUNC_NAME = "entry_check";
        private const string EXIT_FUNC_NAME = "exit_check";
        private const string STOPLOSS_FUNC_NAME = "stoploss_check";

        private readonly string _bootstrap;
        private readonly object _codeBlockLock = new();
        private readonly ConcurrentDictionary<string, PairOrderModel> _orderIdToOrderModelMap = new();
        private readonly DateTime _stopTime = (DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(50)).FromEastern();
        private readonly DateTime _closeTime = (DateTime.Today + TimeSpan.FromHours(15) + TimeSpan.FromMinutes(59)).FromEastern();
        private readonly ConcurrentStack<TradeUnit> _sells = new();
        private readonly ConcurrentStack<TradeUnit> _buys = new();
        private readonly Timer _triggerTimer;

        private bool _isReady;


        private PyObject _entryScriptCompiled;
        private PyObject _exitScriptCompiled;
        private PyObject _stopLossScriptCompiled;


        private double _bid;
        private double _mid;
        private double _ask;

        private double _delta;
        private double _theo;

        private double _ema;
        private double _ema2;
        private double _ema3;
        private double _macd;

        double _prevSignal = double.NaN;
        double _signal;

        private double _bidPrev;
        private double _midPrev;
        private double _askPrev;

        private double _deltaPrev;
        private double _theoPrev;

        private double _emaPrev;
        private double _ema2Prev;
        private double _ema3Prev;
        private double _macdPrev;


        private DataSubscriber _dataSubscriber;

        private DateTime _nextEntryTriggerTime;
        private DateTime _nextExitTriggerTime;
        private DateTime _nextStopLossTriggerTime;
        private DateTime _lastCancelCheck;

        public override Models.Module Module { get; protected set; } = Models.Module.ScriptTrader;

        public List<string> StockPairAccounts { get; } = new() { "TBK1501002", "DDEMO40" };
        public List<InitSide> StockPairInitSides { get; } = Enum.GetValues(typeof(InitSide)).Cast<InitSide>().ToList();
        public List<ScriptTradeType> ScriptTradeTypes { get; } = Enum.GetValues(typeof(ScriptTradeType)).Cast<ScriptTradeType>().ToList();
        public List<ExecutionStyle> StockPairExecutionStyles { get; } = Enum.GetValues(typeof(ExecutionStyle)).Cast<ExecutionStyle>().ToList();
        public List<ScriptTriggerType> ScriptTriggerTypes { get; } = Enum.GetValues(typeof(ScriptTriggerType)).Cast<ScriptTriggerType>().ToList();

        public EmaConfig EmaConfig { get; }
        public EmaConfig Ema2Config { get; }
        public EmaConfig Ema3Config { get; }
        public EmaCalculator EmaCalculator { get; }
        public EmaCalculator Ema2Calculator { get; }
        public EmaCalculator Ema3Calculator { get; }
        public MacdCalculator MacdCalculator { get; }
        public bool CodeRunning { get; private set; }

        [Bindable(Default = "")]
        public partial string ConsoleOutput { get; set; }
        public new bool IsReady
        {
            get => _isReady;
            set => SetValue(ref _isReady, value);
        }
        [Bindable]
        public partial bool IsRunning { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial string ReverseSymbol { get; set; }
        [Bindable]
        public partial int Qty { get; set; }
        [Bindable]
        public partial ScriptTradeType ScriptTradeType { get; set; }
        [Bindable]
        public partial TextDocument EntryScript { get; set; }
        [Bindable]
        public partial TextDocument ExitScript { get; set; }
        [Bindable]
        public partial TextDocument StopLossScript { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial string ContraStatus { get; set; }
        [Bindable]
        public partial StatusMode ContraStatusMode { get; set; }
        [Bindable]
        public partial bool EntryScriptReady { get; set; }
        [Bindable]
        public partial bool ExitScriptReady { get; set; }
        [Bindable]
        public partial bool StopLossScriptReady { get; set; }
        [Bindable]
        public partial string EntryScriptError { get; set; }
        [Bindable]
        public partial string ExitScriptError { get; set; }
        [Bindable]
        public partial string StopLossScriptError { get; set; }
        [Bindable]
        public partial ScriptTriggerType EntryScriptTriggerType { get; set; }
        [Bindable]
        public partial ScriptTriggerType ExitScriptTriggerType { get; set; }
        [Bindable]
        public partial ScriptTriggerType StopLossScriptTriggerType { get; set; }
        [Bindable]
        public partial int EntryInterval { get; set; }
        [Bindable]
        public partial int ExitInterval { get; set; }
        [Bindable]
        public partial int StopLossInterval { get; set; }
        [Bindable]
        public partial TimeSpan EntryScriptCountDown { get; set; }
        [Bindable]
        public partial TimeSpan ExitScriptCountDown { get; set; }
        [Bindable]
        public partial TimeSpan StopLossScriptCountDown { get; set; }
        [Bindable]
        public partial bool SubscribeToQuotes { get; set; }
        [Bindable]
        public partial bool SubscribeToGreeks { get; set; }
        [Bindable]
        public partial bool SubscribeToEma { get; set; }
        [Bindable]
        public partial bool SubscribeToMacd { get; set; }
        public double Bid
        {
            get => _bid;
            set
            {
                _bidPrev = _bid;
                SetValue(ref _bid, value);
            }
        }
        public double Mid
        {
            get => _mid;
            set
            {
                _midPrev = _mid;
                SetValue(ref _mid, value);
            }
        }
        public double Ask
        {
            get => _ask;
            set
            {
                _askPrev = _ask;
                SetValue(ref _ask, value);
            }
        }
        public double Delta
        {
            get => _delta;
            set
            {
                _deltaPrev = _delta;
                SetValue(ref _delta, value);
            }
        }
        public double Theo
        {
            get => _theo;
            set
            {
                _theoPrev = _theo;
                SetValue(ref _theo, value);
            }
        }
        public double Ema
        {
            get => _ema;
            set
            {
                _emaPrev = _ema;
                SetValue(ref _ema, value);
            }
        }
        public double Ema2
        {
            get => _ema2;
            set
            {
                _ema2Prev = _ema2;
                SetValue(ref _ema2, value);
            }
        }
        public double Ema3
        {
            get => _ema3;
            set
            {
                _ema3Prev = _ema3;
                SetValue(ref _ema3, value);
            }
        }
        public double Signal
        {
            get => _signal;
            private set
            {
                _prevSignal = double.IsNaN(_signal) ? value : _signal;
                _signal = value;
            }
        }
        public double Macd
        {
            get => _macd;
            set
            {
                _macdPrev = _macd;
                SetValue(ref _macd, value);
            }
        }
        [Bindable]
        public partial int Position { get; set; }
        [Bindable]
        public partial int WorkingQty { get; set; }
        [Bindable]
        public partial double AvgBuyPx { get; set; }
        [Bindable]
        public partial double AvgSellPx { get; set; }
        [Bindable]
        public partial int TotalBuyQty { get; set; }
        [Bindable]
        public partial int TotalSellQty { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial int MaxPos { get; set; }
        [Bindable]
        public partial double MaxUnreal { get; set; }
        [Bindable]
        public partial double MaxRest { get; set; }
        [Bindable(Default = InitSide.Auto)]
        public partial InitSide StockPairBuyInitialSide { get; set; }
        [Bindable(Default = InitSide.Auto)]
        public partial InitSide StockPairSellInitialSide { get; set; }
        [Bindable(Default = ExecutionStyle.Normal)]
        public partial ExecutionStyle StockPairBuyExecStyle { get; set; }
        [Bindable(Default = ExecutionStyle.Normal)]
        public partial ExecutionStyle StockPairSellExecStyle { get; set; }
        [Bindable]
        public partial string StockPairAccount { get; set; }
        [Bindable(Default = "APEX")]
        public partial string StockPairLocate { get; set; }
        [Bindable]
        public partial ObservableCollection<PairOrderModel> PairOrders { get; set; }
        [Bindable]
        public partial PairOrderModel LastPairOrder { get; set; }
        [Bindable]
        public partial int HistoricRequestDays { get; set; }
        [Bindable]
        public partial double PayThroughMkt { get; set; }
        [Bindable]
        public partial bool LoadHistoric { get; set; }

        public ScriptTraderViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            _triggerTimer = new()
            {
                AutoReset = false,
                Interval = 150
            };
            _triggerTimer.Elapsed += OnTriggerTimerElapsed;

            Qty = 1;
            PairOrders = new();

            ScriptTradeType = ScriptTradeType.StockPair;

            EmaConfig = new()
            {
                EmaPeriods = 20
            };
            Ema2Config = new()
            {
                EmaPeriods = 100
            };
            Ema3Config = new()
            {
                EmaPeriods = 200
            };

            EmaCalculator = new(EmaConfig, SubscriptionFieldType.MidPoint);
            Ema2Calculator = new(Ema2Config, SubscriptionFieldType.MidPoint);
            Ema3Calculator = new(Ema3Config, SubscriptionFieldType.MidPoint);

            MacdCalculator = new();

            EntryInterval = 10;
            ExitInterval = 10;
            StopLossInterval = 10;

            MaxPos = 10;
            MaxUnreal = 100;
            MaxRest = 15000;

            MacdCalculator.SignalEmaConfig.EmaPeriods = 7;
            MacdCalculator.FastEmaConfig.EmaPeriods = 14;
            MacdCalculator.SlowEmaConfig.EmaPeriods = 21;

            EmaCalculator.EmaUpdatedEvent += OnEmaUpdated;
            Ema2Calculator.EmaUpdatedEvent += OnEma2Updated;
            Ema3Calculator.EmaUpdatedEvent += OnEma3Updated;
            MacdCalculator.MacdUpdated += OnMacdUpdated;

            _bootstrap = GetTemplateFromResource("bootstrap.py");
            string entryTemplate = GetTemplateFromResource("script_trader_entry_template.py");
            string exitTemplate = GetTemplateFromResource("script_trader_exit_template.py");
            string stopLossTemplate = GetTemplateFromResource("script_trader_stoploss_template.py");

            EntryScript = new(entryTemplate);
            ExitScript = new(exitTemplate);
            StopLossScript = new(stopLossTemplate);

            EntryScript.UpdateFinished += OnEntryScriptUpdate;
            ExitScript.UpdateFinished += OnExitScriptUpdate;
            StopLossScript.UpdateFinished += OnStopLossScriptUpdate;

            OnEntryScriptUpdate(EntryScript, EventArgs.Empty);
            OnExitScriptUpdate(ExitScript, EventArgs.Empty);
            OnStopLossScriptUpdate(StopLossScript, EventArgs.Empty);

            LoadDefaults();
        }

        private static string GetTemplateFromResource(string templateName)
        {
            try
            {
                string template = default;
                Assembly assembly = Assembly.GetExecutingAssembly();
                string resName = assembly.GetManifestResourceNames().FirstOrDefault(str => str.EndsWith(templateName));
                if (resName != null)
                {
                    using Stream stream = assembly.GetManifestResourceStream(resName);
                    using StreamReader reader = new(stream);
                    template = reader.ReadToEnd();
                }
                return template ?? string.Empty;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetTemplateFromResource));
                return string.Empty;
            }
        }

        public void LoadDefaults()
        {
            foreach (string account in StockPairAccounts.ToList())
            {
                if (!OmsCore.User.Accounts.Contains(account))
                {
                    StockPairAccounts.Remove(account);
                }
            }

            if (string.IsNullOrWhiteSpace(StockPairAccount))
            {
                StockPairAccount = StockPairAccounts.FirstOrDefault();
            }
        }

        private void OnEntryScriptUpdate(object sender, EventArgs e)
        {
            if (TryCompileScript(EntryScript.Text, out PyObject compiledScript, out string error))
            {
                _entryScriptCompiled = compiledScript;
                EntryScriptReady = true;
            }
            else
            {
                EntryScriptReady = false;
                EntryScriptError = error;
            }
        }

        private void OnExitScriptUpdate(object sender, EventArgs e)
        {
            if (TryCompileScript(ExitScript.Text, out PyObject compiledScript, out string error))
            {
                _exitScriptCompiled = compiledScript;
                ExitScriptReady = true;
            }
            else
            {
                ExitScriptReady = false;
                ExitScriptError = error;
            }
        }

        private void OnStopLossScriptUpdate(object sender, EventArgs e)
        {
            if (TryCompileScript(StopLossScript.Text, out PyObject compiledScript, out string error))
            {
                _stopLossScriptCompiled = compiledScript;
                StopLossScriptReady = true;
            }
            else
            {
                StopLossScriptReady = false;
                StopLossScriptError = error;
            }
        }

        private bool TryCompileScript(string text, out PyObject compiledScript, out string error)
        {
            try
            {
                using (Py.GIL())
                {
                    compiledScript = PythonEngine.Compile(text);
                    error = string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryCompileScript));
                compiledScript = null;
                error = ex.Message;
                ConsoleOutput += $"{DateTime.Now:dd/MM hh:mm:ss.fff} [CompileScript] [ERR]> {ex.Message}\r\n";
                return false;
            }
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            try
            {
                ScriptTraderConfig config = await ModuleConfigBase.DeserializeAsync<ScriptTraderConfig>(configJson);

                Symbol = config.Symbol;
                ReverseSymbol = config.ReverseSymbol;
                Qty = config.Qty;
                ScriptTradeType = config.ScriptTradeType;
                EntryScript.Text = config.EntryScript;
                ExitScript.Text = config.ExitScript;
                StopLossScript.Text = config.StopLossScript;
                EntryScriptTriggerType = config.EntryScriptTriggerType;
                ExitScriptTriggerType = config.ExitScriptTriggerType;
                StopLossScriptTriggerType = config.StopLossScriptTriggerType;
                EntryInterval = config.EntryInterval;
                ExitInterval = config.ExitInterval;
                StopLossInterval = config.StopLossInterval;
                LoadHistoric = config.LoadHistoric;
                SubscribeToQuotes = config.SubscribeToQuotes;
                SubscribeToGreeks = config.SubscribeToGreeks;
                SubscribeToEma = config.SubscribeToEma;
                SubscribeToMacd = config.SubscribeToMacd;
                MaxPos = config.MaxPos;
                MaxUnreal = config.MaxUnreal;
                MaxRest = config.MaxRest;
                PayThroughMkt = config.PayThroughMkt;
                StockPairBuyInitialSide = config.StockPairBuyInitialSide;
                StockPairSellInitialSide = config.StockPairSellInitialSide;
                StockPairBuyExecStyle = config.StockPairBuyExecStyle;
                StockPairSellExecStyle = config.StockPairSellExecStyle;
                EmaConfig.EmaSmoothing = config.EmaSmoothing;
                EmaConfig.EmaInterval = config.EmaInterval;
                EmaConfig.EmaPeriods = config.EmaPeriods;
                Ema2Config.EmaSmoothing = config.Ema2Smoothing;
                Ema2Config.EmaInterval = config.Ema2Interval;
                Ema2Config.EmaPeriods = config.Ema2Periods;
                Ema3Config.EmaSmoothing = config.Ema3Smoothing;
                Ema3Config.EmaInterval = config.Ema3Interval;
                Ema3Config.EmaPeriods = config.Ema3Periods;
                MacdCalculator.SignalEmaConfig.EmaSmoothing = config.SignalEmaSmoothing;
                MacdCalculator.SignalEmaConfig.EmaInterval = config.SignalEmaInterval;
                MacdCalculator.SignalEmaConfig.EmaPeriods = config.SignalEmaPeriods;
                MacdCalculator.SlowEmaConfig.EmaSmoothing = config.SlowEmaSmoothing;
                MacdCalculator.SlowEmaConfig.EmaInterval = config.SlowEmaInterval;
                MacdCalculator.SlowEmaConfig.EmaPeriods = config.SlowEmaPeriods;
                MacdCalculator.FastEmaConfig.EmaSmoothing = config.FastEmaSmoothing;
                MacdCalculator.FastEmaConfig.EmaInterval = config.FastEmaInterval;
                MacdCalculator.FastEmaConfig.EmaPeriods = config.FastEmaPeriods;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeserializeAndLoadConfig));
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            try
            {
                ScriptTraderConfig config = GetConfig();
                return config.Serialize();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetConfigSerialized));
                return string.Empty;
            }
        }

        public ScriptTraderConfig GetConfig()
        {
            return new()
            {
                Symbol = Symbol,
                ReverseSymbol = ReverseSymbol,
                Qty = Qty,
                ScriptTradeType = ScriptTradeType,
                EntryScript = EntryScript.Text,
                ExitScript = ExitScript.Text,
                StopLossScript = StopLossScript.Text,
                EntryScriptTriggerType = EntryScriptTriggerType,
                ExitScriptTriggerType = ExitScriptTriggerType,
                StopLossScriptTriggerType = StopLossScriptTriggerType,
                EntryInterval = EntryInterval,
                ExitInterval = ExitInterval,
                StopLossInterval = StopLossInterval,
                LoadHistoric = LoadHistoric,
                SubscribeToQuotes = SubscribeToQuotes,
                SubscribeToGreeks = SubscribeToGreeks,
                SubscribeToEma = SubscribeToEma,
                SubscribeToMacd = SubscribeToMacd,
                MaxPos = MaxPos,
                MaxUnreal = MaxUnreal,
                MaxRest = MaxRest,
                PayThroughMkt = PayThroughMkt,
                StockPairBuyInitialSide = StockPairBuyInitialSide,
                StockPairSellInitialSide = StockPairSellInitialSide,
                StockPairBuyExecStyle = StockPairBuyExecStyle,
                StockPairSellExecStyle = StockPairSellExecStyle,
                EmaSmoothing = EmaConfig.EmaSmoothing,
                EmaInterval = EmaConfig.EmaInterval,
                EmaPeriods = EmaConfig.EmaPeriods,
                Ema2Smoothing = Ema2Config.EmaSmoothing,
                Ema2Interval = Ema2Config.EmaInterval,
                Ema2Periods = Ema2Config.EmaPeriods,
                Ema3Smoothing = Ema3Config.EmaSmoothing,
                Ema3Interval = Ema3Config.EmaInterval,
                Ema3Periods = Ema3Config.EmaPeriods,
                SignalEmaSmoothing = MacdCalculator.SignalEmaConfig.EmaSmoothing,
                SignalEmaInterval = MacdCalculator.SignalEmaConfig.EmaInterval,
                SignalEmaPeriods = MacdCalculator.SignalEmaConfig.EmaPeriods,
                SlowEmaSmoothing = MacdCalculator.SlowEmaConfig.EmaSmoothing,
                SlowEmaInterval = MacdCalculator.SlowEmaConfig.EmaInterval,
                SlowEmaPeriods = MacdCalculator.SlowEmaConfig.EmaPeriods,
                FastEmaSmoothing = MacdCalculator.FastEmaConfig.EmaSmoothing,
                FastEmaInterval = MacdCalculator.FastEmaConfig.EmaInterval,
                FastEmaPeriods = MacdCalculator.FastEmaConfig.EmaPeriods,
            };
        }

        public override void SaveViewModelConfig()
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(ScriptTraderConfig)}.json");
                string configJson = GetConfigSerialized();
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
            }
        }

        [Command]
        public async Task LoadCommand()
        {
            if (IsRunning)
            {
                MessageBoxService.ShowMessage($"Stop before reloading!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                return;
            }
            if (_dataSubscriber != null)
            {
                Unload();
            }
            IsReady = false;

            LoadHistoric = true;
            SubscribeToQuotes = true;
            SubscribeToEma = true;
            SubscribeToMacd = true;
            SubscribeToGreeks = false;

            if (string.IsNullOrWhiteSpace(Symbol))
            {
                MessageBoxService.ShowMessage($"Invalid symbol!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                return;
            }
            SymbolCodec decoded = new SymbolCodec(Symbol);
            if (decoded.LegCount <= 0)
            {
                MessageBoxService.ShowMessage($"Invalid symbol!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                return;
            }
            if (ScriptTradeType == ScriptTradeType.StockPair && decoded.LegCount != 2)
            {
                MessageBoxService.ShowMessage($"Invalid symbol count for stock pair!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                return;
            }

            if (LoadHistoric)
            {
                await LoadFromDataBase(decoded);
            }

            if (decoded.LegCount == 1)
            {
                Instrument security = decoded.GetLeg(0);
                if (security != null)
                {
                    List<SubscriptionFieldType> fields = new List<SubscriptionFieldType>() { SubscriptionFieldType.Bid, SubscriptionFieldType.Ask };
                    if (security.instrumentType == InstrumentType.Option)
                    {
                        fields.Add(SubscriptionFieldType.Greeks);
                        fields.Add(SubscriptionFieldType.DeltaAdjTheo);
                        SubscribeToGreeks = true;
                    }
                    DataSubscriber dataSubscriber = new DataSubscriber(OmsCore)
                    {
                        Side = security.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                        Ratio = 1
                    };
                    security.ratio = 1;
                    Symbol = decoded.ToTOS();
                    decoded.Invert();
                    ReverseSymbol = decoded.ToTOS();
                    dataSubscriber.Subscribe(security.symbol, fields);
                    _dataSubscriber = dataSubscriber;
                }
                IsReady = true;
            }
            else if (decoded.LegCount > 1)
            {
                SpreadDataSubscriber spreadDataSubscriber = new SpreadDataSubscriber(OmsCore);
                for (int i = 0; i < decoded.LegCount; i++)
                {
                    Instrument security = decoded.GetLeg(i);
                    if (security != null)
                    {
                        List<SubscriptionFieldType> fields = new List<SubscriptionFieldType>() { SubscriptionFieldType.Bid, SubscriptionFieldType.Ask };
                        if (security.instrumentType == InstrumentType.Option)
                        {
                            if (ScriptTradeType == ScriptTradeType.StockPair)
                            {
                                MessageBoxService.ShowMessage($"Invalid symbol for stock pair!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                                return;
                            }
                            fields.Add(SubscriptionFieldType.Greeks);
                            fields.Add(SubscriptionFieldType.DeltaAdjTheo);
                            SubscribeToGreeks = true;
                        }
                        DataSubscriber dataSubscriber = new DataSubscriber(OmsCore)
                        {
                            Side = security.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                            Ratio = security.ratio
                        };
                        dataSubscriber.Subscribe(security.symbol, fields);
                        dataSubscriber.DataSubscriberUpdated += spreadDataSubscriber.OnDataSubscriberUpdated;
                        spreadDataSubscriber.SubscriptionFields = fields;
                        spreadDataSubscriber.ChildSubscribers.Add(dataSubscriber);
                    }
                }
                List<int> qtyList = spreadDataSubscriber.ChildSubscribers.Select(x => Math.Abs(x.Ratio)).ToList();
                int divisor = 1;
                if (qtyList.Count > 0)
                {
                    List<int> lcdAdjustedList = Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out divisor);
                    for (int index = 0; index < qtyList.Count; ++index)
                    {
                        var pairLegModel = spreadDataSubscriber.ChildSubscribers[index];
                        int adjustedQty = lcdAdjustedList[index];
                        pairLegModel.Ratio = adjustedQty;
                        Instrument security = decoded.GetLeg(index);
                        security.ratio = adjustedQty;
                    }
                }
                Symbol = decoded.ToTOS();
                decoded.Invert();
                ReverseSymbol = decoded.ToTOS();
                _dataSubscriber = spreadDataSubscriber;
                IsReady = true;
            }

            if (IsReady)
            {
                _dataSubscriber.DataSubscriberUpdated += OnDataSubscriberUpdated;
                EmaConfig.EmaEnabled = true;
                Ema2Config.EmaEnabled = true;
                Ema3Config.EmaEnabled = true;
                MacdCalculator.FastEmaConfig.EmaEnabled = true;
                MacdCalculator.SlowEmaConfig.EmaEnabled = true;
                MacdCalculator.SignalEmaConfig.EmaEnabled = true;
            }
        }

        private async Task LoadFromDataBase(SymbolCodec symbolCodec)
        {
            Dictionary<DateTime, IndicatorDataPoints> timestampToModelMap = new();

            List<List<ZeroPlus.Models.Data.Models.BarModel>> buffer = new();
            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);
                var symbol = leg.symbol;
                var start = DateTime.Today - TimeSpan.FromDays(HistoricRequestDays);
                var end = DateTime.Now;
                var bars = await OmsCore.FullEmaClient.Client.RequestBarsAsync(symbol, start, end);

                if (bars.Count == 0 && timestampToModelMap.Count != 0)
                {
                    MessageBoxService?.ShowMessage($"Historic lookup failed on {symbol}");
                    return;
                }

                foreach (var bar in bars)
                {
                    if (!timestampToModelMap.TryGetValue(bar.Timestamp, out var model))
                    {
                        if (i != 0)
                        {
                            continue;
                        }
                        model = new(bar.Timestamp);
                        model.Mid = 0.0;
                        timestampToModelMap[bar.Timestamp] = model;
                    }

                    if (leg.buySell)
                    {
                        model.Mid += leg.ratio * bar.Close;
                    }
                    else
                    {
                        model.Mid -= leg.ratio * bar.Close;
                    }
                }
            }

            List<IndicatorDataPoints> models = timestampToModelMap.Values.OrderBy(x => x.Timestamp).ToList();
            if (models.Any())
            {
                Recalculate(models);

                var last = models.LastOrDefault();
                if (last != null)
                {
                    Ema = last.MidEma;
                    Macd = last.Macd;
                    Signal = last.Signal;
                    EmaCalculator.Prime(last.MidEma);
                    Ema2Calculator.Prime(last.MidEma2);
                    Ema3Calculator.Prime(last.MidEma3);
                    MacdCalculator.Prime(last.FastEma, last.SlowEma, last.Signal);
                }
            }
        }

        private void Recalculate(List<IndicatorDataPoints> points)
        {
            if (points.Any())
            {
                double alpha = EmaConfig.EmaSmoothing / (1 + EmaConfig.EmaPeriods);
                double alpha2 = Ema2Config.EmaSmoothing / (1 + Ema2Config.EmaPeriods);
                double alpha3 = Ema3Config.EmaSmoothing / (1 + Ema3Config.EmaPeriods);

                double fastAlpha = MacdCalculator.FastEmaConfig.EmaSmoothing / (1 + MacdCalculator.FastEmaConfig.EmaPeriods);
                double slowAlpha = MacdCalculator.SlowEmaConfig.EmaSmoothing / (1 + MacdCalculator.SlowEmaConfig.EmaPeriods);
                double signalAlpha = MacdCalculator.SignalEmaConfig.EmaSmoothing / (1 + MacdCalculator.SignalEmaConfig.EmaPeriods);

                double prevBidEma = double.NaN;
                double prevAskEma = double.NaN;
                double prevMidEma = double.NaN;
                double prevMidEma2 = double.NaN;
                double prevMidEma3 = double.NaN;

                double slowEma = double.NaN;
                double fastEma = double.NaN;
                double signalEma = double.NaN;
                double macd = double.NaN;

                DateTime bidEmaStartTime = default;
                DateTime askEmaStartTime = default;
                DateTime midEmaStartTime = default;
                DateTime midEma2StartTime = default;
                DateTime midEma3StartTime = default;

                DateTime slowEmaStartTime = default;
                DateTime fastEmaStartTime = default;
                DateTime signalEmaStartTime = default;

                foreach (IndicatorDataPoints point in points)
                {
                    if (double.IsNaN(prevBidEma))
                    {
                        prevBidEma = point.HighestBid;
                        bidEmaStartTime = point.HighestBidUpdateTime;
                    }
                    if ((point.HighestBidUpdateTime - bidEmaStartTime).TotalMilliseconds >= EmaConfig.EmaInterval)
                    {
                        prevBidEma = (point.HighestBid * alpha) + (prevBidEma * (1 - alpha));
                        bidEmaStartTime = point.HighestBidUpdateTime;
                    }
                    point.BidEma = prevBidEma;

                    if (double.IsNaN(prevAskEma))
                    {
                        prevAskEma = point.LowestAsk;
                        askEmaStartTime = point.LowestAskUpdateTime;
                    }
                    if ((point.LowestAskUpdateTime - askEmaStartTime).TotalMilliseconds >= EmaConfig.EmaInterval)
                    {
                        prevAskEma = (point.LowestAsk * alpha) + (prevAskEma * (1 - alpha));
                        askEmaStartTime = point.LowestAskUpdateTime;
                    }
                    point.AskEma = prevAskEma;

                    double mid = point.Mid;
                    DateTime midUpdateTime = point.Timestamp;

                    if (double.IsNaN(prevMidEma))
                    {
                        prevMidEma = mid;
                        midEmaStartTime = midUpdateTime;
                    }
                    if ((midUpdateTime - midEmaStartTime).TotalMilliseconds >= EmaConfig.EmaInterval)
                    {
                        prevMidEma = (mid * alpha) + (prevMidEma * (1 - alpha));
                        midEmaStartTime = midUpdateTime;
                    }
                    point.MidEma = prevMidEma;

                    if (double.IsNaN(prevMidEma2))
                    {
                        prevMidEma2 = mid;
                        midEma2StartTime = midUpdateTime;
                    }
                    if ((midUpdateTime - midEma2StartTime).TotalMilliseconds >= Ema2Config.EmaInterval)
                    {
                        prevMidEma2 = (mid * alpha2) + (prevMidEma2 * (1 - alpha2));
                        midEma2StartTime = midUpdateTime;
                    }
                    point.MidEma2 = prevMidEma2;

                    if (double.IsNaN(prevMidEma3))
                    {
                        prevMidEma3 = mid;
                        midEma3StartTime = midUpdateTime;
                    }
                    if ((midUpdateTime - midEma3StartTime).TotalMilliseconds >= Ema3Config.EmaInterval)
                    {
                        prevMidEma3 = (mid * alpha3) + (prevMidEma3 * (1 - alpha3));
                        midEma3StartTime = midUpdateTime;
                    }
                    point.MidEma3 = prevMidEma3;

                    if (double.IsNaN(fastEma))
                    {
                        fastEma = mid;
                        fastEmaStartTime = midUpdateTime;
                    }
                    if ((midUpdateTime - fastEmaStartTime).TotalMilliseconds >= MacdCalculator.FastEmaConfig.EmaInterval)
                    {
                        fastEma = (mid * fastAlpha) + (fastEma * (1 - fastAlpha));
                        fastEmaStartTime = midUpdateTime;
                    }
                    point.FastEma = fastEma;

                    if (double.IsNaN(slowEma))
                    {
                        slowEma = mid;
                        slowEmaStartTime = midUpdateTime;
                    }
                    if ((midUpdateTime - slowEmaStartTime).TotalMilliseconds >= MacdCalculator.SlowEmaConfig.EmaInterval)
                    {
                        slowEma = (mid * slowAlpha) + (slowEma * (1 - slowAlpha));
                        slowEmaStartTime = midUpdateTime;

                        macd = fastEma - slowEma;
                        if (double.IsNaN(signalEma))
                        {
                            signalEma = macd;
                            signalEmaStartTime = midUpdateTime;
                        }
                        if ((midUpdateTime - signalEmaStartTime).TotalMilliseconds >= MacdCalculator.SignalEmaConfig.EmaInterval)
                        {
                            signalEma = (macd * signalAlpha) + (signalEma * (1 - signalAlpha));
                            signalEmaStartTime = midUpdateTime;
                        }
                    }
                    point.SlowEma = slowEma;

                    point.Macd = macd;
                    point.Signal = signalEma;
                    point.Bar = macd - signalEma;
                }
            }
        }

        private void Unload()
        {
            if (_dataSubscriber != null)
            {
                _dataSubscriber.DataSubscriberUpdated -= OnDataSubscriberUpdated;
                _dataSubscriber.Unsubscribe();
                _dataSubscriber = null;
            }

            EmaConfig.EmaEnabled = false;
            Ema2Config.EmaEnabled = false;
            Ema3Config.EmaEnabled = false;
            MacdCalculator.FastEmaConfig.EmaEnabled = false;
            MacdCalculator.SlowEmaConfig.EmaEnabled = false;
            MacdCalculator.SignalEmaConfig.EmaEnabled = false;

            EmaCalculator?.Reset();
            Ema2Calculator?.Reset();
            Ema3Calculator?.Reset();
            MacdCalculator?.Reset();

            Bid = double.NaN;
            Mid = double.NaN;
            Ask = double.NaN;
            Delta = double.NaN;
            Theo = double.NaN;
            Ema = double.NaN;
            Ema2 = double.NaN;
            Ema3 = double.NaN;
            Macd = double.NaN;
        }

        [Command]
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            switch (ScriptTradeType)
            {
                case ScriptTradeType.Stock:
                case ScriptTradeType.Option:
                case ScriptTradeType.OptionSpread:
                    MessageBoxService.ShowMessage($"{ScriptTradeType.ToString().FromCamelCase()} not supported yet!", ModuleTitle, MessageButton.OK, MessageIcon.Warning, MessageResult.OK);
                    return;
            }

            IsRunning = true;
            _triggerTimer.Start();
        }

        [Command]
        public void Stop()
        {
            IsRunning = false;
            EntryScriptCountDown = TimeSpan.Zero;
            ExitScriptCountDown = TimeSpan.Zero;
            StopLossScriptCountDown = TimeSpan.Zero;
        }

        [Command]
        public void ClosePositions()
        {
            if (WorkingQty == 0)
            {
                if (Position > 0)
                {
                    SendSellOrder(Bid, Position, PositionEffect.Close);
                }
                else if (Position < 0)
                {
                    SendBuyOrder(Ask, Math.Abs(Position), PositionEffect.Close);
                }
            }
        }

        [Command]
        public void ClearCommand()
        {
            ConsoleOutput = string.Empty;
        }

        [Command]
        public void ExpandScriptCommand(TextDocument script)
        {
            ScriptEditorView view = new();
            if (view.DataContext is ScriptEditorViewModel viewModel)
            {
                viewModel.Script = script;
                view.Show();
            }
        }

        [Command]
        public void ShowEmaSettingsCommand()
        {
            EmaConfigWindowView view = new();
            if (view.DataContext is EmaConfigWindowViewModel viewModel)
            {
                viewModel.EmaConfigViewModel.EmaConfig = EmaConfig;
                viewModel.Ema2ConfigViewModel.EmaConfig = Ema2Config;
                viewModel.Ema3ConfigViewModel.EmaConfig = Ema3Config;
                view.Show();
            }
        }

        [Command]
        public void ResetEmaCommand()
        {
            EmaConfig?.ResetEma();
            Ema2Config?.ResetEma();
            Ema3Config?.ResetEma();
        }

        [Command]
        public void ShowMacdSettingsCommand()
        {
            MacdConfigWindowView view = new();
            if (view.DataContext is MacdConfigWindowViewModel viewModel)
            {
                viewModel.FastEmaConfigViewModel.EmaConfig = MacdCalculator.FastEmaConfig;
                viewModel.SlowEmaConfigViewModel.EmaConfig = MacdCalculator.SlowEmaConfig;
                viewModel.SignalEmaConfigViewModel.EmaConfig = MacdCalculator.SignalEmaConfig;
                view.Show();
            }
        }

        [Command]
        public void ResetMacdCommand()
        {
            MacdCalculator?.Reset();
        }

        [Command]
        public void CancelOrderCommand(PairOrderRequest pairOrder)
        {
            pairOrder.PairOrderRequestType = PairOrderRequestType.Cancel;
            OmsCore.AutoTraderClient.SendPairOrder(pairOrder, this);
        }

        private async void OnTriggerTimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (WorkingQty != 0)
                {
                    _nextEntryTriggerTime = DateTime.Now + TimeSpan.FromSeconds(EntryInterval);
                    _nextExitTriggerTime = DateTime.Now + TimeSpan.FromSeconds(ExitInterval);
                    _nextStopLossTriggerTime = DateTime.Now + TimeSpan.FromSeconds(StopLossInterval);
                    EntryScriptCountDown = TimeSpan.Zero;
                    ExitScriptCountDown = TimeSpan.Zero;
                    StopLossScriptCountDown = TimeSpan.Zero;
                }
                else if (Position == 0)
                {
                    _nextExitTriggerTime = DateTime.Now + TimeSpan.FromSeconds(ExitInterval);
                    _nextStopLossTriggerTime = DateTime.Now + TimeSpan.FromSeconds(StopLossInterval);
                    ExitScriptCountDown = TimeSpan.Zero;
                    StopLossScriptCountDown = TimeSpan.Zero;

                    if (EntryScriptTriggerType == ScriptTriggerType.Timer)
                    {
                        if (_nextEntryTriggerTime <= DateTime.Now)
                        {
                            await CheckForTriggersAsync(ScriptTriggerType.Timer, PositionEffect.Open);
                            _nextEntryTriggerTime = DateTime.Now + TimeSpan.FromSeconds(EntryInterval);
                        }
                        EntryScriptCountDown = _nextEntryTriggerTime - DateTime.Now;
                    }
                    else
                    {
                        _nextEntryTriggerTime = DateTime.Now + TimeSpan.FromSeconds(EntryInterval);
                        EntryScriptCountDown = TimeSpan.Zero;
                    }
                }
                else
                {
                    _nextEntryTriggerTime = DateTime.Now + TimeSpan.FromSeconds(EntryInterval);
                    EntryScriptCountDown = TimeSpan.Zero;

                    if (ExitScriptTriggerType == ScriptTriggerType.Timer)
                    {
                        if (_nextExitTriggerTime <= DateTime.Now)
                        {
                            await CheckForTriggersAsync(ScriptTriggerType.Timer, PositionEffect.Close);
                            _nextExitTriggerTime = DateTime.Now + TimeSpan.FromSeconds(ExitInterval);
                        }
                        ExitScriptCountDown = _nextExitTriggerTime - DateTime.Now;
                    }
                    else
                    {
                        _nextExitTriggerTime = DateTime.Now + TimeSpan.FromSeconds(ExitInterval);
                        ExitScriptCountDown = TimeSpan.Zero;
                    }

                    if (StopLossScriptTriggerType == ScriptTriggerType.Timer)
                    {
                        if (_nextStopLossTriggerTime <= DateTime.Now)
                        {
                            await CheckForTriggersAsync(ScriptTriggerType.Timer, PositionEffect.Close);
                            _nextStopLossTriggerTime = DateTime.Now + TimeSpan.FromSeconds(StopLossInterval);
                        }
                        StopLossScriptCountDown = _nextStopLossTriggerTime - DateTime.Now;
                    }
                    else
                    {
                        _nextStopLossTriggerTime = DateTime.Now + TimeSpan.FromSeconds(StopLossInterval);
                        StopLossScriptCountDown = TimeSpan.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OnTriggerTimerElapsed));
            }
            finally
            {
                CheckForCancel();
                if (IsRunning)
                {
                    _triggerTimer.Start();
                }
                else
                {
                    Stop();
                }
            }
        }

        private void CheckForCancel()
        {
            try
            {
                if ((DateTime.Now - _lastCancelCheck).TotalMilliseconds >= 1000)
                {
                    _lastCancelCheck = DateTime.Now;
                    if (ScriptTradeType == ScriptTradeType.StockPair)
                    {
                        if (MaxRest > 1000)
                        {
                            List<PairOrderModel> orders = PairOrders.Where(x => IsResting(x) && (DateTime.Now - x.LastUpdateTime).TotalMilliseconds > MaxRest).ToList();
                            foreach (var order in orders)
                            {
                                order.Reason = "Cxl From Timer";
                                CancelOrderCommand(order.OrderRequest);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CheckForCancel));
            }
        }

        private static bool IsResting(PairOrderModel order)
        {
            return order.OrderRequest.PairOrderRequestType != PairOrderRequestType.Cancel &&
                   (order.OrderStatus == OrderStatus.New || order.OrderStatus == OrderStatus.PendingNew);
        }

        private async void OnDataSubscriberUpdated(DataSubscriber dataSubscriber, SubscriptionFieldType updatedField)
        {
            if (dataSubscriber == _dataSubscriber)
            {
                switch (updatedField)
                {
                    case SubscriptionFieldType.Bid:
                    case SubscriptionFieldType.Ask:
                        Bid = dataSubscriber.Bid;
                        Ask = dataSubscriber.Ask;
                        Mid = (dataSubscriber.Bid + dataSubscriber.Ask) / 2;
                        EmaCalculator.AddUpdate(Mid);
                        Ema2Calculator.AddUpdate(Mid);
                        Ema3Calculator.AddUpdate(Mid);
                        MacdCalculator.AddUpdate(Mid);
                        await CheckForTriggersAsync(ScriptTriggerType.QuoteUpdate, PositionEffect.AUTO);
                        UpdatePnl();
                        break;
                    case SubscriptionFieldType.Greeks:
                        Delta = dataSubscriber.Delta;
                        Theo = dataSubscriber.Theo;
                        await CheckForTriggersAsync(ScriptTriggerType.GreekUpdate, PositionEffect.AUTO);
                        break;
                }
            }
        }

        private async void OnEmaUpdated(double ema)
        {
            Ema = ema;
            await CheckForTriggersAsync(ScriptTriggerType.EmaUpdate, PositionEffect.AUTO);
        }

        private async void OnEma2Updated(double ema)
        {
            Ema2 = ema;
            await CheckForTriggersAsync(ScriptTriggerType.EmaUpdate, PositionEffect.AUTO);
        }

        private async void OnEma3Updated(double ema)
        {
            Ema3 = ema;
            await CheckForTriggersAsync(ScriptTriggerType.EmaUpdate, PositionEffect.AUTO);
        }

        private async void OnMacdUpdated(double macd, double signal, double bar)
        {
            Signal = signal;
            Macd = macd;
            await CheckForTriggersAsync(ScriptTriggerType.MacdUpdate, PositionEffect.AUTO);
        }

        private async Task CheckForTriggersAsync(ScriptTriggerType type, PositionEffect positionEffect)
        {
            if (IsRunning)
            {
                if (WorkingQty == 0)
                {
                    if (Position == 0 && (positionEffect == PositionEffect.Open || positionEffect == PositionEffect.AUTO))
                    {
                        if (EntryScriptReady && (EntryScriptTriggerType == type || (EntryScriptTriggerType == ScriptTriggerType.AnyDataUpdate && type != ScriptTriggerType.Timer)))
                        {
                            await RunScriptAsync(_entryScriptCompiled, ENTRY_FUNC_NAME, PositionEffect.Open);
                        }
                    }
                    else if (positionEffect == PositionEffect.Close || positionEffect == PositionEffect.AUTO)
                    {
                        bool stoplossExecuted = false;
                        if (ExitScriptReady && (StopLossScriptTriggerType == type || (StopLossScriptTriggerType == ScriptTriggerType.AnyDataUpdate && type != ScriptTriggerType.Timer)))
                        {
                            stoplossExecuted = await RunScriptAsync(_stopLossScriptCompiled, STOPLOSS_FUNC_NAME, PositionEffect.Close);
                        }
                        if (!stoplossExecuted && StopLossScriptReady && (ExitScriptTriggerType == type || (ExitScriptTriggerType == ScriptTriggerType.AnyDataUpdate && type != ScriptTriggerType.Timer)))
                        {
                            await RunScriptAsync(_exitScriptCompiled, EXIT_FUNC_NAME, PositionEffect.Close);
                        }
                    }
                }
            }
        }

        private async Task<bool> RunScriptAsync(PyObject compiledScript, string funcName, PositionEffect type)
        {
            lock (_codeBlockLock)
            {
                if (CodeRunning)
                {
                    return false;
                }
                else
                {
                    CodeRunning = true;
                }
            }
            return await Task.Run(() => RunScript(compiledScript, funcName, type));
        }

        private bool RunScript(PyObject compiledScript, string funcName, PositionEffect type)
        {
            try
            {
                Dictionary<string, object> dataJson = GetData();
                using (Py.GIL())
                {
                    using PyModule scope = Py.CreateScope();
                    if (!string.IsNullOrWhiteSpace(_bootstrap))
                    {
                        scope.Exec(_bootstrap);
                    }

                    dynamic setConsoleOut = scope.Get("set_console_out");
                    Action<string> writeCallbackFn = (string message) =>
                    {
                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            ConsoleOutput += $"{DateTime.Now:dd/MM hh:mm:ss.fff} [{funcName}]> {message}\r\n";
                        }
                    };
                    setConsoleOut(writeCallbackFn);

                    scope.Execute(compiledScript);
                    dynamic func = scope.Get(funcName);
                    dynamic ret = func(dataJson);
                    string output = (string)ret;
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        switch (output.ToUpper())
                        {
                            case "BUY":
                                return SendBuyOrder(Ask, Qty, type);
                            case "SELL":
                                return SendSellOrder(Bid, Qty, type);
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                ConsoleOutput += $"{DateTime.Now:dd/MM hh:mm:ss.fff} [{funcName}] [ERR]> {ex.Message}\r\n";
                _log.Error(ex, nameof(RunScript));
                return false;
            }
            finally
            {
                CodeRunning = false;
            }
        }

        private bool SendBuyOrder(double price, int qty, PositionEffect type)
        {
            int curQty = Position + WorkingQty;
            int finalQty = qty + curQty;
            if (finalQty > curQty && Math.Abs(finalQty) > MaxPos)
            {
                ConsoleOutput += $"{DateTime.Now:dd/MM hh:mm:ss.fff} [Risk Check]> Max pos limit reached. Pos: {Position}, Wrk: {WorkingQty}\r\n";
                Status = "Max pos limit reached!";
                StatusMode = StatusMode.CancelledSell;
                return false;
            }

            switch (ScriptTradeType)
            {
                case ScriptTradeType.Stock:
                    break;
                case ScriptTradeType.StockPair:
                    return SendBuyPair(price + PayThroughMkt, qty, type) != null;
                case ScriptTradeType.Option:
                    break;
                case ScriptTradeType.OptionSpread:
                    break;
            }

            return false;
        }

        private bool SendSellOrder(double price, int qty, PositionEffect type)
        {
            int curPos = Position + WorkingQty;
            int finalQty = curPos - qty;
            if (finalQty < curPos && Math.Abs(finalQty) > MaxPos)
            {
                ConsoleOutput += $"{DateTime.Now:dd/MM hh:mm:ss.fff} [Risk Check]> Max pos limit reached. Pos: {Position}, Wrk: {WorkingQty}\r\n";
                ContraStatus = "Max pos limit reached!";
                ContraStatusMode = StatusMode.CancelledSell;
                return false;
            }

            switch (ScriptTradeType)
            {
                case ScriptTradeType.Stock:
                    break;
                case ScriptTradeType.StockPair:
                    return SendSellPair(price - PayThroughMkt, qty, type) != null;
                case ScriptTradeType.Option:
                    break;
                case ScriptTradeType.OptionSpread:
                    break;
            }

            return false;
        }

        private PairOrderModel SendBuyPair(double triggerValue, int qty, PositionEffect type, string tag = "")
        {
            try
            {
                if (DateTime.Now.TimeOfDay >= _closeTime.TimeOfDay)
                {
                    Dispatcher?.BeginInvoke(() => MessageBoxService?.ShowMessage("Outside Trading Hours!"));
                    return null;
                }
                else if (double.IsNaN(triggerValue))
                {
                    Status = "Invalid Trigger Value!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }
                else if (qty <= 0)
                {
                    Status = "Invalid Qty!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }
                if (_dataSubscriber is not SpreadDataSubscriber spreadDataSubscriber)
                {
                    Status = "Invalid Symbol!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }

                Status = "";
                StatusMode = StatusMode.Reset;

                string orderId = OrderClient.OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId1 = OrderClient.OPENING_ID + OmsCore.OrderClient.GetNextOrderId();
                string orderId2 = OrderClient.OPENING_ID + OmsCore.OrderClient.GetNextOrderId();

                DataSubscriber pairLeg1 = spreadDataSubscriber.ChildSubscribers[0];
                DataSubscriber pairLeg2 = spreadDataSubscriber.ChildSubscribers[1];

                Side leg1Side = pairLeg1.Side;
                Side leg2Side = pairLeg2.Side;

                int leg1Qty = qty * pairLeg1.Ratio;
                int leg2Qty = qty * pairLeg2.Ratio;

                TriggerMethod triggerMethod = TriggerMethod.SBS;
                triggerValue = Math.Round(triggerValue, 4);

                PairOrderRequest pairOrder = new()
                {
                    Account = StockPairAccount,
                    TriggerMethod = triggerMethod.ToString(),
                    TriggerValue = triggerValue,
                    Style = StockPairBuyExecStyle.ToString().ToUpper(),
                    ClientOrderId = orderId,
                    TriggerValueCurrency = Currency.LOCAL.ToString().ToUpper(),
                    InitSide = StockPairBuyInitialSide,
                    Locate = StockPairLocate,

                    ClientOrderIdLeg1 = orderId1,
                    Leg1Symbol = pairLeg1.Symbol,
                    Leg1Side = leg1Side == ZeroPlus.Models.Data.Enums.Side.Sell && Position >= 0 ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg1Side,
                    Leg1Quantity = leg1Qty,

                    ClientOrderIdLeg2 = orderId2,
                    Leg2Symbol = pairLeg2.Symbol,
                    Leg2Side = leg2Side == ZeroPlus.Models.Data.Enums.Side.Sell && Position >= 0 ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg2Side,
                    Leg2Quantity = leg2Qty,

                    TimeInForce = TimeInForce.DAY,

                    BuyTermsRatio = leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? pairLeg1.Ratio : pairLeg2.Ratio,
                    SellTermsRatio = leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? pairLeg1.Ratio : pairLeg2.Ratio,
                };

                WorkingQty += qty;

                return SendOrder(pairOrder, qty, ZeroPlus.Models.Data.Enums.Side.Buy, type, tag);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendBuyPair));
                return null;
            }
        }

        private PairOrderModel SendSellPair(double triggerValue, int qty, PositionEffect type, string tag = "")
        {
            try
            {
                if (DateTime.Now.TimeOfDay >= _closeTime.TimeOfDay)
                {
                    Dispatcher?.BeginInvoke(() => MessageBoxService?.ShowMessage("Outside Trading Hours!"));
                    return null;
                }
                else if (double.IsNaN(triggerValue))
                {
                    Status = "Invalid Trigger Value!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }
                else if (qty <= 0)
                {
                    Status = "Invalid Qty!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }
                if (_dataSubscriber is not SpreadDataSubscriber spreadDataSubscriber)
                {
                    Status = "Invalid Symbol!";
                    StatusMode = StatusMode.CancelledSell;
                    return null;
                }

                ContraStatus = "";
                ContraStatusMode = StatusMode.Reset;

                string contraOrderId = OrderClient.CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();
                string contraOrderId1 = OrderClient.CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();
                string contraOrderId2 = OrderClient.CLOSING_ID + OmsCore.OrderClient.GetNextOrderId();

                DataSubscriber pairLeg1 = spreadDataSubscriber.ChildSubscribers[0];
                DataSubscriber pairLeg2 = spreadDataSubscriber.ChildSubscribers[1];

                int leg1Qty = qty * pairLeg1.Ratio;
                int leg2Qty = qty * pairLeg2.Ratio;

                Side leg1Side = pairLeg1.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                Side leg2Side = pairLeg2.Side == ZeroPlus.Models.Data.Enums.Side.Buy ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;

                TriggerMethod triggerMethod = TriggerMethod.SSB;
                triggerValue = Math.Round(triggerValue, 4);

                PairOrderRequest pairOrder = new()
                {
                    Account = StockPairAccount,
                    TriggerMethod = triggerMethod.ToString(),
                    TriggerValue = triggerValue,
                    Style = StockPairSellExecStyle.ToString().ToUpper(),
                    ClientOrderId = contraOrderId,
                    TriggerValueCurrency = Currency.LOCAL.ToString().ToUpper(),
                    InitSide = StockPairSellInitialSide,
                    Locate = StockPairLocate,

                    ClientOrderIdLeg1 = contraOrderId1,
                    Leg1Symbol = pairLeg1.Symbol,
                    Leg1Side = leg1Side == ZeroPlus.Models.Data.Enums.Side.Sell && Position <= 0 ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg1Side,
                    Leg1Quantity = leg1Qty,

                    ClientOrderIdLeg2 = contraOrderId2,
                    Leg2Symbol = pairLeg2.Symbol,
                    Leg2Side = leg2Side == ZeroPlus.Models.Data.Enums.Side.Sell && Position <= 0 ? ZeroPlus.Models.Data.Enums.Side.SellShort : leg2Side,
                    Leg2Quantity = leg2Qty,

                    TimeInForce = TimeInForce.DAY,

                    BuyTermsRatio = leg1Side == ZeroPlus.Models.Data.Enums.Side.Buy ? pairLeg1.Ratio : pairLeg2.Ratio,
                    SellTermsRatio = leg2Side == ZeroPlus.Models.Data.Enums.Side.Buy ? pairLeg1.Ratio : pairLeg2.Ratio,
                };

                WorkingQty -= qty;

                return SendOrder(pairOrder, qty, ZeroPlus.Models.Data.Enums.Side.Sell, type, tag);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SendSellPair));
                return null;
            }
        }

        private PairOrderModel SendOrder(PairOrderRequest pairOrder, int qty, Side side, PositionEffect type, string tag = "")
        {
            pairOrder.PairOrderRequestType = PairOrderRequestType.Send;
            PairOrderModel orderModel = new()
            {
                Symbol = side == ZeroPlus.Models.Data.Enums.Side.Buy ? Symbol : ReverseSymbol,
                TriggerValue = pairOrder.TriggerValue,
                TriggerMode = pairOrder.TriggerMethod,
                Quantity = qty,
                Tag = tag,
            };

            _orderIdToOrderModelMap[pairOrder.ClientOrderId] = orderModel;

            if (DateTime.Now.TimeOfDay >= _closeTime.TimeOfDay)
            {
                orderModel.OrderStatus = OrderStatus.Rejected;
                orderModel.Reason = "Outside Trading Hours!";
            }
            else
            {
                OmsCore.AutoTraderClient.SendPairOrder(pairOrder, this);
            }

            orderModel.Init(pairOrder, side, type);
            Dispatcher.BeginInvoke(() =>
            {
                PairOrders.Add(orderModel);
                LastPairOrder = orderModel;
            });
            return orderModel;
        }

        private Dictionary<string, object> GetData()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            if (SubscribeToQuotes)
            {
                data["Bid"] = Math.Round(_bid, 2);
                data["PrevBid"] = Math.Round(_bidPrev, 2);

                data["Mid"] = Math.Round(_mid, 2);
                data["PrevMid"] = Math.Round(_midPrev, 2);

                data["Ask"] = Math.Round(_ask, 2);
                data["PrevAsk"] = Math.Round(_askPrev, 2);
            }
            if (SubscribeToGreeks)
            {
                data["Delta"] = Math.Round(_delta, 4);
                data["PrevDelta"] = Math.Round(_deltaPrev, 4);

                data["Theo"] = Math.Round(_theo, 4);
                data["PrevTheo"] = Math.Round(_theoPrev, 4);
            }
            if (SubscribeToEma)
            {
                data["Ema"] = Math.Round(_ema, 3);
                data["PrevEma"] = Math.Round(_emaPrev, 3);

                data["Ema2"] = Math.Round(_ema2, 3);
                data["PrevEma2"] = Math.Round(_ema2Prev, 3);

                data["Ema3"] = Math.Round(_ema3, 3);
                data["PrevEma3"] = Math.Round(_ema3Prev, 3);
            }
            if (SubscribeToMacd)
            {
                data["Signal"] = Math.Round(_signal, 6);
                data["PrevSignal"] = Math.Round(_prevSignal, 6);

                data["Macd"] = Math.Round(_macd, 6);
                data["PrevMacd"] = Math.Round(_macdPrev, 6);
            }
            data["Position"] = Position;
            data["WorkingQty"] = WorkingQty;
            data["AvgBuyPx"] = Math.Round(AvgBuyPx, 2);
            data["AvgSellPx"] = Math.Round(AvgSellPx, 2);
            data["TotalBuyQty"] = TotalBuyQty;
            data["TotalSellQty"] = TotalSellQty;
            data["RealPnl"] = RealPnl;
            data["UnrealPnl"] = UnrealPnl;
            return data;
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
            // Pass
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
        }

        public void OrderUpdated(OrderUpdateValues update)
        {
            try
            {
                if (_orderIdToOrderModelMap.TryGetValue(update.ParentLocalOrderId, out PairOrderModel orderModel))
                {
                    orderModel.Update(update);
                    bool isClosed = update.OrderStatus.IsClosed();
                    bool packageUpdate = orderModel.Legs.All(x => x.OrderStatus == orderModel.OrderStatus);

                    if (packageUpdate)
                    {
                        if (isClosed)
                        {
                            switch (orderModel.Side)
                            {
                                case ZeroPlus.Models.Data.Enums.Side.Buy:
                                    Position += orderModel.Filled;
                                    WorkingQty -= (orderModel.Filled + orderModel.Leaves);
                                    break;
                                case ZeroPlus.Models.Data.Enums.Side.Sell:
                                    Position -= orderModel.Filled;
                                    WorkingQty += (orderModel.Filled + orderModel.Leaves);
                                    break;
                            }

                            int qty = orderModel.Filled;
                            if (qty != 0)
                            {
                                double avgPx = orderModel.AvgFillPx;
                                Side side = orderModel.Side;
                                switch (side)
                                {
                                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                                        AvgBuyPx = ((AvgBuyPx * TotalBuyQty) + avgPx * qty) / (TotalBuyQty + qty);
                                        TotalBuyQty += qty;
                                        break;
                                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                                        AvgSellPx = ((AvgSellPx * TotalSellQty) + avgPx * qty) / (TotalSellQty + qty);
                                        TotalSellQty += qty;
                                        break;
                                }

                                TradeUnit singleTrade = new()
                                {
                                    Quantity = 1,
                                    Price = avgPx,
                                    TotalPrice = avgPx,
                                    NetPrice = avgPx,
                                };
                                for (int i = 0; i < qty; i++)
                                {
                                    if (side is ZeroPlus.Models.Data.Enums.Side.Sell or ZeroPlus.Models.Data.Enums.Side.SellShort)
                                    {
                                        _sells.Push(singleTrade);
                                    }
                                    else
                                    {
                                        _buys.Push(singleTrade);
                                    }
                                }

                                UpdatePnl();
                                StampValues(orderModel);
                            }
                        }
                    }

                    if (update.ParentLocalOrderId.StartsWith(OrderClient.OPENING_ID))
                    {
                        Status = update.Status;
                        StatusMode = update.StatusMode;
                    }
                    else if (update.ParentLocalOrderId.StartsWith(OrderClient.CLOSING_ID))
                    {
                        ContraStatus = update.Status;
                        ContraStatusMode = update.StatusMode;
                    }
                }
                else
                {
                    _log.Warn($"{nameof(OrderUpdated)} ordermodel not found! Id: {update.ParentLocalOrderId}, Symbol: {Symbol}");
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(OrderUpdated));
            }
        }

        public void AutomationStateChanged(bool running)
        {
        }

        private void UpdatePnl()
        {
            UpdateRealPnl();
            UpdateUnrealPnl();
        }

        private void UpdateRealPnl()
        {
            try
            {
                while (!_buys.IsEmpty && !_sells.IsEmpty)
                {
                    if (_sells.TryPeek(out TradeUnit sell))
                    {
                        if (_buys.TryPeek(out TradeUnit buy))
                        {
                            double netPnl = sell.NetPrice - buy.NetPrice;
                            RealPnl += netPnl;
                            _sells.TryPop(out _);
                            _buys.TryPop(out _);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateRealPnl));
            }
        }

        private void UpdateUnrealPnl()
        {
            int pos = Position;
            if (pos > 0)
            {
                if (!_buys.IsEmpty)
                {
                    double avgPx = _buys.Sum(x => x.Price) / Math.Abs(pos);
                    UnrealPnl = (Mid - avgPx) * pos;
                    CheckForRisk();
                    return;
                }
            }
            else if (pos < 0)
            {
                if (!_sells.IsEmpty)
                {
                    double avgPx = _sells.Sum(x => x.Price) / Math.Abs(pos);
                    UnrealPnl = (avgPx - Mid) * Math.Abs(pos);
                    CheckForRisk();
                    return;
                }
            }

            UnrealPnl = 0;
        }

        private void CheckForRisk()
        {
            if (IsRunning)
            {
                if (UnrealPnl < 0 && Math.Abs(UnrealPnl) > Math.Abs(MaxUnreal))
                {
                    Stop();
                    ClosePositions();
                }
            }
        }

        private void StampValues(PairOrderModel orderModel)
        {
            orderModel.Bid = Bid;
            orderModel.Ask = Ask;
            orderModel.Mid = Mid;
            orderModel.MidEma = Ema;
        }
    }
}
