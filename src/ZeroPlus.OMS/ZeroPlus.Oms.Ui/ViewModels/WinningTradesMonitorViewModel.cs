using DevExpress.Mvvm.DataAnnotations;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class WinningTradesMonitorViewModel : ModuleViewModelBase
{
    private readonly IModuleFactory _moduleFactory;
    public override Module Module { get; protected set; } = Module.WinningTradesMonitor;

    [Bindable]
    public partial TransactionConsumerModel TransactionConsumer { get; set; }

    [Bindable]
    public partial WinningTradeModel LastWinningTrade { get; set; }

    [Bindable]
    public partial bool AutoScroll { get; set; }

    public WinningTradesMonitorViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumer, IModuleFactory moduleFactory) : base(configBrowserViewModel, omsCore)
    {
        _moduleFactory = moduleFactory;
        TransactionConsumer = transactionConsumer;
        TransactionConsumer.WinningTradeAdded += OnWinningTradeAdded;
    }

    [Command]
    public void OpenInOrderTicketCommand(WinningTradeModel model)
    {
        var ticket = _moduleFactory.CreateModule(Module.ComplexOrderTicket);
        if (ticket.ViewModel is ComplexOrderTicketViewModel orderTicketViewModel)
        {
            orderTicketViewModel.LoadLegsFromTosAsync(model.Symbol, model.LastTradeSide, true);
        }
    }

    [Command]
    public void LoadInBasketTraderCommand(WinningTradeModel model)
    {
        var view = new LoadInBasketTraderPromptView();
        if (view.DataContext is LoadInBasketTraderPromptViewModel viewModel)
        {
            viewModel.LoadModelAsync(model);
            view.Show();
        }
    }

    private void OnWinningTradeAdded(WinningTradeModel model)
    {
        if (AutoScroll)
        {
            LastWinningTrade = model;
        }
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