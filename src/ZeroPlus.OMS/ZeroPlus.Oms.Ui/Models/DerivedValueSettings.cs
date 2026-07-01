using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public class DerivedValueSettings : BindableBase
    {
        public string Symbol1 { get; }
        public string Symbol2 { get; }
        public double Multiplier { get; }


        private double _Ratio1;
        public double Ratio1
        {
            get => _Ratio1;
            set
            {
                SetValue(ref _Ratio1, value);
                double ratio2 = value / Multiplier;
                if (ratio2 != Ratio2)
                {
                    Ratio2 = ratio2;
                }
            }
        }


        private double _Ratio2;
        public double Ratio2
        {
            get => _Ratio2;
            set
            {
                SetValue(ref _Ratio2, value);
                double ratio1 = value * Multiplier;
                if (ratio1 != Ratio1)
                {
                    Ratio1 = ratio1;
                }
            }
        }

        public DerivedValueSettings(string symbol1, string symbol2, double multiplier)
        {
            Symbol1 = symbol1?.ToUpper();
            Symbol2 = symbol2?.ToUpper();
            Multiplier = multiplier;
        }

        public double GetRatio(string symbol)
        {
            if (symbol?.ToUpper() == Symbol1)
            {
                return Ratio1;
            }
            else if (symbol?.ToUpper() == Symbol2)
            {
                return Ratio2;
            }
            else
            {
                return double.NaN;
            }
        }

        public double GetMultiplier(string symbol)
        {
            if (symbol?.ToUpper() == Symbol1)
            {
                return 1 / Multiplier;
            }
            else if (symbol?.ToUpper() == Symbol2)
            {
                return Multiplier;
            }
            else
            {
                return double.NaN;
            }
        }

        public string GetDerivativeSymbol(string symbol)
        {
            if (symbol?.ToUpper() == Symbol1)
            {
                return Symbol2;
            }
            else if (symbol?.ToUpper() == Symbol2)
            {
                return Symbol1;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}