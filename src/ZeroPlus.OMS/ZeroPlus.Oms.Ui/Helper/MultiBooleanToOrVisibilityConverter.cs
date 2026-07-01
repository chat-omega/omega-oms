using System;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MultiBooleanToOrVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool visible = false;

            foreach (object value in values)
            {
                if (value is bool val && val)
                {
                    visible = true;
                    break;
                }
            }

            return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
