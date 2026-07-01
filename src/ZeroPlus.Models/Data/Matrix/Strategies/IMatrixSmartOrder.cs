using ZeroPlus.Models.Data.Enums;
using ZeroPlus.Models.Data.Update;
using Side = ZeroPlus.Models.Data.Enums.Matrix.Side;

namespace ZeroPlus.Models.Data.Matrix.Strategies;

public interface IMatrixSmartOrder : IHaveRisk
{
    public string? Account { get; set; }
    public string? ClientGuid { get; set; }
    public string? Symbol { get; set; }
    public string? Exchange { get; set; }
    public string? Destination { get; set; }
    public Side? Side { get; set; }
    public double Price { get; set; }
    public string? Memo { get; set; }
    public int OrderQuantity { get; set; }
    public MinimumTickStyle MinimumTickStyle { get; set; }
    public int CancelDelay { get; set; }

    public IMatrixSmartOrder Clone();
}