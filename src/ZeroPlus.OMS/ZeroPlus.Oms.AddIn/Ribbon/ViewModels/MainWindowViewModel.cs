using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using ZeroPlus.Comms.Models.Data;

namespace ZeroPlus.Oms.AddIn.Ribbon.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private const string APP_CODE = "OMS ADDIN";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public bool _CloseTrigger;
        public bool _AuthConnected;
        public bool _IsBusy;
        public string _BusyMessage;
        public string _Message;
        public string _VersionString;
        public string _UpdateStatus;
        public string _Username;
        public SecureString _SecurePassword;
        public bool _SaveUser;
        public User _SelectedUser;


        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public bool CloseTrigger
        {
            get => _CloseTrigger;
            set => SetValue(ref _CloseTrigger, value);
        }

        public bool AuthConnected
        {
            get => _AuthConnected;
            set => SetValue(ref _AuthConnected, value);
        }

        public bool IsBusy
        {
            get => _IsBusy;
            set => SetValue(ref _IsBusy, value);
        }

        public string BusyMessage
        {
            get => _BusyMessage;
            set => SetValue(ref _BusyMessage, value);
        }

        public string Message
        {
            get => _Message;
            set => SetValue(ref _Message, value);
        }

        public string VersionString
        {
            get => _VersionString;
            set => SetValue(ref _VersionString, value);
        }

        public string UpdateStatus
        {
            get => _UpdateStatus;
            set => SetValue(ref _UpdateStatus, value);
        }

        public string Username
        {
            get => _Username;
            set => SetValue(ref _Username, value);
        }

        public SecureString SecurePassword
        {
            get => _SecurePassword;
            set => SetValue(ref _SecurePassword, value);
        }

        public bool SaveUser
        {
            get => _SaveUser;
            set => SetValue(ref _SaveUser, value);
        }

        public User SelectedUser
        {
            get => _SelectedUser;
            set => SetValue(ref _SelectedUser, value);
        }

        public MainWindowViewModel()
        {
            SaveUser = true;
        }

        [Command]
        public async void Login()
        {
            if (!AuthConnected)
            {
                SetMessage("Could not connect to auth server.");
                _log?.Info("Could not connect to auth server.");
                return;
            }

            if (string.IsNullOrEmpty(Username))
            {
                SetMessage("Username required!");
                return;
            }

            if (SecurePassword == null || SecurePassword.Length == 0)
            {
                SetMessage("Password required!");
                return;
            }

            IsBusy = true;
            BusyMessage = "Authenticating";
            User user = await OmsCore.GatewayClient.AuthenticateAsync(Username, SecurePassword, APP_CODE);
            if (user != null)
            {
                BusyMessage = "Initializing";
                SelectedUser = user;
                LoadUser();
            }
            else
            {
                IsBusy = false;
                SetMessage("Authentication failed.");
            }
        }

        protected override void OnInitializeInRuntime()
        {
            try
            {
                base.OnInitializeInRuntime();
                OmsCore.GatewayClient.ConnectionStatusChangedEvent += AuthClient_ConnectionStatusChangedEvent;
                AuthClient_ConnectionStatusChangedEvent(OmsCore.GatewayClient.IsConnected);
                VersionString = OmsCore.AppUpdateManager.GetCurrentVersion();
                string selectedUser = OmsCore.GetSavedUser();
                if (selectedUser != null)
                {
                    Username = selectedUser;
                }
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show($"Critical Exception Report.\n{ex.Message}\nPlease restart program.",
                                    "ZeroPlus OMS",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                Environment.Exit(-1);
            }
        }

        private void SetMessage(string message)
        {
            Message = message;
            Task.Delay(7000).ContinueWith(t => Message = "");
        }

        private void AuthClient_ConnectionStatusChangedEvent(bool connected)
        {
            AuthConnected = connected;
        }

        private void LoadUser()
        {
            if (SelectedUser != null)
            {
                OmsCore.GatewayClient.ConnectionStatusChangedEvent -= AuthClient_ConnectionStatusChangedEvent;
                OmsCore.User = SelectedUser;
                if (SaveUser)
                {
                    _ = Task.Run(() => UpdateSelectedUserPing());
                }
                if (OmsCore.Config.DominatorClientEnabled)
                {
                    _ = OmsCore.DominatorClient?.StartAsync();
                }
                CloseTrigger = true;
            }
        }

        private async Task UpdateSelectedUserPing()
        {
            await Task.Run(() =>
            {
                OmsCore.SaveUser();
            });
        }
    }
}
