using DevExpress.Xpf.Editors;
using System.Windows;
using System.Windows.Input;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for ModuleHeaderItems.xaml
    /// </summary>
    public partial class ModuleHeaderItems
    {
        public bool ShowSaveLocation { get; set; }

        public bool ShowNameEditor
        {
            set => NameEditButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

        public static readonly DependencyProperty AdditionalMenuItemsContentProperty =
            DependencyProperty.Register(nameof(AdditionalMenuItemsContent), typeof(DataTemplate), typeof(ModuleHeaderItems), new PropertyMetadata(null));

        public DataTemplate AdditionalMenuItemsContent
        {
            get => (DataTemplate)GetValue(AdditionalMenuItemsContentProperty);
            set => SetValue(AdditionalMenuItemsContentProperty, value);
        }

        public ModuleHeaderItems()
        {
            InitializeComponent();
            ShowNameEditor = false;
        }

        public ModuleWindow ParentWindow => Window.GetWindow(this) as ModuleWindow;

        private void ClearFiltersClick(object sender, RoutedEventArgs e)
        {
            ParentWindow?.ClearFiltersClick();
        }

        private void ClearSortingClick(object sender, RoutedEventArgs e)
        {
            ParentWindow?.ClearSortingClick();
        }

        private void CloneModule(object sender, RoutedEventArgs e)
        {
            ParentWindow?.CloneModule();
        }

        private void ShareLayout(object sender, RoutedEventArgs e)
        {
            ParentWindow?.ShareLayout();
        }

        private void SaveLayout(object sender, RoutedEventArgs e)
        {
            ParentWindow?.ShowSaveLayoutPrompt(ShowSaveLocation);
        }

        private void LoadLayout(object sender, RoutedEventArgs e)
        {
            ParentWindow?.LoadLayout();
        }

        private void EditName(object sender, RoutedEventArgs e)
        {
            NameEdit.Visibility = Visibility.Visible;
            NameEdit.Focusable = true;
            NameEdit.Cursor = Cursors.IBeam;
            NameEdit.SelectAll();
            NameEdit.Focus();
        }

        private void NameKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Reset();
            }
        }

        private void SetName(object sender, RoutedEventArgs e)
        {
            Reset();
        }

        private void Reset()
        {
            NameEdit.Visibility = Visibility.Collapsed;
            NameEdit.Focusable = false;
            NameEdit.Cursor = Cursors.Arrow;
            NameEdit.CaretIndex = NameEdit.Text.Length;
            NameEditButton.IsChecked = false;
        }

        private void SelectAll(object sender, MouseButtonEventArgs e)
        {
            if (sender is BaseEdit baseEdit)
            {
                baseEdit.SelectAll();
            }
        }
    }
}
