using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Models;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class ImpliedQuotesFeedViewModel : ModuleViewModelBase, IOmsDataSubscriber
{
    private readonly ConcurrentQueue<ImpliedQuoteUpdate> _updateQueue = new();
    private string _subscribedUnderlying = string.Empty;
    private string _underlyingInput = string.Empty;
    private DispatcherTimer _uiUpdateTimer;

    public override Module Module { get; protected set; } = Module.ImpliedQuoteFeed;
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
    public partial ImpliedQuoteUpdate LastUpdate { get; set; }
    [Bindable]
    public partial ObservableCollection<ImpliedQuoteUpdate> Updates { get; set; }
    [Bindable]
    public partial double MinCross { get; set; }
    [Bindable]
    public partial bool IsSubscribed { get; set; }

    public ImpliedQuotesFeedViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
    {
        Updates = [];
    }

    [Command]
    public void SearchUnderlyingCommand()
    {
        UnsubscribeCommand();
        if (!string.IsNullOrEmpty(UnderlyingInput))
        {
            SubscribedUnderlying = UnderlyingInput;
            OmsCore.UpdateManager.Subscribe(SubscribedUnderlying, SubscriptionFieldType.ImpliedQuoteCross, this);
            IsSubscribed = true;
            _uiUpdateTimer.Start();
        }
    }

    [Command]
    public void UnsubscribeCommand()
    {
        if (!string.IsNullOrEmpty(SubscribedUnderlying))
        {
            OmsCore.UpdateManager.Unsubscribe(SubscribedUnderlying, SubscriptionFieldType.ImpliedQuoteCross, this);
            SubscribedUnderlying = string.Empty;
            IsSubscribed = false;
            _uiUpdateTimer.Stop();
        }
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
                    Updates?.Add(copy);
                }
            }

            if (AutoScroll)
            {
                LastUpdate = Updates?.LastOrDefault();
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
        if (IsSubscribed && key.Symbol.Equals(SubscribedUnderlying, StringComparison.InvariantCultureIgnoreCase) && value is ImpliedQuoteUpdate impliedQuoteUpdate)
        {
            if (impliedQuoteUpdate.ImpliedBid - impliedQuoteUpdate.ImpliedAsk > MinCross)
            {
                _updateQueue.Enqueue(new ImpliedQuoteUpdate
                {
                    Index = impliedQuoteUpdate.Index,
                    Underlying = impliedQuoteUpdate.Underlying,
                    Symbol = impliedQuoteUpdate.Symbol,
                    Bid = impliedQuoteUpdate.Bid,
                    Ask = impliedQuoteUpdate.Ask,
                    Theo = impliedQuoteUpdate.Theo,
                    UnderBid = impliedQuoteUpdate.UnderBid,
                    UnderAsk = impliedQuoteUpdate.UnderAsk,
                    ImpliedBid = impliedQuoteUpdate.ImpliedBid,
                    ImpliedAsk = impliedQuoteUpdate.ImpliedAsk,
                    ImpliedBidRecordPrice = impliedQuoteUpdate.ImpliedBidRecordPrice,
                    ImpliedBidRecordTheo = impliedQuoteUpdate.ImpliedBidRecordTheo,
                    ImpliedBidRecordMovement = impliedQuoteUpdate.ImpliedBidRecordMovement,
                    ImpliedBidRecordTime = impliedQuoteUpdate.ImpliedBidRecordTime,
                    ImpliedAskRecordPrice = impliedQuoteUpdate.ImpliedAskRecordPrice,
                    ImpliedAskRecordTheo = impliedQuoteUpdate.ImpliedAskRecordTheo,
                    ImpliedAskRecordMovement = impliedQuoteUpdate.ImpliedAskRecordMovement,
                    ImpliedAskRecordTime = impliedQuoteUpdate.ImpliedAskRecordTime,
                });
            }
        }
    }

    public override void OnDispose()
    {
        base.OnDispose();
        UnsubscribeCommand();
        _updateQueue.Clear();
        Updates = null;
        LastUpdate = null;
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