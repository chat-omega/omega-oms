using DevExpress.Xpf.Charts;
using System;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public static class DateTimeHelper
    {
        public static TimeSpan ConvertInterval(ChartIntervalItem interval, int intervalsCount)
        {
            return GetInterval(interval.MeasureUnit, interval.MeasureUnitMultiplier * intervalsCount);
        }

        public static DateTime GetInitialDate(ChartIntervalItem interval)
        {
            DateTime now = DateTime.Now;
            switch (interval.MeasureUnit)
            {
                case DateTimeMeasureUnit.Second:
                    DateTime roundSeconds = now.AddMilliseconds(-now.Millisecond);
                    return roundSeconds.AddSeconds(-roundSeconds.Second % interval.MeasureUnitMultiplier);
                case DateTimeMeasureUnit.Minute:
                    return now.Date.AddHours(now.Hour).AddMinutes(now.Minute - (now.Minute % interval.MeasureUnitMultiplier));
                case DateTimeMeasureUnit.Hour:
                    return now.Date.AddHours(now.Hour - (now.Hour % interval.MeasureUnitMultiplier));
                case DateTimeMeasureUnit.Day:
                    return now.Date;
                case DateTimeMeasureUnit.Week:
                    return now.Date.AddDays(-(7 + (now.DayOfWeek - DayOfWeek.Monday)) % 7);
                case DateTimeMeasureUnit.Month:
                    return new DateTime(now.Year, now.Month, 1);
            }
            return DateTime.Now;
        }
        public static TimeSpan GetInterval(DateTimeMeasureUnit measureUnit, int multiplier)
        {
            return measureUnit switch
            {
                DateTimeMeasureUnit.Second => TimeSpan.FromSeconds(multiplier),
                DateTimeMeasureUnit.Minute => TimeSpan.FromMinutes(multiplier),
                DateTimeMeasureUnit.Hour => TimeSpan.FromHours(multiplier),
                DateTimeMeasureUnit.Day => TimeSpan.FromDays(multiplier),
                DateTimeMeasureUnit.Week => TimeSpan.FromDays(multiplier * 7),
                DateTimeMeasureUnit.Month => TimeSpan.FromDays(multiplier * 30),
                _ => TimeSpan.Zero,
            };
        }
    }
}
