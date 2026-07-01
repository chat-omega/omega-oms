using DevExpress.Mvvm;

namespace ZeroPlus.Oms.Ui.Models
{
    public class AutomationConfigMap : BindableBase
    {
        private string _underlyings;
        private double _increment;
        private AutomationConfigModel _automationConfig;

        public string Underlyings { get => _underlyings; set => SetValue(ref _underlyings, value); }

        public double Increment { get => _increment; set => SetValue(ref _increment, value); }

        public AutomationConfigModel AutomationConfig { get => _automationConfig; set => SetValue(ref _automationConfig, value); }
    }
}
