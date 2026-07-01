using DevExpress.Mvvm;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Subscription;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Models
{
    public class SpreadHeatmapCell : BindableBase, IOmsDataSubscriber
    {
        private const int LAST_CHANGE_THRESHOLD = 1;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly NotificationManager _notificationManager;
        private List<Option> _options;
        private List<Option> _calls;
        private List<Option> _puts;
        private DataStore _deltaStore;

        private string _callSymbol;
        private string _putSymbol;
        private string _spreadTos;
        private bool _runningQuery;
        private DateTime _subscriptionTime;

        public DateTime Expiration { get; set; }
        public HeatmapSettingsModel LocalHeatmapSettingsModel { get; set; }
        public HeatmapSettingsModel GlobalHeatmapSettingsModel { get; }
        public bool IsDisposed { get; set; }

        public SpreadHeatmapAlert CellAlert { get; set; }
        public SpreadHeatmapAlert GroupAlert { get; set; }

        private DateTime _crossSetTime;
        private Timer _crossClearTimer;

        private static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public double CrossedTheo { get; set; }
        public double Width { get; set; }
        public double WidthHistoricAvg { get; private set; }
        public double Mid { get; set; }
        public double MidHistoricAvg { get; private set; }
        public double IvMid { get; set; }
        public double IvMidHistoricAvg { get; private set; }
        public string Title { get; set; }

        private string _Symbol;
        public string Symbol
        {
            get => _Symbol;
            set => SetProperty(ref _Symbol, value, nameof(Symbol));
        }

        private double _Spread;
        public double Spread
        {
            get => _Spread;
            set => SetProperty(ref _Spread, value, nameof(Spread));
        }

        private double _HeatPercent;
        public double HeatPercent
        {
            get => _HeatPercent;
            set => SetProperty(ref _HeatPercent, value, nameof(HeatPercent));
        }

        private bool _HideAlert;
        public bool HideAlert
        {
            get => _HideAlert;
            set => SetProperty(ref _HideAlert, value, nameof(HideAlert));
        }

        public bool Initialized { get; internal set; }

        public SpreadHeatmapCell(NotificationManager notificationManager)
        {
            _notificationManager = notificationManager;
            SetCrossClearTimer();
            Reset();
        }

        public SpreadHeatmapCell(HeatmapSettingsModel heatmapSettingsModel, HeatmapSettingsModel globalHeatmapSettingsModel, NotificationManager notificationManager) : this(notificationManager)
        {
            CellAlert = new SpreadHeatmapAlert();
            GlobalHeatmapSettingsModel = globalHeatmapSettingsModel;
            GlobalHeatmapSettingsModel.HeatmapRangeChangedEvent += OnHeatmapRangeChangedEvent;
            LocalHeatmapSettingsModel = heatmapSettingsModel;
            LocalHeatmapSettingsModel.HeatmapRangeChangedEvent += OnHeatmapRangeChangedEvent;
        }

        internal void Reset()
        {
            UnsubscribeSpread(_spreadTos);
            Width = double.NaN;
            Mid = double.NaN;
            IvMid = double.NaN;
            CrossedTheo = double.NaN;
            WidthHistoricAvg = double.NaN;
            MidHistoricAvg = double.NaN;
            IvMidHistoricAvg = double.NaN;
            Spread = double.NaN;
            HeatPercent = double.NaN;
            HideAlert = true;
            LocalHeatmapSettingsModel?.Reset();
        }

        public void OnHeatmapRangeChangedEvent(bool runQuery = false)
        {
            if (runQuery)
            {
                RerunQuery();
            }
            else
            {
                double value = double.NaN;
                switch (GlobalHeatmapSettingsModel.HeatMapMode)
                {
                    case HeatMapMode.Width:
                        if (GlobalHeatmapSettingsModel.HistoricLoadEnabled)
                        {
                            switch (GlobalHeatmapSettingsModel.Operator)
                            {
                                case Operator.Percentage:
                                    value = Width / WidthHistoricAvg * 100;
                                    break;
                                case Operator.Multiplier:
                                    value = Width / WidthHistoricAvg;
                                    break;
                            }
                        }
                        else
                        {
                            value = Width;
                        }
                        break;
                    case HeatMapMode.Mid:
                        if (GlobalHeatmapSettingsModel.HistoricLoadEnabled)
                        {
                            switch (GlobalHeatmapSettingsModel.Operator)
                            {
                                case Operator.Percentage:
                                    value = Mid / MidHistoricAvg * 100;
                                    break;
                                case Operator.Multiplier:
                                    value = Mid / MidHistoricAvg;
                                    break;
                            }
                        }
                        else
                        {
                            value = Mid;
                        }
                        break;
                    case HeatMapMode.IvVega:
                        if (GlobalHeatmapSettingsModel.HistoricLoadEnabled)
                        {
                            switch (GlobalHeatmapSettingsModel.Operator)
                            {
                                case Operator.Percentage:
                                    value = IvMid / IvMidHistoricAvg * 100;
                                    break;
                                case Operator.Multiplier:
                                    value = IvMid / IvMidHistoricAvg;
                                    break;
                            }
                        }
                        else
                        {
                            value = IvMid;
                        }
                        break;
                    case HeatMapMode.CrossedTheo:
                        value = CrossedTheo;
                        break;
                }
                Spread = value;
                GlobalHeatmapSettingsModel.SetValue(value);
                LocalHeatmapSettingsModel.SetValue(value);
                double heatPercent = default;
                if (GlobalHeatmapSettingsModel.Enabled)
                {
                    heatPercent = (value - GlobalHeatmapSettingsModel.Min) / (GlobalHeatmapSettingsModel.Max - GlobalHeatmapSettingsModel.Min) * 100;
                }
                else
                {
                    heatPercent = (value - LocalHeatmapSettingsModel.Min) / (LocalHeatmapSettingsModel.Max - LocalHeatmapSettingsModel.Min) * 100;
                }
                if (heatPercent != _HeatPercent)
                {
                    HeatPercent = heatPercent;
                    GlobalHeatmapSettingsModel.CompareForTop(Title, _Spread);
                }
            }
        }

        private void RerunQuery()
        {
            Reset();
            Task.Run(async () =>
            {
                await UpdateTargetAsync();
                QueryAsync();
            });
        }

        public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache)
        {
            try
            {
                if (key.Symbol == _spreadTos)
                {
                    switch (key.Type)
                    {
                        case SubscriptionFieldType.Spread:
                            if (value is double spread)
                            {
                                if (double.IsNaN(Width))
                                {
                                    FirstUpdateReceived();
                                }
                                Width = spread;
                                if (GlobalHeatmapSettingsModel.HeatMapMode == HeatMapMode.Width)
                                {
                                    LocalHeatmapSettingsModel.SetValue(Width);
                                    GlobalHeatmapSettingsModel.SetValue(Width);
                                }
                            }
                            break;
                        case SubscriptionFieldType.MidPoint:
                            if (value is double mid)
                            {
                                if (double.IsNaN(Mid))
                                {
                                    FirstUpdateReceived();
                                }
                                Mid = mid;
                                if (GlobalHeatmapSettingsModel.HeatMapMode == HeatMapMode.Mid)
                                {
                                    LocalHeatmapSettingsModel.SetValue(Mid);
                                    GlobalHeatmapSettingsModel.SetValue(Mid);
                                }
                            }
                            break;
                        case SubscriptionFieldType.Greeks:
                            if (value is GreekUpdate greeks)
                            {
                                if (double.IsNaN(IvMid))
                                {
                                    FirstUpdateReceived();
                                }
                                IvMid = (double)(greeks.Implied * greeks.Vega);
                                if (GlobalHeatmapSettingsModel.HeatMapMode == HeatMapMode.IvVega)
                                {
                                    LocalHeatmapSettingsModel.SetValue(IvMid);
                                    GlobalHeatmapSettingsModel.SetValue(IvMid);
                                }
                            }
                            break;

                    }
                    PostUpdate();
                }

                switch (key.Type)
                {
                    case SubscriptionFieldType.Greeks:
                        if ((key.Symbol == _callSymbol ||
                             key.Symbol == _putSymbol) && value is GreekUpdate greeks)
                        {
                            if (Math.Abs(greeks.Delta - GlobalHeatmapSettingsModel.Delta) > LAST_CHANGE_THRESHOLD)
                            {
                                UpdateTargetAsync();
                            }
                        }
                        break;
                    case SubscriptionFieldType.DerivedValues:
                        if (value is DerivedValueUpdateModel updateModel)
                        {
                            double cross = Math.Round(updateModel.CustTradeBid - updateModel.CustTradeAsk, 2);
                            if (!double.IsNaN(cross) && (double.IsNaN(CrossedTheo) || cross > CrossedTheo))
                            {
                                CrossedTheo = cross;
                                if (GlobalHeatmapSettingsModel.HeatMapMode == HeatMapMode.CrossedTheo)
                                {
                                    LocalHeatmapSettingsModel.SetValue(CrossedTheo);
                                    GlobalHeatmapSettingsModel.SetValue(CrossedTheo);
                                }
                                _crossSetTime = DateTime.Now;
                                if (_crossClearTimer.Enabled)
                                {
                                    _crossClearTimer.Stop();
                                }
                                _crossClearTimer.Start();
                                PostUpdate();
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SubscribedDataUpdateValue));
            }
        }

        private void FirstUpdateReceived()
        {
            _log.Info($"Heatmap first update received. Symbol: {_spreadTos}, Mode: {GlobalHeatmapSettingsModel.HeatMapMode}, Time: {(DateTime.Now - _subscriptionTime).TotalMilliseconds}ms");
        }

        private void PostUpdate()
        {
            if (!_HideAlert)
            {
                HideAlert = true;
            }
            CheckAlert(CellAlert);
            CheckAlert(GroupAlert);
            CheckAlert(GlobalHeatmapSettingsModel.GlobalAlert);
            OnHeatmapRangeChangedEvent();
        }

        private void SetCrossClearTimer()
        {
            _crossClearTimer = new Timer
            {
                AutoReset = false,
                Interval = 15_000,
            };
            _crossClearTimer.Elapsed += OnCrossClearTimerElapsed;
        }

        private void OnCrossClearTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if ((DateTime.Now - _crossSetTime).TotalSeconds > 20)
            {
                CrossedTheo = double.NaN;
            }
        }

        private void CheckAlert(SpreadHeatmapAlert alert)
        {
            if (alert.AlertEnabled && Spread > alert.Threshold)
            {
                if (alert.AudioEnabled)
                {
                    SoundManager.Play(alert.AudioSound);
                }
                if (alert.VisualEnabled && _HideAlert)
                {
                    HideAlert = false;
                }
                if (alert.NotificationEnabled)
                {
                    _notificationManager.AddAlert("HEATMAP ALERT - " + Symbol + " - " + Expiration.ToString("MMM dd yy") + "\n" + GlobalHeatmapSettingsModel.HeatMapMode + ": " + Spread, DateTime.Now, "Heatmap");
                }
                if (alert.ShareWithUsers.Count > 0)
                {
                    ConfigShare configShare = new()
                    {
                        Sender = OmsCore.User.ID,
                        Username = OmsCore.User.Username,
                        Receivers = alert.ShareWithUsers.ToList(),
                        Module = (int)Module.Notification,
                        Message = "ALERT [" + OmsCore.User.Username + "] - " + Symbol + " - " + Expiration.ToString("MMM dd yy") + "\n" + GlobalHeatmapSettingsModel.HeatMapMode + ": " + Spread,
                        SendTime = DateTime.Now,
                    };
                    OmsCore.GatewayClient.ShareConfig(configShare);
                }
            }
        }

        internal void LoadAsync(OptionChainModel optionChain, DataStore deltaStore)
        {
            List<Option> options = optionChain.OptionChain;
            if (options.Count > 0)
            {
                _deltaStore = deltaStore;
                _options = options.Where(x => x.Expiration == Expiration).ToList();
                _calls = _options.Where(x => x.PutCall == PutCall.Call).ToList();
                _puts = _options.Where(x => x.PutCall == PutCall.Put).ToList();
                UpdateTargetAsync();
            }
        }

        private async Task UpdateTargetAsync()
        {
            var stopwatch = Stopwatch.StartNew();

            List<Task> tasks = [];

            Task<string> callTask = null;
            if (GlobalHeatmapSettingsModel.CallSelected)
            {
                callTask = Task.Run(() => GetClosestDeltaOptionAsync(PutCall.Call));
                tasks.Add(callTask);
            }

            Task<string> putTask = null;
            if (GlobalHeatmapSettingsModel.PutSelected)
            {
                putTask = Task.Run(() => GetClosestDeltaOptionAsync(PutCall.Put));
                tasks.Add(putTask);
            }

            await Task.WhenAll(tasks);

            if (callTask != null)
            {
                _callSymbol = callTask.Result;
            }

            if (putTask != null)
            {
                _putSymbol = putTask.Result;
            }

            _log.Info($"Heatmap delta load complete for target selection. Exp: {Expiration:d}, Symbol: {_spreadTos}, Mode: {GlobalHeatmapSettingsModel.HeatMapMode}, Time: {stopwatch.ElapsedMilliseconds}");

            CheckToResub();
            stopwatch.Stop();
        }

        private Task<string> GetClosestDeltaOptionAsync(PutCall putCall)
        {
            if (OmsCore.SymbolMapClient.IsConnected)
            {
                return OmsCore.SymbolMapClient.Client.GetClosestOptionAsync(Symbol, putCall, Expiration, SubscriptionFieldType.Delta, GlobalHeatmapSettingsModel.Delta);
            }

            return GetClosestDeltaOptionAsync(putCall == PutCall.Call ? _calls : _puts, GlobalHeatmapSettingsModel.Delta);
        }

        private async Task<string> GetClosestDeltaOptionAsync(IEnumerable<Option> options, double delta)
        {
            var deltaTasks = options.Select(async option => new
            {
                Option = option,
                DeltaDifference = Math.Abs(await _deltaStore.GetDataAsync(option.Symbol) - delta)
            });

            var results = await Task.WhenAll(deltaTasks);
            var closestOption = results.OrderBy(r => r.DeltaDifference).FirstOrDefault();
            return closestOption?.Option?.Symbol;
        }

        private void CheckToResub()
        {
            string spreadTos;
            if (GlobalHeatmapSettingsModel.CallSelected && !GlobalHeatmapSettingsModel.PutSelected)
            {
                spreadTos = _callSymbol;
            }
            else if (!GlobalHeatmapSettingsModel.CallSelected && GlobalHeatmapSettingsModel.PutSelected)
            {
                spreadTos = _putSymbol;
            }
            else if (GlobalHeatmapSettingsModel.CallSelected && GlobalHeatmapSettingsModel.PutSelected)
            {
                spreadTos = _callSymbol + "+" + _putSymbol;
                SymbolLib.SymbolCodec codec = new(spreadTos);
                codec.Normalize();
                spreadTos = codec.ToTOS();
            }
            else
            {
                spreadTos = "";
            }

            if (spreadTos != _spreadTos)
            {
                UnsubscribeSpread(_spreadTos);
                _spreadTos = spreadTos;
                SubscribeSpread();
            }
        }

        private void QueryAsync()
        {
            if (GlobalHeatmapSettingsModel.HistoricLoadEnabled)
            {
                if (_runningQuery)
                {
                    return;
                }
                _runningQuery = true;
                MidHistoricAvg = double.NaN;
                WidthHistoricAvg = double.NaN;
                IvMidHistoricAvg = double.NaN;
                Task.Run(async () =>
                {
                    try
                    {
                        TimeSpan days = TimeSpan.FromDays(GlobalHeatmapSettingsModel.TotalDays);
                        TimeSpan mins = TimeSpan.FromMinutes(GlobalHeatmapSettingsModel.TotalMins);
                        DateTime endDateTime = DateTime.Now;
                        DateTime startDateTime = endDateTime - days - mins;

                        List<OptionSnapshot> results = await OmsCore.GatewayClient.RequestOptionSnapshotsAsync(Symbol, Expiration, GlobalHeatmapSettingsModel.Delta, startDateTime, endDateTime);
                        if (results != null)
                        {
                            List<OptionSnapshot> calls = new();
                            List<OptionSnapshot> puts = new();
                            if (GlobalHeatmapSettingsModel.CallSelected)
                            {
                                calls = results.Where(x => x.OptionType?.ToUpper() == "C").ToList();
                            }
                            if (GlobalHeatmapSettingsModel.PutSelected)
                            {
                                puts = results.Where(x => x.OptionType?.ToUpper() == "P").ToList();
                            }

                            if (results.Count > 0)
                            {
                                switch (GlobalHeatmapSettingsModel.HeatMapMode)
                                {
                                    case HeatMapMode.Mid:
                                        double callsAvg = calls.Count > 0 ? calls.Average(x => (x.Bid + x.Ask) * 0.5) : 0;
                                        double putsAvg = puts.Count > 0 ? puts.Average(x => (x.Bid + x.Ask) * 0.5) : 0;
                                        MidHistoricAvg = callsAvg + putsAvg;
                                        break;
                                    case HeatMapMode.Width:
                                        callsAvg = calls.Count > 0 ? calls.Average(x => Math.Abs(x.Bid - x.Ask)) : 0;
                                        putsAvg = puts.Count > 0 ? puts.Average(x => Math.Abs(x.Bid - x.Ask)) : 0;
                                        WidthHistoricAvg = callsAvg + putsAvg;
                                        break;
                                    case HeatMapMode.IvVega:
                                        callsAvg = calls.Count > 0 ? calls.Average(x => Math.Abs(x.HwIV * x.HwVega)) : 0;
                                        putsAvg = puts.Count > 0 ? puts.Average(x => Math.Abs(x.HwIV * x.HwVega)) : 0;
                                        IvMidHistoricAvg = callsAvg + putsAvg;
                                        break;
                                }
                                OnHeatmapRangeChangedEvent();
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    finally
                    {
                        _runningQuery = false;
                    }
                });
            }
        }

        private void SubscribeSpread()
        {
            if (!string.IsNullOrWhiteSpace(_spreadTos))
            {
                switch (GlobalHeatmapSettingsModel.HeatMapMode)
                {
                    case HeatMapMode.Width:
                        OmsCore.QuoteClient.Subscribe(_spreadTos, SubscriptionFieldType.Spread, this);
                        break;
                    case HeatMapMode.Mid:
                        OmsCore.QuoteClient.Subscribe(_spreadTos, SubscriptionFieldType.MidPoint, this);
                        break;
                    case HeatMapMode.CrossedTheo:
                        if (GlobalHeatmapSettingsModel.CallSelected)
                        {
                            foreach (Option symbol in _calls)
                            {
                                OmsCore.UpdateManager.Subscribe(symbol.Symbol, SubscriptionFieldType.DerivedValues, this);
                            }
                        }
                        if (GlobalHeatmapSettingsModel.PutSelected)
                        {
                            foreach (Option symbol in _puts)
                            {
                                OmsCore.UpdateManager.Subscribe(symbol.Symbol, SubscriptionFieldType.DerivedValues, this);
                            }
                        }
                        break;
                }
                OmsCore.GreekClient.Subscribe(_spreadTos, SubscriptionFieldType.Greeks, this);
                _subscriptionTime = DateTime.Now;
                _log.Info($"Heatmap subscribing to data. Exp: {Expiration:d}, Symbol: {_spreadTos}, Mode: {GlobalHeatmapSettingsModel.HeatMapMode}");
            }
        }

        private void UnsubscribeSpread(string spreadTos)
        {
            if (!string.IsNullOrWhiteSpace(spreadTos))
            {
                switch (GlobalHeatmapSettingsModel?.HeatMapMode)
                {
                    case HeatMapMode.Width:
                        OmsCore.QuoteClient.Unsubscribe(spreadTos, SubscriptionFieldType.Spread, this);
                        break;
                    case HeatMapMode.Mid:
                        OmsCore.QuoteClient.Unsubscribe(spreadTos, SubscriptionFieldType.MidPoint, this);
                        break;
                    case HeatMapMode.CrossedTheo:
                        OmsCore.UpdateManager.UnsubscribeAll(SubscriptionFieldType.DerivedValues, this);
                        break;
                }
                OmsCore.GreekClient.Unsubscribe(spreadTos, SubscriptionFieldType.Greeks, this);
            }
        }

        internal void Update()
        {
            if (Initialized)
            {
                RaisePropertyChanged(nameof(Spread));
                RaisePropertyChanged(nameof(HeatPercent));
            }
        }

        internal void ClearAlerts()
        {
            if (Initialized)
            {
                CellAlert.AlertEnabled = false;
                GroupAlert.AlertEnabled = false;
                HideAlert = true;
            }
        }

        internal void Dispose()
        {
            try
            {
                OmsCore.QuoteClient.UnsubscribeAll(this);
                OmsCore.GreekClient.UnsubscribeAll(this);
                Initialized = false;
                IsDisposed = true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Dispose));
            }
        }
    }
}
