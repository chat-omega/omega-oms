using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using DevExpress.Drawing;
using DevExpress.Utils;
using DevExpress.Xpf.Core.Native;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(bool), typeof(DXImage))]
    public sealed class DisableTicketToGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue
                ? WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(Assembly.GetExecutingAssembly(), "Images/ticket-off.svg"))
                : WpfSvgRenderer.CreateImageSource(AssemblyHelper.GetResourceUri(Assembly.GetExecutingAssembly(), "Images/ticket-on.svg"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}