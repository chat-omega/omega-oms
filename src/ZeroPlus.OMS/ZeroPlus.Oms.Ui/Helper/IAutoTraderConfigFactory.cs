using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    public interface IAutoTraderConfigFactory
    {
        AutoTraderConfig CreateFromSettings(OmsAutoTraderSettings settings);
        AutoTraderConfig CreateFromTraderWithAutomation(IAutoTraderSettings automationTrader, AutomationConfigModel automationConfig, EdgeType edgeType, double edgeValue);
    }
}
