using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace LudControl
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly TcpClient _tcpClient;
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEndPoint;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private string _commandViewer;

        public MainViewModel()
        {
            _udpClient = new UdpClient(); // Автоматический выбор порта
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            _tcpClient = new TcpClient();

            StartCommand = new RelayCommand(_ => Start());
            AddMeCommand = new RelayCommand(_ => Subscribe("ADD_ME"));
            DelMeCommand = new RelayCommand(_ => Subscribe("DELL_ME"));

            // Запускаем асинхронный приёмник в фоновом потоке
            Task.Run(() => UdpReceiverAsync(_cts.Token));
        }

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand AddMeCommand { get; }
        public ICommand DelMeCommand { get; }

        public string CommandViewer
        {
            get => _commandViewer;
            set
            {
                _commandViewer = value;
                OnPropertyChanged(nameof(CommandViewer));
            }
        }

        private void Subscribe(string command)
        {
            byte[] data = Encoding.UTF8.GetBytes(command);
            _udpClient.Send(data, data.Length, _serverEndPoint);
        }

        private void Start()
        {
            if (!_tcpClient.Connected)
            _tcpClient.Connect("127.0.0.1", 62125);

            NetworkStream stream = _tcpClient.GetStream();
            byte[] data = Encoding.UTF8.GetBytes("Start");
            stream.Write(data, 0, data.Length);
        }

        private async Task UdpReceiverAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    byte[] data = result.Buffer;
                    string message = $"Первые 10 байт: {string.Join(", ", data.Take(10))}\n";
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CommandViewer += message;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CommandViewer += $"Ошибка в UdpReceiver: {ex.Message}\n";
                });
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            _cts.Cancel();
            _tcpClient?.Close();
            _udpClient?.Close();
        }
    }
}