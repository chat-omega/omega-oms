using System;
using System.Threading.Tasks;
using System.Windows.Media;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels;


namespace ZeroPlus.Oms.Ui.Views
{
    /// <summary>
    /// Interaction logic for EmaChartView.xaml
    /// </summary>
    public partial class EmaChartView : ModuleWindow
    {
        private const Module MODULE = Module.EmaChart;

        private readonly Color _posBarColor = (Color)ColorConverter.ConvertFromString("#2eac2f")!;
        private readonly Color _negBarColor = (Color)ColorConverter.ConvertFromString("#ff3255")!;

        public EmaChartView(IModuleFactory moduleFactory, string uid = "") : base(MODULE, uid, moduleFactory)
        {
            InitializeComponent();
        }

        public override string GetConfigAsJson(bool isDefault = false, bool withContent = false)
        {
            EmaChartModuleConfig config = new()
            {
                WindowSetting = new WindowSetting(this, isDefault).SerializeToJson()
            };
            if (DataContext is EmaChartViewModel viewModel)
            {
                config.ViewModelConfig = viewModel.GetConfigSerialized();
            }
            return config.Serialize();
        }

        public override async Task LoadConfigFromJsonAsync(string configJson, bool offset = false, bool withContent = false)
        {
            EmaChartModuleConfig config = await ModuleConfigBase.DeserializeAsync<EmaChartModuleConfig>(configJson);
            if (config != null)
            {
                Dispatcher?.BeginInvoke(() =>
                {
                    LoadWindowSettingsFromJson(config.WindowSetting, offset: offset);
                    if (DataContext is EmaChartViewModel viewModel)
                    {
                        viewModel.LoadConfigFromJsonAsync(config.ViewModelConfig);
                    }
                });
            }
        }

        private void CustomDrawSeriesPoint(object sender, DevExpress.Xpf.Charts.CustomDrawSeriesPointEventArgs e)
        {
            if (e.Series.Name == "HistogramBarSeries")
            {
                e.DrawOptions.Color = e.SeriesPoint.Value > 0 ? _posBarColor : _negBarColor;
            }
        }
    }
}
