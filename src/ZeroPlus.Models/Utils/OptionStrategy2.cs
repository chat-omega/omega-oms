using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Trading.Interfaces;
using ZeroPlus.Models.Utils.Spreads;

namespace ZeroPlus.Models.Utils;

/// <summary>
/// Static wrapper class that delegates to StrategyDispatcher for option strategy identification.
/// This class provides backward compatibility with the legacy OptionStrategy API while leveraging
/// the new plugin-based StrategyDispatcher architecture.
/// </summary>
public static class OptionStrategy2
{
    /// <summary>
    ///    Tries to identify option strategy from a string representation of a spread.
    ///    A return value indicates whether the conversion succeeded.
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
        return StrategyDispatcher.TryIdentify(spreadId, out baseStrategy, out spreadType, out spreadDescription);
    }
    
    /// <summary>
    ///    Tries to identify option strategy from a string representation of a spread.
    ///    A return value indicates whether the conversion succeeded.
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
        return StrategyDispatcher.TryIdentify(spreadId, out baseStrategy, out spreadType, out spreadDescription, out side);
    }

    /// <summary>
    ///    Tries to identify option strategy from a string representation of a spread.
    ///    A return value indicates whether the conversion succeeded.
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
        baseType = "";

        if (StrategyDispatcher.TryIdentify(spreadId, out BaseStrategy baseStrategy, out spreadType, out spreadDescription))
        {
            baseType = ConvertToString(baseStrategy);
            return true;
        }
        else if (baseStrategy == BaseStrategy.CUSTOM && !string.IsNullOrEmpty(spreadType))
        {
            // CUSTOM fallback was populated by StrategyDispatcher (UAT fix)
            baseType = "CUSTOM";
            return false; // Still return false to match legacy OptionStrategy behavior
        }

        // Truly failed - clear output and return false
        spreadType = "";
        spreadDescription = "";
        return false;
    }

    /// <summary>
    /// Converts a BaseStrategy enum to its string representation.
    /// </summary>
    /// <param name="baseType">The BaseStrategy enum value</param>
    /// <returns>String representation of the strategy</returns>
    public static string ConvertToString(BaseStrategy baseType)
    {
        return StrategyDispatcher.ConvertToString(baseType);
    }

    /// <summary>
    /// Converts a string representation to a BaseStrategy enum.
    /// </summary>
    /// <param name="baseTypeString">The string representation of the strategy</param>
    /// <returns>BaseStrategy enum value</returns>
    public static BaseStrategy ConvertFromString(string baseTypeString)
    {
        BaseStrategy baseStrategy = BaseStrategy.INVALID;
        if (Enum.TryParse(baseTypeString.Replace(" ", "_"), true, out BaseStrategy parsed))
        {
            baseStrategy = parsed;
        }
        return baseStrategy;
    }

    /// <summary>
    /// Identifies the spread side (Buy/Sell) for a given strategy and legs.
    /// </summary>
    /// <param name="baseStrategy">The strategy type as a string</param>
    /// <param name="complexOrderLegs">The order legs</param>
    /// <returns>The identified side, or null if unable to determine</returns>
    public static Side? IdentifySpreadSide(string baseStrategy, IEnumerable<IComplexOrderLeg> complexOrderLegs)
    {
        return IdentifySpreadSide(ConvertFromString(baseStrategy), complexOrderLegs);
    }

    /// <summary>
    /// Identifies the spread side (Buy/Sell) for a given strategy and legs.
    /// </summary>
    /// <param name="baseStrategy">The strategy type</param>
    /// <param name="complexOrderLegs">The order legs</param>
    /// <returns>The identified side, or null if unable to determine</returns>
    public static Side? IdentifySpreadSide(BaseStrategy baseStrategy, IEnumerable<IComplexOrderLeg> complexOrderLegs)
    {
        // IComplexOrderLeg extends IComplexOrderLegMin, so we can pass directly
        return StrategyDispatcher.IdentifySpreadSide(baseStrategy, complexOrderLegs);
    }

    /// <summary>
    /// Evaluates an order to identify its strategy type.
    /// </summary>
    /// <param name="order">The order to evaluate</param>
    /// <param name="baseType">Output: The base strategy type as a string</param>
    /// <param name="spreadType">Output: The spread type description</param>
    /// <param name="spreadDescription">Output: The full spread description</param>
    /// <returns>true if strategy was identified successfully; otherwise, false</returns>
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

        // For single leg orders, use StrategyDispatcher with a single-item list
        order.Security ??= StrategyDispatcher.GetSecurityFromSymbol(order.Symbol ?? "");

        var singleLeg = new List<IComplexOrderLegMin>
        {
            new SpreadLeg(
                Security: order.Security,
                Symbol: order.Symbol ?? string.Empty,
                Side: order.Side,
                Quantity: order.Quantity,
                Ratio: 1
            )
        };

        if (StrategyDispatcher.TryIdentify(singleLeg, out StrategyIdentification? details))
        {
            baseType = details!.BaseType;
            spreadType = details.SpreadType;
            spreadDescription = details.SpreadDescription;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates a collection of complex order legs to identify the strategy.
    /// </summary>
    /// <param name="complexOrderLegs">The order legs to evaluate</param>
    /// <param name="baseType">Output: The base strategy type as a string</param>
    /// <param name="spreadType">Output: The spread type description</param>
    /// <param name="spreadDescription">Output: The full spread description</param>
    /// <returns>true if strategy was identified successfully; otherwise, false</returns>
    public static bool EvaluateLegs(IEnumerable<IComplexOrderLeg> complexOrderLegs,
        out string baseType,
        out string spreadType,
        out string spreadDescription)
    {
        spreadDescription = spreadType = baseType = "";

        // Ensure Security is populated from Symbol if null (matches old OptionStrategy behavior)
        IReadOnlyList<IComplexOrderLegMin> legsList = complexOrderLegs.Select(leg =>
        {
            // If Security is null, create it from Symbol like old OptionStrategy did
            var security = leg.Security ?? StrategyDispatcher.GetSecurityFromSymbol(leg.Symbol ?? "");
            return (IComplexOrderLegMin)new SpreadLeg(
                Security: security,
                Symbol: leg.Symbol ?? "",
                Side: leg.Side,
                Quantity: leg.Ratio,
                Ratio: leg.Ratio
            );
        }).ToList();

        if (StrategyDispatcher.TryIdentify(legsList, out StrategyIdentification? details))
        {
            baseType = details!.BaseType;
            spreadType = details.SpreadType;
            spreadDescription = details.SpreadDescription;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Evaluates a list of SpreadLeg objects to identify the strategy.
    /// </summary>
    /// <param name="legs">The spread legs to evaluate</param>
    /// <param name="baseType">Output: The base strategy type as a string</param>
    /// <param name="spreadType">Output: The spread type description</param>
    /// <param name="spreadDescription">Output: The full spread description</param>
    /// <returns>true if strategy was identified successfully; otherwise, false</returns>
    public static bool EvaluateLegs(List<SpreadLeg> legs,
        out string baseType,
        out string spreadType,
        out string spreadDescription)
    {
        spreadDescription = spreadType = baseType = "";

        // Ensure Security is populated from Symbol if null (matches old OptionStrategy behavior)
        IReadOnlyList<IComplexOrderLegMin> legsList = legs.Select(leg =>
        {
            // If Security is null, create it from Symbol like old OptionStrategy did
            var security = leg.Security ?? StrategyDispatcher.GetSecurityFromSymbol(leg.Symbol ?? "");
            return (IComplexOrderLegMin)new SpreadLeg(
                Security: security,
                Symbol: leg.Symbol ?? "",
                Side: leg.Side,
                Quantity: leg.Quantity,
                Ratio: leg.Ratio
            );
        }).ToList();

        if (StrategyDispatcher.TryIdentify(legsList, out StrategyIdentification? details))
        {
            baseType = details!.BaseType;
            spreadType = details.SpreadType;
            spreadDescription = details.SpreadDescription;
            return true;
        }

        return false;
    }
}
