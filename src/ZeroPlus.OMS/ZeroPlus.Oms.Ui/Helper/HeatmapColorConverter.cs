using System;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(Brush))]
    public class HeatmapColorConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is not double heatPercent)
            {
                return Brushes.Transparent;
            }

            if (double.IsNaN(heatPercent) || double.IsInfinity(heatPercent) ||
                heatPercent < 0 || heatPercent > 100)
            {
                return Brushes.Transparent;
            }

            if (heatPercent < 10)
            {
                return SpreadHeatmapViewModel.Brush10;
            }
            else if (heatPercent is > 10 and < 20)
            {
                return SpreadHeatmapViewModel.Brush20;
            }
            else if (heatPercent is > 20 and < 30)
            {
                return SpreadHeatmapViewModel.Brush30;
            }
            else if (heatPercent is > 30 and < 40)
            {
                return SpreadHeatmapViewModel.Brush40;
            }
            else if (heatPercent is > 40 and < 50)
            {
                return SpreadHeatmapViewModel.Brush50;
            }
            else if (heatPercent is > 50 and < 60)
            {
                return SpreadHeatmapViewModel.Brush60;
            }
            else if (heatPercent is > 60 and < 70)
            {
                return SpreadHeatmapViewModel.Brush70;
            }
            else if (heatPercent is > 70 and < 80)
            {
                return SpreadHeatmapViewModel.Brush80;
            }
            else if (heatPercent is > 80 and < 90)
            {
                return SpreadHeatmapViewModel.Brush90;
            }
            else
            {
                return SpreadHeatmapViewModel.Brush100;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            return value;
        }
    }
}
