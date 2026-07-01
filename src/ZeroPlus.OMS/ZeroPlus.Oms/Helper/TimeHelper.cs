using System;

namespace ZeroPlus.Oms.Helper
{
    public class TimeHelper
    {
        private const int MONDAY = 1;
        private const int TUESDAY = 2;
        private const int WEDNESDAY = 3;
        private const int THURSDAY = 4;
        private const int FRIDAY = 5;
        private const int SATURDAY = 6;
        private const int SUNDAY = 7;

        public static DateTime MarketCloseEastern => DateTime.Today + new TimeSpan(16, 30, 00);

        public static int GetWeekendDaysBetween(DateTime startDate, DateTime endDate)
        {
            if (endDate < startDate)
            {
                return 0;
            }

            TimeSpan timeBetween = endDate.Subtract(startDate);
            int weekendsBetween = timeBetween.Days / 7;
            int sundays = weekendsBetween;
            int saturdays = weekendsBetween;
            int startDay = GetDayOfWeekNumber(startDate.DayOfWeek);
            int endDay = GetDayOfWeekNumber(endDate.DayOfWeek);
            if (startDay > endDay)
            {
                sundays++;
                saturdays += (startDay < SUNDAY) ? 1 : 0;
            }
            else if (startDay < endDay)
            {
                saturdays += (endDay == SUNDAY) ? 1 : 0;
            }

            return saturdays + sundays;
        }

        private static int GetDayOfWeekNumber(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => MONDAY,
                DayOfWeek.Tuesday => TUESDAY,
                DayOfWeek.Wednesday => WEDNESDAY,
                DayOfWeek.Thursday => THURSDAY,
                DayOfWeek.Friday => FRIDAY,
                DayOfWeek.Saturday => SATURDAY,
                DayOfWeek.Sunday => SUNDAY,
                _ => throw new ArgumentException("Invalid day!"),
            };
        }

        public static bool IsThirdFridayOfTheMonth(DateTime expiration)
        {
            return IsThirdOccurenceInMonth(expiration, DayOfWeek.Friday);
        }

        public static bool IsThirdWednesdayOfTheMonth(DateTime expiration)
        {
            return IsThirdOccurenceInMonth(expiration, DayOfWeek.Wednesday);
        }

        private static bool IsThirdOccurenceInMonth(DateTime expiration, DayOfWeek dayOfWeek)
        {
            DateTime firstDayOfMonth = new(expiration.Year, expiration.Month, 1, expiration.Hour, expiration.Minute, expiration.Second);

            DateTime firstoccurrence = firstDayOfMonth.DayOfWeek == dayOfWeek ?
                                  firstDayOfMonth :
                                  firstDayOfMonth.AddDays(dayOfWeek - firstDayOfMonth.DayOfWeek);

            int count = 2;
            if (firstoccurrence.Month < expiration.Month ||
               (firstoccurrence.Month == 12 && expiration.Month == 1))
            {
                count++;
            }

            DateTime thirdOccurrence = firstoccurrence.AddDays(7 * count);
            bool isMatch = thirdOccurrence.Date == expiration.Date;
            return isMatch;
        }

        public static bool IsLastDayOfTheMonth(DateTime expiration)
        {
            DateTime lastDay = new DateTime(expiration.Year, expiration.Month, 1).AddMonths(1).AddDays(-1);
            return expiration.Date == lastDay.Date;
        }
    }
}
