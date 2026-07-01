using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Middleware.Communication.Tcp;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Indicators;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class EmaChartViewModel : ModuleViewModelBase, IOmsDataSubscriber
    {
        public event ResetEmaEventHandler ResetEmaEvent;

        private const string MODULE_TITLE = "Ema Chart";

        private readonly object _readWriteLock;
        private readonly List<Tuple<DateTime, double>> _bidUpdates;
        private readonly List<Tuple<DateTime, double>> _askUpdates;

        private readonly EmaConfig _emaConfig;
        private readonly EmaConfig _ema2Config;
        private readonly EmaConfig _ema3Config;

        private readonly EmaCalculator _bidEmaCalculator;
        private readonly EmaCalculator _midEmaCalculator;
        private readonly EmaCalculator _askEmaCalculator;

        private readonly EmaCalculator _midEma2Calculator;
        private readonly EmaCalculator _midEma3Calculator;

        private readonly OmsCore _omsCore;
        private readonly Timer _resetTimer;
        private readonly List<QuoteModel> _quoteModels;
        private readonly ConcurrentQueue<IndicatorDataPoints> _updatesQueue;
        private readonly ConcurrentDictionary<string, QuoteModel> _symbolToQuoteModelMap;


        private SymbolCodec _symbolCodec;
        private IndicatorDataPoints _lastModel;

        public List<ChartDataSource> DataSources { get; } = ((ChartDataSource[])Enum.GetValues(typeof(ChartDataSource))).ToList();

        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        protected IOpenFileDialogService OpenFileDialogService => GetService<IOpenFileDialogService>();

        private DispatcherTimer _uiUpdateTimer;
        private TcpClient _tcpClient;


        public override Module Module { get; protected set; } = Module.EmaChart;

        public double MaxBidDeviation { get; set; }
        public double MaxAskDeviation { get; set; }
        public EmaType SelectedEmaType { get; set; }
        public double PercentVegaThreshold { get; set; }
        public MacdCalculator MacdCalculator { get; }
        public FastObservableCollection<IndicatorDataPoints> DataPoints { get; }

        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial ChartDataSource DataSource { get; set; }
        [Bindable(Default = 5000)]
        public partial int BarInterval { get; set; }
        [Bindable]
        public partial bool ApplyEmaChangesEnabled { get; set; }
        [Bindable]
        public partial int HistoricRequestDays { get; set; }
        [Bindable(Default = true)]
        public partial bool LoadHistoric { get; set; }
        [Bindable(Default = 5000)]
        public partial double ResetPeriod { get; set; }
        [Bindable]
        public partial bool ShowBid { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowMid { get; set; }
        [Bindable]
        public partial bool ShowAsk { get; set; }
        [Bindable]
        public partial bool ShowBidEma { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowMidEma { get; set; }
        [Bindable]
        public partial bool ShowMidEma2 { get; set; }
        [Bindable]
        public partial bool ShowMidEma3 { get; set; }
        [Bindable]
        public partial bool ShowAskEma { get; set; }
        [Bindable]
        public partial bool ShowHighestBid { get; set; }
        [Bindable]
        public partial bool ShowLowestAsk { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowMacd { get; set; }

        public EmaChartViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            _omsCore = omsCore;
            _lastModel = new(DateTime.Now);
            _resetTimer = new Timer
            {
                AutoReset = false,
                Interval = BarInterval,
            };
            _resetTimer.Elapsed += ResetModel;
            _symbolToQuoteModelMap = new();
            _quoteModels = new();
            _updatesQueue = new();

            _readWriteLock = new();
            _bidUpdates = new();
            _askUpdates = new();

            _emaConfig = new()
            {
                EmaPeriods = 10
            };
            _ema2Config = new()
            {
                EmaPeriods = 100
            };
            _ema3Config = new()
            {
                EmaPeriods = 200
            };

            _bidEmaCalculator = new EmaCalculator(_emaConfig, SubscriptionFieldType.Bid);
            _midEmaCalculator = new EmaCalculator(_emaConfig, SubscriptionFieldType.MidPoint);
            _askEmaCalculator = new EmaCalculator(_emaConfig, SubscriptionFieldType.Ask);

            _midEma2Calculator = new EmaCalculator(_ema2Config, SubscriptionFieldType.MidPoint);
            _midEma3Calculator = new EmaCalculator(_ema3Config, SubscriptionFieldType.MidPoint);

            _bidEmaCalculator.EmaUpdatedEvent += OnBidEmaUpdatedEvent;
            _midEmaCalculator.EmaUpdatedEvent += OnEmaUpdatedEvent;
            _askEmaCalculator.EmaUpdatedEvent += OnAskEmaUpdatedEvent;

            _midEma2Calculator.EmaUpdatedEvent += OnEma2UpdatedEvent;
            _midEma3Calculator.EmaUpdatedEvent += OnEma3UpdatedEvent;

            MacdCalculator = new();
            MacdCalculator.SignalEmaConfig.EmaPeriods = 7;
            MacdCalculator.FastEmaConfig.EmaPeriods = 14;
            MacdCalculator.SlowEmaConfig.EmaPeriods = 21;

            MacdCalculator.MacdUpdated += OnMacdUpdated;

            Symbol = "";
            ModuleTitle = MODULE_TITLE;
            DataPoints = new();
        }

        [Command]
        public void ShowEmaSettingsCommand()
        {
            EmaConfigWindowView view = new();
            if (view.DataContext is EmaConfigWindowViewModel viewModel)
            {
                viewModel.EmaConfigViewModel.EmaConfig = _emaConfig;
                viewModel.Ema2ConfigViewModel.EmaConfig = _ema2Config;
                viewModel.Ema3ConfigViewModel.EmaConfig = _ema3Config;
                view.ShowDialog();
                RecalculateIndicators();
            }
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
                view.ShowDialog();
                RecalculateIndicators();
            }
        }

        [Command]
        public void ResetMacdCommand()
        {
            MacdCalculator?.Reset();
        }

        [Command]
        public async Task SearchCommand()
        {
            if (_symbolCodec != null && _symbolCodec.LegCount > 0)
            {
                MessageResult result = Dispatcher.Invoke(() => MessageBoxService.ShowMessage("You are about to reset the existing chart!\nAre you sure you want to proceed?", ModuleTitle, MessageButton.YesNo, MessageIcon.Warning, MessageResult.No));
                if (result == MessageResult.No)
                {
                    return;
                }
            }
            Clear();
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                ModuleTitle = Symbol + " - " + MODULE_TITLE;
                _symbolCodec = new SymbolCodec(Symbol);
                if (_symbolCodec.LegCount > 0)
                {
                    switch (DataSource)
                    {
                        case ChartDataSource.Local:
                            await LoadChartFromDataBase();
                            break;
                        case ChartDataSource.DataBento:
                            await LoadChartFromDataBento();
                            break;
                    }

                    SubscribeToLiveUpdates();

                    _emaConfig.EmaEnabled = true;
                    _ema2Config.EmaEnabled = true;
                    _ema3Config.EmaEnabled = true;
                    MacdCalculator.SignalEmaConfig.EmaEnabled = true;
                    MacdCalculator.FastEmaConfig.EmaEnabled = true;
                    MacdCalculator.SlowEmaConfig.EmaEnabled = true;

                    _resetTimer.Start();
                    _uiUpdateTimer.Start();
                }
            }
            else
            {
                ModuleTitle = MODULE_TITLE;
            }
        }

        private async Task LoadChartFromDataBento()
        {
            if (HistoricRequestDays > 0)
            {
                await Task.Run(async () =>
                {
                    DateTime startDate = DateTime.Now.ToEastern().Date - TimeSpan.FromDays(HistoricRequestDays);
                    DateTime endDate = DateTime.Now.ToEastern().Date;
                    if (endDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        endDate -= TimeSpan.FromDays(1);
                    }
                    if (endDate.DayOfWeek == DayOfWeek.Monday)
                    {
                        endDate -= TimeSpan.FromDays(2);
                    }

                    byte[] byteArray = Encoding.ASCII.GetBytes($"{OmsCore.Config.DatabentoApiKey}:");
                    AuthenticationHeaderValue header = new("Basic", Convert.ToBase64String(byteArray));
                    using HttpClient httpClient = new();
                    httpClient.DefaultRequestHeaders.Authorization = header;
                    Dictionary<string, string> keys = new()
                                {
                                    { "dataset", "XNAS.ITCH"},
                                    { "symbols", string.Join(",", (IEnumerable<string>)_symbolToQuoteModelMap.Keys)},
                                    { "schema", "ohlcv-1s"},
                                    { "start", startDate.ToString("s")},
                                    { "end", endDate.ToString("s")},
                                    { "encoding", "json"},
                                    { "pretty_px", "true"},
                                    { "pretty_ts", "true"},
                                    { "map_symbols", "true" },
                                };

                    FormUrlEncodedContent content = new(keys);
                    HttpResponseMessage response = await httpClient.PostAsync(OmsCore.Config.DatabentoTsAddress, content);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        List<IndicatorDataPoints> models = new();
                        IndicatorDataPoints lastModel = new();
                        string prevTime = "";
                        foreach (string item in json.Split("\n"))
                        {
                            if (item.Length > 6)
                            {
                                string internalContent = item[6..].Replace("},\"", ",\"");
                                DataBentoHdModel dbModel = JsonConvert.DeserializeObject<DataBentoHdModel>(internalContent);

                                if (_symbolToQuoteModelMap.TryGetValue(dbModel.symbol, out QuoteModel quoteModel))
                                {
                                    if (double.TryParse(dbModel.low, out double bid) && double.TryParse(dbModel.high, out double ask))
                                    {
                                        quoteModel.Bid = bid;
                                        quoteModel.Ask = ask;
                                    }
                                }
                                if (prevTime == "")
                                {
                                    prevTime = dbModel.ts_event;
                                }
                                else if (prevTime != dbModel.ts_event)
                                {
                                    string stringTime = prevTime.Replace("T", " ").Replace("Z", "");
                                    if (stringTime.Contains('.'))
                                    {
                                        stringTime = stringTime.Split('.')[0];
                                    }
                                    if (DateTime.TryParseExact(stringTime, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dateTime))
                                    {
                                        lastModel.Timestamp = dateTime;
                                        if (!double.IsNaN(lastModel.Mid))
                                        {
                                            IEnumerable<Tuple<DateTime, double>> validBids = _bidUpdates.Where(x => (dateTime - x.Item1).TotalMilliseconds < ResetPeriod);
                                            if (validBids.Any())
                                            {
                                                Tuple<DateTime, double> highestBid = validBids.MaxBy(x => x.Item2);
                                                lastModel.HighestBidUpdateTime = highestBid.Item1;
                                                lastModel.HighestBid = highestBid.Item2;
                                            }

                                            IEnumerable<Tuple<DateTime, double>> validAsks = _askUpdates.Where(x => (dateTime - x.Item1).TotalMilliseconds < ResetPeriod);
                                            if (validAsks.Any())
                                            {
                                                Tuple<DateTime, double> lowestAsk = validAsks.MinBy(x => x.Item2);
                                                lastModel.LowestAskUpdateTime = lowestAsk.Item1;
                                                lastModel.LowestAsk = lowestAsk.Item2;
                                            }

                                            models.Add(lastModel);
                                        }
                                        lastModel = new(dateTime);
                                        prevTime = dbModel.ts_event;

                                        double bid = 0.0;
                                        double ask = 0.0;
                                        for (int i = 0; i < _quoteModels.Count; i++)
                                        {
                                            QuoteModel model = _quoteModels[i];
                                            switch (model.Side)
                                            {
                                                case ZeroPlus.Models.Data.Enums.Side.Buy:
                                                    bid += model.Ratio * model.Bid;
                                                    ask += model.Ratio * model.Ask;
                                                    break;
                                                case ZeroPlus.Models.Data.Enums.Side.Sell:
                                                    bid -= model.Ratio * model.Ask;
                                                    ask -= model.Ratio * model.Bid;
                                                    break;
                                            }
                                        }

                                        double mid = Math.Round((bid + ask) / 2, 2);

                                        if (bid != ask && !double.IsNaN(mid))
                                        {
                                            lastModel.Bid = bid;
                                            lastModel.Mid = mid;
                                            lastModel.Ask = ask;

                                            _bidUpdates.Add(Tuple.Create(dateTime, bid));
                                            _askUpdates.Add(Tuple.Create(dateTime, ask));
                                        }
                                    }
                                }
                            }
                        }
                        if (models.Any())
                        {
                            await Dispatcher?.BeginInvoke(() => DataPoints.AddRange(models));
                        }
                    }
                });
            }

            _tcpClient = new TcpClient();
            _tcpClient.MessageParser = new DataBentoMessageParser(_tcpClient, this);
            _tcpClient.Connect(OmsCore.Config.DatabentoEq2Address, OmsCore.Config.DatabentoPort);
        }

        private async Task LoadChartFromDataBase()
        {
            if (LoadHistoric)
            {
                Dictionary<DateTime, IndicatorDataPoints> timestampToModelMap = new();

                List<List<ZeroPlus.Models.Data.Models.BarModel>> buffer = new();
                for (int i = 0; i < _symbolCodec.LegCount; i++)
                {
                    Instrument leg = _symbolCodec.GetLeg(i);
                    var symbol = leg.symbol;
                    var start = DateTime.Today - TimeSpan.FromDays(HistoricRequestDays);
                    var end = DateTime.Now;
                    var bars = await _omsCore.FullEmaClient.Client.RequestBarsAsync(symbol, start, end);

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

                    var prev = models.FirstOrDefault();
                    List<IndicatorDataPoints> finalModels = new()
                {
                    prev
                };

                    for (int i = 1; i < models.Count; i++)
                    {
                        IndicatorDataPoints cur = models[i];
                        if ((cur.Timestamp - prev.Timestamp).TotalMilliseconds >= BarInterval)
                        {
                            prev = cur;
                            finalModels.Add(prev);
                        }
                    }

                    var last = finalModels.LastOrDefault();
                    if (last != null)
                    {
                        _lastModel = last;
                        _midEmaCalculator.Prime(last.MidEma);
                        _midEma2Calculator.Prime(last.MidEma2);
                        _midEma3Calculator.Prime(last.MidEma3);
                        MacdCalculator.Prime(last.FastEma, last.SlowEma, last.Signal);
                    }
                    await Dispatcher?.BeginInvoke(() => DataPoints.AddRange(finalModels));
                }
            }
        }

        private void Recalculate(List<IndicatorDataPoints> points)
        {
            if (points.Any())
            {
                double alpha = _emaConfig.EmaSmoothing / (1 + _emaConfig.EmaPeriods);
                double alpha2 = _ema2Config.EmaSmoothing / (1 + _ema2Config.EmaPeriods);
                double alpha3 = _ema3Config.EmaSmoothing / (1 + _ema3Config.EmaPeriods);

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
                    if ((point.HighestBidUpdateTime - bidEmaStartTime).TotalMilliseconds >= _emaConfig.EmaInterval)
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
                    if ((point.LowestAskUpdateTime - askEmaStartTime).TotalMilliseconds >= _emaConfig.EmaInterval)
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
                    if ((midUpdateTime - midEmaStartTime).TotalMilliseconds >= _emaConfig.EmaInterval)
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
                    if ((midUpdateTime - midEma2StartTime).TotalMilliseconds >= _ema2Config.EmaInterval)
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
                    if ((midUpdateTime - midEma3StartTime).TotalMilliseconds >= _ema3Config.EmaInterval)
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

        private void SubscribeToLiveUpdates()
        {
            for (int i = 0; i < _symbolCodec.LegCount; i++)
            {
                Instrument leg = _symbolCodec.GetLeg(i);
                if (leg != null)
                {
                    QuoteModel quoteModel = new()
                    {
                        Ratio = leg.ratio,
                        Side = leg.buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell
                    };
                    _symbolToQuoteModelMap[leg.symbol] = quoteModel;

                    if (DataSource == ChartDataSource.Local)
                    {
                        _omsCore.QuoteClient.Subscribe(leg.symbol, SubscriptionFieldType.Bid, this);
                        _omsCore.QuoteClient.Subscribe(leg.symbol, SubscriptionFieldType.Ask, this);
                    }
                }
            }
            _quoteModels.AddRange(_symbolToQuoteModelMap.Values);
        }

        [Command]
        public void ResetEmaCommand()
        {
            ResetEmaEvent?.Invoke();
        }

        [Command]
        public void ApplyEmaChangesCommand()
        {
            ApplyEmaChangesEnabled = true;
        }

        [Command]
        public void RecalculateIndicators()
        {
            ApplyEmaChangesEnabled = false;
            List<IndicatorDataPoints> points = DataPoints.OrderBy(x => x.Timestamp).ToList();
            Recalculate(points);
        }

        [Command]
        public void ResetHiLoCommand()
        {
            lock (_readWriteLock)
            {
                _bidUpdates.Clear();
                _askUpdates.Clear();
            }
        }

        [Command]
        public async Task SaveChartCommand()
        {
            try
            {
                SaveFileDialogService.DefaultExt = "json";
                SaveFileDialogService.DefaultFileName = $"EMA Chart - {DateTime.Now:MM-dd-yyyy hh.mm}";
                SaveFileDialogService.Filter = "Json|*.JSON";
                bool dialogResult = SaveFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    string json = await Task.Run(() => JsonConvert.SerializeObject(DataPoints.ToList()));
                    string filePath = SaveFileDialogService.GetFullFileName();
                    File.WriteAllText(filePath, json);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveChartCommand));
            }
        }

        [Command]
        public void LoadChartCommand()
        {
            try
            {
                OpenFileDialogService.Filter = "JSON files|*.JSON";
                bool dialogResult = OpenFileDialogService.ShowDialog();
                if (dialogResult)
                {
                    IFileInfo file = OpenFileDialogService.Files.First();
                    string filePath = file.GetFullName();
                    if (File.Exists(filePath))
                    {
                        string content = File.ReadAllText(filePath);
                        List<IndicatorDataPoints> dataPoints = JsonConvert.DeserializeObject<List<IndicatorDataPoints>>(content);
                        if (dataPoints != null)
                        {
                            Clear();
                            Dispatcher?.BeginInvoke(() => DataPoints.AddRange(dataPoints));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadChartCommand));
            }
        }

        internal List<string> GetSymbols()
        {
            return _symbolToQuoteModelMap.Keys.ToList();
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            SubscriptionFieldType field = key.Type;
            if (_symbolToQuoteModelMap.TryGetValue(symbol, out QuoteModel quoteModel) && value is double update)
            {
                switch (field)
                {
                    case SubscriptionFieldType.Bid:
                        if (quoteModel.Bid != update)
                        {
                            quoteModel.Bid = update;
                            Update();
                        }
                        break;
                    case SubscriptionFieldType.Ask:
                        if (quoteModel.Ask != update)
                        {
                            quoteModel.Ask = update;
                            Update();
                        }
                        break;
                }
            }
        }

        private void OnMacdUpdated(double macd, double signal, double bar)
        {
            _lastModel.Bar = bar;
            _lastModel.Macd = macd;
            _lastModel.Signal = signal;
        }

        private void OnBidEmaUpdatedEvent(double ema)
        {
            _lastModel.BidEma = ema;
        }

        private void OnEmaUpdatedEvent(double ema)
        {
            _lastModel.MidEma = ema;
        }

        private void OnEma2UpdatedEvent(double ema)
        {
            _lastModel.MidEma2 = ema;
        }

        private void OnEma3UpdatedEvent(double ema)
        {
            _lastModel.MidEma3 = ema;
        }

        private void OnAskEmaUpdatedEvent(double ema)
        {
            _lastModel.AskEma = ema;
        }

        private void Update()
        {
            double bid = 0.0;
            double ask = 0.0;
            for (int i = 0; i < _quoteModels.Count; i++)
            {
                QuoteModel model = _quoteModels[i];
                switch (model.Side)
                {
                    case ZeroPlus.Models.Data.Enums.Side.Buy:
                        bid += model.Ratio * model.Bid;
                        ask += model.Ratio * model.Ask;
                        break;
                    case ZeroPlus.Models.Data.Enums.Side.Sell:
                        bid -= model.Ratio * model.Ask;
                        ask -= model.Ratio * model.Bid;
                        break;
                }
            }
            double mid = Math.Round((bid + ask) / 2, 2);
            if (bid != ask && !double.IsNaN(mid))
            {
                _lastModel.Bid = bid;
                _lastModel.Mid = mid;
                _lastModel.Ask = ask;
                DateTime now = DateTime.Now;
                if (!double.IsNaN(bid) && !double.IsNaN(ask))
                {
                    lock (_readWriteLock)
                    {
                        _bidUpdates.Add(Tuple.Create(now, bid));
                        _askUpdates.Add(Tuple.Create(now, ask));
                    }
                    if (_emaConfig.EmaEnabled)
                    {
                        _bidEmaCalculator.AddUpdate(bid);
                        _askEmaCalculator.AddUpdate(ask);
                        _midEmaCalculator.AddUpdate(mid);
                    }
                    if (_ema2Config.EmaEnabled)
                    {
                        _midEma2Calculator.AddUpdate(mid);
                    }
                    if (_ema3Config.EmaEnabled)
                    {
                        _midEma3Calculator.AddUpdate(mid);
                    }
                    MacdCalculator.AddUpdate(mid);
                }
            }
        }

        internal new void Dispose()
        {
            base.Dispose();
            Clear();
        }

        private void Clear()
        {
            try
            {
                _tcpClient?.Disconnect();
                _omsCore.QuoteClient.UnsubscribeAll(this);
                _uiUpdateTimer.Stop();
                _resetTimer.Stop();
                _bidUpdates.Clear();
                _askUpdates.Clear();
                _emaConfig.EmaEnabled = false;
                _ema2Config.EmaEnabled = false;
                ResetEmaEvent?.Invoke();
                _bidEmaCalculator.Reset();
                _askEmaCalculator.Reset();
                _midEmaCalculator.Reset();
                MacdCalculator.Reset();
                _quoteModels.Clear();
                _updatesQueue.Clear();
                _symbolToQuoteModelMap.Clear();
                Dispatcher.Invoke(() => DataPoints.Clear());
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clear));
            }
        }

        private void ResetModel(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (_lastModel.Updated)
                {
                    Tuple<DateTime, double> highestBid = Tuple.Create(_lastModel.HighestBidUpdateTime, _lastModel.HighestBid);
                    Tuple<DateTime, double> lowestAsk = Tuple.Create(_lastModel.LowestAskUpdateTime, _lastModel.LowestAsk);
                    DateTime now = DateTime.Now;
                    lock (_readWriteLock)
                    {
                        IEnumerable<Tuple<DateTime, double>> validBids = _bidUpdates.Where(x => (now - x.Item1).TotalMilliseconds < ResetPeriod);
                        if (validBids.Any())
                        {
                            highestBid = validBids.MaxBy(x => x.Item2);
                        }
                        IEnumerable<Tuple<DateTime, double>> validAsks = _askUpdates.Where(x => (now - x.Item1).TotalMilliseconds < ResetPeriod);
                        if (validAsks.Any())
                        {
                            lowestAsk = validAsks.MinBy(x => x.Item2);
                        }
                    }
                    _lastModel.HighestBidUpdateTime = highestBid.Item1;
                    _lastModel.HighestBid = highestBid.Item2;

                    _lastModel.LowestAskUpdateTime = lowestAsk.Item1;
                    _lastModel.LowestAsk = lowestAsk.Item2;

                    _updatesQueue.Enqueue(_lastModel);
                    _lastModel = _lastModel.Clone(DateTime.Now);
                }
            }
            finally
            {
                _resetTimer.Start();
            }
        }

        [Command]
        public void UpdateInterval()
        {
            try
            {
                _resetTimer.Interval = Math.Max(250, BarInterval);
            }
            catch { }
        }

        internal new void SetDispatcher(Dispatcher dispatcher)
        {
            base.SetDispatcher(dispatcher);
            StartUiUpdateTimer();
        }

        private void StartUiUpdateTimer()
        {
            int interval = 1;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT &&
               Environment.OSVersion.Version.Major < 10)
            {
                // Slow updates for versions hosts below Win-10
                interval = 30;
            }
            _uiUpdateTimer = new DispatcherTimer(TimeSpan.FromSeconds(interval), DispatcherPriority.Render, UpdateChart, Dispatcher);
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            try
            {
                EmaChartConfig config = GetConfig();
                return config.Serialize();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetConfigSerialized));
                return string.Empty;
            }
        }

        public EmaChartConfig GetConfig()
        {
            return new()
            {
                Symbol = Symbol,
                EmaSmoothing = _emaConfig.EmaSmoothing,
                EmaInterval = _emaConfig.EmaInterval,
                EmaPeriods = _emaConfig.EmaPeriods,
                Ema2Smoothing = _ema2Config.EmaSmoothing,
                Ema2Interval = _ema2Config.EmaInterval,
                Ema2Periods = _ema2Config.EmaPeriods,
                Ema3Smoothing = _ema3Config.EmaSmoothing,
                Ema3Interval = _ema3Config.EmaInterval,
                Ema3Periods = _ema3Config.EmaPeriods,
                SignalEmaSmoothing = MacdCalculator.SignalEmaConfig.EmaSmoothing,
                SignalEmaInterval = MacdCalculator.SignalEmaConfig.EmaInterval,
                SignalEmaPeriods = MacdCalculator.SignalEmaConfig.EmaPeriods,
                SlowEmaSmoothing = MacdCalculator.SlowEmaConfig.EmaSmoothing,
                SlowEmaInterval = MacdCalculator.SlowEmaConfig.EmaInterval,
                SlowEmaPeriods = MacdCalculator.SlowEmaConfig.EmaPeriods,
                FastEmaSmoothing = MacdCalculator.FastEmaConfig.EmaSmoothing,
                FastEmaInterval = MacdCalculator.FastEmaConfig.EmaInterval,
                FastEmaPeriods = MacdCalculator.FastEmaConfig.EmaPeriods,
                LoadHistoric = LoadHistoric,
                HistoricRequestDays = HistoricRequestDays,
                ShowBid = ShowBid,
                ShowMid = ShowMid,
                ShowAsk = ShowAsk,
                ShowBidEma = ShowBidEma,
                ShowMidEma = ShowMidEma,
                ShowMidEma2 = ShowMidEma2,
                ShowMidEma3 = ShowMidEma3,
                ShowAskEma = ShowAskEma,
                ShowHighestBid = ShowHighestBid,
                ShowLowestAsk = ShowLowestAsk,
                ShowMacd = ShowMacd,
                BarInterval = BarInterval,
            };
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            try
            {
                EmaChartConfig config = await ModuleConfigBase.DeserializeAsync<EmaChartConfig>(configJson);
                if (config != null)
                {
                    Symbol = config.Symbol;
                    _emaConfig.EmaSmoothing = config.EmaSmoothing;
                    _emaConfig.EmaInterval = config.EmaInterval;
                    _emaConfig.EmaPeriods = config.EmaPeriods;
                    _ema2Config.EmaSmoothing = config.Ema2Smoothing;
                    _ema2Config.EmaInterval = config.Ema2Interval;
                    _ema2Config.EmaPeriods = config.Ema2Periods;
                    _ema3Config.EmaSmoothing = config.Ema3Smoothing;
                    _ema3Config.EmaInterval = config.Ema3Interval;
                    _ema3Config.EmaPeriods = config.Ema3Periods;
                    MacdCalculator.SignalEmaConfig.EmaSmoothing = config.SignalEmaSmoothing;
                    MacdCalculator.SignalEmaConfig.EmaInterval = config.SignalEmaInterval;
                    MacdCalculator.SignalEmaConfig.EmaPeriods = config.SignalEmaPeriods;
                    MacdCalculator.SlowEmaConfig.EmaSmoothing = config.SlowEmaSmoothing;
                    MacdCalculator.SlowEmaConfig.EmaInterval = config.SlowEmaInterval;
                    MacdCalculator.SlowEmaConfig.EmaPeriods = config.SlowEmaPeriods;
                    MacdCalculator.FastEmaConfig.EmaSmoothing = config.FastEmaSmoothing;
                    MacdCalculator.FastEmaConfig.EmaInterval = config.FastEmaInterval;
                    MacdCalculator.FastEmaConfig.EmaPeriods = config.FastEmaPeriods;
                    LoadHistoric = config.LoadHistoric;
                    HistoricRequestDays = config.HistoricRequestDays;
                    ShowBid = config.ShowBid;
                    ShowMid = config.ShowMid;
                    ShowAsk = config.ShowAsk;
                    ShowBidEma = config.ShowBidEma;
                    ShowMidEma = config.ShowMidEma;
                    ShowMidEma2 = config.ShowMidEma2;
                    ShowMidEma3 = config.ShowMidEma3;
                    ShowAskEma = config.ShowAskEma;
                    ShowHighestBid = config.ShowHighestBid;
                    ShowLowestAsk = config.ShowLowestAsk;
                    ShowMacd = config.ShowMacd;
                    BarInterval = config.BarInterval;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeserializeAndLoadConfig));
            }
        }

        public override void SaveViewModelConfig()
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(EmaChartConfig)}.json");
                string configJson = GetConfigSerialized();
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
            }
        }

        private void UpdateChart(object sender, EventArgs e)
        {
            try
            {
                while (_updatesQueue.Count > 0)
                {
                    if (_updatesQueue.TryDequeue(out IndicatorDataPoints model))
                    {
                        DataPoints.Add(model);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            finally
            {
                _uiUpdateTimer.Start();
            }
        }
    }
}
