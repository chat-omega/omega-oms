using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Microsoft.ML.OnnxRuntime;
using Newtonsoft.Json;
using NLog;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Trading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Services;
using ZeroPlus.TagCodecLib;
using Side = ZeroPlus.Models.Data.Enums.Side;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ModelTraderViewModel : CustomizableTableViewModelBase, IOmsDataSubscriber, IOmsOrderUpdateSubscriber
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentStack<TradeUnit> _sells = new();
        private readonly ConcurrentStack<TradeUnit> _buys = new();

        private const string SHARE_IP = @"\\192.168.60.12";
        private readonly Dictionary<string, DoubleUpdateModel> _symbolToLastUpdateMap;
        private readonly Dictionary<string, List<DoubleUpdateModel>> _symbolToUpdatesListMap;
        private readonly List<DoubleUpdateModel> _updatesList;

        private double _bidAtFill;
        private double _askAtFill;


        private TimeSpan _countDown;
        private readonly ConcurrentQueue<DoubleUpdateModel> _barBuffer = new();
        private DispatcherTimer _countDownTimer;

        private string _pythonCode;
        private bool _pausedByPartialFill;
        private int _resubmitAttempt;
        private readonly object _lock = new();


        private Python.Runtime.PyObject _compiledScript;
        private double _lastFillBuyPx;
        private double _lastFillSellPx;
        private Side _lastSide;
        private DateTime _lastEntryTime;
        private double _lastResult;
        private bool _blockLastResult;
        private string _path;
        private List<string> _paths = new();

        private double _scalerMin;
        private double _scalerMax = 1;
        private SymbolCodec _symbolCodec;
        private readonly ConcurrentDictionary<OrderStatus, ConcurrentDictionary<string, OMSExecReport>> _statusToUpdatesMap = new();

        public string Uid { get; internal set; }
        public string Name { get; internal set; }
        public Dispatcher Dispatcher { get; internal set; }
        public bool IsDisposed { get; set; }

        public bool IsHedgeModel { get; private set; }
        public FastObservableCollection<DoubleUpdateModel> Updates { get; set; }
        public ObservableCollection<OrderModel> Orders { get; set; }
        private long[] InputSizes => new long[] { 1, _inputShape, 6 };

        [Bindable(Default = "ML Stock Trader")]
        public partial string ModuleTitle { get; set; }

        [Bindable(Default = "")]
        public partial string Underlying { get; set; }

        [Bindable]
        public partial TimeSpan CountDown { get; set; }

        [Bindable]
        public partial DoubleUpdateModel LastUpdate { get; set; }

        [Bindable]
        public partial OrderModel LastOrderModel { get; set; }

        [Bindable]
        public partial string LoadedModelName { get; set; }
        [Bindable(Default = 2)]
        public partial double StopLoss { get; set; }
        [Bindable(Default = 1)]
        public partial int Quantity { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double StopLossTarget { get; set; }
        [Bindable(Default = LiquidityType.Make)]
        public partial LiquidityType LiquidityType { get; set; }
        [Bindable(Default = 10)]
        public partial double AddLiquidityRestPeriod { get; set; }
        [Bindable(Default = 1)]
        public partial double Interval { get; set; }
        [Bindable(Default = 35)]
        public partial double CacheInterval { get; set; }
        [Bindable(Default = 45)]
        public partial double AutoCloseInterval { get; set; }
        [Bindable(Default = .04)]
        public partial double AutoCloseMinWidth { get; set; }
        [Bindable(Default = 10)]
        public partial double DownAutoCloseInterval { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Bid { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Ask { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Low { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double High { get; set; }
        [Bindable(Default = double.NaN)]
        public partial double Mid { get; set; }
        [Bindable]
        public partial bool HedgeEnabled { get; set; }
        [Bindable(Default = 60)]
        public partial double CandlePeriod { get; set; }
        [Bindable]
        public partial double Beta { get; set; }
        [Bindable(Default = 30)]
        public partial long InputShape { get; set; }
        [Bindable]
        public partial ZeroPlus.Models.Data.Enums.OrderType OrderType { get; set; }
        [Bindable]
        public partial ModelType ModelType { get; set; }
        [Bindable]
        public partial string ModelStatus { get; set; }
        [Bindable]
        public partial double ModelPrevResult { get; set; }
        [Bindable]
        public partial string StatusSell { get; set; }
        [Bindable]
        public partial string Status { get; set; }
        [Bindable]
        public partial StatusMode StatusModeSell { get; set; }
        [Bindable]
        public partial StatusMode StatusMode { get; set; }
        [Bindable]
        public partial int WorkingQty { get; set; }
        [Bindable]
        public partial int TotalQty { get; set; }
        [Bindable]
        public partial int FilledQty { get; set; }
        [Bindable]
        public partial double NetPnl { get; set; }
        [Bindable]
        public partial double UnrealPnl { get; set; }
        [Bindable]
        public partial double RealPnl { get; set; }
        [Bindable]
        public partial double RealPnlPerShare { get; set; }
        [Bindable]
        public partial bool OrderEnabled { get; set; }
        [Bindable]
        public partial bool SimulationEnabled { get; set; }
        [Bindable]
        public partial double AvgSell { get; set; }
        [Bindable]
        public partial int SellQty { get; set; }
        [Bindable]
        public partial double AvgBuy { get; set; }
        [Bindable]
        public partial int BuyQty { get; set; }
        [Bindable]
        public partial ModelTraderViewModel HedgeTrader { get; set; }

        public string InstanceId => "MLT-" + OmsCore.User.Username + "-" + Underlying;
        public OmsCore OmsCore { get; }
        public IEnumerable<ZeroPlus.Models.Data.Enums.OrderType> OrderTypes { get; } = ((ZeroPlus.Models.Data.Enums.OrderType[])Enum.GetValues(typeof(ZeroPlus.Models.Data.Enums.OrderType))).ToList();
        public IEnumerable<ModelType> ModelTypes { get; } = ((ModelType[])Enum.GetValues(typeof(ModelType))).ToList();
        public IEnumerable<LiquidityType> LiquidityTypes { get; } = ((LiquidityType[])Enum.GetValues(typeof(LiquidityType))).ToList();
        protected ILoadCustomColumnService LoadCustomColumnService => GetService<ILoadCustomColumnService>();
        protected IGetItemsByVisualOrderService GetItemsByVisualOrderService => GetService<IGetItemsByVisualOrderService>();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();
        protected ICurrentWindowService WindowService => GetService<ICurrentWindowService>();
        public DateTime NextInterval { get; private set; }
        public OrderSubType? SubType { get; set; }
        public ModelTradersManagerModel ManagerModel { get; }

        public ModelTraderViewModel(ModelTradersManagerModel managerModel, OmsCore omsCore)
        {
            OmsCore = omsCore;
            _symbolToUpdatesListMap = new Dictionary<string, List<DoubleUpdateModel>>();
            _symbolToLastUpdateMap = new Dictionary<string, DoubleUpdateModel>();
            _updatesList = new List<DoubleUpdateModel>();
            Updates = new FastObservableCollection<DoubleUpdateModel>();
            Orders = new ObservableCollection<OrderModel>();
            CountDown = TimeSpan.FromMinutes(Interval);
            ManagerModel = managerModel;
            ManagerModel?.AddTrader(this);
            Beta = 1;
            HedgeTrader = new ModelTraderViewModel(true, OmsCore);
        }

        public ModelTraderViewModel(bool isHedgeModel, OmsCore omsCore)
        {
            OmsCore = omsCore;
            _symbolToUpdatesListMap = new Dictionary<string, List<DoubleUpdateModel>>();
            _symbolToLastUpdateMap = new Dictionary<string, DoubleUpdateModel>();
            _updatesList = new List<DoubleUpdateModel>();
            IsHedgeModel = isHedgeModel;
            Updates = new FastObservableCollection<DoubleUpdateModel>();
            Orders = new ObservableCollection<OrderModel>();
            CountDown = TimeSpan.FromMinutes(Interval);
            Beta = 1;
        }

        private void CountDownTimerTick(object sender, EventArgs e)
        {
            _countDownTimer.Stop();
            UpdateBar();
            if (HedgeEnabled)
            {
                HedgeTrader?.UpdateBar();
            }

            if (NextInterval > DateTime.Now)
            {
                CountDown = NextInterval - DateTime.Now;
                _countDownTimer.Start();
                if (!_blockLastResult)
                {
                    if ((DownAutoCloseInterval > 0 && UnrealPnl < 0 && (DateTime.Now - _lastEntryTime).TotalMinutes > DownAutoCloseInterval) ||
                        (AutoCloseInterval > 0 && (DateTime.Now - _lastEntryTime).TotalMinutes > AutoCloseInterval))
                    {
                        if (_symbolCodec.LegCount < 2 || (Ask - Bid) < AutoCloseMinWidth)
                        {
                            if (FilledQty != 0)
                            {
                                _blockLastResult = true;
                                ClosePositions();
                            }
                        }
                    }
                }
            }
            else
            {
                CountDown = TimeSpan.FromMinutes(Interval);
                NextInterval = DateTime.Now + TimeSpan.FromMinutes(Interval);
                Task.Run(() => Process());
            }
        }

        private void UpdateBar()
        {
            if (_barBuffer.Count > 0)
            {
                List<DoubleUpdateModel> list = new();

                while (_barBuffer.Count > 0)
                {
                    if (_barBuffer.TryDequeue(out DoubleUpdateModel item))
                    {
                        if (DateTime.Today != item.Timestamp.Date ||
                            _lastUpdate.Timestamp - item.Timestamp <= TimeSpan.FromMinutes(CacheInterval))
                        {
                            list.Add(item);
                        }
                    }
                }

                Updates.AddRange(list);
                LastUpdate = list.LastOrDefault();
            }
        }

        [Command]
        public void SearchHedgeSymbolCommand()
        {
            if (!string.IsNullOrWhiteSpace(HedgeTrader.Underlying) && HedgeTrader != null)
            {
                if (HedgeEnabled)
                {
                    HedgeTrader.Subscribe();
                }
                else
                {
                    HedgeTrader.Unsubscribe();
                }
            }
        }

        [Command]
        public async Task SearchUnderlyingCommand()
        {
            await Task.Run(() =>
            {
                Unsubscribe();
                if (!string.IsNullOrWhiteSpace(Underlying))
                {
                    Subscribe();
                    string file = Underlying.ToLower();
                    switch (ModelType)
                    {
                        case ModelType.Python:
                            file += "_interface.py";
                            _path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", file);
                            if (File.Exists(_path))
                            {
                                _pythonCode = File.ReadAllText(_path);
                                LoadedModelName = file;
                                Subscribe();
                                NextInterval = DateTime.Now + TimeSpan.FromMinutes(Interval);
                                _countDownTimer.Start();
                            }
                            else
                            {
                                LoadedModelName = "";
                                ModelStatus = "Model not found!";
                            }
                            break;
                        case ModelType.ONNX:
                            _path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", file + ".onnx");
                            if (File.Exists(_path))
                            {
                                _paths.Add(_path);

                                for (int i = 1; i < 5; i++)
                                {
                                    string path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", file + i + ".onnx");
                                    if (File.Exists(path))
                                    {
                                        _paths.Add(path);
                                    }
                                }

                                LoadedModelName = file;
                                Subscribe();
                                NextInterval = DateTime.Now + TimeSpan.FromMinutes(Interval);
                                _countDownTimer.Start();
                            }
                            else
                            {
                                LoadedModelName = "";
                                ModelStatus = "Model not found!";
                            }
                            break;
                    }
                }
            });
        }

        [Command]
        public async Task SearchModelCommand()
        {
            await Task.Run(() =>
            {
                Unsubscribe();
                if (!string.IsNullOrWhiteSpace(Underlying))
                {
                    string file = LoadedModelName;
                    switch (ModelType)
                    {
                        case ModelType.Python:
                            _path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", file);
                            if (File.Exists(_path))
                            {
                                _pythonCode = File.ReadAllText(_path);

                                LoadedModelName = file;
                                Subscribe();
                                NextInterval = DateTime.Now + TimeSpan.FromMinutes(Interval);
                                _countDownTimer.Start();
                            }
                            else
                            {
                                LoadedModelName = "";
                                ModelStatus = "Model not found!";
                            }
                            break;
                        case ModelType.ONNX:
                            file += ".onnx";
                            _path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", file);
                            if (File.Exists(_path))
                            {
                                TryLoadScalerMinMax();

                                LoadedModelName = file;
                                Subscribe();
                                NextInterval = DateTime.Now + TimeSpan.FromMinutes(Interval);
                                _countDownTimer.Start();
                            }
                            else
                            {
                                LoadedModelName = "";
                                ModelStatus = "Model not found!";
                            }
                            break;
                    }
                }
            });
        }

        [Command]
        public void LiquidateCommand()
        {
            MessageResult response = MessageBoxService.ShowMessage("Are you sure you want to close all positions?", Underlying, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No);
            if (response == MessageResult.Yes)
            {
                OrderEnabled = false;
                ClosePositions();
            }
        }

        [Command]
        public void Activate()
        {
            WindowService?.Show();
            WindowService?.Activate();
        }

        [Command]
        public void Hide()
        {
            WindowService?.Hide();
        }

        [Command]
        public void Close()
        {
            WindowService?.Close();
        }

        private void Subscribe()
        {
            _symbolToUpdatesListMap.Clear();
            _symbolToLastUpdateMap.Clear();
            _updatesList.Clear();
            _symbolCodec = new SymbolCodec(Underlying);
            if (_symbolCodec != null)
            {
                for (int i = 0; i < _symbolCodec.LegCount; i++)
                {
                    Instrument leg = _symbolCodec.GetLeg(i);
                    _symbolToUpdatesListMap[leg.symbol] = new List<DoubleUpdateModel>();
                    OmsCore.UpdateManager.Subscribe(leg.symbol, SubscriptionFieldType.Bar, this);
                    OmsCore.QuoteClient.Subscribe(leg.symbol, SubscriptionFieldType.Low, this);
                    OmsCore.QuoteClient.Subscribe(leg.symbol, SubscriptionFieldType.High, this);
                }
            }
        }

        private void Unsubscribe()
        {
            _paths.Clear();
            _symbolToUpdatesListMap.Clear();
            _symbolToLastUpdateMap.Clear();
            _updatesList.Clear();
            ModelStatus = "";
            OrderEnabled = false;
            _countDownTimer?.Stop();
            CountDown = TimeSpan.FromMinutes(Interval);
            OmsCore.UpdateManager.UnsubscribeAll(this);
            OmsCore.QuoteClient.UnsubscribeAll(this);
            Dispatcher.BeginInvoke(() =>
            {
                Updates.Clear();
            });
        }

        private void Process()
        {
            try
            {
                DoubleUpdateModel lastUpdate = _lastUpdate;
                List<DoubleUpdateModel> updates = new();
                if (lastUpdate != null)
                {

                    for (int i = _updatesList.Count - 1; i >= 0; i--)
                    {
                        DoubleUpdateModel item = _updatesList[i];
                        TimeSpan timeSpan = lastUpdate.Timestamp - item.Timestamp;
                        bool passes = timeSpan > TimeSpan.FromMinutes(CacheInterval);
                        if (DateTime.Today != item.Timestamp.Date)
                        {
                            passes = timeSpan > TimeSpan.FromHours(17) + TimeSpan.FromMinutes(CacheInterval);
                            if (lastUpdate.Timestamp.Date.DayOfWeek == DayOfWeek.Monday)
                            {
                                passes = timeSpan > TimeSpan.FromDays(2) + TimeSpan.FromHours(17) + TimeSpan.FromMinutes(CacheInterval);
                            }
                        }
                        if (passes)
                        {
                            int count = updates.OrderBy(x => x.Timestamp).GroupBy(x => new DateTime(x.Timestamp.Year, x.Timestamp.Month, x.Timestamp.Day, x.Timestamp.Hour, x.Timestamp.Minute, 0)).Count();
                            if (count >= CacheInterval)
                            {
                                break;
                            }
                        }
                        updates.Add(item);
                    }


                    TimeSpan candleSpan = TimeSpan.FromSeconds(CandlePeriod);
                    IEnumerable<IGrouping<DateTime, DoubleUpdateModel>> grouped = updates
                        .OrderBy(x => x.Timestamp)
                        .GroupBy(x => GetGroupKey(x.Timestamp, candleSpan));

                    List<BarModel> bars = new();
                    BarModel prevClose = null;
                    foreach (IGrouping<DateTime, DoubleUpdateModel> group in grouped)
                    {
                        IOrderedEnumerable<DoubleUpdateModel> orderd = group.OrderByDescending(x => x.Timestamp);
                        DoubleUpdateModel last = orderd.LastOrDefault();
                        BarModel newBar = new()
                        {
                            Open = prevClose == null ? double.NaN : prevClose.Close,
                            Close = last.Mid,
                            High = orderd.Max(x => x.Mid),
                            Low = orderd.Min(x => x.Mid),
                            Time = group.Key,
                        };
                        prevClose = newBar;
                        if (!double.IsNaN(newBar.Open))
                        {
                            bars.Add(newBar);
                        }
                    }

                    ModelStatus = "Loading Model";
                    switch (ModelType)
                    {
                        case ModelType.Python:
                            RunPythonModel(bars);
                            break;
                        case ModelType.ONNX:
                            RunOnnxModel(bars);
                            break;
                    }
                }
            }
            finally
            {
                _countDownTimer.Start();
            }
        }

        private static DateTime GetGroupKey(DateTime timestamp, TimeSpan timespan)
        {
            return timestamp.Subtract(TimeSpan.FromTicks(timestamp.TimeOfDay.Ticks % timespan.Ticks));
        }

        private void RunOnnxModel(List<BarModel> bars)
        {
            try
            {
                if (bars.Count > 0)
                {
                    double[] arg = bars.SelectMany(x => new[] { x.Open, x.High, x.Low, x.Close, x.Volume, (x.Time.Hour * 60) + x.Time.Minute }).ToArray();
                    arg = MinMaxScaler(arg);

                    List<double> signals = new();
                    Dictionary<string, double> predLog = new();
                    for (int i = 0; i < _paths.Count; i++)
                    {
                        string modelPath = _paths[i];
                        using InferenceSession infSession = new(modelPath);
                        using RunOptions runOptions = new();
                        using OrtValue inputOrtValue = OrtValue.CreateTensorValueFromMemory(arg, InputSizes);
                        Dictionary<string, OrtValue> input = new()
                        {
                            { "input", inputOrtValue }
                        };
                        IDisposableReadOnlyCollection<OrtValue> outputs = infSession.Run(runOptions, input, infSession.OutputNames);
                        OrtValue output = outputs.FirstOrDefault();
                        if (output != null)
                        {
                            ReadOnlySpan<float> readOnlySpan = output.GetTensorDataAsSpan<float>();
                            if (readOnlySpan.Length > 0)
                            {
                                signals.Add(readOnlySpan[0]);
                                predLog[$"model {i + 1}"] = readOnlySpan[0];
                            }
                        }
                    }
                    double close = arg[^3];
                    int signal = signals.Average(x => x) >= close ? 1 : -1;
                    predLog["last period close"] = close;

                    ProcessSignal(signal);
                    ModelStatus = "Success.";
                    ModelPrevResult = signal;
                    LogPrediction(predLog);
                }
            }
            catch (Exception ex)
            {
                ModelStatus = ex.Message;
            }
        }

        private void LogPrediction(Dictionary<string, double> predLog)
        {
            try
            {
                string path = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", "model_logs.json");
                Dictionary<string, Dictionary<string, Dictionary<string, double>>> log;
                if (File.Exists(path))
                {
                    string content = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        log = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, double>>>>(content);
                    }
                    else
                    {
                        log = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
                    }
                }
                else
                {
                    log = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
                }

                if (!log.TryGetValue(Underlying, out Dictionary<string, Dictionary<string, double>> timeMap))
                {
                    timeMap = new Dictionary<string, Dictionary<string, double>>();
                    log[Underlying] = timeMap;
                }

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffffff");
                timeMap[now] = predLog;
                string export = JsonConvert.SerializeObject(log);
                File.WriteAllText(path, export);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LogPrediction));
            }
        }

        private void TryLoadScalerMinMax()
        {
            try
            {
                string scalerInputPath = Path.Combine(SHARE_IP, "zeroplusshared", "EdgeToTheoModels", "Scaler.json");

                if (File.Exists(scalerInputPath))
                {
                    string content = File.ReadAllText(scalerInputPath);
                    Dictionary<string, Dictionary<string, double>> keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, double>>>(content);
                    if (keyValuePairs != null)
                    {
                        if (keyValuePairs.TryGetValue(Underlying.ToUpper(), out Dictionary<string, double> map) && map != null)
                        {
                            if (map.TryGetValue("Min", out double min))
                            {
                                _scalerMin = min;
                            }
                            if (map.TryGetValue("Max", out double max))
                            {
                                _scalerMax = max;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(TryLoadScalerMinMax));
            }
        }

        public double[] MinMaxScaler(double[] arr)
        {
            double inputMin = arr.Min();
            double inputMax = arr.Max();
            double m = (_scalerMax - _scalerMin) / (inputMax - inputMin);
            double c = _scalerMin - (inputMin * m);
            double[] output = new double[arr.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = (arr[i] * m) + c;
            }
            return output;
        }

        private void RunPythonModel(List<BarModel> bars)
        {
            try
            {
                List<double[]> arg = bars.Select(x => new[] { x.Open, x.High, x.Low, x.Close, x.Volume }).ToList();
                using (Python.Runtime.Py.GIL())
                {
                    using Python.Runtime.PyModule scope = Python.Runtime.Py.CreateScope();
                    _compiledScript = Python.Runtime.PythonEngine.Compile(_pythonCode);
                    scope.Execute(_compiledScript);
                    dynamic func = scope.Get("run_model");
                    dynamic signal = func(arg);
                    if (double.TryParse(signal.ToString(), out double result))
                    {
                        if (!double.IsNaN(result))
                        {
                            if (result is 0 or (-1) or 1)
                            {
                                ProcessSignal(result);
                            }
                            ModelStatus = "Success.";
                            ModelPrevResult = result;
                        }
                        else
                        {
                            ModelStatus = signal.ToString();
                        }
                    }
                    else
                    {
                        ModelStatus = signal.ToString();
                    }
                }

                string message = "Open, High, Low, Close, Volume \n";
                foreach (double[] bar in arg)
                {
                    for (int i = 0; i < bar.Length; i++)
                    {
                        double item = bar[i];
                        if (i == bar.Length - 1)
                        {
                            message += item.ToString("N0");
                        }
                        else
                        {
                            message += item.ToString("N2");
                            message += ", ";
                        }
                    }
                    message += "\n";
                }
                message += "Result: " + ModelStatus + "\nOutput: " + ModelPrevResult;
                _log.Info(message);
            }
            catch (Python.Runtime.PythonException ex)
            {
                ModelStatus = ex.Message;
            }
            catch (Exception ex)
            {
                ModelStatus = ex.Message;
            }
        }

        private void ProcessSignal(double signal)
        {
            if (OrderEnabled)
            {
                if (_blockLastResult)
                {
                    if (_lastResult == signal)
                    {
                        return;
                    }
                    else
                    {
                        _blockLastResult = false;
                    }
                }

                switch (signal)
                {
                    case -1:
                        if (WorkingQty == 0 && FilledQty == 0)
                        {
                            _lastResult = signal;
                            SendOpeningOrder(-Quantity);
                        }
                        else if (FilledQty > 0)
                        {
                            _lastResult = signal;
                            SendOrder(-2 * Quantity);
                        }
                        break;
                    case 0:
                        if (FilledQty > 0)
                        {
                            _lastResult = signal;
                            SendOrder(-Quantity);
                        }
                        else if (FilledQty < 0)
                        {
                            _lastResult = signal;
                            SendOrder(Quantity);
                        }
                        break;
                    case 1:
                        if (WorkingQty == 0 && FilledQty == 0)
                        {
                            _lastResult = signal;
                            SendOpeningOrder(Quantity);
                        }
                        else if (FilledQty < 0)
                        {
                            _lastResult = signal;
                            SendOrder(2 * Quantity);
                        }
                        break;
                }

                if (HedgeEnabled && HedgeTrader != null)
                {
                    HedgeTrader.OrderEnabled = OrderEnabled;
                    HedgeTrader.SimulationEnabled = SimulationEnabled;
                    HedgeTrader.Quantity = (int)Math.Max(1, Math.Floor(Quantity * Math.Abs(Beta)));
                    HedgeTrader.ProcessSignal(signal * -1);
                }
            }
        }

        internal bool Dispose()
        {
            Unsubscribe();
            ManagerModel?.RemoveTrader(this);
            HedgeTrader?.Dispose();
            return false;
        }

        internal void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            HedgeTrader.Dispatcher = dispatcher;
            _countDownTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, CountDownTimerTick, dispatcher);
            _countDownTimer.Stop();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            SubscriptionFieldType type = key.Type;
            switch (type)
            {
                case SubscriptionFieldType.Bar when value is DoubleUpdateModel bar:
                    if (((bar.Timestamp.Date != DateTime.Today) || bar.Timestamp - DateTime.Now <= TimeSpan.FromMinutes(CacheInterval)) && bar.Timestamp.TimeOfDay > TimeSpan.FromHours(8.5))
                    {
                        if (_lastUpdate == null || _lastUpdate.Bid != bar.Bid || _lastUpdate.Ask != bar.Ask)
                        {
                            _barBuffer.Enqueue(bar);
                        }
                        _lastUpdate = bar;
                        if (_symbolToUpdatesListMap.TryGetValue(symbol, out List<DoubleUpdateModel> list))
                        {
                            list.Add(bar);
                            _symbolToLastUpdateMap[symbol] = bar;
                            Update();
                        }
                    }
                    break;
            }
        }

        private void Update()
        {
            if (_symbolCodec.LegCount == _symbolToLastUpdateMap.Count)
            {
                DateTime timestamp = default;
                double spreadBid = 0;
                double spreadAsk = 0;
                int totalBidSize = 0;
                int totalAskSize = 0;
                for (int i = 0; i < _symbolCodec.LegCount; i++)
                {
                    Instrument leg = _symbolCodec.GetLeg(i);
                    if (_symbolToLastUpdateMap.TryGetValue(leg.symbol, out DoubleUpdateModel lastUpdate))
                    {
                        int side = leg.buySell ? 1 : -1;
                        double ratioAbs = Math.Abs(leg.ratio);
                        double bid = ratioAbs * lastUpdate.Bid;
                        double ask = ratioAbs * lastUpdate.Ask;
                        timestamp = lastUpdate.Timestamp;
                        totalBidSize += lastUpdate.BidSize;
                        totalAskSize += lastUpdate.AskSize;
                        if (side == 1)
                        {
                            spreadBid += side * bid;
                            spreadAsk += side * ask;
                        }
                        else
                        {
                            spreadBid += side * ask;
                            spreadAsk += side * bid;
                        }
                    }
                    else
                    {
                        return;
                    }
                }

                Bid = spreadBid;
                Ask = spreadAsk;
                Mid = (spreadBid + spreadAsk) / 2;

                DoubleUpdateModel UpdateModel = new(timestamp, spreadBid, spreadAsk, Generated.QuoteChangeType.None, Generated.QuoteChangeType.None, totalBidSize, totalAskSize, double.NaN);
                _updatesList.Add(UpdateModel);

                CheckForStopLoss();

                UpdateNetPnl();
            }
        }

        private void CheckForStopLoss()
        {
            if (WorkingQty == 0 && FilledQty != 0 && !IsHedgeModel)
            {
                lock (_lock)
                {
                    if (FilledQty > 0 && WorkingQty == 0)
                    {
                        if (StopLossTarget != 0 && !double.IsNaN(StopLossTarget))
                        {
                            double changeInBid = Bid - _bidAtFill;
                            if (Bid <= StopLossTarget)
                            {
                                _blockLastResult = true;
                                ClosePositions();
                            }
                            else if (_lastSide == Side.Buy && changeInBid >= 0.01)
                            {
                                _bidAtFill = Bid;
                                StopLossTarget += changeInBid;
                            }
                        }
                    }
                    else if (FilledQty < 0 && WorkingQty == 0)
                    {
                        if (StopLossTarget != 0 && !double.IsNaN(StopLossTarget))
                        {
                            double changeInAsk = _askAtFill - Ask;
                            if (Ask >= StopLossTarget)
                            {
                                _blockLastResult = true;
                                ClosePositions();
                            }
                            else if (_lastSide == Side.Sell && changeInAsk >= 0.01)
                            {
                                _askAtFill = Ask;
                                StopLossTarget -= changeInAsk;
                            }
                        }
                    }
                }
            }
        }

        public void OrderInfoUpdated(OrderInfoUpdate update)
        {
        }

        public void OrderUpdated(OrderUpdateValues orderUpdate)
        {
        }

        public void HandleExecutionReport(OrderUpdateModel execReport, DateTime receiveTime)
        {
            OrderStatus? orderStatus = execReport.OrderStatus;
            ExecutionType? executionType = execReport.ExecutionType;

            if (orderStatus is not { } status)
            {
                return;
            }

            if (_symbolCodec.LegCount == 1)
            {
                int lastQty = execReport.LastQty;
                int orderQty = execReport.Qty;
                int cumQty = execReport.CumQty;
                int leavesQty = execReport.LeavesQty;
                double avePx = execReport.AvgPrice;
                HandleUpdate(executionType, status, lastQty, avePx, orderQty, cumQty, leavesQty);
            }
            else
            {
                if (!_statusToUpdatesMap.TryGetValue(status, out ConcurrentDictionary<string, OMSExecReport> legToLastUpdateMap))
                {
                    legToLastUpdateMap = new ConcurrentDictionary<string, OMSExecReport>();
                    _statusToUpdatesMap[status] = legToLastUpdateMap;
                }
                if (legToLastUpdateMap.Count == _symbolCodec.LegCount)
                {
                    int lastQty = 0;
                    int orderQty = 0;
                    int cumQty = 0;
                    int leavesQty = 0;

                    List<int> qtyList = legToLastUpdateMap.Select(x => Math.Abs(x.Value.LastQty)).ToList();
                    if (qtyList.Count > 0)
                    {
                        Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out lastQty);
                    }

                    qtyList = legToLastUpdateMap.Select(x => Math.Abs(x.Value.Qty)).ToList();
                    if (qtyList.Count > 0)
                    {
                        Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out orderQty);
                    }

                    qtyList = legToLastUpdateMap.Select(x => Math.Abs(x.Value.CumQty)).ToList();
                    if (qtyList.Count > 0)
                    {
                        Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out cumQty);
                    }

                    qtyList = legToLastUpdateMap.Select(x => Math.Abs(x.Value.LeavesQty)).ToList();
                    if (qtyList.Count > 0)
                    {
                        Comms.Models.Math.Helper.GetLCDAdjustedList(qtyList, out leavesQty);
                    }

                    double avgPx = 0;

                    for (int i = 0; i < _symbolCodec.LegCount; i++)
                    {
                        Instrument leg = _symbolCodec.GetLeg(i);
                        if (legToLastUpdateMap.TryGetValue(leg.symbol, out OMSExecReport legExecReport))
                        {
                            if (leg.buySell)
                            {
                                avgPx += legExecReport.AvePx * legExecReport.LastQty;
                            }
                            else
                            {
                                avgPx -= legExecReport.AvePx * legExecReport.LastQty;
                            }
                        }
                        else
                        {
                            return;
                        }
                    }

                    avgPx /= lastQty;

                    HandleUpdate(executionType, status, lastQty, avgPx, orderQty, cumQty, leavesQty);

                    legToLastUpdateMap.Clear();
                }
            }

            ParseOrderUpdate(execReport, _lastSide);
        }

        public void AutomationStateChanged(bool running)
        {
        }

        private void HandleUpdate(ExecutionType? executionType, OrderStatus status, int lastQty, double avePx, int orderQty, int cumQty, int leavesQty)
        {
            if (executionType != null && executionType.Value.IsFilled())
            {
                TotalQty += lastQty;
                int fillQty = _lastSide == Side.Sell ? -Math.Abs(lastQty) : Math.Abs(lastQty);
                if (_lastSide == ZeroPlus.Models.Data.Enums.Side.Sell)
                {
                    int qty = lastQty;
                    _lastFillSellPx = avePx;
                    AvgSell = ((AvgSell * SellQty) + (_lastFillSellPx * qty)) / (SellQty + qty);
                    SellQty += qty;
                }
                else
                {
                    int qty = lastQty;
                    _lastFillBuyPx = avePx;
                    AvgBuy = ((AvgBuy * BuyQty) + (_lastFillBuyPx * qty)) / (BuyQty + qty);
                    BuyQty += qty;
                }

                lock (_lock)
                {
                    FilledQty += fillQty;
                    WorkingQty -= fillQty;
                    if (FilledQty != 0)
                    {
                        switch (_lastSide)
                        {
                            case Side.Sell:
                                _askAtFill = Ask;
                                StopLossTarget = _lastFillSellPx + StopLoss;
                                break;
                            default:
                                _bidAtFill = Bid;
                                StopLossTarget = _lastFillBuyPx - StopLoss;
                                break;
                        }
                    }
                    else
                    {
                        StopLossTarget = double.NaN;
                    }
                }

                TradeUnit singleTrade = new()
                {
                    Quantity = 1,
                    Price = avePx,
                    TotalPrice = avePx,
                    NetPrice = avePx,
                };
                for (int i = 0; i < lastQty; i++)
                {
                    if (_lastSide == Side.Sell)
                    {
                        _sells.Push(singleTrade);
                    }
                    else
                    {
                        _buys.Push(singleTrade);
                    }
                }

                UpdateNetPnl();
            }
            else if (status is OrderStatus.Canceled or
                     OrderStatus.Rejected)
            {
                int qty = orderQty - cumQty;
                qty = _lastSide == Side.Sell ? -Math.Abs(qty) : Math.Abs(qty);
                lock (_lock)
                {
                    WorkingQty -= qty;
                }

                if (OrderEnabled)
                {
                    _pausedByPartialFill = true;
                    OrderEnabled = false;
                }

                int leaves = leavesQty;
                if (_resubmitAttempt++ < 3)
                {
                    if (_lastSide != Side.Buy)
                    {
                        leaves = -leaves;
                    }
                    SendOrder(leaves);
                }

                if (FilledQty != 0 || (LiquidityType != LiquidityType.Make))
                {
                    Dispatcher.BeginInvoke(() => MessageBoxService.ShowMessage("Order Failed!\nSymbol: " + Underlying + "\nQty: " + leaves + "\nSide: " + _lastSide));
                }
            }

            if (status == OrderStatus.Filled)
            {
                _resubmitAttempt = 0;
                if (_pausedByPartialFill && !OrderEnabled)
                {
                    _pausedByPartialFill = false;
                    OrderEnabled = true;
                }
            }
        }

        private void UpdateNetPnl()
        {
            while (!_buys.IsEmpty && !_sells.IsEmpty)
            {
                if (_sells.TryPop(out TradeUnit sell))
                {
                    if (_buys.TryPop(out TradeUnit buy))
                    {
                        double netPnl = sell.NetPrice - buy.NetPrice;
                        RealPnl += netPnl;
                    }
                    else
                    {
                        _sells.Push(sell);
                    }
                }
            }

            double openPositionAveragePrice = 0.0;
            if (!_sells.IsEmpty)
            {
                openPositionAveragePrice += _sells.Sum(x => x.Price);
            }
            if (!_buys.IsEmpty)
            {
                openPositionAveragePrice -= _buys.Sum(x => x.Price);
            }
            openPositionAveragePrice = FilledQty != 0 ? Math.Abs(openPositionAveragePrice / Math.Abs(FilledQty)) : 0;

            if (FilledQty < 0)
            {
                UnrealPnl = (openPositionAveragePrice - Ask) * Math.Abs(FilledQty);
            }
            else if (FilledQty > 0)
            {
                UnrealPnl = (Bid - openPositionAveragePrice) * FilledQty;
            }
            else
            {
                UnrealPnl = 0;
            }
            if (TotalQty > 0)
            {
                RealPnlPerShare = RealPnl / TotalQty;
            }
            NetPnl = RealPnl + UnrealPnl;
        }

        private void ParseOrderUpdate(OrderUpdateModel execReport, Side side)
        {
            int inverter = 1;

            bool isBuySide = side == Side.Buy;
            if (isBuySide)
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        Status = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Reset;
                        break;
                    case OrderStatus.PendingNew:
                        Status = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Pending;
                        break;
                    case OrderStatus.PartiallyFilled:
                        Status = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusMode = StatusMode.NewBuy;
                        break;
                    case OrderStatus.Filled:
                        Status = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusMode = StatusMode.FilledBuy;
                        break;
                    case OrderStatus.Canceled:
                        Status = execReport.CumQty == 0 && execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        StatusMode = StatusMode.CancelledBuy;
                        break;
                    case OrderStatus.Rejected:
                        Status = $"Rejected {execReport.Message}";
                        StatusMode = StatusMode.RejectedBuy;
                        break;
                    case OrderStatus.Replaced:
                        Status = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusMode = StatusMode.Reset;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    Status = $"Cancel Rejected {execReport.Message}";
                    StatusMode = StatusMode.RejectedBuy;
                }
            }
            else
            {
                switch (execReport.OrderStatus)
                {
                    case OrderStatus.New:
                        StatusSell = $"Order Placed - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Reset;
                        break;
                    case OrderStatus.PendingNew:
                        StatusSell = $"Placing Order - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Pending;
                        break;
                    case OrderStatus.PartiallyFilled:
                        StatusSell = $"Partially Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####} - " +
                                 $"Remaining: {execReport.LeavesQty}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusModeSell = StatusMode.NewSell;
                        break;
                    case OrderStatus.Filled:
                        StatusSell = $"Filled {execReport.CumQty} " +
                                 $"@ {execReport.AvgPrice * inverter:#,###.00####}" +
                                 $"{(!string.IsNullOrWhiteSpace(execReport.LastExchange) ? " On " + execReport.LastExchange : "")}";
                        StatusModeSell = StatusMode.FilledSell;
                        break;
                    case OrderStatus.Canceled:
                        StatusSell = execReport.CumQty == 0
                                                 ? $"Canceled - {execReport.Qty:n0} @ {execReport.Price * inverter}"
                                                 : $"Canceled - Partially Filled {(execReport.CumQty)} " +
                                                   $"@ {((execReport.AvgPrice * inverter).ToString("#,###.00####"))}";
                        StatusModeSell = StatusMode.CancelledSell;
                        break;
                    case OrderStatus.Rejected:
                        StatusSell = $"Rejected {execReport.Message}";
                        StatusModeSell = StatusMode.RejectedSell;
                        break;
                    case OrderStatus.Replaced:
                        StatusSell = $"Replaced - {execReport.Qty:n0} @ {execReport.Price * inverter}";
                        StatusModeSell = StatusMode.Reset;
                        break;
                }

                if (execReport.IsCancelReject)
                {
                    StatusSell = $"Cancel Rejected {execReport.Message}";
                    StatusModeSell = isBuySide ? StatusMode.RejectedBuy : StatusMode.RejectedSell;
                }
            }
        }

        public void HandleOrderCancelReject(OMSOrderCancelReject orderCancelReject)
        {
            Status = $"Cancel Rejected {orderCancelReject.Comment}";
            StatusMode = StatusMode.RejectedBuy;
        }

        internal void ClosePositions()
        {
            lock (_lock)
            {
                if (FilledQty != 0)
                {
                    int qty = -FilledQty;
                    SendOrder(qty);
                }
            }

            if (HedgeEnabled && HedgeTrader != null)
            {
                HedgeTrader.OrderEnabled = OrderEnabled;
                HedgeTrader.ClosePositions();
            }
        }

        private void SendOpeningOrder(int qty)
        {
            double price;
            Side side;

            if (qty < 0)
            {
                side = Side.Sell;
                price = LiquidityType == LiquidityType.Make ? Ask : Bid;
            }
            else
            {
                side = Side.Buy;
                price = LiquidityType == LiquidityType.Make ? Bid : Ask;
            }

            SendOrder(qty, price, side);
        }

        private void SendOrder(int qty)
        {
            double price;
            Side side;

            if (qty < 0)
            {
                side = Side.Sell;
                price = Bid;
            }
            else
            {
                side = Side.Buy;
                price = Ask;
            }

            SendOrder(qty, price, side);
        }

        private void SendOrder(int qty, double price, Side side)
        {
            _lastSide = side;
            if (SimulationEnabled)
            {
                SubmitSimOrder(qty, price, side);
            }
            else
            {
                SubmitLiveOrder(qty, side);
            }

            _lastEntryTime = DateTime.Now;
            OrderModel orderModel = new()
            {
                Symbol = Underlying,
                Price = price,
                Qty = Math.Abs(qty),
                Side = qty > 0 ? Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                Timestamp = DateTime.Now
            };

            Dispatcher?.BeginInvoke(() => Orders.Add(orderModel));
        }

        private void SubmitSimOrder(int qty, double price, Side side)
        {
            _lastSide = side;
            qty = Math.Abs(qty);
            TotalQty += qty;
            int fillQty = _lastSide == Side.Sell ? -Math.Abs(qty) : Math.Abs(qty);
            if (_lastSide == Side.Sell)
            {
                _lastFillSellPx = price;
                AvgSell = ((AvgSell * SellQty) + (_lastFillSellPx * qty)) / (SellQty + qty);
                SellQty += qty;
            }
            else
            {
                _lastFillBuyPx = price;
                AvgBuy = ((AvgBuy * BuyQty) + (_lastFillBuyPx * qty)) / (BuyQty + qty);
                BuyQty += qty;
            }

            lock (_lock)
            {
                FilledQty += fillQty;
                if (FilledQty != 0)
                {
                    switch (_lastSide)
                    {
                        case Side.Sell:
                            _askAtFill = Ask;
                            StopLossTarget = _lastFillSellPx + StopLoss;
                            break;
                        default:
                            _bidAtFill = Bid;
                            StopLossTarget = _lastFillBuyPx - StopLoss;
                            break;
                    }
                }
                else
                {
                    StopLossTarget = double.NaN;
                }
            }

            TradeUnit singleTrade = new()
            {
                Quantity = 1,
                Price = price,
                TotalPrice = price,
                NetPrice = price,
            };
            for (int i = 0; i < qty; i++)
            {
                if (_lastSide == Side.Sell)
                {
                    _sells.Push(singleTrade);
                }
                else
                {
                    _buys.Push(singleTrade);
                }
            }

            UpdateNetPnl();

            bool isBuySide = side == Side.Buy;
            Status = $"Filled {qty} " +
                     $"@ {price:#,###.00####} " +
                     $"On SIM";
            StatusMode = isBuySide ? StatusMode.FilledBuy : StatusMode.FilledSell;
        }

        private void SubmitLiveOrder(int qty, Side side)
        {
            for (int i = 0; i < _symbolCodec.LegCount; i++)
            {
                Instrument leg = _symbolCodec.GetLeg(i);
                if (_symbolToLastUpdateMap.TryGetValue(leg.symbol, out DoubleUpdateModel updateModels))
                {
                    Side legSide = leg.buySell ? side : side == Side.Buy ? Side.Sell : Side.Buy;

                    double price;
                    if (legSide == Side.Sell)
                    {
                        price = LiquidityType == LiquidityType.Make ? updateModels.Ask : updateModels.Bid;
                    }
                    else
                    {
                        price = LiquidityType == LiquidityType.Make ? updateModels.Bid : updateModels.Ask;
                    }

                    var order = BuildOrder(leg.symbol, price, qty * leg.ratio, legSide, true);
                    OmsCore.OrderClient.SendOrder(order, null, this, false, 1);
                }
                StopLossTarget = double.NaN;
            }
            WorkingQty += qty;
        }

        internal OpsOrderModel BuildOrder(string symbol, double price, int qty, Side side, bool isOpening)
        {
            double pxDiff = 0.0;

            var route = OmsCore.Config.DefaultHedgeRoute(OmsCore.Config.InstanceModeV3);
            string tif = ZeroPlus.Models.Data.Enums.TimeInForce.DAY.ToString();
            if (DateTime.Now.TimeOfDay > new TimeSpan(15, 0, 0))
            {
                tif = route.StartsWith("D") ?
                    ZeroPlus.Models.Data.Enums.TimeInForce.GTX.ToString() :
                    ZeroPlus.Models.Data.Enums.TimeInForce.ETH.ToString();
            }

            double interval = isOpening && LiquidityType == LiquidityType.Make ? AddLiquidityRestPeriod * 1000 : 0.0;

            var order = new OpsOrderModel()
            {
                Symbol = symbol,
                Qty = Math.Abs(qty),
                OMSSide = side.ToString(),
                OpenClose = "Auto",
                Price = price,
                Account = OmsCore.Config.DefaultAccount,
                Tif = tif,
                Route = route,
                OMSOrderType = OrderType.ToString().ToUpper(),
                Timestamp = DateTime.Now,
                UnderlyingSymbol = symbol,
                MinUnderBid = double.MinValue,
                MaxUnderAsk = double.MaxValue,
                Tag = new TagCodec(_trader: OmsCore.User.Username,
                                   _edge: pxDiff,
                                   _type: OmsCore.OrderClient.TYPE,
                                   _subtype: "ML Stock Trader",
                                   _tv: 0,
                                   _ema: Mid,
                                   _bid: Bid,
                                   _ask: Ask,
                                   _comment: InstanceId).Encode(),
                OrderTag = new OrderTagModel()
                {
                    Trader = OmsCore.User.Username,
                    Instance = !string.IsNullOrEmpty(InstanceId) ? InstanceId : "",
                    Bid = Bid,
                    Ask = Ask,
                    BidSize = 0,
                    AskSize = 0,
                    Theo = 0,
                    Ema = 0,
                    UnderBid = 0,
                    UnderAsk = 0,
                    UnderBidSize = 0,
                    UnderAskSize = 0,
                    Edge = pxDiff,
                    OrderSubType = SubType ?? ZeroPlus.Models.Data.Enums.OrderSubType.Ticket,
                    ModuleType = ZeroPlus.Models.Data.Enums.ModuleType.None,
                    VolaTheo = 0,
                    VolaTheoAdj = 0,
                    SubType = 0,
                    SharedId = 0,
                    Sequence = 0,
                    SubTypeSequence = 0,
                    ResubmitCount = 0,
                    TotalEstimatedResubmit = 0,
                    ParentSpreadHash = string.Empty,
                }
            };
            order.SetCancelDelay(interval);
            return order;
        }

        internal void LoadConfig(ModelTraderConfig modelTraderConfig)
        {
            Underlying = modelTraderConfig.Underlying;
            StopLoss = modelTraderConfig.StopLoss;
            Quantity = modelTraderConfig.Quantity;
            LiquidityType = modelTraderConfig.LiquidityType;
            AddLiquidityRestPeriod = modelTraderConfig.AddLiquidityRestPeriod;
            Interval = modelTraderConfig.Interval;
            CacheInterval = modelTraderConfig.CacheInterval;
            AutoCloseInterval = modelTraderConfig.AutoCloseInterval;
            DownAutoCloseInterval = modelTraderConfig.DownAutoCloseInterval;
            Beta = modelTraderConfig.Beta;
            OrderType = modelTraderConfig.OrderType;
            ModelType = modelTraderConfig.ModelType;
            SimulationEnabled = modelTraderConfig.SimulationEnabled;
            CandlePeriod = modelTraderConfig.CandlePeriod;
        }

        internal ModelTraderConfig GetConfig()
        {
            ModelTraderConfig modelTraderConfig = new()
            {
                Underlying = Underlying,
                StopLoss = StopLoss,
                Quantity = Quantity,
                LiquidityType = LiquidityType,
                AddLiquidityRestPeriod = AddLiquidityRestPeriod,
                Interval = Interval,
                CacheInterval = CacheInterval,
                AutoCloseInterval = AutoCloseInterval,
                DownAutoCloseInterval = DownAutoCloseInterval,
                Beta = Beta,
                OrderType = OrderType,
                ModelType = ModelType,
                SimulationEnabled = SimulationEnabled,
                CandlePeriod = CandlePeriod,
            };
            return modelTraderConfig;
        }
    }
}
