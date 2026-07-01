using System;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MultiBooleanToVisibilityInverseConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {

            bool visible = false;

            if (parameter != null && parameter is string converterParameter && converterParameter == "and")
            {
                visible = true;
                foreach (object value in values)
                {
                    if (value is bool)
                    {
                        visible = visible && (bool)value;
                    }
                }
            }
            else
            {

                foreach (object value in values)
                {
                    if (value is bool)
                    {
                        visible = visible || (bool)value;
                    }
                }
            }

            return !visible ? System.Windows.Visibility.Visible : (object)System.Windows.Visibility.Hidden;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
