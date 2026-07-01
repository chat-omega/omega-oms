using System;

namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct ClientRegistrationModel
    {
        public readonly string Username;
        public readonly string AppId;
        public readonly Version AppVersion;
        public readonly string Hostname;

        public ClientRegistrationModel(string username, string appId, Version appVersion, string hostname)
        {
            Username = username;
            AppId = appId;
            AppVersion = appVersion;
            Hostname = hostname;
        }
    }
}
