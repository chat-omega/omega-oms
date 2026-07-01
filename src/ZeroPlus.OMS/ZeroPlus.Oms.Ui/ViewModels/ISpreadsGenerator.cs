using System.Collections.ObjectModel;
using ZeroPlus.Models.Generators.SpreadGenerators;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public interface ISpreadsGenerator
    {
        bool ShowProgressBar { get; set; }
        string ProgressStatus { get; set; }
        ObservableCollection<SpreadGeneratorResults> LatestSpreadGeneratorResults { get; set; }
    }
}