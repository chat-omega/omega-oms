using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Concurrent;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public delegate void SaveHandler(ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> config);
    public partial class AutomationConfigMappingViewModel : CustomizableTableViewModelBase
    {

        protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

        [Bindable]
        public partial SaveHandler ApplyConfigHandler { get; set; }
        [Bindable]
        public partial string ModuleTitle { get; set; }
        [Bindable]
        public partial FastObservableCollection<AutomationConfigMap> Configs { get; set; }
        [Bindable]
        public partial FastObservableCollection<AutomationConfigModel> AutomationConfigs { get; set; }

        public AutomationConfigMappingViewModel()
        {
            Configs = new FastObservableCollection<AutomationConfigMap>();
            AutomationConfigs = new FastObservableCollection<AutomationConfigModel>();
        }

        [Command]
        public void SaveCommand()
        {
            if (ApplyConfigHandler != null)
            {
                ConcurrentDictionary<Tuple<string, double>, AutomationConfigModel> dictionary = new();
                foreach (AutomationConfigMap config in Configs)
                {
                    if (!string.IsNullOrWhiteSpace(config.Underlyings) && config.AutomationConfig != null)
                    {
                        string[] underlyings = config.Underlyings.Split(",");
                        double increment = Math.Round(config.Increment, 2);
                        foreach (string underlying in underlyings)
                        {
                            if (!string.IsNullOrWhiteSpace(underlying))
                            {
                                dictionary[Tuple.Create(underlying.Replace(".", "").Trim().ToUpper(), increment)] = config.AutomationConfig;
                            }
                        }
                    }
                }
                ApplyConfigHandler.Invoke(dictionary);
            }
            CloseCommand();
        }

        [Command]
        public void AddCommand()
        {
            AutomationConfigMap automationConfigMap = new();
            Configs.Add(automationConfigMap);
        }

        [Command]
        public void RemoveCommand(AutomationConfigMap config)
        {
            Configs.Remove(config);
        }

        [Command]
        public void CloseCommand()
        {
            CurrentWindowService?.Close();
        }
    }
}
