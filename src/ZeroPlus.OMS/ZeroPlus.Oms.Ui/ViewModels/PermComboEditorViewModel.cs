using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Mvvm.Native;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Oms.Data;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PermComboEditorViewModel : ViewModelBase
    {
        protected OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();


        [Bindable]
        public partial string Title { get; set; }

        [Bindable]
        public partial ObservableCollection<string> Titles { get; set; }

        [Bindable]
        public partial ObservableCollection<PermOperationMode> Combinations { get; set; }

        [Bindable]
        public partial PermOperationMode SelectedCombination { get; set; }

        public PermComboEditorViewModel()
        {
            Titles = OmsCore.Config.CustomPermCombinations?.Keys.ToObservableCollection();
            Combinations = new ObservableCollection<PermOperationMode>();
        }

        [Command]
        public void ConfigLoadedCommand()
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                if (OmsCore.Config.CustomPermCombinations.TryGetValue(Title, out List<PermOperationMode> permOperations))
                {
                    Combinations = permOperations.ToObservableCollection();
                }
            }
        }

        [Command]
        public void MoveRowDownCommand()
        {
            if (SelectedCombination != null)
            {
                int index = Combinations.IndexOf(SelectedCombination);
                if (index < Combinations.Count - 1)
                {
                    Combinations.Move(index, index + 1);
                }
            }
        }

        [Command]
        public void MoveRowUpCommand()
        {
            if (SelectedCombination != null)
            {
                int index = Combinations.IndexOf(SelectedCombination);
                if (index > 0)
                {
                    Combinations.Move(index, index - 1);
                }
            }
        }

        [Command]
        public void AddCommand()
        {
            PermOperationMode mode = new();
            Combinations.Add(mode);
        }

        [Command]
        public void RemoveCommand(PermOperationMode remove)
        {
            Combinations.Remove(remove);
        }

        [Command]
        public void SaveCommand()
        {
            if (!string.IsNullOrWhiteSpace(Title))
            {
                if (Combinations.Count > 0)
                {
                    OmsCore.Config.CustomPermCombinations[Title] = Combinations.ToList();
                }
                else
                {
                    OmsCore.Config.CustomPermCombinations.Remove(Title);
                }
            }
            OmsCore.Config.SaveCustomPermCombinations();
            Titles = OmsCore.Config.CustomPermCombinations?.Keys.ToObservableCollection();
        }
    }
}
