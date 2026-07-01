using DevExpress.CodeParser;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Helper
{
    public class WindowHelper
    {
        private const string STARTUP_MODULES_FILE = "startup_modules.json";
        private const string MODULE_LAYOUT_FILE = "layout_state.json";
        private const long GWL_HWNDPARENT = -8;

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly object _windowsLock = new();
        private readonly HashSet<Window> _windows = new();
        private readonly Dictionary<string, HashSet<Window>> _windowNameToWindowsMap = new();
        private readonly Dictionary<string, WindowSetting> _windowIdToWindowSettingsMap = new();
        private readonly Window _mainWindow;
        private readonly IntPtr _windowHandleOwner;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowLongPtr(IntPtr hwnd, long index, int newStyle);
        public static List<(string windowName, string windowGuid)> NameAndUidPairsForStartupModules { get; private set; }
        private OmsCore OmsCore { get; }

        public WindowHelper(MainView mainWindow, OmsCore omsCore)
        {
            OmsCore = omsCore;
            _mainWindow = mainWindow;
            _windowHandleOwner = GetHandler(_mainWindow);
            RestoreLayout(mainWindow);
            LoadLayout();
            OmsCore.SaveWorkspaceRequestEvent += SaveWorkspace;
        }

        public Window FindParentWindow(DependencyObject child)
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);

            return parent switch
            {
                null => null,
                Window parentWindow => parentWindow,
                _ => FindParentWindow(parent),
            };
        }

        public void AddWindow(Window window)
        {
            try
            {
                if (window == null)
                {
                    return;
                }
                var name = window.Name;
                window.Closed += Window_Closed;
                window.IsVisibleChanged += Window_IsVisibleChanged;
                IntPtr windowHandleOwned = GetHandler(window);
                _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                {
                    SetOwnerWindow(windowHandleOwned, _windowHandleOwner);
                }));
                if (window.Visibility == Visibility.Visible)
                {
                    Task.Run(() =>
                    {
                        lock (_windowsLock)
                        {
                            _windows.Add(window);
                            if (name != null)
                            {
                                if (!_windowNameToWindowsMap.TryGetValue(name, out var windows))
                                {
                                    windows = new HashSet<Window>();
                                    _windowNameToWindowsMap[name] = windows;
                                }
                                windows.Add(window);
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(AddWindow));
            }
        }

        public IntPtr GetHandler(Window window)
        {
            WindowInteropHelper interop = new(window);
            return interop.Handle;
        }

        public void SetOwnerWindow(IntPtr windowHandleOwned, IntPtr windowHandleOwner)
        {
            if (windowHandleOwned != IntPtr.Zero && windowHandleOwner != IntPtr.Zero)
            {
                SetWindowLongPtr(windowHandleOwned, GWL_HWNDPARENT, windowHandleOwner.ToInt32());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (sender is Window window)
            {
                window.Closed -= Window_Closed;
                window.IsVisibleChanged -= Window_IsVisibleChanged;
                if (_windows.Contains(window))
                {
                    RemoveWindow(window);
                }
                ActivateMainWindow();
            }
        }

        private void ActivateMainWindow()
        {
            _mainWindow?.Dispatcher?.BeginInvoke(() => _mainWindow.Activate());
        }

        internal void RemoveWindow(Window window)
        {
            try
            {
                if (window != null)
                {
                    lock (_windowsLock)
                    {
                        _windows.Remove(window);
                        var name = window.Name;
                        if (name != null)
                        {
                            _windowIdToWindowSettingsMap.Remove(window.Uid);
                            if (_windowNameToWindowsMap.TryGetValue(name, out HashSet<Window> windows))
                            {
                                windows.Remove(window);
                                if (windows.Count == 0)
                                {
                                    _windowNameToWindowsMap.Remove(name);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "{}, {}, {}", nameof(RemoveWindow), window?.Name, window?.Uid);
            }
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            try
            {
                if (sender is Window window)
                {
                    if (window.Visibility == Visibility.Hidden)
                    {
                        lock (_windowsLock)
                        {
                            _windows.Remove(window);
                        }
                    }
                    else if (window.Visibility == Visibility.Visible && window.IsVisible)
                    {
                        lock (_windowsLock)
                        {
                            _windows.Add(window);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Window_IsVisibleChanged));
            }
        }

        public void SaveWorkspace()
        {
            try
            {
                SaveOpenWindowLayout();
                SaveOpenWindowSettings();
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"{nameof(SaveWorkspace)}");
            }
        }

        private void SaveOpenWindowLayout()
        {
            foreach (string savedWindowUid in _windowIdToWindowSettingsMap.Keys)
            {
                if (!string.IsNullOrWhiteSpace(savedWindowUid) && (savedWindowUid.StartsWith("BI") || savedWindowUid.Count(c => c == '-') == 4))
                {
                    _windowIdToWindowSettingsMap.Remove(savedWindowUid);
                }
            }

            IEnumerable<Window> allWindows = _windows.Union(new List<Window> { _mainWindow });
            foreach (Window window in allWindows)
            {
                try
                {
                    if (!window.Dispatcher.HasShutdownStarted &&
                        !window.Dispatcher.HasShutdownFinished)
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            WindowSetting windowSetting = new(window);
                            _windowIdToWindowSettingsMap[window.Uid] = windowSetting;
                        });
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(SaveWorkspace));
                }
            }

            string layoutStatePath = GetLayoutExportFilePath();
            string layoutStateJson = JsonConvert.SerializeObject(_windowIdToWindowSettingsMap, Formatting.Indented);
            File.WriteAllText(layoutStatePath, layoutStateJson);
        }

        private void SaveOpenWindowSettings()
        {
            string openWindowsPath = GetOpenWindowsExportFilePath();
            List<Tuple<string, string>> windows = new();
            foreach (Window window in _windows)
            {
                try
                {
                    if (!window.Dispatcher.HasShutdownStarted &&
                        !window.Dispatcher.HasShutdownFinished)
                    {
                        window.Dispatcher.Invoke(() =>
                        {
                            if (OmsCore.Config.AllowSavingEdgeScanFeedBasketsWithWorkspace ||
                                window is not BasketTraderView ||
                                window.DataContext is not BasketTraderViewModel viewModel ||
                                !viewModel.IsEdgeScanFeedAutoTrader)
                            {
                                windows.Add(Tuple.Create(window.Name, window.Uid));
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(SaveWorkspace));
                }
            }
            string openWindowsJson = JsonConvert.SerializeObject(windows, Formatting.Indented);
            File.WriteAllText(openWindowsPath, openWindowsJson);
        }

        private void LoadLayout()
        {
            string layoutStatePath = GetLayoutExportFilePath();

            if (File.Exists(layoutStatePath))
            {
                string myFileStream = File.ReadAllText(layoutStatePath);
                var windowIdToWindowSettingsMap = JsonConvert.DeserializeObject<Dictionary<string, WindowSetting>>(myFileStream);
                if (windowIdToWindowSettingsMap != null)
                {
                    foreach (var kvp in windowIdToWindowSettingsMap)
                    {
                        _windowIdToWindowSettingsMap[kvp.Key] = kvp.Value;
                    }
                }
            }

            string openWindowsPath = GetOpenWindowsExportFilePath();

            if (File.Exists(openWindowsPath))
            {
                string myFileStream = File.ReadAllText(openWindowsPath);
                NameAndUidPairsForStartupModules = JsonConvert.DeserializeObject<List<(string windowName, string windowGuid)>>(myFileStream);
            }
            else
            {
                NameAndUidPairsForStartupModules = new List<(string windowName, string windowGuid)>();
            }
        }

        internal void RestoreLayout(Window window)
        {
            RestoreLayout(window, window.Uid);
        }

        internal void RestoreLayout(Window window, string uid)
        {
            bool found = _windowIdToWindowSettingsMap.TryGetValue(uid, out WindowSetting windowSetting);

            if (found)
            {
                window.Width = windowSetting.Width;
                window.Height = windowSetting.Height;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                if (windowSetting.Left <= SystemParameters.VirtualScreenWidth - windowSetting.Width)
                {
                    window.Left = windowSetting.Left;
                }

                if (windowSetting.Top <= SystemParameters.VirtualScreenHeight - windowSetting.Height)
                {
                    window.Top = windowSetting.Top;
                }

                window.WindowState = windowSetting.WindowState;
            }
        }

        public bool IsVisible(System.Drawing.Rectangle newLoc)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.Bounds.IntersectsWith(newLoc))
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> IsPointTakenAsync(Window window)
        {
            int id = window.GetHashCode();
            string name = window.Name;
            double left = window.Left;
            double top = window.Top;

            if (top < 0)
            {
                return true;
            }

            if (name == null || !_windowNameToWindowsMap.TryGetValue(name, out var windows))
            {
                windows = _windows;
            }

            foreach (Window win in windows.ToList())
            {
                bool found = false;
                await win.Dispatcher.BeginInvoke(() =>
                {
                    found = id != win.GetHashCode()
                            && name == win.Name
                            && left == win.Left
                            && top == win.Top;
                });
                if (found)
                {
                    return true;
                }
                else
                {
                    continue;
                }
            }
            return false;
        }

        public async Task<Tuple<bool, System.Drawing.Rectangle>> IsPointTakenAsync(int id, string name, System.Drawing.Rectangle newRect)
        {
            System.Drawing.Rectangle match = newRect;

            if (name == null || !_windowNameToWindowsMap.TryGetValue(name, out var windows))
            {
                windows = _windows;
            }

            foreach (Window otherWindow in windows.ToList())
            {
                bool found = false;
                System.Drawing.Rectangle otherRect = newRect;
                await otherWindow.Dispatcher.BeginInvoke(() =>
                {
                    System.Drawing.Size otherSize = new((int)otherWindow.Width, (int)otherWindow.Height);
                    System.Drawing.Point otherLocation = new((int)otherWindow.Left, (int)otherWindow.Top);
                    otherRect = new(otherLocation, otherSize);
                    found = id != otherWindow.GetHashCode()
                            && name == otherWindow.Name
                            && otherRect.IntersectsWith(newRect);
                });
                if (found)
                {
                    match = otherRect;
                    return Tuple.Create(true, match);
                }
                else
                {
                    continue;
                }
            }
            return Tuple.Create(false, match);
        }

        private string GetLayoutExportFilePath()
        {
            return Path.Combine(OmsCore.Config.GetWorkspaceDirectory(), MODULE_LAYOUT_FILE);
        }

        private string GetOpenWindowsExportFilePath()
        {
            return Path.Combine(OmsCore.Config.GetWorkspaceDirectory(), STARTUP_MODULES_FILE);
        }

        internal void CloseAll<T>()
        {
            foreach (Window window in _windows.ToList().Where(x => x.GetType() == typeof(T)))
            {
                try
                {
                    window.Dispatcher.Invoke(() =>
                    {
                        window.Close();
                    });
                }
                catch (Exception ex)
                {
                    _log.Error(ex, nameof(CloseAll));
                }
            }
        }

        internal List<Window> GetAll<T>()
        {
            return _windows.ToList().Where(x => x.GetType() == typeof(T)).ToList();
        }

        internal async Task GetScreenshotsForTypeAsync<T>()
        {
            await Task.CompletedTask;
        }
    }
}
