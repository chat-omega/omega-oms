using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class ExpirationModel : ViewModelBase
    {
        private readonly Action<DateTime> _minExpirationChangedHandler;
        private readonly Action<DateTime> _maxExpirationChangedHandler;


        [Bindable]
        public partial DateTime Date { get; set; }

        [Bindable]
        public partial bool IsChecked { get; set; }

        public ExpirationModel(DateTime date, Action<DateTime> minExpirationChangedHandler, Action<DateTime> maxExpirationChangedHandler)
        {
            _minExpirationChangedHandler = minExpirationChangedHandler;
            _maxExpirationChangedHandler = maxExpirationChangedHandler;
            IsChecked = true;
            Date = date;
        }

        public ExpirationModel(DateTime date)
        {
            IsChecked = true;
            Date = date;
        }

        [Command]
        public void MinExpirationChangedCommand()
        {
            _minExpirationChangedHandler?.Invoke(Date);
        }

        [Command]
        public void MaxExpirationChangedCommand()
        {
            _maxExpirationChangedHandler?.Invoke(Date);
        }

        public override bool Equals(object obj)
        {
            if (obj is ExpirationModel expirationModel)
            {
                return expirationModel.Date == Date;
            }
            else
            {
                return base.Equals(obj);
            }
        }

        public override int GetHashCode()
        {
            return Date.GetHashCode();
        }
    }
}
