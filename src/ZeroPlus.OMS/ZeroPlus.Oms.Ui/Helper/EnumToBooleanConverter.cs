using System;
using System.Globalization;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(Enum), typeof(bool))]
    public sealed class EnumToBooleanConverter : IValueConverter
    {
        public bool Inverse { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();

            bool result = checkValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
            return Inverse ? !result : result;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked)
            {
                return parameter;
            }
            return Binding.DoNothing;
        }
    }
}
