using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Xpf;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void ConfigDeletedHandler(Module module, ConfigSave config);
    public delegate void LoadConfigHandler(ConfigSave configSave);

    public partial class ConfigBrowserViewModel : ViewModelBase
    {
        public event ConfigDeletedHandler ConfigDeletedEvent;

        private static readonly string MODULE_TITLE = "Config Browser";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        private readonly ObservableCollection<ConfigGroup> _allConfigs;
        private readonly ObservableCollection<ConfigGroup> _userConfigs;

        private string _module;
        private bool _refreshing;

        public List<string> Modules { get; } = Enum.GetNames(typeof(Module)).Select(GetModuleName).ToList();

        public Dispatcher Dispatcher { get; set; }
        public LoadConfigHandler LoadConfig { get; set; }
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();

        public string Module
        {
            get => _module;
            set
            {
                SetValue(ref _module, value);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    Refresh();
                }
            }
        }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial List<object> SelectedConfig { get; set; }
        [Bindable]
        public partial ObservableCollection<ConfigGroup> Configs { get; set; }
        [Bindable(Default = true)]
        public partial bool ShowAllConfigs { get; set; }

        public ConfigBrowserViewModel(MainViewModel mainViewModel)
        {
            ModuleTitle = MODULE_TITLE;
            LoadConfig = mainViewModel.LoadConfigLocal;
            SelectedConfig = new List<object>();
            _userConfigs = new ObservableCollection<ConfigGroup>();
            _allConfigs = new ObservableCollection<ConfigGroup>();
            ShowAllConfigs = true;
            ViewChanged();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
            Refresh();
        }

        [Command]
        public void ConfigRowDoubleClick(NodeClickArgs args)
        {
            if (args is { Item: ConfigSave configSave })
            {
                LoadConfig(configSave);
            }
        }

        [Command]
        public void ViewChanged()
        {
            Configs = ShowAllConfigs ? _allConfigs : _userConfigs;
        }

        [Command]
        public void Refresh()
        {
            try
            {
                if (_refreshing || string.IsNullOrWhiteSpace(Module))
                {
                    return;
                }
                _refreshing = true;

                Module module = GetModuleFromName(Module);
                switch (module)
                {
                    case Models.Module.Workspace:
                        HashSet<string> spaces = OmsCore.Config.GetWorkspaces();
                        ConfigGroup configGroup = new()
                        {
                            Title = "No Group",
                        };
                        configGroup.AddConfigs(spaces.Select(x => new ConfigSave() { Title = x, Username = OmsCore.User.Username, Module = (int)Models.Module.Workspace }).ToList());
                        Dispatcher?.BeginInvoke(() =>
                        {
                            _allConfigs.Clear();
                            _userConfigs.Clear();
                            _allConfigs.Add(configGroup);
                            _userConfigs.Add(configGroup);
                        });
                        break;
                    default:
                        Task<List<ConfigSave>> loadTask = OmsCore.GatewayClient.RequestConfigsAsync((int)module);

                        loadTask.ContinueWith(_ =>
                        {
                            List<ConfigSave> configs = loadTask.Result;
                            if (configs != null)
                            {
                                var groups = new List<ConfigGroup>();
                                var userGroup = new ConfigGroup()
                                {
                                    Title = OmsCore.User.Username,
                                    Username = OmsCore.User.Username,
                                };
                                userGroup.AddConfigs(configs.Where(x => x.OwnerId == OmsCore.User.ID).ToList());
                                IEnumerable<IGrouping<string, ConfigSave>> configGroups = configs.GroupBy(x => x.Group);
                                foreach (IGrouping<string, ConfigSave> config in configGroups)
                                {
                                    configGroup = new ConfigGroup()
                                    {
                                        Title = string.IsNullOrWhiteSpace(config.Key) ? "No Group" : config.Key,
                                        Username = string.Join(", ", config.Select(x => x.Username).Distinct()),
                                    };
                                    configGroup.AddConfigs(config.ToList());
                                    groups.Add(configGroup);
                                }

                                Dispatcher?.BeginInvoke(() =>
                                {
                                    _allConfigs.Clear();
                                    _userConfigs.Clear();
                                    _userConfigs.Add(userGroup);
                                    foreach (var group in groups)
                                    {
                                        _allConfigs.Add(group);
                                    }
                                });

                                _refreshing = false;
                            }
                            else
                            {
                                _refreshing = false;
                            }
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Refresh));
                _refreshing = false;
            }
        }

        [Command]
        public void Load()
        {
            if (SelectedConfig.Any())
            {
                var first = SelectedConfig.FirstOrDefault();
                if (first is ConfigSave configSave)
                {
                    LoadConfig(configSave);
                    CurrentWindowService?.Close();
                }
            }
        }

        [Command]
        public async void DeleteConfig(List<object> parameter)
        {
            if (parameter != null && parameter.Any())
            {
                string message = parameter.Count == 1 ?
                    $"Are you sure you want to delete {(parameter.FirstOrDefault() as ConfigSave)?.Title}" :
                    $"Are you sure you want to delete {parameter.Count} config?";

                bool result = GetVerification(message);

                if (result)
                {
                    string response = "";
                    foreach (ConfigSave configSave in parameter)
                    {
                        var configSaveModule = (Module)configSave.Module;
                        if (configSaveModule == Models.Module.Workspace)
                        {
                            response += "\n" + OmsCore.Config.DeleteConfig(configSave.Title);
                        }
                        else
                        {
                            response += "\n" + await OmsCore.GatewayClient.DeleteConfigAsync(configSave.Id);
                        }
                        ConfigDeletedEvent?.Invoke(configSaveModule, configSave);
                    }
                    ShowMessage(response.TrimStart());
                    Refresh();
                }
            }
        }

        private bool GetVerification(string message)
        {
            bool result;
            try
            {
                result = MessageBoxService.ShowMessage(message,
                    $"ZeroPlus OMS",
                    MessageButton.YesNo,
                    MessageIcon.Question) == MessageResult.Yes;
            }
            catch (Exception)
            {
                result = MessageBox.Show(message,
                    $"ZeroPlus OMS",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) == MessageBoxResult.Yes;
            }

            return result;
        }

        private void ShowMessage(string response)
        {
            try
            {
                MessageBoxService.ShowMessage(response,
                    $"ZeroPlus OMS",
                    MessageButton.OK,
                    MessageIcon.Information);
            }
            catch (Exception)
            {
                MessageBox.Show(response,
                    $"ZeroPlus OMS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        [Command]
        public void EditConfig(object parameter)
        {
            if (parameter is not null and ConfigSave configSave)
            {
                LoadConfig(configSave);
                CurrentWindowService?.Close();
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

        public void SetModule(Module module)
        {
            Module = GetModuleName(module.ToString());
        }

        public static string GetModuleName(string x)
        {
            return Regex.Replace(x, "(\\B[A-Z])", " $1");
        }

        private static Module GetModuleFromName(string name)
        {
            return (Module)Enum.Parse(typeof(Module), name.Replace(" ", ""));
        }

        public async Task<T> DeserializeConfigAsync<T>(ConfigSave configSave, CancellationToken token = default)
        {
            T config = default(T);
            try
            {
                if (string.IsNullOrWhiteSpace(configSave.ConfigJson))
                    throw new SlimException("Config was invalid");

                config = await Task.Run(() => JsonConvert.DeserializeObject<T>(configSave.ConfigJson), token);
                return config;
            }
            catch (Exception ex)
            {
                _log.Error(ex);
                throw;
            }
        }
    }
}
