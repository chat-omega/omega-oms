using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.Notifications;

namespace ZeroPlus.Oms.Ui.Helper
{
    [ValueConversion(typeof(NotificationType), typeof(DrawingBrush))]
    public sealed class NotificationTypeToIconConverter : IValueConverter
    {
        //private readonly string _buyColor = "#6DBE6D";
        //private readonly string _sellColor = "#BE6D6D";
        //private readonly string _cancelColor = "#A8A8A8";

        private ResourceDictionary _resourceDictionary;
        public ResourceDictionary ResourceDictionary
        {
            get => _resourceDictionary;
            set => _resourceDictionary = value;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is not NotificationType notificationType || _resourceDictionary == null)
                {
                    return "";
                }

                return notificationType switch
                {
                    NotificationType.ORDER_BUY_FILL => ResourceDictionary["ChevronUp"],
                    NotificationType.ORDER_SELL_FILL or NotificationType.ORDER_DROP_SELL => ResourceDictionary["ChevronDown"],
                    NotificationType.ORDER_CANCEL => ResourceDictionary["Close"],
                    NotificationType.ORDER_REJECT => ResourceDictionary["ExclamationTriangle"],
                    NotificationType.ORDER_REPLACE => ResourceDictionary["Retweet"],
                    NotificationType.ORDER_BUY_PLACED => ResourceDictionary["CheckSquare"],
                    NotificationType.ORDER_SELL_PLACED => ResourceDictionary["CheckSquare"],
                    NotificationType.ORDER_BUY_PART_FILL or NotificationType.ORDER_DROP_BUY => ResourceDictionary["AngleUp"],
                    NotificationType.ORDER_SELL_PART_FILL => ResourceDictionary["AngleDown"],
                    NotificationType.FIRST_EDGE_ACQUIRED => ResourceDictionary["CheckSquare"],
                    NotificationType.ALERT => ResourceDictionary["Alert"],
                    _ => ResourceDictionary["ExclamationTriangle"],
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
