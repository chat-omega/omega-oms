using ZeroPlus.Models.Data.Portfolio.Interfaces;

namespace ZeroPlus.Oms.Ui.Models
{
    public class OpenPositionModel
    {
        public string Name { get; set; }
        public double OpenNotional { get; set; }
        public double NetDelta { get; set; }

        public OpenPositionModel(IPosition x)
        {
            Name = x.Name;
            OpenNotional = x.OpenNotional;
            NetDelta = x.NetDelta;
        }
    }
}