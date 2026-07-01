using DevExpress.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class ConfigGroup : BindableBase
    {

        [Bindable]
        public partial string Title { get; set; }


        [Bindable]
        public partial string Username { get; set; }


        [Bindable]
        public partial ObservableCollection<ConfigSave> Configs { get; set; }

        public ConfigGroup()
        {
            Configs = new ObservableCollection<ConfigSave>();
        }

        internal void AddConfigs(List<ConfigSave> configSaves)
        {
            foreach (ConfigSave config in configSaves)
            {
                Configs.Add(config);
            }
        }
    }
}
