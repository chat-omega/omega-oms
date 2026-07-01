namespace Middleware.Communication.Tcp
{
    public class TcpClient
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public bool IsConnected { get; set; }
        public virtual bool Connect() { IsConnected = true; return true; }
        public virtual void Disconnect() { IsConnected = false; }
        public virtual int Send(byte[] buffer, int offset, int count) => count;
        public virtual int Receive(byte[] buffer, int offset, int count) => 0;
    }
}
