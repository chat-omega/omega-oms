using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using DevExpress.Mvvm.UI.Native;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Exceptions;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class NagbotIntervalManagementViewModel : CustomizableTableViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        BasketTraderViewModel _parentBasket;
        public BasketTraderViewModel ParentBasket
        {
            get => _parentBasket; 
            internal set => _parentBasket = value; 
        }
        public FastObservableCollection<NagbotIntervalModel> Models { get; }

        [Bindable]
        public partial NagbotIntervalModel CurrentModel { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        public NagbotIntervalManagementViewModel()
        {
            Models = new FastObservableCollection<NagbotIntervalModel>();
            RefreshCommand();
        }

        [Command]
        public async void RefreshCommand()
        {
            try
            {
                IsBusy = true;
                Models.Clear();
                List<Comms.Models.Data.Oms.Config.ConfigSave> configs = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module.NagbotIntervalConfigs);
                if (configs != null)
                {
                    List<NagbotIntervalModel> models = new();
                    foreach (Comms.Models.Data.Oms.Config.ConfigSave config in configs)
                    {
                        Comms.Models.Data.Oms.Config.ConfigSave details = await Task.Run(() => OmsCore.GatewayClient.RequestConfigDataAsync(config.Id));
                        if (details != null)
                        {
                            NagbotIntervalModel model = JsonConvert.DeserializeObject<NagbotIntervalModel>(details.ConfigJson);
                            model.Id = config.Id;
                            model.Details = config;
                            model.Configs = model.Configs.OrderBy(x => x.Interval).ToObservableCollection();
                            models.Add(model);
                        }
                    }

                    if (models.Count > 0)
                    {
                        Models.AddRange(models);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(RefreshCommand));
            }
            finally
            {
                IsBusy = false;
            }
        }

        [Command]
        public void AddCommand()
        {
            try
            {
                NagbotIntervalView view = new();
                if (view.DataContext is NagbotIntervalViewModel viewModel)
                {
                    viewModel.Model = new NagbotIntervalModel()
                    {
                        Creator = OmsCore.User.Username,
                        LastUpdateTime = DateTime.Now,
                    };
                }
                view.ShowDialog();
                RefreshCommand();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddCommand));
            }
        }

        [Command]
        public void LoadCommand()
        {
            try
            {
                if (ParentBasket.GetAutomationConfig() == null)
                {
                    _log.Error(nameof(LoadCommand) + "Basket Automation Config not loaded.");
                    MessageBoxService.ShowMessage("Select Automation Config First.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                }
                else
                {
                    ParentBasket.BasketSettings.NagbotIntervalModelConfigId = CurrentModel != null ? CurrentModel.Id : 0;
                    ParentBasket.BasketSettings.NagbotIntervalModel = CurrentModel;
                    CurrentWindowService?.Close();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCommand));
            }
        }

        [Command]
        public void EditCommand(NagbotIntervalModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    NagbotIntervalView view = new();
                    if (view.DataContext is NagbotIntervalViewModel viewModel)
                    {
                        viewModel.Model = selectedModel;
                    }
                    view.ShowDialog();
                    RefreshCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(EditCommand));
            }
        }

        [Command]
        public void CloneCommand(NagbotIntervalModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    NagbotIntervalModel model = new()
                    {
                        Creator = OmsCore.User.Username,
                    };
                    model.CloneFrom(selectedModel);
                    model.Title += " Clone";
                    EditCommand(model);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(CloneCommand));
            }
        }

        [Command]
        public void DeleteCommand(NagbotIntervalModel selectedModel)
        {
            try
            {
                if (selectedModel != null)
                {
                    if (selectedModel.Details != null && selectedModel.Details.OwnerId != OmsCore.User.ID)
                    {
                        MessageBoxService.ShowMessage("You do not have permission to delete this config!", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Warning);
                    }
                    else
                    {
                        MessageResult response = MessageBoxService.ShowMessage("Are you sure you want to delete " + selectedModel.Title + "?", "ZeroPlus OMS", MessageButton.YesNo, MessageIcon.Warning);
                        if (response == MessageResult.Yes)
                        {
                            if (selectedModel.Details != null && selectedModel.Details.OwnerId == OmsCore.User.ID)
                            {
                                _ = OmsCore.GatewayClient.DeleteConfigAsync(selectedModel.Details.Id);
                            }
                        }
                    }

                    RefreshCommand();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(DeleteCommand));
            }
        }
    }
}
