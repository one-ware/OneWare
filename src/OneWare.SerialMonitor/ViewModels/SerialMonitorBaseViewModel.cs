using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Binding;
using OneWare.Essentials.Services;
using OneWare.Output.ViewModels;

namespace OneWare.SerialMonitor.ViewModels;

public abstract class SerialMonitorBaseViewModel : OutputBaseViewModel
{
    private readonly List<string> _lastCommands = new();

    private bool _allowConnecting = true;

    private int _byteCount;

    private string _commandBoxText = string.Empty;

    private SerialPort? _currentPort;

    private bool _firstInsert;

    private bool _isConnected;
    private int _lastCommandIndex;

    private int _selectedBaudRate;

    private string _selectedLineEncoding = string.Empty;

    private string _selectedLineEnding = string.Empty;

    private string? _selectedSerialPort;

    public SerialMonitorBaseViewModel(ISettingsService settingsService, string iconKey) : base(iconKey)
    {
        BaudOptions = new ObservableCollection<int>
        {
            75, 110, 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200, 230400, 250000, 256000, 460800,
            500000, 921600, 1000000, 2000000, 3000000, 5000000, 10000000, 12000000
        };

        SerialPorts = new ObservableCollection<string>(SerialPort.GetPortNames());

        settingsService.Bind("SerialMonitor_SelectedBaudRate",
            this.WhenValueChanged(x => x.SelectedBaudRate)).Subscribe(x => SelectedBaudRate = x);

        settingsService.Bind("SerialMonitor_SelectedLineEncoding",
            this.WhenValueChanged(x => x.SelectedLineEncoding)).Subscribe(x => SelectedLineEncoding = x!);

        settingsService.Bind("SerialMonitor_SelectedLineEnding",
            this.WhenValueChanged(x => x.SelectedLineEnding)).Subscribe(x => SelectedLineEnding = x!);
    }

    public ObservableCollection<string> SerialPorts { get; }
    public ObservableCollection<int> BaudOptions { get; }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    public List<string> AvailableLineEndings { get; } = new() { @"\r\n", @"\n", "None" };

    public string SelectedLineEnding
    {
        get => _selectedLineEnding;
        set => SetProperty(ref _selectedLineEnding, value);
    }

    public List<string> AvailableEncodings { get; } = new() { "ASCII", "Byte", "HEX" };

    public string SelectedLineEncoding
    {
        get => _selectedLineEncoding;
        set => SetProperty(ref _selectedLineEncoding, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => SetProperty(ref _isConnected, value);
    }

    public bool AllowConnecting
    {
        get => _allowConnecting;
        set => SetProperty(ref _allowConnecting, value);
    }

    public string CommandBoxText
    {
        get => _commandBoxText;
        set => SetProperty(ref _commandBoxText, value);
    }

    public string? SelectedSerialPort
    {
        get => _selectedSerialPort;
        set
        {
            if (!SetProperty(ref _selectedSerialPort, value)) return;

            _currentPort?.Dispose();

            _currentPort = string.IsNullOrWhiteSpace(value)
                ? null
                : new SerialPort(value, SelectedBaudRate, Parity.None, 8, StopBits.One);

            if (_currentPort == null)
            {
                IsConnected = false;
                return;
            }

            _currentPort.DataReceived += TextReceived;
            _currentPort.ErrorReceived += ErrorReceived;
            _currentPort.ReadBufferSize = 64000000;
            _currentPort.Encoding = Encoding.GetEncoding("ISO-8859-1");
            try
            {
                _currentPort.Open();
                WriteLine("Successfully opened connection on " + value, Brushes.Green);
                IsConnected = true;
            }
            catch (Exception e)
            {
                WriteLine("Could not open serial connection: " + e.Message, Brushes.Red);
                IsConnected = false;
            }
        }
    }

    public void RefreshSerialPorts()
    {
        var selectedPort = _currentPort?.PortName ?? null;
        SelectedSerialPort = null;
        SerialPorts.Clear();
        SerialPorts.AddRange(SerialPort.GetPortNames());
        SelectedSerialPort = selectedPort;

        if (SerialPorts.Count == 1) WriteLine("Found " + SerialPorts.Count + " usable port!", Brushes.Gray);
        else WriteLine("Found " + SerialPorts.Count + " usable ports!", Brushes.Gray);
    }

    public new void Clear()
    {
        _byteCount = 0;
        base.Clear();
        //MainDock.SerialMonitorPlot.Clear();
    }

    public void Disconnect()
    {
        if (_currentPort?.IsOpen ?? false) _currentPort.Close();
        SelectedSerialPort = null;
    }

    /// <summary>
    ///     Gets called everytime the user sends a command
    /// </summary>
    /// <param name="text">Command</param>
    public void SendText(string text)
    {
        if (_currentPort == null)
        {
            WriteLine("[Error] No serial port selected!", Brushes.Red);
        }
        else
        {
            if (!_currentPort.IsOpen)
                try
                {
                    _currentPort.Open();
                }
                catch (Exception e)
                {
                    WriteLine("[Error] Can't open connection: " + e.Message, Brushes.Red);
                    return;
                }

            //this.WriteLine("Sending: " + text + " ...\n");

            try
            {
                if (SelectedLineEncoding == "ASCII")
                {
                    _currentPort.Write(text + (SelectedLineEnding == "None" ? "" : Regex.Unescape(SelectedLineEnding)));
                }
                else
                {
                    var numbers = text.Split(' ');
                    var dataList = new List<byte>();
                    foreach (var n in numbers)
                        dataList.Add(SelectedLineEncoding == "Byte" ? byte.Parse(n) : Convert.ToByte(n, 16));
                    _currentPort.Write(dataList.ToArray(), 0, dataList.Count);
                }
            }
            catch (Exception e)
            {
                WriteLine("Sending failed: " + e.Message, Brushes.Red);
            }

            if (_lastCommands.Count == 0 || _lastCommands[0] != text) _lastCommands.Insert(0, text);
            _lastCommandIndex = 0;
            _firstInsert = true;

            //Clear commandbox    
            CommandBoxText = string.Empty;
        }
    }

    public void TextReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (SelectedLineEncoding == "ASCII")
            {
                var existing = _currentPort?.ReadExisting();
                if (existing != null) Write(existing);
            }
            else
            {
                var existing = "";
                while (_currentPort != null && _currentPort.BytesToRead > 0)
                {
                    var r = _currentPort.ReadByte();
                    if (SelectedLineEncoding == "Byte") existing += r + " ";
                    else existing += r.ToString("X2") + " ";

                    if (_byteCount < 31)
                    {
                        if (_byteCount % 8 == 7) existing += "  ";
                        _byteCount++;
                    }
                    else
                    {
                        _byteCount = 0;
                        existing += '\n';
                    }
                }

                Write(existing);
            }
        });
    }

    public void ErrorReceived(object? sender, SerialErrorReceivedEventArgs args)
    {
        try
        {
            Write(_currentPort?.ReadExisting() ?? "");
        }
        catch (Exception e)
        {
            WriteLine(e.Message, Brushes.Red);
        }
    }

    public void InsertUp()
    {
        if (!_firstInsert && _lastCommands.Count > _lastCommandIndex + 1)
        {
            _lastCommandIndex++;
            CommandBoxText = _lastCommands[_lastCommandIndex];
        }
        else if (_firstInsert && _lastCommands.Count > _lastCommandIndex)
        {
            _firstInsert = false;
            CommandBoxText = _lastCommands[_lastCommandIndex];
        }
    }

    public void InsertDown()
    {
        if (_lastCommandIndex > 0) _lastCommandIndex--;
        if (_lastCommands.Count > _lastCommandIndex) CommandBoxText = _lastCommands[_lastCommandIndex];
    }
}