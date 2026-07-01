using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LiveChartViewModel : ModuleViewModelBase, IOmsDataSubscriber
    {
        private DispatcherTimer _chartUpdateTimer;


        private readonly ConcurrentQueue<ChartValueModel[]> _updateQueue = new();
        public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();

        public override Module Module { get; protected set; } = Module.LiveChart;

        public ObservableCollection<ChartValueModel> TestValueDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> BaseLineDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> TradeIntBidDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> TradeIntAskDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> BestBidDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> BestAskDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> AdjTheoDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> AdjEmaDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> HanweckTheoDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> UnderlyingDataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> Leg1DataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> Leg2DataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> Leg3DataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public ObservableCollection<ChartValueModel> Leg4DataPoints { get; } = new ObservableCollection<ChartValueModel>();
        public FastObservableCollection<LiveChartUpdateModel> Updates { get; } = new FastObservableCollection<LiveChartUpdateModel>();
        public ObservableCollection<ChartConstantLineModel> ResetPoints { get; } = new ObservableCollection<ChartConstantLineModel>();

        [Bindable]
        public partial bool MatchSequence { get; set; }

        [Bindable]
        public partial bool ShowAdjTheo { get; set; }

        [Bindable]
        public partial bool ShowTestValue { get; set; }

        [Bindable]
        public partial bool ShowBaseLine { get; set; }

        [Bindable]
        public partial bool ShowDerivedValues { get; set; }

        [Bindable]
        public partial bool ShowAdjEma { get; set; }

        [Bindable]
        public partial bool ShowHwTheo { get; set; }

        [Bindable]
        public partial bool ShowLegTheo { get; set; }

        [Bindable]
        public partial bool ShowUnderlying { get; set; }

        [Bindable]
        public partial bool Leg1TheoLoaded { get; set; }

        [Bindable]
        public partial bool Leg2TheoLoaded { get; set; }

        [Bindable]
        public partial bool Leg3TheoLoaded { get; set; }

        [Bindable]
        public partial bool Leg4TheoLoaded { get; set; }

        [Bindable]
        public partial string UnderlyingSymbol { get; set; }
        [Bindable]
        public partial string Symbol { get; set; }
        [Bindable]
        public partial bool AutoScroll { get; set; }
        [Bindable]
        public partial LiveChartUpdateModel LastUpdate { get; set; }

        private readonly ConcurrentDictionary<string, LegModel> _legLookup = new();
        private readonly List<LegModel> Legs = new();
        private double _mid = double.NaN;
        private double _adjTheo = double.NaN;
        private double _adjEma = double.NaN;
        private double _tradeIntBid = double.NaN;
        private double _tradeIntAsk = double.NaN;
        private double _tradeIntBidBase = double.NaN;
        private double _tradeIntAskBase = double.NaN;
        private double _tradeIntBidUnderlying = double.NaN;
        private double _tradeIntAskUnderlying = double.NaN;
        private double _bestBid = double.NaN;
        private double _bestAsk = double.NaN;
        private double _bestBidBase = double.NaN;
        private double _bestAskBase = double.NaN;
        private double _bestBidUnderlying = double.NaN;
        private double _bestAskUnderlying = double.NaN;

        private int _legsCount;
        public LiveChartViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
        {
            ModuleTitle = "Live Chart";
            ShowTestValue = OmsCore.Config.TestValueSubscriptionEnabled;
            ShowBaseLine = OmsCore.Config.TestValueSubscriptionEnabled;
            ShowAdjTheo = true;
            ShowAdjEma = true;
            ShowHwTheo = true;
            ShowUnderlying = false;
        }

        public new void SetDispatcher(Dispatcher dispatcher)
        {
            base.SetDispatcher(dispatcher);
            _chartUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _chartUpdateTimer.Tick += UpdateChart;
            _chartUpdateTimer.Start();
        }

        private void UpdateChart(object sender, EventArgs e)
        {
            try
            {
                _chartUpdateTimer.Stop();
                List<LiveChartUpdateModel> updates = null;
                double adjTheo = double.NaN;
                double adjEma = double.NaN;
                double hwTheo = double.NaN;
                double underlying = double.NaN;
                double testValue = double.NaN;
                double baseLine = double.NaN;
                double tradeIntBid = double.NaN;
                double tradeIntAsk = double.NaN;
                double tradeIntBidBase = double.NaN;
                double tradeIntAskBase = double.NaN;
                double tradeIntBidUnderlying = double.NaN;
                double tradeIntAskUnderlying = double.NaN;
                double bestBid = double.NaN;
                double bestAsk = double.NaN;
                double bestBidBase = double.NaN;
                double bestAskBase = double.NaN;
                double bestBidUnderlying = double.NaN;
                double bestAskUnderlying = double.NaN;
                DateTime timestamp;
                while (_updateQueue.Count > 0)
                {
                    if (_updateQueue.TryDequeue(out ChartValueModel[] update))
                    {
                        timestamp = default;
                        if (update[0] != null)
                        {
                            AdjTheoDataPoints.Add(update[0]);
                            timestamp = update[0].Timestamp;
                            adjTheo = update[0].Value;
                        }
                        if (update[1] != null)
                        {
                            AdjEmaDataPoints.Add(update[1]);
                            timestamp = update[0].Timestamp;
                            adjEma = update[1].Value;
                        }
                        if (update[2] != null)
                        {
                            HanweckTheoDataPoints.Add(update[2]);
                            timestamp = update[0].Timestamp;
                            hwTheo = update[2].Value;
                        }
                        if (update[3] != null)
                        {
                            UnderlyingDataPoints.Add(update[3]);
                            timestamp = update[0].Timestamp;
                            underlying = update[3].Value;
                        }
                        if (update[4] != null)
                        {
                            Leg1DataPoints.Add(update[4]);
                        }
                        if (update[5] != null)
                        {
                            Leg2DataPoints.Add(update[5]);
                        }
                        if (update[6] != null)
                        {
                            Leg3DataPoints.Add(update[6]);
                        }
                        if (update[7] != null)
                        {
                            Leg4DataPoints.Add(update[7]);
                        }
                        if (update[8] != null)
                        {
                            TestValueDataPoints.Add(update[8]);
                            timestamp = update[0].Timestamp;
                            testValue = update[8].Value;
                        }
                        if (update[9] != null)
                        {
                            BaseLineDataPoints.Add(update[9]);
                            timestamp = update[0].Timestamp;
                            baseLine = update[9].Value;
                        }
                        if (update[10] != null)
                        {
                            TradeIntBidDataPoints.Add(update[10]);
                            timestamp = update[0].Timestamp;
                            tradeIntBid = update[10].Value;
                        }
                        if (update[11] != null)
                        {
                            TradeIntAskDataPoints.Add(update[11]);
                            timestamp = update[0].Timestamp;
                            tradeIntAsk = update[11].Value;
                        }
                        if (update[12] != null)
                        {
                            timestamp = update[0].Timestamp;
                            tradeIntBidBase = update[12].Value;
                        }
                        if (update[13] != null)
                        {
                            timestamp = update[0].Timestamp;
                            tradeIntAskBase = update[13].Value;
                        }
                        if (update[14] != null)
                        {
                            timestamp = update[0].Timestamp;
                            tradeIntBidUnderlying = update[14].Value;
                        }
                        if (update[15] != null)
                        {
                            timestamp = update[0].Timestamp;
                            tradeIntAskUnderlying = update[15].Value;
                        }
                        if (update[16] != null)
                        {
                            BestBidDataPoints.Add(update[16]);
                            timestamp = update[0].Timestamp;
                            bestBid = update[16].Value;
                        }
                        if (update[17] != null)
                        {
                            BestAskDataPoints.Add(update[17]);
                            timestamp = update[0].Timestamp;
                            bestAsk = update[17].Value;
                        }
                        if (update[18] != null)
                        {
                            timestamp = update[0].Timestamp;
                            bestBidBase = update[18].Value;
                        }
                        if (update[19] != null)
                        {
                            timestamp = update[0].Timestamp;
                            bestAskBase = update[19].Value;
                        }
                        if (update[20] != null)
                        {
                            timestamp = update[0].Timestamp;
                            bestBidUnderlying = update[20].Value;
                        }
                        if (update[21] != null)
                        {
                            timestamp = update[0].Timestamp;
                            bestAskUnderlying = update[21].Value;
                        }

                        if (timestamp != default)
                        {
                            LiveChartUpdateModel tableUpdate = new()
                            {
                                Timestamp = timestamp,
                                AdjTheo = adjTheo,
                                AdjEma = adjEma,
                                HwTheo = hwTheo,
                                Underlying = underlying,
                                TestValue = testValue,
                                BaseLine = baseLine,
                                TradeIntBid = tradeIntBid,
                                TradeIntAsk = tradeIntAsk,
                                TradeIntBidBase = tradeIntBidBase,
                                TradeIntAskBase = tradeIntAskBase,
                                TradeIntBidUnderlying = tradeIntBidUnderlying,
                                TradeIntAskUnderlying = tradeIntAskUnderlying,
                                BestBid = bestBid,
                                BestAsk = bestAsk,
                                BestBidBase = bestBidBase,
                                BestAskBase = bestAskBase,
                                BestBidUnderlying = bestBidUnderlying,
                                BestAskUnderlying = bestAskUnderlying,
                            };
                            updates ??= new List<LiveChartUpdateModel>();
                            updates.Add(tableUpdate);
                        }
                    }
                }
                if (updates != null)
                {
                    Updates.AddRange(updates);
                    if (AutoScroll)
                    {
                        LastUpdate = Updates.LastOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(UpdateChart));
            }
            finally
            {
                if (!IsDisposed)
                {
                    _chartUpdateTimer.Start();
                }
            }
        }

        [Command]
        public void SearchCommand()
        {
            UnsubscribeAll();
            SubscribeAll();
        }

        private void UnsubscribeAll()
        {
            OmsCore.QuoteClient.UnsubscribeAll(this);
            OmsCore.GreekClient.UnsubscribeAll(this);
            OmsCore.UpdateManager.UnsubscribeAll(this);
            Clear();
        }

        private void SubscribeAll()
        {
            if (string.IsNullOrWhiteSpace(Symbol))
            {
                return;
            }
            SymbolCodec codec = new(Symbol);
            _legsCount = codec.LegCount;
            if (_legsCount > 0)
            {
                switch (_legsCount)
                {
                    case 4:
                        Leg1TheoLoaded = true;
                        Leg2TheoLoaded = true;
                        Leg3TheoLoaded = true;
                        Leg4TheoLoaded = true;
                        break;
                    case 3:
                        Leg1TheoLoaded = true;
                        Leg2TheoLoaded = true;
                        Leg3TheoLoaded = true;
                        break;
                    case 2:
                        Leg1TheoLoaded = true;
                        Leg2TheoLoaded = true;
                        break;
                    default:
                        Leg1TheoLoaded = true;
                        break;
                }

                UnderlyingSymbol = codec.UnderlyingSymbol();
                OmsCore.QuoteClient.Subscribe(UnderlyingSymbol, SubscriptionFieldType.MidPoint, this);
                ModuleTitle = $"{UnderlyingSymbol} - {Symbol} - Live Chart";

                for (int i = 0; i < _legsCount; i++)
                {
                    Instrument leg = codec.GetLeg(i);
                    string symbol = leg.symbol;
                    if (!_legLookup.TryGetValue(symbol, out LegModel legModel))
                    {
                        legModel = new LegModel()
                        {
                            Ratio = leg.buySell ? leg.ratio : -leg.ratio,
                        };
                        _legLookup[symbol] = legModel;
                        Legs.Add(legModel);
                    }
                    OmsCore.GreekClient.Subscribe(symbol, SubscriptionFieldType.Greeks, this);
                    OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DeltaAdjTheo, this);
                    OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DebugValue, this);
                    OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.FullEma, this);
                    OmsCore.UpdateManager.Subscribe(symbol, SubscriptionFieldType.DerivedValues, this);
                }
            }
        }

        private void Clear()
        {
            try
            {
                _updateQueue.Clear();
                _mid = double.NaN;
                _adjTheo = double.NaN;
                _adjEma = double.NaN;
                _legLookup.Clear();
                Legs.Clear();

                UnderlyingDataPoints.Clear();
                HanweckTheoDataPoints.Clear();
                TestValueDataPoints.Clear();
                BaseLineDataPoints.Clear();
                AdjTheoDataPoints.Clear();
                AdjEmaDataPoints.Clear();
                TradeIntBidDataPoints.Clear();
                TradeIntAskDataPoints.Clear();
                BestBidDataPoints.Clear();
                BestAskDataPoints.Clear();
                Leg1DataPoints.Clear();
                Leg2DataPoints.Clear();
                Leg3DataPoints.Clear();
                Leg4DataPoints.Clear();
                ResetPoints.Clear();

                Updates.Clear();

                Leg1TheoLoaded = false;
                Leg2TheoLoaded = false;
                Leg3TheoLoaded = false;
                Leg4TheoLoaded = false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Clear));
            }
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            string symbol = key.Symbol;
            SubscriptionFieldType type = key.Type;
            if (type == SubscriptionFieldType.MidPoint && value is double mid)
            {
                _mid = mid;
                DateTime time = DateTime.Now;
                UpdateChart(time);
            }
            else if (_legLookup.TryGetValue(symbol, out LegModel model))
            {
                DateTime time = DateTime.Now;
                switch (type)
                {
                    case SubscriptionFieldType.FullEma:
                        if (value is EmaUpdateModel emaUpdateModel)
                        {
                            model.EmaSeq = emaUpdateModel.Sequence;
                            switch (OmsCore.Config.LiveEmaPeriod)
                            {
                                case LiveEmaPeriod.Low:
                                    model.AdjEma = emaUpdateModel.LowPeriodEmaAdj;
                                    break;
                                case LiveEmaPeriod.Mid:
                                    model.AdjEma = emaUpdateModel.MidPeriodEmaAdj;
                                    break;
                                case LiveEmaPeriod.High:
                                    model.AdjEma = emaUpdateModel.HighPeriodEmaAdj;
                                    break;
                            }
                        }
                        break;
                    case SubscriptionFieldType.Greeks:
                        if (value is GreekUpdate greekUpdate)
                        {
                            model.HanweckTheo = greekUpdate.Theo;
                        }
                        break;
                    case SubscriptionFieldType.DeltaAdjTheo:
                        if (value is DeltaAdjTheo adjTheo)
                        {
                            model.AdjTheo = adjTheo.DeltaAdjustedTheo;
                            model.AdjTheoSeq = adjTheo.UpdateSequence;
                            model.TheoJumpDetected = adjTheo.JumpDetected;
                        }
                        break;
                    case SubscriptionFieldType.DerivedValues:
                        if (value is DerivedValueUpdateModel derivedValueUpdate)
                        {
                            model.TradeIntBid = derivedValueUpdate.BidTradeUpdate;
                            model.TradeIntAsk = derivedValueUpdate.AskTradeUpdate;
                            model.TradeIntBidBase = derivedValueUpdate.BidTradeBase;
                            model.TradeIntAskBase = derivedValueUpdate.AskTradeBase;
                            model.TradeIntBidUnderlying = derivedValueUpdate.BidTradeUnderlying;
                            model.TradeIntAskUnderlying = derivedValueUpdate.AskTradeUnderlying;
                            model.BestBid = derivedValueUpdate.BestBidUpdate;
                            model.BestAsk = derivedValueUpdate.BestAskUpdate;
                            model.BestBidBase = derivedValueUpdate.BestBidBase;
                            model.BestAskBase = derivedValueUpdate.BestAskBase;
                            model.BestBidUnderlying = derivedValueUpdate.BestBidUnderlying;
                            model.BestAskUnderlying = derivedValueUpdate.BestAskUnderlying;
                        }
                        break;
                }
                UpdateChart(time);
            }
        }

        private void UpdateChart(DateTime updateTime)
        {
            try
            {
                if (Legs.Count != _legsCount)
                {
                    return;
                }

                double hwUpdate = 0.0;
                double adjTheo = 0.0;
                double adjEma = 0.0;
                double testValue = 0.0;
                double baseLine = 0.0;
                double tradeIntBid = 0.0;
                double tradeIntAsk = 0.0;
                double tradeIntBidBase = 0.0;
                double tradeIntAskBase = 0.0;
                double tradeIntBidUnderlying = 0.0;
                double tradeIntAskUnderlying = 0.0;
                double bestBid = 0.0;
                double bestAsk = 0.0;
                double bestBidBase = 0.0;
                double bestAskBase = 0.0;
                double bestBidUnderlying = 0.0;
                double bestAskUnderlying = 0.0;

                double leg1 = double.NaN;
                double leg2 = double.NaN;
                double leg3 = double.NaN;
                double leg4 = double.NaN;


                uint seq = Legs[0].AdjTheoSeq;
                uint testTheoSeq = Legs[0].TestAdjTheoSeq;
                ulong emaSeq = Legs[0].EmaSeq;
                for (int i = 0; i < Legs.Count; i++)
                {
                    LegModel leg = Legs[i];

                    hwUpdate += leg.Ratio * leg.HanweckTheo;

                    if (!MatchSequence || seq == leg.AdjTheoSeq)
                    {
                        adjTheo += leg.Ratio * leg.AdjTheo;
                    }
                    else
                    {
                        adjTheo = double.NaN;
                    }

                    if (!MatchSequence || testTheoSeq == leg.TestAdjTheoSeq)
                    {
                        testValue += leg.Ratio * leg.TestAdjTheo;
                        baseLine += leg.Ratio * leg.BaseLine;
                    }
                    else
                    {
                        testValue = double.NaN;
                        baseLine = double.NaN;
                    }

                    if (!MatchSequence || emaSeq == leg.EmaSeq)
                    {
                        adjEma += leg.Ratio * leg.AdjEma;
                    }
                    else
                    {
                        adjEma = double.NaN;
                    }

                    tradeIntBid += leg.Ratio * leg.TradeIntBid;
                    tradeIntAsk += leg.Ratio * leg.TradeIntAsk;
                    tradeIntBidBase += leg.Ratio * leg.TradeIntBidBase;
                    tradeIntAskBase += leg.Ratio * leg.TradeIntAskBase;
                    tradeIntBidUnderlying += leg.Ratio * leg.TradeIntBidUnderlying;
                    tradeIntAskUnderlying += leg.Ratio * leg.TradeIntAskUnderlying;
                    bestBid += leg.Ratio * leg.BestBid;
                    bestAsk += leg.Ratio * leg.BestAsk;
                    bestBidBase += leg.Ratio * leg.BestBidBase;
                    bestAskBase += leg.Ratio * leg.BestAskBase;
                    bestBidUnderlying += leg.Ratio * leg.BestBidUnderlying;
                    bestAskUnderlying += leg.Ratio * leg.BestAskUnderlying;

                    switch (i)
                    {
                        case 0:
                            leg1 = leg.HanweckTheo;
                            break;
                        case 1:
                            leg2 = leg.HanweckTheo;
                            break;
                        case 2:
                            leg3 = leg.HanweckTheo;
                            break;
                        case 3:
                            leg4 = leg.HanweckTheo;
                            break;
                    }
                }

                if (double.IsNaN(adjTheo)) { adjTheo = _adjTheo; } else { _adjTheo = adjTheo; }
                if (double.IsNaN(adjEma)) { adjEma = _adjEma; } else { _adjEma = adjEma; }
                if (double.IsNaN(tradeIntBid)) { tradeIntBid = _tradeIntBid; } else { _tradeIntBid = tradeIntBid; }
                if (double.IsNaN(tradeIntAsk)) { tradeIntAsk = _tradeIntAsk; } else { _tradeIntAsk = tradeIntAsk; }
                if (double.IsNaN(tradeIntBidBase)) { tradeIntBidBase = _tradeIntBidBase; } else { _tradeIntBidBase = tradeIntBidBase; }
                if (double.IsNaN(tradeIntAskBase)) { tradeIntAskBase = _tradeIntAskBase; } else { _tradeIntAskBase = tradeIntAskBase; }
                if (double.IsNaN(tradeIntBidUnderlying)) { tradeIntBidUnderlying = _tradeIntBidUnderlying; } else { _tradeIntBidUnderlying = tradeIntBidUnderlying; }
                if (double.IsNaN(tradeIntAskUnderlying)) { tradeIntAskUnderlying = _tradeIntAskUnderlying; } else { _tradeIntAskUnderlying = tradeIntAskUnderlying; }
                if (double.IsNaN(bestBid)) { bestBid = _bestBid; } else { _bestBid = bestBid; }
                if (double.IsNaN(bestAsk)) { bestAsk = _bestAsk; } else { _bestAsk = bestAsk; }
                if (double.IsNaN(bestBidBase)) { bestBidBase = _bestBidBase; } else { _bestBidBase = bestBidBase; }
                if (double.IsNaN(bestAskBase)) { bestAskBase = _bestAskBase; } else { _bestAskBase = bestAskBase; }
                if (double.IsNaN(bestBidUnderlying)) { bestBidUnderlying = _bestBidUnderlying; } else { _bestBidUnderlying = bestBidUnderlying; }
                if (double.IsNaN(bestAskUnderlying)) { bestAskUnderlying = _bestAskUnderlying; } else { _bestAskUnderlying = bestAskUnderlying; }

                ChartValueModel[] update = new ChartValueModel[22];

                update[0] = !double.IsNaN(adjTheo) ? new ChartValueModel(adjTheo, updateTime) : null;
                update[1] = !double.IsNaN(adjEma) ? new ChartValueModel(adjEma, updateTime) : null;
                update[2] = !double.IsNaN(hwUpdate) ? new ChartValueModel(hwUpdate, updateTime) : null;
                update[3] = new ChartValueModel(_mid, updateTime);

                update[4] = !double.IsNaN(leg1) ? new ChartValueModel(leg1, updateTime) : null;
                update[5] = !double.IsNaN(leg2) ? new ChartValueModel(leg2, updateTime) : null;
                update[6] = !double.IsNaN(leg3) ? new ChartValueModel(leg3, updateTime) : null;
                update[7] = !double.IsNaN(leg4) ? new ChartValueModel(leg4, updateTime) : null;
                update[8] = !double.IsNaN(testValue) ? new ChartValueModel(testValue, updateTime) : null;
                update[9] = !double.IsNaN(baseLine) ? new ChartValueModel(baseLine, updateTime) : null;

                update[10] = !double.IsNaN(tradeIntBid) ? new ChartValueModel(tradeIntBid, updateTime) : null;
                update[11] = !double.IsNaN(tradeIntAsk) ? new ChartValueModel(tradeIntAsk, updateTime) : null;
                update[12] = !double.IsNaN(tradeIntBidBase) ? new ChartValueModel(tradeIntBidBase, updateTime) : null;
                update[13] = !double.IsNaN(tradeIntAskBase) ? new ChartValueModel(tradeIntAskBase, updateTime) : null;
                update[14] = !double.IsNaN(tradeIntBidUnderlying) ? new ChartValueModel(tradeIntBidUnderlying, updateTime) : null;
                update[15] = !double.IsNaN(tradeIntAskUnderlying) ? new ChartValueModel(tradeIntAskUnderlying, updateTime) : null;

                update[16] = !double.IsNaN(bestBid) ? new ChartValueModel(bestBid, updateTime) : null;
                update[17] = !double.IsNaN(bestAsk) ? new ChartValueModel(bestAsk, updateTime) : null;
                update[18] = !double.IsNaN(bestBidBase) ? new ChartValueModel(bestBidBase, updateTime) : null;
                update[19] = !double.IsNaN(bestAskBase) ? new ChartValueModel(bestAskBase, updateTime) : null;
                update[20] = !double.IsNaN(bestBidUnderlying) ? new ChartValueModel(bestBidUnderlying, updateTime) : null;
                update[21] = !double.IsNaN(bestAskUnderlying) ? new ChartValueModel(bestAskUnderlying, updateTime) : null;

                _updateQueue.Enqueue(update);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ChartValueModel));
            }
        }

        internal new void Dispose()
        {
            base.Dispose();
            _chartUpdateTimer.Stop();
            UnsubscribeAll();
        }

        public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
        {
            return null;
        }

        public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
        {
            return null;
        }
    }
}
