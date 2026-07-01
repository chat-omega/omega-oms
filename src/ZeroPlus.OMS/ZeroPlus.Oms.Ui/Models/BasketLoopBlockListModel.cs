using DevExpress.Mvvm;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketLoopBlockListModel : BindableBase
    {

        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }

        [JsonIgnore]
        [Bindable]
        public partial ObservableCollection<SymbolModel> BlockedSymbols { get; set; }
        [JsonProperty]
        public HashSet<string> Items { get; set; }

        public BasketLoopBlockListModel()
        {
            Title = "Block List";
            Items = new HashSet<string>();
            BlockedSymbols = new ObservableCollection<SymbolModel>();
        }

        internal void UpdateList()
        {
            Items.Clear();
            foreach (SymbolModel item in BlockedSymbols.ToList())
            {
                if (!string.IsNullOrWhiteSpace(item.Symbol))
                {
                    Items.Add(item.Symbol);
                }
                else
                {
                    BlockedSymbols.Remove(item);
                }
            }
        }
    }
}
