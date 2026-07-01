using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Interfaces;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Models.Generators.SpreadGenerators
{
    public class CalendarButterflySpreadsGenerator : SpreadGenerator
    {
        public CalendarButterflySpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
            : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
        {
        }

        public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option> leg2Options, List<Option> leg3Options, ICalendarButterflySpreadsGeneratorSettings settings, int count, CancellationToken token, bool isSample = false)
        {
            List<Option> optionChain = leg1Options.Union(leg2Options).Union(leg3Options).Distinct().ToList();
            Option? option = optionChain.FirstOrDefault();
            SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.CalendarButterfly);
            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            var type = option.PutCall;
            _logger.LogInformation($"[Start] {nameof(CalendarButterflySpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options.");

            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (!isSample)
            {
                RequestData(optionChain, settings);

                leg1Options = await ApplyLeg1Filters(leg1Options, settings, spreadGeneratorResults, token);
                leg2Options = await ApplyLeg2Filters(leg2Options, settings, spreadGeneratorResults, token);
                leg3Options = await ApplyLeg3Filters(leg3Options, settings, spreadGeneratorResults, token);
            }

            List<List<Option>> leg1OptionsGroup = GroupLegOptionsByStrike(type, leg1Options);
            List<List<Option>> leg2OptionsGroup = GroupLegOptionsByStrike(type, leg2Options);
            List<List<Option>> leg3OptionsGroup = GroupLegOptionsByStrike(type, leg3Options);
            List<DateTime> expirationDates = optionChain.Select(x => x.Expiration.Date).Distinct().ToList();
            List<List<SpreadHolder>> results = GenerateSpreads(leg1OptionsGroup, leg2OptionsGroup, leg3OptionsGroup, type, expirationDates, settings, token, isSample);

            if (!isSample)
            {
                List<Task<List<SpreadHolder>>> validResults = new();
                foreach (List<SpreadHolder> groupResult in results)
                {
                    validResults.Add(ApplySpreadFilters(settings, spreadGeneratorResults, groupResult, optionChain, token));
                }
                await Task.WhenAll(validResults);
                results = validResults.Select(x => x.Result).ToList();
            }

            if (results.Count != 0 && count != int.MaxValue)
            {
                results = SpreadGeneratorHelper.DistibuteWithDynamicQuota(results, count, token);
            }

            foreach (List<SpreadHolder> result in results)
            {
                token.ThrowIfCancellationRequested();
                SpreadHolder[] groupResult;

                groupResult = result.ToArray();

                foreach (SpreadHolder spread in groupResult)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    string tos = leg1.Symbol + "-2*" + leg2.Symbol + "+" + leg3.Symbol;

                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1));
                    spreadModel.Legs.Add(new SpreadLeg(leg2, Side.Sell, 2));
                    spreadModel.Legs.Add(new SpreadLeg(leg3));

                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
            }

            results.Clear();

            stopwatch.Stop();

            _logger.LogInformation($"[Finish] {nameof(CalendarButterflySpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options, " +
                      $"took {stopwatch.ElapsedMilliseconds}ms, " +
                      $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                      $"with {spreadGeneratorResults.Errors.Count} errors.");
            return spreadGeneratorResults;
        }

        private void RequestData(List<Option> optionChain, ICalendarButterflySpreadsGeneratorSettings settings)
        {
            if (settings.DataRequested())
            {
                if (settings.Leg1DeltaRangeEnabled ||
                    settings.Leg2DeltaRangeEnabled ||
                    settings.Leg3DeltaRangeEnabled ||
                    settings.SpreadDeltaRangeEnabled)
                {
                    _deltaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Delta);
                }

                if (settings.Leg1TheoRangeEnabled ||
                   settings.Leg2TheoRangeEnabled ||
                   settings.Leg3TheoRangeEnabled ||
                   settings.SpreadTheoRangeEnabled ||
                   settings.SpreadTheoAboveMidEnabled ||
                   settings.SpreadTheoBelowMidEnabled ||
                   settings.SpreadTheoToMidRangeEnabled ||
                   settings.SpreadVolaToHanweckDiffEnabled ||
                   settings.SpreadTheoAbsMidEnabled)
                {
                    _theoStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.TheorethicalValue);
                    if (settings.SpreadVolaToHanweckDiffEnabled || settings.TheoModel != TheoModel.Hanw)
                    {
                        _volaStore.GetVolaDataFor(optionChain, SubscriptionFieldType.TheorethicalValue);
                    }
                }

                if (settings.SpreadEmaToMidRangeEnabled)
                {
                    _emaStore.GetEmaDataFor(optionChain, SubscriptionFieldType.FullEma);
                }

                if (settings.Leg1VegaRangeEnabled ||
                    settings.Leg1WeightedVegaRangeEnabled ||
                    settings.Leg2VegaRangeEnabled ||
                    settings.Leg2WeightedVegaRangeEnabled ||
                    settings.Leg3VegaRangeEnabled ||
                    settings.Leg3WeightedVegaRangeEnabled ||
                    settings.SpreadVegaRangeEnabled ||
                    settings.WeightedVegaRangeEnabled)
                {
                    _vegaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Vega);
                }

                if (settings.Leg1MarketRangeEnabled ||
                    settings.Leg2MarketRangeEnabled ||
                    settings.Leg3MarketRangeEnabled ||
                    settings.SpreadMarketRangeEnabled ||
                    settings.Leg1WidthRangeEnabled ||
                    settings.Leg2WidthRangeEnabled ||
                    settings.Leg3WidthRangeEnabled ||
                    settings.SpreadWidthRangeEnabled ||
                    settings.SpreadTheoAboveMidEnabled ||
                    settings.SpreadTheoBelowMidEnabled ||
                    settings.SpreadEmaToMidRangeEnabled ||
                    settings.SpreadTheoToMidRangeEnabled ||
                    settings.WidthSortingEnabled ||
                    settings.SpreadTheoAbsMidEnabled)
                {
                    _bidStore.GetQuoteDataFor(optionChain, SubscriptionFieldType.Bid);
                    _askStore.GetQuoteDataFor(optionChain, SubscriptionFieldType.Ask);
                }
            }
        }

        private async Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, ICalendarButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        {
            List<Option>? leg1Options = optionChain.ToList();

            if (settings.Leg1StrikeRangeEnabled)
            {
                leg1Options = leg1Options.Where(x => x.Strike >= settings.Leg1StrikeRangeFloor &&
                                                     x.Strike <= settings.Leg1StrikeRangeCeil).ToList();
            }

            if (settings.Leg1DeltaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg1Delta = Math.Abs(await _deltaStore.GetDataAsync(leg1.Symbol));
                    if (double.IsNaN(leg1Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg1.Symbol);
                    }
                    else if (leg1Delta >= settings.Leg1DeltaRangeFloor &&
                             leg1Delta <= settings.Leg1DeltaRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            if (settings.Leg1TheoRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg1Theo = settings.TheoModel == TheoModel.Hanw ? Math.Abs(await _theoStore.GetDataAsync(leg1.Symbol)) : Math.Abs(await _volaStore.GetDataAsync(leg1.Symbol));
                    if (double.IsNaN(leg1Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg1.Symbol);
                    }
                    else if (leg1Theo >= settings.Leg1TheoRangeFloor &&
                             leg1Theo <= settings.Leg1TheoRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            if (settings.Leg1VegaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg1Vega = Math.Abs(await _vegaStore.GetDataAsync(leg1.Symbol));
                    if (double.IsNaN(leg1Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg1.Symbol);
                    }
                    else if (leg1Vega >= settings.Leg1VegaRangeFloor &&
                             leg1Vega <= settings.Leg1VegaRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            if (settings.Leg1WeightedVegaRangeEnabled && leg1Options.Count > 0)
            {
                List<Option> selected = new();

                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();

                    double leg1WeightedVega = Math.Abs(await _wVegaStore.GetDataAsync(leg1.Symbol));

                    if (leg1WeightedVega >= settings.Leg1WeightedVegaRangeFloor &&
                        leg1WeightedVega <= settings.Leg1WeightedVegaRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            if (settings.Leg1MarketRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();
                    double bid = Math.Abs(await _bidStore.GetDataAsync(leg1.Symbol));
                    double ask = Math.Abs(await _askStore.GetDataAsync(leg1.Symbol));

                    if (double.IsNaN(bid) ||
                        double.IsNaN(ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg1.Symbol);
                    }

                    if (bid >= settings.Leg1MarketRangeFloor &&
                        ask <= settings.Leg1MarketRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            if (settings.Leg1WidthRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();
                    double legWidth = Math.Abs(await _bidStore.GetDataAsync(leg1.Symbol) -
                                               await _askStore.GetDataAsync(leg1.Symbol));

                    if (double.IsNaN(legWidth))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg1.Symbol);
                    }
                    else if (legWidth >= settings.Leg1WidthRangeFloor &&
                             legWidth <= settings.Leg1WidthRangeCeil)
                    {
                        selected.Add(leg1);
                    }
                }
                leg1Options = selected;
            }

            return leg1Options;
        }

        private async Task<List<Option>> ApplyLeg2Filters(List<Option> optionChain, ICalendarButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        {
            List<Option>? leg2Options = optionChain.ToList();

            if (settings.Leg2StrikeRangeEnabled)
            {
                leg2Options = leg2Options.Where(x => x.Strike >= settings.Leg2StrikeRangeFloor &&
                                                     x.Strike <= settings.Leg2StrikeRangeCeil).ToList();
            }

            if (settings.Leg2DeltaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg2Delta = Math.Abs(await _deltaStore.GetDataAsync(leg2.Symbol));
                    if (double.IsNaN(leg2Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg2.Symbol);
                    }
                    else if (leg2Delta >= settings.Leg2DeltaRangeFloor &&
                             leg2Delta <= settings.Leg2DeltaRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            if (settings.Leg2TheoRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg2Theo = settings.TheoModel == TheoModel.Hanw ? Math.Abs(await _theoStore.GetDataAsync(leg2.Symbol)) : Math.Abs(await _volaStore.GetDataAsync(leg2.Symbol));
                    if (double.IsNaN(leg2Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg2.Symbol);
                    }
                    else if (leg2Theo >= settings.Leg2TheoRangeFloor &&
                             leg2Theo <= settings.Leg2TheoRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            if (settings.Leg2VegaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg2Vega = Math.Abs(await _vegaStore.GetDataAsync(leg2.Symbol));
                    if (double.IsNaN(leg2Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg2.Symbol);
                    }
                    else if (leg2Vega >= settings.Leg2VegaRangeFloor &&
                             leg2Vega <= settings.Leg2VegaRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            if (settings.Leg2WeightedVegaRangeEnabled && leg2Options.Count > 0)
            {
                List<Option> selected = new();

                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();

                    double leg2WeightedVega = Math.Abs(await _wVegaStore.GetDataAsync(leg2.Symbol));

                    if (leg2WeightedVega >= settings.Leg2WeightedVegaRangeFloor &&
                        leg2WeightedVega <= settings.Leg2WeightedVegaRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            if (settings.Leg2MarketRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();
                    double bid = Math.Abs(await _bidStore.GetDataAsync(leg2.Symbol));
                    double ask = Math.Abs(await _askStore.GetDataAsync(leg2.Symbol));

                    if (double.IsNaN(bid) ||
                        double.IsNaN(ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg2.Symbol);
                    }

                    if (bid >= settings.Leg2MarketRangeFloor &&
                        ask <= settings.Leg2MarketRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            if (settings.Leg2WidthRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg2 in leg2Options)
                {
                    token.ThrowIfCancellationRequested();
                    double legWidth = Math.Abs(await _bidStore.GetDataAsync(leg2.Symbol) -
                                               await _askStore.GetDataAsync(leg2.Symbol));

                    if (double.IsNaN(legWidth))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg2.Symbol);
                    }
                    else if (legWidth >= settings.Leg2WidthRangeFloor &&
                             legWidth <= settings.Leg2WidthRangeCeil)
                    {
                        selected.Add(leg2);
                    }
                }
                leg2Options = selected;
            }

            return leg2Options;
        }

        private async Task<List<Option>> ApplyLeg3Filters(List<Option> optionChain, ICalendarButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        {
            List<Option>? leg3Options = optionChain.ToList();

            if (settings.Leg3StrikeRangeEnabled)
            {
                leg3Options = leg3Options.Where(x => x.Strike >= settings.Leg3StrikeRangeFloor &&
                                                     x.Strike <= settings.Leg3StrikeRangeCeil).ToList();
            }

            if (settings.Leg3DeltaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg3Delta = Math.Abs(await _deltaStore.GetDataAsync(leg3.Symbol));
                    if (double.IsNaN(leg3Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg3.Symbol);
                    }
                    else if (leg3Delta >= settings.Leg3DeltaRangeFloor &&
                             leg3Delta <= settings.Leg3DeltaRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            if (settings.Leg3TheoRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg3Theo = settings.TheoModel == TheoModel.Hanw ? Math.Abs(await _theoStore.GetDataAsync(leg3.Symbol)) : Math.Abs(await _volaStore.GetDataAsync(leg3.Symbol));
                    if (double.IsNaN(leg3Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg3.Symbol);
                    }
                    else if (leg3Theo >= settings.Leg3TheoRangeFloor &&
                             leg3Theo <= settings.Leg3TheoRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            if (settings.Leg3VegaRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();
                    double leg3Vega = Math.Abs(await _vegaStore.GetDataAsync(leg3.Symbol));
                    if (double.IsNaN(leg3Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg3.Symbol);
                    }
                    else if (leg3Vega >= settings.Leg3VegaRangeFloor &&
                             leg3Vega <= settings.Leg3VegaRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            if (settings.Leg3WeightedVegaRangeEnabled && leg3Options.Count > 0)
            {
                List<Option> selected = new();

                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();

                    double leg3WeightedVega = Math.Abs(await _wVegaStore.GetDataAsync(leg3.Symbol));

                    if (leg3WeightedVega >= settings.Leg3WeightedVegaRangeFloor &&
                        leg3WeightedVega <= settings.Leg3WeightedVegaRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            if (settings.Leg3MarketRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();
                    double bid = Math.Abs(await _bidStore.GetDataAsync(leg3.Symbol));
                    double ask = Math.Abs(await _askStore.GetDataAsync(leg3.Symbol));

                    if (double.IsNaN(bid) ||
                        double.IsNaN(ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg3.Symbol);
                    }

                    if (bid >= settings.Leg3MarketRangeFloor &&
                        ask <= settings.Leg3MarketRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            if (settings.Leg3WidthRangeEnabled)
            {
                List<Option> selected = new();
                foreach (Option? leg3 in leg3Options)
                {
                    token.ThrowIfCancellationRequested();
                    double legWidth = Math.Abs(await _bidStore.GetDataAsync(leg3.Symbol) -
                                               await _askStore.GetDataAsync(leg3.Symbol));

                    if (double.IsNaN(legWidth))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg3.Symbol);
                    }
                    else if (legWidth >= settings.Leg3WidthRangeFloor &&
                             legWidth <= settings.Leg3WidthRangeCeil)
                    {
                        selected.Add(leg3);
                    }
                }
                leg3Options = selected;
            }

            return leg3Options;
        }

        private async Task<List<SpreadHolder>> ApplySpreadFilters(ICalendarButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
        {
            if (settings.SpreadDeltaRangeEnabled)
            {
                List<SpreadHolder> passed = new(results.Count);
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Delta = await _deltaStore.GetDataAsync(leg1.Symbol);
                    double leg2Delta = await _deltaStore.GetDataAsync(leg2.Symbol);
                    double leg3Delta = await _deltaStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg3.Symbol);
                        continue;
                    }

                    double spreadDelta = Math.Abs(leg1Delta - 2 * leg2Delta + leg3Delta);
                    if (spreadDelta < settings.SpreadDeltaRangeFloor ||
                        spreadDelta > settings.SpreadDeltaRangeCeil)
                    {
                        continue;
                    }
                    passed.Add(spread);
                }
                results = passed;
            }

            if (settings.SpreadTheoRangeEnabled)
            {
                List<SpreadHolder> passed = new(results.Count);
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg1.Symbol) : await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg2.Symbol) : await _volaStore.GetDataAsync(leg2.Symbol);
                    double leg3Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg3.Symbol) : await _volaStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg3.Symbol);
                        continue;
                    }

                    double spreadTheo = leg1Theo - 2 * leg2Theo + leg3Theo;
                    if (spreadTheo < settings.SpreadTheoRangeFloor ||
                        spreadTheo > settings.SpreadTheoRangeCeil)
                    {
                        continue;
                    }
                    passed.Add(spread);
                }
                results = passed;
            }

            if (settings.SpreadVegaRangeEnabled && results.Count > 0)
            {
                List<SpreadHolder> passed = new(results.Count);

                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Vega = await _vegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _vegaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vega = await _vegaStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg3.Symbol);
                        continue;
                    }

                    double spreadVega = leg1Vega - 2 * leg2Vega + leg3Vega;

                    if (settings.SpreadVegaRangeEnabled && (
                        spreadVega < settings.SpreadVegaRangeFloor ||
                        spreadVega > settings.SpreadVegaRangeCeil))
                    {
                        continue;
                    }

                    passed.Add(spread);
                }
                results = passed;
            }

            if (settings.WeightedVegaRangeEnabled && results.Count > 0)
            {
                List<SpreadHolder> passed = new(results.Count);

                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Vega = await _wVegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _wVegaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vega = await _wVegaStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg3.Symbol);
                        continue;
                    }

                    double spreadVega = leg1Vega - 2 * leg2Vega + leg3Vega;

                    if (settings.WeightedVegaRangeEnabled && (
                        spreadVega < settings.WeightedVegaRangeFloor ||
                        spreadVega > settings.WeightedVegaRangeCeil))
                    {
                        continue;
                    }

                    passed.Add(spread);
                }
                results = passed;
            }

            if (settings.SpreadVolaToHanweckDiffEnabled)
            {
                List<SpreadHolder> passed = new(results.Count);
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Theo = await _theoStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = await _theoStore.GetDataAsync(leg2.Symbol);
                    double leg3Theo = await _theoStore.GetDataAsync(leg3.Symbol);

                    double leg1Vola = await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vola = await _volaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vola = await _volaStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg3.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg1Vola))
                    {
                        spreadGeneratorResults.Errors.Add("Vola not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Vola))
                    {
                        spreadGeneratorResults.Errors.Add("Vola not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Vola))
                    {
                        spreadGeneratorResults.Errors.Add("Vola not found for " + leg3.Symbol);
                        continue;
                    }

                    double spreadTheo = leg1Theo - 2 * leg2Theo + leg3Theo;
                    double spreadVola = leg1Vola - 2 * leg2Vola + leg3Vola;

                    var diff = Math.Abs(spreadTheo - spreadVola);

                    if (diff < settings.SpreadVolaToHanweckDiff)
                    {
                        continue;
                    }

                    passed.Add(spread);
                }

                results = passed;
            }

            if (settings.SpreadWidthRangeEnabled ||
                settings.SpreadMarketRangeEnabled ||
                settings.SpreadTheoAboveMidEnabled ||
                settings.SpreadTheoBelowMidEnabled ||
                settings.SpreadEmaToMidRangeEnabled ||
                settings.SpreadTheoToMidRangeEnabled ||
                settings.WidthSortingEnabled ||
                settings.SpreadTheoAbsMidEnabled)
            {
                List<SpreadHolder> passed = new(results.Count);
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];

                    double leg1Bid = await _bidStore.GetDataAsync(leg1.Symbol);
                    double leg1Ask = await _askStore.GetDataAsync(leg1.Symbol);
                    double leg2Bid = await _bidStore.GetDataAsync(leg2.Symbol);
                    double leg2Ask = await _askStore.GetDataAsync(leg2.Symbol);
                    double leg3Bid = await _bidStore.GetDataAsync(leg3.Symbol);
                    double leg3Ask = await _askStore.GetDataAsync(leg3.Symbol);

                    if (double.IsNaN(leg1Bid) || double.IsNaN(leg1Ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg1.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg2Bid) || double.IsNaN(leg2Ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg2.Symbol);
                        continue;
                    }

                    if (double.IsNaN(leg3Bid) || double.IsNaN(leg3Ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg3.Symbol);
                        continue;
                    }

                    // For buys  + _bidStore
                    // For sells - _askStore
                    double low = leg1Bid - 2 * leg2Ask + leg3Bid;

                    // For buys  + _askStore
                    // For sells - _bidStore
                    double high = leg1Ask - 2 * leg2Bid + leg3Ask;

                    if (settings.SpreadMarketRangeEnabled)
                    {
                        if (low < settings.SpreadMarketRangeFloor ||
                            high > settings.SpreadMarketRangeCeil)
                        {
                            continue;
                        }
                    }

                    double spreadWidth = Math.Abs(low - high);
                    if (settings.SpreadWidthRangeEnabled)
                    {

                        if (spreadWidth < settings.SpreadWidthRangeFloor ||
                            spreadWidth > settings.SpreadWidthRangeCeil)
                        {
                            continue;
                        }
                    }
                    spread.Width = spreadWidth;


                    if (settings.SpreadTheoAboveMidEnabled ||
                        settings.SpreadTheoBelowMidEnabled ||
                        settings.SpreadTheoToMidRangeEnabled ||
                        settings.SpreadTheoAbsMidEnabled)
                    {

                        double leg1Theo = await _theoStore.GetDataAsync(leg1.Symbol);
                        double leg2Theo = await _theoStore.GetDataAsync(leg2.Symbol);
                        double leg3Theo = await _theoStore.GetDataAsync(leg3.Symbol);

                        if (double.IsNaN(leg1Theo))
                        {
                            spreadGeneratorResults.Errors.Add("Theo not found for " + leg1.Symbol);
                            continue;
                        }

                        if (double.IsNaN(leg2Theo))
                        {
                            spreadGeneratorResults.Errors.Add("Theo not found for " + leg2.Symbol);
                            continue;
                        }

                        if (double.IsNaN(leg3Theo))
                        {
                            spreadGeneratorResults.Errors.Add("Theo not found for " + leg3.Symbol);
                            continue;
                        }

                        double spreadTheo = leg1Theo - 2 * leg2Theo + leg3Theo;
                        double spreadMid = (low + high) / 2;

                        if (settings.SpreadTheoAboveMidEnabled &&
                            Math.Round(spreadTheo, 2) - Math.Round(spreadMid, 2) < settings.SpreadTheoAboveMid)
                        {
                            continue;
                        }

                        if (settings.SpreadTheoBelowMidEnabled &&
                            Math.Round(spreadMid, 2) - Math.Round(spreadTheo, 2) < settings.SpreadTheoBelowMid)
                        {
                            continue;
                        }

                        if (settings.SpreadTheoAbsMidEnabled &&
                            Math.Abs(Math.Round(spreadTheo, 2) - Math.Round(spreadMid, 2)) > settings.SpreadTheoAbsMid)
                        {
                            continue;
                        }

                        if (settings.SpreadTheoToMidRangeEnabled &&
                            (spreadTheo - spreadMid < settings.SpreadTheoToMidRangeFloor || spreadTheo - spreadMid > settings.SpreadTheoToMidRangeCeil))
                        {
                            continue;
                        }
                    }

                    if (settings.SpreadEmaToMidRangeEnabled)
                    {
                        double leg1Ema = await _emaStore.GetDataAsync(leg1.Symbol);
                        double leg2Ema = await _emaStore.GetDataAsync(leg2.Symbol);
                        double leg3Ema = await _emaStore.GetDataAsync(leg3.Symbol);

                        if (double.IsNaN(leg1Ema))
                        {
                            spreadGeneratorResults.Errors.Add("Ema not found for " + leg1.Symbol);
                            continue;
                        }

                        if (double.IsNaN(leg2Ema))
                        {
                            spreadGeneratorResults.Errors.Add("Ema not found for " + leg2.Symbol);
                            continue;
                        }

                        if (double.IsNaN(leg3Ema))
                        {
                            spreadGeneratorResults.Errors.Add("Ema not found for " + leg3.Symbol);
                            continue;
                        }

                        double spreadEma = leg1Ema - 2 * leg2Ema + leg3Ema;
                        double spreadMid = (low + high) / 2;

                        if (settings.SpreadEmaToMidRangeEnabled &&
                            (spreadEma - spreadMid < settings.SpreadEmaToMidRangeFloor || spreadEma - spreadMid > settings.SpreadEmaToMidRangeCeil))
                        {
                            continue;
                        }
                    }


                    passed.Add(spread);
                }
                results = passed;
            }

            return results;
        }

        private List<List<SpreadHolder>> GenerateSpreads(List<List<Option>> leg1OptionsGroup,
                                                     List<List<Option>> leg2OptionsGroup,
                                                     List<List<Option>> leg3OptionsGroup,
                                                     PutCall type,
                                                     List<DateTime> expirationDates,
                                                     ICalendarButterflySpreadsGeneratorSettings settings,
                                                     CancellationToken token,
                                                     bool isSample)
        {
            List<double>? spacing1List = settings.SpreadSpacing1ListEnabled
                ? settings.SpreadSpacing1List!.Split(',')
                    .Where(x => double.TryParse(x, out _))
                    .Select(x => double.Parse(x))
                    .Where(x => x != 0)
                    .ToList()
                : null;

            List<double>? spacing2List = settings.SpreadSpacing2ListEnabled
                ? settings.SpreadSpacing2List!.Split(',')
                    .Where(x => double.TryParse(x, out _))
                    .Select(x => double.Parse(x))
                    .Where(x => x != 0)
                    .ToList()
                : null;

            List<int>? expirationGapList = settings.SpreadExpirationGapListEnabled
                ? settings.SpreadExpirationGapList!.Split(',')
                    .Where(x => int.TryParse(x, out _))
                    .Select(x => int.Parse(x))
                    .Where(x => x != 0)
                    .ToList()
                : null;

            bool range1Enabled = settings.SpreadSpacing1RangeEnabled;
            double range1Floor = settings.SpreadSpacing1RangeFloor;
            double range1Ceil = settings.SpreadSpacing1RangeCeil;

            bool range2Enabled = settings.SpreadSpacing2RangeEnabled;
            double range2Floor = settings.SpreadSpacing2RangeFloor;
            double range2Ceil = settings.SpreadSpacing2RangeCeil;

            bool expirationGapRangeEnabled = settings.SpreadExpirationGapRangeEnabled;
            double expirationGapRangeFloor = settings.SpreadExpirationGapRangeFloor;
            double expirationGapRangeCeil = settings.SpreadExpirationGapRangeCeil;

            bool isCall = type == PutCall.Call;

            Dictionary<double, List<Option>> leg2ByStrike = new(leg2OptionsGroup.Count);
            foreach (var group in leg2OptionsGroup)
                if (group.Count > 0)
                    leg2ByStrike[group[0].Strike] = group;

            Dictionary<double, List<Option>> leg3ByStrike = new(leg3OptionsGroup.Count);
            foreach (var group in leg3OptionsGroup)
                if (group.Count > 0)
                    leg3ByStrike[group[0].Strike] = group;

            List<List<SpreadHolder>> results = new();
            foreach (List<Option> leg1Options in leg1OptionsGroup)
            {
                token.ThrowIfCancellationRequested();
                List<SpreadHolder> groupResults = new();
                List<int> leg2Indexes = new();
                List<int> leg3Indexes = new();
                for (int leg1Index = 0; leg1Index < leg1Options.Count; leg1Index++)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = leg1Options[leg1Index];

                    if (!leg2ByStrike.TryGetValue(leg1.Strike, out List<Option>? leg2Options))
                    {
                        continue;
                    }

                    leg2Indexes.Clear();

                    if (spacing1List != null)
                    {
                        foreach (double spacing in spacing1List)
                        {
                            token.ThrowIfCancellationRequested();
                            Option? leg2WithCurrentSpacing = leg2Options.FirstOrDefault(x => x.Expiration.Date == leg1.Expiration.Date + TimeSpan.FromDays(isCall ? spacing : -spacing));

                            if (leg2WithCurrentSpacing == null)
                            {
                                continue;
                            }

                            leg2Indexes.Add(leg2Options.IndexOf(leg2WithCurrentSpacing));
                        }
                    }
                    else
                    {
                        Option? leg2 = isCall
                            ? leg2Options.FirstOrDefault(x => x.Expiration.Date > leg1.Expiration.Date)
                            : leg2Options.FirstOrDefault(x => x.Expiration.Date < leg1.Expiration.Date);

                        if (leg2 == null)
                        {
                            continue;
                        }

                        int leg2StartIndex = leg2Options.IndexOf(leg2);
                        int leg2EndIndex = leg2Options.Count;

                        if (range1Enabled)
                        {
                            Option? leg2Start = leg2Options.Skip(leg2StartIndex)
                                                       .FirstOrDefault(x => Math.Abs((leg1.Expiration.Date - x.Expiration.Date).TotalDays) >= range1Floor);
                            Option? leg2End = leg2Options.Skip(leg2StartIndex)
                                                     .LastOrDefault(x => Math.Abs((leg1.Expiration.Date - x.Expiration.Date).TotalDays) <= range1Ceil);
                            if (leg2Start == null || leg2End == null)
                            {
                                continue;
                            }
                            else
                            {
                                leg2StartIndex = leg2Options.IndexOf(leg2Start);
                                leg2EndIndex = leg2Options.IndexOf(leg2End) + 1;
                            }
                        }

                        for (int i = leg2StartIndex; i < leg2EndIndex && i < leg2Options.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            leg2Indexes.Add(i);
                        }
                    }


                    if (expirationGapList != null)
                    {
                        switch (leg1.PutCall)
                        {
                            case PutCall.Put:
                                leg2Indexes.RemoveAll(i => !expirationGapList.Contains(expirationDates.Count(ex => ex < leg1.Expiration.Date && ex > leg2Options[i].Expiration.Date)));
                                break;
                            case PutCall.Call:
                                leg2Indexes.RemoveAll(i => !expirationGapList.Contains(expirationDates.Count(ex => ex > leg1.Expiration.Date && ex < leg2Options[i].Expiration.Date)));
                                break;
                        }
                    }

                    if (expirationGapRangeEnabled)
                    {
                        switch (leg1.PutCall)
                        {
                            case PutCall.Put:
                                leg2Indexes.RemoveAll(i => expirationDates.Count(ex => ex < leg1.Expiration.Date && ex > leg2Options[i].Expiration.Date) < expirationGapRangeFloor ||
                                                          expirationDates.Count(ex => ex < leg1.Expiration.Date && ex > leg2Options[i].Expiration.Date) > expirationGapRangeCeil);
                                break;
                            case PutCall.Call:
                                leg2Indexes.RemoveAll(i => expirationDates.Count(ex => ex > leg1.Expiration.Date && ex < leg2Options[i].Expiration.Date) < expirationGapRangeFloor ||
                                                          expirationDates.Count(ex => ex > leg1.Expiration.Date && ex < leg2Options[i].Expiration.Date) > expirationGapRangeCeil);
                                break;
                        }
                    }

                    foreach (int leg2Index in leg2Indexes)
                    {
                        token.ThrowIfCancellationRequested();
                        Option? leg2 = leg2Options[leg2Index];

                        if (!leg3ByStrike.TryGetValue(leg2.Strike, out List<Option>? leg3Options))
                        {
                            continue;
                        }

                        leg3Indexes.Clear();

                        if (spacing2List != null)
                        {
                            foreach (double spacing in spacing2List)
                            {
                                token.ThrowIfCancellationRequested();
                                Option? leg3WithCurrentSpacing = leg3Options.FirstOrDefault(x => x.Expiration.Date == leg2.Expiration.Date + TimeSpan.FromDays(isCall ? spacing : -spacing));

                                if (leg3WithCurrentSpacing == null)
                                {
                                    continue;
                                }

                                leg3Indexes.Add(leg3Options.IndexOf(leg3WithCurrentSpacing));
                            }
                        }
                        else
                        {
                            Option? leg3 = isCall
                                ? leg3Options.FirstOrDefault(x => x.Expiration.Date > leg2.Expiration.Date)
                                : leg3Options.FirstOrDefault(x => x.Expiration.Date < leg2.Expiration.Date);

                            if (leg3 == null)
                            {
                                continue;
                            }

                            int leg3StartIndex = leg3Options.IndexOf(leg3);
                            int leg3EndIndex = leg3Options.Count;

                            if (range2Enabled)
                            {
                                Option? leg3Start = leg3Options.Skip(leg3StartIndex)
                                                         .FirstOrDefault(x => Math.Abs((leg2.Expiration.Date - x.Expiration.Date).TotalDays) >= range2Floor);
                                Option? leg3End = leg3Options.Skip(leg3StartIndex)
                                                       .LastOrDefault(x => Math.Abs((leg2.Expiration.Date - x.Expiration.Date).TotalDays) <= range2Ceil);
                                if (leg3Start == null || leg3End == null)
                                {
                                    continue;
                                }
                                else
                                {
                                    leg3StartIndex = leg3Options.IndexOf(leg3Start);
                                    leg3EndIndex = leg3Options.IndexOf(leg3End) + 1;
                                }
                            }

                            for (int i = leg3StartIndex; i < leg3EndIndex && i < leg3Options.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                leg3Indexes.Add(i);
                            }
                        }


                        if (expirationGapList != null)
                        {
                            switch (leg2.PutCall)
                            {
                                case PutCall.Put:
                                    leg3Indexes.RemoveAll(i => !expirationGapList.Contains(expirationDates.Count(ex => ex < leg2.Expiration.Date && ex > leg3Options[i].Expiration.Date)));
                                    break;
                                case PutCall.Call:
                                    leg3Indexes.RemoveAll(i => !expirationGapList.Contains(expirationDates.Count(ex => ex > leg2.Expiration.Date && ex < leg3Options[i].Expiration.Date)));
                                    break;
                            }
                        }

                        if (expirationGapRangeEnabled)
                        {
                            switch (leg2.PutCall)
                            {
                                case PutCall.Put:
                                    leg3Indexes.RemoveAll(i => expirationDates.Count(ex => ex < leg2.Expiration.Date && ex > leg3Options[i].Expiration.Date) < expirationGapRangeFloor ||
                                                              expirationDates.Count(ex => ex < leg2.Expiration.Date && ex > leg3Options[i].Expiration.Date) > expirationGapRangeCeil);
                                    break;
                                case PutCall.Call:
                                    leg3Indexes.RemoveAll(i => expirationDates.Count(ex => ex > leg2.Expiration.Date && ex < leg3Options[i].Expiration.Date) < expirationGapRangeFloor ||
                                                              expirationDates.Count(ex => ex > leg2.Expiration.Date && ex < leg3Options[i].Expiration.Date) > expirationGapRangeCeil);
                                    break;
                            }
                        }


                        foreach (int leg3Index in leg3Indexes)
                        {
                            token.ThrowIfCancellationRequested();
                            Option? leg3 = leg3Options[leg3Index];

                            if (Math.Abs((leg1.Expiration.Date - leg2.Expiration.Date).TotalDays) == Math.Abs((leg2.Expiration.Date - leg3.Expiration.Date).TotalDays))
                            {
                                continue;
                            }

                            groupResults.Add(new SpreadHolder(leg1, leg2, leg3));

                            if (isSample)
                            {
                                results.Add(groupResults);
                                return results;
                            }
                        }
                    }
                }
                if (groupResults.Count > 0)
                {
                    results.Add(groupResults);
                }
            }

            return results;
        }

    }
}