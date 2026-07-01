using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class ThresholdToBrushConverter : IMultiValueConverter
    {
        private static readonly Brush DefaultThresholdBrush = CreateBrush("#FF4F4F");
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        public Brush ThresholdBrush { get; set; }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return TransparentBrush;

            if (values.Length >= 3 && values[2] is bool enabled && !enabled)
                return TransparentBrush;

            if (double.TryParse(values[0].ToString(), out double cellValue) &&
                double.TryParse(values[1].ToString(), out double threshold) &&
                !double.IsNaN(cellValue) &&
                cellValue < threshold)
            {
                return ThresholdBrush ?? DefaultThresholdBrush;
            }

            return TransparentBrush;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static Brush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
            brush.Freeze();
            return brush;
        }
    }
}
