using System;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.Views.Interfaces
{
    public interface IModuleView
    {
        public event RoutedEventHandler Loaded;
        public event EventHandler Closed;

        public string Uid { get; set; }
        public object DataContext { get; set; }
        public Dispatcher Dispatcher { get; }
        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public void Show();
    }
}
