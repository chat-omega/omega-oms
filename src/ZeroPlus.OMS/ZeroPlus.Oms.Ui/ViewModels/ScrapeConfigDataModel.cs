using System;
using System.Collections.ObjectModel;
using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class ScrapeConfigDataModel : BindableBase
{


    private bool _cxlOnHaltEnabled;

    [Bindable]
    public partial bool ShowAdvancedConfigsPanel { get; set; }
    [Bindable]
    public partial ObservableCollection<ExchangeModel> Exchanges { get; set; }
    [Bindable]
    public partial ObservableCollection<ExchangeModel> ExchangesTake { get; set; }
    [Bindable]
    public partial bool MakeTakeEnabled { get; set; }
    [Bindable]
    public partial MakeTake? MakeTake { get; set; }
    [Bindable]
    public partial bool ExtTradingHoursEnabled { get; set; }
    [Bindable]
    public partial bool? ExtTradingHours { get; set; }
    [Bindable]
    public partial bool LimitToMarketTimeEnabled { get; set; }
    [Bindable]
    public partial DateTime? LimitToMarketTime { get; set; }
    [Bindable]
    public partial bool? TakeHidden { get; set; }
    [Bindable]
    public partial double? BadRatioTimeout { get; set; }
    [Bindable]
    public partial double? BadRatioPriceDiscretion { get; set; }
    [Bindable]
    public partial uint? BadRatioTryThreshold { get; set; }
    [Bindable]
    public partial bool WorkingQtyEnabled { get; set; }
    [Bindable]
    public partial uint? MinWorkingQty { get; set; }
    [Bindable]
    public partial uint? WorkingQty { get; set; }
    public bool CancelOnHaltEnabled
    {
        get => _cxlOnHaltEnabled;
        set => SetValue(ref _cxlOnHaltEnabled, value);
    }
    [Bindable]
    public partial bool? CancelOnHalt { get; set; }
    [Bindable]
    public partial bool? LeggingOnly { get; set; }
    [Bindable]
    public partial bool AtsModeEnabled { get; set; }
    [Bindable]
    public partial bool? AtsMode { get; set; }
    [Bindable]
    public partial bool? SeparateEquityLeg { get; set; }
    [Bindable]
    public partial bool? SynthFeeOptimal { get; set; }
    [Bindable]
    public partial bool? TakeOnly { get; set; }
    [Bindable]
    public partial uint? NumOfTries { get; set; }
    [Bindable]
    public partial Algorithm? PassiveMode { get; set; }
    [Bindable]
    public partial uint? PassiveModeCancelDelay { get; set; }
    [Bindable]
    public partial bool SpreadPriceDiscretionEnabled { get; set; }
    [Bindable]
    public partial bool? SpreadPriceDiscretion { get; set; }

    public ScrapeConfigDataModel()
    {
        Exchanges = new();
        ExchangesTake = new();
    }

    public void UpdateModel(ScrapeStrategyData strategyData)
    {
        if (strategyData == null)
        {
            return;
        }

        if (strategyData.Exchanges != null)
        {
            foreach (var exchange in strategyData.Exchanges)
            {
                Exchanges.Add(new ExchangeModel { Name = exchange });
            }
        }

        if (strategyData.ExchangesTake != null)
        {
            foreach (var exchange in strategyData.ExchangesTake)
            {
                ExchangesTake.Add(new ExchangeModel { Name = exchange });
            }
        }

        MakeTakeEnabled = strategyData.MakeTake.HasValue;
        MakeTake = strategyData.MakeTake;
        WorkingQtyEnabled = strategyData.MinWorkingQty.HasValue && strategyData.WorkingQty.HasValue;
        MinWorkingQty = strategyData.MinWorkingQty;
        WorkingQty = strategyData.WorkingQty;
        CancelOnHaltEnabled = strategyData.CancelOnHalt.HasValue;
        CancelOnHalt = strategyData.CancelOnHalt;
        AtsModeEnabled = strategyData.AtsMode.HasValue;
        AtsMode = strategyData.AtsMode;
        LimitToMarketTimeEnabled = strategyData.LimitToMarketTime.HasValue;
        LimitToMarketTime = strategyData.LimitToMarketTime;
    }

    public void LoadModel(ScrapeStrategyData strategyData)
    {
        strategyData.Exchanges?.Clear();
        foreach (var exchange in Exchanges)
        {
            strategyData.Exchanges?.Add(exchange.Name);
        }
        strategyData.ExchangesTake?.Clear();
        foreach (var exchange in ExchangesTake)
        {
            strategyData.ExchangesTake?.Add(exchange.Name);
        }
        strategyData.MakeTake = MakeTakeEnabled ? MakeTake : null;
        if (WorkingQtyEnabled)
        {
            strategyData.MinWorkingQty = MinWorkingQty;
            strategyData.WorkingQty = WorkingQty;
        }
        else
        {
            strategyData.MinWorkingQty = null;
            strategyData.WorkingQty = null;
        }
        strategyData.CancelOnHalt = CancelOnHaltEnabled ? CancelOnHalt : null;
        strategyData.AtsMode = AtsModeEnabled ? AtsMode : null;
        strategyData.LimitToMarketTime = LimitToMarketTimeEnabled ? LimitToMarketTime : null;
    }
}