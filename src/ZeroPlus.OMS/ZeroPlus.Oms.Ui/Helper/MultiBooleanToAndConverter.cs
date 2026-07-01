using System;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class MultiBooleanToAndConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool visible = true;

            foreach (bool value in values)
            {
                if (!value)
                {
                    visible = false;
                    break;
                }
            }

            return visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
