using DevExpress.Mvvm.UI;
using DevExpress.Xpf.Editors;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Controls
{
    public class TokenAutoSuggestEdit : AutoSuggestEdit
    {
        private ComboBoxEdit _innerTokenComboBox;
        public static readonly DependencyProperty SelectedTokensProperty = DependencyProperty.Register(nameof(SelectedTokens), typeof(List<object>), typeof(TokenAutoSuggestEdit));

        public List<object> SelectedTokens
        {
            get => (List<object>)GetValue(SelectedTokensProperty);
            set => SetValue(SelectedTokensProperty, value);
        }

        public TokenAutoSuggestEdit()
        {
            EditValueChanged += TokenAutoSuggestEdit_EditValueChanged;
            Loaded += TokenAutoSuggestEdit_Loaded;
        }

        private void TokenAutoSuggestEdit_Loaded(object sender, RoutedEventArgs e)
        {
            _innerTokenComboBox = LayoutTreeHelper.GetVisualChildren(this).OfType<ComboBoxEdit>().First(el => el.Name == "innerTokenComboBox");
            _innerTokenComboBox.EditValueChanged += InnerTokenComboBox_EditValueChanged;
        }

        private void InnerTokenComboBox_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            SelectedTokens = (List<object>)((ComboBoxEdit)sender).EditValue;
        }

        private void TokenAutoSuggestEdit_EditValueChanged(object sender, EditValueChangedEventArgs e)
        {
            if (e.NewValue == null)
            {
                return;
            }

            List<object> selectedTokens;
            if (_innerTokenComboBox.EditValue is List<object> previousTokens)
            {
                selectedTokens = previousTokens.Concat(new[] { e.NewValue }).ToList();
            }
            else
            {
                selectedTokens = new List<object>() { e.NewValue };
            }

            _innerTokenComboBox.ItemsSource = selectedTokens;
            _innerTokenComboBox.EditValue = selectedTokens;
            ((AutoSuggestEdit)sender).EditValue = null;
        }
    }
}
