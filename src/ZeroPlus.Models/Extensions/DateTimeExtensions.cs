using System;

namespace ZeroPlus.Models.Extensions
{
    public static class DateTimeExtensions
    {
        private static readonly TimeZoneInfo _easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        private static readonly TimeZoneInfo _utcZone = TimeZoneInfo.Utc;
        public static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly ulong UUnixEpoch = ToUnixEpoch(DateTime.UnixEpoch);
        public static readonly TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        public static readonly TimeZoneInfo cstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        public static readonly TimeZoneInfo localZone = TimeZoneInfo.Local;
        public const long UnixEpochTicks = 621355968000000000; // Ticks between Unix epoch and Windows epoch
        public static long _localOffsetTicks = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).Ticks;

        public static ulong ConvertToUnixEpoch(this DateTime dateTime)
        {
            try
            {
                return (ulong)((dateTime.Subtract(UnixEpoch).TotalMilliseconds * 1_000_000) + 1.8e+13);
            }
            catch (Exception)
            {
                return UnixEpochTicks;
            }
        }
        
        public static long ConvertNanosToWindowsTicks(this ulong timestampNanos)
        {
            return (long)(timestampNanos / 100) + UnixEpochTicks;
        }
        
        public static long ConvertNanosToWindowsTicksLocal(this ulong timestampNanos)
        {
            return ConvertNanosToWindowsTicks(timestampNanos) + _localOffsetTicks;
        }

        public static DateTime ConvertToWindowsDateTimeFromBlockTimestamp(this ulong blockTimestamp)
        {
            uint epochSec = (uint)(blockTimestamp >> 32);
            uint epochNanos = (uint)blockTimestamp;

            double unixEpochSeconds = epochSec;

            long totalTicks = (long)(unixEpochSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks + (epochNanos / 100);

            DateTime windowsDateTime = new DateTime(totalTicks, DateTimeKind.Utc);

            return windowsDateTime;
        }

        public static DateTime ConvertToWindowsDateTimeFromNanos(this ulong timestampNanos)
        {
            uint epochSec = (uint)(timestampNanos / 1_000_000_000);
            uint epochNanos = (uint)(timestampNanos % 1_000_000_000);

            double unixEpochSeconds = epochSec;

            long totalTicks = (long)(unixEpochSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks + (epochNanos / 100);

            DateTime windowsDateTime = new DateTime(totalTicks, DateTimeKind.Utc);

            return windowsDateTime;
        }

        public static string ToHHMMSSfff(this DateTime dateTime)
        {
            return $"{dateTime:HH:mm:ss.fff}";
        }

        public static string ToHHMMSSfff(this ulong timestampNanos)
        {
            return $"{ConvertToWindowsDateTimeWithLocalTimeZone(timestampNanos):HH:mm:ss.fff}";
        }

        public static string ToHHMMSSffffff(this ulong timestampNanos)
        {
            return $"{ConvertToWindowsDateTimeWithLocalTimeZone(timestampNanos):HH:mm:ss.ffffff}";
        }

        public static int ConvertToYyMMddInt(this DateTime dateTime)
        {
            string year = dateTime.Year.ToString()[2..];
            string month = dateTime.Month.ToString("D2");
            string day = dateTime.Day.ToString("D2");

            string yyMMddString = year + month + day;
            return int.Parse(yyMMddString);
        }

        public static DateTime ConvertToWindowsDateTimeWithLocalTimeZone(this ulong timestampNanos)
        {
            DateTime utcDateTime = timestampNanos.ConvertToWindowsDateTimeFromNanos();
            DateTime localDateTime = utcDateTime.ToLocalTime();

            return localDateTime;
        }

        public static DateTime ToEastern(this DateTime localTime)
        {
            return TimeZoneInfo.ConvertTime(localTime, TimeZoneInfo.Local, _easternZone);
        }

        public static DateTime FromEastern(this DateTime easternTime)
        {
            easternTime = DateTime.SpecifyKind(easternTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(easternTime, _easternZone, TimeZoneInfo.Local);
        }

        public static DateTime ToUtc(this DateTime localTime)
        {
            return localTime.ToUniversalTime();
        }

        public static DateTime FromUtc(this DateTime utcTime)
        {
            utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTime(utcTime, _utcZone, TimeZoneInfo.Local);
        }

        public static DateTime FromUnixEpoch(this ulong timestamp)
        {
            try
            {
                if (timestamp < 1_701_982_800_011 || timestamp > 5_000_000_000_000_000)
                {
                    return DateTime.UnixEpoch;
                }
                else
                {
                    return DateTime.UnixEpoch.AddMilliseconds(timestamp).ToLocalTime();
                }
            }
            catch (Exception)
            {
                return DateTime.UnixEpoch;
            }
        }

        public static ulong ToUnixEpoch(this DateTime dateTime)
        {
            try
            {
                return dateTime == default ? UUnixEpoch : (ulong)((DateTimeOffset)dateTime).ToUnixTimeMilliseconds();
            }
            catch (Exception)
            {
                return UUnixEpoch;
            }
        }
    }
}
