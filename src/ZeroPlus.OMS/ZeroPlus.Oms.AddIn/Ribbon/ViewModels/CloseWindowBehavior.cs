using Microsoft.Xaml.Behaviors;
using System.Windows;

namespace ZeroPlus.Oms.AddIn.Ribbon.ViewModels
{
    public class CloseWindowBehavior : Behavior<Window>
    {
        public bool CloseTrigger
        {
            get => (bool)GetValue(CloseTriggerProperty);
            set => SetValue(CloseTriggerProperty, value);
        }

        public static readonly DependencyProperty CloseTriggerProperty =
            DependencyProperty.Register("CloseTrigger", typeof(bool), typeof(CloseWindowBehavior), new PropertyMetadata(false, OnCloseTriggerChanged));

        private static void OnCloseTriggerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            CloseWindowBehavior behavior = d as CloseWindowBehavior;

            behavior?.OnCloseTriggerChanged();
        }

        private void OnCloseTriggerChanged()
        {
            if (CloseTrigger)
            {
                AssociatedObject.Close();
            }
        }
    }
}
