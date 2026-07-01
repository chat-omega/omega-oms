using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(ChartField), typeof(string))]
    public sealed class ChartFieldToDisplayTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not ChartField type)
            {
                return "";
            }

            return type switch
            {
                ChartField.Iv => "Iv",
                ChartField.Theo => "Theo",
                ChartField.AdjTheo => "Adj Theo",
                ChartField.Snapshot => "Snapshot",
                ChartField.BidAskIv => "Bid Ask IV",
                ChartField.RecalculatedBidAskFromIv => "Recalculated Price",
                _ => type.ToString(),
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
