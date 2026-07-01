using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Update;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ChangeLogViewModel : ViewModelBase
    {
        public Dispatcher Dispatcher { get; set; }

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Version { get; set; }

        [Bindable]
        public partial List<string> Versions { get; set; }

        [Bindable]
        public partial List<ReleaseNoteModel> ReleaseNotes { get; set; }

        public ChangeLogViewModel()
        {
            ModuleTitle = "Change Log";
            Versions = new List<string>();
            ReleaseNotes = new List<ReleaseNoteModel>();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        internal void Load(Information updateInfo)
        {
            Version = updateInfo.Version.ToString();

            foreach (string line in updateInfo.ReleaseNotes
                .ReplaceLineEndings().Split(Environment.NewLine)
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                ReleaseNoteModel releaseNoteModel = new();
                releaseNoteModel.Update(line);
                ReleaseNotes.Add(releaseNoteModel);
            }
        }

        [Command]
        public void Close()
        {
            CurrentWindowService?.Close();
        }
    }
}
