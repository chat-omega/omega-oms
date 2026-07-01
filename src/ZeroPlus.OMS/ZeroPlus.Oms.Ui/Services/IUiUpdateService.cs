namespace ZeroPlus.Oms.Ui.Services
{
    public interface IUiUpdateService
    {
        void ClearSorting();
        void BeginUpdate();
        void EndUpdate();
        void RefreshData();
        void ReapplyFilter(params string[] columnNames);
    }
}
