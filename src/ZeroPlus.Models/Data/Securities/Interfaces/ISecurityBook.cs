namespace ZeroPlus.Models.Data.Securities.Interfaces
{
    public interface ISecurityBook
    {
        Security? GetSecurity(string? symbol);
    }
}