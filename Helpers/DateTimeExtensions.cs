using System;

namespace TambayanCafeAPI.Helpers
{
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt)
        {
            var diff = (int)DayOfWeek.Monday - (int)dt.DayOfWeek;
            if (diff > 0) diff -= 7;
            return dt.AddDays(diff).Date;
        }

        public static DateTime EndOfWeek(this DateTime dt)
        {
            return dt.StartOfWeek().AddDays(6).Date.AddDays(1).AddTicks(-1);
        }

        public static DateTime StartOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1);
        }

        public static DateTime EndOfMonth(this DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, 1).AddMonths(1).AddDays(-1).Date.AddDays(1).AddTicks(-1);
        }
    }
}