
using System.Collections.Generic;

namespace ZeroPlus.Comms.Models.Data
{
    public class User
    {
        public string? Username { get; set; }
        public string? AppCode { get; set; }
        public List<string>? Accounts { get; set; }
        public HashSet<int> GrantedModules { get; set; } = new();
    }

    public class UserEntitlement
    {
        public string? Module { get; set; }
        public bool Granted { get; set; }
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public User? User { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ConfigResponse
    {
        public string? Config { get; set; }
    }

    public delegate void ConnectionStatusChangedEventHandler(bool isConnected);
}
