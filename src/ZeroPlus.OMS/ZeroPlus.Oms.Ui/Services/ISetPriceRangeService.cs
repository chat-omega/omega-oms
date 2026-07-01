namespace ZeroPlus.Oms.Ui.Services
{
    public interface ISetPriceRangeService
    {
        void SetPriceRange(double low, double high);
        void SetPrice(double price);
    }
}
