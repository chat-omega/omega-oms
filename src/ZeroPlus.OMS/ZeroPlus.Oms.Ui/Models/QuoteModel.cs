using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.Models
{
    public class QuoteModel
    {
        public double Bid { get; set; } = double.NaN;
        public double Ask { get; set; } = double.NaN;
        public int Ratio { get; internal set; }
        public Side Side { get; internal set; }
    }
}
