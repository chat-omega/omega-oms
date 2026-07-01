using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Oms.Data.Securities;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms.Helper
{
    public class OptionsHelper
    {
        private const int CACHE_INTERVAL = 5000;

        public static readonly HashSet<string> Indices = new()
        {
            "BKX", "DJX", "HGX", "MRUT", "MXEA", "MXEF", "NANOS", "NDX", "NQX", "NYFANG", "OEX", "OSX", "RLG", "RLV",
            "RUI", "RUT", "SIXB", "SIXI", "SIXM", "SIXRE", "SIXU", "SIXV", "SOX", "SPESG", "SPIKE", "SPX",
            "UTY", "VIX", "XAU", "XDA", "XDB", "XDC", "XDE", "XDN", "XDS", "XDZ", "XEO", "XND", "XSP"
        };

        private static readonly ConcurrentDictionary<Tuple<string, DateTime, OptionType>, Option> _cache = new();
        private static readonly bool USE_SYMBOL_LIB = true;
        private static readonly Regex _optionSymbolRegex = new(@"([.]{1})([\w\d]*)([\d]{6})([PC]{1})([\d]*)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly CultureInfo _enUSCultureInfo = new("en-US");
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public static bool IsIndex(string value)
        {
            return !string.IsNullOrEmpty(value) && Indices.Contains(value.ToUpper());
        }

        public static string GetSymbolFromOption(Option omsOption)
        {
            string underlyingSymbol = omsOption.RootSymbol?.Replace("$", string.Empty);
            string expirationString = omsOption.Expiration.ToString("yyMMdd");
            char callPut = omsOption.Type.ToString()[0];
            double strike = omsOption.Strike;
            string optionSymbol = $".{underlyingSymbol}{expirationString}{callPut}{strike}";
            return optionSymbol;
        }

        public static string GetSymbolFromComponents(string underlyingSymbol, DateTime expiratio, string callPutString, double strike)
        {
            underlyingSymbol = underlyingSymbol?.Replace("$", string.Empty);
            string expirationString = expiratio.ToString("yyMMdd");
            char callPut = callPutString[0];
            string optionSymbol = $".{underlyingSymbol}{expirationString}{callPut}{strike}";
            return optionSymbol;
        }

        public static string GetUnderlyingFromSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Length <= 1)
            {
                throw new SlimException("Symbol can not be empty");
            }

            Option option = GetOptionFromSymbol(symbol);

            return option != null ? option.UnderlyingSymbol : throw new SlimException($"Failed to parse {symbol}");
        }

        public static Option GetOptionFromSymbol(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
            {
                throw new SlimException("Symbol can not be null or empty");
            }

            Option option = USE_SYMBOL_LIB ? GetOptionFromSymbolSymbolLib(symbol) : GetOptionFromSymbolRegex(symbol);
            if (option != null)
            {
                if (IsIndex(option.UnderlyingSymbol))
                {
                    option.UnderlyingSymbol = "$" + option.UnderlyingSymbol;
                }
            }
            return option;
        }

        public static void CorrectLegOrder(SymbolCodec spread)
        {
            List<Instrument> legs = new();
            for (int i = 0; i < spread.LegCount; i++)
            {
                legs.Add(spread.GetLeg(i));
            }

            bool callSpread = !legs[0].callPut;
            if (callSpread)
            {
                legs = legs.GroupBy(x => x.expiration)
                        .OrderBy(x => x.Key)
                        .SelectMany(g => g.OrderBy(x => x.strike).ToList())
                        .ToList();
            }
            else
            {
                legs = legs.GroupBy(x => x.expiration)
                        .OrderBy(x => x.Key)
                        .SelectMany(g => g.OrderByDescending(x => x.strike).ToList())
                        .ToList();
            }

            for (int i = 0; i < legs.Count; i++)
            {
                spread.SetLeg(i, legs[i]);
            }
        }

        private static Option GetOptionFromSymbolSymbolLib(string symbol)
        {
            Instrument instrument = new(symbol);
            Option option = new()
            {
                OptionSymbol = symbol,
                UnderlyingSymbol = instrument.underlyingSymbol,
                RootSymbol = instrument.rootSymbol,
                Expiration = instrument.expiration,
                Type = instrument.callPut ? OptionType.PUT : OptionType.CALL,
                Strike = instrument.strike,
            };
            return option;
        }

        private static Option GetOptionFromSymbolRegex(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Length <= 1)
            {
                throw new SlimException("Symbol can not be empty");
            }

            Match result = _optionSymbolRegex.Match(symbol);

            if (result.Success)
            {
                string rootSymbol = result.Groups[2].Value;
                string exp = result.Groups[3].Value;
                string type = result.Groups[4].Value;
                string strikeString = result.Groups[5].Value;

                Option omsOption = new();
                #region Root and Under
                omsOption.UnderlyingSymbol = GetUnderlyingFromRootSymbol(rootSymbol);
                omsOption.RootSymbol = rootSymbol;
                omsOption.OptionSymbol = symbol;
                #endregion

                #region Expiration
                if (DateTime.TryParseExact(exp, "yyMMdd", _enUSCultureInfo, DateTimeStyles.None, out DateTime expiration))
                {
                    omsOption.Expiration = expiration;

                    if (rootSymbol is "SPX" or "NDX" or "RUT")
                    {
                        omsOption.Expiration += new TimeSpan(8, 30, 0);
                    }
                    else if (rootSymbol is "SPXW" or "NDXP" or "RUTW")
                    {
                        omsOption.Expiration += new TimeSpan(15, 15, 00);
                    }
                    else
                    {
                        omsOption.Expiration += new TimeSpan(15, 0, 0);
                    }
                }
                else
                {
                    throw new SlimException($"Parsing Expiration failed for {symbol}");
                }
                #endregion

                #region Type

                omsOption.Type = type == "P" ? OptionType.PUT : OptionType.CALL;

                #endregion

                #region Strike
                if (double.TryParse(strikeString, out double strike))
                {
                    omsOption.Strike = strikeString.Length == 8 ? strike / 1000.0 : strike;
                }
                else
                {
                    throw new SlimException($"Parsing Strike failed for {symbol}");
                }
                #endregion
                return omsOption;
            }
            else
            {
                throw new SlimException($"{symbol} is not a valid option symbol.");
            }
        }

        private static string GetUnderlyingFromRootSymbol(string rootSymbol)
        {
            if (rootSymbol is "SPX" or "SPXW")
            {
                return "$SPX";
            }
            else if (rootSymbol is "NDX" or "NDXP")
            {
                return "$NDX";
            }
            else if (rootSymbol is "RUT" or "RUTW")
            {
                return "$RUT";
            }
            else if (rootSymbol == "OEX")
            {
                return "$OEX";
            }
            else
            {
                return Regex.Replace(rootSymbol, @"[\d-]", string.Empty);
            }
        }

        public static async Task<Option> GetAtmOption(string underlyingSymbol, DateTime expiration, OptionType type)
        {
            try
            {
                Tuple<string, DateTime, OptionType> key = Tuple.Create(underlyingSymbol, expiration, type);
                if (!_cache.TryGetValue(key, out Option atmOption))
                {
                    List<Option> optionChain = await OmsCore.QuoteClient.GetSymbolsAsync(underlyingSymbol);
                    List<Option> options = optionChain.Where(x => x.Expiration == expiration && x.Type == type).ToList();
                    DataStore vegaStore = new(OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                    vegaStore.GetHanweckDataFor(options, SubscriptionFieldType.Vega);
                    double value = double.MinValue;
                    atmOption = null;
                    foreach (Option option in options)
                    {
                        double vega = await vegaStore.GetDataAsync(option.OptionSymbol);
                        if (vega > value)
                        {
                            atmOption = option;
                            value = vega;
                        }
                    }
                    if (atmOption != null)
                    {
                        _cache[key] = atmOption;
                        Timer clearTimer = new(CACHE_INTERVAL)
                        {
                            AutoReset = false
                        };
                        clearTimer.Elapsed += (s, e) => _cache.TryRemove(key, out _);
                        clearTimer.Start();
                    }
                }
                return atmOption;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
