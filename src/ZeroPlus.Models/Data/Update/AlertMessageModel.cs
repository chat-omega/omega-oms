using System;

namespace ZeroPlus.Models.Data.Update
{
    public class AlertMessageModel
    {
        public int AlertId { get; }
        public DateTime Time { get; }
        public string Message { get; }

        public AlertMessageModel(int alertId, DateTime time, string message)
        {
            AlertId = alertId;
            Time = time;
            Message = message;
        }
    }
}
