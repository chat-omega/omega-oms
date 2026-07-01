using DevExpress.Xpf.Core;
using DevExpress.Xpf.Editors;
using System;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for PositionManagerView.xaml
    /// </summary>
    public partial class PositionManagerView : ThemedWindow, IModuleView
    {
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public PositionManagerView()
        {
            InitializeComponent();
        }

        private void SpinEdit_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                SpinEdit spinEdit = (SpinEdit)sender;
                spinEdit.SelectAll();
            }
            catch (Exception)
            {
            }
        }
    }
}
