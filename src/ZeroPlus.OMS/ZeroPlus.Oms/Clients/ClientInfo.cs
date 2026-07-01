using System;
using System.Linq;
using System.Net;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Clients
{
    internal class ClientInfo
    {
        private readonly OmsConfig _config;

        public string ClientVersion { get; set; } = "";
        public Guid ClientGUID { get; set; }
        public string HostName { get; set; } = "";
        public string LocalIP { get; set; } = "";

        public ClientInfo(OmsConfig config)
        {
            _config = config;
        }

        internal void SetClientInfo(Guid guid)
        {
            try
            {
                ClientGUID = guid;
                ClientVersion = _config.MdsClientVersion;
                HostName = Dns.GetHostName();
                LocalIP = string.Join(", ", Dns.GetHostEntry(HostName).AddressList.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork));
            }
            catch (Exception)
            {
                HostName = "";
                LocalIP = "";

                throw;
            }
        }

        internal string GetRegistrationString()
        {
            return ClientGUID.ToString()
                   + ":"
                   + ClientVersion
                   + ":"
                   + Environment.MachineName
                   + ":"
                   + HostName
                   + ":"
                   + LocalIP
                   + ":"
                   + System.Security.Principal.WindowsIdentity.GetCurrent().Name;
        }
    }
}
