using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(Enum), typeof(Visibility))]
    public sealed class EnumToVisibilityConverter : IValueConverter
    {
        public bool Inverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility visibility;
            if (parameter is string enumName && value is Enum someEnum)
            {
                visibility = someEnum.ToString() == enumName ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                visibility = Visibility.Collapsed;
            }

            return !Inverse ? visibility : visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
