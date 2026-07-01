namespace ZeroPlus.Oms.Data.Models
{
    public class DerivedValueConfigModel
    {
        public string Symbol { get; set; } = "";
        public string DerivedSymbol { get; set; } = "";
        public double Multiplier { get; set; }
        public bool LoadDerivatives { get; set; }
    }
}
