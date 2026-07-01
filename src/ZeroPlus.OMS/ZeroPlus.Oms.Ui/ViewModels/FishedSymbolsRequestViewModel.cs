using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using DevExpress.Spreadsheet;
using NLog;
using SymbolLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Responses;
using ZeroPlus.Oms.Ui.Helper;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    public partial class FishedSymbolsRequestViewModel : ViewModelBase
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public static OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        private List<SymbolFishStatusResponse> _response;

        protected Services.IOmsMessageBoxService MessageBoxService => GetService<Services.IOmsMessageBoxService>();
        protected ISaveFileDialogService SaveFileDialogService => GetService<ISaveFileDialogService>();
        public List<string> SupportedFormats { get; set; } = new List<string> { "Dominator List", "CSV" };

        [Bindable]
        public partial string ModuleTitle { get; set; }

        [Bindable(Default = "DOMINATOR SPREADS")]
        public partial string ExportFormat { get; set; }

        [Bindable]
        public partial string Status { get; set; }

        [Bindable]
        public partial bool CanExport { get; set; }

        [Bindable]
        public partial bool IncludeOutrights { get; set; }

        [Bindable]
        public partial bool IncludeSpreads { get; set; }

        [Bindable]
        public partial bool IncludeBackdays { get; set; }

        [Bindable]
        public partial DateTime LastDateToInclude { get; set; }

        [Bindable]
        public partial string Underlyings { get; set; }

        [Bindable]
        public partial string Symbols { get; set; }

        [Bindable]
        public partial string Tags { get; set; }

        [Bindable]
        public partial bool RandomizeExport { get; set; }

        [Command]
        public async Task LoadCommand()
        {
            Status = "Loading";
            await Task.Run(() =>
            {
                _response = OmsCore.HerculesClient.RequestSymbolFishStatus(IncludeOutrights, IncludeSpreads, IncludeBackdays, LastDateToInclude, Underlyings, Symbols, Tags);
                if (_response.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(Underlyings))
                    {
                        List<SymbolFishStatusResponse> results = new();
                        HashSet<string> underlyings = Underlyings.Split(',').Select(x => x.Trim().ToUpper()).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
                        foreach (SymbolFishStatusResponse response in results)
                        {
                            SymbolCodec symbol = new(response.Symbol);
                            if (underlyings.Contains(symbol.UnderlyingSymbol()))
                            {
                                results.Add(response);
                            }
                        }
                        _response = results;
                    }
                }
            });
            if (_response != null)
            {
                Status = "Loaded " + _response.Count + " Symbols.";
                CanExport = _response.Count > 0;
            }
            else
            {
                Status = "Load Failed!";
            }
        }

        [Command]
        public void CancelCommand()
        {
            CanExport = false;
            Status = "Stopped!";
        }

        [Command]
        public void ExportCommand()
        {
            try
            {
                ExportSpreadsToFileView exportToFileView = new()
                {
                    DataContext = this
                };
                exportToFileView.ShowDialog();
            }
            catch (Exception) { }
        }

        [Command]
        public async Task WriteToFile()
        {
            try
            {
                string titleString = "Fished Symbols Export";
                if (titleString.Length > 230)
                {
                    titleString = titleString.Substring(0, 230);
                }
                bool save = false;
                switch (ExportFormat.ToUpper())
                {
                    case "DOMINATOR LIST":
                        titleString = "DOMINATOR SPREADS " + titleString;
                        if (titleString.Length > 230)
                        {
                            titleString = titleString.Substring(0, 230);
                        }
                        SaveFileDialogService.DefaultExt = "xlsx";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {_response.Count} spreads";
                        SaveFileDialogService.Filter = "Dominator List|*.XLSX";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string filePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => ExportHelper.WriteSpreadsToFileUsingDominatorFormat(OmsCore.User.Username, filePath, _response, RandomizeExport));
                            }
                        }
                        break;
                    case "CSV":
                        SaveFileDialogService.DefaultExt = "csv";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {_response.Count} spreads";
                        SaveFileDialogService.Filter = "Comma Separated Values|*.CSV";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string filePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => WriteSpreadsToFile(filePath));
                            }
                        }
                        break;
                    case "INDIVIDUAL CSV":
                        SaveFileDialogService.DefaultExt = "csv";
                        SaveFileDialogService.DefaultFileName = $"{titleString} - {DateTime.Now:MM-dd-yyyy hh.mm} - {_response.Count} spreads";
                        SaveFileDialogService.Filter = "Comma Separated Values|*.CSV";
                        save = SaveFileDialogService.ShowDialog();
                        if (save)
                        {
                            string filePath = SaveFileDialogService.GetFullFileName();
                            if (CanExport)
                            {
                                await Task.Run(() => WriteSpreadsToFile(filePath));
                            }
                        }
                        break;
                }
            }
            catch (Exception)
            {
            }
        }

        private void WriteSpreadsToFile(string filePath)
        {
            List<string> spreads = _response.Select(x => x.Symbol).ToList();
            if (RandomizeExport)
            {
                ListHelper.Shuffle(spreads);
            }
            FileStream file = new(filePath, FileMode.Create);
            StreamWriter streamWriter = new(file, Encoding.Default);

            foreach (string spread in spreads)
            {
                streamWriter.WriteLine(spread);
            }

            streamWriter.Close();
            file.Close();
        }
    }
}
