namespace ZeroPlus.Oms.Ui.ViewModels;

public readonly struct RiskCheckResult(bool isValid, double newPrice)
{
    public readonly bool IsValid = isValid;
    public readonly double NewPrice = newPrice;
}