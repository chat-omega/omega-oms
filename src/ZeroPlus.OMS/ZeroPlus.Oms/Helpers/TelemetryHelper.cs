using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ZeroPlus.Oms.Helpers;

public static class TelemetryHelper
{
    private static readonly Lazy<byte> _boxId = new(DetectBoxId);
    private static readonly Lazy<byte> _instanceId = new(DetectInstanceId);

    public static byte GetBoxId() => _boxId.Value;
    public static byte GetInstanceId() => _instanceId.Value;

    private static byte DetectBoxId()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bytes = addr.Address.GetAddressBytes();
                if (bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 61)
                    return bytes[3];
            }
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                return addr.Address.GetAddressBytes()[3];
            }
        }

        return 0;
    }

    public static bool IsLocal()
    {
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            foreach (var addr in nic.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                var bytes = addr.Address.GetAddressBytes();
                if (bytes[0] == 192 && bytes[1] == 168 && bytes[2] == 61)
                    return true;
            }
        }

        return false;
    }

    private static byte DetectInstanceId()
    {
        try
        {
            int currentPid = Environment.ProcessId;
            int count = 0;
            foreach (var proc in Process.GetProcessesByName("ZeroPlus OMS"))
            {
                if (proc.Id < currentPid)
                    count++;
            }
            return (byte)count;
        }
        catch
        {
            return 0;
        }
    }
}
