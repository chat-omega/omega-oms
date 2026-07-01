using DevExpress.Xpf.Charts;
using System.Collections.ObjectModel;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class YAxisViewModel
    {
        public string Title { get; private set; }
        public ObservableCollection<ConstantLineViewModel> ConstantLines { get; private set; }
        public AxisAlignment Alignment { get; set; }

        public YAxisViewModel(string title, ObservableCollection<ConstantLineViewModel> constantLines, AxisAlignment alignment = AxisAlignment.Near)
        {
            Title = title;
            ConstantLines = constantLines;
            Alignment = alignment;
        }
    }

    public class ConstantLineViewModel
    {
        public string Title { get; private set; }
        public double Value { get; private set; }

        public ConstantLineViewModel(string title, double value)
        {
            Title = title;
            Value = value;
        }
    }
}