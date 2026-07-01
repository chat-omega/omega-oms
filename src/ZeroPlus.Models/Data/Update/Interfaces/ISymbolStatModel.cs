namespace ZeroPlus.Models.Data.Update.Interfaces
{
    public interface ISymbolStatModel
    {
        int Id { get; set; }
        int MultiLegTradesCount { get; set; }
        int SingleLegTradesCount { get; set; }
        int MultiLegTradesPerHour { get; set; }
        int SingleLegTradesPerHour { get; set; }
        int MultiLegTradesPerMinute { get; set; }
        int SingleLegTradesPerMinute { get; set; }
        int Volume { get; set; }
        int OptionVolume { get; set; }
        double DayPercentChange { get; set; }
        double HourPercentChange { get; set; }
        double HalfHourPercentChange { get; set; }
        double QuarterHourPercentChange { get; set; }
        double DayNetChange { get; set; }
        double HourNetChange { get; set; }
        double HalfHourNetChange { get; set; }
        double QuarterHourNetChange { get; set; }
        double Last { get; set; }
        string Symbol { get; set; }
    }
}