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
    public class ButterflySpreadsGenerator : SpreadGenerator
    {
        public ButterflySpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
            : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
        {
        }

        public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option> leg2Options, List<Option> leg3Options, IButterflySpreadsGeneratorSettings butterflySpreadsSettings, int count, CancellationToken token, bool isSample = false)
        {
            List<Option> optionChain = leg1Options.Union(leg2Options).Union(leg3Options).Distinct().ToList();
            Option? option = optionChain.FirstOrDefault();
            SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.Butterfly);
            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            var type = option.PutCall;
            _logger.LogInformation($"[Start] {nameof(ButterflySpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options.");

            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (!isSample)
            {
                RequestData(optionChain, butterflySpreadsSettings);

                leg1Options = await ApplyLeg1Filters(leg1Options, butterflySpreadsSettings, spreadGeneratorResults, token);
                leg2Options = await ApplyLeg2Filters(leg2Options, butterflySpreadsSettings, spreadGeneratorResults, token);
                leg3Options = await ApplyLeg3Filters(leg3Options, butterflySpreadsSettings, spreadGeneratorResults, token);
            }

            List<List<Option>> leg1OptionsGroup = GroupLegOptions(type, leg1Options);
            List<List<Option>> leg2OptionsGroup = GroupLegOptions(type, leg2Options);
            List<List<Option>> leg3OptionsGroup = GroupLegOptions(type, leg3Options);

            List<List<SpreadHolder>> results = GenerateSpreads(leg1OptionsGroup, leg2OptionsGroup, leg3OptionsGroup, type, butterflySpreadsSettings, token, isSample);

            if (!isSample)
            {
                List<Task<List<SpreadHolder>>> validResults = new();
                foreach (List<SpreadHolder> groupResult in results)
                {
                    validResults.Add(ApplySpreadFilters(butterflySpreadsSettings, spreadGeneratorResults, groupResult, optionChain, token));
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

            _logger.LogInformation($"[Finish] {nameof(ButterflySpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options, " +
                      $"took {stopwatch.ElapsedMilliseconds}ms, " +
                      $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                      $"with {spreadGeneratorResults.Errors.Count} errors.");
            return spreadGeneratorResults;
        }

        private void RequestData(List<Option> optionChain, IButterflySpreadsGeneratorSettings settings)
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

        private async Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, IButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
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

        private async Task<List<Option>> ApplyLeg2Filters(List<Option> optionChain, IButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
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

        private async Task<List<Option>> ApplyLeg3Filters(List<Option> optionChain, IButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
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

        private async Task<List<SpreadHolder>> ApplySpreadFilters(IButterflySpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
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
                bool needsQuoteFilter = settings.SpreadWidthRangeEnabled ||
                    settings.SpreadMarketRangeEnabled ||
                    settings.SpreadTheoAboveMidEnabled ||
                    settings.SpreadTheoBelowMidEnabled ||
                    settings.SpreadEmaToMidRangeEnabled ||
                    settings.SpreadTheoToMidRangeEnabled ||
                    settings.SpreadTheoAbsMidEnabled;

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

                    bool quotesAvailable = !(double.IsNaN(leg1Bid) || double.IsNaN(leg1Ask) ||
                                              double.IsNaN(leg2Bid) || double.IsNaN(leg2Ask) ||
                                              double.IsNaN(leg3Bid) || double.IsNaN(leg3Ask));

                    if (!quotesAvailable)
                    {
                        if (needsQuoteFilter)
                        {
                            if (double.IsNaN(leg1Bid) || double.IsNaN(leg1Ask))
                                spreadGeneratorResults.Errors.Add("Quote not found for " + leg1.Symbol);
                            else if (double.IsNaN(leg2Bid) || double.IsNaN(leg2Ask))
                                spreadGeneratorResults.Errors.Add("Quote not found for " + leg2.Symbol);
                            else
                                spreadGeneratorResults.Errors.Add("Quote not found for " + leg3.Symbol);
                        }
                        else
                        {
                            passed.Add(spread);
                        }
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
                                                      IButterflySpreadsGeneratorSettings settings,
                                                      CancellationToken token,
                                                      bool isSample)
        {
            List<double>? spacingList = settings.SpreadSpacingListEnabled
                ? settings.SpreadSpacingList!.Split(',')
                    .Where(x => double.TryParse(x, out _))
                    .Select(x => double.Parse(x))
                    .Where(x => x != 0)
                    .ToList()
                : null;

            bool rangeEnabled = settings.SpreadSpacingRangeEnabled;
            double rangeFloor = settings.SpreadSpacingRangeFloor;
            double rangeCeil = settings.SpreadSpacingRangeCeil;
            bool isCall = type == PutCall.Call;

            Dictionary<DateTime, List<Option>> leg2ByExpiration = new(leg2OptionsGroup.Count);
            foreach (var group in leg2OptionsGroup)
                if (group.Count > 0)
                    leg2ByExpiration[group[0].Expiration] = group;

            Dictionary<DateTime, List<Option>> leg3ByExpiration = new(leg3OptionsGroup.Count);
            foreach (var group in leg3OptionsGroup)
                if (group.Count > 0)
                    leg3ByExpiration[group[0].Expiration] = group;

            Dictionary<DateTime, Dictionary<double, int>> leg2StrikeToIndex = new(leg2ByExpiration.Count);
            foreach (var (exp, list) in leg2ByExpiration)
            {
                var d = new Dictionary<double, int>(list.Count);
                for (int i = 0; i < list.Count; i++)
                    d[list[i].Strike] = i;
                leg2StrikeToIndex[exp] = d;
            }

            Dictionary<DateTime, Dictionary<double, int>> leg3StrikeToIndex = new(leg3ByExpiration.Count);
            foreach (var (exp, list) in leg3ByExpiration)
            {
                var d = new Dictionary<double, int>(list.Count);
                for (int i = 0; i < list.Count; i++)
                    d[list[i].Strike] = i;
                leg3StrikeToIndex[exp] = d;
            }

            List<List<SpreadHolder>> results = new();
            List<int> leg2Indexes = new();
            foreach (List<Option> leg1Options in leg1OptionsGroup)
            {
                token.ThrowIfCancellationRequested();
                List<SpreadHolder> groupResults = new();
                for (int leg1Index = 0; leg1Index < leg1Options.Count; leg1Index++)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = leg1Options[leg1Index];

                    if (!leg2ByExpiration.TryGetValue(leg1.Expiration, out List<Option>? leg2Options))
                    {
                        continue;
                    }

                    leg2Indexes.Clear();

                    if (spacingList != null)
                    {
                        foreach (double spacing in spacingList)
                        {
                            token.ThrowIfCancellationRequested();
                            double targetStrike = isCall ? leg1.Strike + spacing : leg1.Strike - spacing;

                            if (!leg2StrikeToIndex[leg1.Expiration].TryGetValue(targetStrike, out int leg2Idx))
                            {
                                continue;
                            }

                            leg2Indexes.Add(leg2Idx);
                        }
                    }
                    else
                    {
                        Option? leg2 = isCall
                            ? leg2Options.FirstOrDefault(x => x.Strike > leg1.Strike)
                            : leg2Options.FirstOrDefault(x => x.Strike < leg1.Strike);

                        if (leg2 == null)
                        {
                            continue;
                        }

                        int leg2StartIndex = leg2StrikeToIndex[leg1.Expiration][leg2.Strike];
                        int leg2EndIndex = leg2Options.Count;

                        if (rangeEnabled)
                        {
                            Option? leg2Start = leg2Options.Skip(leg2StartIndex)
                                                       .FirstOrDefault(x => Math.Abs(leg1.Strike - x.Strike) >= rangeFloor);
                            Option? leg2End = leg2Options.Skip(leg2StartIndex)
                                                     .LastOrDefault(x => Math.Abs(leg1.Strike - x.Strike) <= rangeCeil);
                            if (leg2Start == null || leg2End == null)
                            {
                                continue;
                            }
                            else
                            {
                                leg2StartIndex = leg2StrikeToIndex[leg1.Expiration][leg2Start.Strike];
                                leg2EndIndex = leg2StrikeToIndex[leg1.Expiration][leg2End.Strike] + 1;
                            }
                        }

                        for (int i = leg2StartIndex; i < leg2EndIndex && i < leg2Options.Count; i++)
                        {
                            token.ThrowIfCancellationRequested();
                            leg2Indexes.Add(i);
                        }
                    }

                    foreach (int leg2Index in leg2Indexes)
                    {
                        token.ThrowIfCancellationRequested();
                        Option? leg2 = leg2Options[leg2Index];

                        if (!leg3ByExpiration.TryGetValue(leg2.Expiration, out List<Option>? leg3Group))
                        {
                            continue;
                        }

                        double leg3Strike = leg2.Strike + (leg2.Strike - leg1.Strike);

                        if (!leg3StrikeToIndex[leg2.Expiration].TryGetValue(leg3Strike, out int leg3Idx))
                        {
                            continue;
                        }

                        Option? leg3 = leg3Group[leg3Idx];

                        groupResults.Add(new SpreadHolder(leg1, leg2, leg3));

                        if (isSample)
                        {
                            results.Add(groupResults);
                            return results;
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