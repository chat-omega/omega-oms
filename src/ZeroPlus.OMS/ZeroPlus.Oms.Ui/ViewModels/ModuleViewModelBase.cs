using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Extensions;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;
using Module = ZeroPlus.Oms.Ui.Models.Module;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void ReadyEventHandler(IModuleViewModel module);

    public abstract partial class ModuleViewModelBase : CustomizableTableViewModelBase, IModuleViewModel
    {
        public event ReadyEventHandler Ready;

        protected static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private string _moduleTitle;

        public bool IsReady { get; private set; }
        public string Uid { get; set; }
        public abstract Module Module { get; protected set; }
        public bool IsDisposed { get; set; }
        public Dispatcher Dispatcher { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public ConfigBrowserViewModel ConfigBrowserViewModel { get; set; }
        public OmsCore OmsCore { get; }
        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        
        public string ModuleTitle
        {
            get => _moduleTitle ?? Module.ToString().FromCamelCase();
            set => SetValue(ref _moduleTitle, ModuleWindow.CleanTitle(value));
        }
        [Bindable(Default = true)]
        public partial bool AllowSave { get; set; }
        public ObservableCollection<ConfigSave> AdminConfigs { get; } = [];
        [Bindable]
        public partial ConfigSave SelectedConfig { get; set; }

        protected ModuleViewModelBase(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore)
        {
            OmsCore = omsCore;
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;
            ConfigBrowserViewModel = configBrowserViewModel;
        }

        public abstract string GetConfigSerialized(bool withContent = false, bool layoutOnly = false);
        public abstract Task DeserializeAndLoadConfig(string configJson, bool withContent = true);

        public virtual void SaveViewModelConfig() { }

        [Command]
        public void BrowseConfigs()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;
                void loader(object sender, RoutedEventArgs args)
                {
                    windowView.Loaded -= loader;
                    viewModel?.SetModule(Module);
                }
                windowView.Loaded += loader;
                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseConfigs));
            }
        }

        [Command]
        public void BrowseLayouts()
        {
            try
            {
                ConfigBrowserWindowView windowView = new();
                ConfigBrowserViewModel viewModel = windowView.DataContext as ConfigBrowserViewModel;
                void loader(object sender, RoutedEventArgs args)
                {
                    windowView.Loaded -= loader;
                    viewModel?.SetModule(Module);
                }
                windowView.Loaded += loader;
                windowView.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(BrowseLayouts));
            }
        }

        [Command]
        public void SaveConfigOnServer()
        {
            try
            {
                SaveView view = new();

                if (view.DataContext is not SaveViewModel viewModel)
                {
                    return;
                }

                viewModel.LoadGroups(Module);
                viewModel.ShowDefault = false;
                viewModel.Config = GetConfigSerialized(true);

                if (ConfigSave != null)
                {
                    viewModel.Id = ConfigSave.Id;
                    viewModel.Title = ConfigSave.Title;
                    viewModel.SelectedGroup = ConfigSave.Group;
                }

                view.ShowDialog();

                if (!string.IsNullOrWhiteSpace(viewModel.Title) && viewModel.Success)
                {
                    ModuleTitle = viewModel.Title;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveConfigOnServer));
            }
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            ConfigBrowserViewModel.SetDispatcher(dispatcher);
            OnSetDispatcher();
        }

        public virtual void OnSetDispatcher() { }

        internal async Task LoadConfigFromJsonAsync(string configJson = "", bool withContent = true)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(configJson))
                {
                    await InvokeReady();
                    return;
                }

                await OnReadyAsync();
                await DeserializeAndLoadConfig(configJson, withContent);
                await InvokeReady();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadConfigFromJsonAsync));
            }
        }

        [Command]
        public void ShareLayout()
        {
            try
            {
                ShareWithView view = new();

                if (view.DataContext is not ShareWithViewModel viewModel)
                {
                    return;
                }

                viewModel.Module = Module;
                viewModel.Config = GetConfigSerialized(true);
                view.ShowDialog();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ShareLayout));
            }
        }

        public void Dispose()
        {
            try
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                    OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
                    OnDispose();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(Dispose)}, {Module}");
            }
        }

        public virtual void OnDispose() { }

        internal async Task InvokeReady()
        {
            await OnReadyAsync().ContinueWith(_ =>
            {
                Ready?.Invoke(this);
                IsReady = true;
            });
        }

        protected virtual Task OnReadyAsync()
        {
            return Task.CompletedTask;
        }
    }
}
