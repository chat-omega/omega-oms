using System;

namespace ZeroPlus.Oms.Ui.Notifications
{
    public class Notification
    {
        public string Text { get; set; }

        public NotificationType Type { get; set; }

        public DateTime Time { get; set; }

        public int Height { get; set; }

        public string Account { get; set; }

        public object Tag { get; set; }

        public Notification()
        {
        }
    }
}
