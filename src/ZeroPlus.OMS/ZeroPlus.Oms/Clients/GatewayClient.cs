using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using ZeroPlus.Comms.Models.Data;
using ZeroPlus.Comms.Models.Data.MarketData;
using ZeroPlus.Comms.Models.Data.Oms.Config;
using ZeroPlus.Comms.Models.Data.Oms.DomsManager;
using ZeroPlus.Comms.Models.Data.Requests;
using ZeroPlus.Comms.Models.Data.Responses;
using ZeroPlus.Comms.Models.Protocols.FAST;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Clients
{
    public delegate void ConfigShareEventHandler(ConfigShare configShare);
    public delegate void ConfigChangeEventHandler(ConfigSave configSave);
    public delegate void EntitlementMapUpdatedHandler();

    public class GatewayClient
    {
        public event ConnectionStatusChangedEventHandler ConnectionStatusChangedEvent;
        public event ConfigShareEventHandler ConfigShareEvent;
        public event ConfigChangeEventHandler ConfigChangeEvent;
        public event EntitlementMapUpdatedHandler EntitlementUpdated;


        private readonly OmsConfig _config;
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly CommsClient _commsClient;
        private readonly ConcurrentDictionary<int, User> _userIdToUserNameLookup = new();
        private User _user;

        public string AuthCode { get; set; }
        public bool IsConnected { get; set; }
        public HashSet<int> GrantedModules { get; set; } = new();
        public ConcurrentDictionary<string, UserEntitlement> EntitlementMap { get; } = new();

        public GatewayClient(OmsConfig config, OmsCore omsCore)
        {
            _config = config;
            _commsClient = new CommsClient(OmsConfig.GatewayGuid, config, HandleMessage, omsCore, register: false);
            _commsClient.ConnectionStatusChangedEvent += OnConnectionStatusChangedEvent;
        }

        public async Task RestartAsync()
        {
            await StopAsync();
            await StartAsync();
        }

        public async Task<bool> StartAsync()
        {
            _log.Info(nameof(StartAsync));
            return await Task.Run(() => _commsClient.Start(_config.AuthServer, _config.AuthServerPort));
        }

        public async Task StopAsync()
        {
            _log.Info(nameof(StopAsync));
            await Task.Run(() => _commsClient.Stop());
        }

        public async Task<User> AuthenticateAsync(string username, SecureString securePassword, string appCode)
        {
            var currentWindowsIdentity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return await Task.Run(async () =>
            {
                LoginResponse loginResponse = _commsClient.SendAuthMessage(username, securePassword, appCode);
                _log.Info("Login attempt to account: {0} by user {1}", username, currentWindowsIdentity.Name);
                return await HandleLoginResponse(loginResponse);
            });
        }

        public async Task<User> AuthenticateAsync(string username, string authCode)
        {
            return await Task.Run(async () =>
            {
                LoginResponse loginResponse = _commsClient.SendAuthMessage(username, authCode);
                return await HandleLoginResponse(loginResponse);
            });
        }

        private async Task<User> HandleLoginResponse(LoginResponse loginResponse)
        {
            if (loginResponse == null)
            {
                return null;
            }

            _user = loginResponse.User;
            AuthCode = loginResponse.AuthCode;
            GrantedModules = loginResponse.User.Modules;
            SaveEntitlements(loginResponse.User.Entitlements);
            await LoadCommissionsAsync();
            await LoadUserLookup();
            return _user;
        }

        private async Task LoadUserLookup()
        {
            try
            {
                var users = await GetUsersAsync();
                if (users != null)
                {
                    foreach (var user in users)
                    {
                        _userIdToUserNameLookup[user.ID] = user;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadUserLookup));
            }
        }

        public void ShareConfig(ConfigShare config)
        {
            Task.Run(() => _commsClient.SendConfigShareMessage(config));
        }

        public void SaveConfig(ConfigSave config)
        {
            Task.Run(() => _commsClient.SendConfigSaveMessage(config));
            ConfigChangeEvent?.Invoke(config);
        }

        public async Task<string> DeleteConfigAsync(int id)
        {
            return await Task.Run(() => _commsClient.SendConfigDeleteMessage(id));
        }

        public async Task<bool> RequestPasswordChangeAsync(SecureString currentPassword, SecureString newPassword)
        {
            return await Task.Run(() => _commsClient.SendRequestPasswordChangeMessage(currentPassword, newPassword));
        }

        public async Task<string> RequestPnlReportAsync(string format, DateTime start, DateTime end, List<string> usernames, List<string> tags, List<string> symbols, List<string> underlyings)
        {
            return await Task.Run(() => _commsClient.SendRequestPnlReportMessage(format, start, end, usernames, tags, symbols, underlyings));
        }

        public List<OptionSnapshot> RequestOptionSnapshots(string symbol, DateTime expiration, double delta, DateTime startDateTime, DateTime endDateTime)
        {
            Task<List<OptionSnapshot>> snapshotsTask = RequestOptionSnapshotsAsync(symbol, expiration, delta, startDateTime, endDateTime);
            snapshotsTask.Wait();
            List<OptionSnapshot> results = snapshotsTask.Result;
            return results;
        }

        public async Task<List<OptionSnapshot>> RequestOptionSnapshotsAsync(string symbol, DateTime expiration, double delta, DateTime startDateTime, DateTime endDateTime)
        {
            return await Task.Run(() => _commsClient.SendRequestOptionSnapshotsMessage(symbol, expiration, delta, startDateTime, endDateTime));
        }

        public async Task<List<ConfigSave>> RequestConfigsAsync(int module)
        {
            return await Task.Run(() => _commsClient.SendRequestConfigsMessage(module));
        }

        public async Task<ConfigSave> RequestConfigDataAsync(int id)
        {
            return await Task.Run(() => _commsClient.SendRequestConfigDataAsyncMessage(id));
        }

        public async Task<List<User>> GetUsersAsync()
        {
            return await Task.Run(() => _commsClient.SendRequestUsersMessage());
        }

        public async Task<List<DomListInfo>> GetDomListInfosAsync()
        {
            return await Task.Run(() => _commsClient.SendRequestDomListInfosMessage());
        }

        public async Task<GetCommissionsResponse> GetCommissionsAsync()
        {
            return await Task.Run(() => _commsClient.SendGetCommissionsMessage());
        }

        public void SendUserFeedback(string selectedModule, string selectedType, string selectedSeverity, string subject, string details)
        {
            Task.Run(() => _commsClient.SendUserFeedback(selectedModule, selectedType, selectedSeverity, subject, details));
        }

        private void OnConnectionStatusChangedEvent(bool connected)
        {
            _log.Info($"Connection status changed. Connected: {connected}");
            IsConnected = connected;
            ConnectionStatusChangedEvent?.Invoke(IsConnected);
            if (IsConnected)
            {
                _ = LoadCommissionsAsync();

                if (!string.IsNullOrEmpty(AuthCode))
                {
                    LoginResponse loginResponse = _commsClient.SendAuthMessage(_user.Username, AuthCode);
                    if (loginResponse != null)
                    {
                        AuthCode = loginResponse.AuthCode;
                        SaveEntitlements(loginResponse.User.Entitlements);
                    }
                    else
                    {
                        AuthCode = null;
                    }
                }
            }
            else
            {
                EntitlementMap.Clear();
            }
        }

        private async Task LoadCommissionsAsync()
        {
            try
            {
                GetCommissionsResponse commissions = await GetCommissionsAsync();

                _config.BrokerageFee = commissions.BrokerageFee;
                _config.OrfFee = commissions.OrfFee;
                _config.SecFee = commissions.SecFee;
                _config.VolantFee = commissions.VolantFee;
                _config.VolantZprollFee = commissions.VolantZprollFee;
                _config.DashFee = commissions.DashFee;
                _config.DashSPXFee = commissions.DashSPXFee;

                foreach (Comms.Models.Data.Oms.Commission commission in commissions.Commissions)
                {
                    List<KeyValuePair<string, double>> lookup = commission.Lookup.ToList();
                    commission.Lookup = new Dictionary<string, double>();

                    foreach (KeyValuePair<string, double> com in lookup)
                    {
                        commission.Lookup[com.Key.Replace("\r\n", "").Replace("\r", "").Replace("\n", "")] = com.Value;
                    }

                    _config.UnderlyingToCommissionsMap[commission.Symbol] = commission;
                }
                _config.ExecutingBrokerFeeModelsMax = _config.UnderlyingToCommissionsMap.Values
                    .Where(x => x.IsPenny).SelectMany(x => x.Lookup.Values).Max();
                _config.ExecutingBrokerFeeModelsAverage = _config.UnderlyingToCommissionsMap.Values
                    .Where(x => x.IsPenny).SelectMany(x => x.Lookup.Values).Average();
                _config.ExecutingBrokerFeeModelsMaxNonPenny = _config.UnderlyingToCommissionsMap.Values
                    .Where(x => x.IsPenny).SelectMany(x => x.Lookup.Values).Max();
                _config.ExecutingBrokerFeeModelsAverageNonPenny = _config.UnderlyingToCommissionsMap.Values
                    .Where(x => x.IsPenny).SelectMany(x => x.Lookup.Values).Average();
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(LoadCommissionsAsync));
            }
        }

        private void HandleMessage(Message message)
        {
            try
            {
                switch (message.Template.TemplateType)
                {
                    case TemplateType.ConfigShare:

                        ConfigShare configShare = MessageFactory.DecodeConfigShareMessage(message);
                        HandleConfigShareMessage(configShare);
                        break;
                    case TemplateType.DataRequest:
                        DataRequest request = MessageFactory.DecodeDataRequestMessage(message);
                        switch (request.DataRequestType)
                        {
                            case DataRequestType.LoginRequest:
                                if (request is LoginRequest { IsResponse: true, Response: LoginResponse loginResponse })
                                {
                                    SaveEntitlements(loginResponse.User.Entitlements);
                                }
                                break;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(HandleMessage));
            }
        }

        private void SaveEntitlements(List<UserEntitlement> userEntitlements)
        {
            try
            {
                var updated = false;
                foreach (var entitlement in userEntitlements)
                {
                    if (entitlement != null)
                    {
                        if (EntitlementMap.TryGetValue(entitlement.SubGroup, out var oldEnt))
                        {
                            if (oldEnt.ActivationTime != entitlement.ActivationTime ||
                                oldEnt.DeactivationTime != entitlement.DeactivationTime ||
                                oldEnt.Simultaneous != entitlement.Simultaneous)
                            {
                                EntitlementMap[entitlement.SubGroup] = entitlement;
                                updated = true;
                            }
                        }
                        else
                        {
                            EntitlementMap[entitlement.SubGroup] = entitlement;
                            updated = true;
                        }
                    }
                }

                if (updated)
                {
                    EntitlementUpdated?.Invoke();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(SaveEntitlements));
            }
        }

        private void HandleConfigShareMessage(ConfigShare configShare)
        {
            ConfigShareEvent?.Invoke(configShare);
        }

        public bool TryGetUser(ushort userId, out User user)
        {
            return _userIdToUserNameLookup.TryGetValue(userId, out user);
        }
    }
}
