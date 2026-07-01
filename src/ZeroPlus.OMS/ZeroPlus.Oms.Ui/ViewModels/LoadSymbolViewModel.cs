using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using SymbolLib;
using ZeroPlus.Oms.Clients;
using ZeroPlus.Oms.Data.Securities;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class LoadSymbolViewModel(OmsCore omsCore) : ViewModelBase
{
    protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();

    private readonly OmsCore _omsCore = omsCore;

    [Bindable]
    public partial string Symbol { get; set; }
    [Bindable]
    public partial int LegsCount { get; set; }
    [Bindable]
    public partial string Underlying { get; set; }
    [Bindable]
    public partial bool IsValid { get; set; }

    [Command]
    public async System.Threading.Tasks.Task SearchCommand()
    {
        if (string.IsNullOrWhiteSpace(Symbol))
        {
            IsValid = false;
            return;
        }

        // 1. Parse Structure First
        SymbolCodec codec = new(Symbol);
        List<string> legsToCheck = [];
        string underlyingToCheck = null;

        if (codec.LegCount > 0)
        {
            LegsCount = codec.LegCount;
            underlyingToCheck = codec.UnderlyingSymbol();
            for (int i = 0; i < codec.LegCount; i++)
            {
                legsToCheck.Add(codec.GetLeg(i).symbol);
            }
        }
        else
        {
            Instrument instrument = new(Symbol);
            if (instrument.valid && instrument.strike > 0 && instrument.expiration > DateTime.MinValue)
            {
                LegsCount = 1;
                underlyingToCheck = instrument.underlyingSymbol;
                legsToCheck.Add(Symbol);
            }
            else
            {
                LegsCount = 0;
                IsValid = false;
                return;
            }
        }

        Underlying = underlyingToCheck;

        // 2. Validate against OMS Market Data
        try
        {
            IsValid = false; // Reset while searching

            if (string.IsNullOrEmpty(underlyingToCheck)) return;

            var chain = await _omsCore.QuoteClient.GetSymbolsAsync(underlyingToCheck);
            if (chain == null || chain.Count == 0)
            {
                return; // Underlying not found or no options
            }

            // Verify every leg exists in the chain
            bool allLegsFound = true;
            foreach (var legSymbol in legsToCheck)
            {
                Instrument legInst = new(legSymbol);
                bool found = chain.Any(o =>
                    Math.Abs(o.Strike - legInst.strike) < 0.001 &&
                    o.Expiration.Date == legInst.expiration.Date &&
                    ((o.Type == OptionType.CALL && !legInst.callPut) ||
                     (o.Type == OptionType.PUT && legInst.callPut))
                );

                if (!found)
                {
                    allLegsFound = false;
                    break;
                }
            }

            IsValid = allLegsFound;
        }
        catch (Exception)
        {
            IsValid = false;
        }
    }

    [Command]
    public void AddCommand()
    {
        if (!IsValid && !string.IsNullOrWhiteSpace(Symbol))
        {
            SearchCommand();
        }

        if (IsValid)
        {
            CurrentWindowService?.Close();
        }
    }

    [Command]
    public void CancelCommand()
    {
        IsValid = false;
        CurrentWindowService?.Close();
    }
}