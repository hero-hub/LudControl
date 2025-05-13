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

        public async Task<string> SubscribeAsync()
        {
            byte[] data = Encoding.ASCII.GetBytes("ADD_ME");
            await _udpClient.SendAsync(data, data.Length, _serverEndPoint);

            var result = await _udpClient.ReceiveAsync();
            IsSubscribed = true;
            return Encoding.ASCII.GetString(result.Buffer);
        }

        public async Task<string> UnsubscribeAsync()
        {
            byte[] data = Encoding.ASCII.GetBytes("DEL_ME");
            await _udpClient.SendAsync(data, data.Length, _serverEndPoint);

            var result = await _udpClient.ReceiveAsync();
            IsSubscribed = false;
            return Encoding.ASCII.GetString(result.Buffer);
        }

        public void Dispose()
        {
            _udpClient?.Dispose();
        }
    }
}