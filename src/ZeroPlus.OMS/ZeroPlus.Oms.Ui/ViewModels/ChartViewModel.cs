using DevExpress.Mvvm;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class ChartViewModel : ViewModelBase
    {
        private static readonly string MODULE_TITLE = "Chart";
        public Dispatcher Dispatcher { get; set; }
        private IDispatcherService DispatcherService => GetService<IDispatcherService>();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial string Field { get; set; }

        [Bindable]
        public partial ObservableCollection<ChartValueModel> ChartValues { get; set; }

        public ChartViewModel()
        {
            ModuleTitle = MODULE_TITLE;
            ChartValues = new ObservableCollection<ChartValueModel>();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        internal void LoadChart(List<ChartValueModel> values)
        {
            DispatcherService.BeginInvoke(() =>
            {
                foreach (ChartValueModel item in values)
                {
                    ChartValues.Add(item);
                }
            });
        }
    }
}
