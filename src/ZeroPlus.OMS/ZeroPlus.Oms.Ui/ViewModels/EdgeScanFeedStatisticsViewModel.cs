using DevExpress.Mvvm.DataAnnotations;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public class EdgeScanFeedStatisticsViewModel : ModuleViewModelBase
{
    private TransactionConsumerModel _transactionConsumer;
    private readonly OmsCore _omsCore;
    public override Module Module { get; protected set; } = Module.EdgeScanFeedStatistics;

    public TransactionConsumerModel TransactionConsumer
    {
        get => _transactionConsumer;
        set => SetProperty(ref _transactionConsumer, value, "TransactionConsumer");
    }
    public EdgeScanFeedStatisticsViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore, TransactionConsumerModel transactionConsumer) : base(configBrowserViewModel, omsCore)
    {
        _omsCore = omsCore;
        TransactionConsumer = transactionConsumer;
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        return default;
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        return Task.CompletedTask;
    }

    [Command]
    public void RefreshChartCommand()
    {
        Dispatcher?.BeginInvoke(() => TransactionConsumer.EdgeScanFeedStatChartValues.Clear());
        _omsCore.EdgeScannerClient.ScannerClient.RequestData(SubscriptionFieldType.EdgeScanFeedSubmissionStats);
    }
}