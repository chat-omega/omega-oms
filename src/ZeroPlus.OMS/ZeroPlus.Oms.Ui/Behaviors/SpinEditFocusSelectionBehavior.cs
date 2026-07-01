using DevExpress.Mvvm.UI.Interactivity;
using DevExpress.Xpf.Editors;
using System;
using System.Windows.Input;
using System.Windows.Threading;

namespace ZeroPlus.Oms.Ui.Behaviors
{
    internal class SpinEditFocusSelectionBehavior : Behavior<SpinEdit>
    {
        protected override void OnAttached()
        {
            AssociatedObject.PreviewMouseLeftButtonDown += AssociatedObjectOnPreviewMouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseLeftButtonDown -= AssociatedObjectOnPreviewMouseLeftButtonDown;
        }

        private void AssociatedObjectOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AssociatedObject.SelectAll();
            }), DispatcherPriority.Input);
        }
    }
}
