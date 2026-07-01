using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(object), typeof(string))]
    public partial class CamelCaseFormatter : IValueConverter
    {
        [GeneratedRegex("(\\B[A-Z])")]
        public static partial Regex CamelCaseConverter();

        public bool AllCaps { get; set; }
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null)
                {
                    return value;
                }
                string camelCaseString = value.ToString();
                if (!string.IsNullOrWhiteSpace(camelCaseString))
                {
                    string output = CamelCaseConverter().Replace(camelCaseString, " $1");
                    return AllCaps ? output.ToUpper() : output;
                }
                else
                {
                    return value;
                }
            }
            catch (Exception)
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
