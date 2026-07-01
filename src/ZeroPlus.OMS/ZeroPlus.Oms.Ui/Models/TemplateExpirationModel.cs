using DevExpress.Mvvm;
using System;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public class TemplateExpirationModel : BindableBase
    {
        public DateTime Expiration { get; set; }
        public bool IsExpired { get; set; }
        public bool IsRegular { get; set; }

        public TemplateExpirationModel(DateTime expiration)
        {
            Expiration = expiration;
            IsExpired = expiration < DateTime.Today;
            IsRegular = TimeHelper.IsThirdFridayOfTheMonth(expiration);
        }
    }
}
