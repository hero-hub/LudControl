using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using System.Text;
using OxyPlot;
using OxyPlot.Series;

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

        public MainViewModel()
        {
            _udpClient = new UdpClient(62127); // Привязка к локальному порту 62127
            _serverEndPoint = new IPEndPoint(IPAddress.Loopback, 62126); // Серверный порт
            _tcpClient = new TcpClient();

            SetupPlot();
            StartCommand = new RelayCommand(async _ => await ManagerAsync("Start"));
            StopCommand = new RelayCommand(_ => Dispose());
            AddMeCommand = new RelayCommand(async _ => await SubscribeAsync("ADD_ME"));
            DelMeCommand = new RelayCommand(async _ => await SubscribeAsync("DELL_ME"));

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
                await _udpClient.SendAsync(data, data.Length, _serverEndPoint);
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
                if (!_tcpClient.Connected)
                {
                    await _tcpClient.ConnectAsync("127.0.0.1", 62125)
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
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    byte[] buffer = result.Buffer;

                    UInt16[] data = new UInt16[buffer.Length / 2];
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (UInt16)((buffer[i * 2 + 1] << 8) | buffer[i * 2]);
                    }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        CommandViewer += $"Получено по UDP {data.Length} значений UInt16 от {result.RemoteEndPoint}\n";
                        PlotSignal(data);
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

        private void PlotSignal(UInt16[] values)
        {
            PlotModel.Series.Clear();
            var series = new LineSeries();

            for (int i = 0; i < values.Length; i++)
            {
                int result = (values[i] > 2047) ? values[i] - 4096 : values[i];
                float finalValue = (float)(result * 1.75 / 4096.0);
                series.Points.Add(new DataPoint(i, finalValue));
            }

            PlotModel.Series.Add(series);
            PlotModel.InvalidatePlot(true);
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