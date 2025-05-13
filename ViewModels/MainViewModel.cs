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
        private readonly TcpCommandServer _tcpServer;
        private readonly UdpDataServer _udpServer;
        private readonly UdpDataClient _udpClient;

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

            AddMeCommand = new RelayCommand(async _ => await SubscribeAsync(), _ => _isConnected && !_udpClient.IsSubscribed);
            DelMeCommand = new RelayCommand(async _ => await UnsubscribeAsync(), _ => _isConnected && _udpClient.IsSubscribed);
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
        public string CommandViewer // Поле команд (нужно ли?)
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
                _ = _udpServer.StartAsync(); // Запускаем в фоновом режиме
                _isConnected = true;
                CommandViewer += "[System] Сервер запущен\n";
            }
            catch (Exception ex)
            {
                CommandViewer += $"[Error] Ошибка запуска: {ex.Message}\n";
            }
        }
        private void StopServer()
        {
            _tcpServer.Stop();
            _udpServer.Stop();
            _isConnected = false;
            CommandViewer += "[System] Сервер остановлен\n";
        }
        private void ExitApplication()
        {
            StopServer();
            System.Windows.Application.Current.Shutdown();
        }

        private async Task SubscribeAsync()
        {
            try
            {
                var response = await _udpClient.SubscribeAsync();
                CommandViewer += $"[UDP] {response}\n";
            }
            catch (Exception ex)
            {
                CommandViewer += $"[UDP Error] {ex.Message}\n";
            }
        }

        private async Task UnsubscribeAsync()
        {
            try
            {
                var response = await _udpClient.UnsubscribeAsync();
                CommandViewer += $"[UDP] {response}\n";
            }
            catch (Exception ex)
            {
                CommandViewer += $"[UDP Error] {ex.Message}\n";
            }
        }

        private void OnTcpCommandReceived(string command)
        {
            CommandViewer += $"[TCP] Получена команда: {command}\n";
        }

        private void OnUdpDataReceived(string data)
        {
            // Здесь можно обрабатывать поступающие данные
            // Например, обновлять график
            CommandViewer += $"[UDP Data] {data}\n";
        }

        /*private void SetupPlot() // Метод для графика
        {
            PlotModel = new PlotModel
            {
                Title = "Анализ дефектоскопии",
                DefaultColors = new List<OxyColor> { OxyColors.Blue },
                IsLegendVisible = true
            };

            PlotModel.Legends.Add(new OxyPlot.Legends.Legend
            {
                LegendPosition = OxyPlot.Legends.LegendPosition.RightTop,
                LegendPlacement = OxyPlot.Legends.LegendPlacement.Outside,
                LegendOrientation = OxyPlot.Legends.LegendOrientation.Vertical
            });

            PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Left,
                Title = "Величина сигнала",
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot
            });

            PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "Время (с)"
            });
        }*/


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}