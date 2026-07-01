using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using NLog;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class LoadSetupViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();

        public ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();

        [Bindable]
        public partial List<DominatorSetupGroupModel> SetupGroups { get; set; }

        [Bindable]
        public partial DominatorSetupGroupModel SelectedSetupGroup { get; set; }

        public List<DominatorModel> SelectedDominators { get; }

        public LoadSetupViewModel(List<DominatorModel> selected)
        {
            SelectedDominators = selected;

            SetupGroups = new List<DominatorSetupGroupModel>
            {
                new("Dominator", selected.SelectMany(x => x.DominatorSetups).Distinct().ToList()),
                new("Full Auto", selected.SelectMany(x => x.FullAutoSetups).Distinct().ToList()),
            };

            foreach (KeyValuePair<string, List<string>> type in selected.SelectMany(x => x.CustomSetups).Distinct())
            {
                SetupGroups.Add(new DominatorSetupGroupModel(type.Key, type.Value.ToList()));
            }
        }

        [Command]
        public void Load()
        {
            if (SelectedSetupGroup != null &&
                SelectedSetupGroup.SelectedSetup != null &&
                SelectedSetupGroup.SelectedSetup is string setup)
            {
                foreach (DominatorModel dominator in SelectedDominators)
                {
                    switch (SelectedSetupGroup.Title)
                    {
                        case "Dominator":
                            dominator.LoadCustomSetup("DominatorSetup", setup);
                            break;
                        case "Full Auto":
                            dominator.LoadCustomSetup("FullAutoSetup", setup);
                            break;
                        default:
                            dominator.LoadCustomSetup(SelectedSetupGroup.Title, setup);
                            break;
                    }
                }
                CurrentWindowService?.Close();
            }
        }

        [Command]
        public void Cancel()
        {
            CurrentWindowService?.Close();
        }

    }
}
