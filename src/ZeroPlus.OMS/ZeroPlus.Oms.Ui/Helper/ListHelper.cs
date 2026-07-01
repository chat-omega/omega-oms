using System;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class ListHelper
    {
        public static void Shuffle<T>(IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException(nameof(list));
            }

            Random random = new();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = random.Next(n + 1);
                (list[n], list[k]) = (list[k], list[n]);
            }
        }
    }
}
