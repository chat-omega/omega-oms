namespace ZeroPlus.Models.Data.Models;

public class SlimGreekUpdateModel : IGreekUpdate
{
    public int TickerId { get; set; }
    public byte ModelId { get; set; }
    public double Theo { get; set; }
    public double Delta { get; set; }
    public double Gamma { get; set; }
    public double Vega { get; set; }
    public double Vol { get; set; }
    public ulong TimeStamp { get; set; }
}