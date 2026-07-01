using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System.Windows.Input;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LoadInBasketTraderPromptView.xaml
    /// </summary>
    public partial class LoadInBasketTraderPromptView : ThemedWindow
    {
        public LoadInBasketTraderPromptView()
        {
            InitializeComponent();
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
