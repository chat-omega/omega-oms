using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Configs;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update
{
    public class DynamicEdgeConfigModel : IDynamicConfigModel
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Creator { get; set; } = string.Empty;

        public DateTime LastUpdateTime { get; set; }

        public bool PercentBidRangeEnabled { get; set; }

        public bool BaseEdgeEnabled { get; set; } = true;

        public bool EmaRangeEnabled { get; set; }

        public bool TradePxRangeEnabled { get; set; }

        public bool MinMarketWidthEnabled { get; set; }

        public bool MinMarketCrossEnabled { get; set; }

        public bool TheoRangeEnabled { get; set; }

        public bool VolaRangeEnabled { get; set; }

        public TheoModel VolaModel { get; set; }

        public bool DynamicVolaRangeEnabled { get; set; }

        public TheoModel DynamicVolaModel { get; set; }

        public bool DynamicLookupMode { get; set; }

        public double UnderDivisor { get; set; }

        public List<DaysToExpirationEdgeModel?> DteTable { get; set; } = new();

        public List<DaysToExpirationEdgeModel?> DynamicDteTable { get; set; } = new();

        public List<DeltaEdgeModel> DeltaTable { get; set; } = new();

        public ConfigSave? Details { get; set; }

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
                            double getWeightedVega,
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
                DaysToExpirationEdgeModel? dteModel = DteTable.Where(x => x != null && x.Active && x.DaysToExpiration >= daysToExpiration && minOfBidAskSize >= x.MinBidAskSize && minTick >= x.MinIncrement && Math.Abs(width) >= x.MinWidth).OrderBy(x => x?.DaysToExpiration ?? 0).ThenByDescending(x => x?.MinWidth ?? 0).FirstOrDefault();

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

                    DeltaEdgeModel? deltaAdditionModel = DeltaTable.Where(x => x.Active && Math.Abs(x.Delta) <= Math.Abs(delta)).OrderByDescending(x => Math.Abs(x.Delta)).FirstOrDefault();
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

                    double wVega = getWeightedVega;
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
                        double addition = deltaAdditionModel!.AddedEdge + contracts * deltaAdditionModel!.AdditionalEdgePerContract;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
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
                    maxPercentBid = PercentBidRangeEnabled ? dteModel.MaxPercentBid : double.NaN;
                    maxThroughTradePx = TradePxRangeEnabled ? dteModel.MaxThroughTradePx : double.NaN;
                    minMarketWidth = MinMarketWidthEnabled ? dteModel.MinMarketWidth : double.NaN;
                    minMarketCross = MinMarketCrossEnabled ? dteModel.MinMarketCross : double.NaN;
                    return true;
                }
            }
            else
            {
                DaysToExpirationEdgeModel? dteModel = DynamicDteTable.Where(x => x != null && x.Active && x.DaysToExpiration >= daysToExpiration && minOfBidAskSize >= x.MinBidAskSize && minTick >= x.MinIncrement && Math.Abs(width) >= x.MinWidth).OrderBy(x => x?.DaysToExpiration ?? 0).ThenByDescending(x => x?.MinWidth ?? 0).FirstOrDefault();

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

                    DeltaEdgeModel? deltaAdditionModel = DeltaTable.Where(x => x.Active && Math.Abs(x.Delta) <= Math.Abs(delta)).OrderByDescending(x => Math.Abs(x.Delta)).FirstOrDefault();
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

                    double wVega = getWeightedVega;
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
                        double addition = deltaAdditionModel!.AddedEdge + contracts * deltaAdditionModel!.AdditionalEdgePerContract;
                        if (!double.IsNaN(addition))
                        {
                            edge += addition;
                            loopMinEdge += addition;
                        }
                    }

                    edge = !fish || BaseEdgeEnabled ? Math.Round(edge, 3) : 0;
                    loopMinEdge = Math.Round(loopMinEdge, 3);

                    qty = strategy switch
                    {
                        BaseStrategy.CALL_VERTICAL or BaseStrategy.PUT_VERTICAL => Math.Max(dteModel.VerticalQty, qty),
                        _ => Math.Max(dteModel.Qty, qty),
                    };

                    permMinEdge = dteModel.DynamicAutoPermMinEdge + (underlying / UnderDivisor * dteModel.DynamicAutoPermMinEdgeAddition);
                    loopMaxLoss = dteModel.DynamicLoopMaxLoss + (underlying / UnderDivisor * dteModel.DynamicLoopMaxLossAddition);
                    return true;
                }
            }
        }

        public bool GetEdge(BaseStrategy strategy,
                            double strikeSpacing,
                            int daysToExpiration,
                            int contracts,
                            int minOfBidAskSize,
                            double delta,
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
                            out int qty,
                            out double permMinEdge,
                            out double closeEdge)
        {
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
            permMinEdge = double.NaN;
            closeEdge = double.NaN;
            qty = 1;
            if (daysToExpiration < 0 || contracts < 0 || contracts > 4)
            {
                return false;
            }
            else
            {
                DaysToExpirationEdgeModel? dteModel = DteTable.Where(x => x is { Active: true } && x.DaysToExpiration >= daysToExpiration && minOfBidAskSize >= x.MinBidAskSize).OrderBy(x => x?.DaysToExpiration).FirstOrDefault();

                if (dteModel == null)
                {
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
                                return false;
                            }
                            break;
                        case BaseStrategy.CALL_BUTTERFLY:
                        case BaseStrategy.PUT_BUTTERFLY:
                        case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                        case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                            if (strikeSpacing < dteModel.MinSpacingForFlys)
                            {
                                return false;
                            }
                            break;
                    }

                    DeltaEdgeModel? deltaAdditionModel = DeltaTable.Where(x => x.Active && Math.Abs(x.Delta) <= Math.Abs(delta)).OrderByDescending(x => Math.Abs(x.Delta)).FirstOrDefault();

                    edge = dteModel.BaseEdge;
                    if (contracts > 1)
                    {
                        edge += (contracts - 1) * dteModel.AdditionalEdgePerContract;
                    }

                    if (deltaAdditionModel != null)
                    {
                        edge += deltaAdditionModel.AddedEdge + contracts * deltaAdditionModel.AdditionalEdgePerContract;
                    }

                    switch (strategy)
                    {
                        case BaseStrategy.CALL_VERTICAL:
                        case BaseStrategy.PUT_VERTICAL:
                            qty = Math.Max(dteModel.VerticalQty, qty);
                            break;
                        default:
                            qty = Math.Max(dteModel.Qty, qty);
                            break;
                    }

                    loopMinEdge = dteModel.LoopMinEdge;
                    permMinEdge = dteModel.AutoPermMinEdge;
                    loopMaxLoss = dteModel.LoopMaxLoss;
                    closeEdge = dteModel.CloseEdge;
                    maxThroughTheo = TheoRangeEnabled ? dteModel.MaxAllowedAboveTheo : double.NaN;
                    maxThroughVola = VolaRangeEnabled ? dteModel.MaxAllowedAboveVola : double.NaN;
                    volaModel = VolaModel;
                    maxPercentBid = PercentBidRangeEnabled ? dteModel.MaxPercentBid : double.NaN;
                    maxThroughEma = EmaRangeEnabled ? dteModel.MaxAllowedAboveEma : double.NaN;
                    maxThroughTradePx = TradePxRangeEnabled ? dteModel.MaxThroughTradePx : double.NaN;
                    minMarketWidth = MinMarketWidthEnabled ? dteModel.MinMarketWidth : double.NaN;
                    return true;
                }
            }
        }

        public void Save()
        {
        }

        public void Load()
        {
        }
    }
}