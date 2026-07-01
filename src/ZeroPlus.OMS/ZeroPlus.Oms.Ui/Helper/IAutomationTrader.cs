using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    public interface IAutomationTrader
    {
        AutomationConfigModel GetAutomationConfig(string underlying = null, double increment = 0);
    }
}
