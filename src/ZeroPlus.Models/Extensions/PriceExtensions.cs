namespace ZeroPlus.Models.Extensions
{
    public static class PriceExtensions
    {
        public static string ToPriceLog(this double price)
        {
            if (double.IsNaN(price))
            {
                return "_.____";
            }
            else
            {
                return price.ToString("f4");
            }
        }

        public static string ToGreekLog(this double greek)
        {
            if (double.IsNaN(greek))
            {
                return "_.______";
            }
            else
            {
                return greek.ToString("f6");
            }
        }
    }
}
