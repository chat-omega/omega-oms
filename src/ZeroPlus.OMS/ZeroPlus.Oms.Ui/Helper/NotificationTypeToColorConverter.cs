using System;
using System.Globalization;
using System.Windows.Data;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(NotificationType), typeof(string))]
    public sealed class NotificationTypeToColorConverter : IValueConverter
    {
        private readonly string _buyColor = "#6DBE6D";
        private readonly string _sellColor = "#BE6D6D";
        private readonly string _edgeColor = "#36a46a";
        private readonly string _cancelColor = "#A8A8A8";
        private readonly string _alertColor = "#FFB115";
        private readonly string _alertBgColor = "#CC0000";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not NotificationType notificationType)
                {
                    return _cancelColor;
                }

                if (parameter is string arg)
                {
                    if (arg == "BG")
                    {
                        return notificationType switch
                        {
                            NotificationType.ALERT => _alertBgColor,
                            _ => _cancelColor,
                        };
                    }
                }

                return notificationType switch
                {
                    NotificationType.ORDER_BUY_FILL or NotificationType.ORDER_DROP_BUY => _buyColor,
                    NotificationType.ORDER_SELL_FILL or NotificationType.ORDER_DROP_SELL => _sellColor,
                    NotificationType.ORDER_CANCEL => _cancelColor,
                    NotificationType.ORDER_REJECT => _cancelColor,
                    NotificationType.ORDER_BUY_PLACED or NotificationType.ORDER_BUY_PART_FILL => _buyColor,
                    NotificationType.ORDER_SELL_PLACED or NotificationType.ORDER_SELL_PART_FILL => _sellColor,
                    NotificationType.FIRST_EDGE_ACQUIRED => _edgeColor,
                    NotificationType.ALERT => _alertColor,
                    _ => _cancelColor,
                };
            }
            catch (Exception)
            {
                return _cancelColor;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}
