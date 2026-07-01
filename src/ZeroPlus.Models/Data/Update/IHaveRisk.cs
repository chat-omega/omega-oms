namespace ZeroPlus.Models.Data.Update;

public interface IHaveRisk
{
    uint UserId { get; }
    uint RiskCheckId { get; set; }

    bool RiskCheckPassed { get; set; }
    string? RiskCheckMessage { get; set; }
}