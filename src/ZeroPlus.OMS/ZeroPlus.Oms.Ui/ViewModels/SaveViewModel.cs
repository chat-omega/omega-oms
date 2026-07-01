using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class SaveViewModel : ViewModelBase
    {

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        public IDispatcherService DispatcherService => GetService<IDispatcherService>();


        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public int Id { get; set; } = int.MinValue;
        public Module Module { get; set; }
        public string Config { get; set; }
        public List<ConfigSave> Configs { get; private set; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial ObservableCollection<string> Titles { get; set; }

        [Bindable]
        public partial ObservableCollection<string> Groups { get; set; }

        [Bindable]
        public partial string SelectedGroup { get; set; }

        [Bindable]
        public partial string Title { get; set; }

        [Bindable]
        public partial bool SetAsDefault { get; set; }

        [Bindable]
        public partial bool AddToFavorites { get; set; }

        [Bindable]
        public partial bool SaveLocation { get; set; }

        [Bindable]
        public partial bool ShowGroup { get; set; }

        [Bindable]
        public partial bool ShowDefault { get; set; }

        [Bindable]
        public partial bool ShowLocation { get; set; }

        public bool Success { get; set; }
        public bool Workspace { get; set; }

        public SaveViewModel()
        {
            Titles = new ObservableCollection<string>();
            Groups = new ObservableCollection<string>();
            Configs = new List<ConfigSave>();
            ShowGroup = true;
            ShowDefault = true;
            ShowLocation = false;
            SelectedGroup = OmsCore.User.Username;
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void LoadGroups(Module module)
        {
            Module = module;
            OmsCore.GatewayClient.RequestConfigsAsync((int)module).ContinueWith(loadTask =>
            {
                Configs = loadTask.Result;
                if (Configs != null)
                {
                    List<string> groups = Configs.Select(x => x.Group)
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .Distinct()
                                        .OrderBy(x => x)
                                        .ToList();

                    List<string> titles = Configs.Where(x => x.Group == SelectedGroup)
                                        .Select(x => x.Title)
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .Distinct()
                                        .OrderBy(x => x)
                                        .ToList();

                    DispatcherService?.BeginInvoke(() =>
                    {
                        foreach (string group in groups)
                        {
                            Groups.Add(group);
                        }

                        foreach (string title in titles)
                        {
                            Titles.Add(title);
                        }
                    });
                }
            });
        }

        [Command]
        public void LoadTitles()
        {
            OmsCore.GatewayClient.RequestConfigsAsync((int)Module).ContinueWith(loadTask =>
            {
                Configs = loadTask.Result;
                if (Configs != null)
                {
                    List<string> titles = Configs.Where(x => x.Group == SelectedGroup)
                                        .Select(x => x.Title)
                                        .Where(x => !string.IsNullOrEmpty(x))
                                        .Distinct()
                                        .OrderBy(x => x)
                                        .ToList();
                    DispatcherService?.BeginInvoke(() =>
                    {
                        Titles.Clear();
                        foreach (string title in titles)
                        {
                            Titles.Add(title);
                        }
                    });
                }
            });
        }

        [Command]
        public async void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(Title))
                {
                    MessageBoxService.ShowMessage($"{nameof(Title)} can not be empty.",
                                                  $"ZeroPlus OMS",
                                                  MessageButton.OK,
                                                  MessageIcon.Warning);
                    return;
                }
                if (ShowGroup)
                {
                    List<ConfigSave> config = await OmsCore.GatewayClient.RequestConfigsAsync((int)Module);
                    ConfigSave sameConfig = config?.FirstOrDefault(x => x.Title == Title && x.Group == SelectedGroup);
                    if (sameConfig != null)
                    {
                        if (sameConfig.OwnerId != OmsCore.User.ID)
                        {
                            MessageBoxService.ShowMessage($"{Title} already exists in {SelectedGroup} group.",
                                                         $"ZeroPlus OMS",
                                                         MessageButton.OK,
                                                         MessageIcon.Error);
                            return;
                        }
                        else
                        {
                            MessageResult response = MessageBoxService.ShowMessage($"{Title} already exists in {SelectedGroup} group.\n" +
                                                                         $"Do you want to replace it?",
                                                                         $"ZeroPlus OMS",
                                                                         MessageButton.YesNo,
                                                                         MessageIcon.Warning);

                            if (response != MessageResult.Yes)
                            {
                                return;
                            }
                            Id = sameConfig.Id;
                        }
                    }

                    // Rename or create new
                    ConfigSave sameConfigId = config?.FirstOrDefault(x => Id == x.Id);
                    if (sameConfigId != null)
                    {
                        MessageResult response = MessageBoxService.ShowMessage($"Do you want to update the existing config or create a new one?.\n" +
                                                                         $"Select Yes to create a new config. Select no to update the existing config.",
                                                                         $"ZeroPlus OMS",
                                                                         MessageButton.YesNo,
                                                                         MessageIcon.Warning);

                        if (response == MessageResult.Yes)
                        {
                            Id = int.MinValue;
                        }
                    }

                    ConfigSave configSave = new()
                    {
                        Id = Id,
                        OwnerId = OmsCore.User.ID,
                        Username = OmsCore.User.Username,
                        Module = (int)Module,
                        ConfigJson = Config,
                        Group = SelectedGroup,
                        SaveTime = DateTime.Now,
                        Title = Title,
                    };

                    OmsCore.GatewayClient.SaveConfig(configSave);

                    MessageBoxService.ShowMessage($"{Title} config saved.",
                                                  $"ZeroPlus OMS",
                                                  MessageButton.OK,
                                                  MessageIcon.Information);
                }
                else if (Workspace)
                {
                    if (Title == OmsConfig.DEFAULT_WORKSPACE)
                    {
                        MessageBoxService.ShowMessage($"Can not override default workspace.",
                                                      $"ZeroPlus OMS",
                                                      MessageButton.OK,
                                                      MessageIcon.Error);
                        Success = false;
                        return;
                    }
                    if (OmsCore.Config.GetWorkspaces().Contains(Title))
                    {
                        MessageResult response = MessageBoxService.ShowMessage($"{Title} already exists.\n" +
                                                                     $"Do you want to override it?",
                                                                     $"ZeroPlus OMS",
                                                                     MessageButton.YesNo,
                                                                     MessageIcon.Warning);

                        if (response != MessageResult.Yes)
                        {
                            Success = false;
                            return;
                        }
                    }
                    Dispatcher.Invoke(() => OmsCore.Config.WorkspaceTitle = Title);
                }
                Success = true;
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Save));
            }
        }

        [Command]
        public void Cancel()
        {
            Success = false;
            CurrentWindowService?.Close();
        }
    }
}
