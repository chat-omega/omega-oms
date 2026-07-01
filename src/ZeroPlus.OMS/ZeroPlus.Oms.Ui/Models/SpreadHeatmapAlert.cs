using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Models;

public class SpreadHeatmapAlert
{
    public bool AlertEnabled { get; set; }
    public bool AudioEnabled { get; set; }
    public string AudioSound { get; set; }
    public double Threshold { get; set; }
    public bool VisualEnabled { get; set; }
    public bool NotificationEnabled { get; set; }
    public HashSet<int> ShareWithUsers { get; set; } = new();
}