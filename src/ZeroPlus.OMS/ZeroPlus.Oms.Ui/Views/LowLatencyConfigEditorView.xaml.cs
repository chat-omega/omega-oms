using DevExpress.Xpf.Core;
using System.Windows;
using System.Windows.Controls;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LowLatencyConfigEditorView.xaml
    /// </summary>
    public partial class LowLatencyConfigEditorView : ThemedWindow
    {
        public LowLatencyConfigEditorViewModel ViewModel => DataContext as LowLatencyConfigEditorViewModel;

        public Module Module { get; private set; }

        public LowLatencyConfigEditorView()
        {
            InitializeComponent();
            SetQuickAccessWidth();
        }

        internal void SetupConfigView(Module module)
        {
            Module = module;
            UserControl config = null;
            switch (module)
            {
                case Module.LoLaInitiator:
                    config = new LowLatencyInitiatorConfigView
                    {
                        DataContext = (InitiatorModel)ViewModel.Model
                    };
                    break;
                case Module.LoLaLiquidator:
                    config = new LowLatencyLiquidatorConfigView
                    {
                        DataContext = (LiquidatorModel)ViewModel.Model
                    };
                    break;
                case Module.LoLaLoop:
                    config = new LowLatencyLoopConfigView
                    {
                        DataContext = (LoopModel)ViewModel.Model
                    };
                    break;
                case Module.LoLaRisk:
                    config = new LowLatencyRiskConfigView
                    {
                        DataContext = (LowLatencyRiskModel)ViewModel.Model
                    };
                    break;
                case Module.LoLaSignal:
                    config = new LowLatencySignalConfigView
                    {
                        DataContext = (SignalModel)ViewModel.Model
                    };
                    break;
            }

            if (config != null)
            {
                double width = config.MinWidth + 20;
                if (Width < width)
                {
                    Width = width;
                }
                Container.Children.Add(config);
            }
        }

        private void ExpandCollapseGrid_Click(object sender, RoutedEventArgs e)
        {
            SetQuickAccessWidth();
        }

        private void SetQuickAccessWidth()
        {
            GridLengthConverter glc = new();
            if (GridSplitterCol.Width.Value > 0)
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("0");
                ExpandCollapseGridButton.Content = 4;
            }
            else
            {
                GridSplitterCol.Width = (GridLength)glc.ConvertFromString("400");
                ExpandCollapseGridButton.Content = 3;
            }
        }
    }
}
