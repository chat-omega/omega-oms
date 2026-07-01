using System.Collections.ObjectModel;
using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class SeekerConfigDataModel : BindableBase
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
    public partial bool AtsModeEnabled { get; set; }
    [Bindable]
    public partial bool? AtsMode { get; set; }

    public SeekerConfigDataModel()
    {
        Exchanges = new();
        ExchangesTake = new();
    }

    public void UpdateModel(SeekerStrategyData strategyData)
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
    }

    public void LoadModel(SeekerStrategyData strategyData)
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
    }

    public void LoadModel(SeekerSpreadStrategyData strategyData)
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
    }
}