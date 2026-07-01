using DevExpress.Mvvm.UI;
using System;
using System.Collections.Generic;

namespace ZeroPlus.Oms.Ui.Services
{
    public class GetItemsByVisualOrderService : ServiceBase, IGetItemsByVisualOrderService
    {
        private ISupportGettingItemsByVisualOrder AssociatedView => AssociatedObject as ISupportGettingItemsByVisualOrder;

        public List<Tuple<int, object>> GetItemsByVisualOrder(bool startFromSelectedRow = false, bool renderedOnly = false)
        {
            List<Tuple<int, object>> items = null;
            AssociatedView.Dispatcher.Invoke(() => items = AssociatedView.GetItemsByVisualOrder(startFromSelectedRow, renderedOnly));
            return items;
        }

        public HashSet<T> GetVisibleItems<T>()
        {
            HashSet<T> items = null;
            AssociatedView.Dispatcher.Invoke(() => items = AssociatedView.GetVisibleItems<T>());
            return items;
        }

        public bool ItemIsVisible(object item)
        {
            bool isVisible = false;
            AssociatedView.Dispatcher.Invoke(() => isVisible = AssociatedView.ItemIsVisible(item));
            return isVisible;
        }
    }
}
