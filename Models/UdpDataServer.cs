using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LudControl.Models
{
    public class UdpDataServer : IDisposable
    {
        private readonly UdpClient _udpServer;
        private readonly HashSet<IPEndPoint> _subscribedClients = new HashSet<IPEndPoint>();
        private bool _isRunning;
        private readonly int _port;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public event Action<string> DataReceived;
        public event Action<string> LogMessage;  // Новое событие для логгирования

        public UdpDataServer(int port)
        {
            _port = port;
            _udpServer = new UdpClient(port);
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            OnLogMessage("[UDP] Сервер запущен");

            try
            {
                while (_isRunning && !_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    var result = await _udpServer.ReceiveAsync(_cancellationTokenSource.Token);
                    ProcessMessage(result);
                }
            }
            catch (OperationCanceledException)
            {
                OnLogMessage("[UDP] Сервер остановлен по запросу");
            }
            catch (Exception ex)
            {
                OnLogMessage($"[UDP Error] {ex.Message}");
            }
        }

        private void ProcessMessage(UdpReceiveResult result)
        {
            var clientEndPoint = result.RemoteEndPoint;
            string message = Encoding.ASCII.GetString(result.Buffer);

            switch (message)
            {
                case "ADD_ME":
                    _subscribedClients.Add(clientEndPoint);
                    _ = SendConfirmationAsync(clientEndPoint, "SUBSCRIBED");
                    OnLogMessage($"[UDP] Клиент {clientEndPoint} подписан");
                    break;

                case "DEL_ME":
                    _subscribedClients.Remove(clientEndPoint);
                    _ = SendConfirmationAsync(clientEndPoint, "UNSUBSCRIBED");
                    OnLogMessage($"[UDP] Клиент {clientEndPoint} отписан");
                    break;

                default:
                    DataReceived?.Invoke(message);
                    _ = BroadcastDataAsync(message);
                    break;
            }
        }

        private async Task SendConfirmationAsync(IPEndPoint client, string message)
        {
            byte[] data = Encoding.ASCII.GetBytes(message);
            await _udpServer.SendAsync(data, data.Length, client);
        }

        private async Task BroadcastDataAsync(string data)
        {
            if (_subscribedClients.Count == 0) return;

            byte[] bytes = Encoding.ASCII.GetBytes(data);
            var sendTasks = new List<Task>();

            foreach (var client in _subscribedClients)
            {
                sendTasks.Add(_udpServer.SendAsync(bytes, bytes.Length, client));
            }

            await Task.WhenAll(sendTasks);
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cancellationTokenSource.Cancel();
            OnLogMessage("[UDP] Сервер остановлен");
        }

        protected virtual void OnLogMessage(string message)
        {
            LogMessage?.Invoke(message);
        }

        public void Dispose()
        {
            Stop();
            _udpServer?.Dispose();
            _cancellationTokenSource?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}