using DevExpress.Xpf.Core;
using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Oms.Config;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Views.Interfaces;

namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for NotificationsView.xaml
    /// </summary>
    public partial class NotificationsView : ThemedWindow, IModuleView
    {
        private readonly OmsConfig _config;
        private Point _lastLocation;
        private bool _locationChanged;

        public Module Module { get; set; }
        public ConfigSave ConfigSave { get; set; }

        public NotificationsView(OmsConfig config)
        {
            InitializeComponent();
            _config = config;
            BorderThickness = new Thickness(0);
            Margin = new Thickness(4, 5, 4, 5);
            Name = nameof(NotificationsView);
            ShowInTaskbar = true;
            ShowTitle = false;
            ShowGlow = false;
            ShowIcon = false;
            ShowActivated = false;
            Topmost = true;
            Loaded += NotificationsView_Loaded;
            MouseDown += NotificationsView_MouseClick;
            MouseUp += NotificationsView_MouseUp;
            LocationChanged += NotificationsView_LocationChanged;
        }

        private void NotificationsView_LocationChanged(object sender, EventArgs e)
        {
            _lastLocation = PointToScreen(new Point(0, 0));
            _locationChanged = true;
        }

        private void NotificationsView_Loaded(object sender, RoutedEventArgs e)
        {
            Point point = new(_config.GlobalNotificationSettingsX, _config.GlobalNotificationSettingsY);
            if (point.X == 0 || point.Y == 0)
            {
                Screen screen = Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));
                point.X = screen.Bounds.Width - Width;
                point.Y = screen.Bounds.Top + 20;
            }

            if (point.X <= SystemParameters.VirtualScreenWidth - Width)
            {
                Left = point.X;
            }

            if (point.Y <= SystemParameters.VirtualScreenHeight - Height)
            {
                Top = point.Y;
            }

            _lastLocation = point;

            Visibility = Visibility.Collapsed;
        }

        private void NotificationsView_MouseClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void NotificationsView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_locationChanged)
            {
                _config.GlobalNotificationSettingsX = _lastLocation.X;
                _config.GlobalNotificationSettingsY = _lastLocation.Y;
                _locationChanged = false;
            }
        }
    }
}
