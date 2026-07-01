using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class NagbotIntervalModel : BindableBase
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
        public partial bool StopOnFailure { get; set; }

        [JsonProperty]
        [Bindable]
        public partial bool Repeat { get; set; }

        [JsonProperty]
        [Bindable]
        public partial ObservableCollection<NagbotIntervalConfigModel> Configs { get; set; }

        public NagbotIntervalModel()
        {
            Configs = new ObservableCollection<NagbotIntervalConfigModel>();
        }

        internal void CloneFrom(NagbotIntervalModel model)
        {
            LastUpdateTime = DateTime.Now;
            Title = model.Title;
            StopOnFailure = model.StopOnFailure;
            Repeat = model.Repeat;
            foreach (NagbotIntervalConfigModel item in model.Configs.OrderBy(x => x.Interval))
            {
                NagbotIntervalConfigModel config = new(item);
                Configs.Add(config);
            }
        }

        internal string GetAsJson()
        {
            Configs = Configs.OrderBy(x => x.Interval).ToObservableCollection();
            return JsonConvert.SerializeObject(this);
        }
    }
}
