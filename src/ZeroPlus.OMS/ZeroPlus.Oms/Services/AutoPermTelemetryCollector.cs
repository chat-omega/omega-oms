using System.Collections.Generic;
using System.Globalization;

namespace ZeroPlus.Oms.Services;

public sealed class AutoPermTelemetryCollector
{
    private readonly Dictionary<string, string> _kvps = new();
    private int _attemptIndex;
    private int _rejectedRecentTrade;
    private int _rejectedDelta;
    private int _rejectedAddDelta;
    private int _rejectedLegDelta;
    private int _rejectedWeightedVega;
    private int _riskCheckFailures;
    private int _marketCrossFailures;
    private int _emaLoadFailures;
    private int _totalAttempts;
    private int _totalFills;

    public long TimestampNanos { get; set; }

    public void Set(string key, string value) => _kvps[key] = value ?? "";

    public void Set(string key, double value) => _kvps[key] = value.ToString("G", CultureInfo.InvariantCulture);

    public void Set(string key, int value) => _kvps[key] = value.ToString(CultureInfo.InvariantCulture);

    public void Set(string key, bool value) => _kvps[key] = value ? "true" : "false";

    public void IncrementRejection(string reason)
    {
        switch (reason)
        {
            case "RecentTrade": _rejectedRecentTrade++; break;
            case "Delta": _rejectedDelta++; break;
            case "AddDelta": _rejectedAddDelta++; break;
            case "LegDelta": _rejectedLegDelta++; break;
            case "WeightedVega": _rejectedWeightedVega++; break;
        }
    }

    public void IncrementRiskCheckFailure() => _riskCheckFailures++;

    public void IncrementMarketCrossFailure() => _marketCrossFailures++;

    public void IncrementEmaLoadFailure() => _emaLoadFailures++;

    public void RecordAttempt(string spreadId, double edgeInc, double price, double bid, double ask,
        double theo, double adjTheo, string status, bool filled, double elapsedMs, string riskFail)
    {
        int i = _attemptIndex++;
        string prefix = $"Attempt_{i}_";
        _kvps[prefix + "SpreadId"] = spreadId ?? "";
        _kvps[prefix + "EdgeInc"] = edgeInc.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "Price"] = price.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "Bid"] = bid.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "Ask"] = ask.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "Theo"] = theo.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "AdjTheo"] = adjTheo.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "Status"] = status ?? "";
        _kvps[prefix + "Filled"] = filled ? "true" : "false";
        _kvps[prefix + "ElapsedMs"] = elapsedMs.ToString("G", CultureInfo.InvariantCulture);
        _kvps[prefix + "RiskFail"] = riskFail ?? "";
        _totalAttempts++;
        if (filled) _totalFills++;
    }

    public KeyValuePair<string, string>[] ToKeyValuePairs()
    {
        _kvps["RejectedRecentTrade"] = _rejectedRecentTrade.ToString(CultureInfo.InvariantCulture);
        _kvps["RejectedDelta"] = _rejectedDelta.ToString(CultureInfo.InvariantCulture);
        _kvps["RejectedAddDelta"] = _rejectedAddDelta.ToString(CultureInfo.InvariantCulture);
        _kvps["RejectedLegDelta"] = _rejectedLegDelta.ToString(CultureInfo.InvariantCulture);
        _kvps["RejectedWeightedVega"] = _rejectedWeightedVega.ToString(CultureInfo.InvariantCulture);
        _kvps["RiskCheckFailures"] = _riskCheckFailures.ToString(CultureInfo.InvariantCulture);
        _kvps["MarketCrossFailures"] = _marketCrossFailures.ToString(CultureInfo.InvariantCulture);
        _kvps["EmaLoadFailures"] = _emaLoadFailures.ToString(CultureInfo.InvariantCulture);
        _kvps["TotalAttempts"] = _totalAttempts.ToString(CultureInfo.InvariantCulture);
        _kvps["TotalFills"] = _totalFills.ToString(CultureInfo.InvariantCulture);

        var result = new KeyValuePair<string, string>[_kvps.Count];
        int idx = 0;
        foreach (var kvp in _kvps)
        {
            result[idx++] = kvp;
        }
        return result;
    }
}
