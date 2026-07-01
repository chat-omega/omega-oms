using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Models;
using ZeroPlus.Oms.Data.Updates;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models;

public class ExplorerRowModel(OmsCore omsCore, Dispatcher dispatcher) : BindableBase, IOmsDataSubscriber
{
    public event Action<object, SubscriptionFieldType, byte> Updated;

    protected readonly OmsCore _omsCore = omsCore;
    protected readonly Dispatcher _dispatcher = dispatcher;
    private string _underlying;
    private string _symbol;
    private double _bid = double.NaN;
    private double _mid = double.NaN;
    private double _ask = double.NaN;

    public string Underlying { get => _underlying; set => SetValue(ref _underlying, value); }
    public string Symbol { get => _symbol; set => SetValue(ref _symbol, value); }
    public double Bid { get => _bid; set => SetValue(ref _bid, value); }
    public double Mid { get => _mid; set => SetValue(ref _mid, value); }
    public double Ask { get => _ask; set => SetValue(ref _ask, value); }

    private List<ExplorerLegRowModel> Legs { get; } = [];

    public bool IsDisposed { get; set; }

    public virtual void Initialize(string symbol)
    {
        Legs.Clear();
        Symbol = symbol;

        // Use OpsComplexOrderModel to parse both single and multi-leg symbols
        var tempOrder = new OpsComplexOrderModel { Symbol = symbol };
        tempOrder.AddLegs(_omsCore.SecurityBook, out string underlying);
        Underlying = underlying;

        foreach (var leg in tempOrder.Legs)
        {
            var legModel = new ExplorerLegRowModel(_omsCore, _dispatcher)
            {
                Ratio = leg.Ratio
            };
            legModel.Initialize(leg.Symbol);
            legModel.Updated += OnLegUpdate;
            Legs.Add(legModel);
        }

        // Trigger initial calculation
        OnLegUpdate(this, SubscriptionFieldType.Bid, 0);
    }

    public virtual void Subscribe()
    {
        foreach (var leg in Legs)
        {
            leg.Subscribe();
        }
    }

    public virtual void Unsubscribe()
    {
        foreach (var leg in Legs)
        {
            leg.Unsubscribe();
        }
    }

    protected void RaiseUpdated(SubscriptionFieldType type)
    {
        Updated?.Invoke(this, type, 0);
    }

    private void OnLegUpdate(object sender, SubscriptionFieldType type, byte modelId)
    {
        if (Legs.Count == 1)
        {
            var single = Legs[0];
            Bid = single.Bid;
            Ask = single.Ask;
            Mid = single.Mid;
        }
        else
        {
            Bid = Legs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.Ask) +
                  Legs.Where(x => x.Ratio > 0).Sum(x => x.Ratio * x.Bid);
            Ask = Legs.Where(x => x.Ratio < 0).Sum(x => x.Ratio * x.Bid) +
                  Legs.Where(x => x.Ratio > 0).Sum(x => x.Ratio * x.Ask);
            Mid = (Bid + Ask) * .5;
        }

        RaiseUpdated(SubscriptionFieldType.Bid);
    }

    public void SubscribedDataUpdateValue(SubscriptionKey key, object value, bool isFromCache = false)
    {
        switch (key.Type)
        {
            case SubscriptionFieldType.Bid when value is double bid:
                if (key.Symbol == Symbol)
                {
                    _dispatcher.Invoke(() =>
                    {
                        Bid = bid;
                        Mid = (Bid + Ask) * .5;
                        RaiseUpdated(key.Type);
                    });
                }
                break;
            case SubscriptionFieldType.Ask when value is double ask:
                if (key.Symbol == Symbol)
                {
                    _dispatcher.Invoke(() =>
                    {
                        Ask = ask;
                        Mid = (Bid + Ask) * .5;
                        RaiseUpdated(key.Type);
                    });
                }
                break;
        }
    }


    private class ExplorerLegRowModel(OmsCore omsCore, Dispatcher dispatcher) : ExplorerRowModel(omsCore, dispatcher)
    {
        public double Ratio { get; set; }

        public override void Initialize(string symbol)
        {
            Symbol = symbol;
            // Do not call base.Initialize for legs to avoid recursion
        }

        public override void Subscribe()
        {
            _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Bid, this);
            _omsCore.QuoteClient.Subscribe(Symbol, SubscriptionFieldType.Ask, this);
        }

        public override void Unsubscribe()
        {
            _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Bid, this);
            _omsCore.QuoteClient.Unsubscribe(Symbol, SubscriptionFieldType.Ask, this);
        }
    }
}
