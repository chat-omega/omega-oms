using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ComboTraderView.xaml
    /// </summary>
    public partial class ComboTraderView : ThemedWindow
    {
        private const string MODULE_NAME = "Combo Trader";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private bool _layoutRestored;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }
        public Dictionary<string, ColumnConfigModel> GridFieldNameToConfigMap { get; set; }
        public ComboTraderViewModel ViewModel { get; }
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public ComboTraderView() : this(Guid.NewGuid().ToString())
        {
        }

        public ComboTraderView(string uid)
        {
            Uid = uid;
            InitializeComponent();
            Name = nameof(ComboTraderView);
            OmsCore.SaveWorkspaceRequestEvent += OnSaveLayoutRequest;
            Closing += View_Closing;
            Loaded += RestoreLayout;
            Module = Module.ComboTraderLayout;
            ConfigSave = new ConfigSave()
            {
                Title = MODULE_NAME,
                Module = (int)Module,
                Username = OmsCore.User.Username,
                Group = OmsCore.User.Username,
                OwnerId = OmsCore.User.ID,
            };
            GridFieldNameToConfigMap = new();
            ViewModel = DataContext as ComboTraderViewModel;
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            ComboTraderViewModel viewModel = (ComboTraderViewModel)DataContext;
            bool cancel = viewModel.Dispose();
            e.Cancel = cancel;
            if (!cancel)
            {
                OmsCore.SaveWorkspaceRequestEvent -= OnSaveLayoutRequest;
            }
        }

        public void OnSaveLayoutRequest()
        {
            SaveLayout(false, true, true);
        }

        private async void RestoreLayout(object sender, RoutedEventArgs e)
        {
            await RestoreLayoutAsync();
        }

        public async Task RestoreLayoutAsync()
        {
            Loaded -= RestoreLayout;
            StartupWindowViewModel.MainWindow.WindowHelper.AddWindow(this);
            ComboTraderViewModel dataContext = (ComboTraderViewModel)DataContext;
            dataContext.Uid = Uid;
            dataContext.Name = "Combo Trader " + Uid.Split('-').LastOrDefault();
            string path = $"{Uid}-{Module}-layout.json";
            string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
            string instanceExportPath = Path.Combine(layoutDir, path);
            string defaultExportPath = Path.Combine(layoutDir, $"Default-{Module}-layout.json");

            if (!_layoutRestored)
            {
                _layoutRestored = true;
                if (!string.IsNullOrWhiteSpace(instanceExportPath) && File.Exists(instanceExportPath))
                {
                    string export = File.ReadAllText(instanceExportPath);
                    ConfigSave configSave = await Task.Run(() => JsonConvert.DeserializeObject<ConfigSave>(export));
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(defaultExportPath) && File.Exists(defaultExportPath))
                    {
                        string export = File.ReadAllText(defaultExportPath);
                        ConfigSave configSave = await Task.Run(() => JsonConvert.DeserializeObject<ConfigSave>(export));
                    }
                    else
                    {
                    }
                }
            }
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }

        private void RemoveButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is SimpleButton baseEdit)
            {
                baseEdit.Opacity = 1;
            }
        }

        private void RemoveButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is SimpleButton baseEdit)
            {
                baseEdit.Opacity = 0.5;
            }
        }

        public void SaveLayout(bool saveDefault, bool saveLocation = false, bool withItems = false)
        {
        }
    }
}
