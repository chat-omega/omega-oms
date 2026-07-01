using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class CloseSubsMonitorViewModel : ModuleViewModelBase
{
    private readonly TransactionConsumerModel _transactionConsumer;

    private System.Timers.Timer _uiClearTimer;

    [Bindable(Default = true)]
    public partial bool ShowZeroSubs { get; set; }
    [Bindable(Default = 10)]
    public partial int MinCount { get; set; }
    [Bindable(Default = 120)]
    public partial int ClearInterval { get; set; }
    [Bindable]
    public partial ObservableCollection<CloseSubsModel> Models { get; set; }
    [Bindable]
    public partial CloseSubsModel LastModel { get; set; }

    public override Module Module { get; protected set; } = Module.CloseSubsMonitor;

    public CloseSubsMonitorViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumer) : base(configBrowserViewModel, omsCore)
    {
        _transactionConsumer = transactionConsumer;
        _transactionConsumer.CloseSubsUpdated += OnCloseSubsUpdate;
        Models = new ObservableCollection<CloseSubsModel>();
    }

    public override void OnSetDispatcher()
    {
        base.OnSetDispatcher();
        _uiClearTimer = new System.Timers.Timer();
        _uiClearTimer.Interval = 30000;
        _uiClearTimer.AutoReset = false;
        _uiClearTimer.Elapsed += OnUpdateTimerTick;
        _uiClearTimer.Start();
    }

    public override void OnDispose()
    {
        base.OnDispose();
        _transactionConsumer.CloseSubsUpdated -= OnCloseSubsUpdate;
        if (_uiClearTimer != null)
        {
            _uiClearTimer.Elapsed -= OnUpdateTimerTick;
            _uiClearTimer.Stop();
            _uiClearTimer = null;
        }
    }

    private void OnUpdateTimerTick(object sender, EventArgs e)
    {
        try
        {
            List<CloseSubsModel> removeList = null;
            for (var index = Models.Count - 1; index >= 0; index--)
            {
                var model = Models[index];
                if ((DateTime.Now - model.Time).TotalSeconds > ClearInterval)
                {
                    removeList ??= new();
                    removeList.Add(model);
                }
            }

            if (removeList != null)
            {
                Dispatcher?.BeginInvoke(() =>
                {
                    foreach (var model in removeList)
                    {
                        Models.Remove(model);
                    }
                });
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                _uiClearTimer.Start();
            }
        }
    }

    private void OnCloseSubsUpdate(OmsOrderModel order)
    {
        if (!(ShowZeroSubs && order.CloseSubs == 0) && order.CloseSubs < MinCount)
        {
            return;
        }

        var model = new CloseSubsModel
        {
            Underlying = order.UnderlyingSymbol,
            SpreadId = order.SpreadId,
            Symbol = order.Symbol,
            Time = order.LastUpdateTime,
            Trader = order.Tag,
            CloseSubs = order.CloseSubs,
            AdjustedPnl = order.AdjustedPnl,
        };
        Dispatcher?.BeginInvoke(() =>
        {
            Models.Add(model);
            LastModel = model;
        });
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        return string.Empty;
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        return Task.CompletedTask;
    }
}