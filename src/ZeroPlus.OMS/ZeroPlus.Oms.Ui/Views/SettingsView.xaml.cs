using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.ViewModels;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for SettingsView.xaml
    /// </summary>
    public partial class SettingsView : ThemedWindow
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
            Closed += SettingsView_Closed;
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.SetDispatcher(Dispatcher);
            }
        }

        private void SettingsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= SettingsView_Loaded;
            StartupWindowViewModel.MainWindow?.WindowHelper?.AddWindow(this);
        }

        private void SettingsView_Closed(object sender, System.EventArgs e)
        {
            Closed -= SettingsView_Closed;
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.SaveSettings();
            }
        }

        private void SpinEdit_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is BaseEdit spinEdit)
            {
                spinEdit.SelectAll();
            }
        }
        private void HotkeyEdit_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            Key key = e.Key;
            var k = e.Key.ToString();
            if (k.Length != 1 || !char.IsLetter(k[0])) return;
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.None) return;
            ModifierKeys modifier = Keyboard.Modifiers;

            if (sender is ButtonEdit editor && editor.DataContext is SettingsViewModel viewModel)
            {
                KeyGesture gesture = new(key, modifier);
                var keyString = gesture.GetDisplayStringForCulture(CultureInfo.CurrentUICulture);
                editor.Text = viewModel.Config.BasketBorderKeyGesture = keyString;
                e.Handled = true;
            }
        }

        private void HotkeyEdit_Clear_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (HotkeyEdit_BasketBorder is ButtonEdit editor && editor.DataContext is SettingsViewModel viewModel)
            {
                viewModel.Config.BasketBorderKeyGesture = null;
                editor.Text = null;
                e.Handled = true;
            }
        }

        private void HotkeyEdit_BasketBorder_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is ButtonEdit editor && editor.DataContext is SettingsViewModel viewModel)
            {
                editor.Text = viewModel.Config.BasketBorderKeyGesture;
                e.Handled = true;
            }
        }

        private void AlwaysChecked(object sender, EditValueChangingEventArgs e)
        {
            if (e.NewValue is false)
            {
                e.IsCancel = true;
                e.Handled = true;
            }
        }
    }
}
