using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LudControl.Models
{
    public class TcpCommandServer : IDisposable
    {
        private TcpListener _listener;
        private bool _isRunning;
        private readonly int _port;

        public event Action<string> CommandReceived;

        public TcpCommandServer(int port)
        {
            _port = port;
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), _port);
            _listener.Start();

            while (_isRunning)
            {
                try
                {
                    using (var client = await _listener.AcceptTcpClientAsync())
                    using (var stream = client.GetStream())
                    {
                        byte[] buffer = new byte[16004];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string command = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

                        CommandReceived?.Invoke(command);

                        // Отправляем подтверждение
                        string response = $"OK: {command}";
                        byte[] reply = Encoding.ASCII.GetBytes(response);
                        await stream.WriteAsync(reply, 0, reply.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"TCP Error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}

