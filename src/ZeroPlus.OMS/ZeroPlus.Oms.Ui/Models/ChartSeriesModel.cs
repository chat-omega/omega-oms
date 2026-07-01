using System.Collections.Generic;
using System.Collections.ObjectModel;
using ZeroPlus.Models.Data.Update;

namespace ZeroPlus.Oms.Ui.Models;

public class ChartSeriesModel
{
    public string Title { get; set; }

    public ObservableCollection<ChartValueModel> ChartValues { get; set; }

    public ChartSeriesModel(string title, List<ChartValueModel> values)
    {
        Title = title;
        ChartValues = new ObservableCollection<ChartValueModel>(values);
    }
}