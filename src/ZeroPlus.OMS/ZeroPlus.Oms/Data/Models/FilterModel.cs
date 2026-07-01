namespace ZeroPlus.Oms.Data.Models;

public class FilterModel
{
    public string Name { get; set; }
    public string Filter { get; set; }

    public override bool Equals(object obj)
    {
        return obj is FilterModel other && other.Name == Name && other.Filter == Filter;
    }

    public override int GetHashCode()
    {
        return (Name + Filter).GetHashCode();
    }
}