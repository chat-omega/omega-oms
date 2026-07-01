using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroPlus.Models.Data.Models;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Models.Extensions
{
    public static class HardSideExtensions
    {
        public static ConcurrentDictionary<string, HardSideKey?> HardSideKeyCache { get; } = new();

        public static bool ValidateHardSideStrikes(this List<double>? strikes, List<double>? hardSideStrikes)
        {
            try
            {
                if (strikes == null || hardSideStrikes == null || strikes.Count != hardSideStrikes.Count)
                {
                    return false;
                }

                bool isValid = true;
                bool isAllGreater = true;
                bool isAllLess = true;
                for (int i = 0; i < strikes.Count - 1; i++)
                {
                    double strike = strikes[i];
                    double nextStrike = strikes[i + 1];
                    double hardSideStrike = hardSideStrikes[i];
                    double hardSideNextStrike = hardSideStrikes[i + 1];
                    isAllGreater &= strike >= hardSideStrike && nextStrike >= hardSideNextStrike;
                    isAllLess &= strike <= hardSideStrike && nextStrike <= hardSideNextStrike;
                    if ((!isAllGreater || strike >= hardSideNextStrike) &&
                        (!isAllLess || nextStrike <= hardSideStrike))
                    {
                        isValid = false;
                    }
                }

                return isValid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static HardSideKey? GetHardSideKey(this IOrder order)
        {
            HardSideKey? key = null;
            if (order.SpreadId != null)
            {
                if (HardSideKeyCache.TryGetValue(order.SpreadId, out key))
                {
                    return key;
                }

                Models.Data.Enums.BaseStrategy baseStrategy = order.BaseStrategy;
                if (baseStrategy == Models.Data.Enums.BaseStrategy.CALL_SKEWED_BUTTERFLY)
                {
                    baseStrategy = Models.Data.Enums.BaseStrategy.CALL_BUTTERFLY;
                }
                else if (baseStrategy == Models.Data.Enums.BaseStrategy.PUT_SKEWED_BUTTERFLY)
                {
                    baseStrategy = Models.Data.Enums.BaseStrategy.PUT_BUTTERFLY;
                }

                if (!string.IsNullOrWhiteSpace(order.UnderlyingSymbol))
                {
                    if (!order.IsComplexOrder)
                    {
                        if (order.Symbol != null)
                        {
                            var instrument = new SymbolLib.Instrument(order.Symbol);
                            if (instrument.valid)
                            {
                                key = new HardSideKey()
                                {
                                    Underlying = order.UnderlyingSymbol,
                                    ExpirationKey =
                                        int.Parse(
                                            $"{instrument.expiration.Date.Year}{instrument.expiration.Date.Month}{instrument.expiration.Date.Day}"),
                                    BaseStrategy = baseStrategy,
                                };
                            }
                        }
                    }
                    else if(order is IComplexOrder complexOrder)
                    {
                        int expiration = 0;
                        foreach (var leg in complexOrder.Legs)
                        {
                            if (leg.Security != null)
                            {
                                if (leg.Security.SecurityType == Models.Data.Enums.SecurityType.Option)
                                {
                                    if (leg.Security is Option option)
                                    {
                                        expiration +=
                                            int.Parse(
                                                $"{option.Expiration.Date.Year}{option.Expiration.Date.Month}{option.Expiration.Date.Day}");
                                    }
                                }
                            }
                        }

                        key = new HardSideKey()
                        {
                            Underlying = order.UnderlyingSymbol,
                            ExpirationKey = expiration,
                            BaseStrategy = baseStrategy,
                        };
                    }
                }

                if (key != null)
                {
                    HardSideKeyCache[order.SpreadId] = key;
                }
            }

            return key;
        }

    }
}
