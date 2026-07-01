using System;

namespace ZeroPlus.Oms.Ui.Extensions
{
    public static class UnixEpochConverterExtensions
    {
        public static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public static readonly TimeZoneInfo easternZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        public static readonly TimeZoneInfo cstZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        public static readonly TimeZoneInfo localZone = TimeZoneInfo.Local;
        private const long UnixEpochTicks = 621355968000000000; // Ticks between Unix epoch and Windows epoch

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

        public static DateTime ConvertToWindowsDateTimeFromBlockTimestamp(this ulong blockTimestamp)
        {
            uint epochSec = (uint)(blockTimestamp >> 32);
            uint epochNanos = (uint)blockTimestamp;

            double unixEpochSeconds = epochSec;

            long totalTicks = (long)(unixEpochSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks + (epochNanos / 100);

            DateTime windowsDateTime = new(totalTicks, DateTimeKind.Utc);

            return windowsDateTime;
        }

        public static DateTime ConvertToWindowsDateTimeFromNanos(this ulong timestampNanos)
        {
            uint epochSec = (uint)(timestampNanos / 1_000_000_000);
            uint epochNanos = (uint)(timestampNanos % 1_000_000_000);

            double unixEpochSeconds = epochSec;

            long totalTicks = (long)(unixEpochSeconds * TimeSpan.TicksPerSecond) + UnixEpochTicks + (epochNanos / 100);

            DateTime windowsDateTime = new(totalTicks, DateTimeKind.Utc);

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

        public static DateTime ConvertToWindowsDateTimeWithLocalTimeZone(this ulong timestampNanos)
        {
            DateTime utcDateTime = timestampNanos.ConvertToWindowsDateTimeFromNanos();
            DateTime localDateTime = utcDateTime.ToLocalTime();

            return localDateTime;
        }
    }
}
