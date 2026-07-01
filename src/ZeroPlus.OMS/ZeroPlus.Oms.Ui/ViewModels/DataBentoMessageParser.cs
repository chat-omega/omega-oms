using Middleware.Communication.Tcp;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZeroPlus.Models.Data.Enums;

namespace ZeroPlus.Oms.Ui.ViewModels
{
    internal class DataBentoMessageParser : ITcpMessageParser
    {
        private static readonly ILogger _log = LogManager.GetCurrentClassLogger();
        private readonly TcpClient _tcpClient;
        private EmaChartViewModel _emaChartViewModel;
        private ConcurrentDictionary<string, string> _controlMessages;

        public DataBentoMessageParser(TcpClient tcpClient, EmaChartViewModel emaChartViewModel)
        {
            _controlMessages = new ConcurrentDictionary<string, string>();
            _controlMessages["lsg_version"] = "";
            _controlMessages["cram"] = "";
            _tcpClient = tcpClient;
            _emaChartViewModel = emaChartViewModel;
        }

        public void Parse(TcpSocket tcpSocket, IReadBuffer readBuffer)
        {
            try
            {
                if (readBuffer != null && readBuffer.Length > 0)
                {
                    readBuffer.SeekOrigin();
                    long length = readBuffer.Length;
                    byte[] buffer = new byte[length];
                    readBuffer.Read(buffer, 0, 0, (int)length);
                    string lines = Encoding.ASCII.GetString(buffer);
                    readBuffer.Remove((int)length);
                    ProcessLines(lines);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(Parse));
            }
        }

        private void ProcessLines(string lines)
        {
            try
            {
                List<string> linesPart = lines.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (string line in linesPart)
                {
                    List<string> messages;
                    if (line.Contains('|'))
                    {
                        messages = line.Split('|').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    }
                    else
                    {
                        messages = new List<string>()
                        {
                            line
                        };
                    }
                    foreach (string message in messages)
                    {
                        if (message.Contains('='))
                        {
                            string[] kvp = message.Split('=');
                            string key = kvp[0];
                            string value = kvp[1];
                            _controlMessages[key] = value;
                            _log.Info($"{nameof(DataBentoMessageParser)} {key}:{value}");
                            switch (key)
                            {
                                case "cram":
                                    string authResponse = value + "|" + OmsCore.Config.DatabentoApiKey;
                                    string authResponseEnc = SHA256(authResponse);
                                    string bucketId = OmsCore.Config.DatabentoApiKey.Substring(OmsCore.Config.DatabentoApiKey.Length - 5, 5);
                                    string fullResponse = $"auth={authResponseEnc}-{bucketId}|dataset=XNAS.ITCH|encoding=json|ts_out=0\n";
                                    _tcpClient.SendData(Encoding.ASCII.GetBytes(fullResponse));
                                    break;
                                case "success":
                                    if (value == "1")
                                    {
                                        List<string> symbols = _emaChartViewModel.GetSymbols();
                                        string subscriptionKey = $"schema=ohlcv-1s|stype_in=raw_symbol|symbols={string.Join(",", symbols)}|start={DateTime.Today:s}\n";
                                        _tcpClient.SendData(Encoding.ASCII.GetBytes(subscriptionKey));
                                    }
                                    break;
                            }
                        }

                        if (message.StartsWith("{\"hd\":"))
                        {
                            Dictionary<string, Dictionary<string, string>> payload = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(message);
                            if (payload != null)
                            {
                                if (payload.TryGetValue("hd", out Dictionary<string, string> content))
                                {
                                    if (content.TryGetValue("instrument_id", out string instrument_id))
                                    {
                                        foreach (KeyValuePair<string, string> kvp in content)
                                        {
                                            if (Enum.TryParse(kvp.Key, true, out SubscriptionFieldType type) && double.TryParse(kvp.Value, out double value))
                                            {
                                                SubscriptionKey key = new(instrument_id, type);
                                                _emaChartViewModel.SubscribedDataUpdateValue(key, value, false);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, nameof(ProcessLines));
            }
        }

        static string SHA256(string randomString)
        {
            System.Security.Cryptography.SHA256 crypt = System.Security.Cryptography.SHA256.Create();
            StringBuilder hash = new();
            byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
            foreach (byte theByte in crypto)
            {
                hash.Append(theByte.ToString("x2"));
            }
            return hash.ToString();
        }

        public byte[] GetHeartbeatPacket()
        {
            return Array.Empty<byte>();
        }
    }
}
