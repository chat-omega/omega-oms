using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Charts;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class TradePnlChartViewModel : ModuleViewModelBase, IOrderArchiveReceiver, IChartModule
    {
        private List<string> _symbols;
        private readonly object _bufferLock = new();
        private readonly Queue<IOrder> _buffer = new();

        private readonly TransactionConsumerModel _transactionConsumerModel;
        private string _Symbol;

        private static readonly string MODULE_TITLE = "Chart";

        private IDispatcherService DispatcherService => GetService<IDispatcherService>();
        public ChartModuleConfig ChartModuleConfig { get; set; }

        public List<Side> Sides { get; } = Enum.GetValues(typeof(Side)).Cast<Side>().ToList();
        public List<ChartField> ChartFields { get; } = Enum.GetValues(typeof(ChartField)).Cast<ChartField>().ToList();
        public List<OptionType> OptionTypes { get; } = Enum.GetValues(typeof(OptionType)).Cast<OptionType>().ToList();
        public List<UnderPriceSource> UnderPriceSources { get; } = Enum.GetValues(typeof(UnderPriceSource)).Cast<UnderPriceSource>().ToList();
        public List<SeriesAggregateFunction> SeriesAggregateFunctions { get; } = Enum.GetValues(typeof(SeriesAggregateFunction)).Cast<SeriesAggregateFunction>().ToList();

        public override Module Module { get; protected set; } = Module.TradePnlChart;
        [Bindable]
        public partial bool IsBusy { get; set; }

        [Bindable]
        public partial string IsBusyMessage { get; set; }

        private EdgeScanFeedModel _trade;

        public string Symbol
        {
            get => _Symbol;
            set => SetValue(ref _Symbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial Side Side { get; set; }

        [Bindable]
        public partial int Interval { get; set; }

        [Bindable]
        public partial int Qty { get; set; }

        [Bindable]
        public partial double Multiplier { get; set; }

        [Bindable]
        public partial double BasePrice { get; set; }

        [Bindable]
        public partial bool HedgeEnabled { get; set; }

        [Bindable]
        public partial SeriesAggregateFunction AggregateFunction { get; set; }

        [Bindable]
        public partial OptionType OptionType { get; set; }

        [Bindable]
        public partial int RequestDays { get; set; }

        [Bindable]
        public partial double UnderLastPrice { get; set; }

        [Bindable]
        public partial double UnderPriceOffset { get; set; }

        [Bindable]
        public partial double Delta { get; set; }

        public string DeltaOffsetMask => OmsCore.Config.UsePercentageForDeltaNotion ? "N0" : "N2";
        public decimal DeltaOffsetIncrement => OmsCore.Config.UsePercentageForDeltaNotion ? 10M : .01M;

        [Bindable]
        public partial bool ShowFittedUnderlying { get; set; }

        [Bindable]
        public partial ObservableCollection<DataPointModel> ChartDataPoints { get; set; }

        [Bindable]
        public partial ObservableCollection<ChartConstantLineModel> ZpTradePoints { get; set; }

        public TradePnlChartViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumerModel) : base(configBrowserViewModel, omsCore)
        {
            _transactionConsumerModel = transactionConsumerModel;
            ModuleTitle = MODULE_TITLE;
            ChartDataPoints = new ObservableCollection<DataPointModel>();
            ZpTradePoints = new ObservableCollection<ChartConstantLineModel>();
            Interval = 500;
            Qty = 1;
            Multiplier = 100;
            BasePrice = double.NaN;
        }

        [Command]
        public void Clone()
        {
            try
            {
                if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.Heatmap) ||
                    OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ChartModule))
                {
                    Thread newWindowThread = new(() =>
                    {
                        SynchronizationContext.SetSynchronizationContext(
                            new DispatcherSynchronizationContext(
                                Dispatcher.CurrentDispatcher));

                        TradePnlChartView window = new();
                        TradePnlChartViewModel viewModel = (TradePnlChartViewModel)window.DataContext;
                        viewModel.SetDispatcher(window.Dispatcher);

                        window.Dispatcher.UnhandledException += (s, e) =>
                        {
                            _log.Error(e.Exception, "DispatcherUnhandledException");
                            e.Handled = true;
                        };

                        window.Closed += (s, e) => window.Dispatcher.InvokeShutdown();
                        window.Loaded += (s, e) => viewModel.LoadFromConfig(GetConfig());

                        window.Show();

                        Dispatcher.Run();
                    });
                    newWindowThread.SetApartmentState(ApartmentState.STA);
                    newWindowThread.Start();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clone));
            }
        }

        [Command]
        public async void SearchCommand()
        {
            try
            {
                await UpdateSnapshotChart();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchCommand));
            }
        }

        internal string GetConfigJson()
        {
            try
            {
                ChartModuleConfig config = GetConfig();
                return JsonConvert.SerializeObject(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GetConfigJson));
                return null;
            }
        }

        private ChartModuleConfig GetConfig()
        {
            ChartModuleConfig config = new()
            {
                Symbol = Symbol,
                Interval = Interval,
                OptionType = OptionType,
                RequestDays = RequestDays,
                UnderLastPrice = UnderLastPrice,
                UnderPriceOffset = UnderPriceOffset,
                AggregateFunction = AggregateFunction,
            };
            return config;
        }

        internal async Task LoadConfigFromJsonAsync(string json)
        {
            try
            {
                ChartModuleConfig config = await Task.Run(() => JsonConvert.DeserializeObject<ChartModuleConfig>(json));
                LoadFromConfig(config);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        private void LoadFromConfig(ChartModuleConfig config)
        {
            if (config != null)
            {
                Symbol = config.Symbol;
                Interval = config.Interval;
                OptionType = config.OptionType;
                RequestDays = config.RequestDays;
                UnderLastPrice = config.UnderLastPrice;
                UnderPriceOffset = config.UnderPriceOffset;
                AggregateFunction = config.AggregateFunction;
            }
            _ = InvokeReady();
        }

        private async Task UpdateSnapshotChart()
        {
            try
            {
                IsBusy = true;
                ClearChart();
                await LoadChartFromSnapshots();
            }
            catch (Exception ex)
            {
                IsBusy = false;
                MessageBoxService.ShowMessage("Failed to refresh chart.\n" + ex.Message, "Heatmap - ZeroPlus OMS", MessageButton.OK, MessageIcon.Error);
                _log.Error(ex, nameof(UpdateSnapshotChart));
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadChartFromSnapshots()
        {
            DateTime tradeTime;
            if (_trade != null)
            {
                tradeTime = _trade.BuyTime - TimeSpan.FromHours(1);
                ChartConstantLineModel constant = new(_trade.EdgeScannerType.ToString().FromCamelCase(), tradeTime, LineMode.Secondary);
                ZpTradePoints.Add(constant);
            }
            else
            {
                tradeTime = DateTime.Now - TimeSpan.FromDays(1);
            }

            DateTime startDateTime = tradeTime.Date;
            DateTime endDateTime = tradeTime + TimeSpan.FromDays(2);

            SymbolCodec symbolCodec = new(Symbol);
            if (symbolCodec.LegCount == 0)
            {
                return;
            }
            Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();
            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);

                List<Comms.Models.Data.MarketData.OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                if (results != null)
                {
                    foreach (Comms.Models.Data.MarketData.OptionSnapshot result in results)
                    {
                        if (result.SnapTime >= tradeTime)
                        {
                            if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapTime,
                                };
                                snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                            }

                            int ratio = leg.buySell ? leg.ratio : -leg.ratio;
                            dataPoint.AddResult(i, ratio, result);

                            dataPoint.UnderMid = (result.UnderAsk1 + result.UnderBid1) / 2;

                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, result.Bid, result);
                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, result.Ask, result);
                        }
                    }
                }
            }

            ChartDataPoints = SetChartDataPoints(symbolCodec, snapTimeToChartDataPointMap);
            _ = LoadFirmTradesAsync(endDateTime, startDateTime, symbolCodec);
        }

        private ObservableCollection<DataPointModel> SetChartDataPoints(SymbolCodec symbolCodec, Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap)
        {
            List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
            ObservableCollection<DataPointModel> chartDataPoints = new();
            for (int i = 0; i < dataPoints.Count; i++)
            {
                DataPointModel data = dataPoints[i];

                double baseAdjPrice = BasePrice;

                if (HedgeEnabled)
                {
                    baseAdjPrice += (data.UnderMid - UnderLastPrice) * Delta;
                }

                double bidPnl = Side == Side.Buy ? data.BidIv - baseAdjPrice : baseAdjPrice - data.BidIv;
                double askPnl = Side == Side.Buy ? data.AskIv - baseAdjPrice : baseAdjPrice - data.AskIv;

                double qtyNotional = Qty * Multiplier;

                data.BidIv = Math.Round(bidPnl * qtyNotional, 2);
                data.AskIv = Math.Round(askPnl * qtyNotional, 2);

                chartDataPoints.Add(data);
            }

            if (chartDataPoints.Count > 0)
            {
                DataPointModel first = chartDataPoints.FirstOrDefault();
                double minimum = chartDataPoints.SelectMany(x => new double[] { x.BidIv, x.AskIv, x.Iv }).Min();
                double maximum = chartDataPoints.SelectMany(x => new double[] { x.BidIv, x.AskIv, x.Iv }).Max();
                double minUnder = chartDataPoints.Select(x => x.UnderPx).Min();
                double maxUnder = chartDataPoints.Select(x => x.UnderPx).Max();

                double underRange = maxUnder - minUnder;
                double newRange = maximum - minimum;
                if (underRange == 0)
                {
                    foreach (DataPointModel item in chartDataPoints)
                    {
                        item.UnderlyingFitted = minimum;
                    }
                }
                else
                {
                    foreach (DataPointModel item in chartDataPoints)
                    {
                        item.UnderlyingFitted = ((item.UnderPx - minUnder) * newRange / underRange) + minimum;
                    }
                }
            }

            return chartDataPoints;
        }

        private ObservableCollection<DataPointModel> SetChartDataPointsMinimal(SymbolCodec symbolCodec, Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap)
        {
            List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
            ObservableCollection<DataPointModel> chartDataPoints = new();
            for (int i = 0; i < dataPoints.Count; i++)
            {
                DataPointModel data = dataPoints[i];
                chartDataPoints.Add(data);
                if (i > 0)
                {
                    DataPointModel prev = dataPoints[i - 1];
                    if (prev.Timestamp.Date != data.Timestamp.Date)
                    {
                        ChartConstantLineModel constant = new(data.Timestamp.ToString("dd MMM"), data.Timestamp, LineMode.Secondary);
                        ZpTradePoints.Add(constant);
                    }
                }
            }

            return chartDataPoints;
        }

        private void ClearChart()
        {
            ChartDataPoints.Clear();
            ZpTradePoints.Clear();
        }

        private void DisposeSeries(ICollection<LiveChartSeriesModel> chartSeries)
        {
            foreach (LiveChartSeriesModel series in chartSeries)
            {
                foreach (LiveChartValueModel model in series.ChartValues)
                {
                    model.Dispose();
                }
                series.ChartValues = null;
            }
            chartSeries.Clear();
        }

        private async Task LoadFirmTradesAsync(DateTime endDateTime, DateTime startDateTime, SymbolCodec symbolCodec)
        {
            await Task.Run(() =>
            {
                _symbols = new List<string>()
                {
                    Symbol,
                };

                symbolCodec.Invert();
                _symbols.Add(symbolCodec.ToTOS());
                List<string> emptyList = new();
                List<OrderStatus> orderStatus = new()
                {
                    OrderStatus.PartiallyFilled,
                    OrderStatus.Filled
                };
                int requestId = OmsCore.HerculesClient.RequestTransactionsFromArchive(startDateTime, endDateTime, ordersOnly: true, orderStatus, emptyList, emptyList, _symbols, emptyList);
                _transactionConsumerModel.AddRequester(requestId, this);
            });
        }

        public void AddMultipleOrders(List<IOrder> orders, int totalQueued, int lastMessageIndex)
        {
            if (orders != null)
            {
                if (lastMessageIndex - orders.Count == 0)
                {
                    lock (_bufferLock)
                    {
                        _buffer.Clear();
                    }
                }

                lock (_bufferLock)
                {
                    foreach (IOrder order in orders)
                    {
                        if (order != null)
                        {
                            _buffer.Enqueue(order);
                        }
                    }
                }

                if (totalQueued == _buffer.Count)
                {
                    List<IOrder> buffered = null;

                    lock (_bufferLock)
                    {
                        buffered = _buffer.ToList();
                        _buffer.Clear();
                    }

                    if (buffered != null)
                    {
                        ProcessOrdersForChart(buffered);
                    }
                }
            }
        }

        public void AddMultiplePortfolios(HashSet<IPortfolio> portfolios)
        {
        }

        private void ProcessOrdersForChart(List<IOrder> orders)
        {
            try
            {
                foreach (IOrder order in orders)
                {
                    string title = order.Trader + " Avg: " + order.AveragePrice.ToString("c2") + " E2T: " + (order.HanweckTotalTheo - order.AveragePrice).ToString("c2");
                    ChartConstantLineModel constant = new(title, order.LastUpdateTime);
                    ZpTradePoints.Add(constant);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        internal void LoadFromEdgeScanFeed(EdgeScanFeedModel trade)
        {
            _trade = trade;

            if (_trade != null)
            {
                Symbol = _trade.BuySymbol;
                Qty = _trade.BuyQty;
                BasePrice = _trade.BuyPrice;
                UnderLastPrice = _trade.BuyTradeUnderlyingMid;
                Delta = _trade.BuyTradeDelta;

                _ = UpdateSnapshotChart();
            }
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return GetConfigJson();
        }

        public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            await LoadConfigFromJsonAsync(configJson);
        }
    }
}
