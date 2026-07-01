using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Interfaces;
using ZeroPlus.Models.Generators.SpreadGenerators.Settings;

namespace ZeroPlus.Models.Generators.SpreadGenerators;

public class OneThreeTwoSpreadsGenerator : SpreadGenerator
{
    private const double TOLERANCE = .01;

    public OneThreeTwoSpreadsGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
        : base(logger, deltaStore, theoStore, vegaStore, bidStore, askStore, lastStore, emaStore, wVegaStore, volaStore)
    {
    }

    public async Task<SpreadGeneratorResults> GenerateAsync(List<Option> leg1Options, List<Option> leg2Options, List<Option> leg3Options, IOneThreeTwoSpreadsGeneratorSettings settings, int count, CancellationToken token, bool isSample = false)
    {
        List<Option> optionChain = leg1Options.Union(leg2Options).Union(leg3Options).Distinct().ToList();
        Option? option = optionChain.FirstOrDefault();
        SpreadGeneratorResults spreadGeneratorResults = new(option?.Underlying?.Symbol, option?.PutCall, Strategy.OneThreeTwo);
        if (option == null)
        {
            spreadGeneratorResults.Errors.Add("Option chain is empty.");
            return spreadGeneratorResults;
        }

        var type = option.PutCall;
        _logger.LogInformation($"[Start] {nameof(OneThreeTwoSpreadsGenerator)}. " +
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

        List<List<Option>> leg1OptionsGroup = GroupLegOptions(type, leg1Options);
        List<List<Option>> leg2OptionsGroup = GroupLegOptions(type, leg2Options);
        List<List<Option>> leg3OptionsGroup = GroupLegOptions(type, leg3Options);

        List<List<SpreadHolder>> results = GenerateSpreads(leg1OptionsGroup, leg2OptionsGroup, leg3OptionsGroup, type, settings, token, isSample);

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

            SpreadHolder[] groupResult = result.ToArray();

            foreach (SpreadHolder spread in groupResult)
            {
                token.ThrowIfCancellationRequested();
                Option? leg1 = spread[0];
                Option? leg2 = spread[1];
                Option? leg3 = spread[2];
                string tos;

                bool sortLowToHigh = (type == PutCall.Call && !settings.Reversed) ||
                                     (type == PutCall.Put && settings.Reversed);
                if (sortLowToHigh)
                {
                    tos = leg1.Symbol + "-3*" + leg2.Symbol + "+2*" + leg3.Symbol;

                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1, Side.Buy, 1));
                    spreadModel.Legs.Add(new SpreadLeg(leg2, Side.Sell, 3));
                    spreadModel.Legs.Add(new SpreadLeg(leg3, Side.Buy, 2));

                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
                else
                {
                    tos = "2*" + leg1.Symbol + "-3*" + leg2.Symbol + "+" + leg3.Symbol;

                    Spread spreadModel = new(tos, spread.Width);
                    spreadModel.Legs.Add(new SpreadLeg(leg1, Side.Buy, 2));
                    spreadModel.Legs.Add(new SpreadLeg(leg2, Side.Sell, 3));
                    spreadModel.Legs.Add(new SpreadLeg(leg3, Side.Buy, 1));

                    spreadGeneratorResults.Spreads.Add(spreadModel);
                }
            }
        }

        results.Clear();

        stopwatch.Stop();

        _logger.LogInformation($"[Finish] {nameof(OneThreeTwoSpreadsGenerator)}. " +
                               $"For {option.Underlying?.Symbol} {type}, " +
                               $"using {optionChain.Count} options, " +
                               $"took {stopwatch.ElapsedMilliseconds}ms, " +
                               $"generated {spreadGeneratorResults.Spreads.Count} spreads, " +
                               $"with {spreadGeneratorResults.Errors.Count} errors.");
        return spreadGeneratorResults;
    }

    private void RequestData(List<Option> optionChain, IOneThreeTwoSpreadsGeneratorSettings settings)
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

    private Task<List<Option>> ApplyLeg1Filters(List<Option> optionChain, IOneThreeTwoSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        => ApplyLegFilters(optionChain, new LegFilterConfig(
            settings.Leg1StrikeRangeEnabled, settings.Leg1StrikeRangeFloor, settings.Leg1StrikeRangeCeil,
            settings.Leg1DeltaRangeEnabled, settings.Leg1DeltaRangeFloor, settings.Leg1DeltaRangeCeil,
            settings.Leg1TheoRangeEnabled, settings.Leg1TheoRangeFloor, settings.Leg1TheoRangeCeil,
            settings.Leg1VegaRangeEnabled, settings.Leg1VegaRangeFloor, settings.Leg1VegaRangeCeil,
            settings.Leg1WeightedVegaRangeEnabled, settings.Leg1WeightedVegaRangeFloor, settings.Leg1WeightedVegaRangeCeil,
            settings.Leg1MarketRangeEnabled, settings.Leg1MarketRangeFloor, settings.Leg1MarketRangeCeil,
            settings.Leg1WidthRangeEnabled, settings.Leg1WidthRangeFloor, settings.Leg1WidthRangeCeil,
            settings.TheoModel), spreadGeneratorResults, token);

    private Task<List<Option>> ApplyLeg2Filters(List<Option> optionChain, IOneThreeTwoSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        => ApplyLegFilters(optionChain, new LegFilterConfig(
            settings.Leg2StrikeRangeEnabled, settings.Leg2StrikeRangeFloor, settings.Leg2StrikeRangeCeil,
            settings.Leg2DeltaRangeEnabled, settings.Leg2DeltaRangeFloor, settings.Leg2DeltaRangeCeil,
            settings.Leg2TheoRangeEnabled, settings.Leg2TheoRangeFloor, settings.Leg2TheoRangeCeil,
            settings.Leg2VegaRangeEnabled, settings.Leg2VegaRangeFloor, settings.Leg2VegaRangeCeil,
            settings.Leg2WeightedVegaRangeEnabled, settings.Leg2WeightedVegaRangeFloor, settings.Leg2WeightedVegaRangeCeil,
            settings.Leg2MarketRangeEnabled, settings.Leg2MarketRangeFloor, settings.Leg2MarketRangeCeil,
            settings.Leg2WidthRangeEnabled, settings.Leg2WidthRangeFloor, settings.Leg2WidthRangeCeil,
            settings.TheoModel), spreadGeneratorResults, token);

    private Task<List<Option>> ApplyLeg3Filters(List<Option> optionChain, IOneThreeTwoSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, CancellationToken token)
        => ApplyLegFilters(optionChain, new LegFilterConfig(
            settings.Leg3StrikeRangeEnabled, settings.Leg3StrikeRangeFloor, settings.Leg3StrikeRangeCeil,
            settings.Leg3DeltaRangeEnabled, settings.Leg3DeltaRangeFloor, settings.Leg3DeltaRangeCeil,
            settings.Leg3TheoRangeEnabled, settings.Leg3TheoRangeFloor, settings.Leg3TheoRangeCeil,
            settings.Leg3VegaRangeEnabled, settings.Leg3VegaRangeFloor, settings.Leg3VegaRangeCeil,
            settings.Leg3WeightedVegaRangeEnabled, settings.Leg3WeightedVegaRangeFloor, settings.Leg3WeightedVegaRangeCeil,
            settings.Leg3MarketRangeEnabled, settings.Leg3MarketRangeFloor, settings.Leg3MarketRangeCeil,
            settings.Leg3WidthRangeEnabled, settings.Leg3WidthRangeFloor, settings.Leg3WidthRangeCeil,
            settings.TheoModel), spreadGeneratorResults, token);

    private async Task<List<SpreadHolder>> ApplySpreadFilters(IOneThreeTwoSpreadsGeneratorSettings settings, SpreadGeneratorResults spreadGeneratorResults, List<SpreadHolder> results, List<Option> optionChain, CancellationToken token)
    {

        var type = optionChain.FirstOrDefault()?.PutCall ?? PutCall.Unknown;
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

                double spreadDelta = CalculateSpreadResult(type, leg1Delta, leg2Delta, leg3Delta, settings);
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

                double spreadTheo = CalculateSpreadResult(type, leg1Theo, leg2Theo, leg3Theo, settings);
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

                double spreadVega = CalculateSpreadResult(type, leg1Vega, leg2Vega, leg3Vega, settings);

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

                double spreadVega = CalculateSpreadResult(type, leg1Vega, leg2Vega, leg3Vega, settings);

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

                double spreadTheo = CalculateSpreadResult(type, leg1Theo, leg2Theo, leg3Theo, settings);
                double spreadVola = CalculateSpreadResult(type, leg1Vola, leg2Vola, leg3Vola, settings);

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
                double low = CalculateSpreadResult(type, leg1Bid, leg2Ask, leg3Bid, settings);

                // For buys  + _askStore
                // For sells - _bidStore
                double high = CalculateSpreadResult(type, leg1Ask, leg2Bid, leg3Ask, settings);

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

                    double spreadTheo = CalculateSpreadResult(type, leg1Theo, leg2Theo, leg3Theo, settings);
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

                    double spreadEma = CalculateSpreadResult(type, leg1Ema, leg2Ema, leg3Ema, settings);
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

    private double CalculateSpreadResult(PutCall type, double leg1Input, double leg2Input, double leg3Input, IOneThreeTwoSpreadsGeneratorSettings settings)
    {
        bool as132 = (type == PutCall.Call && !settings.Reversed) || (type == PutCall.Put && settings.Reversed);
        return as132
            ? leg1Input - (3 * leg2Input) + (2 * leg3Input)
            : (2 * leg1Input) - (3 * leg2Input) + leg3Input;
    }

    private List<List<SpreadHolder>> GenerateSpreads(List<List<Option>> leg1OptionsGroup,
        List<List<Option>> leg2OptionsGroup,
        List<List<Option>> leg3OptionsGroup,
        PutCall type,
        IOneThreeTwoSpreadsGeneratorSettings oneThreeTwoSpreadsSettings,
        CancellationToken token,
        bool isSample)
    {
        bool sortLowToHigh = (type == PutCall.Call && !oneThreeTwoSpreadsSettings.Reversed) ||
                             (type == PutCall.Put && oneThreeTwoSpreadsSettings.Reversed);

        List<double>? spacingList = oneThreeTwoSpreadsSettings.SpreadSpacingListEnabled
            ? oneThreeTwoSpreadsSettings.SpreadSpacingList!.Split(',')
                .Where(x => double.TryParse(x, out _))
                .Select(x => double.Parse(x))
                .Where(x => x != 0)
                .ToList()
            : null;

        bool rangeEnabled = oneThreeTwoSpreadsSettings.SpreadSpacingRangeEnabled;
        double rangeFloor = oneThreeTwoSpreadsSettings.SpreadSpacingRangeFloor;
        double rangeCeil = oneThreeTwoSpreadsSettings.SpreadSpacingRangeCeil;
        if (sortLowToHigh)
        {
            rangeFloor *= 2;
            rangeCeil *= 2;
        }

        Dictionary<DateTime, List<Option>> leg2ByExpiration = new(leg2OptionsGroup.Count);
        foreach (var group in leg2OptionsGroup)
            if (group.Count > 0)
                leg2ByExpiration[group[0].Expiration] = group.OrderBy(x => x.Strike).ToList();

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
                        var spacingUsed = sortLowToHigh ? spacing * 2 : spacing;

                        token.ThrowIfCancellationRequested();
                        Option? leg2WithCurrentSpacing = leg2Options.FirstOrDefault(x => x.Strike > leg1.Strike && Math.Abs(x.Strike - (leg1.Strike + spacingUsed)) < TOLERANCE);

                        if (leg2WithCurrentSpacing == null)
                        {
                            continue;
                        }

                        leg2Indexes.Add(leg2StrikeToIndex[leg1.Expiration][leg2WithCurrentSpacing.Strike]);
                    }
                }
                else
                {
                    Option? leg2 = leg2Options.FirstOrDefault(x => x.Strike > leg1.Strike);

                    if (leg2 == null)
                    {
                        continue;
                    }

                    int leg2StartIndex = leg2StrikeToIndex[leg1.Expiration][leg2.Strike];
                    int leg2EndIndex = leg2Options.Count;

                    if (rangeEnabled)
                    {
                        Option? leg2Start = leg2Options.Skip(leg2StartIndex).FirstOrDefault(x => Math.Abs(leg1.Strike - x.Strike) >= rangeFloor);
                        Option? leg2End = leg2Options.Skip(leg2StartIndex).LastOrDefault(x => Math.Abs(leg1.Strike - x.Strike) <= rangeCeil);
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

                    var leg3Strike = sortLowToHigh
                        ? leg2.Strike + (leg2.Strike - leg1.Strike) / 2
                        : leg2.Strike + (leg2.Strike - leg1.Strike) * 2;

                    Option? leg3 = leg3Group.FirstOrDefault(x => Math.Abs(x.Strike - leg3Strike) < TOLERANCE);

                    if (leg3 == null)
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
            if (groupResults.Count > 0)
            {
                results.Add(groupResults);
            }
        }
        return results;
    }
}