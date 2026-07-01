using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Exceptions;
using ZeroPlus.Models.Generators.SpreadGenerators;
using ZeroPlus.Oms.Subscription;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class SpreadTemplateGenerator
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private CancellationToken _token = new();
        private readonly HashSet<string> _errors = new();

        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public async Task<SpreadGeneratorResults> GenerateFromTemplateAsync(string underlyingSymbol, List<Option> options, SpreadTemplateRowViewModel template, System.Threading.CancellationToken token)
        {
            _errors.Clear();
            _token = token;
            _token.ThrowIfCancellationRequested();
            List<PutCall> callOnly = new() { PutCall.Call };
            List<PutCall> putOnly = new() { PutCall.Put };
            List<PutCall> callAndPut = new() { PutCall.Call, PutCall.Put };
            switch (template.Strategy)
            {
                case BaseStrategy.CALL_VERTICAL:
                    return await GenerateVerticalAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_VERTICAL:
                    return await GenerateVerticalAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_CALENDAR:
                    return await GenerateCalendarAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_CALENDAR:
                    return await GenerateCalendarAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_DIAGONAL:
                    return await GenerateDiagonalAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_DIAGONAL:
                    return await GenerateDiagonalAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_BUTTERFLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_BUTTERFLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_SKEWED_BUTTERFLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, callOnly, template, Strategy.SkewedButterfly);
                case BaseStrategy.PUT_SKEWED_BUTTERFLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, putOnly, template, Strategy.SkewedButterfly);
                case BaseStrategy.CALL_CALENDAR_FLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_CALENDAR_FLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_1X2:
                    return await GenerateRatioAsync(underlyingSymbol, options, callOnly, template, Strategy.Ratio1X2);
                case BaseStrategy.PUT_1X2:
                    return await GenerateRatioAsync(underlyingSymbol, options, putOnly, template, Strategy.Ratio1X2);
                case BaseStrategy.CALL_1X3:
                    return await GenerateRatioAsync(underlyingSymbol, options, callOnly, template, Strategy.Ratio1X3);
                case BaseStrategy.PUT_1X3:
                    return await GenerateRatioAsync(underlyingSymbol, options, putOnly, template, Strategy.Ratio1X3);
                case BaseStrategy.CALL_2X3:
                    return await GenerateRatioAsync(underlyingSymbol, options, callOnly, template, Strategy.RatioCustom);
                case BaseStrategy.PUT_2X3:
                    return await GenerateRatioAsync(underlyingSymbol, options, putOnly, template, Strategy.RatioCustom);
                case BaseStrategy.CALL_CONDOR:
                    return await GenerateCondorAsync(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_CONDOR:
                    return await GenerateCondorAsync(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.CALL_1X3X3X1:
                    return await Generate1331Async(underlyingSymbol, options, callOnly, template);
                case BaseStrategy.PUT_1X3X3X1:
                    return await Generate1331Async(underlyingSymbol, options, putOnly, template);
                case BaseStrategy.IRON_CONDOR:
                    return await GenerateCondorAsync(underlyingSymbol, options, callAndPut, template, Strategy.IronCondor);
                case BaseStrategy.IRON_BUTTERFLY:
                    return await GenerateButterflyAsync(underlyingSymbol, options, callAndPut, template, Strategy.IronButterfly);
                default:
                    SpreadGeneratorResults results = new(default, default, default);
                    results.Errors.Add("Template not supported " + template.Strategy);
                    return results;
            }
        }

        private async Task<SpreadGeneratorResults> GenerateVerticalAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), Strategy.Vertical);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                string tos = leg1.Symbol + "-" + leg2.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };

                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell));

                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> GenerateCalendarAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), Strategy.Calendar);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                string tos = leg1.Symbol + "-" + leg2.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };

                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell));

                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> GenerateDiagonalAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), Strategy.Diagonal);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                string tos = leg1.Symbol + "-" + leg2.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };

                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell));

                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> GenerateRatioAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template, Strategy ratioStrategy)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), ratioStrategy);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                string tos = "";
                Spread spread = null;
                switch (ratioStrategy)
                {
                    case Strategy.Ratio1X2:
                        tos = leg1.Symbol + "-2*" + leg2.Symbol;

                        spread = new(tos)
                        {
                            Side = template.Side.ToString()
                        };
                        spread.Legs.Add(new(leg1));
                        spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell, 2));
                        break;
                    case Strategy.Ratio1X3:
                        tos = leg1.Symbol + "-3*" + leg2.Symbol;

                        spread = new(tos)
                        {
                            Side = template.Side.ToString()
                        };
                        spread.Legs.Add(new(leg1));
                        spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell, 3));
                        break;
                    case Strategy.RatioCustom:
                        tos = "2*" + leg1.Symbol + "-3*" + leg2.Symbol;

                        spread = new(tos)
                        {
                            Side = template.Side.ToString()
                        };
                        spread.Legs.Add(new(leg1, ZeroPlus.Models.Data.Enums.Side.Buy, 2));
                        spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell, 3));
                        break;
                    default:
                        return results;
                }
                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> GenerateButterflyAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template, Strategy strategy = Strategy.Butterfly)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), strategy);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                Task<Option> leg3Builder = LocateOptionAsync(options, type, template.Leg3Expiration, template.Leg3Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder, leg3Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                Option leg3 = leg3Builder?.Result;
                string tos = leg1.Symbol + "-2*" + leg2.Symbol + "+" + leg3.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };
                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell, 2));
                spread.Legs.Add(new(leg3));
                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> GenerateCondorAsync(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template, Strategy strategy = Strategy.Condor)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), strategy);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                Task<Option> leg3Builder = LocateOptionAsync(options, type, template.Leg3Expiration, template.Leg3Delta);
                Task<Option> leg4Builder = LocateOptionAsync(options, type, template.Leg4Expiration, template.Leg4Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder, leg3Builder, leg4Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                Option leg3 = leg3Builder?.Result;
                Option leg4 = leg4Builder?.Result;
                string tos = leg1.Symbol + "-" + leg2.Symbol + "-" + leg3.Symbol + "+" + leg4.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };
                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell));
                spread.Legs.Add(new(leg3, ZeroPlus.Models.Data.Enums.Side.Sell));
                spread.Legs.Add(new(leg4));
                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<SpreadGeneratorResults> Generate1331Async(string underlyingSymbol, List<Option> options, List<PutCall> type, SpreadTemplateRowViewModel template)
        {
            SpreadGeneratorResults results = new(underlyingSymbol, type.FirstOrDefault(), Strategy.OneThreeThreeOne);
            try
            {
                Task<Option> leg1Builder = LocateOptionAsync(options, type, template.Leg1Expiration, template.Leg1Delta);
                Task<Option> leg2Builder = LocateOptionAsync(options, type, template.Leg2Expiration, template.Leg2Delta);
                Task<Option> leg3Builder = LocateOptionAsync(options, type, template.Leg3Expiration, template.Leg3Delta);
                Task<Option> leg4Builder = LocateOptionAsync(options, type, template.Leg4Expiration, template.Leg4Delta);
                await Task.WhenAll(new Task[] { leg1Builder, leg2Builder, leg3Builder, leg4Builder });
                Option leg1 = leg1Builder?.Result;
                Option leg2 = leg2Builder?.Result;
                Option leg3 = leg3Builder?.Result;
                Option leg4 = leg4Builder?.Result;
                string tos = leg1.Symbol + "-3*" + leg2.Symbol + "+3*" + leg3.Symbol + "-" + leg4.Symbol;
                Spread spread = new(tos)
                {
                    Side = template.Side.ToString()
                };
                spread.Legs.Add(new(leg1));
                spread.Legs.Add(new(leg2, ZeroPlus.Models.Data.Enums.Side.Sell, 3));
                spread.Legs.Add(new(leg3, ZeroPlus.Models.Data.Enums.Side.Buy, 3));
                spread.Legs.Add(new(leg4, ZeroPlus.Models.Data.Enums.Side.Sell));
                if (template.EdgeOverrideEnabled)
                {
                    spread.EdgeOverride = template.EdgeOverride;
                }
                results.Spreads.Add(spread);
                results.Errors = _errors;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(GenerateFromTemplateAsync));
                results.Errors.Add("Template failed for " + underlyingSymbol + " " + template.Strategy + " " + template.Strategy + ", " + ex.Message);
            }
            return results;
        }

        private async Task<Option> LocateOptionAsync(List<Option> options, List<PutCall> types, Models.TemplateExpirationModel expiration, double legDelta)
        {
            if (options == null ||
                expiration == default)
            {
                throw new ArgumentException("invalid input.");
            }

            return await Task.Run(async () =>
            {
                _token.ThrowIfCancellationRequested();
                List<Option> filteredOptions = options.Where(x => types.Contains(x.PutCall) && x.Expiration.Date == expiration.Expiration.Date).ToList();

                if (types.Distinct().Count() == 1)
                {
                    PutCall type = types.FirstOrDefault();
                    switch (type)
                    {
                        case PutCall.Put:
                            filteredOptions = filteredOptions.OrderBy(x => x.Strike).ToList();
                            break;
                        case PutCall.Call:
                            filteredOptions = filteredOptions.OrderByDescending(x => x.Strike).ToList();
                            break;
                    }
                }

                if (filteredOptions.Count == 0)
                {
                    throw new SlimException("no symbols found for " + expiration.Expiration.ToString("d") + ".");
                }

                DataStore deltaStore = new(_token, OmsCore.Config.SpreadGeneratorTimeout, OmsCore.Config.SpreadGeneratorUseGlobalTimeout);
                deltaStore.GetHanweckDataFor(filteredOptions, SubscriptionFieldType.Delta);

                Option selectedOption = null;
                double smallestChange = double.MaxValue;
                foreach (Option option in filteredOptions)
                {
                    _token.ThrowIfCancellationRequested();
                    double delta = Math.Abs(await deltaStore.GetDataAsync(option.Symbol));
                    _token.ThrowIfCancellationRequested();
                    if (double.IsNaN(delta))
                    {
                        _errors.Add("Delta not found for " + option.Symbol);
                    }
                    else
                    {
                        double deltaChange = Math.Abs(delta - legDelta);
                        if (deltaChange < smallestChange)
                        {
                            smallestChange = deltaChange;
                            selectedOption = option;
                        }
                        if (deltaChange == 0)
                        {
                            break;
                        }
                    }
                }

                return selectedOption;
            });
        }
    }
}