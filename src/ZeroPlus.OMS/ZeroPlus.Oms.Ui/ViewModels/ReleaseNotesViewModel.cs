using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Oms.Update;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ReleaseNotesViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private Information _updateInfo;

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Dispatcher Dispatcher { get; set; }

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Version { get; set; }

        [Bindable]
        public partial string ReleaseNotes { get; set; }

        public ReleaseNotesViewModel()
        {
            ModuleTitle = "Update Manager";
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        internal void Load(Information updateInfo)
        {
            try
            {
                _updateInfo = updateInfo;
                Version = "V-" + updateInfo.Version.ToString();
                ReleaseNotes = updateInfo.ReleaseNotes;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Load));
            }
        }

        [Command]
        public void RestartNow()
        {
            MessageResult? result = MessageResult.No;

            if (OmsCore.User != null)
            {
                Application.Current.Dispatcher.Invoke(new Action(() => result = MessageBoxService?.ShowMessage($"Do you want to save your workspace before quitting?", "ZeroPlus OMS", MessageButton.YesNoCancel, MessageIcon.Question, MessageResult.No)));
            }

            switch (result)
            {
                case MessageResult.Cancel:
                    return;
                case MessageResult.Yes:
                    OmsCore.RequestSaveWorkspace();
                    break;
            }

            string[] args = StartupWindowViewModel.GetStartUpArgs().Split(" ");
            _updateInfo.ApplyUpdate(args);
        }

        [Command]
        public void RestartLater()
        {
            CurrentWindowService?.Close();
        }
    }
}
