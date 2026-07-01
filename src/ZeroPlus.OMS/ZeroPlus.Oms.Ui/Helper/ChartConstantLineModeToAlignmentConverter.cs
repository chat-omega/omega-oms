using DevExpress.Xpf.Core;
using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(double), typeof(string))]
    public sealed class ChartConstantLineModeToAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is LineMode lineMode)
                {
                    return lineMode switch
                    {
                        LineMode.Primary => Alignment.Near.ToString(),
                        LineMode.Secondary => Alignment.Far.ToString(),
                        _ => Alignment.Near.ToString(),
                    };
                }
                return Alignment.Near.ToString();
            }
            catch (Exception)
            {
                return Alignment.Near.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
