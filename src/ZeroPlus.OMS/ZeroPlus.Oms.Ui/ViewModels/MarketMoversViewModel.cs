using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public class MarketMoversViewModel : ModuleViewModelBase
{
    public override Module Module { get; protected set; } = Module.MarketMovers;

    public PortfolioManagerModel PortfolioManager { get; }

    public MarketMoversViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, PortfolioManagerModel portfolioManager) : base(configBrowserViewModel, omsCore)
    {
        PortfolioManager = portfolioManager;
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        return default;
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        return Task.CompletedTask;
    }
}