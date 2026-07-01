using DevExpress.Mvvm;
using System;
using ZeroPlus.Comms.Models.Data.Oms.Config;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class BasketLayoutQuickAccessModel : BindableBase
    {
        public int _Index;
        public string _Title;
        public ConfigSave _Layout;

        public Tuple<int, string, ConfigSave> Export => Tuple.Create(Index, Title, Layout);

        [Bindable]
        public partial int Index { get; set; }
        [Bindable]
        public partial string Title { get; set; }
        public ConfigSave Layout
        {
            get => _Layout;
            set
            {
                SetValue(ref _Layout, value);
                if (string.IsNullOrWhiteSpace(Title) && value != null && !string.IsNullOrWhiteSpace(value.Title))
                {
                    Title = value.Title;
                }
            }
        }

        internal bool IsValid()
        {
            if (Index < 0)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(Title))
            {
                return false;
            }
            if (Layout == null)
            {
                return false;
            }

            return true;
        }
    }
}
