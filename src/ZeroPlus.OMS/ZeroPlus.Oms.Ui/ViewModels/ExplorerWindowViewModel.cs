using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Oms.Ui.Collections;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ModuleConfigs;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class ExplorerWindowViewModel(ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : ModuleViewModelBase(configBrowserViewModel, omsCore)
{


    public override Module Module { get; protected set; } = Module.ExplorerWindow;
    public FastObservableCollection<ExplorerRowModel> Updates { get; } = [];
    public IDocumentManagerService OptionsVisualizerDocumentManagerService => GetService<IDocumentManagerService>("OptionsVisualizerWindowService");

    [Bindable]
    public partial ExplorerRowModel SelectedItem { get; set; }


    [Command(CanExecuteMethodName = nameof(CanOpenOptionsVisualizerCommand))]
    public void OpenOptionsVisualizerCommand(ExplorerRowModel model)
    {
        try
        {
            var viewModel = new Volatility.OptionsVisualizerViewModel(OmsCore);

            var targetModel = model ?? SelectedItem;
            if (targetModel == null) return;

            // Note: OptionsVisualizer currently expects QuotesAndGreeksModel or SpreadQuotesAndGreeksModel.
            // We might need to adapt it, but for now we'll pass the symbol and let it load.
            // If it strictly needs the model, we'd need a way to get one from the symbol.
            string sym = targetModel.Symbol;
            try
            {
                viewModel.LoadStrategy(sym);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to load strategy for symbol '{sym}': {ex.Message}", ex);
            }

            // Capture the specific row instance to avoid duplicate issues
            ExplorerRowModel activeRow = targetModel;

            // Synchronization Listener
            viewModel.SpreadUpdated += (originalSym, newSym) =>
            {
                // Robust check: Ensure activeRow is still valid and attached
                int index = Updates.IndexOf(activeRow);
                if (index == -1) return;

                // Unsubscribe from old
                activeRow.Unsubscribe();

                // Simply update the row in-place. The unified model handles all symbol types.
                activeRow.Initialize(newSym);
                activeRow.Subscribe();
            };


            IDocument document = OptionsVisualizerDocumentManagerService.CreateDocument("OptionsVisualizerView", viewModel);
            document.Title = "Options Strategy Visualizer";
            document.DestroyOnClose = true;
            document.Show();
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(OpenOptionsVisualizerCommand));
        }
    }

    public bool CanOpenOptionsVisualizerCommand(ExplorerRowModel model) => model != null || SelectedItem != null;

    [Command]
    public void AddCommand()
    {
        try
        {
            LoadSymbolView view = new();
            view.ShowDialog();
            if (view.DataContext is LoadSymbolViewModel { IsValid: true } viewModel)
            {
                try
                {
                    // Strict Validation: Must be a Spread (Legs > 0) OR a valid Option
                    bool isValidStrategy = false;
                    SymbolLib.SymbolCodec codec = new SymbolLib.SymbolCodec(viewModel.Symbol);
                    if (codec.LegCount > 0)
                    {
                        isValidStrategy = true;
                    }
                    else
                    {
                        SymbolLib.Instrument instrument = new SymbolLib.Instrument(viewModel.Symbol);
                        // Check if it's a valid Option (has strike and expiration)
                        if (instrument.valid && instrument.strike > 0 && instrument.expiration > DateTime.MinValue)
                        {
                            isValidStrategy = true;
                        }
                    }

                    if (!isValidStrategy)
                    {
                        MessageBoxService?.ShowMessage("The symbol must be a valid Option or Option Strategy.", "Invalid Symbol", MessageButton.OK, MessageIcon.Warning);
                        return;
                    }

                    ExplorerRowModel model = new(OmsCore, Dispatcher);
                    model.Initialize(viewModel.Symbol);
                    model.Subscribe(); // Subscribe on addition
                    Updates.Add(model);
                }
                catch (Exception ex)
                {
                    throw new ArgumentException($"Failed to initialize symbol '{viewModel.Symbol}': {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(AddCommand));
        }
    }

    [Command]
    public void RemoveCommand(ExplorerRowModel model)
    {
        try
        {
            model.Unsubscribe();
            Updates.Remove(model);
        }
        catch (Exception ex)
        {
            HandleException(ex, nameof(RemoveCommand));
        }
    }

    public override void OnDispose()
    {
        foreach (var model in Updates)
        {
            model.Unsubscribe();
        }
    }

    private void HandleException(Exception ex, string caller)
    {
        _log.Error(ex, caller);
        if (ex is ArgumentException)
        {
            MessageBoxService?.ShowMessage(ex.Message, "Error", MessageButton.OK, MessageIcon.Error);
        }
    }

    public void LoadSymbols(List<string> spreads)
    {
        var models = new List<ExplorerRowModel>();

        foreach (var symbol in spreads)
        {
            if (symbol != null)
            {
                ExplorerRowModel model = new(OmsCore, Dispatcher);
                model.Initialize(symbol);
                model.Subscribe(); // Always subscribe for Explorer
                models.Add(model);
            }
        }

        if (models.Count > 0)
        {
            Dispatcher.BeginInvoke(() => Updates.AddRange(models));
        }
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        ExplorerViewModelConfig config = new()
        {
            LoadedSymbols = [.. Updates.Select(d => d.Symbol).Distinct()],
        };
        return JsonConvert.SerializeObject(config, Formatting.Indented); ;
    }

    public override Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        ExplorerViewModelConfig config = JsonConvert.DeserializeObject<ExplorerViewModelConfig>(configJson);
        if (config != null)
        {
            LoadSymbols(config.LoadedSymbols);
        }

        return Task.CompletedTask;
    }
}
