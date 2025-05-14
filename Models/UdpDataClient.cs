using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LudControl.Models
{
    public class UdpDataClient : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEndPoint;
        public bool IsSubscribed { get; private set; }

        public UdpDataClient(string ip, int port)
        {
            _serverEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            _udpClient = new UdpClient();
        }

        public void SubscribeAsync()
        {
            byte[] data = Encoding.ASCII.GetBytes("ADD_ME");
            _udpClient.SendAsync(data, data.Length, _serverEndPoint);

            IsSubscribed = true;
        }

        public void UnsubscribeAsync()
        {
            byte[] data = Encoding.ASCII.GetBytes("DEL_ME");
            _udpClient.SendAsync(data, data.Length, _serverEndPoint);

            IsSubscribed = false;
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}