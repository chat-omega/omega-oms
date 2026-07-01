using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace ZeroPlus.Models.Data.Models;

public class DeltaAdjustedUnderlyingModel
{
    public string? Symbol { get; set; }
    public bool DiscardUpdatesEnabled { get; set; }
    public double UpdateThreshold { get; set; } = 0.10;
    public double UpdateThresholdFloorPercentage { get; set; } = .2;
    public double AcceptedChange { get; set; } = 0.05;
    public double AcceptedChangeFloorPercentage { get; set; } = .2;
    public double Multiplier { get; set; } = 1;
    public int MaxConsecutiveDiscards { get; set; } = 0;
    public bool UseHanweckUnderlying { get; set; } = true;

    public bool UseGamma { get; set; } = true;
    public bool DeltaAdjustVola { get; set; } = true;
    public bool UseInputMidForDeltaAdjusting { get; set; } = true;

    [JsonIgnore]
    [IgnoreDataMember]
    public bool UseFastData { get; set; }
    [JsonIgnore]
    [IgnoreDataMember]
    public int OptionsCount { get; set; }
    [JsonIgnore]
    [IgnoreDataMember]
    public double OffsetMargin { get; set; } = 500;

    [JsonIgnore]
    [IgnoreDataMember]
    public double UpdateDeltaThreshold { get; set; } = 0.05;
}