using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for OptionSelectorView.xaml
    /// </summary>
    public partial class OptionSelectorView : ThemedWindow
    {
        public OptionSelectorViewModel ViewModel { get; }

        public OptionSelectorView()
        {
            InitializeComponent();
            ViewModel = DataContext as OptionSelectorViewModel;
        }

        private void AutoSuggestEdit_QuerySubmitted(object sender, AutoSuggestEditQuerySubmittedEventArgs e)
        {
            if (sender is not AutoSuggestEdit suggestEdit)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(e.Text) || ViewModel.Options == null || ViewModel.Options.Count == 0)
            {
                suggestEdit.ItemsSource = null;
                suggestEdit.ClosePopup();
            }
            else
            {
                IEnumerable<string> match = ViewModel.Options.Where(x => x.Contains(e.Text.ToUpper())).ToList();
                suggestEdit.ItemsSource = match.Count() > 20 ? match.Take(20).ToArray() : match.ToArray();
                if (match.Any())
                {
                    suggestEdit.ShowPopup();
                }
            }
        }
    }
}
