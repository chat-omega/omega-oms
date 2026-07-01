using DevExpress.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using ZeroPlus.Models.Data.Securities;
using ZeroPlus.Models.Data.Securities.Interfaces;
using ZeroPlus.Oms.Helper;

namespace ZeroPlus.Oms.Ui.Models
{
    public partial class OptionChainModel : BindableBase
    {

        private string _Symbol;
        public string Symbol
        {
            get => _Symbol;
            set => SetValue(ref _Symbol, OptionsHelper.IsIndex(value) ? "$" + value : value);
        }

        [Bindable]
        public partial int Count { get; set; }

        private List<Option> _OptionChain;
        public List<Option> OptionChain
        {
            get => _OptionChain;
            set
            {
                SetValue(ref _OptionChain, value);
                Count = value.Count;
            }
        }

        public OptionChainModel(ISecurityBook securityBook, List<Oms.Data.Securities.Option> options)
        {
            List<Option> output = new();
            foreach (var option in options)
            {
                if (securityBook.GetSecurity(option.OptionSymbol) is Option converted)
                {
                    output.Add(converted);
                }
            }
            Symbol = output.FirstOrDefault()?.Underlying?.Symbol;
            OptionChain = output.Where(x => x.Expiration.Date >= DateTime.Today).ToList();
        }

        public OptionChainModel(string symbol, List<Option> options)
        {
            Symbol = symbol;
            OptionChain = options.Where(x => x.Expiration.Date >= DateTime.Today).ToList();
        }

        public OptionChainModel(List<Option> options)
        {
            if (options != null && options.Count != 0)
            {
                Symbol = options[0]?.Underlying?.Symbol;
                OptionChain = options.Where(x => x.Expiration.Date >= DateTime.Today).ToList();
            }
        }
    }
}
