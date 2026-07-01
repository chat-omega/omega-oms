using System;
using System.Collections.ObjectModel;
using ZeroPlus.Models.Data.Configs;

namespace ZeroPlus.Models.Data.EdgeScanner
{
    public class EdgeScanFeedTradeFilterModel
    {
        [Newtonsoft.Json.JsonIgnore]
        public ConfigSave? Details { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public int Id { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string? Title { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public string? Creator { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public DateTime LastUpdateTime { get; set; }
        [Newtonsoft.Json.JsonProperty]
        public ObservableCollection<EdgeScanFeedTradeFilterRowModel> Filters { get; set; }

        public EdgeScanFeedTradeFilterModel()
        {
            LastUpdateTime = DateTime.Now;
            Filters = new ObservableCollection<EdgeScanFeedTradeFilterRowModel>();
        }

        public void CloneFrom(EdgeScanFeedTradeFilterModel model)
        {
            LastUpdateTime = DateTime.Now;
            Filters = new ObservableCollection<EdgeScanFeedTradeFilterRowModel>();
            Title = model.Title;
            foreach (EdgeScanFeedTradeFilterRowModel filter in model.Filters)
            {
                EdgeScanFeedTradeFilterRowModel? newFilter = filter.Clone();
                if (newFilter != null)
                {
                    Filters.Add(newFilter);
                }
            }
            NormalizeAfterLoad();
        }

        public void Normalize()
        {
            for (var index = 0; index < Filters.Count; index++)
            {
                var filter = Filters[index];
                filter.Header = "Filter " + (index + 1);
                filter.UpdateMap();
                filter.SaveCopy();
            }
        }

        public void NormalizeAfterLoad()
        {
            for (var index = 0; index < Filters.Count; index++)
            {
                var filter = Filters[index];
                filter.Header = "Filter " + (index + 1);
                filter.UpdateMap();
                filter.LoadCopy();
            }
        }

        public string GetAsJson()
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(this);
        }
    }
}