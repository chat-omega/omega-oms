using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;

namespace ZeroPlus.Oms.Ui.Views
{
    public partial class AdminControlsView
    {
        private const Module MODULE = Module.AdminControls;

        public AdminControlsView(IModuleFactory moduleFactory, string uid = null) : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            WindowSetting windowSetting = new(this, isDefault);
            Dictionary<string, string> configDictionary = new()
            {
                [nameof(WindowSetting)] = windowSetting.SerializeToJson(),
            };
            return JsonConvert.SerializeObject(configDictionary);
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = true)
        {
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return;
            }

            Dictionary<string, string> configDictionary = await Task.Run(() =>
                JsonConvert.DeserializeObject<Dictionary<string, string>>(configJson));

            if (configDictionary.TryGetValue(nameof(WindowSetting), out string windowSettingExport))
            {
                LoadWindowSettingsFromJson(windowSettingExport, offset);
            }
        }

        public override void ClearFiltersClick()
        {
        }

        public override void ClearSortingClick()
        {
        }
    }
}
