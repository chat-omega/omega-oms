using DevExpress.Mvvm.UI;
using System.Windows;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Services;

public class VerificationService : ServiceBase, IVerificationService
{
    public RiskWarningMessageResponse GetRiskVerification(string message, string title, bool showCancelAll = false)
    {
        RiskWarningMessageResponse result = RiskWarningMessageResponse.Cancel;
        RiskWarningMessageView view = new()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = AssociatedObject as Window
        };
        if (view.DataContext is RiskWarningMessageViewModel viewModel)
        {
            viewModel.Title = title;
            viewModel.Message = message;
            viewModel.ShowCancelAll = showCancelAll;
            view.ShowDialog();
            result = viewModel.MessageResult;
        }
        return result;
    }

    public void ShowMessage(string message, string title, bool showCancelAll = false)
    {
        RiskWarningMessageView view = new()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = AssociatedObject as Window
        };
        if (view.DataContext is RiskWarningMessageViewModel viewModel)
        {
            viewModel.Title = title;
            viewModel.Message = message;
            viewModel.ShowCancelAll = showCancelAll;
            view.Show();
        }
    }
}