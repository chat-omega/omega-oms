using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ZeroPlus.Oms.Ui.ModuleConfigs;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class PnlReportViewModel : ViewModelBase
    {
        private static readonly string MODULE_TITLE = "PnL Report";
        private const string DEFAULT_FORMAT = "HTML";
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();


        public string Uid { get; internal set; }
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();
        public Dispatcher Dispatcher { get; set; }
        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable]
        public partial bool IsBusy { get; set; }

        [Bindable]
        public partial DateTime StartDateTime { get; set; }

        [Bindable]
        public partial DateTime EndDateTime { get; set; }

        [Bindable]
        public partial string ApiUsernames { get; set; }

        [Bindable]
        public partial string Tags { get; set; }

        [Bindable]
        public partial string Symbols { get; set; }

        [Bindable]
        public partial string Underlyings { get; set; }

        [Bindable]
        public partial string Format { get; set; }

        public PnlReportViewModel()
        {
            ModuleTitle = MODULE_TITLE;
            OmsCore.SaveWorkspaceRequestEvent += SaveViewModelConfig;

            StartDateTime = DateTime.Today;
            EndDateTime = DateTime.Today + TimeSpan.FromDays(1);
            Format = DEFAULT_FORMAT;

            _ = LoadViewModelConfigAsync();
        }

        public void SetDispatcher(Dispatcher dispatcher)
        {
            Dispatcher = dispatcher;
        }

        [Command]
        public async Task Request()
        {
            IsBusy = true;

            string format = string.IsNullOrEmpty(Format) ? DEFAULT_FORMAT : Format;
            List<string> usernames = ParseSeparatedString(ApiUsernames);
            List<string> tags = ParseSeparatedString(Tags);
            List<string> symbols = ParseSeparatedString(Symbols);
            List<string> underlyings = ParseSeparatedString(Underlyings);

            string report = await OmsCore.GatewayClient.RequestPnlReportAsync(format, StartDateTime, EndDateTime, usernames, tags, symbols, underlyings);
            if (report == null)
            {
                await Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService?.ShowMessage($"PnL Report Request Failed.", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
            else if (format == "HTML")
            {
                string path = Path.Combine(Path.GetTempPath(), "PnL_Report_ZeroPlus_Derivatives.html");
                File.WriteAllText(path, report);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }

            IsBusy = false;
        }

        internal void Dispose()
        {
            OmsCore.SaveWorkspaceRequestEvent -= SaveViewModelConfig;
        }

        private void SaveViewModelConfig()
        {
            try
            {
                PnLReportViewModelConfig config = new()
                {
                    StartDateTime = StartDateTime,
                    EndDateTime = EndDateTime,
                    ApiUsernames = ApiUsernames,
                    Tags = Tags,
                    Symbols = Symbols,
                    Underlyings = Underlyings,
                    Format = Format,
                };

                string configJson = JsonConvert.SerializeObject(config, Formatting.Indented);

                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{Uid}-{nameof(PnLReportViewModelConfig)}.json");
                File.WriteAllText(configExportPath, configJson);
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveViewModelConfig));
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        internal async Task LoadViewModelConfigAsync(string uid = "Default")
        {
            try
            {
                string layoutDir = OmsCore.Config.GetWorkspaceDirectory();
                string configExportPath = Path.Combine(layoutDir, $"{uid}-{nameof(PnLReportViewModelConfig)}.json");

                if (File.Exists(configExportPath))
                {
                    string myFileStream = File.ReadAllText(configExportPath);
                    PnLReportViewModelConfig config = await Task.Run(() => JsonConvert.DeserializeObject<PnLReportViewModelConfig>(myFileStream));
                    StartDateTime = config.StartDateTime;
                    EndDateTime = config.EndDateTime;
                    ApiUsernames = config.ApiUsernames;
                    Tags = config.Tags;
                    Symbols = config.Symbols;
                    Underlyings = config.Underlyings;
                    Format = config.Format;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadViewModelConfigAsync));
                _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    MessageBoxService?.ShowMessage($"Something went wrong.\n{ex.Message}", "ZeroPlus OMS", MessageButton.OK, MessageIcon.Error)
                ));
            }
        }

        private List<string> ParseSeparatedString(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return new List<string>();
            }
            else
            {
                return input.Replace(",", ";")
                            .Split(';')
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Select(x => x.Trim().ToUpper())
                            .ToList();
            }
        }
    }
}
