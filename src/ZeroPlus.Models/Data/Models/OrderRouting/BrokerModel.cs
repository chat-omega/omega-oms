namespace ZeroPlus.Models.Data.Models.OrderRouting;

public class BrokerModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsInternal { get; set; }
}
