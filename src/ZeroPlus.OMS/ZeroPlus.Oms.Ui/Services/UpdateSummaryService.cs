using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;
using System;

namespace ZeroPlus.Oms.Ui.Services
{
    public class UpdateSummaryService : ServiceBase, IUpdateSummaryService
    {
        private GridControl AssociatedGrid => AssociatedObject as GridControl;

        public void UpdateSummary()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    AssociatedGrid.UpdateTotalSummary();
                }
                catch (Exception)
                {
                }
            }));
        }
    }
}
