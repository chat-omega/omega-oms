using System.Windows;

namespace ZeroPlus.Oms.AddIn.Ribbon.Views
{
    /// <summary>
    /// Interaction logic for MainWindowView.xaml
    /// </summary>
    public partial class MainWindowView : Window
    {
        public MainWindowView()
        {
            Microsoft.Xaml.Behaviors.EventTrigger behavior = new();
            InitializeComponent();
        }
    }
}
