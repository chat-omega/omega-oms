using DevExpress.Mvvm;
using ZeroPlus.Comms.Models.Data.Types;
using ZeroPlus.Models.Data.Trading.Interfaces;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DepthItemModel : BindableBase
    {
        private static readonly string[] _propertyNames = new string[] {
            nameof(MMID),
            nameof(Price),
            nameof(Size),
            nameof(Time),
            nameof(Level),
            nameof(Refresh),
            nameof(IsOrder),
            nameof(CustFlag)};

        public string MMID { get; set; }
        public double Price { get; set; }
        public int Size { get; set; }
        public Time Time { get; set; }
        public int Level { get; set; }
        public bool Refresh { get; set; }
        public bool IsOrder { get; set; }
        public bool CustFlag { get; set; }
        public IOrder Order { get; set; }

        internal void RefreshItems()
        {
            RaisePropertiesChanged(_propertyNames);
        }

        public override string ToString()
        {
            return $"{(IsOrder ? "O" : "Q")} {MMID} {Level} {Size} {Price:N2}";
        }
    }
}
