using DevExpress.Mvvm;
using ZeroPlus.Models.Data.Update.Interfaces;

namespace ZeroPlus.Oms.Ui.Models
{
    public class SymbolStatModel : BindableBase, ISymbolStatModel
    {
        private static readonly string[] _propertyNames = {
            nameof(MultiLegTradesCount),
            nameof(SingleLegTradesCount),
            nameof(MultiLegTradesPerHour),
            nameof(SingleLegTradesPerHour),
            nameof(MultiLegTradesPerMinute),
            nameof(SingleLegTradesPerMinute),
            nameof(Volume),
            nameof(OptionVolume),
            nameof(DayPercentChange),
            nameof(HourPercentChange),
            nameof(HalfHourPercentChange),
            nameof(QuarterHourPercentChange),
            nameof(DayNetChange),
            nameof(HourNetChange),
            nameof(HalfHourNetChange),
            nameof(QuarterHourNetChange),
            nameof(Last),
        };

        public int Id { get; set; }
        public int MultiLegTradesCount { get; set; }
        public int SingleLegTradesCount { get; set; }
        public int MultiLegTradesPerHour { get; set; }
        public int SingleLegTradesPerHour { get; set; }
        public int MultiLegTradesPerMinute { get; set; }
        public int SingleLegTradesPerMinute { get; set; }
        public int Volume { get; set; }
        public int OptionVolume { get; set; }
        public double DayPercentChange { get; set; }
        public double HourPercentChange { get; set; }
        public double HalfHourPercentChange { get; set; }
        public double QuarterHourPercentChange { get; set; }
        public double DayNetChange { get; set; }
        public double HourNetChange { get; set; }
        public double HalfHourNetChange { get; set; }
        public double QuarterHourNetChange { get; set; }
        public double Last { get; set; }
        public string Symbol { get; set; } = string.Empty;

        public void Notify()
        {
            RaisePropertiesChanged(_propertyNames);
        }
    }
}