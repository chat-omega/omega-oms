using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ShareWithViewModel : ViewModelBase
    {
        private static readonly string MODULE_TITLE = "Share With";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        protected IDispatcherService DispatcherService => GetService<IDispatcherService>();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Module Module { get; set; }
        public string Config { get; set; }
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        [Bindable]
        public partial string Message { get; set; }

        [Bindable]
        public partial ObservableCollection<UserSelectionModel> Users { get; set; }

        public HashSet<int> SelectedUsers { get; set; }
        public bool CanShare { get; set; }

        public ShareWithViewModel()
        {
            ModuleTitle = MODULE_TITLE;
            Users = new ObservableCollection<UserSelectionModel>();
            SelectedUsers = new HashSet<int>();
            CanShare = true;
        }

        protected override void OnInitializeInRuntime()
        {
            base.OnInitializeInRuntime();
            LoadUsers();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public void Share()
        {
            try
            {
                SelectedUsers = Users.Where(x => x.IsSelected).Select(x => x.Id).ToHashSet();
                if (SelectedUsers.Count > 0 && CanShare)
                {
                    ConfigShare configShare = new()
                    {
                        Sender = OmsCore.User.ID,
                        Username = OmsCore.User.Username,
                        Receivers = SelectedUsers.ToList(),
                        Module = (int)Module,
                        ConfigJson = Config,
                        Message = Message,
                        SendTime = DateTime.Now,
                    };

                    OmsCore.GatewayClient.ShareConfig(configShare);
                }
                CurrentWindowService?.Close();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Share));
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

        private void LoadUsers()
        {
            IsBusy = true;
            _ = OmsCore.GatewayClient.GetUsersAsync().ContinueWith(task =>
            {
                List<Comms.Models.Data.User> users = task.Result;
                if (users != null)
                {
                    DispatcherService?.BeginInvoke(() =>
                    {
                        foreach (Comms.Models.Data.User user in users.OrderBy(x => x.IsOnline).OrderBy(x => x.Username))
                        {
                            Users.Add(new UserSelectionModel()
                            {
                                Id = user.ID,
                                Username = user.Username,
                                IsOnline = user.IsOnline,
                                IsSelected = SelectedUsers.Contains(user.ID),
                            });
                        }
                        IsBusy = false;
                    });
                }
            });
        }
    }
}
