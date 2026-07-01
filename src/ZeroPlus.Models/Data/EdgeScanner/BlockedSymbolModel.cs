using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Models.Data.Configs;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class BlockedSymbolModel
    {
        [Newtonsoft.Json.JsonIgnore]
        public ConfigSave? Details { get; set; }
        [Newtonsoft.Json.JsonIgnore]
        public HashSet<string?>? SymbolsSet { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int Id { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string? Title { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string? Creator { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public DateTime LastUpdateTime { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public ObservableCollection<BlockedSymbolModelItem> Symbols { get; set; }

        public BlockedSymbolModel()
        {
            Symbols = new ObservableCollection<BlockedSymbolModelItem>();
            SymbolsSet = new HashSet<string?>();
        }

        public void UpdateSet()
        {
            SymbolsSet = Symbols?.Select(x => x.Symbol)?.Where(x => !string.IsNullOrWhiteSpace(x))?.ToHashSet();
        }

        public void CloneFrom(BlockedSymbolModel model)
        {
            LastUpdateTime = DateTime.Now;
            Symbols = new(model.Symbols);
            Title = model.Title;
            Details = null;
            UpdateSet();
        }

        public string GetAsJson()
        {
            Symbols = new(Symbols.Where(x => !string.IsNullOrWhiteSpace(x.Symbol)).OrderBy(x => x.Symbol).DistinctBy(x => x.Symbol));
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }
}
