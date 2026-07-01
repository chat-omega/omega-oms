using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Oms.Helper;
using ZeroPlus.Oms.Ui.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class GammaScalpViewModel : ModuleViewModelBase
{
    private readonly OmsCore _omsCore;

    private string _underlying;
    private string _hedgeUnderlying;

    private DispatcherTimer _uiUpdateTimer;
    public override Module Module { get; protected set; } = Module.GammaScalp;
    public IEnumerable<GreekSource> GreekSources { get; } = ((GreekSource[])Enum.GetValues(typeof(GreekSource))).ToList();

    [Bindable]
    public partial ObservableCollection<IQuoteDisplay> Legs { get; set; }
    [Bindable]
    public partial Scalper Scalper { get; set; }
    [Bindable]
    public partial GammaScalpOrderTicket OrderTicket { get; set; }
    [Bindable]
    public partial bool UnderlyingLoaded { get; set; }
    public string Underlying
    {
        get => _underlying;
        set => SetValue(ref _underlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
    }
    public string HedgeUnderlying
    {
        get => _hedgeUnderlying;
        set => SetValue(ref _hedgeUnderlying, OptionsHelper.IsIndex(value) ? "$" + value : value);
    }
    [Bindable(Default = 1)]
    public partial double HedgeMultiplier { get; set; }

    public GammaScalpViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, GammaScalpOrderTicket ticket) : base(configBrowserViewModel, omsCore)
    {
        _omsCore = omsCore;
        OrderTicket = ticket;
        Legs = new ObservableCollection<IQuoteDisplay>();
    }

    public override void OnSetDispatcher()
    {
        base.OnSetDispatcher();
        OrderTicket.Dispatcher = Dispatcher;
        StartUiUpdateTimer();
        OrderTicket.Legs.CollectionChanged += OnTicketLegsChanged;
        UpdateLegs();
    }

    public override void OnDispose()
    {
        base.OnDispose();
        DisposeScalper();
        OrderTicket.Legs.CollectionChanged -= OnTicketLegsChanged;
    }

    private void OnTicketLegsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateLegs();
    }

    private void UpdateLegs()
    {
        Dispatcher?.BeginInvoke(() =>
        {
            Legs.Clear();
            if (Scalper != null)
            {
                Legs.Add(Scalper);
            }
            foreach (var leg in OrderTicket.Legs)
            {
                Legs.Add(leg);
            }
        });
    }

    private void DisposeScalper()
    {
        if (Scalper != null)
        {
            Scalper.Running = false;
        }
    }

    private void StartUiUpdateTimer()
    {
        _uiUpdateTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(OmsCore.Config.TicketUiUpdateInterval),
        };
        _uiUpdateTimer.Dispatcher.Thread.Priority = ThreadPriority.AboveNormal;
        _uiUpdateTimer.Tick += (_, _) => OrderTicket.UpdateUiProperties();
        _uiUpdateTimer.Start();
    }

    public async Task LoadUnderlying(string underlying)
    {
        if (Underlying != underlying)
        {
            Underlying = underlying;
            HedgeUnderlying = underlying;
            HedgeMultiplier = 1;
        }
        await SearchUnderlyingCommand();
    }

    [Command]
    public async Task LiquidateCommand()
    {
        try
        {
            Scalper.Running = false;
            await Task.Run(() => Scalper.LiquidateHedge());
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(LiquidateCommand));
        }
    }

    [Command]
    public async Task SearchUnderlyingCommand()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(HedgeUnderlying))
            {
                HedgeUnderlying = Underlying;
                HedgeMultiplier = 1;
            }
            List<Data.Securities.Option> symbols = await OmsCore.QuoteClient.GetSymbolsAsync(Underlying);
            if (symbols.Count > 0)
            {
                MDUnderlying details = await OmsCore.QuoteClient.GetUnderlyingDetailsAsync(Underlying);
                if (details != null)
                {
                    UnderlyingLoaded = true;

                    if (OrderTicket.Underlying != Underlying)
                    {
                        OrderTicket.Underlying = Underlying;
                        await OrderTicket.SearchUnderlying();
                    }

                    DisposeScalper();
                    Scalper = new Scalper(_omsCore, OrderTicket, Underlying, HedgeUnderlying, HedgeMultiplier, details);
                    UpdateLegs();
                }
            }
            else
            {
                UnderlyingLoaded = false;
                MessageBoxService.ShowMessage("Symbol not found.");
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(SearchUnderlyingCommand));
        }
    }

    [Command]
    public void StartStopCommand()
    {
        try
        {
            Scalper.Running = !Scalper.Running;
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(StartStopCommand));
        }
    }

    [Command]
    public void ActivateAllCommand()
    {
        try
        {
            foreach (var position in OrderTicket.Legs)
            {
                position.Active = true;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(ActivateAllCommand));
        }
    }

    [Command]
    public void DeactivateAllCommand()
    {
        try
        {
            foreach (var position in OrderTicket.Legs)
            {
                position.Active = false;
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(ActivateAllCommand));
        }
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