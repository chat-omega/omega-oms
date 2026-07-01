using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public class OpenCloseCounterModel : BindableBase
    {
        private int _open;
        private int _close;
        private bool _isFlat;

        public int Open
        {
            get { return _open; }
            set { SetValue(ref _open, value); }
        }
        public int Close
        {
            get { return _close; }
            set { SetValue(ref _close, value); }
        }
        public bool IsFlat
        {
            get { return _isFlat; }
            set { SetValue(ref _isFlat, value); }
        }

        public OpenCloseCounterModel(int open, int close)
        {
            Open = open;
            Close = close;
            IsFlat = Open == Close;
        }

        internal void AddToOpen(int filled)
        {
            Open += filled;
            IsFlat = Open == Close;
        }

        internal void AddToClose(int filled)
        {
            Close += filled;
            IsFlat = Open == Close;
        }
    }
}
