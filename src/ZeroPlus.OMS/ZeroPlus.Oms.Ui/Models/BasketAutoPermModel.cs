using DevExpress.Mvvm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Configs;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketAutoPermModel : BindableBase, IDynamicConfigModel
    {

        [JsonProperty]
        [Bindable]
        public partial List<object> AutoPermOtherInstances { get; set; }
        [JsonProperty]
        [Bindable]
        public partial AutoPermSelectionMode AutoPermSelectionMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string Title { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SubmitAutoPerms { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool SubmitExistingItemsOnly { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool ShowAutoPermOthers { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool WaitForPrevious { get; set; }
        [JsonProperty]
        [Bindable]
        public partial bool AutoPermOthers { get; set; }
        [JsonProperty]
        [Bindable]
        public partial PermMatchingMode PermMatchingMode { get; set; }
        [JsonProperty]
        [Bindable]
        public partial List<AutoPermConfigModel> AutoPermConfigs { get; set; }
        [JsonProperty]
        [Bindable]
        public partial int Id { get; set; }
        [JsonProperty]
        [Bindable]
        public partial string Creator { get; set; }
        [JsonProperty]
        [Bindable]
        public partial DateTime LastUpdateTime { get; set; }
        [JsonProperty]
        [Bindable]
        public partial ConfigSave Details { get; set; }

        public BasketAutoPermModel()
        {
            AutoPermOtherInstances = new List<object>();
            AutoPermConfigs = new List<AutoPermConfigModel>();
        }

        public void Save()
        {
        }

        public void Load()
        {
        }

        internal string GetAsJson()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
