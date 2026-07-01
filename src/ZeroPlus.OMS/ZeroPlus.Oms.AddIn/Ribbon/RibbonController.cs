using ExcelDna.Integration;
using ExcelDna.Integration.CustomUI;
using NLog;
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using ZeroPlus.Oms.AddIn.Ribbon.Views;

namespace ZeroPlus.Oms.AddIn.Ribbon
{
    [ComVisible(true)]
    public class RibbonController : ExcelRibbon
    {
        private const string RIBBON_TITLE = "ZeroPlus OMS";

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private IRibbonUI _ribbonUI;
        private static bool _activeWindow;
        private static Application _mainApplication;
        private static readonly object _windowLock = new();

        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public void OnRibbonLoad(IRibbonUI ribbon)
        {
            _ribbonUI = ribbon;
            if (OmsCore != null)
            {
                OmsCore.GatewayClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.UpdateManager.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.InterpolatorClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.TheosClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.AutoTraderClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.FullEmaClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                OmsCore.HerculesClientWrapper.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;

                if (OmsCore.Config.DominatorClientEnabled)
                {
                    OmsCore.DominatorClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
                    _ = OmsCore.DominatorClient.StartAsync();
                }
            }
        }

        public override string GetCustomUI(string RibbonID)
        {
            string markup = GetUIMarkup();
            if (!string.IsNullOrEmpty(markup))
            {
                return markup;
            }
            else
            {
                return base.GetCustomUI(RibbonID);
            }
        }

        public string GetRibbonTitle(IRibbonControl _)
        {
            if (ExcelDnaUtil.ExcelVersion > 15)
            {
                return RIBBON_TITLE;
            }
            else
            {
                return RIBBON_TITLE.ToUpper();
            }
        }

        public bool LoginEnabled(IRibbonControl _)
        {
            return OmsCore is { User: null };
        }

        public bool UsernameEnabled(IRibbonControl _)
        {
            return OmsCore?.User != null;
        }

        public string GetUsername(IRibbonControl _)
        {
            if (OmsCore is { User: not null })
            {
                return OmsCore.User.Username;
            }
            else
            {
                return "";
            }
        }

        public string GetDomsManagerStatus(IRibbonControl _)
        {
            try
            {
                if (OmsCore.Config.DominatorClientEnabled)
                {
                    return OmsCore is { DominatorClient.IsConnected: true } ? "Connected" : "Disconnected";
                }
                else
                {
                    return "Disabled";
                }
            }
            catch (Exception)
            {
                return "Disabled";
            }
        }

        public string GetRaptorStatus(IRibbonControl _)
        {
            return OmsCore is { UpdateManager.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetHerculesStatus(IRibbonControl _)
        {
            return OmsCore is { HerculesClientWrapper.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetFullEmaClientStatus(IRibbonControl _)
        {
            return OmsCore is { FullEmaClient.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetInterpolatorStatus(IRibbonControl _)
        {
            return OmsCore is { InterpolatorClient.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetTheosStatus(IRibbonControl _)
        {
            return OmsCore is { TheosClient.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetAdjEmaStatus(IRibbonControl _)
        {
            return OmsCore is { FullEmaClient.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetAutoTraderStatus(IRibbonControl _)
        {
            return OmsCore is { AutoTraderClient.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetTransactionsStatus(IRibbonControl _)
        {
            return OmsCore is { HerculesClientWrapper.IsConnected: true } ? "Connected" : "Disconnected";
        }

        public string GetVersion(IRibbonControl _)
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return $"V-{version?.ToString(3)}";
        }

        public Bitmap LoadImage(IRibbonControl control, string imageName)
        {
            try
            {
                string xllDir = Path.GetDirectoryName(ExcelDnaUtil.XllPath);
                string imagePath = Path.Combine(xllDir, "Images", $"{imageName}.png");
                if (File.Exists(imagePath))
                {
                    return new Bitmap(imagePath);
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(LoadImage));
            }
            return null;
        }

        public void OnAboutButtonPressed(IRibbonControl control)
        {
            // No-op — About button has no action currently
        }

        public void OnLoginButtonPressed(IRibbonControl control)
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                lock (_windowLock)
                {
                    if (_activeWindow)
                    {
                        return;
                    }

                    _activeWindow = true;
                    if (Application.Current == null && _mainApplication == null)
                    {
                        _mainApplication = new Application()
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };
                    }
                    else
                    {
                        _mainApplication = Application.Current;
                    }

                    MainWindowView startupView = new();
                    if (Application.Current != null && _mainApplication != null)
                    {
                        _mainApplication.MainWindow = startupView;
                    }
                    startupView.Closed += (_, _) => RefreshUiItems();
                    startupView.ShowDialog();
                    _activeWindow = false;
                }
            });
        }

        public void OnReconnectButtonPressed(IRibbonControl control)
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                try
                {
                    _ = OmsCore?.GatewayClient?.RestartAsync();
                    _ = OmsCore?.UpdateManager?.RestartAsync();
                    _ = OmsCore?.FullEmaClient?.RestartAsync();
                    _ = OmsCore?.InterpolatorClient?.RestartAsync();
                    _ = OmsCore?.TheosClient?.RestartAsync();
                    _ = OmsCore?.AutoTraderClient?.RestartAsync();
                    _ = OmsCore?.HerculesClientWrapper?.RestartAsync();
                    _ = OmsCore?.DominatorClient?.RestartAsync();
                    RefreshUiItems();
                }
                catch (Exception ex)
                {
                    _log?.Error(ex, nameof(OnReconnectButtonPressed));
                }
            });
        }

        public void OnLogoutButtonPressed(IRibbonControl control)
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                OmsCore.User = null;
                RefreshUiItems();
            });
        }

        public void OnSettingsButtonPressed(IRibbonControl control)
        {
            ExcelAsyncUtil.QueueAsMacro(() =>
            {
                lock (_windowLock)
                {
                    if (_activeWindow)
                    {
                        return;
                    }

                    _activeWindow = true;
                    if (Application.Current == null && _mainApplication == null)
                    {
                        _mainApplication = new Application()
                        {
                            ShutdownMode = ShutdownMode.OnExplicitShutdown
                        };
                    }
                    else
                    {
                        _mainApplication = Application.Current;
                    }

                    SettingsWindowView settingsView = new();
                    if (Application.Current != null && _mainApplication != null)
                    {
                        _mainApplication.MainWindow = settingsView;
                    }

                    settingsView.ShowDialog();
                    _activeWindow = false;
                }
            });
        }

        public void OnWriteFillsCheckChange(IRibbonControl control, bool selected)
        {
            RefreshUiItems();
        }

        private string GetUIMarkup()
        {
            string markup = string.Empty;

            try
            {
                using Stream stream = GetType().Assembly.GetManifestResourceStream("ZeroPlus.Oms.AddIn.Ribbon.RibbonUI.xml");
                if (stream != null)
                {
                    using StreamReader sr = new(stream);
                    markup = sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                _log?.Error(ex, nameof(GetUIMarkup));
            }

            return markup;
        }

        private void OnConnectionStatusChangedEvent(bool connected)
        {
            RefreshUiItems();
        }

        private void RefreshUiItems()
        {
            try
            {
                _ribbonUI?.Invalidate();
            }
            catch (Exception)
            {
                // Ignore
            }
        }
    }
}