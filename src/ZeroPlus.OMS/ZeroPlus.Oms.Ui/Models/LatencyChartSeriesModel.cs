using DevExpress.Mvvm;
using System.Collections.ObjectModel;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LatencyChartSeriesModel : BindableBase
    {
        public IChartModule ChartModule { get; set; }
        public YAxisViewModel ChartYAxes { get; private set; }
        public ObservableCollection<ChartValueModel> ChartPoints { get; set; }

        private string _Title;
        public string Title
        {
            get => _Title;
            set => SetValue(ref _Title, value?.ToUpper());
        }

        [Bindable]
        public partial ObservableCollection<LatencyChartValueModel> ChartValues { get; set; }
    }
}
