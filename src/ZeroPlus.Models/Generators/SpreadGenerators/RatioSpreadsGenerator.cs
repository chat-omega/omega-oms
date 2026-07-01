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
    public class RatioSpreadsGenerator : SpreadGenerator
    {
        public RatioSpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
            : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
        {
        }

        public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option> leg2Options, IRatioSpreadsGeneratorSettings settings, int count, CancellationToken token, bool isSample = false)
        {
            List<Option> optionChain = leg1Options.Union(leg2Options).Distinct().ToList();
            Option? option = optionChain.FirstOrDefault();
            SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.RatioCustom);

            if (settings.Leg1Ratio == 1)
            {
                if (settings.Leg2Ratio == 2)
                {
                    spreadGeneratorResults.Strategy = Strategy.Ratio1X2;
                }
                else if (settings.Leg2Ratio == 3)
                {
                    spreadGeneratorResults.Strategy = Strategy.Ratio1X3;
                }
            }

            if (option == null)
            {
                spreadGeneratorResults.Errors.Add("Option chain is empty.");
                return spreadGeneratorResults;
            }

            var type = option.PutCall;
            _logger.LogInformation($"[Start] {nameof(RatioSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options.");

            Stopwatch stopwatch = new();
            stopwatch.Start();


            if (!isSample)
            {
                RequestData(optionChain, settings);

                leg1Options = await ApplyLeg1Filters(leg1Options, settings, spreadGeneratorResults, token);
                leg2Options = await ApplyLeg2Filters(leg2Options, settings, spreadGeneratorResults, token);
            }

            List<List<Option>> leg1OptionsGroup = GroupLegOptions(type, leg1Options);
            List<List<Option>> leg2OptionsGroup = GroupLegOptions(type, leg2Options);

            List<List<SpreadHolder>> results = GenerateSpreads(leg1OptionsGroup, leg2OptionsGroup, type, settings, token, isSample);

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
                    string leg1RatioString = settings.Leg1Ratio == 1 ? "" : settings.Leg1Ratio + "*";

                    string tos = leg1RatioString + leg1.Symbol + "-" + settings.Leg2Ratio + "*" + leg2.Symbol;

                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1, Side.Buy, settings.Leg1Ratio));
                    spreadModel.Legs.Add(new SpreadLeg(leg2, Side.Sell, settings.Leg2Ratio));

                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
            }

            results.Clear();

            stopwatch.Stop();

            _logger.LogInformation($"[Finish] {nameof(RatioSpreadsGenerator)}. " +
                      $"For {option.Underlying?.Symbol} {type}, " +
                      $"using {optionChain.Count} options, " +
                      $"took {stopwatch.ElapsedMilliseconds}ms, " +
                      $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                      $"with {spreadGeneratorResults.Errors.Count} errors.");
            return spreadGeneratorResults;
        }

        private void RequestData(List<Option> optionChain, IRatioSpreadsGeneratorSettings settings)
        {
            if (settings.DataRequested())
            {
                if (settings.Leg1DeltaRangeEnabled ||
                    settings.Leg2DeltaRangeEnabled ||
                    settings.SpreadDeltaRangeEnabled)
                {
                    _deltaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Delta);
                }

                if (settings.Leg1TheoRangeEnabled ||
                    settings.Leg2TheoRangeEnabled ||
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
                    settings.SpreadVegaRangeEnabled ||
                    settings.WeightedVegaRangeEnabled)
                {
                    _vegaStore.GetHanweckDataFor(optionChain, SubscriptionFieldType.Vega);
                }

                if (settings.Leg1MarketRangeEnabled ||
                    settings.Leg2MarketRangeEnabled ||
                    settings.SpreadMarketRangeEnabled ||
                    settings.Leg1WidthRangeEnabled ||
                    settings.Leg2WidthRangeEnabled ||
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

        private Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, IRatioSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg1StrikeRangeEnabled, settings.Leg1StrikeRangeFloor, settings.Leg1StrikeRangeCeil,
                settings.Leg1DeltaRangeEnabled, settings.Leg1DeltaRangeFloor, settings.Leg1DeltaRangeCeil,
                settings.Leg1TheoRangeEnabled, settings.Leg1TheoRangeFloor, settings.Leg1TheoRangeCeil,
                settings.Leg1VegaRangeEnabled, settings.Leg1VegaRangeFloor, settings.Leg1VegaRangeCeil,
                settings.Leg1WeightedVegaRangeEnabled, settings.Leg1WeightedVegaRangeFloor, settings.Leg1WeightedVegaRangeCeil,
                settings.Leg1MarketRangeEnabled, settings.Leg1MarketRangeFloor, settings.Leg1MarketRangeCeil,
                settings.Leg1WidthRangeEnabled, settings.Leg1WidthRangeFloor, settings.Leg1WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private Task<List<Option>> ApplyLeg2Filters(List<Option> optionChain, IRatioSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
            => ApplyLegFilters(optionChain, new LegFilterConfig(
                settings.Leg2StrikeRangeEnabled, settings.Leg2StrikeRangeFloor, settings.Leg2StrikeRangeCeil,
                settings.Leg2DeltaRangeEnabled, settings.Leg2DeltaRangeFloor, settings.Leg2DeltaRangeCeil,
                settings.Leg2TheoRangeEnabled, settings.Leg2TheoRangeFloor, settings.Leg2TheoRangeCeil,
                settings.Leg2VegaRangeEnabled, settings.Leg2VegaRangeFloor, settings.Leg2VegaRangeCeil,
                settings.Leg2WeightedVegaRangeEnabled, settings.Leg2WeightedVegaRangeFloor, settings.Leg2WeightedVegaRangeCeil,
                settings.Leg2MarketRangeEnabled, settings.Leg2MarketRangeFloor, settings.Leg2MarketRangeCeil,
                settings.Leg2WidthRangeEnabled, settings.Leg2WidthRangeFloor, settings.Leg2WidthRangeCeil,
                settings.TheoModel), spreadGeneratorResults, token);

        private async Task<List<SpreadHolder>> ApplySpreadFilters(IRatioSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
        {
            if (settings.SpreadDeltaRangeEnabled)
            {
                List<SpreadHolder> passed = new();
                foreach (SpreadHolder spread in results)
                {
                    token.ThrowIfCancellationRequested();
                    Option? leg1 = spread[0];
                    Option? leg2 = spread[1];
                    double leg1Delta = await _deltaStore.GetDataAsync(leg1.Symbol);
                    double leg2Delta = await _deltaStore.GetDataAsync(leg2.Symbol);

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

                    double spreadDelta = Math.Abs(settings.Leg1Ratio * leg1Delta - settings.Leg2Ratio * leg2Delta);
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
                    double leg1Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg1.Symbol) : await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = settings.TheoModel == TheoModel.Hanw ? await _theoStore.GetDataAsync(leg2.Symbol) : await _volaStore.GetDataAsync(leg2.Symbol);

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

                    double spreadTheo = settings.Leg1Ratio * leg1Theo - settings.Leg2Ratio * leg2Theo;
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
                    double leg1Vega = await _vegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _vegaStore.GetDataAsync(leg2.Symbol);

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

                    double spreadVega = settings.Leg1Ratio * leg1Vega - settings.Leg2Ratio * leg2Vega;

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
                    double leg1Vega = await _wVegaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vega = await _wVegaStore.GetDataAsync(leg2.Symbol);

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

                    double spreadVega = settings.Leg1Ratio * leg1Vega - settings.Leg2Ratio * leg2Vega;

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

                    double leg1Theo = await _theoStore.GetDataAsync(leg1.Symbol);
                    double leg2Theo = await _theoStore.GetDataAsync(leg2.Symbol);

                    double leg1Vola = await _volaStore.GetDataAsync(leg1.Symbol);
                    double leg2Vola = await _volaStore.GetDataAsync(leg2.Symbol);

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

                    double spreadTheo = settings.Leg1Ratio * leg1Theo - settings.Leg2Ratio * leg2Theo;
                    double spreadVola = settings.Leg1Ratio * leg1Vola - settings.Leg2Ratio * leg2Vola;

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
                    double leg1Bid = await _bidStore.GetDataAsync(leg1.Symbol);
                    double leg1Ask = await _askStore.GetDataAsync(leg1.Symbol);
                    double leg2Bid = await _bidStore.GetDataAsync(leg2.Symbol);
                    double leg2Ask = await _askStore.GetDataAsync(leg2.Symbol);

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

                    // For buys  + _bidStore
                    // For sells - _askStore
                    double low = -(settings.Leg1Ratio * leg1Ask) + settings.Leg2Ratio * leg2Bid;

                    // For buys  + _askStore
                    // For sells - _bidStore
                    double high = -(settings.Leg1Ratio * leg1Bid) + settings.Leg2Ratio * leg2Ask;

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

                        double spreadTheo = settings.Leg1Ratio * leg1Theo - settings.Leg2Ratio * leg2Theo;
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

                        double spreadEma = settings.Leg1Ratio * leg1Ema - settings.Leg2Ratio * leg2Ema;
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
                                                      PutCall type,
                                                      IRatioSpreadsGeneratorSettings settings,
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

            List<List<SpreadHolder>> results = new();
            foreach (List<Option> leg1Options in leg1OptionsGroup)
            {
                token.ThrowIfCancellationRequested();
                List<SpreadHolder> groupResults = new();
                List<int> leg2Indexes = new();
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

                        groupResults.Add(new SpreadHolder(leg1, leg2));

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