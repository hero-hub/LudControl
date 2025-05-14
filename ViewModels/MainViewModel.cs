using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.IO;
using LudControl.Models;
using OxyPlot;
using OxyPlot.Series;


namespace LudControl
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly TcpCommandServer _tcpServer; // TCP-сокет
        private readonly UdpDataServer _udpServer; // UDP-сокет
        private readonly UdpDataClient _udpClient; // UDP-клиент для работы с сокетом

        private PlotModel _plotModel;
        private string _commandViewer = string.Empty;
        private bool _isConnected = false;

        public MainViewModel()
        {
            _tcpServer = new TcpCommandServer(62125);
            _udpServer = new UdpDataServer(62126);
            _udpClient = new UdpDataClient("127.0.0.1", 62126);

            _tcpServer.CommandReceived += OnTcpCommandReceived;
            _udpServer.DataReceived += OnUdpDataReceived;

            //SetupPlot();
            Start = new RelayCommand(async _ => await StartServerAsync(), _ => !_isConnected);
            Stop = new RelayCommand(_ => StopServer(), _ => _isConnected);
            Exit = new RelayCommand(_ => ExitApplication(), _ => true);

            AddMeCommand = new RelayCommand(_ => _udpClient.SubscribeAsync());
            DelMeCommand = new RelayCommand(_ => _udpClient.UnsubscribeAsync());
        }

        public ICommand Start { get; }
        public ICommand Stop { get; }
        public ICommand Exit { get; }
        public ICommand AddMeCommand { get; }
        public ICommand DelMeCommand { get; }

        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }
        public string CommandViewer // Поле вывода команд/ошибок
        {
            get => _commandViewer;
            set
            {
                _commandViewer = value;
                OnPropertyChanged(nameof(CommandViewer));
            }
        }

        private async Task StartServerAsync()
        {
            try
            {
                await _tcpServer.StartAsync();
                _ = _udpServer.StartAsync();
                _isConnected = true;
                CommandViewer += "Сервер запущен\n";
            }
            catch (Exception ex)
            {
                CommandViewer += $"Ошибка запуска: {ex.Message}\n";
            }
        }
        private void StopServer()
        {
            _tcpServer.Stop();
            _udpServer.Stop();
            _isConnected = false;
            CommandViewer += "Сервер остановлен\n";
        }
        private void ExitApplication()
        {
            StopServer();
            System.Windows.Application.Current.Shutdown();
        }

        private void OnTcpCommandReceived(string command)
        {
            CommandViewer += $"[TCP] Получена команда: {command}\n";
        }

        private void OnUdpDataReceived(string data)
        {
            CommandViewer += $"[UDP Data] {data}\n";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}