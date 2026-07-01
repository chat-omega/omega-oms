using System;

namespace ZeroPlus.Models.Data.Subscription
{
    public readonly struct ClientAuthenticationModel
    {
        public readonly int UserId;
        public readonly string Username;
        public readonly string UserToken;
        public readonly string AppId;
        public readonly Version AppVersion;
        public readonly string Hostname;

        public ClientAuthenticationModel(int userId, string username, string userToken, string appId, Version appVersion, string hostname)
        {
            UserId = userId;
            Username = username;
            UserToken = userToken;
            AppId = appId;
            AppVersion = appVersion;
            Hostname = hostname;
        }
    }
}
