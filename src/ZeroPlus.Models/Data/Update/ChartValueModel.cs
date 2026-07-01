using System;

namespace ZeroPlus.Models.Data.Update;

public class ChartValueModel
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }

    public ChartValueModel(double value, DateTime timestamp)
    {
        Value = value;
        Timestamp = timestamp;
    }

    public ChartValueModel(DateTime timestamp, double value)
    {
        Value = value;
        Timestamp = timestamp;
    }

    public override string ToString()
    {
        return $"Value: {Value}, Timestamp: {Timestamp}";
    }
}