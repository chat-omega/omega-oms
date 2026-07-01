using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Models.Data.Update;

public class AutomationConfig
{

    private Dictionary<string, string>? _exchToRouteMap;
    public ConfigKey? ConfigKey { get; set; }

    public bool LoopingEnabled { get; set; }

    public string OpenRoute { get; set; } = string.Empty;
    public string CloseRoute { get; set; } = string.Empty;
    public string OpenRouteSingleLeg { get; set; } = string.Empty;
    public string CloseRouteSingleLeg { get; set; } = string.Empty;

    public string OpenRouteSize { get; set; } = string.Empty;
    public string CloseRouteSize { get; set; } = string.Empty;
    public string OpenRouteSingleLegSize { get; set; } = string.Empty;
    public string CloseRouteSingleLegSize { get; set; } = string.Empty;

    public SelectionType CloseEdgeType { get; set; }
    public double StaticCloseEdge { get; set; }
    public double StaticMinLoopEdge { get; set; }
    public double StaticMaxLoss { get; set; }
    public DynamicEdgeConfigModel? DynamicCloseEdge { get; set; }

    // Route
    public bool LooperDynamicRouting { get; set; }
    public bool AttemptIncrementUsingDynamicRoute { get; set; }
    public bool EnableDynamicRouteForOpeningOrders { get; set; }
    public bool EnableDynamicRouteForClosingOrders { get; set; }
    public List<Tuple<string, string>>? ExchToRouteList { get; set; }
    [JsonIgnore]
    public Dictionary<string, string>? ExchToRouteMap => _exchToRouteMap ??= ExchToRouteList?.ToDictionary(x => x.Item1, x => x.Item2);

    public SelectionType CloseIntervalType { get; set; }
    public double StaticCloseInterval { get; set; }
    public double StaticCloseIntervalMax { get; set; }
    public double StaticLoopInterval { get; set; }
    public double StaticLoopIntervalMax { get; set; }
    public DynamicIntervalConfigModel? DynamicCloseInterval { get; set; }

    public SelectionType IncrementType { get; set; }
    public double StaticIncrement { get; set; }
    public List<DynamicIncrementModel>? DynamicIncrement { get; set; }

    public SelectionType SizeUpType { get; set; }
    public int StaticSizeUpLoopCountBeforeSizeup { get; set; }
    public int StaticSizeUp { get; set; }
    public DynamicSizeUpConfigModel? DynamicSizeUp { get; set; }

    public bool AutoAggressorEnabled { get; set; }
    public AutoAggressorMode AutoAggressorMode { get; set; }
    public AutoAggressorEdgeTightenMode AutoAggressorEdgeTightenMode { get; set; }
    public double AutoAggressorEdgeTightenPercentage { get; set; }

    public bool ScratchOnLowDeltaSize { get; set; }
    public double ScratchOnLowDeltaMax { get; set; }
    public double ScratchOnLowDeltaMaxLoss { get; set; }
    public int ScratchOnLowDeltaMinSize { get; set; }

    public bool FreeLookRequireMinFillTime { get; set; }
    public double FreeLookMinFillTime { get; set; }

    public bool FreeLookOnLosers { get; set; }
    public int FreeLookOnLosersMax { get; set; }

    public bool FreeLookOnAll { get; set; }
    public bool FreeWhenGettingCloseEdge { get; set; }
    public bool FreeLookAfterLastAttempt { get; set; }
    public double FreeLookBackUpIncrement { get; set; }
    public double FreeLookOnAllWalkBackIncrement { get; set; }

    public bool LoopFreeLookOnAllUsingTicks { get; set; }
    public double FreeLookOnAllIncrementTicks { get; set; } = 1;
    public double FreeLookOnAllWalkBackIncrementTicks { get; set; } = 1;

    public bool LoopFreeLookOnNickelNames { get; set; }
    public double LoopFreeLookOnNickelNamesIncrement { get; set; }
    public string? LoopFreeLookOnNickelNamesRoute { get; set; }

    public bool LoopFreeLookOnDimeNames { get; set; }
    public double LoopFreeLookOnDimeNamesIncrement { get; set; }
    public string? LoopFreeLookOnDimeNamesRoute { get; set; }

    public bool MaintainLastEdge { get; set; }

    public int AttemptResubmitCount { get; set; }
    public int LastFillResubmitCount { get; set; }

    public int MaxNumberOfLoops { get; set; }

    public double PartialFillPercentage { get; set; }
    public int PartialFillResubmit { get; set; }

    public LoopPricingMode LoopPricingMode { get; set; }
    public bool AdjustClosingPriceToMarketWinnersOnly { get; set; }

    public PxCrossOption PxCrossOption { get; set; }
    public PxCrossOption ClosePxCrossOption { get; set; }

    // Hedge house settings
    public bool AutoHedgeOnClose { get; set; }
    public bool AutoHedgeOnCloseSizeOnly { get; set; }
    public double MinHedgeHouseEdge { get; set; } = .10;
    public bool AutoHedgeOnFailure { get; set; }
    public bool AutoHedgePartial { get; set; }

    public bool AutoLegEnabled { get; set; }
    public double AutoLegMaxWidth { get; set; }
    public double AutoLegCloseEdge { get; set; }
    public double AutoLegMaxLoss { get; set; }
    public double AutoLegCloseIncrement { get; set; }
    public string? AutoLegCloseRoute { get; set; }
    public double AutoLegRestTime { get; set; }
}