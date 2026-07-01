using System;
using System.Diagnostics;
using System.Windows;

namespace ZeroPlus.OMS.KillSwitch
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string processName = Process.GetCurrentProcess().ProcessName;
            Process[] processes = Process.GetProcessesByName(processName);
            if (processes.Length > 1)
            {
                Environment.Exit(0);
            }
        }
    }
}
