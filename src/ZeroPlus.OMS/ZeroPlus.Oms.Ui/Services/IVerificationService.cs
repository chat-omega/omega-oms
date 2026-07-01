using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Services;

public interface IVerificationService
{
    RiskWarningMessageResponse GetRiskVerification(string message, string title, bool showCancelAll = false);
    void ShowMessage(string message, string title, bool showCancelAll = false);
}