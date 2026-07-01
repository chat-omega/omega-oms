using System.Collections.ObjectModel;
using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Models.Data.Matrix.Strategies;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class SyntheticSpreadConfigDataModel : BindableBase
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
    public partial bool DiscretionTakeEnabled { get; set; }
    [Bindable]
    public partial double? DiscretionTake { get; set; }
    [Bindable]
    public partial bool MinQuoteQtyEnabled { get; set; }
    [Bindable]
    public partial uint? MinQuoteQty { get; set; }
    [Bindable]
    public partial bool TakeHiddenEnabled { get; set; }
    [Bindable]
    public partial bool? TakeHidden { get; set; }
    [Bindable]
    public partial bool BadRatioTimeoutEnabled { get; set; }
    [Bindable]
    public partial double? BadRatioTimeout { get; set; }
    [Bindable]
    public partial bool BadRatioPriceDiscretionEnabled { get; set; }
    [Bindable]
    public partial double? BadRatioPriceDiscretion { get; set; }
    [Bindable]
    public partial bool BadRatioTryThresholdEnabled { get; set; }
    [Bindable]
    public partial uint? BadRatioTryThreshold { get; set; }
    [Bindable]
    public partial bool WorkingQtyEnabled { get; set; }
    [Bindable]
    public partial uint? MinWorkingQty { get; set; }
    [Bindable]
    public partial uint? WorkingQty { get; set; }
    [Bindable]
    public partial bool ExtTradingHoursEnabled { get; set; }
    [Bindable]
    public partial bool? ExtTradingHours { get; set; }
    public bool CancelOnHaltEnabled
    {
        get => _cxlOnHaltEnabled;
        set => SetValue(ref _cxlOnHaltEnabled, value);
    }
    [Bindable]
    public partial bool? CancelOnHalt { get; set; }
    [Bindable]
    public partial bool LeggingOnlyEnabled { get; set; }
    [Bindable]
    public partial bool? LeggingOnly { get; set; }
    [Bindable]
    public partial bool AtsModeEnabled { get; set; }
    [Bindable]
    public partial bool? AtsMode { get; set; }
    [Bindable]
    public partial bool SeparateEquityLegEnabled { get; set; }
    [Bindable]
    public partial bool? SeparateEquityLeg { get; set; }
    [Bindable]
    public partial bool SynthFeeOptimalEnabled { get; set; }
    [Bindable]
    public partial bool? SynthFeeOptimal { get; set; }
    [Bindable]
    public partial bool TakeOnlyEnabled { get; set; }
    [Bindable]
    public partial bool? TakeOnly { get; set; }
    [Bindable]
    public partial bool NumOfTriesEnabled { get; set; }
    [Bindable]
    public partial uint? NumOfTries { get; set; }
    [Bindable]
    public partial bool PassiveModeEnabled { get; set; }
    [Bindable]
    public partial Algorithm? PassiveMode { get; set; }
    [Bindable]
    public partial bool PassiveModeCancelDelayEnabled { get; set; }
    [Bindable]
    public partial uint? PassiveModeCancelDelay { get; set; }
    [Bindable]
    public partial bool SpreadPriceDiscretionEnabled { get; set; }
    [Bindable]
    public partial bool? SpreadPriceDiscretion { get; set; }

    public SyntheticSpreadConfigDataModel()
    {
        Exchanges = new();
        ExchangesTake = new();
    }

    public void UpdateModel(SyntheticSpreadStrategyData strategyData)
    {
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
        BadRatioTimeoutEnabled = strategyData.BadRatioTimeout.HasValue;
        BadRatioTimeout = strategyData.BadRatioTimeout;
        BadRatioPriceDiscretionEnabled = strategyData.BadRatioPriceDiscretion.HasValue;
        BadRatioPriceDiscretion = strategyData.BadRatioPriceDiscretion;
        BadRatioTryThresholdEnabled = strategyData.BadRatioTryThreshold.HasValue;
        BadRatioTryThreshold = strategyData.BadRatioTryThreshold;
        WorkingQtyEnabled = strategyData.MinWorkingQty.HasValue && strategyData.WorkingQty.HasValue;
        MinWorkingQty = strategyData.MinWorkingQty;
        WorkingQty = strategyData.WorkingQty;
        ExtTradingHoursEnabled = strategyData.ExtTradingHours.HasValue;
        ExtTradingHours = strategyData.ExtTradingHours;
        CancelOnHaltEnabled = strategyData.CancelOnHalt.HasValue;
        CancelOnHalt = strategyData.CancelOnHalt;
        LeggingOnlyEnabled = strategyData.LeggingOnly.HasValue;
        LeggingOnly = strategyData.LeggingOnly;
        AtsModeEnabled = strategyData.AtsMode.HasValue;
        AtsMode = strategyData.AtsMode;
        SeparateEquityLegEnabled = strategyData.SeparateEquityLeg.HasValue;
        SeparateEquityLeg = strategyData.SeparateEquityLeg;
        SynthFeeOptimalEnabled = strategyData.SynthFeeOptimal.HasValue;
        SynthFeeOptimal = strategyData.SynthFeeOptimal;
        TakeOnlyEnabled = strategyData.SynthComplexTakeOnly.HasValue;
        TakeOnly = strategyData.SynthComplexTakeOnly;
        NumOfTriesEnabled = strategyData.NumOfTries.HasValue;
        NumOfTries = strategyData.NumOfTries;
        PassiveModeEnabled = strategyData.SynthPassiveMode.HasValue;
        PassiveMode = strategyData.SynthPassiveMode;
        PassiveModeCancelDelayEnabled = strategyData.SynthPassiveCancelDelayMs.HasValue;
        PassiveModeCancelDelay = strategyData.SynthPassiveCancelDelayMs;
        SpreadPriceDiscretionEnabled = strategyData.SpreadPriceDiscretion.HasValue;
        SpreadPriceDiscretion = strategyData.SpreadPriceDiscretion;
    }

    public void LoadModel(SyntheticSpreadStrategyData strategyData)
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
        strategyData.BadRatioTimeout = BadRatioTimeoutEnabled ? BadRatioTimeout : null;
        strategyData.BadRatioPriceDiscretion = BadRatioPriceDiscretionEnabled ? BadRatioPriceDiscretion : null;
        strategyData.BadRatioTryThreshold = BadRatioTryThresholdEnabled ? BadRatioTryThreshold : null;
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
        strategyData.ExtTradingHours = ExtTradingHoursEnabled ? ExtTradingHours : null;
        strategyData.CancelOnHalt = CancelOnHaltEnabled ? CancelOnHalt : null;
        strategyData.LeggingOnly = LeggingOnlyEnabled ? LeggingOnly : null;
        strategyData.AtsMode = AtsModeEnabled ? AtsMode : null;
        strategyData.SeparateEquityLeg = SeparateEquityLegEnabled ? SeparateEquityLeg : null;
        strategyData.SynthFeeOptimal = SynthFeeOptimalEnabled ? SynthFeeOptimal : null;
        strategyData.SynthComplexTakeOnly = TakeOnlyEnabled ? TakeOnly : null;
        strategyData.NumOfTries = NumOfTriesEnabled ? NumOfTries : null;
        strategyData.SynthPassiveMode = PassiveModeEnabled ? PassiveMode : null;
        strategyData.SynthPassiveCancelDelayMs = PassiveModeCancelDelayEnabled ? PassiveModeCancelDelay : null;
    }
}