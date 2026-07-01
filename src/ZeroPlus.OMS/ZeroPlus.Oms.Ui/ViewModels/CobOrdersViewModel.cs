using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Cob.Client.Models;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class CobOrdersViewModel : ModuleViewModelBase, IOmsDataSubscriber
{
    private readonly IModuleFactory _moduleFactory;
    private readonly ConcurrentQueue<OpenSpreadExchOrderModel> _updateQueue = new();
    private string _subscribedUnderlying = string.Empty;
    private string _underlyingInput = string.Empty;
    private DispatcherTimer _uiUpdateTimer;


    private readonly bool[] _selectedStrategiesSet;


    public override Module Module { get; protected set; } = Module.CobOrders;
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
    [Bindable]
    public partial bool IsLoading { get; set; }
    [Bindable]
    public partial OpenSpreadExchOrderModel LastOrder { get; set; }
    [Bindable]
    public partial ObservableCollection<OpenSpreadExchOrderModel> Orders { get; set; }
    [Bindable]
    public partial bool IsSubscribed { get; set; }
    [Bindable]
    public partial List<object> SelectedStrategies { get; set; }
    [Bindable]
    public partial List<object> SelectedCallPut { get; set; }

    public CobOrdersViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
    {
        _moduleFactory = moduleFactory;
        _selectedStrategiesSet = new bool[Enum.GetValues<BaseStrategy>().Length];
        Array.Fill(_selectedStrategiesSet, true);
        SelectedStrategies = Enum.GetValues<StrategyTypes>().Select(x => (object)x).ToList();
        SelectedCallPut = Enum.GetValues<PutCall>().Where(x => x != PutCall.Unknown).Select(x => (object)x).ToList();
        Orders = [];
    }

    [Command]
    public void SearchUnderlyingCommand()
    {
        UnsubscribeCommand();
        if (!string.IsNullOrEmpty(UnderlyingInput))
        {
            SubscribedUnderlying = UnderlyingInput;
            OmsCore.UpdateManager.Subscribe(SubscribedUnderlying, SubscriptionFieldType.CobOrders, this);
            IsSubscribed = true;
            _uiUpdateTimer.Start();
        }
    }

    [Command]
    public void UnsubscribeCommand()
    {
        if (!string.IsNullOrEmpty(SubscribedUnderlying))
        {
            OmsCore.UpdateManager.Unsubscribe(SubscribedUnderlying, SubscriptionFieldType.CobOrders, this);
            SubscribedUnderlying = string.Empty;
            IsSubscribed = false;
            _uiUpdateTimer.Stop();
        }
    }

    [Command]
    public void StrategiesUpdated()
    {
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
            Array.Fill(_selectedStrategiesSet, false);
            foreach (var selectedStrategy in SelectedStrategies)
            {
                switch ((StrategyTypes)selectedStrategy)
                {
                    case StrategyTypes.Index:
                        _selectedStrategiesSet[(int)BaseStrategy.INDEX] = true;
                        break;
                    case StrategyTypes.Stock:
                        _selectedStrategiesSet[(int)BaseStrategy.STOCK] = true;
                        break;
                    case StrategyTypes.Invalid:
                        _selectedStrategiesSet[(int)BaseStrategy.INVALID] = true;
                        break;
                    case StrategyTypes.Custom:
                        _selectedStrategiesSet[(int)BaseStrategy.CUSTOM] = true;
                        break;
                    case StrategyTypes.Covered:
                        _selectedStrategiesSet[(int)BaseStrategy.COVERED_CALL] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.COVERED_PUT] = putsEnabled;
                        break;
                    case StrategyTypes.Protective:
                        _selectedStrategiesSet[(int)BaseStrategy.PROTECTIVE_PUT] = putsEnabled;
                        break;
                    case StrategyTypes.Vertical:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_VERTICAL] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_VERTICAL] = putsEnabled;
                        break;
                    case StrategyTypes.Calendar:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_CALENDAR] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_CALENDAR] = putsEnabled;
                        break;
                    case StrategyTypes.Diagonal:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_DIAGONAL] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_DIAGONAL] = putsEnabled;
                        break;
                    case StrategyTypes.Butterfly:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_BUTTERFLY] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_BUTTERFLY] = putsEnabled;
                        break;
                    case StrategyTypes.SkewedButterfly:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_SKEWED_BUTTERFLY] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_SKEWED_BUTTERFLY] = putsEnabled;
                        break;
                    case StrategyTypes.CalendarFly:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_CALENDAR_FLY] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_CALENDAR_FLY] = putsEnabled;
                        break;
                    case StrategyTypes.Triagonal:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_TRIAGONAL] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_TRIAGONAL] = putsEnabled;
                        break;
                    case StrategyTypes.Ratio1X2:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_1X2] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_1X2] = putsEnabled;
                        break;
                    case StrategyTypes.Ratio1X3:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_1X3] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_1X3] = putsEnabled;
                        break;
                    case StrategyTypes.Ratio2X3:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_2X3] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_2X3] = putsEnabled;
                        break;
                    case StrategyTypes.Condor:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_CONDOR] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_CONDOR] = putsEnabled;
                        break;
                    case StrategyTypes.OneThreeThreeOne:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_1X3X3X1] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_1X3X3X1] = putsEnabled;
                        break;
                    case StrategyTypes.Straddle:
                        _selectedStrategiesSet[(int)BaseStrategy.STRADDLE] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.Strangle:
                        _selectedStrategiesSet[(int)BaseStrategy.STRANGLE] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.Conversion:
                        _selectedStrategiesSet[(int)BaseStrategy.CONVERSION] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.Reversal:
                        _selectedStrategiesSet[(int)BaseStrategy.REVERSAL] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.IronCondor:
                        _selectedStrategiesSet[(int)BaseStrategy.IRON_CONDOR] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.IronButterfly:
                        _selectedStrategiesSet[(int)BaseStrategy.IRON_BUTTERFLY] = callsEnabled && putsEnabled;
                        break;
                    case StrategyTypes.SingleLeg:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT] = putsEnabled;
                        break;
                    case StrategyTypes.SkewedCalendarFly:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_SKEWED_CALENDAR_FLY] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_SKEWED_CALENDAR_FLY] = putsEnabled;
                        break;
                    case StrategyTypes.StockTied:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_STOCK_TIED] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_STOCK_TIED] = putsEnabled;
                        break;
                    case StrategyTypes.OneThreeTwo:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_1x3x2] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_1x3x2] = putsEnabled;
                        break;
                    case StrategyTypes.TwoThreeOne:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_2x3x1] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_2x3x1] = putsEnabled;
                        break;
                    case StrategyTypes.Tree:
                        _selectedStrategiesSet[(int)BaseStrategy.CALL_TREE] = callsEnabled;
                        _selectedStrategiesSet[(int)BaseStrategy.PUT_TREE] = putsEnabled;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    [Command]
    public void OpenInComplexOrderTicketCommand(OpenSpreadExchOrderModel model)
    {
        try
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.ComplexOrderTicket))
            {
                if (_moduleFactory.CreateModule(Module.ComplexOrderTicket) is ComplexOrderTicketView { ViewModel: ComplexOrderTicketViewModel viewModel } view)
                {
                    viewModel.LoadLegsFromTosAsync(model.Symbol, model.Side, loadOptions: true);
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(OpenInBasketTraderCommand));
        }
    }

    [Command]
    public void OpenInBasketTraderCommand(OpenSpreadExchOrderModel model)
    {
        try
        {
            if (OmsCore.GatewayClient.GrantedModules.Contains((int)Module.BasketTrader))
            {
                if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                {
                    if (viewModel.IsReady)
                    {
                        Task.Run(() => OnReady(viewModel));
                    }
                    else
                    {
                        viewModel.Ready += OnReady;
                    }

                    void OnReady(IModuleViewModel _)
                    {
                        viewModel.Ready -= OnReady;
                        viewModel.LoadFromSymbol(model.Symbol);

                    }
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(OpenInBasketTraderCommand));
        }
    }

    [Command]
    public void ClearAll()
    {
        ClearOrders();
    }

    [Command]
    public void ClearOrders()
    {
        Orders.Clear();
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
                    switch (copy.UpdateType)
                    {
                        case UpdateType.Add:
                            Orders?.Add(copy);
                            break;
                        case UpdateType.Remove:
                            Orders?.Remove(copy);
                            break;
                    }
                }

                if (AutoScroll)
                {
                    LastOrder = Orders?.LastOrDefault();
                }
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
        if (IsSubscribed && key.Symbol.Equals(SubscribedUnderlying, StringComparison.InvariantCultureIgnoreCase) && value is OpenSpreadExchOrderModel cobData)
        {
            bool add = false;
            if (cobData.BaseStrategy != null)
            {
                add = _selectedStrategiesSet[(int)cobData.BaseStrategy];
            }

            if (add)
            {
                _updateQueue.Enqueue(cobData);
            }
        }
    }

    public override void OnDispose()
    {
        base.OnDispose();
        UnsubscribeCommand();
        _updateQueue.Clear();
        Orders = null;
        LastOrder = null;
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