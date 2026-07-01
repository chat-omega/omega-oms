using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class DynamicSizeupModel : BindableBase
    {

        [JsonIgnore]
        public ConfigSave Details { get; internal set; }

        [JsonProperty]
        public int Id { get; internal set; }

        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }

        [JsonProperty]
        [Bindable]
        public partial string Creator { get; set; }

        [JsonProperty]
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<SizeupConfigModel> SizeUpConfigs { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool MatchFilledSizeAfterPartialFill { get; set; }

        public DynamicSizeupModel()
        {
            SizeUpConfigs = new ObservableCollection<SizeupConfigModel>();
        }

        internal void CloneFrom(DynamicSizeupModel model)
        {
            LastUpdateTime = DateTime.Now;
            Title = model.Title;
            foreach (SizeupConfigModel item in model.SizeUpConfigs.OrderByDescending(x => x.Edge).ThenByDescending(x => x.Size))
            {
                SizeupConfigModel config = new(item);
                SizeUpConfigs.Add(config);
            }
        }

        internal string GetAsJson()
        {
            SizeUpConfigs = SizeUpConfigs.OrderByDescending(x => x.Edge).ThenByDescending(x => x.Size).ToObservableCollection();
            return JsonConvert.SerializeObject(this);
        }

        public ZeroPlus.Models.Data.Update.DynamicSizeUpConfigModel GetConfig()
        {
            ZeroPlus.Models.Data.Update.DynamicSizeUpConfigModel config = new()
            {
                Id = Id,
                Title = Title,
                Creator = Creator,
                LastUpdateTime = LastUpdateTime,
                SizeUpConfigs = SizeUpConfigs.Select(x => x.GetConfig()).ToList()
            };
            return config;
        }
    }
}
