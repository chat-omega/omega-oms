using System;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MultiBooleanToAndVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool visible = true;

            foreach (object value in values)
            {
                if (value is bool val && !val)
                {
                    visible = false;
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
