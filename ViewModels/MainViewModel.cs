using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using System.Text;
using OxyPlot;
using OxyPlot.Series;
using System.Collections.Concurrent;

namespace LudControl
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private PlotModel _plotModel;
        private readonly TcpClient _tcpClient;
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _serverEndPoint;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private string _commandViewer;
        private readonly ConcurrentQueue<UInt16[]> _dataBuffer = new ConcurrentQueue<UInt16[]>();
        private const int BufferSize = 500;

        public MainViewModel()
        {
            _udpClient = new UdpClient(0); // 
            _udpClient.Connect("127.0.0.1", 62126);
            _serverEndPoint = new IPEndPoint(IPAddress.Any, 62126); // Серверный порт
            _tcpClient = new TcpClient();

            SetupPlot();
            StartCommand = new RelayCommand(async _ => await ManagerAsync("start"));
            StopCommand = new RelayCommand(async _ => await ManagerAsync("stop"));
            ExitCommand = new RelayCommand(_ => Dispose());
            AddMeCommand = new RelayCommand(async _ => await SubscribeAsync("ADD_ME"));
            DelMeCommand = new RelayCommand(async _ => await SubscribeAsync("DELL_ME"));

            Task.Run(() => UdpReceiverAsync(_cts.Token));
            Task.Run(() => RenderGraphAsync(_cts.Token));
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

        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged(nameof(PlotModel));
            }
        }

        private async Task SubscribeAsync(string command)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(command);
                 _udpClient.Send(data, data.Length);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CommandViewer += $"Ошибка отправки команды {command}: {ex.Message}\n";
                });
            }
        }

        private async Task ManagerAsync(string command)
        {
            try
            {
                if (!_tcpClient.Connected && command == "start")
                {
                    await _tcpClient.ConnectAsync("127.0.0.1", 62125);
                }
                
                NetworkStream stream = _tcpClient.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(command);
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CommandViewer += $"Ошибка отправки команды {command}: {ex.Message}\n";
                });
            }
        }

        private async Task UdpReceiverAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    IPEndPoint dd = new IPEndPoint(IPAddress.Any, 0);
                    byte[] buffer = _udpClient.Receive(ref dd);
                    
                    UInt16[] data = new UInt16[buffer.Length / 2];
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (UInt16)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                    }

                    while (_dataBuffer.Count >= BufferSize)
                    {
                        _dataBuffer.TryDequeue(out _);
                    }
                    _dataBuffer.Enqueue(data);

                    //System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    //{
                    //    PlotSignal(data);
                    //});

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


        private async Task RenderGraphAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_dataBuffer.TryPeek(out UInt16[] data))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        PlotSignal(data);
                    });
                }
                await Task.Delay(1);
            }
        }

        private void PlotSignal(UInt16[] values)
        {
            _plotModel.Series.Clear();
            var series = new LineSeries();

            for (int i = 0; i < values.Length; i++)
            {
                Int16 result = (short)((values[i] > 2047) ? values[i] - 4096 : values[i]);
                float finalValue = (float)(result * 1.75 / 4096.0);
                series.Points.Add(new DataPoint(i, finalValue));
            }

            _plotModel.Series.Add(series);
            _plotModel.InvalidatePlot(true);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void SetupPlot()
        {
            PlotModel = new PlotModel
            {
                Title = "График",
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
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Title = "дБ"
            });

            PlotModel.Axes.Add(new OxyPlot.Axes.LinearAxis
            {
                Position = OxyPlot.Axes.AxisPosition.Bottom,
                Title = "МКС"
            });
        }
        public void Dispose()
        {
            _cts.Cancel();
            _tcpClient?.Close();
            _udpClient?.Close();
        }
    }
}