using DevExpress.Drawing;
using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(bool), typeof(DXImage))]
    public sealed class MuteToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue
                ? WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(Assembly.GetExecutingAssembly(), "Images/Mute.svg"))
                : WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(Assembly.GetExecutingAssembly(), "Images/Sound.svg"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
