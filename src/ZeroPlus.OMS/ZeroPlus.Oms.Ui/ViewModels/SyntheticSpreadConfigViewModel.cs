using DevExpress.Mvvm;
using DevExpress.Mvvm.DataAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ZeroPlus.Models.Data.Enums.Matrix;
using ZeroPlus.Oms.Ui.Models;

namespace ZeroPlus.Oms.Ui.ViewModels;

public class SyntheticSpreadConfigViewModel : DynamicConfigEditorBase
{

    public override Module Module => Module.MatrixSmartConfig;
    protected ICurrentWindowService CurrentWindowService => GetService<ICurrentWindowService>();
    public List<MakeTake?> MakeTakeOptions { get; }
    public List<Algorithm?> Algorithms { get; }

    public SyntheticSpreadConfigDataModel SyntheticSpreadConfig { get; }
    public ScrapeConfigDataModel ScrapeConfig { get; }
    public SeekerConfigDataModel SeekerConfig { get; }

    public SyntheticSpreadConfigViewModel(OmsCore omsCore) : base(omsCore)
    {
        MakeTakeOptions = Enum.GetValues<MakeTake>().Cast<MakeTake?>().ToList();
        MakeTakeOptions.Insert(0, null);
        Algorithms = Enum.GetValues<Algorithm>().Cast<Algorithm?>().ToList();
        Algorithms.Insert(0, null);
        SyntheticSpreadConfig = new();
        ScrapeConfig = new();
        SeekerConfig = new();
    }

    public void SetModel(MatrixStrategyConfigModel model)
    {
        Model = model;
        if (model != null)
        {
            Title = model.Title;
            SyntheticSpreadConfig.UpdateModel(model.SyntheticSpreadStrategyData);
            ScrapeConfig.UpdateModel(model.ScrapeStrategyData);
            SeekerConfig.UpdateModel(model.SeekerStrategyData);
        }
    }

    [Command]
    public async Task SaveConfigCommand()
    {
        if (Model is MatrixStrategyConfigModel model)
        {
            model.Title = Title;

            SyntheticSpreadConfig.LoadModel(model.SyntheticSpreadStrategyData);
            ScrapeConfig.LoadModel(model.ScrapeStrategyData);
            SeekerConfig.LoadModel(model.SeekerStrategyData);
            SeekerConfig.LoadModel(model.SeekerSpreadStrategyData);

            await Save(model.GetAsJson());
        }
        CurrentWindowService?.Close();
    }

    [Command]
    public void AddExchangeCommand()
    {
        SyntheticSpreadConfig.Exchanges.Add(new ExchangeModel());
    }

    [Command]
    public void AddExchangeTakeCommand()
    {
        SyntheticSpreadConfig.ExchangesTake.Add(new ExchangeModel());
    }

    [Command]
    public void RemoveExchangeCommand(ExchangeModel model)
    {
        SyntheticSpreadConfig.Exchanges.Remove(model);
    }

    [Command]
    public void RemoveExchangeTakeCommand(ExchangeModel model)
    {
        SyntheticSpreadConfig.ExchangesTake.Remove(model);
    }

    [Command]
    public void AddScrapeExchangeCommand()
    {
        ScrapeConfig.Exchanges.Add(new ExchangeModel());
    }

    [Command]
    public void AddScrapeExchangeTakeCommand()
    {
        ScrapeConfig.ExchangesTake.Add(new ExchangeModel());
    }

    [Command]
    public void RemoveScrapeExchangeCommand(ExchangeModel model)
    {
        ScrapeConfig.Exchanges.Remove(model);
    }

    [Command]
    public void RemoveScrapeExchangeTakeCommand(ExchangeModel model)
    {
        ScrapeConfig.ExchangesTake.Remove(model);
    }

    [Command]
    public void AddSeekerExchangeCommand()
    {
        SeekerConfig.Exchanges.Add(new ExchangeModel());
    }

    [Command]
    public void AddSeekerExchangeTakeCommand()
    {
        SeekerConfig.ExchangesTake.Add(new ExchangeModel());
    }

    [Command]
    public void RemoveSeekerExchangeCommand(ExchangeModel model)
    {
        SeekerConfig.Exchanges.Remove(model);
    }

    [Command]
    public void RemoveSeekerExchangeTakeCommand(ExchangeModel model)
    {
        SeekerConfig.ExchangesTake.Remove(model);
    }
}