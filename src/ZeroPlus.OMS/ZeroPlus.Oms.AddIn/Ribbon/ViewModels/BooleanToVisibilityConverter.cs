using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZeroPlus.Oms.AddIn.Ribbon.ViewModels
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        private bool hiddenInsteadOfCollapsed;

        public bool Inverse { get; set; }

        [Obsolete("Use the HiddenInsteadOfCollapsed property instead.")]
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool HiddenInsteadCollapsed
        {
            get => hiddenInsteadOfCollapsed;
            set => hiddenInsteadOfCollapsed = value;
        }

        public bool HiddenInsteadOfCollapsed
        {
            get => hiddenInsteadOfCollapsed;
            set => hiddenInsteadOfCollapsed = value;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool booleanValue = GetBooleanValue(value);
            return BooleanToVisibility(booleanValue ^ Inverse, HiddenInsteadOfCollapsed);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = (value is Visibility && (Visibility)value == Visibility.Visible) ^ Inverse;

            return flag;
        }

        public static bool GetBooleanValue(object value)
        {
            if (value is bool)
            {
                return (bool)value;
            }

            if (value is bool?)
            {
                bool? flag = (bool?)value;
                if (!flag.HasValue)
                {
                    return false;
                }

                return flag.Value;
            }

            return false;
        }

        private object BooleanToVisibility(bool booleanValue, bool hiddenInsteadOfCollapsed)
        {
            if (!booleanValue)
            {
                if (!hiddenInsteadOfCollapsed)
                {
                    return Visibility.Collapsed;
                }

                return Visibility.Hidden;
            }

            return Visibility.Visible;
        }
    }
}
