using System;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Services
{
    public interface IGetItemsByVisualOrderService
    {
        List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow = false, bool renderedOnly = false);
        bool ItemIsVisible(object item);
        HashSet<T> GetVisibleItems<T>();
    }
}
