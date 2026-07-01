using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class ConfigChainModel : BindableBase
    {

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public SpreadsGeneratorViewModel ParentViewModel { get; private set; }

        [Bindable]
        public partial ObservableCollection<ConfigSave> Configs { get; set; }

        [Bindable]
        public partial ConfigSave Config { get; set; }

        public ConfigChainModel(SpreadsGeneratorViewModel spreadsGeneratorViewModel, List<ConfigSave> configs)
        {
            ParentViewModel = spreadsGeneratorViewModel;
            Configs = new ObservableCollection<ConfigSave>();
            if (configs != null)
            {
                foreach (ConfigSave config in configs.OrderBy(x => x.Username).ToList())
                {
                    ParentViewModel.Dispatcher?.Invoke(new Action(() => Configs.Add(config)));
                }
            }
        }
    }
}
