using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Xpf.Grid;
using System.Collections;
using System.Linq;
using System.Reflection;
using ZeroPlus.Models.Attributes;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public class CustomizableTableViewModelBase : ViewModelBase
    {
        public Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();

        [Command]
        public void ShowColumnChooser(TableView tableView)
        {
            tableView?.ShowColumnChooser();
        }

        [Command]
        public void ClearColumns(TableView tableView)
        {
            if (!ShowLayoutVerificationMessageBox("Are you sure you want to clear all columns?"))
                return;

            var grid = tableView?.Grid;
            foreach (GridColumn col in grid.Columns.ToList())
            {
                col.Visible = false;
            }
            tableView.ShowColumnChooser();
        }

        [Command]
        public void ShowAllColumns(TableView tableView)
        {
            if (!ShowLayoutVerificationMessageBox("Are you sure you want to select all columns?"))
                return;

            var grid = tableView?.Grid;
            foreach (GridColumn col in grid.Columns.ToList())
            {
                col.Visible = true;
            }
            tableView.ShowColumnChooser();
        }

        [Command]
        public void ShowDefaultColumns(TableView tableView)
        {
            if (!ShowLayoutVerificationMessageBox("Are you sure you want to show only the default columns?"))
                return;

            var grid = tableView?.Grid;
            var itemsSource = grid?.ItemsSource;
            if (itemsSource == null)
                return;
            var type = itemsSource.GetType();
            if (type.IsGenericType)
                type = type.GetGenericArguments()[0];
            else if (itemsSource is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    type = item?.GetType();
                    break;
                }
            }

            foreach (GridColumn col in grid.Columns.ToList())
            {
                var property = type.GetProperty(col.FieldName);
                var attribute = property?.GetCustomAttribute<GridVisibleByDefaultAttribute>();
                if (attribute == null)
                    col.Visible = false;
                else
                    col.Visible = true;
            }
        }

        private bool ShowLayoutVerificationMessageBox(string message)
        {
            return MessageBoxService?.Show(message,
                                           "Layout Verification",
                                           MessageButton.YesNo,
                                           MessageIcon.Warning,
                                           MessageResult.Yes) == MessageResult.Yes;
        }
    }
}
