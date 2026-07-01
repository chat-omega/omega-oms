using DevExpress.Xpf.Core;
using System;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for LoadDomListView.xaml
    /// </summary>
    public partial class LoadDomListView : ThemedWindow, IModuleView
    {
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public LoadDomListView()
        {
            InitializeComponent();
        }

        private void GridControl_CustomColumnsDisplayText(object sender, DevExpress.Xpf.Grid.CustomColumnDisplayTextEventArgs e)
        {
            if (e.Value is DateTime dateTime)
            {
                if (e.Column.FieldName == "DateMade")
                {
                    e.DisplayText = dateTime.ToUniversalTime() == DateTime.MinValue ? "" : dateTime.ToString("d");
                }
            }
        }
    }
}
