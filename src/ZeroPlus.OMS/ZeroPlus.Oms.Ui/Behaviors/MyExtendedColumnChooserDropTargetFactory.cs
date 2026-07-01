using DevExpress.Xpf.Core;
using DevExpress.Xpf.Grid;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Behaviors
{
    public class MyExtendedColumnChooserDropTargetFactory : GridDropTargetFactoryBase
    {
        protected sealed override IDropTarget CreateDropTarget(UIElement dropTargetElement)
        {
            if (dropTargetElement is not ExtendedColumnChooserTreeListView view)
            {
                return null;
            }

            if (view.DataContext is not ExtendedColumnChooserViewModel viewModel)
            {
                return null;
            }

            return new MyExtendedColumnChooserDropTarget();
        }

        public override bool IsCompatibleDropTargetFactory(UIElement dropTargetElement, BaseGridHeader sourceHeader)
        {
            if (dropTargetElement is not ExtendedColumnChooserTreeListView view)
            {
                return false;
            }

            BaseColumn column = BaseGridHeader.GetGridColumn(sourceHeader);
            if (column == null || column.View == null)
            {
                return false;
            }

            return IsCompatibleDataControl(view.OriginalDataControl, column.View.DataControl);
        }
    }

    public class MyExtendedColumnChooserDropTarget : IDropTarget
    {
        public void Drop(UIElement source, Point pt) { }
        public void OnDragLeave() { }
        public void OnDragOver(UIElement source, Point pt) { }
    }
}
