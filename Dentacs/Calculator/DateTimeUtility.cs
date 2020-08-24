﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Suconbu.Dentacs
{
    public class DateTimeUtility
    {
        private static readonly string[] isoDateTimeWithTimeZoneFormats =
        {
            "yyyy-MM-ddTHH:mm:sszzz",
            //"yyyy-MM-ddTHH:mm:sszz",
            "yyyy-MM-ddTHH:mmzzz",
            //"yyyy-MM-ddTHH:mmzz",
            "yyyy-MM-ddTHHzzz",
            //"yyyy-MM-ddTHHzz",

            "yyyyMMddTHHmmsszzz",
            //"yyyyMMddTHHmmsszz",
            "yyyyMMddTHHmmzzz",
            //"yyyyMMddTHHmmzz",
            "yyyyMMddTHHzzz",
            //"yyyyMMddTHHzz",

            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mmZ",
            "yyyy-MM-ddTHHZ",

            "yyyyMMddTHHmmssZ",
            "yyyyMMddTHHmmZ",
            "yyyyMMddTHHZ",
        };
        private static readonly string[] isoDateTimeWithoutTimeZoneformats =
        {
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm",
            "yyyy-MM-ddTHH",
            "yyyy-MM-dd",
            "yyyy-MM",
            //"yyyy",

            "yyyyMMddTHHmmss",
            "yyyyMMddTHHmm",
            "yyyyMMddTHH",
            //"yyyyMMdd",
        };
        private static readonly string[] generalDateTimeFormats =
        {
            "yyyy/M/d H:m:s",
            "yyyy/M/d H:m",
            "yyyy/M/d H",
            "yyyy/M/d",
            "yyyy/M",

            "M/d/yyyy H:m:s",
            "M/d/yyyy H:m",
            "M/d/yyyy H",
            "M/d/yyyy",
            "M/yyyy",

            "M/d H:m:s",
            "M/d H:m",
            "M/d H",
            "M/d",
        };
        private static readonly Regex colonSeparatedTimeRegex = new Regex(@"^(\d+):(\d+)(?::(\d+)(?:\.(\d+))?)?$");
        private static readonly string dayPattern = @"(?:(\d+(?:\.\d+)?)(?:d|day))?";
        private static readonly string hourPattern = @"(?:(\d+(?:\.\d+)?)(?:h|hour))?";
        private static readonly string minutePattern = @"(?:(\d+(?:\.\d+)?)(?:m|min|minute))?";
        private static readonly string seccondPattern = @"(?:(\d+(?:\.\d+)?)(?:s|sec|second))?";
        private static readonly string milliSeccondPattern = @"(?:(\d+(?:\.\d+)?)(?:ms|msec|millisecond))?";
        private static readonly Regex unitSpecifiedTimeRegex = new Regex($"^{dayPattern}\\s*{hourPattern}\\s*{minutePattern}\\s*{seccondPattern}\\s*{milliSeccondPattern}$");

        public static DateTime ParseDateTime(string input)
        {
            if (DateTimeUtility.TryParseDateTime(input, out var result))
            {
                return result;
            }
            throw new FormatException();
        }

        public static TimeSpan ParseTimeSpan(string input)
        {
            if (DateTimeUtility.TryParseTimeSpan(input, out var result))
            {
                return result;
            }
            throw new FormatException();
        }

        public static bool TryParseDateTime(string input, out DateTime result)
        {
            var culture = CultureInfo.InvariantCulture;
            var style = DateTimeStyles.None;
            if (DateTimeOffset.TryParseExact(input, DateTimeUtility.isoDateTimeWithTimeZoneFormats, culture, style, out var date))
            {
                result = date.UtcDateTime;
                return true;
            }
            if (DateTime.TryParseExact(input, DateTimeUtility.isoDateTimeWithoutTimeZoneformats, culture, style, out result))
            {
                return true;
            }
            if (DateTime.TryParseExact(input, DateTimeUtility.generalDateTimeFormats, culture, style, out result))
            {
                return true;
            }
            return false;
        }

        public static bool TryParseTimeSpan(string input, out TimeSpan result)
        {
            var match = DateTimeUtility.colonSeparatedTimeRegex.Match(input);
            if (match.Success)
            {
                // 26:00:00 -> 1day + 02:00:00
                var h = match.Groups[1].Value;
                var m = match.Groups[2].Value;
                var s = match.Groups[3].Value;
                var ms = match.Groups[4].Value;
                var hours = long.Parse(h);
                var minutes = string.IsNullOrEmpty(m) ? 0 : long.Parse(m);
                var seconds = string.IsNullOrEmpty(s) ? 0 : long.Parse(s);
                double milliseconds = string.IsNullOrEmpty(ms) ? 0 : long.Parse(ms);
                milliseconds = milliseconds * 1000.0 / Math.Pow(10.0, ms.Length);
                var ticks = DateTimeUtility.GetTicks(0.0, hours, minutes, seconds + milliseconds / 1000.0);
                result = new TimeSpan(ticks);
                return true;
            }
            match = DateTimeUtility.unitSpecifiedTimeRegex.Match(input);
            if (match.Success)
            {
                var d = match.Groups[1].Value;
                var h = match.Groups[2].Value;
                var m = match.Groups[3].Value;
                var s = match.Groups[4].Value;
                var ms = match.Groups[5].Value;
                var days = string.IsNullOrEmpty(d) ? 0 : double.Parse(d);
                var hours = string.IsNullOrEmpty(h) ? 0 : double.Parse(h);
                var minutes = string.IsNullOrEmpty(m) ? 0 : double.Parse(m);
                var seconds = string.IsNullOrEmpty(s) ? 0 : double.Parse(s);
                var milliseconds = string.IsNullOrEmpty(ms) ? 0 : double.Parse(ms);
                var ticks = DateTimeUtility.GetTicks(days, hours, minutes, seconds + milliseconds / 1000.0);
                result = new TimeSpan(ticks);
                return true;
            }
            result = TimeSpan.Zero;
            return false;
        }

        public static DateTime DateTimeFromSeconds(decimal seconds)
        {
            return new DateTime((long)(seconds * TimeSpan.TicksPerSecond));
        }

        public static TimeSpan TimeSpanFromSeconds(decimal seconds)
        {
            return new TimeSpan((long)(seconds * TimeSpan.TicksPerSecond));
        }

        // yyyy/MM/dd HH:mm:ss[.fff]
        public static string DateTimeToString(DateTime d)
        {
            var sb = new StringBuilder();
            sb.Append(d.ToString("yyyy'/'MM'/'dd' 'HH':'mm':'ss"));
            var milliseconds = GetMillisecondPartFromTicks(d.Ticks) / 1000.0;
            if (milliseconds != 0)
            {
                sb.Append(milliseconds.ToString().Substring(1).TrimEnd('0'));
            }
            return sb.ToString();
        }

        // [{day}d ]{HH}:{mm}:{ss}[.{fff}]
        public static string TimeSpanToString(TimeSpan t)
        {
            var sb = new StringBuilder();
            if (t.Days != 0)
            {
                sb.Append($"{t.Days}d ");
            }
            sb.Append(t.ToString("hh':'mm':'ss"));
            var milliseconds = GetMillisecondPartFromTicks(t.Ticks) / 1000.0;
            if (milliseconds != 0)
            {
                sb.Append(milliseconds.ToString().Substring(1).TrimEnd('0'));
            }
            return sb.ToString();
        }

        private static long GetTicks(double days, double hours, double minutes, double seconds)
        {
            double ticks = 0;
            ticks += TimeSpan.TicksPerSecond * seconds;
            ticks += TimeSpan.TicksPerMinute * minutes;
            ticks += TimeSpan.TicksPerHour * hours;
            ticks += TimeSpan.TicksPerDay * days;
            if (ticks < long.MinValue || long.MaxValue < ticks) throw new OverflowException();
            return (long)ticks;
        }

        private static double GetMillisecondPartFromTicks(long ticks)
        {
            var millisecondTicks = ticks % TimeSpan.TicksPerSecond;
            return (double)millisecondTicks / TimeSpan.TicksPerMillisecond;
        }
    }
}