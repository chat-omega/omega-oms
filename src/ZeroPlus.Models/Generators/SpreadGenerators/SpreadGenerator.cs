using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Subscription.Interfaces;

namespace ZeroPlus.Models.Generators.SpreadGenerators;

public readonly record struct LegFilterConfig(
    bool StrikeRangeEnabled, double StrikeRangeFloor, double StrikeRangeCeil,
    bool DeltaRangeEnabled, double DeltaRangeFloor, double DeltaRangeCeil,
    bool TheoRangeEnabled, double TheoRangeFloor, double TheoRangeCeil,
    bool VegaRangeEnabled, double VegaRangeFloor, double VegaRangeCeil,
    bool WeightedVegaRangeEnabled, double WeightedVegaRangeFloor, double WeightedVegaRangeCeil,
    bool MarketRangeEnabled, double MarketRangeFloor, double MarketRangeCeil,
    bool WidthRangeEnabled, double WidthRangeFloor, double WidthRangeCeil,
    TheoModel TheoModel);

public abstract class SpreadGenerator
{
    protected readonly ILogger _logger;
    protected readonly IDataStore _deltaStore;
    protected readonly IDataStore _theoStore;
    protected readonly IDataStore _vegaStore;
    protected readonly IDataStore _bidStore;
    protected readonly IDataStore _askStore;
    protected readonly IDataStore _lastStore;
    protected readonly IDataStore _emaStore;
    protected readonly IDataStore _wVegaStore;
    protected readonly IDataStore _volaStore;

    protected SpreadGenerator(ILogger logger, IDataStore deltaStore, IDataStore theoStore, IDataStore vegaStore, IDataStore bidStore, IDataStore askStore, IDataStore lastStore, IDataStore emaStore, IDataStore wVegaStore, IDataStore volaStore)
    {
        _logger = logger;
        _deltaStore = deltaStore;
        _theoStore = theoStore;
        _vegaStore = vegaStore;
        _bidStore = bidStore;
        _askStore = askStore;
        _lastStore = lastStore;
        _emaStore = emaStore;
        _wVegaStore = wVegaStore;
        _volaStore = volaStore;
    }

    protected static List<List<Option>> GroupLegOptions(List<Option> options)
    {
        return options.GroupBy(x => x.Expiration)
            .Select(g => g.OrderBy(x => x.Strike).ToList())
            .ToList();
    }

    protected static List<List<Option>> GroupLegOptions(PutCall type, List<Option> options)
    {
        return type == PutCall.Call
               ? options.GroupBy(x => x.Expiration)
                        .Select(g => g.OrderBy(x => x.Strike).ToList())
                        .ToList()
               : options.GroupBy(x => x.Expiration)
                        .Select(g => g.OrderByDescending(x => x.Strike).ToList())
                        .ToList();
    }

    protected async Task<List<Option>> ApplyLegFilters(
        List<Option> optionChain,
        LegFilterConfig config,
        SpreadGeneratorResults spreadGeneratorResults,
        CancellationToken token)
    {
        List<Option> filtered = new(optionChain);

        if (config.StrikeRangeEnabled)
        {
            filtered.RemoveAll(x => x.Strike < config.StrikeRangeFloor || x.Strike > config.StrikeRangeCeil);
        }

        if (config.DeltaRangeEnabled)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double delta = Math.Abs(await _deltaStore.GetDataAsync(option.Symbol));
                if (double.IsNaN(delta))
                    spreadGeneratorResults.Errors.Add(string.Concat("Delta not found for ", option.Symbol));
                else if (delta >= config.DeltaRangeFloor && delta <= config.DeltaRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        if (config.TheoRangeEnabled)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double theo = config.TheoModel == TheoModel.Hanw
                    ? Math.Abs(await _theoStore.GetDataAsync(option.Symbol))
                    : Math.Abs(await _volaStore.GetDataAsync(option.Symbol));
                if (double.IsNaN(theo))
                    spreadGeneratorResults.Errors.Add(string.Concat("Theo not found for ", option.Symbol));
                else if (theo >= config.TheoRangeFloor && theo <= config.TheoRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        if (config.VegaRangeEnabled)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double vega = Math.Abs(await _vegaStore.GetDataAsync(option.Symbol));
                if (double.IsNaN(vega))
                    spreadGeneratorResults.Errors.Add(string.Concat("Vega not found for ", option.Symbol));
                else if (vega >= config.VegaRangeFloor && vega <= config.VegaRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        if (config.WeightedVegaRangeEnabled && filtered.Count > 0)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double wVega = Math.Abs(await _wVegaStore.GetDataAsync(option.Symbol));
                if (wVega >= config.WeightedVegaRangeFloor && wVega <= config.WeightedVegaRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        if (config.MarketRangeEnabled)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double bid = Math.Abs(await _bidStore.GetDataAsync(option.Symbol));
                double ask = Math.Abs(await _askStore.GetDataAsync(option.Symbol));
                if (double.IsNaN(bid) || double.IsNaN(ask))
                    spreadGeneratorResults.Errors.Add(string.Concat("Quote not found for ", option.Symbol));
                if (bid >= config.MarketRangeFloor && ask <= config.MarketRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        if (config.WidthRangeEnabled)
        {
            List<Option> selected = new(filtered.Count);
            foreach (Option option in filtered)
            {
                token.ThrowIfCancellationRequested();
                double legWidth = Math.Abs(await _bidStore.GetDataAsync(option.Symbol) -
                                           await _askStore.GetDataAsync(option.Symbol));
                if (double.IsNaN(legWidth))
                    spreadGeneratorResults.Errors.Add(string.Concat("Quote not found for ", option.Symbol));
                else if (legWidth >= config.WidthRangeFloor && legWidth <= config.WidthRangeCeil)
                    selected.Add(option);
            }
            filtered = selected;
        }

        return filtered;
    }

    protected static List<List<Option>> GroupLegOptionsByStrike(PutCall type, List<Option> options)
    {
        return type == PutCall.Call
               ? options.GroupBy(x => x.Strike)
                        .Select(g => g.OrderBy(x => x.Expiration).ToList())
                        .ToList()
               : options.GroupBy(x => x.Strike)
                        .Select(g => g.OrderByDescending(x => x.Expiration).ToList())
                        .ToList();
    }
}