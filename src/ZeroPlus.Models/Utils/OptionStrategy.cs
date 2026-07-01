using SymbolLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils
{
    public class OptionStrategy
    {
        private const double STRIKE_TOLERANCE = .01;

        private static readonly Regex _legSplitRegex = new Regex(@"(?=[-+])", RegexOptions.Compiled);
        private static readonly ConcurrentDictionary<string, Tuple<string, string, string, bool>> _cache = new ConcurrentDictionary<string, Tuple<string, string, string, bool>>();

        /// <summary>
        ///    Tries to identify option strategy from a string representation of a spread.
        ///    A return value indicates whether the conversion succeeded.
        /// 
        /// </summary>
        /// <param name="spreadId">
        ///     A string representation of the spread in TOS format.
        /// </param>
        /// <param name="baseStrategy"></param>
        /// <param name="spreadType"></param>
        /// <param name="spreadDescription"></param>
        /// <returns>
        ///    true if strategy was identified successfully; otherwise, false.
        /// </returns>
        public static bool TryIdentify(string spreadId, out BaseStrategy baseStrategy, out string spreadType, out string spreadDescription)
        {
            baseStrategy = BaseStrategy.CUSTOM;
            if (TryIdentify(spreadId, out string baseTypeString, out spreadType, out spreadDescription))
            {
                baseStrategy = ConvertFromString(baseTypeString);
                return true;
            }
            return false;
        }

        /// <summary>
        ///    Tries to identify option strategy from a string representation of a spread.
        ///    A return value indicates whether the conversion succeeded.
        /// 
        /// </summary>
        /// <param name="spreadId">
        ///     A string representation of the spread in TOS format.
        /// </param>
        /// <param name="baseType"></param>
        /// <param name="spreadType"></param>
        /// <param name="spreadDescription"></param>
        /// <returns>
        ///    true if strategy was identified successfully; otherwise, false.
        /// </returns>
        public static bool TryIdentify(string spreadId, out string baseType, out string spreadType, out string spreadDescription)
        {
            if (string.IsNullOrWhiteSpace(spreadId))
            {
                baseType = "";
                spreadType = "";
                spreadDescription = "";
                return false;
            }
            if (_cache.TryGetValue(spreadId, out Tuple<string, string, string, bool>? cache))
            {
                baseType = cache.Item1;
                spreadType = cache.Item2;
                spreadDescription = cache.Item3;
                return cache.Item4;
            }

            List<SpreadLeg> legs = ParseLegs(spreadId);
            bool result = EvaluateLegs(legs, out baseType, out spreadType, out spreadDescription);
            _cache[spreadId] = Tuple.Create(baseType, spreadType, spreadDescription, result);
            return result;
        }

        public Side? IdentifyOrderSide(IOrder order, string? side = null)
        {
            if (order.Side == null)
            {
                if (!order.IsComplexOrder)
                {
                    if (!string.IsNullOrEmpty(side))
                    {
                        order.Side = side.Contains("SELL", StringComparison.OrdinalIgnoreCase) ? Side.Sell : Side.Buy;
                    }
                }
                else
                {
                    Data.Trading.ComplexOrder complexOrder = (Data.Trading.ComplexOrder)order;
                    var complexOrderLegs = complexOrder.Legs;
                    if (complexOrderLegs.Count > 1)
                    {
                        if (complexOrderLegs.All(x => x.Security != null && x.Security.SecurityType == SecurityType.Option))
                        {
                            var baseStrategy = order.BaseStrategy;
                            var result = IdentifySpreadSide(baseStrategy, complexOrderLegs);

                            complexOrder.Side = result ?? (complexOrder.AveragePrice < 0 || complexOrder.Price < 0 || (side != null && side.Contains("SELL", StringComparison.OrdinalIgnoreCase)) ? Side.Sell : Side.Buy);
                        }
                    }
                }
            }

            return order.Side;
        }

        public static Side? IdentifySpreadSide(string baseStrategy, IEnumerable<IComplexOrderLeg> complexOrderLegs)
        {
            return IdentifySpreadSide(ConvertFromString(baseStrategy), complexOrderLegs);
        }

        public static Side? IdentifySpreadSide(BaseStrategy baseStrategy, IEnumerable<IComplexOrderLeg> complexOrderLegs)
        {
            Side? result = null;
            switch (baseStrategy)
            {
                case BaseStrategy.CALL_1X2:
                case BaseStrategy.CALL_1X3:
                case BaseStrategy.CALL_2X3:
                case BaseStrategy.CALL_VERTICAL:
                case BaseStrategy.CALL_1X3X3X1:
                case BaseStrategy.CALL_CONDOR:
                case BaseStrategy.PUT_CONDOR:
                case BaseStrategy.STRADDLE:
                case BaseStrategy.STRANGLE:
                case BaseStrategy.SYNTHETIC_COLLAR:
                case BaseStrategy.COVERED_STRADDLE:
                    result = complexOrderLegs.OrderBy(x => ((Option)x.Security!).Strike).FirstOrDefault()?.Side;
                    break;
                case BaseStrategy.BOX:
                case BaseStrategy.GUT_IRON_FLY:
                case BaseStrategy.GUT_SKEWED_IRON_FLY:
                case BaseStrategy.GUT_IRON_CONDOR:
                case BaseStrategy.GUT_SKEWED_IRON_CONDOR:
                case BaseStrategy.IRON_BUTTERFLY:
                case BaseStrategy.PUT_RATIO_FLY:
                case BaseStrategy.CALL_RATIO_FLY:
                case BaseStrategy.RATIO_FLY:
                case BaseStrategy.DRAGONFLY:
                case BaseStrategy.RATIO_DRAGONFLY:
                case BaseStrategy.DRAGON:
                case BaseStrategy.RATIO_DRAGON:
                case BaseStrategy.IRON_CONDOR:
                case BaseStrategy.MARRIED_STRADDLE:
                case BaseStrategy.CALL_SPREAD_VS_PUT:
                case BaseStrategy.PUT_SPREAD_VS_CALL:
                    result = complexOrderLegs.OrderBy(x => ((Option)x.Security!).Strike).FirstOrDefault()?.Side == Side.Buy ? Side.Sell : Side.Buy;
                    break;
                case BaseStrategy.PUT_1X2:
                case BaseStrategy.PUT_1X3:
                case BaseStrategy.PUT_2X3:
                case BaseStrategy.PUT_VERTICAL:
                case BaseStrategy.PUT_1X3X3X1:
                    result = complexOrderLegs.OrderByDescending(x => ((Option)x.Security!).Strike).FirstOrDefault()?.Side;
                    break;
                case BaseStrategy.CALL_1x3x2:
                case BaseStrategy.PUT_1x3x2:
                case BaseStrategy.CALL_2x3x1:
                case BaseStrategy.PUT_2x3x1:
                case BaseStrategy.CALL_BUTTERFLY:
                case BaseStrategy.PUT_BUTTERFLY:
                case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                    result = complexOrderLegs.OrderBy(x => x.Ratio).FirstOrDefault()?.Side;
                    break;
                case BaseStrategy.CALL_CALENDAR:
                case BaseStrategy.PUT_CALENDAR:
                case BaseStrategy.CALL_DIAGONAL:
                case BaseStrategy.PUT_DIAGONAL:
                case BaseStrategy.CALL_TRIAGONAL:
                case BaseStrategy.PUT_TRIAGONAL:
                    result = complexOrderLegs.OrderByDescending(x => ((Option)x.Security!).Expiration).FirstOrDefault()?.Side;
                    break;
                case BaseStrategy.CALL_CALENDAR_FLY:
                case BaseStrategy.PUT_CALENDAR_FLY:
                case BaseStrategy.CALL_SKEWED_CALENDAR_FLY:
                case BaseStrategy.PUT_SKEWED_CALENDAR_FLY:
                    result = complexOrderLegs.OrderByDescending(x => ((Option)x.Security!).Expiration).FirstOrDefault()?.Side;
                    break;
                case BaseStrategy.REVERSAL:
                case BaseStrategy.CONVERSION:
                    result = complexOrderLegs.FirstOrDefault(x => ((Option)x.Security!).PutCall == PutCall.Put)?.Side;
                    break;
                case BaseStrategy.CALL:
                case BaseStrategy.PUT:
                case BaseStrategy.CALL_STOCK_TIED:
                case BaseStrategy.PUT_STOCK_TIED:
                    result = complexOrderLegs.FirstOrDefault(x => x.Security?.SecurityType == SecurityType.Option)?.Side;
                    break;
            }

            return result;
        }

        public static BaseStrategy ConvertFromString(string baseTypeString)
        {
            BaseStrategy baseStrategy = BaseStrategy.INVALID;
            switch (baseTypeString)
            {
                case "INDEX":
                    baseStrategy = BaseStrategy.INDEX;
                    break;
                case "STOCK":
                    baseStrategy = BaseStrategy.STOCK;
                    break;
                case "INVALID":
                    baseStrategy = BaseStrategy.INVALID;
                    break;
                case "CUSTOM":
                    baseStrategy = BaseStrategy.CUSTOM;
                    break;
                case "CALL COVERED":
                    baseStrategy = BaseStrategy.COVERED_CALL;
                    break;
                case "PUT COVERED":
                    baseStrategy = BaseStrategy.COVERED_PUT;
                    break;
                case "PUT PROTECTIVE":
                    baseStrategy = BaseStrategy.PROTECTIVE_PUT;
                    break;
                case "CALL VERTICAL":
                    baseStrategy = BaseStrategy.CALL_VERTICAL;
                    break;
                case "PUT VERTICAL":
                    baseStrategy = BaseStrategy.PUT_VERTICAL;
                    break;
                case "CALL CALENDAR":
                    baseStrategy = BaseStrategy.CALL_CALENDAR;
                    break;
                case "PUT CALENDAR":
                    baseStrategy = BaseStrategy.PUT_CALENDAR;
                    break;
                case "CALL DIAGONAL":
                    baseStrategy = BaseStrategy.CALL_DIAGONAL;
                    break;
                case "PUT DIAGONAL":
                    baseStrategy = BaseStrategy.PUT_DIAGONAL;
                    break;
                case "CALL BUTTERFLY":
                    baseStrategy = BaseStrategy.CALL_BUTTERFLY;
                    break;
                case "PUT BUTTERFLY":
                    baseStrategy = BaseStrategy.PUT_BUTTERFLY;
                    break;
                case "CALL SKEWED BUTTERFLY":
                    baseStrategy = BaseStrategy.CALL_SKEWED_BUTTERFLY;
                    break;
                case "PUT SKEWED BUTTERFLY":
                    baseStrategy = BaseStrategy.PUT_SKEWED_BUTTERFLY;
                    break;
                case "CALL CALENDAR FLY":
                    baseStrategy = BaseStrategy.CALL_CALENDAR_FLY;
                    break;
                case "PUT CALENDAR FLY":
                    baseStrategy = BaseStrategy.PUT_CALENDAR_FLY;
                    break;
                case "CALL SKEWED CALENDAR FLY":
                    baseStrategy = BaseStrategy.CALL_SKEWED_CALENDAR_FLY;
                    break;
                case "PUT SKEWED CALENDAR FLY":
                    baseStrategy = BaseStrategy.PUT_SKEWED_CALENDAR_FLY;
                    break;
                case "CALL TRIAGONAL":
                    baseStrategy = BaseStrategy.CALL_TRIAGONAL;
                    break;
                case "PUT TRIAGONAL":
                    baseStrategy = BaseStrategy.PUT_TRIAGONAL;
                    break;
                case "CALL 1X2":
                    baseStrategy = BaseStrategy.CALL_1X2;
                    break;
                case "PUT 1X2":
                    baseStrategy = BaseStrategy.PUT_1X2;
                    break;
                case "CALL 1X3":
                    baseStrategy = BaseStrategy.CALL_1X3;
                    break;
                case "PUT 1X3":
                    baseStrategy = BaseStrategy.PUT_1X3;
                    break;
                case "CALL 2X3":
                    baseStrategy = BaseStrategy.CALL_2X3;
                    break;
                case "PUT 2X3":
                    baseStrategy = BaseStrategy.PUT_2X3;
                    break;
                case "CALL CONDOR":
                    baseStrategy = BaseStrategy.CALL_CONDOR;
                    break;
                case "PUT CONDOR":
                    baseStrategy = BaseStrategy.PUT_CONDOR;
                    break;
                case "CALL 1X3X3X1":
                    baseStrategy = BaseStrategy.CALL_1X3X3X1;
                    break;
                case "PUT 1X3X3X1":
                    baseStrategy = BaseStrategy.PUT_1X3X3X1;
                    break;
                case "STRADDLE":
                    baseStrategy = BaseStrategy.STRADDLE;
                    break;
                case "STRANGLE":
                    baseStrategy = BaseStrategy.STRANGLE;
                    break;
                case "CONVERSION":
                    baseStrategy = BaseStrategy.CONVERSION;
                    break;
                case "REVERSAL":
                    baseStrategy = BaseStrategy.REVERSAL;
                    break;
                case "IRON CONDOR":
                    baseStrategy = BaseStrategy.IRON_CONDOR;
                    break;
                case "IRON BUTTERFLY":
                    baseStrategy = BaseStrategy.IRON_BUTTERFLY;
                    break;
                case "CALL":
                    baseStrategy = BaseStrategy.CALL;
                    break;
                case "PUT":
                    baseStrategy = BaseStrategy.PUT;
                    break;
                case "CALL STOCK TIED":
                    baseStrategy = BaseStrategy.CALL_STOCK_TIED;
                    break;
                case "PUT STOCK TIED":
                    baseStrategy = BaseStrategy.PUT_STOCK_TIED;
                    break;
                case "CALL 1X3X2":
                case "CALL ONE THREE TWO":
                    baseStrategy = BaseStrategy.CALL_1x3x2;
                    break;
                case "PUT 1X3X2":
                case "PUT ONE THREE TWO":
                    baseStrategy = BaseStrategy.PUT_1x3x2;
                    break;
                case "CALL 2X3X1":
                case "CALL TWO THREE ONE":
                    baseStrategy = BaseStrategy.CALL_2x3x1;
                    break;
                case "PUT 2X3X1":
                case "PUT TWO THREE ONE":
                    baseStrategy = BaseStrategy.PUT_2x3x1;
                    break;
                case "CALL TREE":
                    baseStrategy = BaseStrategy.CALL_TREE;
                    break;
                case "PUT TREE":
                    baseStrategy = BaseStrategy.PUT_TREE;
                    break;
                case "BOX SPREAD":
                    baseStrategy = BaseStrategy.BOX;
                    break;
                case "SYNTHETIC COLLAR":
                    baseStrategy = BaseStrategy.SYNTHETIC_COLLAR;
                    break;
                case "CALL SPREAD VS PUT":
                    baseStrategy = BaseStrategy.CALL_SPREAD_VS_PUT;
                    break;
                case "PUT SPREAD VS CALL":
                    baseStrategy = BaseStrategy.PUT_SPREAD_VS_CALL;
                    break;
                case "MARRIED STRADDLE":
                    baseStrategy = BaseStrategy.MARRIED_STRADDLE;
                    break;
                case "COVERED STRADDLE":
                    baseStrategy = BaseStrategy.COVERED_STRADDLE;
                    break;
                case "CALL RATIO FLY":
                    baseStrategy = BaseStrategy.CALL_RATIO_FLY;
                    break;
                case "PUT RATIO FLY":
                    baseStrategy = BaseStrategy.PUT_RATIO_FLY;
                    break;
                case "DRAGONFLY":
                    baseStrategy = BaseStrategy.DRAGONFLY;
                    break;
                case "RATIO DRAGONFLY":
                    baseStrategy = BaseStrategy.RATIO_DRAGONFLY;
                    break;
                case "DRAGON":
                    baseStrategy = BaseStrategy.DRAGON;
                    break;
                case "RATIO DRAGON":
                    baseStrategy = BaseStrategy.RATIO_DRAGON;
                    break;
            }

            if (Enum.TryParse(baseTypeString.Replace(" ", "_"), true, out BaseStrategy parsed))
            {
                baseStrategy = parsed;
            }

            return baseStrategy;
        }

        public static string ConvertToString(BaseStrategy baseType)
        {
            string baseStrategy;
            switch (baseType)
            {
                case BaseStrategy.INDEX:
                    baseStrategy = "INDEX";
                    break;
                case BaseStrategy.STOCK:
                    baseStrategy = "STOCK";
                    break;
                case BaseStrategy.INVALID:
                    baseStrategy = "INVALID";
                    break;
                case BaseStrategy.CUSTOM:
                    baseStrategy = "CUSTOM";
                    break;
                case BaseStrategy.COVERED_CALL:
                    baseStrategy = "CALL COVERED";
                    break;
                case BaseStrategy.COVERED_PUT:
                    baseStrategy = "PUT COVERED";
                    break;
                case BaseStrategy.PROTECTIVE_PUT:
                    baseStrategy = "PUT PROTECTIVE";
                    break;
                case BaseStrategy.CALL_VERTICAL:
                    baseStrategy = "CALL VERTICAL";
                    break;
                case BaseStrategy.PUT_VERTICAL:
                    baseStrategy = "PUT VERTICAL";
                    break;
                case BaseStrategy.CALL_CALENDAR:
                    baseStrategy = "CALL CALENDAR";
                    break;
                case BaseStrategy.PUT_CALENDAR:
                    baseStrategy = "PUT CALENDAR";
                    break;
                case BaseStrategy.CALL_DIAGONAL:
                    baseStrategy = "CALL DIAGONAL";
                    break;
                case BaseStrategy.PUT_DIAGONAL:
                    baseStrategy = "PUT DIAGONAL";
                    break;
                case BaseStrategy.CALL_BUTTERFLY:
                    baseStrategy = "CALL BUTTERFLY";
                    break;
                case BaseStrategy.PUT_BUTTERFLY:
                    baseStrategy = "PUT BUTTERFLY";
                    break;
                case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                    baseStrategy = "CALL SKEWED BUTTERFLY";
                    break;
                case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                    baseStrategy = "PUT SKEWED BUTTERFLY";
                    break;
                case BaseStrategy.CALL_CALENDAR_FLY:
                    baseStrategy = "CALL CALENDAR FLY";
                    break;
                case BaseStrategy.PUT_CALENDAR_FLY:
                    baseStrategy = "PUT CALENDAR FLY";
                    break;
                case BaseStrategy.CALL_SKEWED_CALENDAR_FLY:
                    baseStrategy = "CALL SKEWED CALENDAR FLY";
                    break;
                case BaseStrategy.PUT_SKEWED_CALENDAR_FLY:
                    baseStrategy = "PUT SKEWED CALENDAR FLY";
                    break;
                case BaseStrategy.CALL_TRIAGONAL:
                    baseStrategy = "CALL TRIAGONAL";
                    break;
                case BaseStrategy.PUT_TRIAGONAL:
                    baseStrategy = "PUT TRIAGONAL";
                    break;
                case BaseStrategy.CALL_1X2:
                    baseStrategy = "CALL 1X2";
                    break;
                case BaseStrategy.PUT_1X2:
                    baseStrategy = "PUT 1X2";
                    break;
                case BaseStrategy.CALL_1X3:
                    baseStrategy = "CALL 1X3";
                    break;
                case BaseStrategy.PUT_1X3:
                    baseStrategy = "PUT 1X3";
                    break;
                case BaseStrategy.CALL_2X3:
                    baseStrategy = "CALL 2X3";
                    break;
                case BaseStrategy.PUT_2X3:
                    baseStrategy = "PUT 2X3";
                    break;
                case BaseStrategy.CALL_CONDOR:
                    baseStrategy = "CALL CONDOR";
                    break;
                case BaseStrategy.PUT_CONDOR:
                    baseStrategy = "PUT CONDOR";
                    break;
                case BaseStrategy.CALL_1X3X3X1:
                    baseStrategy = "CALL 1X3X3X1";
                    break;
                case BaseStrategy.PUT_1X3X3X1:
                    baseStrategy = "PUT 1X3X3X1";
                    break;
                case BaseStrategy.STRADDLE:
                    baseStrategy = "STRADDLE";
                    break;
                case BaseStrategy.STRANGLE:
                    baseStrategy = "STRANGLE";
                    break;
                case BaseStrategy.CONVERSION:
                    baseStrategy = "CONVERSION";
                    break;
                case BaseStrategy.REVERSAL:
                    baseStrategy = "REVERSAL";
                    break;
                case BaseStrategy.IRON_CONDOR:
                    baseStrategy = "IRON CONDOR";
                    break;
                case BaseStrategy.IRON_BUTTERFLY:
                    baseStrategy = "IRON BUTTERFLY";
                    break;
                case BaseStrategy.GUT_IRON_FLY:
                    baseStrategy = "GUT IRON FLY";
                    break;
                case BaseStrategy.GUT_SKEWED_IRON_FLY:
                    baseStrategy = "GUT SKEWED IRON FLY";
                    break;
                case BaseStrategy.GUT_IRON_CONDOR:
                    baseStrategy = "GUT IRON CONDOR";
                    break;
                case BaseStrategy.GUT_SKEWED_IRON_CONDOR:
                    baseStrategy = "GUT SKEWED IRON CONDOR";
                    break;
                case BaseStrategy.CALL:
                    baseStrategy = "CALL";
                    break;
                case BaseStrategy.PUT:
                    baseStrategy = "PUT";
                    break;
                case BaseStrategy.CALL_STOCK_TIED:
                    baseStrategy = "CALL STOCK TIED";
                    break;
                case BaseStrategy.PUT_STOCK_TIED:
                    baseStrategy = "PUT STOCK TIED";
                    break;
                case BaseStrategy.CALL_1x3x2:
                    baseStrategy = "CALL 1X3X2";
                    break;
                case BaseStrategy.PUT_1x3x2:
                    baseStrategy = "PUT 1X3X2";
                    break;
                case BaseStrategy.CALL_2x3x1:
                    baseStrategy = "CALL 2X3X1";
                    break;
                case BaseStrategy.PUT_2x3x1:
                    baseStrategy = "PUT 2X3X1";
                    break;
                case BaseStrategy.CALL_TREE:
                    baseStrategy = "CALL TREE";
                    break;
                case BaseStrategy.PUT_TREE:
                    baseStrategy = "PUT TREE";
                    break;
                case BaseStrategy.BOX:
                    baseStrategy = "BOX SPREAD";
                    break;
                case BaseStrategy.SYNTHETIC_COLLAR:
                    baseStrategy = "SYNTHETIC COLLAR";
                    break;
                case BaseStrategy.CALL_SPREAD_VS_PUT:
                    baseStrategy = "CALL SPREAD VS PUT";
                    break;
                case BaseStrategy.PUT_SPREAD_VS_CALL:
                    baseStrategy = "PUT SPREAD VS CALL";
                    break;
                case BaseStrategy.MARRIED_STRADDLE:
                    baseStrategy = "MARRIED STRADDLE";
                    break;
                case BaseStrategy.COVERED_STRADDLE:
                    baseStrategy = "COVERED STRADDLE";
                    break;
                case BaseStrategy.CALL_RATIO_FLY:
                    baseStrategy = "CALL RATIO FLY";
                    break;
                case BaseStrategy.PUT_RATIO_FLY:
                    baseStrategy = "PUT RATIO FLY";
                    break;
                case BaseStrategy.DRAGONFLY:
                    baseStrategy = "DRAGONFLY";
                    break;
                case BaseStrategy.RATIO_DRAGONFLY:
                    baseStrategy = "RATIO DRAGONFLY";
                    break;
                case BaseStrategy.DRAGON:
                    baseStrategy = "DRAGON";
                    break;
                case BaseStrategy.RATIO_DRAGON:
                    baseStrategy = "RATIO DRAGON";
                    break;
                default:
                    baseStrategy = baseType.ToString().Replace("_", " ");
                    break;
            }
            return baseStrategy;
        }

        protected static List<SpreadLeg> ParseLegs(string spread)
        {
            List<string> legSymbols = _legSplitRegex.Split(spread).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            int legCount = legSymbols.Count;
            List<SpreadLeg> order = new List<SpreadLeg>();
            if (legCount == 1)
            {
                string legSymbol = legSymbols[0];
                var side = legSymbol.StartsWith("-") ? Side.Sell : Side.Buy;
                int qty = 1;
                if (legSymbol.Contains("*"))
                {
                    string[] parts = legSymbol.Split('*');
                    qty = Math.Abs(Convert.ToInt32(parts[0]));
                    legSymbol = parts[1];
                }
                legSymbol = legSymbol.Replace("+", "").Replace("-", "");
                SpreadLeg leg = new SpreadLeg()
                {
                    Symbol = legSymbol,
                    Quantity = qty,
                    Side = side,
                };
                leg.Security = GetOptionFromSymbol(leg.Symbol);
                order.Add(leg);
            }
            else
            {
                for (int index = 0; index < legCount; index++)
                {
                    string legSymbol = legSymbols[index];
                    var side = legSymbol.StartsWith("-") ? Side.Sell : Side.Buy;
                    int qty = 1;
                    if (legSymbol.Contains("*"))
                    {
                        string[] parts = legSymbol.Split('*');
                        qty = Math.Abs(Convert.ToInt32(parts[0]));
                        legSymbol = parts[1];
                    }
                    legSymbol = legSymbol.Replace("+", "").Replace("-", "");

                    SpreadLeg leg = new SpreadLeg()
                    {
                        Symbol = legSymbol,
                        Quantity = qty,
                        Side = side,
                    };
                    leg.Security = GetOptionFromSymbol(leg.Symbol);
                    order.Add(leg);
                }
            }
            return order;
        }

        protected static Security GetOptionFromSymbol(string symbol)
        {
            Instrument instrument = new Instrument(symbol);
            Security option = new Security()
            {
                Symbol = symbol,
                UnderlyingSymbol = instrument.underlyingSymbol,
                RootSymbol = instrument.rootSymbol,
                Expiration = instrument.expiration,
                Type = instrument.callPut ? PutCall.Put : PutCall.Call,
                Strike = instrument.strike,
            };
            if (!string.IsNullOrEmpty(option.Symbol))
            {
                option.SecurityType = option.Symbol.StartsWith(".") ? SecurityType.Option : SecurityType.Stock;
                option.Multiplier = option.SecurityType == SecurityType.Option ? 100 : 1;
            }
            return option;
        }

        public struct Security
        {
            public SecurityType SecurityType { get; internal set; }
            public string Symbol { get; internal set; }
            public string UnderlyingSymbol { get; internal set; }
            public PutCall Type { get; internal set; }
            public DateTime Expiration { get; internal set; }
            public double Strike { get; internal set; }
            public int Multiplier { get; internal set; }
            public string RootSymbol { get; internal set; }
        }

        public class SpreadLeg
        {
            public Security Security;
            public string Symbol = string.Empty;
            public Side? Side;
            public int Quantity;
        }

        public static bool EvaluateOrder(IOrder order,
            out string baseType,
            out string spreadType,
            out string spreadDescription)
        {
            spreadDescription = spreadType = baseType = "";
            if (order.IsComplexOrder)
            {
                if (order is IComplexOrder complexOrder)
                {
                    return EvaluateLegs(complexOrder.Legs, out baseType, out spreadType, out spreadDescription);
                }

                return false;
            }

            var legSymbol = order.Symbol ?? "";
            var legSecurity = GetOptionFromSymbol(legSymbol);
            var legSide = order.Side;

            return EvaluateSingleLeg(legSymbol, legSecurity, legSide, out baseType, out spreadType, out spreadDescription);
        }

        public static bool EvaluateLegs(IEnumerable<IComplexOrderLeg> complexOrderLegs,
            out string baseType,
            out string spreadType,
            out string spreadDescription)
        {
            spreadDescription = spreadType = baseType = "";

            List<SpreadLeg> legs = new List<SpreadLeg>();

            foreach (var leg in complexOrderLegs)
            {
                if (leg.Symbol == null)
                {
                    return false;
                }

                SpreadLeg spreadLeg = new SpreadLeg
                {
                    Security = GetOptionFromSymbol(leg.Symbol),
                    Symbol = leg.Symbol,
                    Side = leg.Side,
                    Quantity = leg.Ratio
                };
                legs.Add(spreadLeg);
            }

            return EvaluateLegs(legs, out baseType, out spreadType, out spreadDescription);
        }

        public static bool EvaluateLegs(List<SpreadLeg> legs,
            out string baseType,
            out string spreadType,
            out string spreadDescription)
        {

            if (legs.Count == 1)
            {
                SpreadLeg leg = legs[0];
                var legSymbol = leg.Symbol;
                var legSecurity = leg.Security;
                var legSide = leg.Side;

                return EvaluateSingleLeg(legSymbol, legSecurity, legSide, out baseType, out spreadType, out spreadDescription);
            }
            else if (legs.Count == 2)
            {
                spreadDescription = spreadType = baseType = "";

                SpreadLeg[] legsOrderdByQty = legs.OrderBy(x => x.Quantity).ToArray();
                SpreadLeg leg1 = legsOrderdByQty[0];
                SpreadLeg leg2 = legsOrderdByQty[1];
                if (leg1.Security.SecurityType == SecurityType.Stock || leg2.Security.SecurityType == SecurityType.Stock)
                {
                    SpreadLeg? stockLeg = legs.FirstOrDefault(x => x.Security.SecurityType == SecurityType.Stock);
                    SpreadLeg? optionLeg = legs.FirstOrDefault(x => x.Security.SecurityType == SecurityType.Option);
                    if (optionLeg == null)
                    {
                        spreadDescription = spreadType = baseType = "INVALID";
                        return true;
                    }

                    if (stockLeg != null && stockLeg.Symbol == optionLeg.Security.UnderlyingSymbol)
                    {
                        string type = optionLeg.Security.Type == PutCall.Call ? "CALL" : "PUT";
                        if (((stockLeg.Side == Side.Buy && optionLeg.Side == Side.Sell) || (stockLeg.Side == Side.Sell && optionLeg.Side == Side.Buy))
                            && stockLeg.Quantity == optionLeg.Quantity * optionLeg.Security.Multiplier)
                        {
                            baseType = type + " COVERED";
                            spreadType = $"COVERED " + stockLeg.Symbol + " " + optionLeg.Security.Expiration.ToString("MMM-dd-yy") + " " + optionLeg.Security.Strike + " " + type + "/" + stockLeg.Symbol;
                            spreadDescription = $"COVERED " + stockLeg.Symbol + " " + optionLeg.Security.Expiration.ToString("MMM-dd-yy") + " " + optionLeg.Security.Strike + " " + type + "/" + stockLeg.Symbol;
                            return true;
                        }

                        if (optionLeg.Security.Type == PutCall.Put && stockLeg.Side == Side.Buy && optionLeg.Side == Side.Buy && stockLeg.Quantity == optionLeg.Quantity * optionLeg.Security.Multiplier)
                        {
                            baseType = "PUT PROTECTIVE";
                            spreadType = $"PROTECTIVE PUT " + stockLeg.Symbol + " " + optionLeg.Security.Expiration.ToString("MMM-dd-yy") + " " + optionLeg.Security.Strike + " PUT/" + stockLeg.Symbol;
                            spreadDescription = $"PROTECTIVE PUT " + stockLeg.Symbol + " " + optionLeg.Security.Expiration.ToString("MMM-dd-yy") + " " + optionLeg.Security.Strike + " PUT/" + stockLeg.Symbol;
                            return true;
                        }
                    }
                }
                else if (leg1.Security.SecurityType == SecurityType.Option && leg2.Security.SecurityType == SecurityType.Option)
                {
                    List<int> lcdAdjustedList = MathHelper.GetLCDAdjustedList(legs.Select(x => x.Quantity).OrderBy(x => x).ToList(), out _);
                    int leg1Qty = lcdAdjustedList.Count == 2 ? lcdAdjustedList[0] : leg1.Quantity;
                    int leg2Qty = lcdAdjustedList.Count == 2 ? lcdAdjustedList[1] : leg2.Quantity;
                    if (leg1.Security.Expiration == leg2.Security.Expiration)
                    {
                        var min = Math.Min(leg1Qty, leg2Qty);
                        var max = Math.Max(leg1Qty, leg2Qty);
                        if (leg1.Security.Type == PutCall.Call && leg2.Security.Type == PutCall.Call)
                        {
                            if (leg1.Security.Strike == leg2.Security.Strike && leg1.Security.Symbol == leg2.Security.Symbol)
                            {
                                baseType = "INVALID";
                                spreadType = "INVALID";
                                spreadDescription = "INVALID";
                                return true;
                            }

                            if (leg1.Quantity == leg2.Quantity)
                            {
                                if ((leg1.Side == Side.Buy && leg2.Side == Side.Sell && leg1.Security.Strike < leg2.Security.Strike) || (leg1.Side == Side.Sell && leg2.Side == Side.Buy && leg1.Security.Strike > leg2.Security.Strike))
                                {
                                    spreadDescription = "BULL CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                }
                                else if ((leg1.Side == Side.Buy && leg2.Side == Side.Sell && leg1.Security.Strike > leg2.Security.Strike) || (leg1.Side == Side.Sell && leg2.Side == Side.Buy && leg1.Security.Strike < leg2.Security.Strike))
                                {
                                    spreadDescription = "BEAR CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                }
                            }
                            else if (leg1.Side != leg2.Side)
                            {
                                if (min is 1 or 2 && max is 2 or 3)
                                {
                                    if (legs.FirstOrDefault(x => x.Side == Side.Buy)?.Quantity > legs.FirstOrDefault(x => x.Side == Side.Sell)?.Quantity)
                                    {
                                        spreadDescription = "LONG RATIO CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    }
                                    else
                                    {
                                        spreadDescription = "SHORT RATIO CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    }
                                }
                                else
                                {
                                    if (legs.FirstOrDefault(x => x.Side == Side.Buy)?.Quantity > legs.FirstOrDefault(x => x.Side == Side.Sell)?.Quantity)
                                    {
                                        spreadDescription = $"LONG {min}X{max} RATIO CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    }
                                    else
                                    {
                                        spreadDescription = $"SHORT {min}X{max} RATIO CALL SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(spreadDescription))
                            {
                                if (leg1Qty == leg2Qty)
                                {
                                    baseType = "CALL VERTICAL";
                                    spreadType = $"CALL VERTICAL " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    return true;
                                }
                                else
                                {
                                    baseType = "CALL " + leg1Qty + "X" + leg2Qty;
                                    spreadType = baseType + " " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + "/" + leg2.Security.Strike;
                                    return true;
                                }
                            }
                        }
                        else if (leg1.Security.Type == PutCall.Put && leg2.Security.Type == PutCall.Put)
                        {
                            if (leg1.Security.Strike == leg2.Security.Strike && leg1.Security.Symbol == leg2.Security.Symbol)
                            {
                                baseType = "INVALID";
                                spreadType = "INVALID";
                                spreadDescription = "INVALID";
                                return true;
                            }


                            if (leg1.Quantity == leg2.Quantity)
                            {
                                if ((leg1.Side == Side.Sell && leg2.Side == Side.Buy && leg1.Security.Strike > leg2.Security.Strike) || (leg1.Side == Side.Buy && leg2.Side == Side.Sell && leg1.Security.Strike < leg2.Security.Strike))
                                {
                                    spreadDescription = "BULL PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                }
                                if ((leg1.Side == Side.Sell && leg2.Side == Side.Buy && leg1.Security.Strike < leg2.Security.Strike) || (leg1.Side == Side.Buy && leg2.Side == Side.Sell && leg1.Security.Strike > leg2.Security.Strike))
                                {
                                    spreadDescription = "BEAR PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                }
                            }
                            else if (leg1.Side != leg2.Side)
                            {
                                if (min is 1 or 2 && max is 2 or 3)
                                {
                                    if (legs.FirstOrDefault(x => x.Side == Side.Buy)?.Quantity > legs.FirstOrDefault(x => x.Side == Side.Sell)?.Quantity)
                                    {
                                        spreadDescription = "LONG RATIO PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                    }
                                    else
                                    {
                                        spreadDescription = "SHORT RATIO PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                    }
                                }
                                else
                                {
                                    if (legs.FirstOrDefault(x => x.Side == Side.Buy)?.Quantity > legs.FirstOrDefault(x => x.Side == Side.Sell)?.Quantity)
                                    {
                                        spreadDescription = $"LONG {min}X{max} RATIO PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                    }
                                    else
                                    {
                                        spreadDescription = $"SHORT {min}X{max} RATIO PUT SPREAD " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg2.Security.Strike : leg1.Security.Strike);
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(spreadDescription))
                            {
                                if (leg1Qty == leg2Qty)
                                {
                                    baseType = "PUT VERTICAL";
                                    spreadType = $"PUT VERTICAL " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1.Security.Strike < leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1.Security.Strike > leg2.Security.Strike ? leg1.Security.Strike : leg2.Security.Strike);
                                    return true;
                                }
                                else
                                {
                                    baseType = $"PUT " + leg1Qty + "X" + leg2Qty;
                                    spreadType = baseType + " " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + (leg1Qty == 1 ? leg1.Security.Strike : leg2.Security.Strike) + "/" + (leg1Qty == 2 ? leg1.Security.Strike : leg2.Security.Strike);
                                    return true;
                                }
                            }
                        }
                        else if (leg1.Security.Type != leg2.Security.Type)
                        {
                            if (leg1.Side == Side.Buy && leg2.Side == Side.Buy && leg1.Security.Strike == leg2.Security.Strike)
                            {
                                baseType = "STRADDLE";
                                spreadDescription = $"LONG STRADDLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike;
                                spreadType = $"STRADDLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike;
                                return true;
                            }

                            if (leg1.Side == Side.Sell && leg2.Side == Side.Sell && leg1.Security.Strike == leg2.Security.Strike)
                            {
                                baseType = "STRADDLE";
                                spreadDescription = $"SHORT STRADDLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike;
                                spreadType = $"STRADDLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike;
                                return true;
                            }

                            if (leg1.Side == Side.Buy && leg2.Side == Side.Buy)
                            {
                                if ((leg1.Security.Type == PutCall.Call && leg2.Security.Type == PutCall.Put && leg1.Security.Strike > leg2.Security.Strike) || (leg1.Security.Type == PutCall.Put && leg2.Security.Type == PutCall.Call && leg1.Security.Strike < leg2.Security.Strike))
                                {
                                    if (leg1.Security.Type == PutCall.Call)
                                    {
                                        baseType = "STRANGLE";
                                        spreadDescription = $"LONG STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + "/" + leg2.Security.Strike + "CALL / PUT";
                                        spreadType = $"STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + "/" + leg2.Security.Strike + "CALL / PUT";
                                        return true;
                                    }
                                    else
                                    {
                                        baseType = "STRANGLE";
                                        spreadDescription = $"LONG STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg2.Security.Strike + "/" + leg1.Security.Strike + "CALL / PUT";
                                        spreadType = $"STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg2.Security.Strike + "/" + leg1.Security.Strike + "CALL / PUT";
                                        return true;
                                    }
                                }
                            }
                            else if (leg1.Side == Side.Sell && leg2.Side == Side.Sell && ((leg1.Security.Type == PutCall.Call && leg2.Security.Type == PutCall.Put && leg1.Security.Strike > leg2.Security.Strike) || (leg1.Security.Type == PutCall.Put && leg2.Security.Type == PutCall.Call && leg1.Security.Strike < leg2.Security.Strike)))
                            {
                                if (leg1.Security.Type == PutCall.Call)
                                {
                                    baseType = "STRANGLE";
                                    spreadDescription = $"SHORT STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + "/" + leg2.Security.Strike + "CALL / PUT";
                                    spreadType = $"STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + "/" + leg2.Security.Strike + "CALL / PUT";
                                    return true;
                                }
                                else
                                {
                                    baseType = "STRANGLE";
                                    spreadDescription = $"SHORT STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg2.Security.Strike + "/" + leg1.Security.Strike + "CALL / PUT";
                                    spreadType = $"STRANGLE " + GetRootSymbols(legs) + " " + leg1.Security.Expiration.ToString("MMM-dd-yy") + " " + leg2.Security.Strike + "/" + leg1.Security.Strike + "CALL / PUT";
                                    return true;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (leg1.Security.Type == leg2.Security.Type && leg1.Security.Strike == leg2.Security.Strike && leg1.Side != leg2.Side)
                        {
                            string type = leg1.Security.Type == PutCall.Call ? "CALL" : "PUT";
                            Security opt1 = leg1.Security.Expiration < leg2.Security.Expiration ? leg1.Security : leg2.Security;
                            Security opt2 = leg1.Security.Expiration > leg2.Security.Expiration ? leg1.Security : leg2.Security;
                            baseType = type + " CALENDAR";
                            if (leg1Qty == leg2Qty)
                            {
                                spreadDescription = spreadType = $"CALENDAR " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + "/" + opt2.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + " " + (leg1.Security.Type == PutCall.Call ? "CALL" : "PUT");
                            }
                            else
                            {
                                if (leg1Qty == 1 && leg2Qty == 2)
                                {
                                    spreadDescription = spreadType = "1X2 CALENDAR " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + " " + type;
                                }
                                else if (leg1Qty == 1 && leg2Qty == 3)
                                {
                                    spreadDescription = spreadType = "1X3 CALENDAR " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + " " + type;
                                }
                                else
                                {
                                    spreadDescription = spreadType = "RATIO CALENDAR " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + leg1.Security.Strike + " " + type;
                                }
                            }
                            return true;
                        }
                        if (leg1.Security.Type == leg2.Security.Type && leg1.Security.Strike != leg2.Security.Strike && leg1.Side != leg2.Side)
                        {
                            string type = leg1.Security.Type == PutCall.Call ? "CALL" : "PUT";
                            Security opt1 = leg1.Security.Expiration < leg2.Security.Expiration ? leg1.Security : leg2.Security;
                            Security opt2 = leg1.Security.Expiration > leg2.Security.Expiration ? leg1.Security : leg2.Security;
                            double spacing = Math.Round(Math.Abs(opt1.Strike - opt2.Strike), 2);
                            baseType = type + " DIAGONAL";
                            if (leg1Qty == leg2Qty)
                            {
                                spreadDescription = spreadType = "DIAGONAL " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + "/" + opt2.Expiration.ToString("MMM-dd-yy") + " " + opt1.Strike + "/" + opt2.Strike + " " + (leg1.Security.Type == PutCall.Call ? "CALL" : "PUT") + "[" + spacing + "]";
                            }
                            else
                            {
                                if (leg1Qty == 1 && leg2Qty == 2)
                                {
                                    spreadDescription = spreadType = "1X2 DIAGONAL " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + opt1.Strike + " / " + opt2.Strike + " " + type + "[" + spacing + "]";
                                }
                                else if (leg1Qty == 1 && leg2Qty == 3)
                                {
                                    spreadDescription = spreadType = "1X3 DIAGONAL " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + opt1.Strike + " / " + opt2.Strike + " " + type + "[" + spacing + "]";
                                }
                                else
                                {
                                    spreadDescription = spreadType = "RATIO DIAGONAL " + GetRootSymbols(legs) + " " + opt1.Expiration.ToString("MMM-dd-yy") + " / " + opt2.Expiration.ToString("MMM-dd-yy") + " " + opt1.Strike + " / " + opt2.Strike + " " + type + "[" + spacing + "]";
                                }
                            }
                            return true;
                        }
                    }
                }
            }
            else if (legs.Count == 3)
            {
                if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 3 && (legs.Count(x => x.Security.Type == PutCall.Call) == 3 || legs.Count(x => x.Security.Type == PutCall.Put) == 3))
                {
                    List<SpreadLeg> opts = legs.OrderBy(x => x.Security.Strike).ToList();
                    Security leg1 = opts[0].Security;
                    Security leg2 = opts[1].Security;
                    Security leg3 = opts[2].Security;
                    string type = leg1.Type == PutCall.Call ? "CALL" : "PUT";
                    double spacing1 = Math.Round(Math.Abs(leg1.Strike - leg2.Strike), 2);
                    double spacing2 = Math.Round(Math.Abs(leg2.Strike - leg3.Strike), 2);
                    var sameExpiration = leg1.Expiration == leg2.Expiration && leg2.Expiration == leg3.Expiration;
                    var allDiffStrike = leg1.Strike != leg2.Strike && leg2.Strike != leg3.Strike;
                    if (sameExpiration && allDiffStrike && spacing1 == spacing2 && opts[0].Quantity == opts[2].Quantity && opts[1].Quantity == opts[0].Quantity * 2)
                    {
                        baseType = type + " BUTTERFLY";
                        spreadDescription = spreadType = "BUTTERFLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "]";
                        return true;
                    }

                    if (sameExpiration && allDiffStrike && spacing1 != spacing2 && opts[0].Quantity == opts[2].Quantity && opts[1].Quantity == opts[0].Quantity * 2)
                    {
                        baseType = type + " SKEWED BUTTERFLY";
                        spreadDescription = spreadType = "SKEWED BUTTERFLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                        return true;
                    }

                    if (sameExpiration && Math.Abs(leg1.Strike - leg2.Strike) > STRIKE_TOLERANCE && Math.Abs(leg2.Strike - leg3.Strike) > STRIKE_TOLERANCE)
                    {
                        var ordByRatio = legs.OrderBy(x => x.Quantity).ToList();
                        double spacing1X3 = Math.Round(Math.Abs(ordByRatio[0].Security.Strike - ordByRatio[2].Security.Strike), 2);
                        double spacing3X2 = Math.Round(Math.Abs(ordByRatio[1].Security.Strike - ordByRatio[2].Security.Strike), 2);
                        if (Math.Abs(spacing1X3 - 2 * spacing3X2) < STRIKE_TOLERANCE)
                        {
                            if (opts[0].Quantity == 1 && opts[1].Quantity == 3 && opts[2].Quantity == 2)
                            {
                                if (leg1.Type == PutCall.Call)
                                {
                                    baseType = type + " ONE THREE TWO";
                                    spreadDescription = spreadType = "ONE THREE TWO " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                                    return true;
                                }
                                if (leg1.Type == PutCall.Put)
                                {
                                    baseType = type + " TWO THREE ONE";
                                    spreadDescription = spreadType = "TWO THREE ONE " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                                    return true;
                                }
                            }

                            if (opts[0].Quantity == 2 && opts[1].Quantity == 3 && opts[2].Quantity == 1)
                            {
                                if (leg1.Type == PutCall.Call)
                                {
                                    baseType = type + " TWO THREE ONE";
                                    spreadDescription = spreadType = "TWO THREE ONE " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                                    return true;
                                }
                                if (leg1.Type == PutCall.Put)
                                {
                                    baseType = type + " ONE THREE TWO";
                                    spreadDescription = spreadType = "ONE THREE TWO " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                                    return true;
                                }
                            }
                        }
                    }

                    if (sameExpiration && allDiffStrike)
                    {
                        if (spacing1 == spacing2 && (opts[0].Quantity == 1 || opts[0].Quantity == 2) && (opts[2].Quantity == 1 || opts[2].Quantity == 2) && opts[1].Quantity == 3)
                        {
                            baseType = type + " RATIO FLY";
                            spreadDescription = spreadType = $"RATIO FLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "]";
                            return true;
                        }

                        if (opts[0].Quantity == 1 && opts[1].Quantity == 1 && opts[2].Quantity == 1 && ((opts[0].Side != opts[1].Side && opts[0].Side != opts[2].Side) || (opts[0].Side != opts[2].Side && opts[1].Side != opts[2].Side)))
                        {
                            baseType = type + " TREE";
                            spreadDescription = spreadType = $"TREE " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "]";
                            return true;
                        }
                    }

                    opts = legs.OrderBy(x => x.Security.Expiration).ToList();

                    if (leg1.Strike == leg2.Strike && leg2.Strike == leg3.Strike && leg1.Expiration != leg2.Expiration && leg2.Expiration != leg3.Expiration && opts[0].Quantity == opts[2].Quantity && opts[1].Quantity == opts[0].Quantity * 2 && opts[0].Side == opts[2].Side && opts[0].Side != opts[1].Side)
                    {
                        baseType = type + " CALENDAR FLY";
                        spreadDescription = spreadType = $"CALENDAR FLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + "/" + leg2.Expiration.ToString("MMM-dd-yy") + "/" + leg3.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + " " + type;
                        return true;
                    }

                    if (leg1.Expiration != leg2.Expiration && leg2.Expiration != leg3.Expiration && leg1.Strike < leg2.Strike && leg2.Strike < leg3.Strike && opts[0].Quantity == opts[2].Quantity && opts[1].Quantity == opts[0].Quantity * 2)
                    {
                        baseType = type + " SKEWED CALENDAR FLY";
                        spreadDescription = spreadType = $"SKEWED CALENDAR FLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + "/" + leg2.Expiration.ToString("MMM-dd-yy") + "/" + leg3.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "]";
                        return true;
                    }

                    if (legs.Select(x => x.Security.Expiration).Distinct().Count() == 2)
                    {
                        if (legs.Select(x => x.Security.Strike).Distinct().Count() == 3)
                        {
                            opts = legs.OrderBy(x => x.Security.Strike).ThenBy(x => x.Quantity).ToList();
                            if (opts[0].Quantity == opts[1].Quantity && opts[0].Quantity + opts[1].Quantity == opts[2].Quantity)
                            {
                                if (leg1.Expiration == leg2.Expiration && opts[0].Side == opts[1].Side && opts[1].Side != opts[2].Side)
                                {
                                    baseType = type + " TRIAGONAL";
                                    spreadDescription = spreadType = $"TRIAGONAL " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + "/" + leg3.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type;
                                    return true;
                                }
                            }
                        }
                    }

                    if (legs.Select(x => x.Security.Expiration).Distinct().Count() == 1)
                    {
                        if (legs.Select(x => x.Security.Strike).Distinct().Count() == 3 && legs.Select(x => x.Quantity).Distinct().Count() == 2)
                        {
                            SpreadLeg[] strikes = legs.OrderByDescending(x => x.Security.Strike).ToArray();
                            if (strikes[0].Quantity == 2 && strikes[2].Quantity == 1 && strikes[0].Side != strikes[1].Side && strikes[1].Side == strikes[2].Side)
                            {
                                baseType = type + " TREE";
                                spreadDescription = spreadType = $"TREE " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + type;
                                return true;
                            }
                        }
                    }
                }
                else if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 3 && legs.Count(x => x.Security.Type == PutCall.Call) == 2 && legs.Count(x => x.Security.Type == PutCall.Put) == 1)
                {
                    List<SpreadLeg> opts = legs.OrderBy(x => (x.Security.Strike, x.Security.Type)).ToList();
                    Security leg1 = opts[0].Security; // Lower strike PUT
                    Security leg2 = opts[1].Security; // Lower strike CALL  
                    Security leg3 = opts[2].Security; // Higher strike CALL

                    var sameExpiration = leg1.Expiration == leg2.Expiration && leg2.Expiration == leg3.Expiration;
                    var straddleStrike = leg1.Strike == leg2.Strike;
                    var upperCallStrike = leg3.Strike > leg1.Strike;
                    var sameQuantity = Math.Abs(opts[0].Quantity) == Math.Abs(opts[1].Quantity) && Math.Abs(opts[1].Quantity) == Math.Abs(opts[2].Quantity);
                    if (sameExpiration && straddleStrike && upperCallStrike && sameQuantity && opts[0].Side == opts[1].Side && opts[2].Side != opts[0].Side)
                    {
                        double spacing = Math.Round(Math.Abs(leg3.Strike - leg1.Strike), 2);
                        baseType = "COVERED STRADDLE";
                        spreadDescription = spreadType = "COVERED STRADDLE  " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg3.Strike + " [" + spacing + "]";
                        return true;
                    }
                    if (sameExpiration && leg1.Strike != leg2.Strike && leg2.Strike != leg3.Strike && leg1.Strike != leg3.Strike && leg1.Strike < leg2.Strike && leg2.Strike < leg3.Strike && sameQuantity && opts[0].Side == opts[2].Side && opts[1].Side != opts[0].Side)
                    {
                        double spacing = Math.Round(Math.Abs(leg3.Strike - leg1.Strike), 2);
                        baseType = "CALL SPREAD VS PUT";
                        spreadDescription = spreadType = "CALL SPREAD VS PUT " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + " [" + spacing + "]";
                        return true;
                    }
                }
                else if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 3 && legs.Count(x => x.Security.Type == PutCall.Put) == 2 && legs.Count(x => x.Security.Type == PutCall.Call) == 1)
                {
                    List<SpreadLeg> opts = legs.OrderBy(x => (x.Security.Strike, x.Security.Type)).ToList();
                    Security leg1 = opts[0].Security; // Lower strike PUT
                    Security leg2 = opts[1].Security; // Higher strike PUT  
                    Security leg3 = opts[2].Security; // Higher strike CALL

                    var sameExpiration = leg1.Expiration == leg2.Expiration && leg2.Expiration == leg3.Expiration;
                    var sameQuantity = Math.Abs(opts[0].Quantity) == Math.Abs(opts[1].Quantity) && Math.Abs(opts[1].Quantity) == Math.Abs(opts[2].Quantity);
                    if (sameExpiration && leg2.Strike == leg3.Strike && leg1.Strike < leg2.Strike && sameQuantity && opts[1].Side == opts[2].Side && opts[0].Side != opts[1].Side)
                    {
                        double spacing = Math.Round(Math.Abs(leg3.Strike - leg1.Strike), 2);
                        baseType = "MARRIED STRADDLE";
                        spreadDescription = spreadType = "MARRIED STRADDLE " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + " [" + spacing + "]";
                        return true;
                    }
                    if (sameExpiration && leg1.Strike != leg2.Strike && leg2.Strike != leg3.Strike && leg1.Strike != leg3.Strike && leg1.Strike < leg2.Strike && leg2.Strike < leg3.Strike && sameQuantity && opts[0].Side == opts[2].Side && opts[1].Side != opts[0].Side)
                    {
                        double spacing1 = Math.Round(Math.Abs(leg2.Strike - leg1.Strike), 2);
                        double spacing2 = Math.Round(Math.Abs(leg3.Strike - leg2.Strike), 2);
                        string spacingString = spacing1 == spacing2 ? $"[{spacing1}]" : $"[{spacing1}/{spacing2}]";
                        baseType = "PUT SPREAD VS CALL";
                        spreadDescription = spreadType = "PUT SPREAD VS CALL " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + leg1.Strike + "/" + leg2.Strike + "/" + leg3.Strike + " " + spacingString;
                        return true;
                    }
                }
                else if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 2 && legs.Count(x => x.Security.SecurityType == SecurityType.Stock) == 1)
                {
                    SpreadLeg? stockLeg = legs.FirstOrDefault(x => x.Security.SecurityType == SecurityType.Stock);
                    List<SpreadLeg> opts = legs.Where(x => x.Security.SecurityType == SecurityType.Option).ToList();
                    SpreadLeg? optionLeg1 = legs.FirstOrDefault(x => x.Symbol == opts[0].Symbol);
                    SpreadLeg? optionLeg2 = legs.FirstOrDefault(x => x.Symbol == opts[1].Symbol);
                    if (opts[0].Security.Expiration == opts[1].Security.Expiration && opts[0].Security.Strike == opts[1].Security.Strike && opts[0].Security.Type != opts[1].Security.Type)
                    {
                        SpreadLeg? complexOrderLeg4 = optionLeg1?.Security.Type == PutCall.Call ? optionLeg1 : optionLeg2;
                        SpreadLeg? complexOrderLeg5 = optionLeg1?.Security.Type == PutCall.Call ? optionLeg2 : optionLeg1;
                        if (complexOrderLeg4?.Quantity == complexOrderLeg5?.Quantity)
                        {
                            if (complexOrderLeg4?.Side == Side.Sell && complexOrderLeg5?.Side == Side.Buy && stockLeg?.Side == Side.Buy)
                            {
                                baseType = "CONVERSION";
                                spreadDescription = spreadType = $"CONVERSION " + stockLeg.Symbol + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike;
                                return true;
                            }



                            if (complexOrderLeg4?.Side == Side.Buy && complexOrderLeg5?.Side == Side.Sell && stockLeg?.Side == Side.Sell)
                            {
                                baseType = "REVERSAL";
                                spreadDescription = spreadType = $"REVERSAL " + stockLeg.Symbol + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike;
                                return true;
                            }
                        }
                    }
                }
            }
            else if (legs.Count == 4)
            {
                if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 4 && (legs.Count(x => x.Security.Type == PutCall.Call) == 4 || legs.Count(x => x.Security.Type == PutCall.Put) == 4))
                {
                    List<SpreadLeg> opts = legs.OrderBy(x => x.Security.Strike).ToList();
                    string type = opts[0].Security.Type == PutCall.Call ? "CALL" : "PUT";
                    if (opts.Select(x => x.Security.Strike).Distinct().Count() == 4 && opts.Select(x => x.Security.Expiration).Distinct().Count() == 1 && legs.Select(x => x.Quantity).Distinct().Count() == 1)
                    {
                        if ((opts[0].Side == Side.Buy && opts[1].Side == Side.Sell && opts[2].Side == Side.Sell && opts[3].Side == Side.Buy) || (opts[0].Side == Side.Sell && opts[1].Side == Side.Buy && opts[2].Side == Side.Buy && opts[3].Side == Side.Sell))
                        {
                            baseType = type + " CONDOR";
                            spreadDescription = spreadType = $"CONDOR " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[2].Security.Strike + "/" + opts[3].Security.Strike + " " + type;
                            return true;
                        }
                    }

                    if (opts.Select(x => x.Security.Strike).Distinct().Count() == 4 &&
                        opts.Select(x => x.Security.Expiration).Distinct().Count() == 1 &&
                        legs.Select(x => x.Quantity).Distinct().Count() == 2 &&
                        legs.Select(x => x.Side).Distinct().Count() == 2 &&
                        opts[0].Side == opts[2].Side &&
                        opts[1].Side == opts[3].Side &&
                        opts[0].Side != opts[1].Side)
                    {
                        double spacing1 = Math.Round(Math.Abs(opts[0].Security.Strike - opts[1].Security.Strike), 2);
                        double spacing2 = Math.Round(Math.Abs(opts[1].Security.Strike - opts[2].Security.Strike), 2);
                        double spacing3 = Math.Round(Math.Abs(opts[2].Security.Strike - opts[3].Security.Strike), 2);

                        if (spacing1 == spacing2 &&
                            spacing2 == spacing3 &&
                            opts[0].Quantity == 1 &&
                            opts[1].Quantity == 3 &&
                            opts[2].Quantity == 3 &&
                            opts[3].Quantity == 1)
                        {
                            baseType = type + " 1X3X3X1";
                            spreadDescription = spreadType = $"1X3X3X1 " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[2].Security.Strike + "/" + opts[3].Security.Strike + " " + type + "[" + spacing1 + "/" + spacing2 + "/" + spacing3 + "]";
                            return true;
                        }
                    }

                    var ratios = legs.Select(x => x.Quantity).Distinct().ToList();
                    if (ratios.Count == 2)
                    {
                        opts = legs.OrderByDescending(x => x.Quantity).ToList();
                        var strikes = opts.Select(x => x.Security.Strike).Distinct().ToList();
                        if (strikes.Count == 3)
                        {
                            var expirations = opts.Select(x => x.Security.Expiration).Distinct().ToList();
                            if (expirations.Count == 2 &&
                                legs.Select(x => x.Side).Distinct().Count() == 2)
                            {
                                if (opts[0].Security.Expiration == opts[1].Security.Expiration)
                                {
                                    baseType = type + " RATIO DIAGONAL";
                                    spreadDescription = spreadType = "RATIO DIAGONAL " + GetRootSymbols(legs) + " " + expirations[0].ToString("MMM-dd-yy") + "/" + expirations[1].ToString("MMM-dd-yy") + " " + strikes[0] + "*/" + strikes[1] + "/" + strikes[2] + " " + type + "[" + string.Join(":", ratios) + "]";
                                    return true;
                                }
                            }
                        }
                    }
                }

                if (legs.Count(x => x.Security.SecurityType == SecurityType.Option) == 4 && legs.Count(x => x.Security.Type == PutCall.Call) == 2 && legs.Count(x => x.Security.Type == PutCall.Put) == 2)
                {
                    List<SpreadLeg> opts = legs.OrderBy(x => (x.Security.Strike, x.Security.Type)).ToList();

                    if (opts.Select(x => x.Security.Strike).Distinct().Count() == 2 && opts.Select(x => x.Security.Expiration).Distinct().Count() == 1 && legs.Select(x => x.Quantity).Distinct().Count() == 1)
                    {
                        if ((opts[1].Side == Side.Buy && opts[3].Side == Side.Sell && opts[2].Side == Side.Buy && opts[0].Side == Side.Sell) || (opts[1].Side == Side.Sell && opts[3].Side == Side.Buy && opts[2].Side == Side.Sell && opts[0].Side == Side.Buy))
                        {
                            baseType = "BOX SPREAD";
                            spreadDescription = spreadType = $"BOX " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[3].Security.Strike;
                            return true;
                        }
                    }

                    if (opts.Select(x => x.Security.Strike).Distinct().Count() == 4 && opts.Select(x => x.Security.Expiration).Distinct().Count() == 1 && legs.Select(x => x.Quantity).Distinct().Count() == 1)
                    {
                        if (opts[0].Security.Type == opts[1].Security.Type && opts[2].Security.Type == opts[3].Security.Type && opts[0].Security.Type != opts[3].Security.Type && opts[0].Side == opts[3].Side && opts[1].Side == opts[2].Side && opts[0].Side != opts[1].Side)
                        {
                            string type = opts[0].Security.Type == PutCall.Call ? "CALL" : "PUT";
                            string type2 = opts[2].Security.Type == PutCall.Call ? "CALL" : "PUT";

                            // Check for Gut Iron Condor (Call/Call/Put/Put pattern at 4 different strikes)
                            if (opts[0].Security.Type == PutCall.Call && opts[3].Security.Type == PutCall.Put)
                            {
                                double spacing1 = Math.Round(Math.Abs(opts[1].Security.Strike - opts[0].Security.Strike), 2);
                                double spacing2 = Math.Round(Math.Abs(opts[3].Security.Strike - opts[2].Security.Strike), 2);
                                if (spacing1 == spacing2)
                                {
                                    baseType = "GUT IRON CONDOR";
                                    spreadDescription = spreadType = $"GUT IRON CONDOR " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[2].Security.Strike + "/" + opts[3].Security.Strike + " " + type + "/" + type2;
                                }
                                else
                                {
                                    baseType = "GUT SKEWED IRON CONDOR";
                                    spreadDescription = spreadType = $"GUT SKEWED IRON CONDOR " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[2].Security.Strike + "/" + opts[3].Security.Strike + " " + type + "/" + type2;
                                }
                                return true;
                            }

                            // Regular Iron Condor (Put/Put/Call/Call pattern)
                            baseType = "IRON CONDOR";
                            spreadDescription = spreadType = $"IRON CONDOR " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[2].Security.Strike + "/" + opts[3].Security.Strike + " " + type + "/" + type2;
                            return true;
                        }
                    }

                    if (opts[0].Security.Type != opts[1].Security.Type)
                    {
                        (opts[1], opts[2]) = (opts[2], opts[1]);
                    }
                    if (opts.Select(x => x.Security.Strike).Distinct().Count() == 3 && opts.Select(x => x.Security.Expiration).Distinct().Count() == 1 && legs.Select(x => x.Quantity).Distinct().Count() == 1)
                    {
                        if (opts[1].Security.Strike == opts[2].Security.Strike && opts[1].Security.Type != opts[2].Security.Type)
                        {
                            double spacing1 = Math.Round(Math.Abs(opts[0].Security.Strike - opts[1].Security.Strike), 2);
                            double spacing2 = Math.Round(Math.Abs(opts[2].Security.Strike - opts[3].Security.Strike), 2);
                            if (opts[0].Security.Type == opts[1].Security.Type && opts[2].Security.Type == opts[3].Security.Type && opts[0].Security.Type != opts[3].Security.Type)
                            {
                                // Check for Gut Iron Fly first (Call/Call/Put/Put pattern)
                                if (opts[0].Security.Type == PutCall.Call && opts[3].Security.Type == PutCall.Put)
                                {
                                    if (opts[0].Side == opts[3].Side && opts[1].Side == opts[2].Side && opts[0].Side != opts[1].Side)
                                    {
                                        string spacingString = spacing1 == spacing2 ? $"[{spacing1}]" : $"[{spacing1 + "/" + spacing2}]";
                                        if (spacing1 == spacing2)
                                        {
                                            baseType = "GUT IRON FLY";
                                            spreadDescription = spreadType = $"GUT IRON FLY " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[3].Security.Strike + " " + spacingString;
                                        }
                                        else
                                        {
                                            baseType = "GUT SKEWED IRON FLY";
                                            spreadDescription = spreadType = $"GUT SKEWED IRON FLY " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[3].Security.Strike + " " + spacingString;
                                        }
                                        return true;
                                    }
                                }
                                // Regular Iron Butterfly (Put/Put/Call/Call pattern)
                                if (opts[0].Side == opts[3].Side && opts[1].Side == opts[2].Side && opts[0].Side != opts[1].Side)
                                {
                                    string spacingString = spacing1 == spacing2 ? $"[{spacing1}]" : $"[{spacing1 + "/" + spacing2}]";
                                    baseType = "IRON BUTTERFLY";
                                    spreadDescription = spreadType = $"IRON BUTTERFLY " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[3].Security.Strike + " " + spacingString;
                                    return true;
                                }
                                if (opts[0].Side == opts[1].Side && opts[2].Side == opts[3].Side && opts[0].Side != opts[2].Side)
                                {
                                    string spacingString = spacing1 == spacing2 ? $"[{spacing1}]" : $"[{spacing1}/{spacing2}]";
                                    baseType = "SYNTHETIC COLLAR";
                                    spreadDescription = spreadType = $"SYNTHETIC COLLAR " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + opts[0].Security.Strike + "/" + opts[1].Security.Strike + "/" + opts[3].Security.Strike + " " + spacingString;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            else if (legs.Count == 6) // dragon, dragonfly, ratio dragon fly, ratio dragon
            {
                if (legs.Select(x => x.Security.Expiration).Distinct().Count() == 1)
                {
                    if (legs.Count(x => x.Security.Type == PutCall.Call) == 3 && legs.Count(x => x.Security.Type == PutCall.Put) == 3)
                    {
                        List<SpreadLeg> opts = legs.OrderBy(x => x.Security.Strike).ThenBy(x => x.Security.Type).ToList();
                        List<double> strikes = opts.Select(x => x.Security.Strike).Distinct().ToList();
                        if (opts[0].Security.Type == opts[1].Security.Type && opts[1].Security.Type == opts[2].Security.Type && opts[2].Security.Type != opts[3].Security.Type && opts[3].Security.Type == opts[4].Security.Type && opts[4].Security.Type == opts[5].Security.Type)
                        {
                            if (opts[0].Side != opts[1].Side && opts[1].Side == opts[4].Side && opts[0].Side == opts[2].Side && opts[2].Side == opts[3].Side && opts[3].Side == opts[5].Side
                                && opts[1].Quantity == opts[4].Quantity && opts[0].Quantity == opts[5].Quantity && opts[2].Quantity == opts[3].Quantity)
                            {
                                Security leg1 = opts[0].Security;
                                Security leg2 = opts[1].Security;
                                Security leg3 = opts[2].Security;
                                Security leg4 = opts[3].Security;
                                Security leg5 = opts[4].Security;
                                Security leg6 = opts[5].Security;

                                int wing1Qty = opts[1].Quantity;
                                int wing2Qty = opts[0].Quantity;
                                int bodyQty = opts[2].Quantity;

                                string strikesList = string.Join("/", strikes.Select(s => s.ToString()));

                                if (strikes.Count == 5 && opts[2].Security.Strike == opts[3].Security.Strike)
                                {
                                    if (wing1Qty == 3 && wing2Qty == 4 && bodyQty == 1)  // dragon fly
                                    {
                                        baseType = "DRAGONFLY";
                                        spreadDescription = spreadType = "DRAGON FLY " + GetRootSymbols(legs) + " " + opts[0].Security.Expiration.ToString("MMM-dd-yy") + " " + strikesList;
                                        return true;
                                    }
                                    else  // ratio dragon fly
                                    {
                                        string ratioPattern = wing2Qty + ":" + (-wing1Qty) + ":" + (-bodyQty) + ":" + (-bodyQty) + ":" + (-wing1Qty) + ":" + wing2Qty;
                                        baseType = "RATIO DRAGONFLY";
                                        spreadDescription = spreadType = "RATIO DRAGON FLY " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + strikesList + " RATIO[" + ratioPattern + "]";
                                        return true;
                                    }
                                }
                                else if (strikes.Count == 6 && opts[2].Security.Strike == opts[3].Security.Strike) // dragon
                                {
                                    if (wing1Qty == 3 && wing2Qty == 4 && bodyQty == 1)  // dragon
                                    {
                                        baseType = "DRAGON";
                                        spreadDescription = spreadType = "DRAGON " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + strikesList;
                                        return true;
                                    }
                                    else  // ratio dragon
                                    {
                                        string ratioPattern = wing2Qty + ":" + (-wing1Qty) + ":" + (-bodyQty) + ":" + (-bodyQty) + ":" + (-wing1Qty) + ":" + wing2Qty;
                                        baseType = "RATIO DRAGON";
                                        spreadDescription = spreadType = "RATIO DRAGON " + GetRootSymbols(legs) + " " + leg1.Expiration.ToString("MMM-dd-yy") + " " + strikesList + " RATIO[" + ratioPattern + "]";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (legs.Count > 1 && legs.Count(x => x.Security.SecurityType == SecurityType.Stock) == 1)
            {
                var optionLegs = legs.Where(x => x.Security.SecurityType == SecurityType.Option).ToList();

                if (EvaluateLegs(optionLegs, out var _, out var optSpreadType, out var optSpreadDesc))
                {
                    var types = optionLegs.Select(x => x.Security.Type).Distinct().ToList();
                    string type = types.Count == 1 ? types.FirstOrDefault().ToString().ToUpper() : string.Join("/", types).ToUpper();

                    baseType = $"{type} STOCK TIED";
                    spreadType = $"STOCK TIED {type} ";
                    spreadDescription = $"STOCK TIED {type} ";

                    spreadType += optSpreadType;
                    spreadDescription += optSpreadDesc;
                    return true;
                }
            }

            string str = "CUSTOM " + GetRootSymbols(legs) + " ";
            foreach (SpreadLeg orderLeg in legs)
            {
                if (orderLeg.Security.SecurityType == SecurityType.Option)
                {
                    str = str + orderLeg.Security.Expiration.ToString("MMM-dd-yy") + " " + orderLeg.Security.Strike + " " + (orderLeg.Security.Type == PutCall.Call ? "C" : "P") + " ";
                }
                else
                {
                    str = str + orderLeg.Symbol + " ";
                }
            }
            baseType = "CUSTOM";
            spreadType = str;
            spreadDescription = str;
            return false;
        }

        private static bool EvaluateSingleLeg(string legSymbol, Security legSecurity, Side? legSide, out string baseType,
            out string spreadType, out string spreadDescription)
        {
            spreadDescription = spreadType = baseType = "";

            if (legSymbol == null)
            {
                return false;
            }

            if (legSymbol.StartsWith("."))
            {
                string strategy = legSecurity.Type.ToString().ToUpper() + " " + legSecurity.UnderlyingSymbol + " " + legSecurity.Expiration.ToString("MMM-dd-yy") + " " + legSecurity.Strike;
                baseType = legSecurity.Type.ToString().ToUpper();
                spreadType = strategy;
                spreadDescription = legSide?.ToString().ToUpper() + " " + strategy;
                return true;
            }
            else
            {
                if (legSymbol.StartsWith("$"))
                {
                    baseType = "INDEX";
                }
                else
                {
                    baseType = "STOCK";
                }
                spreadType = legSymbol;
                spreadDescription = legSide?.ToString().ToUpper() + " " + legSymbol;
                return true;
            }
        }

        protected static string GetRootSymbols(List<SpreadLeg> legs)
        {
            string[] source = legs.Where(x => x.Security.SecurityType == SecurityType.Option).Select(x => x.Security.UnderlyingSymbol).Distinct().ToArray();
            return source.Length == 1 ? source.First() : string.Join("/", source);
        }
    }
}