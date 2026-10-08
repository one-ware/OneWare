using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DynamicData.Binding;
using OneWare.Essentials.Services;
using OneWare.Output.ViewModels;

namespace OneWare.SerialMonitor.ViewModels;

public abstract class SerialMonitorBaseViewModel : OutputBaseViewModel
{
    private readonly List<string> _lastCommands = new();

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
            75, 110, 300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 74880, 115200, 230400, 250000, 256000, 460800,
            500000, 921600, 1000000, 2000000, 3000000, 5000000, 10000000, 12000000
        };

        SerialPorts = new ObservableCollection<string>(SerialPort.GetPortNames().Distinct().OrderBy(x => x));

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
        set
        {
            if (!SetProperty(ref _selectedBaudRate, value)) return;
            if (_currentPort is not { IsOpen: true } || value <= 0) return;
            try
            {
                _currentPort.BaudRate = value;
                OnPropertyChanged(nameof(ConnectionStatus));
            }
            catch (Exception e)
            {
                WriteLine("Could not change baud rate: " + e.Message, GetThemeBrush("ErrorBrush"));
            }
        }
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
        set
        {
            if (SetProperty(ref _isConnected, value)) OnPropertyChanged(nameof(ConnectionStatus));
        }
    }

    public string ConnectionStatus => IsConnected && _currentPort != null
        ? $"Connected to {_currentPort.PortName} at {_currentPort.BaudRate} baud"
        : "Not connected";

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

            ClosePort();

            if (!string.IsNullOrWhiteSpace(value)) Connect();
        }
    }

    /// <summary>
    ///     Opens the selected serial port (again) with the selected baud rate
    /// </summary>
    public void Connect()
    {
        if (string.IsNullOrWhiteSpace(SelectedSerialPort))
        {
            WriteLine("[Error] No serial port selected!", GetThemeBrush("ErrorBrush"));
            return;
        }

        ClosePort();

        var port = new SerialPort(SelectedSerialPort, SelectedBaudRate > 0 ? SelectedBaudRate : 9600,
            Parity.None, 8, StopBits.One);
        port.DataReceived += TextReceived;
        port.ErrorReceived += ErrorReceived;
        port.ReadBufferSize = 64000000;
        port.Encoding = Encoding.GetEncoding("ISO-8859-1");
        _currentPort = port;

        try
        {
            port.Open();
            WriteLine($"Connected to {port.PortName} at {port.BaudRate} baud", GetThemeBrush("SuccessBrush"));
            IsConnected = true;
        }
        catch (Exception e)
        {
            WriteLine("Could not open serial connection: " + e.Message, GetThemeBrush("ErrorBrush"));
            IsConnected = false;
        }
    }

    public void ToggleConnection()
    {
        if (IsConnected) Disconnect();
        else Connect();
    }

    private void ClosePort()
    {
        if (_currentPort == null) return;

        var port = _currentPort;
        _currentPort = null;
        port.DataReceived -= TextReceived;
        port.ErrorReceived -= ErrorReceived;
        try
        {
            if (port.IsOpen) port.Close();
        }
        catch
        {
            // The device may already be gone
        }

        port.Dispose();
        IsConnected = false;
    }

    public void RefreshSerialPorts()
    {
        var available = SerialPort.GetPortNames().Distinct().OrderBy(x => x).ToList();

        // Update in place so the ComboBox keeps its selection and the connection stays untouched
        foreach (var removed in SerialPorts.Except(available).ToList())
        {
            if (removed == SelectedSerialPort) SelectedSerialPort = null;
            SerialPorts.Remove(removed);
        }

        foreach (var added in available.Except(SerialPorts).ToList())
        {
            var index = SerialPorts.TakeWhile(x => string.CompareOrdinal(x, added) < 0).Count();
            SerialPorts.Insert(index, added);
        }

        WriteLine(SerialPorts.Count == 1 ? "Found 1 serial port" : $"Found {SerialPorts.Count} serial ports",
            GetThemeBrush("ThemeForegroundLowBrush"));
    }

    public new void Clear()
    {
        _byteCount = 0;
        base.Clear();
        //MainDock.SerialMonitorPlot.Clear();
    }

    public void Disconnect()
    {
        if (_currentPort == null) return;
        var name = _currentPort.PortName;
        ClosePort();
        WriteLine("Disconnected from " + name, GetThemeBrush("ThemeForegroundLowBrush"));
    }

    /// <summary>
    ///     Gets called everytime the user sends a command
    /// </summary>
    /// <param name="text">Command</param>
    public void SendText(string text)
    {
        if (_currentPort is not { IsOpen: true }) Connect();

        if (_currentPort is { IsOpen: true })
        {
            try
            {
                if (SelectedLineEncoding == "ASCII")
                {
                    _currentPort.Write(text + (SelectedLineEnding == "None" ? "" : Regex.Unescape(SelectedLineEnding)));
                }
                else
                {
                    var numbers = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var dataList = new List<byte>();
                    foreach (var n in numbers)
                        dataList.Add(SelectedLineEncoding == "Byte" ? byte.Parse(n) : Convert.ToByte(n, 16));
                    _currentPort.Write(dataList.ToArray(), 0, dataList.Count);
                }
            }
            catch (Exception e)
            {
                WriteLine("Sending failed: " + e.Message, GetThemeBrush("ErrorBrush"));
            }

            if (_lastCommands.Count == 0 || _lastCommands[0] != text) _lastCommands.Insert(0, text);
            _lastCommandIndex = 0;
            _firstInsert = true;

            //Clear commandbox    
            CommandBoxText = string.Empty;
        }
    }

    private static IBrush? GetThemeBrush(string key)
    {
        var app = Avalonia.Application.Current;
        return app?.FindResource(app.RequestedThemeVariant, key) as IBrush;
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
            WriteLine(e.Message, GetThemeBrush("ErrorBrush"));
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