namespace OneWare.Essentials.Helpers;

/// <summary>
///     Helps reading from streams with specified timeout or cancellation token.
/// </summary>
public class StreamReaderHelper : IDisposable
{
    private readonly AutoResetEvent _getInput, _gotInput;
    private readonly StreamReader _outputReader;
    private string? _input;
    private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

    public StreamReaderHelper(StreamReader output)
    {
        _getInput = new AutoResetEvent(false);
        _gotInput = new AutoResetEvent(false);
        var inputThread = new Thread(Reader)
        {
            IsBackground = true
        };
        inputThread.Start();
        _outputReader = output;
    }

    private void Reader()
    {
        while (!_cancellationSource.IsCancellationRequested)
        {
            _getInput.WaitOne();
            _input = _outputReader.ReadLine();
            _gotInput.Set();
        }
    }
    
    public string? ReadLine(int timeout = Timeout.Infinite, CancellationToken cancellationToken = default)
    {
   
        var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cancellationSource.Token).Token;

        _getInput.Set();

        var waitHandles = new WaitHandle[] { _gotInput, combinedToken.WaitHandle };

        var index = WaitHandle.WaitAny(waitHandles, timeout);

        _cancellationSource.Cancel();
        
        return index == 0 ? _input : null; // Timeout
    }

    public void Dispose()
    {
        _getInput.Dispose();
        _gotInput.Dispose();
        _outputReader.Dispose();
        _cancellationSource?.Dispose();
    }
}