using System;
using DevExpress.Mvvm.DataAnnotations;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.Modules;
using ZeroPlus.Oms.Ui.ViewModels.Interfaces;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.ViewModels;

public partial class BasketGroupViewModel : ModuleViewModelBase
{
    private readonly IModuleFactory _moduleFactory;
    private readonly ConcurrentDictionary<BasketTraderViewModel, BasketDragItem> _added = new();
    public override Module Module { get; protected set; } = Module.BasketGroup;
    public static ConcurrentDictionary<string, BasketDragItem> BasketIdToBasketDragMap { get; } = new();
    public static string LastConfig { get; set; }

    [Bindable]
    public partial ObservableCollection<BasketTraderViewModel> Baskets { get; set; }

    [Bindable]
    public partial BasketTraderViewModel SelectedBasket { get; set; }

    public BasketGroupViewModel(IModuleFactory moduleFactory, ConfigBrowserViewModel configBrowserViewModel, OmsCore omsCore) : base(configBrowserViewModel, omsCore)
    {
        _moduleFactory = moduleFactory;
        Baskets = new();
    }

    public override string GetConfigSerialized(bool withContent = false, bool layoutOnly = false)
    {
        List<string> configs = new();

        foreach (var basketDragItem in _added.Values)
        {
            basketDragItem.Dispatcher?.Invoke(() =>
            {
                var config = basketDragItem.Window.GetConfigAsJson(false, true);
                configs.Add(config);
            });
        }

        return JsonConvert.SerializeObject(configs);
    }

    public override async Task DeserializeAndLoadConfig(string configJson, bool withContent = true)
    {
        if (!string.IsNullOrWhiteSpace(configJson))
        {
            var configs = await Task.Run(() => JsonConvert.DeserializeObject<List<string>>(configJson));
            foreach (var config in configs)
            {
                if (_moduleFactory.CreateModule(Module.BasketTrader) is BasketTraderView { ViewModel: BasketTraderViewModel viewModel } view)
                {
                    if (viewModel.IsReady)
                    {
                        Task.Run(() => OnReady(viewModel));
                    }
                    else
                    {
                        viewModel.Ready += OnReady;
                    }

                    async void OnReady(IModuleViewModel module)
                    {
                        viewModel.Ready -= OnReady;
                        await view.LoadConfigFromJsonAsync(config, false, withContent);
                        BasketDragItem basketDragItem = new()
                        {
                            Dispatcher = view.Dispatcher,
                            ConfigAsJson = config,
                            ViewModel = viewModel,
                            Window = view
                        };
                        AttachBasket(basketDragItem);
                    }
                }
            }
        }
    }

    [Command]
    public void AttachBasket(BasketDragItem basketDragItem)
    {
        LastConfig = basketDragItem.ConfigAsJson;
        var basketTraderViewModel = basketDragItem.ViewModel;
        if (_added.TryAdd(basketTraderViewModel, basketDragItem))
        {
            Dispatcher?.BeginInvoke(() =>
            {
                Baskets.Add(basketTraderViewModel);
                SelectedBasket = basketTraderViewModel;
            });

            basketDragItem.Dispatcher?.BeginInvoke(() => basketDragItem.Window?.Hide());
        }
    }

    [Command]
    public void DetachCommand(BasketTraderViewModel basketTraderViewModel)
    {
        try
        {
            if (_added.TryRemove(basketTraderViewModel, out var basketDragItem))
            {
                basketDragItem.Dispatcher?.BeginInvoke(() =>
                {
                    basketDragItem.Window.Show();
                    basketDragItem.Window.Activate();
                });

                Dispatcher?.BeginInvoke(() => Baskets.Remove(basketTraderViewModel));
            }
        }
        catch (Exception ex)
        {
            _log.Error(ex, nameof(DetachCommand));
        }
    }

    [Command]
    public async void CloneBasketCommand(BasketTraderViewModel basketTraderViewModel)
    {
        if (BasketIdToBasketDragMap.TryGetValue(basketTraderViewModel.Uid, out var model))
        {
            string config = null;
            await model.Dispatcher.BeginInvoke(() => config = model.Window.GetConfigAsJson());
            var cloneBasket = basketTraderViewModel.Clone(config);
            BasketDragItem basketDragItem = null;
            string uid = null;
            await cloneBasket.View.Dispatcher.BeginInvoke(() =>
            {
                uid = cloneBasket.ViewModel.Uid;
                basketDragItem = new BasketDragItem
                {
                    Dispatcher = cloneBasket.View.Dispatcher,
                    ConfigAsJson = cloneBasket.View.GetConfigAsJson(),
                    Window = cloneBasket.View,
                    ViewModel = cloneBasket.ViewModel,
                };
            });
            BasketIdToBasketDragMap[uid] = basketDragItem;
            AttachBasket(basketDragItem);
        }
    }

    public override void OnDispose()
    {
        base.OnDispose();
        foreach (var instance in _added.Keys)
        {
            DetachCommand(instance);
        }
    }
}