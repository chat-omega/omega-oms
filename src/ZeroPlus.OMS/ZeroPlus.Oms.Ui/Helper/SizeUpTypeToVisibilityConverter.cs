using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ZeroPlus.Oms.Enums;
namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(LoopSizeupType), typeof(Visibility))]
    [ValueConversion(typeof(LoopIncrementType), typeof(Visibility))]
    [ValueConversion(typeof(LoopCloseEdgeType), typeof(Visibility))]
    [ValueConversion(typeof(LoopIntervalType), typeof(Visibility))]
    public sealed class SizeUpTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LoopSizeupType sizeupType && parameter is string lType)
            {
                return sizeupType.ToString() == lType ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (value is LoopIncrementType loopIncrementType && parameter is string paramenterString)
            {
                return loopIncrementType.ToString() == paramenterString ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (value is LoopCloseEdgeType loopCloseEdgeType && parameter is string loopCloseEdgeTypeParamenterString)
            {
                return loopCloseEdgeType.ToString() == loopCloseEdgeTypeParamenterString ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (value is LoopIntervalType loopIntervalType && parameter is string loopIntervalTypeParamenterString)
            {
                return loopIntervalType.ToString() == loopIntervalTypeParamenterString ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
