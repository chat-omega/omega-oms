using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.Services
{
    internal interface ISupportGettingItemsByVisualOrder
    {
        Dispatcher Dispatcher { get; }
        List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow, bool renderedOnly);
        bool ItemIsVisible(object item);
        HashSet<T> GetVisibleItems<T>();
    }
}
