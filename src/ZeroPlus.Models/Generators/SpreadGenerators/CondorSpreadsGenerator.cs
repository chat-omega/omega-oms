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
    public class CondorSpreadsGenerator : SpreadGenerator
    {
        public CondorSpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
            : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
        {
        }

        public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option> leg2Options, List<Option> leg3Options, List<Option> leg4Options, ICondorSpreadsGeneratorSettings condorSpreadsSettings, int count, CancellationToken token, bool isSample = false)
        {
            List<Option> optionChain = leg1Options.Union(leg2Options).Union(leg3Options).Union(leg4Options).Distinct().ToList();
            Option? option = optionChain.FirstOrDefault();
            SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.Condor);
            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            var type = option.PutCall;
            _logger.LogInformation($"[Start] {nameof(CondorSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options.");

            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (!isSample)
            {
                RequestData(optionChain, condorSpreadsSettings);

                leg1Options = await ApplyLeg1Filters(leg1Options, condorSpreadsSettings, spreadGeneratorResults, token);
                leg2Options = await ApplyLeg2Filters(leg2Options, condorSpreadsSettings, spreadGeneratorResults, token);
                leg3Options = await ApplyLeg3Filters(leg3Options, condorSpreadsSettings, spreadGeneratorResults, token);
                leg4Options = await ApplyLeg4Filters(leg4Options, condorSpreadsSettings, spreadGeneratorResults, token);
            }

            List<List<Option>> leg1OptionsGroup = GroupLegOptions(type, leg1Options);
            List<List<Option>> leg2OptionsGroup = GroupLegOptions(type, leg2Options);
            List<List<Option>> leg3OptionsGroup = GroupLegOptions(type, leg3Options);
            List<List<Option>> leg4OptionsGroup = GroupLegOptions(type, leg4Options);

            List<List<SpreadHolder>> results = GenerateSpreads(leg1OptionsGroup, leg2OptionsGroup, leg3OptionsGroup, leg4OptionsGroup, type, condorSpreadsSettings, token, isSample);

            if (!isSample)
            {
                List<Task<List<SpreadHolder>>> validResults = new();
                foreach (List<SpreadHolder> groupResult in results)
                {
                    validResults.Add(ApplySpreadFilters(condorSpreadsSettings, spreadGeneratorResults, groupResult, optionChain, token));
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
                    Option? leg4 = spread[3];

                    string tos = leg1.Symbol + "-" + leg2.Symbol + "-" + leg3.Symbol + "+" + leg4.Symbol;

                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1));
                    spreadModel.Legs.Add(new SpreadLeg(leg2, Side.Sell));
                    spreadModel.Legs.Add(new SpreadLeg(leg3, Side.Sell));
                    spreadModel.Legs.Add(new SpreadLeg(leg4));

                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
            }

            results.Clear();

            stopwatch.Stop();

            _logger.LogInformation($"[Finish] {nameof(CondorSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options, " +
                      $"took {stopwatch.ElapsedMilliseconds}ms, " +
                      $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                      $"with {spreadGeneratorResults.Errors.Count} errors.");
            return spreadGeneratorResults;
        }

        private void RequestData(List<Option> optionChain, ICondorSpreadsGeneratorSettings settings)
        {
            if (settings.DataRequested())
            {
                if (settings.Leg1DeltaRangeEnabled ||
                    settings.Leg2DeltaRangeEnabled ||
                    settings.Leg3DeltaRangeEnabled ||
                    settings.Leg4DeltaRangeEnabled ||
                    settings.SpreadDeltaRangeEnabled)
                {
                    _deltaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Delta);
                }

                if (settings.Leg1TheoRangeEnabled ||
                    settings.Leg2TheoRangeEnabled ||
                    settings.Leg3TheoRangeEnabled ||
                    settings.Leg4TheoRangeEnabled ||
                    settings.SpreadTheoRangeEnabled ||
                    settings.SpreadTheoAboveMidEnabled ||
                    settings.SpreadTheoBelowMidEnabled ||
                    settings.SpreadEmaToMidRangeEnabled ||
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
                    settings.Leg2VegaRangeEnabled ||
                    settings.Leg3VegaRangeEnabled ||
                    settings.Leg4VegaRangeEnabled ||
                    settings.SpreadVegaRangeEnabled ||
                    settings.Leg1WeightedVegaRangeEnabled ||
                    settings.Leg2WeightedVegaRangeEnabled ||
                    settings.Leg3WeightedVegaRangeEnabled ||
                    settings.Leg4WeightedVegaRangeEnabled ||
                    settings.WeightedVegaRangeEnabled)
                {
                    _vegaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Vega);
                }

                if (settings.Leg1MarketRangeEnabled ||
                    settings.Leg2MarketRangeEnabled ||
                    settings.Leg3MarketRangeEnabled ||
                    settings.Leg4MarketRangeEnabled ||
                    settings.SpreadMarketRangeEnabled ||
                    settings.Leg1WidthRangeEnabled ||
                    settings.Leg2WidthRangeEnabled ||
                    settings.Leg3WidthRangeEnabled ||
                    settings.Leg4WidthRangeEnabled ||
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

        private Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, ICondorSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg1StrikeRangeEnabled, settings.Leg1StrikeRangeFloor, settings.Leg1StrikeRangeCeil,
                settings.Leg1DeltaRangeEnabled, settings.Leg1DeltaRangeFloor, settings.Leg1DeltaRangeCeil,
                settings.Leg1TheoRangeEnabled, settings.Leg1TheoRangeFloor, settings.Leg1TheoRangeCeil,
                settings.Leg1VegaRangeEnabled, settings.Leg1VegaRangeFloor, settings.Leg1VegaRangeCeil,
                settings.Leg1WeightedVegaRangeEnabled, settings.Leg1WeightedVegaRangeFloor, settings.Leg1WeightedVegaRangeCeil,
                settings.Leg1MarketRangeEnabled, settings.Leg1MarketRangeFloor, settings.Leg1MarketRangeCeil,
                settings.Leg1WidthRangeEnabled, settings.Leg1WidthRangeFloor, settings.Leg1WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private Task<List<Option>> ApplyLeg2Filters(List<Option> optionChain, ICondorSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg2StrikeRangeEnabled, settings.Leg2StrikeRangeFloor, settings.Leg2StrikeRangeCeil,
                settings.Leg2DeltaRangeEnabled, settings.Leg2DeltaRangeFloor, settings.Leg2DeltaRangeCeil,
                settings.Leg2TheoRangeEnabled, settings.Leg2TheoRangeFloor, settings.Leg2TheoRangeCeil,
                settings.Leg2VegaRangeEnabled, settings.Leg2VegaRangeFloor, settings.Leg2VegaRangeCeil,
                settings.Leg2WeightedVegaRangeEnabled, settings.Leg2WeightedVegaRangeFloor, settings.Leg2WeightedVegaRangeCeil,
                settings.Leg2MarketRangeEnabled, settings.Leg2MarketRangeFloor, settings.Leg2MarketRangeCeil,
                settings.Leg2WidthRangeEnabled, settings.Leg2WidthRangeFloor, settings.Leg2WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private Task<List<Option>> ApplyLeg3Filters(List<Option> optionChain, ICondorSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg3StrikeRangeEnabled, settings.Leg3StrikeRangeFloor, settings.Leg3StrikeRangeCeil,
                settings.Leg3DeltaRangeEnabled, settings.Leg3DeltaRangeFloor, settings.Leg3DeltaRangeCeil,
                settings.Leg3TheoRangeEnabled, settings.Leg3TheoRangeFloor, settings.Leg3TheoRangeCeil,
                settings.Leg3VegaRangeEnabled, settings.Leg3VegaRangeFloor, settings.Leg3VegaRangeCeil,
                settings.Leg3WeightedVegaRangeEnabled, settings.Leg3WeightedVegaRangeFloor, settings.Leg3WeightedVegaRangeCeil,
                settings.Leg3MarketRangeEnabled, settings.Leg3MarketRangeFloor, settings.Leg3MarketRangeCeil,
                settings.Leg3WidthRangeEnabled, settings.Leg3WidthRangeFloor, settings.Leg3WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private Task<List<Option>> ApplyLeg4Filters(List<Option> optionChain, ICondorSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg4StrikeRangeEnabled, settings.Leg4StrikeRangeFloor, settings.Leg4StrikeRangeCeil,
                settings.Leg4DeltaRangeEnabled, settings.Leg4DeltaRangeFloor, settings.Leg4DeltaRangeCeil,
                settings.Leg4TheoRangeEnabled, settings.Leg4TheoRangeFloor, settings.Leg4TheoRangeCeil,
                settings.Leg4VegaRangeEnabled, settings.Leg4VegaRangeFloor, settings.Leg4VegaRangeCeil,
                settings.Leg4WeightedVegaRangeEnabled, settings.Leg4WeightedVegaRangeFloor, settings.Leg4WeightedVegaRangeCeil,
                settings.Leg4MarketRangeEnabled, settings.Leg4MarketRangeFloor, settings.Leg4MarketRangeCeil,
                settings.Leg4WidthRangeEnabled, settings.Leg4WidthRangeFloor, settings.Leg4WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private async Task<List<SpreadHolder>> ApplySpreadFilters(ICondorSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
        {
            if (settings.SpreadDeltaRangeEnabled)
            {
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Delta = await _deltaStore.GetDataAsync(leg1.Symbol);
                    double leg2Delta = await _deltaStore.GetDataAsync(leg2.Symbol);
                    double leg3Delta = await _deltaStore.GetDataAsync(leg3.Symbol);
                    double leg4Delta = await _deltaStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Delta))
                    {
                        spreadGeneratorResults.Errors.Add("Delta not found for " + leg4.Symbol);
                        continue;
                    }

                    double spreadDelta = Math.Abs(leg1Delta - leg2Delta - leg3Delta + leg4Delta);
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
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg1.Symbol) : await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg2.Symbol) : await _volaStore.GetDataAsync(leg2.Symbol);
                    double leg3Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg3.Symbol) : await _volaStore.GetDataAsync(leg3.Symbol);
                    double leg4Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg4.Symbol) : await _volaStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg4.Symbol);
                        continue;
                    }

                    double spreadTheo = leg1Theo - leg2Theo - leg3Theo + leg4Theo;
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
                List<SpreadHolder> passed = new();

                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Vega = await _vegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _vegaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vega = await _vegaStore.GetDataAsync(leg3.Symbol);
                    double leg4Vega = await _vegaStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg4.Symbol);
                        continue;
                    }

                    double spreadVega = leg1Vega - leg2Vega - leg3Vega + leg4Vega;

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
                List<SpreadHolder> passed = new();

                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Vega = await _wVegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _wVegaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vega = await _wVegaStore.GetDataAsync(leg3.Symbol);
                    double leg4Vega = await _wVegaStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Vega))
                    {
                        spreadGeneratorResults.Errors.Add("Vega not found for " + leg4.Symbol);
                        continue;
                    }

                    double spreadVega = leg1Vega - leg2Vega - leg3Vega + leg4Vega;

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
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Theo = await _theoStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = await _theoStore.GetDataAsync(leg2.Symbol);
                    double leg3Theo = await _theoStore.GetDataAsync(leg3.Symbol);
                    double leg4Theo = await _theoStore.GetDataAsync(leg4.Symbol);

                    double leg1Vola = await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vola = await _volaStore.GetDataAsync(leg2.Symbol);
                    double leg3Vola = await _volaStore.GetDataAsync(leg3.Symbol);
                    double leg4Vola = await _volaStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Theo))
                    {
                        spreadGeneratorResults.Errors.Add("Theo not found for " + leg4.Symbol);
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

                    if (double.IsNaN(leg4Vola))
                    {
                        spreadGeneratorResults.Errors.Add("Vola not found for " + leg4.Symbol);
                        continue;
                    }

                    double spreadTheo = leg1Theo - leg2Theo - leg3Theo + leg4Theo;
                    double spreadVola = leg1Vola - leg2Vola - leg3Vola + leg4Vola;

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
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    Option? leg3 = spread[2];
                    Option? leg4 = spread[3];

                    double leg1Bid = await _bidStore.GetDataAsync(leg1.Symbol);
                    double leg1Ask = await _askStore.GetDataAsync(leg1.Symbol);
                    double leg2Bid = await _bidStore.GetDataAsync(leg2.Symbol);
                    double leg2Ask = await _askStore.GetDataAsync(leg2.Symbol);
                    double leg3Bid = await _bidStore.GetDataAsync(leg3.Symbol);
                    double leg3Ask = await _askStore.GetDataAsync(leg3.Symbol);
                    double leg4Bid = await _bidStore.GetDataAsync(leg4.Symbol);
                    double leg4Ask = await _askStore.GetDataAsync(leg4.Symbol);

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

                    if (double.IsNaN(leg4Bid) || double.IsNaN(leg4Ask))
                    {
                        spreadGeneratorResults.Errors.Add("Quote not found for " + leg4.Symbol);
                        continue;
                    }

                    // For buys  + _bidStore
                    // For sells - _askStore
                    double low = leg1Bid - leg2Ask - leg3Ask + leg4Bid;

                    // For buys  + _askStore
                    // For sells - _bidStore
                    double high = leg1Ask - leg2Bid - leg3Bid + leg4Ask;

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
                        double leg4Theo = await _theoStore.GetDataAsync(leg4.Symbol);

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

                        if (double.IsNaN(leg4Theo))
                        {
                            spreadGeneratorResults.Errors.Add("Theo not found for " + leg4.Symbol);
                            continue;
                        }

                        double spreadTheo = leg1Theo - leg2Theo - leg3Theo + leg4Theo;
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
                        double leg4Ema = await _emaStore.GetDataAsync(leg4.Symbol);

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

                        if (double.IsNaN(leg4Ema))
                        {
                            spreadGeneratorResults.Errors.Add("Ema not found for " + leg4.Symbol);
                            continue;
                        }

                        double spreadEma = leg1Ema - leg2Ema - leg3Ema + leg4Ema;
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
                                               List<List<Option>> leg4OptionsGroup,
                                               PutCall type,
                                               ICondorSpreadsGeneratorSettings settings,
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
            List<double>? spacing3List = settings.SpreadSpacing3ListEnabled
                ? settings.SpreadSpacing3List!.Split(',')
                    .Where(x => double.TryParse(x, out _))
                    .Select(x => double.Parse(x))
                    .Where(x => x != 0)
                    .ToList()
                : null;

            bool isCall = type == PutCall.Call;

            bool spacing1RangeEnabled = settings.SpreadSpacing1RangeEnabled;
            double spacing1RangeFloor = settings.SpreadSpacing1RangeFloor;
            double spacing1RangeCeil = settings.SpreadSpacing1RangeCeil;
            bool spacing2RangeEnabled = settings.SpreadSpacing2RangeEnabled;
            double spacing2RangeFloor = settings.SpreadSpacing2RangeFloor;
            double spacing2RangeCeil = settings.SpreadSpacing2RangeCeil;
            bool spacing3RangeEnabled = settings.SpreadSpacing3RangeEnabled;
            double spacing3RangeFloor = settings.SpreadSpacing3RangeFloor;
            double spacing3RangeCeil = settings.SpreadSpacing3RangeCeil;

            Dictionary<DateTime, List<Option>> leg2ByExpiration = new(leg2OptionsGroup.Count);
            foreach (var group in leg2OptionsGroup)
                if (group.Count > 0)
                    leg2ByExpiration[group[0].Expiration] = group;

            Dictionary<DateTime, List<Option>> leg3ByExpiration = new(leg3OptionsGroup.Count);
            foreach (var group in leg3OptionsGroup)
                if (group.Count > 0)
                    leg3ByExpiration[group[0].Expiration] = group;

            Dictionary<DateTime, List<Option>> leg4ByExpiration = new(leg4OptionsGroup.Count);
            foreach (var group in leg4OptionsGroup)
                if (group.Count > 0)
                    leg4ByExpiration[group[0].Expiration] = group;

            List<List<SpreadHolder>> results = new();
            foreach (List<Option> leg1Options in leg1OptionsGroup)
            {
                token.ThrowIfCancellationRequested();
                List<SpreadHolder> groupResults = new();
                List<int> leg2Indexes = new();
                List<int> leg3Indexes = new();
                List<int> leg4Indexes = new();
                for (int leg1Index = 0; leg1Index < leg1Options.Count; leg1Index++)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = leg1Options[leg1Index];

                    leg2ByExpiration.TryGetValue(leg1.Expiration, out List<Option>? leg2Options);

                    if (leg2Options == null)
                    {
                        continue;
                    }

                    leg2Indexes.Clear();

                    if (spacing1List != null)
                    {
                        foreach (double spacing in spacing1List)
                        {
                            token.ThrowIfCancellationRequested();
                            double targetStrike = isCall ? leg1.Strike + spacing : leg1.Strike - spacing;
                            Option? leg2WithCurrentSpacing = leg2Options.FirstOrDefault(x => x.Strike == targetStrike);

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
                            ? leg2Options.FirstOrDefault(x => x.Strike > leg1.Strike)
                            : leg2Options.FirstOrDefault(x => x.Strike < leg1.Strike);

                        if (leg2 == null)
                        {
                            continue;
                        }

                        int leg2StartIndex = leg2Options.IndexOf(leg2);
                        int leg2EndIndex = leg2Options.Count;

                        if (spacing1RangeEnabled)
                        {
                            Option? leg2Start = leg2Options.Skip(leg2StartIndex)
                                                       .FirstOrDefault(x => Math.Abs(leg1.Strike - x.Strike) >= spacing1RangeFloor);
                            Option? leg2End = leg2Options.Skip(leg2StartIndex)
                                                     .LastOrDefault(x => Math.Abs(leg1.Strike - x.Strike) <= spacing1RangeCeil);
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

                    foreach (int leg2Index in leg2Indexes)
                    {
                        token.ThrowIfCancellationRequested();
                        Option? leg2 = leg2Options[leg2Index];

                        leg3ByExpiration.TryGetValue(leg2.Expiration, out List<Option>? leg3Options);

                        if (leg3Options == null)
                        {
                            continue;
                        }

                        leg3Indexes.Clear();

                        if (spacing2List != null)
                        {
                            foreach (double spacing in spacing2List)
                            {
                                token.ThrowIfCancellationRequested();
                                double targetStrike = isCall ? leg2.Strike + spacing : leg2.Strike - spacing;
                                Option? leg3WithCurrentSpacing = leg3Options.FirstOrDefault(x => x.Strike == targetStrike);

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
                                ? leg3Options.FirstOrDefault(x => x.Strike > leg2.Strike)
                                : leg3Options.FirstOrDefault(x => x.Strike < leg2.Strike);

                            if (leg3 == null)
                            {
                                continue;
                            }

                            int leg3StartIndex = leg3Options.IndexOf(leg3);
                            int leg3EndIndex = leg3Options.Count;

                            if (spacing2RangeEnabled)
                            {
                                Option? leg3Start = leg3Options.Skip(leg3StartIndex)
                                                           .FirstOrDefault(x => Math.Abs(leg2.Strike - x.Strike) >= spacing2RangeFloor);
                                Option? leg3End = leg3Options.Skip(leg3StartIndex)
                                                         .LastOrDefault(x => Math.Abs(leg2.Strike - x.Strike) <= spacing2RangeCeil);
                                if (leg3Start == null || leg3End == null)
                                {
                                    continue;
                                }
                                else
                                {
                                    leg3StartIndex = leg3Options.IndexOf(leg3Start);
                                    leg3EndIndex = leg3Options.IndexOf(leg3End) + 2;
                                }
                            }

                            for (int i = leg3StartIndex; i < leg3EndIndex && i < leg3Options.Count; i++)
                            {
                                token.ThrowIfCancellationRequested();
                                leg3Indexes.Add(i);
                            }
                        }

                        foreach (int leg3Index in leg3Indexes)
                        {
                            token.ThrowIfCancellationRequested();
                            Option? leg3 = leg3Options[leg3Index];

                            leg4ByExpiration.TryGetValue(leg3.Expiration, out List<Option>? leg4Options);

                            if (leg4Options == null)
                            {
                                continue;
                            }

                            leg4Indexes.Clear();

                            if (spacing3List != null)
                            {
                                foreach (double spacing in spacing3List)
                                {
                                    token.ThrowIfCancellationRequested();
                                    double targetStrike = isCall ? leg3.Strike + spacing : leg3.Strike - spacing;
                                    Option? leg4WithCurrentSpacing = leg4Options.FirstOrDefault(x => x.Strike == targetStrike);

                                    if (leg4WithCurrentSpacing == null)
                                    {
                                        continue;
                                    }

                                    leg4Indexes.Add(leg4Options.IndexOf(leg4WithCurrentSpacing));
                                }
                            }
                            else
                            {
                                Option? leg4 = isCall
                                    ? leg4Options.FirstOrDefault(x => x.Strike > leg3.Strike)
                                    : leg4Options.FirstOrDefault(x => x.Strike < leg3.Strike);

                                if (leg4 == null)
                                {
                                    continue;
                                }

                                int leg4StartIndex = leg4Options.IndexOf(leg4);
                                int leg4EndIndex = leg4Options.Count;

                                if (spacing3RangeEnabled)
                                {
                                    Option? leg4Start = leg4Options.Skip(leg4StartIndex)
                                                               .FirstOrDefault(x => Math.Abs(leg3.Strike - x.Strike) >= spacing3RangeFloor);
                                    Option? leg4End = leg4Options.Skip(leg4StartIndex)
                                                             .LastOrDefault(x => Math.Abs(leg3.Strike - x.Strike) <= spacing3RangeCeil);
                                    if (leg4Start == null || leg4End == null)
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        leg4StartIndex = leg4Options.IndexOf(leg4Start);
                                        leg4EndIndex = leg4Options.IndexOf(leg4End) + 3;
                                    }
                                }

                                for (int i = leg4StartIndex; i < leg4EndIndex && i < leg4Options.Count; i++)
                                {
                                    token.ThrowIfCancellationRequested();
                                    leg4Indexes.Add(i);
                                }
                            }

                            foreach (int leg4Index in leg4Indexes)
                            {
                                token.ThrowIfCancellationRequested();
                                Option? leg4 = leg4Options[leg4Index];

                                groupResults.Add(new SpreadHolder(leg1, leg2, leg3, leg4));

                                if (isSample)
                                {
                                    results.Add(groupResults);
                                    return results;
                                }
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