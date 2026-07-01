using System.Collections.Generic;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class DepthItemComparer : IComparer<DepthItemModel>
    {
        private readonly bool isBid;

        public DepthItemComparer(bool isBid)
        {
            this.isBid = isBid;
        }

        public int Compare(DepthItemModel x, DepthItemModel y)
        {
            double firstPrice = x.IsOrder ? x.Order.Price : x.Price;
            double secondPrice = y.IsOrder ? y.Order.Price : y.Price;
            double num3 = x.IsOrder ? x.Order.Quantity : x.Size;
            double num4 = y.IsOrder ? y.Order.Quantity : y.Size;
            if (isBid)
            {
                if (firstPrice != secondPrice)
                {
                    return secondPrice.CompareTo(firstPrice);
                }

                if ((x.IsOrder && y.IsOrder) || (!x.IsOrder && !y.IsOrder))
                {
                    return num3.CompareTo(num4);
                }

                if (x.IsOrder)
                {
                    return -1;
                }

                return y.IsOrder ? 1 : 0;
            }
            if (firstPrice == secondPrice)
            {
                if ((x.IsOrder && y.IsOrder) || (!x.IsOrder && !y.IsOrder))
                {
                    return num3.CompareTo(num4);
                }

                if (x.IsOrder)
                {
                    return -1;
                }

                return y.IsOrder ? 1 : 0;
            }
            return firstPrice == 0.0 || secondPrice == 0.0 ? secondPrice.CompareTo(firstPrice) : firstPrice.CompareTo(secondPrice);
        }
    }
}
