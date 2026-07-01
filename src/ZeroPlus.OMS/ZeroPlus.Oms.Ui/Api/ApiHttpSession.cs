using NetCoreServer;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using ZeroPlus.Oms.Ui.Models;
using ZeroPlus.Oms.Ui.ViewModels;
using ZeroPlus.Oms.Ui.Views;

namespace ZeroPlus.Oms.Ui.Api
{
    internal class ApiHttpSession : HttpSession
    {
        private enum RequestMode
        {
            None,
            Add,
            Delete,
            Load,
        }

        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        public OmsCore OmsCore => ServiceLocator.GetService<OmsCore>();

        public ApiHttpSession(HttpServer server) : base(server) { }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            switch (request.Method)
            {
                case "GET":
                    HandleGetRequest(request);
                    break;
                case "POST":
                    HandlePostRequest(request);
                    break;
                default:
                    break;
            }
        }

        private void HandleGetRequest(HttpRequest request)
        {
            try
            {
                string[] split = Uri.UnescapeDataString(request.Url).Split('?');
                string key = split[0].Replace("/oms/", "");
                string parameters = "";
                if (split.Length > 1)
                {
                    parameters = split[1];
                }

                if (Enum.TryParse(key, ignoreCase: true, out Module module))
                {
                    switch (module)
                    {
                        case Module.BasketTrader:
                            HandleBasketOrderUpdateRequest(parameters);
                            return;
                        case Module.SpreadsGenerator:
                            HandleSpreadsGeneratorRequest(parameters);
                            return;
                    }
                }
                SendResponseAsync(Response.MakeErrorResponse(404));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(HandleGetRequest));
            }
        }

        private void HandlePostRequest(HttpRequest request)
        {
            try
            {
                string value = request.Body;
                string[] split = Uri.UnescapeDataString(request.Url).Split('?');
                string key = split[0].Replace("/oms/", "");
                string parameters = "";
                if (split.Length > 1)
                {
                    parameters = split[1];
                }

                if (Enum.TryParse(key, ignoreCase: true, out Module module))
                {
                    switch (module)
                    {
                        case Module.BasketTrader:
                            HandleBasketUpdate(parameters, value);
                            return;
                        case Module.SpreadsGenerator:
                            HandleSpreadsGeneratorUpdate(parameters, value);
                            return;
                    }
                }
                SendResponseAsync(Response.MakeErrorResponse(404));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(HandleGetRequest));
            }
        }

        private void HandleBasketOrderUpdateRequest(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                parameters = "";
            }
            string[] splits = parameters.Split("&");
            string id = "";
            string type = "";

            foreach (string pair in splits)
            {
                string[] pSplit = pair.Split("=");
                if (pSplit.Length > 1)
                {
                    string key = pSplit[0];
                    string value = pSplit[1];
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        id = value;
                    }
                    if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    {
                        type = value;
                    }
                }
            }
            switch (type.ToLower())
            {
                case "settings":
                    GetBasketConfigs(id, all: string.IsNullOrWhiteSpace(id));
                    break;
                case "orders":
                    HandleBasketOrderUpdateRequest(RequestMode.None, id, string.Empty, all: string.IsNullOrWhiteSpace(id));
                    break;
                default:
                    SendResponseAsync(Response.MakeErrorResponse(404));
                    break;
            }
        }

        private void HandleSpreadsGeneratorRequest(string parameters)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                parameters = "";
            }
            string[] splits = parameters.Split("&");
            string id = "";
            string type = "";

            foreach (string pair in splits)
            {
                string[] pSplit = pair.Split("=");
                if (pSplit.Length > 1)
                {
                    string key = pSplit[0];
                    string value = pSplit[1];
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        id = value;
                    }
                    if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    {
                        type = value;
                    }
                }
            }
            switch (type.ToLower())
            {
                case "settings":
                    GetSpreadsGeneratorConfigs(id, all: string.IsNullOrWhiteSpace(id));
                    break;
                case "spreads":
                    GetSpreadsGeneratorSpreadsAsync(id, all: string.IsNullOrWhiteSpace(id));
                    break;
                default:
                    SendResponseAsync(Response.MakeErrorResponse(404));
                    break;
            }
        }

        private void HandleBasketUpdate(string parameters, string update)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                parameters = "";
            }
            string[] splits = parameters.Split("&");
            string id = "";
            string type = "";

            foreach (string pair in splits)
            {
                string[] pSplit = pair.Split("=");
                if (pSplit.Length > 1)
                {
                    string key = pSplit[0];
                    string value = pSplit[1];
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        id = value;
                    }
                    if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    {
                        type = value;
                    }
                }
            }
            switch (type.ToLower())
            {
                case "settings":
                    SetBasketConfigs(id, update, all: string.IsNullOrWhiteSpace(id));
                    break;
                case "orders":
                    HandleBasketOrderUpdateRequest(RequestMode.Load, id, update, all: string.IsNullOrWhiteSpace(id));
                    break;
                case "add":
                    HandleBasketOrderUpdateRequest(RequestMode.Add, id, update, all: string.IsNullOrWhiteSpace(id));
                    break;
                case "delete":
                    HandleBasketOrderUpdateRequest(RequestMode.Delete, id, update, all: string.IsNullOrWhiteSpace(id));
                    break;
                default:
                    SendResponseAsync(Response.MakeErrorResponse(404));
                    break;
            }
        }

        private void HandleSpreadsGeneratorUpdate(string parameters, string update)
        {
            if (string.IsNullOrEmpty(parameters))
            {
                parameters = "";
            }
            string[] splits = parameters.Split("&");
            string id = "";
            string type = "";

            foreach (string pair in splits)
            {
                string[] pSplit = pair.Split("=");
                if (pSplit.Length > 1)
                {
                    string key = pSplit[0];
                    string value = pSplit[1];
                    if (key.Equals("id", StringComparison.OrdinalIgnoreCase))
                    {
                        id = value;
                    }
                    if (key.Equals("type", StringComparison.OrdinalIgnoreCase))
                    {
                        type = value;
                    }
                }
            }
            switch (type.ToLower())
            {
                case "settings":
                    SetSpreadsGeneratorConfigs(id, update, all: string.IsNullOrWhiteSpace(id));
                    break;
                default:
                    SendResponseAsync(Response.MakeErrorResponse(404));
                    break;
            }
        }

        private async void GetBasketConfigs(string id, bool all = true)
        {
            try
            {
                Dictionary<string, string> basketIdToBasketSettingMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>();
                foreach (System.Windows.Window window in windows)
                {
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            BasketTraderView basket = (BasketTraderView)window;
                            BasketTraderViewModel basketTraderViewModel = basket.DataContext as BasketTraderViewModel;
                            basketIdToBasketSettingMap[basket.Uid] = basketTraderViewModel.GetConfigSerialized();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(basketIdToBasketSettingMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(GetBasketConfigs));
            }
        }

        private async void GetSpreadsGeneratorConfigs(string id, bool all = true)
        {
            try
            {
                Dictionary<string, string> idToSettingMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<SpreadsGeneratorView>();
                foreach (System.Windows.Window window in windows)
                {
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            SpreadsGeneratorView view = (SpreadsGeneratorView)window;
                            SpreadsGeneratorViewModel viewModel = view.DataContext as SpreadsGeneratorViewModel;
                            idToSettingMap[view.Uid] = viewModel.GetConfigSerialized();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(idToSettingMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(GetBasketConfigs));
            }
        }

        private async void GetSpreadsGeneratorSpreadsAsync(string id, bool all = true)
        {
            try
            {
                Dictionary<string, string> idToSettingMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<SpreadsGeneratorView>();
                foreach (System.Windows.Window window in windows)
                {
                    await await window.Dispatcher.InvokeAsync(async () =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            SpreadsGeneratorView view = (SpreadsGeneratorView)window;
                            SpreadsGeneratorViewModel viewModel = view.DataContext as SpreadsGeneratorViewModel;
                            idToSettingMap[view.Uid] = await viewModel.GetSpreadsJsonAsync();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(idToSettingMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(GetBasketConfigs));
            }
        }

        private async void SetBasketConfigs(string id, string config, bool all = true)
        {
            try
            {
                Dictionary<string, string> basketIdToBasketSettingMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>();
                foreach (System.Windows.Window window in windows)
                {
                    await window.Dispatcher.InvokeAsync(() =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            BasketTraderView basket = (BasketTraderView)window;
                            BasketTraderViewModel basketTraderViewModel = basket.DataContext as BasketTraderViewModel;
                            basketTraderViewModel.LoadConfigFromJson(config, isApiRequest: true);
                            basketIdToBasketSettingMap[basket.Uid] = basketTraderViewModel.GetConfigSerialized();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(basketIdToBasketSettingMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(SetBasketConfigs));
            }
        }

        private async void SetSpreadsGeneratorConfigs(string id, string config, bool all = true)
        {
            try
            {
                Dictionary<string, string> idToSettingsMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<SpreadsGeneratorView>();
                foreach (System.Windows.Window window in windows)
                {
                    await await window.Dispatcher.InvokeAsync(async () =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            SpreadsGeneratorView view = (SpreadsGeneratorView)window;
                            SpreadsGeneratorViewModel viewModel = view.DataContext as SpreadsGeneratorViewModel;
                            await viewModel.LoadConfigFromJsonAsync(config);
                            await viewModel.GenerateSpreads();
                            idToSettingsMap[view.Uid] = await viewModel.GetSpreadsJsonAsync();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(idToSettingsMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(SetSpreadsGeneratorConfigs));
            }
        }

        private async void HandleBasketOrderUpdateRequest(RequestMode requestMode, string id, string orders, bool all)
        {
            try
            {
                Dictionary<string, string> basketIdToBasketSettingMap = new();
                List<System.Windows.Window> windows = StartupWindowViewModel.MainWindow.WindowHelper.GetAll<BasketTraderView>();
                foreach (System.Windows.Window window in windows)
                {
                    await window.Dispatcher.BeginInvoke(async () =>
                    {
                        if (all || id.Equals(window.Uid, StringComparison.OrdinalIgnoreCase))
                        {
                            BasketTraderView basket = (BasketTraderView)window;
                            BasketTraderViewModel basketTraderViewModel = basket.DataContext as BasketTraderViewModel;
                            switch (requestMode)
                            {
                                case RequestMode.Add:
                                    await basketTraderViewModel.AddOrders(orders);
                                    break;
                                case RequestMode.Delete:
                                    await basketTraderViewModel.DeleteOrders(orders);
                                    break;
                                case RequestMode.Load:
                                    await basketTraderViewModel.LoadOrdersFromJson(orders, isApiRequest: true);
                                    break;
                            }

                            basketIdToBasketSettingMap[basket.Uid] = basketTraderViewModel.GetOrdersJson();
                        }
                    });
                }
                string json = JsonConvert.SerializeObject(basketIdToBasketSettingMap, Formatting.Indented);
                SendResponseAsync(Response.MakeGetResponse(json, "application/json; charset=UTF-8"));
            }
            catch (Exception ex)
            {
                SendResponseAsync(Response.MakeErrorResponse(ex.Message));
                _log.Error(ex, nameof(HandleBasketOrderUpdateRequest));
            }
        }
    }
}