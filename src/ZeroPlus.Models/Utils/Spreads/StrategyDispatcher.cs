using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Utils.Spreads;

public static class StrategyDispatcher
{
    public record SpreadCacheEntry(string BaseType, string SpreadType, string SpreadDescription, bool Result);
    static readonly ConcurrentDictionary<string, SpreadCacheEntry> _cache = new();


    // Groups all registered strategies by their leg count for fast lookup.
    private static readonly Dictionary<int, List<IStrategy>> _strategiesByLegCount = new()
    {
        {
            1, new List<IStrategy>
            {
                // Single-leg strategies
                new SCall(),
                new SPut(),
                new SStock(),
                new SIndex(),
            }
        },
        {
            2, new List<IStrategy>
            {
                // Vertical spreads
                new SCallVertical(),
                new SPutVertical(),
                
                // Basic ratios
                new SCall1x2(),
                new SPut1x2(),
                new SCall1x3(),
                new SPut1x3(),
                new SCall2x3(),
                new SPut2x3(),
                
                // Generic ratios (fallback)
                new SCallRatioSpread(),
                new SPutRatioSpread(),
                
                // Volatility strategies
                new SStraddle(),
                new SStrangle(),
                
                // Time spreads
                new SCallCalendar(),
                new SPutCalendar(),
                new SCallDiagonal(),
                new SPutDiagonal(),
                
                // Stock combinations
                new SCoveredCall(),
                new SCoveredPut(),
                new SProtectivePut(),
            }
        },
        {
            3, new List<IStrategy>
            {
                // Butterflies
                new SCallButterfly(),
                new SPutButterfly(),
                new SCallSkewedButterfly(),
                new SPutSkewedButterfly(),
                
                // Time-based butterflies
                new SCallCalendarFly(),
                new SPutCalendarFly(),
                new SCallSkewedCalendarFly(),
                new SPutSkewedCalendarFly(),
                
                // Complex ratios
                new SCallRatioFly(),
                new SPutRatioFly(),
                new SCall1x3x2(),
                new SPut1x3x2(),
                new SCall2x3x1(),
                new SPut2x3x1(),
                
                // Trees
                new SCallTree(),
                new SPutTree(),
                
                // Time spreads
                new SCallTriagonal(),
                new SPutTriagonal(),
                
                // Mixed strategies
                new SCoveredStraddle(),
                new SMarriedStraddle(),
                new SCallSpreadVsPut(),
                new SPutSpreadVsCall(),
                
                // Arbitrage
                new SConversion(),
                new SReversal(),
            }
        },
        {
            4, new List<IStrategy>
            {
                // Iron strategies
                new SIronCondor(),
                new SIronButterfly(),
                new SSyntheticCollar(),
                new SGutIronFly(),
                new SGutIronCondor(),

                // Condors
                new SCallCondor(),
                new SPutCondor(),
                
                // Arbitrage
                new SBoxSpread(),
                
                // Complex ratios
                new SCall1x3x3x1(),
                new SPut1x3x3x1(),
                
                // Ratio diagonals
                new SCallRatioDiagonal(),
                new SPutRatioDiagonal(),
            }
        },
        {
            6, new List<IStrategy>
            {
                // Complex volatility strategies
                new SDragonfly(),
                new SRatioDragonfly(),
            }
        }
    };

    private static BaseStrategy ConvertFromString(string baseTypeString)
    {
        BaseStrategy baseStrategy = BaseStrategy.INVALID;
        if (Enum.TryParse(baseTypeString.Replace(" ", "_"), true, out BaseStrategy parsed))
        {
            baseStrategy = parsed;
        }
        return baseStrategy;
    }

    public static string ConvertToString(BaseStrategy baseType)
    {
        return baseType.ToString().Replace("_", " ");
    }

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
        spreadType = "";
        spreadDescription = "";
        if (TryIdentify(ParseLegs(spreadId), out StrategyIdentification? details))
        {
            spreadType = details!.SpreadType;
            spreadDescription = details!.SpreadDescription;
            baseStrategy = ConvertFromString(details!.BaseType);
            return true;
        }
        else if (details != null)
        {
            // CUSTOM fallback was populated by GenerateCustomSpreadDescription
            spreadType = details.SpreadType;
            spreadDescription = details.SpreadDescription;
            baseStrategy = BaseStrategy.CUSTOM;
            return false; // Still return false to match legacy behavior
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
    /// <param name="baseStrategy"></param>
    /// <param name="spreadType"></param>
    /// <param name="spreadDescription"></param>
    /// <returns>
    ///    true if strategy was identified successfully; otherwise, false.
    /// </returns>
    public static bool TryIdentify(string spreadId, out BaseStrategy baseStrategy, out string spreadType, out string spreadDescription, out Side? side)
    {
        baseStrategy = BaseStrategy.CUSTOM;
        spreadType = "";
        spreadDescription = "";
        var legs = ParseLegs(spreadId);
        if (TryIdentify(legs, out StrategyIdentification? details))
        {
            spreadType = details!.SpreadType;
            spreadDescription = details!.SpreadDescription;
            baseStrategy = ConvertFromString(details!.BaseType);
            side = IdentifySpreadSide(baseStrategy, legs);
            return true;
        }
        else if (details != null)
        {
            // CUSTOM fallback was populated by GenerateCustomSpreadDescription
            spreadType = details.SpreadType;
            spreadDescription = details.SpreadDescription;
            baseStrategy = BaseStrategy.CUSTOM;
            side = null;
            return false; // Still return false to match legacy behavior
        }
        side = null;
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
    public static bool TryIdentify(IReadOnlyList<IComplexOrderLegMin> legs, out StrategyIdentification? details)
    {
        details = null;
        int legCount = legs.Count;

        // Handle empty list - don't generate CUSTOM description
        if (legCount == 0)
        {
            return false;
        }

        // Defensive Security population for UAT compatibility
        // Ensure all legs have Security populated from Symbol if null
        var legsWithSecurity = legs.Select(leg =>
        {
            if (leg == null) return null;

            if (leg.Security == null && !string.IsNullOrEmpty(leg.Symbol))
            {
                // Create new leg with Security populated from Symbol
                return new SpreadLeg(
                    Security: GetSecurityFromSymbol(leg.Symbol),
                    Symbol: leg.Symbol,
                    Side: leg.Side,
                    Quantity: leg.Quantity,
                    Ratio: leg.Ratio
                );
            }
            return leg;
        }).Where(l => l != null).Cast<IComplexOrderLegMin>().ToList();

        // Update legCount after filtering nulls
        legCount = legsWithSecurity.Count;
        if (legCount == 0)
        {
            return false;
        }

        if (legCount == 1)
        {
            var leg = legsWithSecurity[0];
            if (EvaluateSingleLeg(leg.Symbol ?? string.Empty, leg.Security, leg.Side, out string baseType, out string spreadType, out string spreadDescription))
            {
                details = new StrategyIdentification(
                    BaseType: baseType,
                    SpreadType: spreadType,
                    SpreadDescription: spreadDescription
                );
                return true;
            }
            return false;
        }

        // Sort legs: stock/index legs first, then option legs by strike and type
        var sortedLegs = legsWithSecurity.Where(l => l?.Security?.SecurityType != SecurityType.Option).ToList();

        var sortedOptionLegs = legsWithSecurity.Where(l => l?.Security?.SecurityType == SecurityType.Option)
            .OrderBy(l => (l!.Security as Option)?.Strike)
            .ThenBy(l => (l!.Security as Option)?.PutCall) // Puts Call (2) after Put (1)
            .ToList();

        sortedLegs.AddRange(sortedOptionLegs);

        // 1. Route based on leg count.
        if (!_strategiesByLegCount.TryGetValue(legCount, out var potentialStrategies))
        {
            // No strategies registered for this number of legs - generate CUSTOM description
            details = GenerateCustomSpreadDescription(sortedLegs);
            return false;
        }

        // 3. Dispatch to the correct, strongly-typed TryIdentify method.
        foreach (var strategy in potentialStrategies)
        {
            // This switch handles calling the correct interface method.
            bool identified = strategy.TryIdentify(sortedLegs, out details);

            if (identified) return true;
        }

        // Generate CUSTOM description for unrecognized patterns (UAT fix)
        details = GenerateCustomSpreadDescription(sortedLegs);
        return false; // Still return false to indicate no recognized pattern
    }

    public static Side? IdentifySpreadSide(BaseStrategy baseStrategy, IEnumerable<IComplexOrderLegMin> complexOrderLegs)
    {
        // Filter out null legs and legs with null Security before processing - no more null-forgiving operators
        var validLegs = complexOrderLegs.Where(l => l?.Security != null).ToList();
        if (!validLegs.Any()) return null;

        Side? result;
        if (validLegs.FirstOrDefault(l => l.Security is not Option) is IComplexOrderLegMin under)
        {
            return under.Side;
        }

        // Filter to only valid option legs for all operations
        var optionLegs = validLegs.Where(l => l.Security is Option).ToList();
        if (!optionLegs.Any()) return null;

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
                result = optionLegs.OrderBy(x => ((Data.Securities.Option)x.Security!).Strike).FirstOrDefault()?.Side;
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
                result = optionLegs.OrderBy(x => ((Data.Securities.Option)x.Security!).Strike).FirstOrDefault()?.Side == Side.Buy ? Side.Sell : Side.Buy;
                break;
            case BaseStrategy.PUT_1X2:
            case BaseStrategy.PUT_1X3:
            case BaseStrategy.PUT_2X3:
            case BaseStrategy.PUT_VERTICAL:
            case BaseStrategy.PUT_1X3X3X1:
                result = optionLegs.OrderByDescending(x => ((Data.Securities.Option)x.Security!).Strike).FirstOrDefault()?.Side;
                break;
            case BaseStrategy.CALL_1x3x2:
            case BaseStrategy.PUT_1x3x2:
            case BaseStrategy.CALL_2x3x1:
            case BaseStrategy.PUT_2x3x1:
            case BaseStrategy.CALL_BUTTERFLY:
            case BaseStrategy.PUT_BUTTERFLY:
            case BaseStrategy.CALL_SKEWED_BUTTERFLY:
            case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                result = optionLegs.OrderBy(x => x.Ratio).FirstOrDefault()?.Side;
                break;
            case BaseStrategy.CALL_CALENDAR:
            case BaseStrategy.PUT_CALENDAR:
            case BaseStrategy.CALL_DIAGONAL:
            case BaseStrategy.PUT_DIAGONAL:
            case BaseStrategy.CALL_TRIAGONAL:
            case BaseStrategy.PUT_TRIAGONAL:
                result = optionLegs.OrderByDescending(x => ((Data.Securities.Option)x.Security!).Expiration).FirstOrDefault()?.Side;
                break;
            case BaseStrategy.CALL_CALENDAR_FLY:
            case BaseStrategy.PUT_CALENDAR_FLY:
            case BaseStrategy.CALL_SKEWED_CALENDAR_FLY:
            case BaseStrategy.PUT_SKEWED_CALENDAR_FLY:
                result = optionLegs.OrderByDescending(x => ((Data.Securities.Option)x.Security!).Expiration).FirstOrDefault()?.Side;
                break;
            case BaseStrategy.REVERSAL:
            case BaseStrategy.CONVERSION:
                result = optionLegs.FirstOrDefault(x => ((Data.Securities.Option)x.Security!).PutCall == PutCall.Put)?.Side;
                break;
            case BaseStrategy.CALL:
            case BaseStrategy.PUT:
            case BaseStrategy.CALL_STOCK_TIED:
            case BaseStrategy.PUT_STOCK_TIED:
                result = validLegs.FirstOrDefault(x => x.Security?.SecurityType == SecurityType.Option)?.Side;
                break;
            default:
                result = validLegs.FirstOrDefault()?.Side ?? Side.Buy;
                break;
        }

        return result;
    }

    private static readonly Regex _legSplitRegex = new Regex(@"(?=[-+])", RegexOptions.Compiled);
    public static List<IComplexOrderLegMin> ParseLegs(string spread)
    {
        List<string> legSymbols = _legSplitRegex.Split(spread).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        int legCount = legSymbols.Count;
        List<IComplexOrderLegMin> order = new();
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
                Security = GetSecurityFromSymbol(legSymbol),
                Ratio = 1
            };
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

                IComplexOrderLegMin leg = new SpreadLeg()
                {
                    Symbol = legSymbol,
                    Quantity = qty,
                    Side = side,
                    Security = GetSecurityFromSymbol(legSymbol),
                    Ratio = 1
                };
                order.Add(leg);
            }
        }

        // Auto-adjust stock quantities when mixed with options
        // This handles conversion/reversal spreads where stock quantity should match option quantity * multiplier
        var stockLegs = order.Where(x => x?.Security?.SecurityType == SecurityType.Stock).ToList();
        var optionLegs = order.Where(x => x?.Security?.SecurityType == SecurityType.Option).ToList();

        if (stockLegs.Any() && optionLegs.Any())
        {
            // Get the multiplier from the first option (typically 100 for equity options)
            var firstOption = optionLegs.FirstOrDefault()?.Security;
            if (firstOption != null)
            {
                int multiplier = (int)firstOption.Multiplier;

                // Recreate order list with adjusted stock quantities
                List<IComplexOrderLegMin> adjustedOrder = new();
                foreach (var leg in order)
                {
                    if (leg == null)
                        continue;

                    if (leg.Security?.SecurityType == SecurityType.Stock && leg.Quantity == 1)
                    {
                        // Recreate stock leg with adjusted quantity
                        adjustedOrder.Add(new SpreadLeg()
                        {
                            Symbol = leg.Symbol ?? string.Empty,
                            Quantity = multiplier,
                            Side = leg.Side,
                            Security = leg.Security,
                            Ratio = leg.Ratio
                        });
                    }
                    else
                    {
                        adjustedOrder.Add(leg);
                    }
                }
                return adjustedOrder;
            }
        }

        return order;
    }
    internal static Security GetSecurityFromSymbol(string symbol)
    {
        // Defensive null/empty check
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return new Security { Symbol = string.Empty, SecurityType = SecurityType.Stock, Multiplier = 1 };
        }

        SymbolLib.Instrument? instrument = null;
        try
        {
            instrument = new SymbolLib.Instrument(symbol);
        }
        catch
        {
            // Failed to parse - return basic security based on symbol prefix
            return new Security
            {
                Symbol = symbol,
                SecurityType = symbol.StartsWith(".") ? SecurityType.Option :
                              symbol.StartsWith("$") ? SecurityType.Index :
                              SecurityType.Stock,
                Multiplier = 1
            };
        }

        // Determine if this is an option or stock symbol
        bool isOption = symbol.StartsWith('.');
        if (isOption && instrument != null)
        {
            bool indexOption = instrument.underlyingSymbol?.StartsWith('$') ?? false;

            // Validate we have required fields for an option
            if (string.IsNullOrEmpty(instrument.underlyingSymbol))
            {
                return new Security
                {
                    Symbol = symbol,
                    SecurityType = SecurityType.Option,
                    Multiplier = 100
                };
            }

            // Create underlying with null checks - ensure it's never null
            Security underlying = new Security
            {
                Symbol = instrument.underlyingSymbol,
                SecurityType = indexOption ? SecurityType.Index : SecurityType.Stock,
                Multiplier = 1
            };

            Security option = new Option()
            {
                Symbol = symbol,
                RootSymbol = instrument.rootSymbol ?? symbol,
                Expiration = instrument.expiration,
                PutCall = instrument.callPut ? PutCall.Put : PutCall.Call,
                Strike = instrument.strike,
                Underlying = underlying, // Never null now
                Multiplier = 100,
                SecurityType = SecurityType.Option,
                SecurityCategory = indexOption ? SecurityCategory.IndexOption : SecurityCategory.EquityOption
            };
            return option;
        }

        bool isIndex = symbol.StartsWith('$');

        Security security = new()
        {
            Symbol = symbol,
            Multiplier = 1,
            SecurityType = isIndex ? SecurityType.Index : SecurityType.Stock
        };

        return security;
    }

    public static string GetRootSymbols(IList<IComplexOrderLegMin> legs)
    {
        // Filter out nulls before accessing properties - no more null-forgiving operators
        var optionLegs = legs
            .Where(x => x?.Security?.SecurityType == SecurityType.Option)
            .Select(x => x.Security as Option)
            .Where(opt => opt?.Underlying?.Symbol != null)
            .Select(opt => opt!.Underlying!.Symbol) // Now safe - filtered nulls above
            .Distinct()
            .ToArray();

        // Match legacy OptionStrategy behavior: return empty string when no option legs
        // This maintains backward compatibility for CUSTOM spread descriptions
        return optionLegs.Length == 1 ? optionLegs.First() : string.Join("/", optionLegs);
    }

    private static string SafeGetRootSymbols(IList<IComplexOrderLegMin> legs)
    {
        try
        {
            return GetRootSymbols(legs);
        }
        catch
        {
            // Fallback if GetRootSymbols fails - return empty string to match legacy behavior
            return string.Empty;
        }
    }

    private static StrategyIdentification GenerateCustomSpreadDescription(IList<IComplexOrderLegMin> legs)
    {
        // Build custom description from all legs (stocks and options)
        string rootSymbols = SafeGetRootSymbols(legs);
        string description = "CUSTOM";

        // Add root symbols if we have any option legs
        if (!string.IsNullOrEmpty(rootSymbols))
        {
            description += " " + rootSymbols;
        }

        description += " ";

        foreach (var leg in legs)
        {
            if (leg?.Security == null) continue;

            if (leg.Security.SecurityType == SecurityType.Option && leg.Security is Option option)
            {
                var underlying = option.Underlying;
                if (underlying?.Symbol != null)
                {
                    description += option.Expiration.ToString("MMM-dd-yy") + " "
                        + option.Strike + " "
                        + (option.PutCall == PutCall.Call ? "C" : "P") + " ";
                }
            }
            else
            {
                // For stock/index legs, just add the symbol
                if (!string.IsNullOrEmpty(leg.Symbol))
                {
                    description += leg.Symbol + " ";
                }
            }
        }

        return new StrategyIdentification(
            BaseType: "CUSTOM",
            SpreadType: description.Trim(),
            SpreadDescription: description.Trim()
        );
    }

    private static bool EvaluateSingleLeg(string legSymbol, Security? legSecurity, Side? legSide, out string baseType, out string spreadType, out string spreadDescription)
    {
        spreadDescription = spreadType = baseType = "";

        if (string.IsNullOrWhiteSpace(legSymbol))
        {
            return false;
        }

        if (legSymbol.StartsWith(".") && legSecurity is Option option)
        {
            // Validate option has required fields - no more null-forgiving operators
            if (option.Underlying == null || string.IsNullOrEmpty(option.Underlying.Symbol))
            {
                baseType = "INVALID";
                spreadType = "INVALID";
                spreadDescription = "INVALID";
                return false;
            }

            // Now safe to access - removed null-forgiving operators
            string strategy = option.PutCall.ToString().ToUpper() + " "
                + option.Underlying.Symbol + " "
                + option.Expiration.ToString("MMM-dd-yy") + " "
                + option.Strike;
            baseType = option.PutCall.ToString().ToUpper();
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
}
