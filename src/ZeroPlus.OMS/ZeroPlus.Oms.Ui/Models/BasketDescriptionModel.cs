using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public class BasketDescriptionModel : IComparable
    {
        public string ExpirationDescription { get; set; }
        public DateTime ExpirationSample { get; set; }

        public override string ToString()
        {
            return ExpirationDescription;
        }

        public int CompareTo(object obj)
        {
            if (obj is not BasketDescriptionModel other)
            {
                return 1;
            }

            return ExpirationSample.CompareTo(other.ExpirationSample);
        }
    }
}
