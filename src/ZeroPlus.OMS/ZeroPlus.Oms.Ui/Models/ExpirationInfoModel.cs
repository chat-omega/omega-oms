using DevExpress.Mvvm;
using System;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class ExpirationInfoModel : BindableBase
    {
        public string Info => Expiration.ToString("MMM dd yy") + " (" + (int)Math.Floor((Expiration.Date - DateTime.Today).TotalDays) + ")";

        [Bindable]
        public partial DateTime Expiration { get; set; }

        [Bindable]
        public partial string RootSymbol { get; set; }

        [Bindable]
        public partial string Settlement { get; set; }

        public ExpirationInfoModel()
        {
        }

        public ExpirationInfoModel(ExpirationInfoModel expirationInfo)
        {
            Expiration = expirationInfo.Expiration;
            RootSymbol = expirationInfo.RootSymbol;
        }

        public ExpirationInfoModel(DateTime expiration, string rootSymbol)
        {
            Expiration = expiration;
            RootSymbol = rootSymbol;
        }

        public ExpirationInfoModel Clone()
        {
            return new ExpirationInfoModel(this);
        }

        public override bool Equals(object obj)
        {
            return obj is ExpirationInfoModel expiryInfo && (ReferenceEquals(obj, this) ||
                                                            (expiryInfo.Expiration.Date == Expiration.Date &&
                                                             expiryInfo.RootSymbol == RootSymbol &&
                                                             expiryInfo.Settlement == Settlement));
        }

        public override string ToString()
        {
            return "Exp:" + Expiration.ToString("yyyyMMdd:hh:mm") + ";Root:" + RootSymbol + ";Set:" + Settlement;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }
}
