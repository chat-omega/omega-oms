using DevExpress.Mvvm;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Trading;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class ExecutionTransactionsViewModel : ModuleViewModelBase
{

    public override Module Module { get; protected set; } = Module.ExecutionTransaction;
    public ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();

    [Bindable]
    public partial bool AutoScroll { get; set; }
    [Bindable]
    public partial Transaction LastTransaction { get; set; }
    [Bindable]
    public partial ObservableCollection<Transaction> Transactions { get; set; }

    public ExecutionTransactionsViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, ExecutionTransactionsContainer executionTransactionsContainer) : base(configBrowserViewModel, omsCore)
    {
        Transactions = executionTransactionsContainer.ExecutionTransactions;
        LastTransaction = Transactions.LastOrDefault();
    }

    public override void OnDispose()
    {
        base.OnDispose();
        Transactions = null;
        LastTransaction = null;
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