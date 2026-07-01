using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.SpiderRock;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class CobFeedViewModel : ModuleViewModelBase, IOmsDataSubscriber
{
    private readonly ConcurrentQueue<ICobData> _updateQueue = new();
    private string _subscribedUnderlying = string.Empty;
    private string _underlyingInput = string.Empty;
    private DispatcherTimer _uiUpdateTimer;


    private readonly HashSet<BaseStrategy> _selectedStrategiesSet = Enum.GetValues<BaseStrategy>().ToHashSet();
    private readonly object _lock = new();

    private string _underlyingRequestInput = string.Empty;
    private CancellationTokenSource _requestCts;

    public override Module Module { get; protected set; } = Module.CobFeed;
    public List<StrategyTypes> Strategies { get; } = Enum.GetValues<StrategyTypes>().ToList();
    public List<PutCall> CallPut { get; } = Enum.GetValues<PutCall>().Where(x => x != PutCall.Unknown).ToList();
    public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();

    [Bindable]
    public partial bool AutoScroll { get; set; }
    public string SubscribedUnderlying
    {
        get => _subscribedUnderlying;
        set => SetValue(ref _subscribedUnderlying, (OptionsHelper.IsIndex(value) ? "$" + value : value)?.Trim().ToUpper());
    }
    public string UnderlyingInput
    {
        get => _underlyingInput;
        set => SetValue(ref _underlyingInput, (OptionsHelper.IsIndex(value) ? "$" + value : value)?.Trim().ToUpper());
    }
    public string UnderlyingRequestInput
    {
        get => _underlyingRequestInput;
        set => SetValue(ref _underlyingRequestInput, (OptionsHelper.IsIndex(value) ? "$" + value : value)?.Trim().ToUpper());
    }
    [Bindable]
    public partial bool IsLoading { get; set; }
    [Bindable]
    public partial bool UseManualTime { get; set; }
    [Bindable]
    public partial DateTime StartTime { get; set; }
    [Bindable]
    public partial DateTime EndTime { get; set; }
    [Bindable]
    public partial int Limit { get; set; }
    [Bindable]
    public partial string SelectedTime { get; set; }
    [Bindable]
    public partial SpreadBookQuote LastUpdate { get; set; }
    [Bindable]
    public partial SpreadExchOrder LastOrder { get; set; }
    [Bindable]
    public partial SpreadPrint LastPrint { get; set; }
    [Bindable]
    public partial AuctionPrint LastAuctionPrint { get; set; }
    [Bindable]
    public partial ObservableCollection<SpreadBookQuote> Updates { get; set; }
    [Bindable]
    public partial ObservableCollection<SpreadExchOrder> Orders { get; set; }
    [Bindable]
    public partial ObservableCollection<SpreadPrint> Prints { get; set; }
    [Bindable]
    public partial ObservableCollection<AuctionPrint> AuctionPrints { get; set; }
    [Bindable]
    public partial bool IsSubscribed { get; set; }
    [Bindable]
    public partial List<object> SelectedStrategies { get; set; }
    [Bindable]
    public partial List<object> SelectedCallPut { get; set; }
    [Bindable]
    public partial FastObservableCollection<SpreadExchPrint> Trades { get; set; }
    [Bindable]
    public partial bool IncludeTradesFromPriorDays { get; set; }

    public CobFeedViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
    {
        SelectedStrategies = Enum.GetValues<StrategyTypes>().Select(x => (object)x).ToList();
        SelectedCallPut = Enum.GetValues<PutCall>().Where(x => x != PutCall.Unknown).Select(x => (object)x).ToList();
        Updates = [];
        Orders = [];
        Prints = [];
        AuctionPrints = [];
        Trades = [];
        SelectedTime = "10 Min";
    }

    [Command]
    public void SearchUnderlyingCommand()
    {
        UnsubscribeCommand();
        if (!string.IsNullOrEmpty(UnderlyingInput))
        {
            SubscribedUnderlying = UnderlyingInput;
            OmsCore.UpdateManager.Subscribe(SubscribedUnderlying, SubscriptionFieldType.Cob, this);
            IsSubscribed = true;
            _uiUpdateTimer.Start();
        }
    }

    [Command]
    public void UnsubscribeCommand()
    {
        if (!string.IsNullOrEmpty(SubscribedUnderlying))
        {
            OmsCore.UpdateManager.Unsubscribe(SubscribedUnderlying, SubscriptionFieldType.Cob, this);
            SubscribedUnderlying = string.Empty;
            IsSubscribed = false;
            _uiUpdateTimer.Stop();
        }
    }

    [Command]
    public void RequestTradesCommand()
    {
        if (string.IsNullOrWhiteSpace(UnderlyingRequestInput))
        {
            return;
        }

        IsLoading = true;

        if (!UseManualTime)
        {
            SetTimeRange();
        }

        Task.Run(SendRequest);
    }

    private async void SendRequest()
    {
        _requestCts = new CancellationTokenSource();
        List<SpreadExchPrint> trades = await OmsCore.CobClient.Client.RequestCobTradesAsync(UnderlyingRequestInput, StartTime, EndTime, Limit, _requestCts.Token);
        if (trades != null)
        {
            Dispatcher.BeginInvoke(() =>
            {
                Trades.AddRange(trades);
                IsLoading = false;
            });
        }
    }

    [Command]
    public void CancelRequestCommand()
    {
        _requestCts?.Cancel();
        IsLoading = false;
    }

    [Command]
    public void SetTimeRange()
    {
        int min;
        switch (SelectedTime)
        {
            case "1 Min":
                min = 1;
                break;
            case "2 Min":
                min = 2;
                break;
            case "3 Min":
                min = 3;
                break;
            case "5 Min":
                min = 5;
                break;
            case "10 Min":
                min = 10;
                break;
            case "15 Min":
                min = 15;
                break;
            case "20 Min":
                min = 20;
                break;
            case "30 Min":
                min = 30;
                break;
            case "1 Hour":
                min = 60;
                break;
            case "Today":
                StartTime = DateTime.Today + TimeSpan.FromHours(5);
                EndTime = StartTime + TimeSpan.FromHours(13);
                return;
            default:
                return;
        }

        EndTime = DateTime.Now.ToEastern();
        StartTime = EndTime - TimeSpan.FromMinutes(min);
    }

    [Command]
    public void StrategiesUpdated()
    {
        lock (_lock)
        {
            _selectedStrategiesSet.Clear();
            if (SelectedCallPut != null && SelectedStrategies != null)
            {
                var callsEnabled = false;
                var putsEnabled = false;

                foreach (PutCall callPut in SelectedCallPut)
                {
                    switch (callPut)
                    {
                        case PutCall.Put:
                            putsEnabled = true;
                            break;
                        case PutCall.Call:
                            callsEnabled = true;
                            break;
                    }
                }

                foreach (var selectedStrategy in SelectedStrategies)
                {
                    switch ((StrategyTypes)selectedStrategy)
                    {
                        case StrategyTypes.Index:
                            _selectedStrategiesSet.Add(BaseStrategy.INDEX);
                            break;
                        case StrategyTypes.Stock:
                            _selectedStrategiesSet.Add(BaseStrategy.STOCK);
                            break;
                        case StrategyTypes.Invalid:
                            _selectedStrategiesSet.Add(BaseStrategy.INVALID);
                            break;
                        case StrategyTypes.Custom:
                            _selectedStrategiesSet.Add(BaseStrategy.CUSTOM);
                            break;
                        case StrategyTypes.Covered:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.COVERED_CALL);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.COVERED_PUT);
                            break;
                        case StrategyTypes.Protective:
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PROTECTIVE_PUT);
                            break;
                        case StrategyTypes.Vertical:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_VERTICAL);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_VERTICAL);
                            break;
                        case StrategyTypes.Calendar:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_CALENDAR);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_CALENDAR);
                            break;
                        case StrategyTypes.Diagonal:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_DIAGONAL);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_DIAGONAL);
                            break;
                        case StrategyTypes.Butterfly:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_BUTTERFLY);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_BUTTERFLY);
                            break;
                        case StrategyTypes.SkewedButterfly:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_SKEWED_BUTTERFLY);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_SKEWED_BUTTERFLY);
                            break;
                        case StrategyTypes.CalendarFly:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_CALENDAR_FLY);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_CALENDAR_FLY);
                            break;
                        case StrategyTypes.Triagonal:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_TRIAGONAL);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_TRIAGONAL);
                            break;
                        case StrategyTypes.Ratio1X2:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_1X2);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_1X2);
                            break;
                        case StrategyTypes.Ratio1X3:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_1X3);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_1X3);
                            break;
                        case StrategyTypes.Ratio2X3:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_2X3);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_2X3);
                            break;
                        case StrategyTypes.Condor:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_CONDOR);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_CONDOR);
                            break;
                        case StrategyTypes.OneThreeThreeOne:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_1X3X3X1);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_1X3X3X1);
                            break;
                        case StrategyTypes.Straddle:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.STRADDLE);
                            break;
                        case StrategyTypes.Strangle:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.STRANGLE);
                            break;
                        case StrategyTypes.Conversion:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.CONVERSION);
                            break;
                        case StrategyTypes.Reversal:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.REVERSAL);
                            break;
                        case StrategyTypes.IronCondor:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.IRON_CONDOR);
                            break;
                        case StrategyTypes.IronButterfly:
                            if (callsEnabled)
                                if (putsEnabled)
                                    _selectedStrategiesSet.Add(BaseStrategy.IRON_BUTTERFLY);
                            break;
                        case StrategyTypes.SingleLeg:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT);
                            break;
                        case StrategyTypes.SkewedCalendarFly:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_SKEWED_CALENDAR_FLY);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_SKEWED_CALENDAR_FLY);
                            break;
                        case StrategyTypes.StockTied:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_STOCK_TIED);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_STOCK_TIED);
                            break;
                        case StrategyTypes.OneThreeTwo:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_1x3x2);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_1x3x2);
                            break;
                        case StrategyTypes.TwoThreeOne:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_2x3x1);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_2x3x1);
                            break;
                        case StrategyTypes.Tree:
                            if (callsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.CALL_TREE);
                            if (putsEnabled)
                                _selectedStrategiesSet.Add(BaseStrategy.PUT_TREE);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }
    }

    [Command]
    public void ClearAll()
    {
        ClearUpdates();
        ClearOrders();
        ClearPrints();
        ClearAuctionPrints();
    }

    [Command]
    public void ClearUpdates()
    {
        Updates.Clear();
    }

    [Command]
    public void ClearOrders()
    {
        Orders.Clear();
    }

    [Command]
    public void ClearPrints()
    {
        Prints.Clear();
    }

    [Command]
    public void ClearAuctionPrints()
    {
        AuctionPrints.Clear();
    }

    public override void OnSetDispatcher()
    {
        base.OnSetDispatcher();
        _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _uiUpdateTimer.Tick += UpdateUi;
    }

    private void UpdateUi(object sender, EventArgs e)
    {
        try
        {
            _uiUpdateTimer.Stop();
            var count = 0;

            while (!_updateQueue.IsEmpty && IsSubscribed && count++ < 1000)
            {
                if (_updateQueue.TryDequeue(out var copy))
                {
                    if (copy is SpreadBookQuote spreadBookQuote)
                    {
                        Updates?.Add(spreadBookQuote);
                    }
                    else if (copy is SpreadExchOrder spreadExchOrder)
                    {
                        Orders?.Add(spreadExchOrder);
                    }
                    else if (copy is SpreadPrint spreadPrint)
                    {
                        Prints?.Add(spreadPrint);
                    }
                    else if (copy is AuctionPrint auctionPrint)
                    {
                        AuctionPrints?.Add(auctionPrint);
                    }
                }
            }

            if (AutoScroll)
            {
                LastUpdate = Updates?.LastOrDefault();
                LastOrder = Orders?.LastOrDefault();
                LastPrint = Prints?.LastOrDefault();
                LastAuctionPrint = AuctionPrints?.LastOrDefault();
            }
        }
        finally
        {
            if (!IsDisposed && IsSubscribed)
            {
                _uiUpdateTimer.Start();
            }
        }
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
    {
        if (IsSubscribed && key.Symbol.Equals(SubscribedUnderlying, StringComparison.InvariantCultureIgnoreCase) && value is ICobData cobData)
        {
            bool add;
            lock (_lock)
            {
                add = _selectedStrategiesSet.Contains(cobData.BaseStrategy);
            }

            if (add)
            {
                if (cobData is SpreadBookQuote spreadBookQuote)
                {
                    _updateQueue.Enqueue(new SpreadBookQuote(spreadBookQuote));
                }
                else if (cobData is SpreadExchOrder spreadExchOrder)
                {
                    _updateQueue.Enqueue(new SpreadExchOrder(spreadExchOrder));
                }
                else if (cobData is SpreadPrint spreadPrint)
                {
                    _updateQueue.Enqueue(new SpreadPrint(spreadPrint));
                }
                else if (cobData is AuctionPrint auctionPrint)
                {
                    if (IncludeTradesFromPriorDays || auctionPrint.TradeDate.Date >= DateTime.Today)
                    {
                        _updateQueue.Enqueue(new AuctionPrint(auctionPrint));
                    }
                }
            }
        }
    }

    public override void OnDispose()
    {
        base.OnDispose();
        UnsubscribeCommand();
        _updates.Clear();
        _updateQueue.Clear();
        Updates = null;
        Orders = null;
        Prints = null;
        AuctionPrints = null;
        LastUpdate = null;
        LastOrder = null;
        LastPrint = null;
        LastAuctionPrint = null;
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        return default;
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        return Task.CompletedTask;
    }
}