namespace ZeroPlus.Models.Data.Models;

public class GlobalRiskModel
{
    public bool CheckForLongCredit { get; set; } = true;
    public bool CheckForVerticalSpacing { get; set; } = true;
    public bool LimitByMaxSubmissionsPerSecond { get; set; } = true;
    public int MaxSubmissionsPerSecond { get; set; } = 100;
}