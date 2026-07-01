using System;
using System.Text.RegularExpressions;

namespace ZeroPlus.Oms.Ui.LowLatency.Ext
{
    /// <summary>
    /// Helper methods taken from scalp hunter
    /// </summary>
    internal class Helpers
    {
        const char ORDERID_PREFIX = '#';
        const char COMPONENT_ID_SEPARATOR = ':';


        public static readonly string strExecutionHunter = "Hunter";
        public static readonly string strExecutionChaser = "Chaser";
        public static readonly string strExecutionDrifter = "Drifter";
        public static readonly string strExecutionTrailer = "Trailer";
        public static readonly string strExecutionBracket = "Bracket";
        public static readonly string strExecutionManual = "Manual";
        public static readonly string strSignalTradeWatcher = "TradeWatcher";

        public static void Decode(string s, string action, ref string who, ref string what)
        {
            if (string.IsNullOrWhiteSpace(s) || s[0] != ORDERID_PREFIX)
            {
                who = "Unknown";
                what = "Unknown";
                return;
            }

            string[] ss = s.Split(COMPONENT_ID_SEPARATOR);
            if (ss.Length < 3)
            {
                who = "Unknown";
                what = "Unknown";
                return;
            }

            string userid = ss[2];

            var clOrdId = ss[3];

            char initOrLiq = clOrdId[0];
            char strat = clOrdId[2];
            char x = clOrdId[1];

            switch (initOrLiq)
            {
                case 'I':
                    switch (strat)
                    {
                        case 'H':
                            who = strExecutionHunter;
                            break;
                        case 'D':
                            who = strExecutionDrifter;
                            break;
                        case 'B':
                            who = strExecutionBracket;
                            break;
                        case 'M':
                            who = strExecutionManual;
                            break;
                        case 'T':
                            who = strExecutionTrailer;
                            break;
                        default:
                            who = "Initiator";
                            break;
                    }

                    break;
                default:
                    switch (strat)
                    {
                        case 'C':
                            who = strExecutionChaser;
                            break;
                        case 'D':
                            who = strExecutionDrifter;
                            break;
                        case 'B':
                            who = strExecutionBracket;
                            break;
                        case 'T':
                            who = strExecutionTrailer;
                            break;
                        case 'M':
                            who = strExecutionManual;
                            break;
                        default:
                            who = "Liquidator";
                            break;
                    }

                    break;
            }

            switch (x)
            {
                case 'B':
                    what = "OpenBuy";
                    break;
                case 'b':
                    what = "CloseBuy";
                    break;
                case 'S':
                    what = "OpenSell";
                    break;
                case 's':
                    what = "CloseSell";
                    break;
                case 'C':
                    what = "CancelBuy";
                    break;
                case 'c':
                    what = "CancelSell";
                    break;
                case 'R':
                    what = "ReplaceBuy";
                    break;
                case 'r':
                    what = "ReplaceSell";
                    break;
                default:
                    what = "?";
                    break;
            }
        }

        public class XConverter
        {
            public static decimal asDecimal(string str)
            {
                if (string.IsNullOrEmpty(str)) return 0;
                return decimal.Parse(str);
            }
        }

        public class SymbolNamer
        {
            private static readonly Regex reOptionTOS2TBSym =
                new Regex(@"^(\.\S+?)(\d{6}[CP])(.*$)", RegexOptions.Singleline | RegexOptions.Compiled);
            private static readonly Regex reOptionTB2TOSSym =
                new Regex(@"^(\.\S+?)\s*(\d{6}[CP])(.*$)", RegexOptions.Singleline | RegexOptions.Compiled);

            private static readonly char[] period = { '.' };

            public static string PatchTOS2TB(string symbol)
            {
                if (symbol[0] != '.')
                {
                    return symbol[0] == '/' ? symbol : symbol.Replace('.', '/');
                }

                Match match = reOptionTOS2TBSym.Match(symbol);
                if (!match.Success || match.Groups.Count != 4) return symbol;

                string[] ss = $"{match.Groups[3]}".Split(period, 2);
                string whole = ss[0] == "0" ? "" : ss[0];

                string strike = ss.Length == 2 ? ss[1].PadRight(3, '0') : "000";

                //string s = $"{match.Groups[1]} {match.Groups[2]}{whole}{strike}";
                //System.Diagnostics.Debug.WriteLine($"PatchTOS2TB [{symbol}] -> [{s}]");
                //var xxx = PatchTB2TOS(s);
                //System.Diagnostics.Debug.WriteLine($"PatchTB2TOS [{s}] -> [{xxx}]");

                return $"{match.Groups[1]} {match.Groups[2]}{whole}{strike}";
            }

            public static string PatchTB2TOS(string symbol)
            {
                Match match = reOptionTB2TOSSym.Match(symbol);
                if (!match.Success || match.Groups.Count != 4) return symbol;

                string ss = $"{match.Groups[3]}";
                string whole = $"{ss.Substring(0, ss.Length - 3)}".TrimStart('0');
                string dec = ss.Substring(ss.Length - 3).TrimEnd('0');
                string strike = $"{(whole.Length == 0 ? "0" : whole)}{(dec.Length == 0 ? "" : $".{dec}")}";

                return $"{match.Groups[1]}{match.Groups[2]}{strike}";
            }
        }

        public class ExpiredOptionHelper
        {
            public static readonly Regex reOption = new Regex(@"^[.].*(\d{6})([CP])[\d.]", RegexOptions.Compiled);

            private DateTime DaysAgo = DateTime.Today;

            public int MaxDaysBack
            {
                set
                {
                    switch (DateTime.Now.DayOfWeek)
                    {
                        case DayOfWeek.Saturday:
                            DaysAgo = DateTime.Today.AddDays(-(value + 1));
                            break;
                        case DayOfWeek.Sunday:
                            DaysAgo = DateTime.Today.AddDays(-(value + 2));
                            break;
                        case DayOfWeek.Monday:
                        case DayOfWeek.Tuesday:
                        case DayOfWeek.Wednesday:
                        case DayOfWeek.Thursday:
                        case DayOfWeek.Friday:
                        default:
                            DaysAgo = DateTime.Today.AddDays(-value);
                            break;
                    }
                }
            }

            public bool IsExpired(string symbol)
            {
                if (string.IsNullOrEmpty(symbol)) return true;

                var match = reOption.Match(symbol);
                if (!match.Success) return false;

                if (!int.TryParse(match.Groups[1].Value, out int yymmdd)) return false;

                if (yymmdd < 201001 || yymmdd > 300101) return true;

                return new DateTime(2000 + yymmdd / 10000, (yymmdd / 100) % 100, yymmdd % 100) < DaysAgo;
            }

            public static int DaysToExpiration(string symbol)
            {
                if (string.IsNullOrEmpty(symbol)) return -1;

                var match = reOption.Match(symbol);
                if (!match.Success) return -1;

                if (!int.TryParse(match.Groups[1].Value, out int yymmdd)) return -1;

                if (yymmdd < 201001 || yymmdd > 300101) return -1;

                var d = new DateTime(2000 + yymmdd / 10000, (yymmdd / 100) % 100, yymmdd % 100);

                var dte = d - DateTime.Today;

                return dte.Days;
            }

            public static bool IsWithin(string symbol, DateTime startDate, DateTime endDate, string cp = "CP")
            {
                if (string.IsNullOrEmpty(symbol)) return false;

                var match = reOption.Match(symbol);
                if (!match.Success) return false;

                var xcp = match.Groups[2].Value;
                if (!cp.Contains(xcp)) return false;

                if (!int.TryParse(match.Groups[1].Value, out int yymmdd)) return false;

                if (yymmdd < 201001 || yymmdd > 300101) return true;

                DateTime dt = new DateTime(2000 + yymmdd / 10000, (yymmdd / 100) % 100, yymmdd % 100);

                return dt >= startDate && dt <= endDate;
            }
        }
    }
}
