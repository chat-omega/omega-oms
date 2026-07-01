namespace ZeroPlus.Oms.Ui.Models
{
    public class UnderMultiplierModel
    {
        public string Under { get; set; }
        public double Multiplier { get; set; } = 1;

        internal UnderMultiplierModel Clone()
        {
            return new UnderMultiplierModel()
            {
                Under = Under,
                Multiplier = Multiplier
            };
        }
    }
}