using Newtonsoft.Json;
using System.Windows;

namespace ZeroPlus.Oms.Ui.Helper
{
    internal class WindowSetting
    {
        private WindowState _windowState;
        public string GUID { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Top { get; set; }
        public double Left { get; set; }

        public WindowState WindowState
        {
            get => _windowState == WindowState.Minimized ? WindowState.Normal : _windowState;
            set => _windowState = value;
        }

        public WindowSetting()
        {

        }

        public WindowSetting(Window window, bool ignoreLocation = false)
        {
            GUID = window.Uid;
            Width = window.Width;
            Height = window.Height;
            WindowState = window.WindowState;
            if (ignoreLocation)
            {
                Top = -1;
                Left = -1;
            }
            else
            {
                Top = window.Top;
                Left = window.Left;
            }
        }

        public string SerializeToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static WindowSetting DeserializeFromJson(string json)
        {
            return JsonConvert.DeserializeObject<WindowSetting>(json);
        }
    }
}