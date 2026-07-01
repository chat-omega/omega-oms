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
    public class SingleLegSpreadsGenerator : SpreadGenerator
    {
        public SingleLegSpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
            : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
        {
        }

        public async Task<SpreadGeneratorStat> CalculateStatsAsync(List<Option> optionChain, ISingleLegSpreadsGeneratorSettings settings, CancellationToken token)
        {
            SpreadGeneratorStat spreadGeneratorResults = new();
            Option? option = optionChain.FirstOrDefault();
            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            spreadGeneratorResults.Title = option.PutCall.ToString();

            RequestData(optionChain, settings);

            List<Option>? leg1Options = await ApplyLeg1Filters(optionChain, settings, new SpreadGeneratorResults(option?.Underlying?.Symbol, option?.PutCall, Strategy.SingleLeg), token);

            foreach (IGrouping<DateTime, Option> leg1Exp in leg1Options.GroupBy(x => x.Expiration)
                                               .Where(x => x.Count() > 0))
            {
                string title = leg1Exp.Key.ToString("MMM dd yy");
                spreadGeneratorResults.Details.Add(new SpreadGeneratorStat()
                {
                    Title = title,
                    Leg1Count = leg1Exp.Count(),
                });
            }

            spreadGeneratorResults.Leg1Count = spreadGeneratorResults.Details.Sum(x => x.Leg1Count);

            return spreadGeneratorResults;
        }

        public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option>? mustInclude, bool mustIncludeAsAddition, ISingleLegSpreadsGeneratorSettings settings, int count, CancellationToken token, bool isSample = false)
        {
            List<Option> optionChain = leg1Options.Distinct().ToList();
            Option? option = optionChain.FirstOrDefault();
            SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.SingleLeg);
            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            var type = option.PutCall;
            _logger.LogInformation($"[Start] {nameof(SingleLegSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options.");

            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (!isSample)
            {
                RequestData(optionChain, settings);

                leg1Options = await ApplyLeg1Filters(leg1Options, settings, spreadGeneratorResults, token);
            }

            List<List<Option>> leg1OptionsGroup = GroupLegOptions(type, leg1Options);

            List<List<SpreadHolder>>? results;

            if (mustInclude is { Count: > 0 } && !mustIncludeAsAddition)
            {
                results = new()
                {
                    mustInclude.Select(x => new SpreadHolder(x)).ToList()
                };
            }
            else
            {
                results = GenerateSpreads(leg1OptionsGroup, type, settings, token, isSample);

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

                if (mustInclude != null && mustInclude.Count > 0 && mustIncludeAsAddition)
                {
                    results.Add(mustInclude.Select(x => new SpreadHolder(x)).ToList());
                }
            }

            if (results.Count != 0 && count != int.MaxValue)
            {
                results = SpreadGeneratorHelper.DistibuteWithDynamicQuota(results, count, token);
            }

            foreach (List<SpreadHolder> result in results)
            {
                token.ThrowIfCancellationRequested();
                SpreadHolder[] groupResult = result.ToArray();

                foreach (SpreadHolder spread in groupResult)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];

                    string tos = leg1.Symbol;
                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1));
                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
            }

            stopwatch.Stop();

            _logger.LogInformation($"[Finish] {nameof(SingleLegSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options, " +
                      $"took {stopwatch.ElapsedMilliseconds}ms, " +
                      $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                      $"with {spreadGeneratorResults.Errors.Count} errors.");
            return spreadGeneratorResults;
        }

        private void RequestData(List<Option> optionChain, ISingleLegSpreadsGeneratorSettings settings)
        {
            if (settings.DataRequested())
            {
                if (settings.Leg1DeltaRangeEnabled)
                {
                    _deltaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Delta);
                }

                if (settings.Leg1TheoRangeEnabled ||
                    settings.SpreadTheoAboveMidEnabled ||
                    settings.SpreadTheoBelowMidEnabled ||
                    settings.SpreadVolaToHanweckDiffEnabled ||
                    settings.SpreadTheoAbsMidEnabled)
                {
                    _theoStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.TheorethicalValue);
                    if (settings.SpreadVolaToHanweckDiffEnabled || settings.TheoModel != TheoModel.Hanw)
                    {
                        _volaStore.GetVolaDataFor(optionChain, SubscriptionFieldType.TheorethicalValue);
                    }
                }

                if (settings.Leg1VegaRangeEnabled ||
                    settings.Leg1WeightedVegaRangeEnabled)
                {
                    _vegaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Vega);
                }

                if (settings.Leg1MarketRangeEnabled ||
                    settings.Leg1WidthRangeEnabled ||
                    settings.SpreadTheoAboveMidEnabled ||
                    settings.SpreadTheoBelowMidEnabled ||
                    settings.SpreadTheoAbsMidEnabled)
                {
                    _bidStore.GetQuoteDataFor(optionChain, SubscriptionFieldType.Bid);
                    _askStore.GetQuoteDataFor(optionChain, SubscriptionFieldType.Ask);
                }
            }
        }

        private Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, ISingleLegSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg1StrikeRangeEnabled, settings.Leg1StrikeRangeFloor, settings.Leg1StrikeRangeCeil,
                settings.Leg1DeltaRangeEnabled, settings.Leg1DeltaRangeFloor, settings.Leg1DeltaRangeCeil,
                settings.Leg1TheoRangeEnabled, settings.Leg1TheoRangeFloor, settings.Leg1TheoRangeCeil,
                settings.Leg1VegaRangeEnabled, settings.Leg1VegaRangeFloor, settings.Leg1VegaRangeCeil,
                settings.Leg1WeightedVegaRangeEnabled, settings.Leg1WeightedVegaRangeFloor, settings.Leg1WeightedVegaRangeCeil,
                settings.Leg1MarketRangeEnabled, settings.Leg1MarketRangeFloor, settings.Leg1MarketRangeCeil,
                settings.Leg1WidthRangeEnabled, settings.Leg1WidthRangeFloor, settings.Leg1WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private List<List<SpreadHolder>> GenerateSpreads(List<List<Option>> leg1OptionsGroup,
                                                     PutCall type,
                                                     ISingleLegSpreadsGeneratorSettings settings,
                                                     CancellationToken token,
                                                     bool isSample)
        {
            List<List<SpreadHolder>> results = new();
            foreach (List<Option> leg1Options in leg1OptionsGroup)
            {
                token.ThrowIfCancellationRequested();
                List<SpreadHolder> groupResults = new();
                foreach (Option? leg1 in leg1Options)
                {
                    token.ThrowIfCancellationRequested();

                    groupResults.Add(new SpreadHolder (leg1));

                    if (isSample)
                    {
                        results.Add(groupResults);
                        return results;
                    }
                }
                if (groupResults.Count > 0)
                {
                    results.Add(groupResults);
                }
            }

            return results;
        }

        private async Task<List<SpreadHolder>> ApplySpreadFilters(ISingleLegSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
        {
            if (settings.SpreadTheoAboveMidEnabled ||
                settings.SpreadTheoBelowMidEnabled ||
                settings.SpreadTheoAbsMidEnabled)
            {
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    double leg1Bid = await _bidStore.GetDataAsync(leg1.Symbol);
                    double leg1Ask = await _askStore.GetDataAsync(leg1.Symbol);

                    if (double.IsNaN(leg1Bid) || double.IsNaN(leg1Ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg1.Symbol);
                        continue;
                    }

                    double low = leg1Bid;
                    double high = leg1Ask;

                    if (settings.SpreadTheoAboveMidEnabled ||
                        settings.SpreadTheoBelowMidEnabled ||
                        settings.SpreadTheoAbsMidEnabled)
                    {
                        double leg1Theo = await _theoStore.GetDataAsync(leg1.Symbol);

                        if (double.IsNaN(leg1Theo))
                        {
                            spreadGeneratorResults.Errors.Add("Theo not found for " + leg1.Symbol);
                            continue;
                        }

                        double spreadTheo = Math.Abs(leg1Theo);
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
                    }

                    passed.Add(spread);
                }
                results = passed;
            }

            return results;
        }
    }
}