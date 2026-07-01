using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Oms.Data;
using ZeroPlus.Oms.Enums;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class LoadInBasketTraderPromptViewModel : ViewModelBase
{
    private readonly OmsCore _omsCore;
    private readonly IModuleFactory _moduleFactory;

    protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
    protected IDispatcherService DispatcherService => GetService<IDispatcherService>();
    public IEnumerable<Side> Sides { get; } = new List<Side> { ZeroPlus.Models.Data.Enums.Side.Buy, ZeroPlus.Models.Data.Enums.Side.Sell };

    [Bindable(Default = "Loading Configs...")]
    public partial string IsBusyMessage { get; set; }
    [Bindable]
    public partial bool IsBusy { get; set; }
    [Bindable]
    public partial WinningTradeModel Model { get; set; }
    [Bindable]
    public partial Side? OpeningSide { get; set; }
    [Bindable]
    public partial string ModuleTitle { get; set; }
    [Bindable]
    public partial int PermCount { get; set; }
    [Bindable]
    public partial double TargetEdge { get; set; }
    [Bindable]
    public partial double BackupEdge { get; set; }
    [Bindable]
    public partial List<ConfigSave> BasketConfigs { get; set; }
    [Bindable]
    public partial ConfigSave BasketConfig { get; set; }

    public LoadInBasketTraderPromptViewModel(OmsCore omsCore, IModuleFactory moduleFactory)
    {
        _omsCore = omsCore;
        _moduleFactory = moduleFactory;
    }

    [Command]
    public async Task LoadCommand()
    {
        IsBusyMessage = "Opening Basket...";
        IsBusy = true;
        await Task.Run(() =>
        {
            if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView
                {
                    ViewModel: BasketTraderViewModel viewModel
                } view)
            {
                if (viewModel.IsReady)
                {
                    Task.Run(() => OnReady(viewModel));
                }
                else
                {
                    viewModel.Ready += OnReady;
                }

                async void OnReady(IModuleViewModel _)
                {
                    viewModel.Ready -= OnReady;
                    if (BasketConfig != null)
                    {
                        var configSave = await _omsCore.GatewayClient.RequestConfigDataAsync(BasketConfig.Id);
                        view.RestoreFromConfigSave(configSave);
                    }

                    viewModel.ResetEdgeTypes();
                    viewModel.BasketSettings.UseLastFillAdjPx = true;
                    viewModel.BasketSettings.LastFillAdjEdge = BackupEdge;

                    var basketItem = await viewModel.LoadFromSymbol(Model.Symbol, OpeningSide);
                    switch (((IOrder)basketItem).Side)
                    {
                        case ZeroPlus.Models.Data.Enums.Side.Buy:
                            basketItem.AveragePrice = Model.BuyPrice;
                            basketItem.LastMainUnderMidAtFill = Model.BuyUnderPrice;
                            break;
                        case ZeroPlus.Models.Data.Enums.Side.Sell:
                            basketItem.AveragePrice = Model.SellPrice;
                            basketItem.LastMainUnderMidAtFill = Model.SellUnderPrice;
                            break;
                    }
                    basketItem.SetEdgeAsync();

                    if (PermCount > 0)
                    {
                        AutoPermConfigModel autoPermConfig = new AutoPermConfigModel()
                        {
                            Enabled = true,
                            TargetEdge = TargetEdge,
                            AttemptBothSides = false,
                            AutoPermTemplate = new PermOperationModel()
                            {
                                Perms = new List<PermOperationMode>()
                                {
                                    new()
                                    {
                                        Count = PermCount,
                                        MaintainBaseStrategy = true,
                                        PermMode = PermMode.StrikeUp,
                                        PermSide = PermSide.Alternate,
                                    },
                                    new()
                                    {
                                        Count = PermCount,
                                        MaintainBaseStrategy = true,
                                        PermMode = PermMode.StrikeDown,
                                        PermSide = PermSide.Alternate,
                                    }
                                }
                            }
                        };
                        viewModel.LoadAutoPerms(basketItem, Model.HighestEdge, autoPermConfig, sendOrders: false, false, false, activate: true);
                    }
                }
            }
        });
        DispatcherService?.BeginInvoke(() =>
        {
            IsBusy = false;
            CurrentWindowService?.Close();
        });
    }

    public async void LoadModelAsync(WinningTradeModel model)
    {
        IsBusy = true;
        Model = model;
        OpeningSide = model.HardSide;
        ModuleTitle = model.SpreadId;
        BasketConfigs = (await _omsCore.GatewayClient.RequestConfigsAsync((int)Module.BasketTraderLayout))?.Where(x => x.OwnerId == _omsCore.User.ID).ToList();
        IsBusy = false;
    }
}