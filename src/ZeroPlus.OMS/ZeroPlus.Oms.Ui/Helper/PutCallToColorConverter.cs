using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(PutCall), typeof(string))]
    public sealed class PutCallToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not PutCall)
                {
                    return "";
                }
                PutCall putCall = (PutCall)value;

                return putCall switch
                {
                    PutCall.Unknown => "",
                    PutCall.Put => "#e0211f",
                    PutCall.Call => "#1c7fff",
                    _ => "",
                };
            }
            catch (Exception)
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
