using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections.ObjectModel;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class BasketBlockListConfigViewModel : CustomizableTableViewModelBase
    {

        [Bindable]
        public partial ObservableCollection<BasketLoopBlockListModel> List { get; set; }

        [Bindable]
        public partial BasketLoopBlockListModel Model { get; set; }

        [Command]
        public void AddSymbolCommand()
        {
            Model?.BlockedSymbols.Add(new SymbolModel());
        }

        [Command]
        public void RemoveSymbolCommand(SymbolModel item)
        {
            Model?.BlockedSymbols.Remove(item);
        }

        [Command]
        public void SaveSymbolCommand()
        {
            Model?.UpdateList();
        }

        [Command]
        public void DeleteSymbolCommand()
        {
            if (Model != null)
            {
                List.Remove(Model);
                Model = null;
            }
        }
    }
}
