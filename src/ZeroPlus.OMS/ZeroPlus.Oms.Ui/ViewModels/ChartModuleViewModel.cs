using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Xpf.Charts;
using Newtonsoft.Json;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.OptionPricing;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Portfolio.Interfaces;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Models.Utils;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;
using static DevExpress.Xpo.Helpers.AssociatedCollectionCriteriaHelper;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public enum ChartField
    {
        Iv,
        Theo,
        AdjTheo,
        Snapshot,
        BidAskIv,
        RecalculatedBidAskFromIv
    }

    public partial class ChartModuleViewModel : ModuleViewModelBase, IOrderArchiveReceiver, IChartModule
    {
        private List<string> _symbols;
        private readonly object _bufferLock = new();
        private readonly Queue<IOrder> _buffer = new();

        private readonly TransactionConsumerModel _transactionConsumerModel;

        private static readonly string MODULE_TITLE = "Chart";

        private DispatcherTimer _chartUpdateTimer;
        private FastObservableCollection<DataPointModel> _chartDataPointsCopy;
        private string _symbol;

        public ChartModuleConfig ChartModuleConfig { get; set; }

        public override Module Module { get; protected set; } = Module.ChartModule;

        public List<ChartField> ChartFields { get; } = Enum.GetValues(typeof(ChartField)).Cast<ChartField>().ToList();
        public List<OptionType> OptionTypes { get; } = Enum.GetValues(typeof(OptionType)).Cast<OptionType>().ToList();
        public List<UnderPriceSource> UnderPriceSources { get; } = Enum.GetValues(typeof(UnderPriceSource)).Cast<UnderPriceSource>().ToList();
        public List<SeriesAggregateFunction> SeriesAggregateFunctions { get; } = Enum.GetValues(typeof(SeriesAggregateFunction)).Cast<SeriesAggregateFunction>().ToList();
        public string DeltaOffsetMask => OmsCore.Config.UsePercentageForDeltaNotion ? "N0" : "N2";
        public decimal DeltaOffsetIncrement => OmsCore.Config.UsePercentageForDeltaNotion ? 10M : .01M;

        [Bindable]
        public partial bool IsBusy { get; set; }
        [Bindable]
        public partial string IsBusyMessage { get; set; }
        public string Symbol
        {
            get => _symbol;
            set => SetValue(ref _symbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }
        [Bindable]
        public partial bool SnapshotMode { get; set; }
        [Bindable]
        public partial int Interval { get; set; }
        [Bindable]
        public partial ChartField SelectedChartField { get; set; }
        [Bindable]
        public partial SeriesAggregateFunction AggregateFunction { get; set; }
        [Bindable]
        public partial OptionType OptionType { get; set; }
        [Bindable]
        public partial int RequestDays { get; set; }
        [Bindable]
        public partial UnderPriceSource UnderPriceSource { get; set; }
        [Bindable]
        public partial double UnderLastPrice { get; set; }
        [Bindable]
        public partial double UnderPriceOffset { get; set; }
        [Bindable]
        public partial double DeltaOffset { get; set; }
        [Bindable]
        public partial bool ShowNearStrikes { get; set; }
        [Bindable(Default = true)]
        public partial bool SmoothByEma { get; set; }
        [Bindable(Default = 5)]
        public partial int SmoothByEmaPeriod { get; set; }
        [Bindable]
        public partial int NearStrikes { get; set; }
        [Bindable]
        public partial FastObservableCollection<LiveChartSeriesModel> ChartSeries { get; set; }
        [Bindable]
        public partial bool UseLivePriceEnabled { get; set; }
        [Bindable]
        public partial bool ShowFittedUnderlying { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowHighestBidLowestAsk { get; set; }
        [Bindable]
        public partial bool ShowGreeks { get; set; }
        [Bindable]
        public partial FastObservableCollection<DataPointModel> ChartDataPoints { get; set; }
        [Bindable]
        public partial FastObservableCollection<DataPointModel> HighestBidLowestAskChartDataPoints { get; set; }
        [Bindable]
        public partial FastObservableCollection<DataPointModel> ZpTradePriceDataPoints { get; set; }
        [Bindable]
        public partial FastObservableCollection<DataPointModel> StrikeUpChartDataPoints { get; set; }
        [Bindable]
        public partial FastObservableCollection<DataPointModel> StrikeDownChartDataPoints { get; set; }

        [Bindable]
        public partial FastObservableCollection<ChartConstantLineModel> ZpTradePoints { get; set; }

        public ChartModuleViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumerModel) : base(configBrowserViewModel, omsCore)
        {
            _transactionConsumerModel = transactionConsumerModel;
            ModuleTitle = MODULE_TITLE;
            ChartSeries = new();
            ChartDataPoints = new();
            HighestBidLowestAskChartDataPoints = new();
            StrikeUpChartDataPoints = new();
            StrikeDownChartDataPoints = new();
            ZpTradePriceDataPoints = new();
            ZpTradePoints = new();
            NearStrikes = 1;
            Interval = 500;
        }

        public new void SetDispatcher(Dispatcher dispatcher)
        {
            base.SetDispatcher(dispatcher);
            _chartUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(Interval)
            };
            _chartUpdateTimer.Tick += UpdateChart;
            _chartUpdateTimer.Start();
        }

        private void UpdateChart(object sender, EventArgs e)
        {
            try
            {
                foreach (LiveChartSeriesModel series in ChartSeries)
                {
                    foreach (LiveChartValueModel model in series.ChartValues)
                    {
                        model.Update();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateChart));
            }
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

                        ChartModuleView window = new();
                        ChartModuleViewModel viewModel = (ChartModuleViewModel)window.DataContext;
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
        public void ModeChangedCommand()
        {
            try
            {
                SnapshotMode = SelectedChartField is
                    ChartField.Snapshot or
                    ChartField.BidAskIv or
                    ChartField.RecalculatedBidAskFromIv;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ModeChangedCommand));
            }
        }

        [Command]
        public async void SearchCommand()
        {
            try
            {
                if (ChartSeries != null)
                {
                    DisposeSeries(ChartSeries);
                }
                NormalizeSymbol();
                ModeChangedCommand();
                if (!SnapshotMode)
                {
                    await UpdateLiveChart();
                }
                else
                {
                    await UpdateSnapshotChart();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SearchCommand));
            }
        }

        private void NormalizeSymbol()
        {
            try
            {
                if (OptionStrategy.TryIdentify(Symbol, out string baseStrategy, out _, out _))
                {
                    SymbolCodec symbolCodec = new SymbolCodec(Symbol);
                    var legs = new List<Instrument>();
                    for (int i = 0; i < symbolCodec.LegCount; i++)
                    {
                        var leg = symbolCodec.GetLeg(i);
                        legs.Add(leg);
                    }

                    var side = EvaluateSide(baseStrategy, legs);
                    if (side != ZeroPlus.Models.Data.Enums.Side.Buy)
                    {
                        symbolCodec.Invert();
                        Symbol = symbolCodec.ToTOS();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(NormalizeSymbol));
            }
        }

        public static Side EvaluateSide(string spreadType, List<Instrument> legs)
        {
            if (legs.Count == 1)
            {
                Side side = !legs[0].buySell ? ZeroPlus.Models.Data.Enums.Side.Sell : ZeroPlus.Models.Data.Enums.Side.Buy;
                return side;
            }

            return spreadType switch
            {
                "CALL 1X2" or "CALL 1X3" or "CALL 2X3" or "CALL VERTICAL" or "CALL 1X3X3X1" or "CALL CONDOR" or "PUT CONDOR" or "STRADDLE" or "STRANGLE" => legs.OrderBy(x => x.strike).First().buySell == false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "IRON BUTTERFLY" or "IRON CONDOR" => legs.OrderBy(x => x.strike).First().buySell ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "PUT 1X2" or "PUT 1X3" or "PUT 2X3" or "PUT VERTICAL" or "PUT 1X3X3X1" => legs.OrderByDescending(x => x.strike).First().buySell == false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "CALL BUTTERFLY" or "PUT BUTTERFLY" or "CALL SKEWED BUTTERFLY" or "PUT SKEWED BUTTERFLY" => legs.OrderBy(x => x.ratio).First().buySell == false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "CALL CALENDAR" or "PUT CALENDAR" or "CALL DIAGONAL" or "PUT DIAGONAL" or "CALL TRIAGONAL" or "PUT TRIAGONAL" => legs.OrderBy(x => x.expiration).First().buySell ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "CALL CALENDAR FLY" or "PUT CALENDAR FLY" or "CALL SKEWED CALENDAR FLY" or "PUT SKEWED CALENDAR FLY" => legs.OrderBy(x => x.expiration).First().buySell ? ZeroPlus.Models.Data.Enums.Side.Buy : ZeroPlus.Models.Data.Enums.Side.Sell,
                "REVERSAL" or "CONVERSION" => legs.Where(x => x.callPut).First().buySell == false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                "CALL STOCK TIED" or "PUT STOCK TIED" => legs.FirstOrDefault(x => x.symbol.StartsWith(".")).buySell == false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
                _ => false ? ZeroPlus.Models.Data.Enums.Side.Sell :ZeroPlus.Models.Data.Enums.Side.Buy,
            };
        }

        [Command]
        public void UpdateIntervalCommand()
        {
            _chartUpdateTimer.Interval = TimeSpan.FromMilliseconds(Interval);
        }

        internal new void Dispose()
        {
            base.Dispose();
            _chartUpdateTimer.Stop();
            DisposeSeries(ChartSeries);
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
                SnapshotMode = SnapshotMode,
                Interval = Interval,
                SelectedChartField = SelectedChartField,
                OptionType = OptionType,
                RequestDays = RequestDays,
                UnderPriceSource = UnderPriceSource,
                UnderLastPrice = UnderLastPrice,
                UnderPriceOffset = UnderPriceOffset,
                DeltaOffset = OmsCore.Config.UsePercentageForDeltaNotion ? DeltaOffset / 100 : DeltaOffset,
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
                SnapshotMode = config.SnapshotMode;
                Interval = config.Interval;
                SelectedChartField = config.SelectedChartField;
                OptionType = config.OptionType;
                RequestDays = config.RequestDays;
                UnderPriceSource = config.UnderPriceSource;
                UnderLastPrice = config.UnderLastPrice;
                UnderPriceOffset = config.UnderPriceOffset;
                DeltaOffset = OmsCore.Config.UsePercentageForDeltaNotion ? config.DeltaOffset * 100 : config.DeltaOffset;
                AggregateFunction = config.AggregateFunction;
            }
            _ = InvokeReady();
        }

        private async Task UpdateLiveChart()
        {
            if (!string.IsNullOrEmpty(Symbol))
            {
                List<Option> options = await OmsCore.QuoteClient.GetSymbolsAsync(Symbol);
                if (options != null)
                {
                    Dictionary<DateTime, List<Option>> expirationToOptionsMap = options.Where(x => x.Type == OptionType)
                                                        .GroupBy(x => x.Expiration)
                                                        .OrderBy(x => x.Key)
                                                        .ToDictionary(x => x.Key, y => y.ToList());

                    List<LiveChartSeriesModel> models = new();
                    foreach (KeyValuePair<DateTime, List<Option>> kvp in expirationToOptionsMap)
                    {
                        DateTime expiration = kvp.Key;
                        List<Option> strikes = kvp.Value;
                        LiveChartSeriesModel model = new(this)
                        {
                            Title = expiration.ToString("MMM dd yy"),
                        };
                        model.Initialize(strikes, SelectedChartField);
                        models.Add(model);
                    }
                    Dispatcher?.BeginInvoke(() =>
                    {
                        ChartSeries.Clear();
                        ChartSeries.AddRange(models);
                    });
                }
            }
        }

        private async Task UpdateSnapshotChart(double underlyingPrice = double.NaN)
        {
            try
            {
                IsBusy = true;
                ClearChart();
                if (string.IsNullOrEmpty(Symbol))
                {
                    return;
                }

                switch (SelectedChartField)
                {
                    case ChartField.Snapshot:
                        await LoadChartFromSnapshots();
                        break;
                    case ChartField.BidAskIv:
                        await LoadChartFromRecalculations(underlyingPrice);
                        break;
                    case ChartField.RecalculatedBidAskFromIv:
                        await LoadChartFromPriceRecalculations(underlyingPrice);
                        break;
                }
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
            TimeSpan days = TimeSpan.FromDays(RequestDays);
            DateTime endDateTime = DateTime.Now;
            DateTime startDateTime = endDateTime - days;

            SymbolCodec symbolCodec = new(Symbol);
            if (symbolCodec.LegCount == 0)
            {
                return;
            }

            _ = LoadFirmTradesAsync(endDateTime, startDateTime);
            _ = LoadHighestBidLowestAskPoints(symbolCodec);

            Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();
            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);

                List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                if (results != null)
                {
                    foreach (OptionSnapshot result in results)
                    {
                        if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                        {
                            dataPoint = new DataPointModel()
                            {
                                Timestamp = result.SnapTime,
                            };
                            snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                        }
                        int ratio = symbolCodec.LegCount == 1 || leg.buySell ? leg.ratio : -leg.ratio;
                        dataPoint.AddResult(i, ratio, result);
                    }
                }
            }

            SetCache(SetChartDataPoints(symbolCodec, snapTimeToChartDataPointMap));
        }

        private async Task LoadChartFromRecalculations(double underlyingPrice)
        {
            TimeSpan days = TimeSpan.FromDays(RequestDays);
            DateTime endDateTime = DateTime.Now;
            DateTime startDateTime = endDateTime - days;

            SymbolCodec symbolCodec = new(Symbol);
            if (symbolCodec.LegCount == 0)
            {
                return;
            }

            _ = LoadFirmTradesAsync(endDateTime, startDateTime);
            _ = LoadHighestBidLowestAskPoints(symbolCodec);

            string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
            MDUnderlying underlyingDetails = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(underlyingSymbol);

            if (!double.IsNaN(underlyingPrice))
            {
                UnderLastPrice = underlyingPrice;
                UseLivePriceEnabled = false;
            }
            else if (UseLivePriceEnabled)
            {
                switch (UnderPriceSource)
                {
                    case UnderPriceSource.Mid:
                        DataStore askStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        askStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.Ask);
                        DataStore bidStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        bidStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.Bid);
                        UnderLastPrice = (await bidStore.GetDataAsync(underlyingSymbol) + await askStore.GetDataAsync(underlyingSymbol)) / 2;
                        break;
                    case UnderPriceSource.Last:
                        DataStore dataStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        dataStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.LastPrice);
                        UnderLastPrice = await dataStore.GetDataAsync(underlyingSymbol);
                        break;
                }
            }

            Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();

            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);
                List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                if (results != null)
                {
                    foreach (OptionSnapshot result in results)
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
                        double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                        PricingParameters pricingParameters = new()
                        {
                            Volatility = 0.0,
                            PutCall = leg.callPut == true ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                            Strike = leg.strike,
                            DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                            RiskFreeRate = underlyingDetails.RiskFreeRate,
                            StockRate = underlyingDetails.StockRate,
                            UnderlyingPrice = resultMid,
                            UnderlyingMultiplier = underlyingDetails.Multiplier,
                            ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                        };
                        pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);

                        Greeks greeks = new();

                        double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidIv, result);

                        double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwIv, result);

                        double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                        dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askIv, result);
                    }
                }
            }

            SetCache(SetChartDataPoints(symbolCodec, snapTimeToChartDataPointMap));

            if (ShowNearStrikes && NearStrikes > 0)
            {
                await LoadPerms(endDateTime, startDateTime, symbolCodec, underlyingSymbol, underlyingDetails, PermutationDirection.Up);
                await LoadPerms(endDateTime, startDateTime, symbolCodec, underlyingSymbol, underlyingDetails, PermutationDirection.Down);
            }
        }

        private async Task LoadPerms(DateTime endDateTime, DateTime startDateTime, SymbolCodec symbolCodec, string underlyingSymbol, MDUnderlying underlyingDetails, PermutationDirection direction)
        {
            try
            {
                Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);
                    int ratio = leg.buySell ? -leg.ratio : leg.ratio;

                    for (int j = 0; j < NearStrikes; j++)
                    {
                        leg = new Instrument((await OmsCore.QuoteClient.GetNextStrikeOption(leg.symbol, direction)).OptionSymbol);
                    }

                    List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    if (results != null)
                    {
                        foreach (OptionSnapshot result in results)
                        {
                            if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapTime,
                                };
                                snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                            }
                            double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                            PricingParameters pricingParameters = new()
                            {
                                Volatility = 0.0,
                                PutCall = leg.callPut == true ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                Strike = leg.strike,
                                DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                                RiskFreeRate = underlyingDetails.RiskFreeRate,
                                StockRate = underlyingDetails.StockRate,
                                UnderlyingPrice = resultMid,
                                UnderlyingMultiplier = underlyingDetails.Multiplier,
                                ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                            };
                            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);

                            Greeks greeks = new();

                            double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidIv, result);

                            double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwIv, result);

                            double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askIv, result);
                        }
                    }
                }

                List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
                FastObservableCollection<DataPointModel> chartDataPoints = new();
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

                switch (direction)
                {
                    case PermutationDirection.Down:
                        StrikeDownChartDataPoints = chartDataPoints;
                        break;
                    case PermutationDirection.Up:
                        StrikeUpChartDataPoints = chartDataPoints;
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ex));
            }
        }

        private async Task LoadChartFromPriceRecalculations(double underlyingPrice)
        {
            TimeSpan days = TimeSpan.FromDays(RequestDays);
            DateTime endDateTime = DateTime.Now;
            DateTime startDateTime = endDateTime - days;

            SymbolCodec symbolCodec = new(Symbol);
            if (symbolCodec.LegCount == 0)
            {
                return;
            }

            _ = LoadFirmTradesAsync(endDateTime, startDateTime);
            _ = LoadHighestBidLowestAskPoints(symbolCodec);

            string underlyingSymbol = symbolCodec.UnderlyingSymbol().Replace(".", "");
            MDUnderlying underlyingDetails = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(underlyingSymbol);
            if (!double.IsNaN(underlyingPrice))
            {
                UnderLastPrice = underlyingPrice;
                UseLivePriceEnabled = false;
            }
            else if (UseLivePriceEnabled)
            {
                switch (UnderPriceSource)
                {
                    case UnderPriceSource.Mid:
                        DataStore askStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        askStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.Ask);
                        DataStore bidStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        bidStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.Bid);
                        UnderLastPrice = (await bidStore.GetDataAsync(underlyingSymbol) + await askStore.GetDataAsync(underlyingSymbol)) / 2;
                        break;
                    case UnderPriceSource.Last:
                        DataStore dataStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                        dataStore.GetQuoteDataFor(underlyingSymbol, SubscriptionFieldType.LastPrice);
                        UnderLastPrice = await dataStore.GetDataAsync(underlyingSymbol);
                        break;
                }
            }

            Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();

            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);
                int ratio = symbolCodec.LegCount == 1 || leg.buySell ? leg.ratio : -leg.ratio;
                var callPut = leg.callPut ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call;

                List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                if (results != null)
                {
                    foreach (OptionSnapshot result in results)
                    {
                        if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                        {
                            dataPoint = new DataPointModel()
                            {
                                Timestamp = result.SnapTime,
                            };
                            snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                        }
                        var exStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American;
                        double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                        var dte = (leg.expiration - result.SnapTime).TotalDays;

                        var bidPrice = Calculate(callPut, underlyingDetails, leg.strike, resultMid, dte, result.Bid, result.SnapTime, exStyle);
                        dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                        var hwIvPrice = Calculate(callPut, underlyingDetails, leg.strike, resultMid, dte, result.HwTV, result.SnapTime, exStyle);
                        dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwIvPrice, result);

                        var askPrice = Calculate(callPut, underlyingDetails, leg.strike, resultMid, dte, result.Ask, result.SnapTime, exStyle);
                        dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                    }
                }
            }

            SetCache(SetChartDataPoints(symbolCodec, snapTimeToChartDataPointMap));

            if (ShowNearStrikes && NearStrikes > 0)
            {
                await LoadPermsForPriceChart(endDateTime, startDateTime, symbolCodec, underlyingSymbol, underlyingDetails, PermutationDirection.Up);
                await LoadPermsForPriceChart(endDateTime, startDateTime, symbolCodec, underlyingSymbol, underlyingDetails, PermutationDirection.Down);
            }
        }

        private async Task<SymbolCodec> LoadHighestBidLowestAskPoints(SymbolCodec symbolCodec)
        {
            Dictionary<DateTime, DataPointModel> snapTimeToHiBidLoAskChartDataPointMap = new();
            for (int i = 0; i < symbolCodec.LegCount; i++)
            {
                Instrument leg = symbolCodec.GetLeg(i);
                int ratio = symbolCodec.LegCount == 1 || leg.buySell ? leg.ratio : -leg.ratio;

                List<HighestBidLowestAskTrackerModel> historicHighestBidLowestAsk = await OmsCore.InterpolatorClient.Client.RequestHistoricHighestBidLowestAskAsync(leg.symbol);
                if (historicHighestBidLowestAsk != null)
                {
                    foreach (var model in historicHighestBidLowestAsk)
                    {
                        var endTime = model.EndTime;
                        if (endTime.Date != model.StartTime)
                        {
                            endTime = model.StartTime;
                        }
                        var timestamp = new DateTime(endTime.Year, endTime.Month, endTime.Day, endTime.Hour, endTime.Minute, endTime.Second).ToEastern() - TimeSpan.FromHours(1);
                        if (!snapTimeToHiBidLoAskChartDataPointMap.TryGetValue(timestamp, out DataPointModel dataPoint))
                        {
                            dataPoint = new DataPointModel()
                            {
                                Timestamp = timestamp,
                            };
                            snapTimeToHiBidLoAskChartDataPointMap[timestamp] = dataPoint;
                        }

                        var under = model.HighestBidTime > model.LowestAskTime
                            ? model.HighestBidUnderlyingMid
                            : model.LowestAskUnderlyingMid;

                        dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, model.HighestBid, under);
                        dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, model.Delta, under);
                        dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, model.LowestAsk, under);
                    }
                }
            }

            List<DataPointModel> dataPoints = snapTimeToHiBidLoAskChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
            FastObservableCollection<DataPointModel> chartDataPoints = new();
            chartDataPoints.AddRange(dataPoints);
            Dispatcher.BeginInvoke(() => HighestBidLowestAskChartDataPoints = chartDataPoints);
            return symbolCodec;
        }

        private double Calculate(Comms.Models.Data.Securities.PutCall callPut, MDUnderlying underlyingDetails, double strike, double resultMid, double dte, double price, DateTime snapTime, Comms.Models.Data.Securities.ExerciseStyle exStyle)
        {
            var pricingParameters = new PricingParameters()
            {
                Volatility = 0.0,
                PutCall = callPut,
                Strike = strike,
                DaysToExpiration = dte,
                RiskFreeRate = underlyingDetails.RiskFreeRate,
                StockRate = underlyingDetails.StockRate,
                UnderlyingPrice = resultMid,
                UnderlyingMultiplier = underlyingDetails.Multiplier,
                ExerciseStyle = exStyle,
            };
            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, snapTime);
            Greeks greeks = new();
            double iv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, price, greeks);
            pricingParameters.Volatility = iv;
            double optPx = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
            optPx += (UnderLastPrice + UnderPriceOffset - resultMid) * (greeks.Delta + (OmsCore.Config.UsePercentageForDeltaNotion ? DeltaOffset / 100 : DeltaOffset));
            return optPx;
        }

        private async Task LoadPermsForPriceChart(DateTime endDateTime, DateTime startDateTime, SymbolCodec symbolCodec, string underlyingSymbol, MDUnderlying underlyingDetails, PermutationDirection direction)
        {
            try
            {
                Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap = new();
                for (int i = 0; i < symbolCodec.LegCount; i++)
                {
                    Instrument leg = symbolCodec.GetLeg(i);
                    int ratio = leg.buySell ? -leg.ratio : leg.ratio;

                    for (int j = 0; j < NearStrikes; j++)
                    {
                        leg = new Instrument((await OmsCore.QuoteClient.GetNextStrikeOption(leg.symbol, direction)).OptionSymbol);
                    }

                    List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(leg.symbol, leg.expiration, default, startDateTime, endDateTime);
                    if (results != null)
                    {
                        foreach (OptionSnapshot result in results)
                        {
                            if (!snapTimeToChartDataPointMap.TryGetValue(result.SnapTime, out DataPointModel dataPoint))
                            {
                                dataPoint = new DataPointModel()
                                {
                                    Timestamp = result.SnapTime,
                                };
                                snapTimeToChartDataPointMap[result.SnapTime] = dataPoint;
                            }
                            double resultMid = (result.UnderAsk1 + result.UnderBid1) / 2;
                            PricingParameters pricingParameters = new()
                            {
                                Volatility = 0.0,
                                PutCall = leg.callPut == true ? Comms.Models.Data.Securities.PutCall.Put : Comms.Models.Data.Securities.PutCall.Call,
                                Strike = leg.strike,
                                DaysToExpiration = (leg.expiration - result.SnapTime).TotalDays,
                                RiskFreeRate = underlyingDetails.RiskFreeRate,
                                StockRate = underlyingDetails.StockRate,
                                UnderlyingPrice = resultMid,
                                UnderlyingMultiplier = underlyingDetails.Multiplier,
                                ExerciseStyle = underlyingSymbol.StartsWith("$") ? Comms.Models.Data.Securities.ExerciseStyle.European : Comms.Models.Data.Securities.ExerciseStyle.American
                            };
                            pricingParameters.PopulateDividends(underlyingDetails.AnnualDividend, resultMid, underlyingDetails.Dividends, result.SnapTime);
                            Greeks greeks = new();

                            double bidIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Bid, greeks);
                            pricingParameters.Volatility = bidIv;
                            double bidPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            bidPrice += (UnderLastPrice + UnderPriceOffset - resultMid) * (greeks.Delta + (OmsCore.Config.UsePercentageForDeltaNotion ? DeltaOffset / 100 : DeltaOffset));
                            dataPoint.AddResult(i, SubscriptionFieldType.Bid, ratio, bidPrice, result);

                            double hwIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.HwTV, greeks);
                            pricingParameters.Volatility = hwIv;
                            double hwIvPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            hwIvPrice += (UnderLastPrice + UnderPriceOffset - resultMid) * (greeks.Delta + (OmsCore.Config.UsePercentageForDeltaNotion ? DeltaOffset / 100 : DeltaOffset));
                            dataPoint.AddResult(i, SubscriptionFieldType.TheorethicalValue, ratio, hwIvPrice, result);

                            double askIv = OptionModel.Binomial.ImpliedVolatility(pricingParameters, result.Ask, greeks);
                            pricingParameters.Volatility = askIv;
                            double askPrice = OptionModel.Binomial.PriceOption(pricingParameters, greeks);
                            askPrice += (UnderLastPrice + UnderPriceOffset - resultMid) * (greeks.Delta + (OmsCore.Config.UsePercentageForDeltaNotion ? DeltaOffset / 100 : DeltaOffset));
                            dataPoint.AddResult(i, SubscriptionFieldType.Ask, ratio, askPrice, result);
                        }
                    }
                }

                List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
                FastObservableCollection<DataPointModel> chartDataPoints = new();
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

                switch (direction)
                {
                    case PermutationDirection.Down:
                        StrikeDownChartDataPoints = chartDataPoints;
                        break;
                    case PermutationDirection.Up:
                        StrikeUpChartDataPoints = chartDataPoints;
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadPermsForPriceChart));
            }
        }

        private FastObservableCollection<DataPointModel> SetChartDataPoints(SymbolCodec symbolCodec, Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap)
        {
            List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
            FastObservableCollection<DataPointModel> chartDataPoints = new();
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

            if (chartDataPoints.Count > 0)
            {
                GetRange(chartDataPoints, out double minimum, out double maximum);
                double minUnder, maxUnder;
                GetMinMaxUnder(chartDataPoints, out minUnder, out maxUnder);

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
            string csv = string.Join("\n", chartDataPoints.Select(x => $"{x.Timestamp:MM-dd hh:mm:ss.ffffff},{x.BidIv},{x.Iv},{x.AskIv},{x.UnderPx}"));
            return chartDataPoints;
        }

        private static void GetMinMaxUnder(FastObservableCollection<DataPointModel> chartDataPoints, out double minUnder, out double maxUnder)
        {
            try
            {
                minUnder = chartDataPoints.Where(x => x.UnderPx > 0).Select(x => x.UnderPx).Min();
                maxUnder = chartDataPoints.Where(x => x.UnderPx > 0).Select(x => x.UnderPx).Max();
            }
            catch (Exception)
            {
                minUnder = 0;
                maxUnder = 0;
            }
        }

        private static void GetRange(FastObservableCollection<DataPointModel> chartDataPoints, out double minimum, out double maximum)
        {
            try
            {
                minimum = chartDataPoints.Where(x => !double.IsNaN(x.BidIv) && !double.IsNaN(x.AskIv)).SelectMany(x => new double[] { x.BidIv, x.AskIv, x.Iv }).Min();
                maximum = chartDataPoints.Where(x => !double.IsNaN(x.BidIv) && !double.IsNaN(x.AskIv)).SelectMany(x => new double[] { x.BidIv, x.AskIv, x.Iv }).Max();
            }
            catch (Exception)
            {
                minimum = 0;
                maximum = 0;
            }
        }

        private FastObservableCollection<DataPointModel> SetChartDataPointsMinimal(SymbolCodec symbolCodec, Dictionary<DateTime, DataPointModel> snapTimeToChartDataPointMap)
        {
            List<DataPointModel> dataPoints = snapTimeToChartDataPointMap.Values.Where(chartDataPoint => chartDataPoint.TryRecalculate(symbolCodec.LegCount)).OrderBy(x => x.Timestamp).ToList();
            FastObservableCollection<DataPointModel> chartDataPoints = new();
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
            ZpTradePriceDataPoints?.Clear();
            HighestBidLowestAskChartDataPoints?.Clear();
            ChartDataPoints?.Clear();
            StrikeDownChartDataPoints?.Clear();
            StrikeUpChartDataPoints?.Clear();
            ZpTradePoints?.Clear();
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

        private async Task LoadFirmTradesAsync(DateTime endDateTime, DateTime startDateTime)
        {
            await Task.Run(() =>
            {
                _symbols = new List<string>()
                {
                    Symbol,
                };
                SymbolCodec symbolCodec = new SymbolCodec(Symbol);
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
                List<ChartConstantLineModel> constantLineModels = new();
                List<DataPointModel> dataPointModels = new();
                foreach (IOrder order in orders)
                {
                    if (order.OrderStatus.IsFilled())
                    {
                        string title = order.Trader + " Avg: " + order.AveragePrice.ToString("c2") + " E2T: " + (order.HanweckTotalTheo - order.AveragePrice).ToString("c2");
                        ChartConstantLineModel constant = new(title, order.LastUpdateTime);
                        DataPointModel dataPointModel = new();
                        dataPointModel.Timestamp = order.LastUpdateTime;
                        dataPointModel.TradePx = order.AveragePrice;
                        constantLineModels.Add(constant);
                        dataPointModels.Add(dataPointModel);
                    }
                }

                Dispatcher.BeginInvoke(() =>
                {
                    ZpTradePoints.AddRange(constantLineModels);
                    ZpTradePriceDataPoints.AddRange(dataPointModels);
                    MoveFromCache();
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void SetCache(FastObservableCollection<DataPointModel> dataPointModels)
        {
            if (SmoothByEma)
            {
                if (dataPointModels.Any())
                {
                    double prevBidEma = dataPointModels.FirstOrDefault().BidIv;
                    double prevAskEma = dataPointModels.FirstOrDefault().AskIv;

                    double alpha = 2.0 / (1 + SmoothByEmaPeriod);

                    for (int i = 1; i < dataPointModels.Count; i++)
                    {
                        DataPointModel point = dataPointModels[i];
                        double bidIv = point.BidIv;
                        double askIv = point.AskIv;

                        prevBidEma = (bidIv * alpha) + (prevBidEma * (1 - alpha));
                        prevAskEma = (askIv * alpha) + (prevAskEma * (1 - alpha));

                        if (bidIv < prevBidEma)
                        {
                            bidIv = prevBidEma;
                        }
                        if (askIv > prevAskEma)
                        {
                            askIv = prevAskEma;
                        }
                    }
                }
            }

            _chartDataPointsCopy = dataPointModels;
            Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(t => MoveFromCache());
        }

        private void MoveFromCache()
        {
            if (_chartDataPointsCopy != _chartDataPoints)
            {
                Dispatcher.BeginInvoke(() => ChartDataPoints = _chartDataPointsCopy);
            }
        }

        internal void LoadSymbol(string symbol, int days = 5, int mins = 0, double underlyingPrice = double.NaN, UnderPriceSource underPriceSource = UnderPriceSource.Mid, IvChartType ivChartType = IvChartType.RecalculatePrice)
        {
            SnapshotMode = true;
            Symbol = symbol;
            SelectedChartField = (ChartField)(ivChartType + 3);
            RequestDays = days;
            UnderPriceSource = underPriceSource;
            UseLivePriceEnabled = true;
            _ = UpdateSnapshotChart(underlyingPrice);
        }

        internal void LoadSnapshotsChart(string symbol, double midPrice)
        {
            SnapshotMode = true;
            Symbol = symbol;
            SelectedChartField = ChartField.Snapshot;
            RequestDays = 5;
            UnderPriceSource = UnderPriceSource.Mid;
            if (double.IsNaN(midPrice) || midPrice == 0)
            {
                UseLivePriceEnabled = true;
            }
            else
            {
                UnderLastPrice = midPrice;
                UseLivePriceEnabled = false;
            }
            SearchCommand();
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
