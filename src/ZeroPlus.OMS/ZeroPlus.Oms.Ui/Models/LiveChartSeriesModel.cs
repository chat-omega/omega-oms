using DevExpress.Mvvm;
using DevExpress.Mvvm.Native;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ZeroPlus.Models.Data.Update;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class LiveChartSeriesModel : BindableBase
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
        public partial ObservableCollection<LiveChartValueModel> ChartValues { get; set; }

        public LiveChartSeriesModel(string title, YAxisViewModel yAxes)
        {
            Title = title;
            ChartYAxes = yAxes;
            ChartPoints = new ObservableCollection<ChartValueModel>();
            ChartValues = new ObservableCollection<LiveChartValueModel>();
        }

        public LiveChartSeriesModel(IChartModule chartModule)
        {
            ChartModule = chartModule;
            ChartPoints = new ObservableCollection<ChartValueModel>();
            ChartValues = new ObservableCollection<LiveChartValueModel>();
        }

        internal void Initialize(List<Option> options, ChartField selectedChartField)
        {
            List<LiveChartValueModel> models = new();
            foreach (Option option in options)
            {
                LiveChartValueModel model = new()
                {
                    Dispatcher = ChartModule.Dispatcher,
                };
                model.Initialize(option, selectedChartField);
                models.Add(model);
            }
            ChartModule.Dispatcher?.BeginInvoke(() => ChartValues = models.ToObservableCollection());
        }
    }
}
