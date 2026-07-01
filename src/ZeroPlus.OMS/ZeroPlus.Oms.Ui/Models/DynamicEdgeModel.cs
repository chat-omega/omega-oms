using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DynamicEdgeModel : BindableBase, IDynamicEdgeModel
    {
        [JsonIgnore]
        public IEnumerable<TheoModel> VolaModels { get; } = Enum.GetValues<TheoModel>().Where(x => x != TheoModel.Hanw).ToList();
        [JsonIgnore]
        public ConfigSave Details { get; set; }

        [JsonProperty]
        public int Id { get; set; }

        private bool _StaticLookupMode;
        [JsonProperty]
        public bool DynamicLookupMode
        {
            get => _StaticLookupMode;
            set => SetValue(ref _StaticLookupMode, value);
        }

        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string Creator { get; set; }

        [JsonProperty]
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool EmaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool PercentBidRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool TradePxRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool MinMarketWidthEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool MinMarketCrossEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool TheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool VolaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable(Default = TheoModel.VolaV0)]
        public partial TheoModel VolaModel { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool DynamicPercentBidRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool DynamicEmaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool DynamicTheoRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool DynamicVolaRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable(Default = TheoModel.VolaV0)]
        public partial TheoModel DynamicVolaModel { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool DynamicWidthRangeEnabled { get; set; }

        [JsonProperty]
        [Bindable(Default = 1000)]
        public partial int UnderDivisor { get; set; }

        [JsonProperty]
        [Bindable(Default = true)]
        public partial bool BaseEdgeEnabled { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<DaysToExpirationEdgeModel> DteTable { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<DaysToExpirationEdgeModel> DynamicDteTable { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<DeltaEdgeModel> DeltaTable { get; set; }

        private ConcurrentDictionary<string, double> _underMultiplierLookup = new();
        private ObservableCollection<UnderMultiplierModel> _UnderMultiplierTable;
        [JsonProperty]
        public ObservableCollection<UnderMultiplierModel> UnderMultiplierTable
        {
            get => _UnderMultiplierTable;
            set
            {
                SetValue(ref _UnderMultiplierTable, value);
                Load();
            }
        }

        public DynamicEdgeModel()
        {
            DteTable = new();
            DeltaTable = new();
            DynamicDteTable = new();
            UnderMultiplierTable = new();
        }

        public bool GetEdge(bool fish,
                            BaseStrategy strategy,
                            string underlyingSymbol,
                            double underlying,
                            double strikeSpacing,
                            int daysToExpiration,
                            int contracts,
                            int minOfBidAskSize,
                            double delta,
                            double width,
                            double minTick,
                            GetDouble getWeightedVega,
                            out double edge,
                            out double loopMinEdge,
                            out double loopMaxLoss,
                            out double maxThroughTheo,
                            out double maxThroughVola,
                            out TheoModel volaModel,
                            out double maxPercentBid,
                            out double maxThroughEma,
                            out double maxThroughTradePx,
                            out double minMarketWidth,
                            out double minMarketCross,
                            out int qty,
                            out double permMinEdge,
                            out string reason)
        {
            reason = string.Empty;
            edge = double.NaN;
            loopMinEdge = double.NaN;
            loopMaxLoss = double.NaN;
            maxThroughTheo = double.NaN;
            maxThroughVola = double.NaN;
            volaModel = TheoModel.VolaV0;
            maxPercentBid = double.NaN;
            maxThroughEma = double.NaN;
            maxThroughTradePx = double.NaN;
            minMarketWidth = double.NaN;
            minMarketCross = double.NaN;
            permMinEdge = double.NaN;
            qty = 1;
            if (daysToExpiration < 0)
            {
                reason = "Invalid DTE.";
                return false;
            }
            else if (contracts < 0 || contracts > 4)
            {
                reason = "Invalid Contracts.";
                return false;
            }
            else if (!DynamicLookupMode)
            {
                DaysToExpirationEdgeModel dteModel = DteTable.Where(x => x.Active && x.DaysToExpiration >= daysToExpiration && minOfBidAskSize >= x.MinBidAskSize && minTick >= x.MinIncrement && Math.Abs(width) >= x.MinWidth).OrderBy(x => x.DaysToExpiration).ThenByDescending(x => x.MinWidth).FirstOrDefault();

                if (dteModel == null)
                {
                    reason = "DTE lookup failed.";
                    return false;
                }
                else
                {
                    switch (strategy)
                    {
                        case BaseStrategy.CALL_VERTICAL:
                        case BaseStrategy.PUT_VERTICAL:
                            if (strikeSpacing < dteModel.MinSpacingForVertical)
                            {
                                reason = $"{strategy} Strike Spacing failed.";
                                return false;
                            }
                            break;
                        case BaseStrategy.CALL_BUTTERFLY:
                        case BaseStrategy.PUT_BUTTERFLY:
                        case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                        case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                            if (strikeSpacing < dteModel.MinSpacingForFlys)
                            {
                                reason = $"{strategy} Strike Spacing failed.";
                                return false;
                            }
                            break;
                    }

                    DeltaEdgeModel deltaAdditionModel = DeltaTable.Where(x => x.Active && Math.Abs(x.Delta) <= Math.Abs(delta)).OrderByDescending(x => Math.Abs(x.Delta)).FirstOrDefault();
                    bool addDelta = deltaAdditionModel != null;

                    edge = fish || double.IsNaN(dteModel.CloseEdge) ? dteModel.BaseEdge : dteModel.CloseEdge;
                    loopMinEdge = dteModel.LoopMinEdge;
                    if (contracts > 1)
                    {
                        double addition = (contracts - 1) * dteModel.AdditionalEdgePerContract;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    double wVega = Math.Abs(getWeightedVega(false));
                    if (!double.IsNaN(wVega))
                    {
                        double addition = wVega * dteModel.AdditionalEdgePerWeightedVega;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    if (addDelta)
                    {
                        double addition = deltaAdditionModel.AddedEdge + contracts * deltaAdditionModel.AdditionalEdgePerContract;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    if (UnderMultiplierTable != null &&
                        UnderMultiplierTable.Count > 0 &&
                        !string.IsNullOrWhiteSpace(underlyingSymbol) &&
                        _underMultiplierLookup.TryGetValue(underlyingSymbol, out var multiplier))
                    {
                        edge *= Math.Abs(multiplier);
                    }

                    edge = !fish || BaseEdgeEnabled ? Math.Round(edge, 3) : 0;
                    loopMinEdge = Math.Round(loopMinEdge, 3);

                    qty = strategy switch
                    {
                        BaseStrategy.CALL_VERTICAL or BaseStrategy.PUT_VERTICAL => Math.Max(dteModel.VerticalQty, qty),
                        _ => Math.Max(dteModel.Qty, qty),
                    };
                    maxThroughTheo = TheoRangeEnabled ? -dteModel.MaxAllowedAboveTheo : double.NaN;
                    maxThroughVola = VolaRangeEnabled ? -dteModel.MaxAllowedAboveVola : double.NaN;
                    volaModel = VolaModel;
                    maxThroughEma = EmaRangeEnabled ? -dteModel.MaxAllowedAboveEma : double.NaN;

                    permMinEdge = dteModel.AutoPermMinEdge;
                    loopMaxLoss = dteModel.LoopMaxLoss;
                    maxPercentBid = PercentBidRangeEnabled ? dteModel.MaxAllowedPercentBid : double.NaN;
                    maxThroughTradePx = TradePxRangeEnabled ? dteModel.MaxThroughTradePx : double.NaN;
                    minMarketWidth = MinMarketWidthEnabled ? dteModel.MinMarketWidth : double.NaN;
                    minMarketCross = MinMarketCrossEnabled ? dteModel.MinMarketCross : double.NaN;
                    return true;
                }
            }
            else
            {
                DaysToExpirationEdgeModel dteModel = DynamicDteTable.Where(x => x.Active && x.DaysToExpiration >= daysToExpiration && minOfBidAskSize >= x.MinBidAskSize && minTick >= x.MinIncrement && Math.Abs(width) >= x.MinWidth).OrderBy(x => x.DaysToExpiration).ThenByDescending(x => x.MinWidth).FirstOrDefault();

                if (dteModel == null)
                {
                    reason = "DTE lookup failed.";
                    return false;
                }
                else
                {
                    switch (strategy)
                    {
                        case BaseStrategy.CALL_VERTICAL:
                        case BaseStrategy.PUT_VERTICAL:
                            if (strikeSpacing < underlying * dteModel.MinSpacingForVerticalPercentage)
                            {
                                reason = $"{strategy} Strike Spacing failed.";
                                return false;
                            }
                            break;
                        case BaseStrategy.CALL_BUTTERFLY:
                        case BaseStrategy.PUT_BUTTERFLY:
                        case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                        case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                            if (strikeSpacing < underlying * dteModel.MinSpacingForFlysPercentage)
                            {
                                reason = $"{strategy} Strike Spacing failed.";
                                return false;
                            }
                            break;
                    }

                    DeltaEdgeModel deltaAdditionModel = DeltaTable.Where(x => x.Active && Math.Abs(x.Delta) <= Math.Abs(delta)).OrderByDescending(x => Math.Abs(x.Delta)).FirstOrDefault();
                    bool addDelta = deltaAdditionModel != null;

                    if (fish || double.IsNaN(dteModel.DynamicCloseEdge))
                    {
                        double additionPerWidth = width * dteModel.AdditionalEdgePerWidth;
                        if (double.IsNaN(additionPerWidth))
                        {
                            additionPerWidth = 0;
                        }
                        edge = dteModel.DynamicBaseEdge + (underlying / UnderDivisor * dteModel.DynamicBaseEdgeAddition) + additionPerWidth;
                    }
                    else
                    {
                        double additionPerWidth = width * dteModel.AdditionalCloseEdgePerWidth;
                        if (double.IsNaN(additionPerWidth))
                        {
                            additionPerWidth = 0;
                        }
                        edge = dteModel.DynamicCloseEdge + (underlying / UnderDivisor * dteModel.DynamicCloseEdgeAddition) + additionPerWidth;
                    }


                    loopMinEdge = dteModel.DynamicLoopMinEdge + (underlying / UnderDivisor * dteModel.DynamicLoopMinEdgeAddition);
                    if (contracts > 1)
                    {
                        double addition = (contracts - 1) * (dteModel.DynamicAdditionalEdgePerContract + (underlying / UnderDivisor * dteModel.DynamicAdditionalEdgePerContractAddition));
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    double wVega = getWeightedVega(false);
                    if (!double.IsNaN(wVega))
                    {
                        double addition = wVega * (dteModel.DynamicAdditionalEdgePerWeightedVega + (underlying / UnderDivisor * dteModel.DynamicAdditionalEdgePerWeightedVegaAddition));
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    if (addDelta)
                    {
                        double addition = deltaAdditionModel.AddedEdge + contracts * deltaAdditionModel.AdditionalEdgePerContract;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    if (UnderMultiplierTable != null &&
                        UnderMultiplierTable.Count > 0 &&
                        !string.IsNullOrWhiteSpace(underlyingSymbol) &&
                        _underMultiplierLookup.TryGetValue(underlyingSymbol, out var multiplier))
                    {
                        edge *= Math.Abs(multiplier);
                    }

                    edge = !fish || BaseEdgeEnabled ? Math.Round(edge, 3) : 0;
                    loopMinEdge = Math.Round(loopMinEdge, 3);

                    qty = strategy switch
                    {
                        BaseStrategy.CALL_VERTICAL or BaseStrategy.PUT_VERTICAL => Math.Max(dteModel.VerticalQty, qty),
                        _ => Math.Max(dteModel.Qty, qty),
                    };
                    maxThroughTheo = DynamicTheoRangeEnabled ? -(dteModel.DynamicMaxAllowedAboveTheo + (underlying / UnderDivisor * dteModel.DynamicMaxAllowedAboveTheoAddition)) : double.NaN;
                    maxThroughVola = DynamicVolaRangeEnabled ? -(dteModel.DynamicMaxAllowedAboveVola + (underlying / UnderDivisor * dteModel.DynamicMaxAllowedAboveVolaAddition)) : double.NaN;
                    volaModel = DynamicVolaModel;
                    maxThroughEma = DynamicEmaRangeEnabled ? -(dteModel.DynamicMaxAllowedAboveEma + (underlying / UnderDivisor * dteModel.DynamicMaxAllowedAboveEmaAddition)) : double.NaN;

                    permMinEdge = dteModel.DynamicAutoPermMinEdge + (underlying / UnderDivisor * dteModel.DynamicAutoPermMinEdgeAddition);
                    loopMaxLoss = dteModel.DynamicLoopMaxLoss + (underlying / UnderDivisor * dteModel.DynamicLoopMaxLossAddition);
                    maxPercentBid = DynamicPercentBidRangeEnabled ? (dteModel.DynamicMaxAllowedPercentBid + (underlying / UnderDivisor * dteModel.DynamicMaxAllowedPercentBidAddition)) : double.NaN;
                    minMarketWidth = DynamicWidthRangeEnabled ? (dteModel.DynamicMinMarketWidth + (underlying / UnderDivisor * dteModel.DynamicMinMarketWidthAddition)) : double.NaN;
                    return true;
                }
            }
        }

        internal void CloneFrom(DynamicEdgeModel model)
        {
            LastUpdateTime = DateTime.Now;
            DteTable = new();
            DeltaTable = new();
            DynamicDteTable = new();
            UnderMultiplierTable = new();

            Title = model.Title;
            Details = null;

            TheoRangeEnabled = model.TheoRangeEnabled;
            VolaRangeEnabled = model.VolaRangeEnabled;
            VolaModel = model.VolaModel;
            EmaRangeEnabled = model.EmaRangeEnabled;
            PercentBidRangeEnabled = model.PercentBidRangeEnabled;
            BaseEdgeEnabled = model.BaseEdgeEnabled;
            TradePxRangeEnabled = model.TradePxRangeEnabled;
            MinMarketWidthEnabled = model.MinMarketWidthEnabled;
            MinMarketCrossEnabled = model.MinMarketCrossEnabled;

            foreach (DaysToExpirationEdgeModel filter in model.DteTable.OrderBy(x => x.DaysToExpiration))
            {
                DaysToExpirationEdgeModel newFilter = filter.Clone();
                if (newFilter != null)
                {
                    DteTable.Add(newFilter);
                }
            }
            foreach (DaysToExpirationEdgeModel filter in model.DynamicDteTable.OrderBy(x => x.DaysToExpiration))
            {
                DaysToExpirationEdgeModel newFilter = filter.Clone();
                if (newFilter != null)
                {
                    DynamicDteTable.Add(newFilter);
                }
            }
            foreach (UnderMultiplierModel filter in model.UnderMultiplierTable.OrderBy(x => x.Under))
            {
                UnderMultiplierModel newFilter = filter.Clone();
                if (newFilter != null)
                {
                    UnderMultiplierTable.Add(newFilter);
                }
            }
            foreach (DeltaEdgeModel filter in model.DeltaTable.OrderBy(x => x.Delta))
            {
                DeltaEdgeModel newFilter = filter.Clone();
                if (newFilter != null)
                {
                    DeltaTable.Add(newFilter);
                }
            }

            Load();
        }

        internal string GetAsJson()
        {
            DteTable = DteTable.OrderBy(x => x.DaysToExpiration).ToObservableCollection();
            DynamicDteTable = DynamicDteTable.OrderBy(x => x.DaysToExpiration).ToObservableCollection();
            UnderMultiplierTable = UnderMultiplierTable.OrderBy(x => x.Under).ToObservableCollection();
            return JsonConvert.SerializeObject(this);
        }

        public ZeroPlus.Models.Data.Update.DynamicEdgeConfigModel GetConfig()
        {
            return new ZeroPlus.Models.Data.Update.DynamicEdgeConfigModel()
            {
                Id = Id,
                Title = Title,
                Creator = Creator,
                LastUpdateTime = LastUpdateTime,
                EmaRangeEnabled = EmaRangeEnabled,
                PercentBidRangeEnabled = PercentBidRangeEnabled,
                BaseEdgeEnabled = BaseEdgeEnabled,
                TradePxRangeEnabled = TradePxRangeEnabled,
                MinMarketWidthEnabled = MinMarketWidthEnabled,
                TheoRangeEnabled = TheoRangeEnabled,
                VolaRangeEnabled = VolaRangeEnabled,
                VolaModel = VolaModel,
                DynamicVolaRangeEnabled = DynamicVolaRangeEnabled,
                DynamicVolaModel = DynamicVolaModel,
                DynamicLookupMode = DynamicLookupMode,
                UnderDivisor = UnderDivisor,
                DteTable = DteTable.Select(x => x.GetConfig()).ToList(),
                DynamicDteTable = DynamicDteTable.Select(x => x.GetConfig()).ToList(),
                DeltaTable = DeltaTable.Select(x => x.GetConfig()).ToList(),
            };
        }

        public void Save()
        {
        }

        public void Load()
        {
            try
            {
                _underMultiplierLookup.Clear();
                foreach (var map in UnderMultiplierTable)
                {
                    var symbols = map.Under.Split(",");
                    foreach (var symbol in symbols)
                    {
                        _underMultiplierLookup[symbol.Trim().ToUpper()] = map.Multiplier;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }
}
