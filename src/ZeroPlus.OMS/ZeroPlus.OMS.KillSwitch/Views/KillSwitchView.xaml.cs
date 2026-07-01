using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ZeroPlus.OMS.KillSwitch.Views
{
    /// <summary>
    /// Interaction logic for KillSwitchView.xaml
    /// </summary>
    public partial class KillSwitchView : Window
    {
        public KillSwitchView()
        {
            InitializeComponent();
            Loaded += OnViewLoaded;
        }

        private void OnViewLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnViewLoaded;
            Left = SystemParameters.WorkArea.Right - Width;
            Top = SystemParameters.WorkArea.Bottom - Height;
        }

        private void KillOms(object sender, RoutedEventArgs e)
        {
            string processName = "ZeroPlus OMS";
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                KillProcess(process);
            }
        }

        private static void KillProcess(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
            }
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            try
            {
                Environment.Exit(0);
            }
            catch (Exception)
            {
            }
        }

        private void Move(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}
