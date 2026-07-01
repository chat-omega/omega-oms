using DevExpress.Mvvm.UI;
using System;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Services
{
    public class LoadCustomColumnService : ServiceBase, ILoadCustomColumnService
    {
        private ISupportCustomColumn AssociatedView => AssociatedObject as ISupportCustomColumn;

        public void AddCustomColumn(CustomColumnTemplateModel colTemplate)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                AssociatedView.AddColumn(colTemplate);
            }));
        }
    }
}
