using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Grid;

namespace ZeroPlus.Oms.Ui.Services
{
    public class UiUpdateService : ServiceBase, IUiUpdateService
    {
        private GridControl ActuaGridControl => AssociatedObject as GridControl;

        public void BeginUpdate()
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                ActuaGridControl.BeginDataUpdate();
            }));
        }

        public void ClearSorting()
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                ActuaGridControl.ClearSorting();
            }));
        }

        public void EndUpdate()
        {
            Dispatcher.Invoke(new System.Action(() =>
            {
                ActuaGridControl.EndDataUpdate();
            }));
        }

        public void RefreshData()
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                ActuaGridControl.RefreshData();
            }));
        }

        public void ReapplyFilter(params string[] columnNames)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                var grid = ActuaGridControl;
                var filterString = grid.FilterString;
                if (string.IsNullOrEmpty(filterString))
                    return;

                foreach (var name in columnNames)
                {
                    if (filterString.Contains("[" + name + "]"))
                    {
                        var criteria = grid.FilterCriteria;
                        grid.FilterCriteria = null;
                        grid.FilterCriteria = criteria;
                        return;
                    }
                }
            }));
        }
    }
}
