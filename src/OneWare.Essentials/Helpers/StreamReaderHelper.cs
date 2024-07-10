namespace OneWare.Essentials.Helpers
{
    /// <summary>
    ///     Helps reading from streams with specified timeout
    /// </summary>
    public class StreamReaderHelper
    {
        private readonly AutoResetEvent _getInput, _gotInput;
        private readonly Thread _inputThread;
        private readonly StreamReader _outputReader;
        private string? _input;

        public StreamReaderHelper(StreamReader output)
        {
            _getInput = new AutoResetEvent(false);
            _gotInput = new AutoResetEvent(false);
            _inputThread = new Thread(Reader)
            {
                IsBackground = true
            };
            _inputThread.Start();
            _outputReader = output;
        }

        private void Reader()
        {
            while (true)
            {
                _getInput.WaitOne();
                _input = _outputReader.ReadLine();
                _gotInput.Set();
            }
        }

        // omit the parameter to read a line without a timeout
        public string? ReadLine(int timeOutMillisecs = Timeout.Infinite)
        {
            _getInput.Set();
            var success = _gotInput.WaitOne(timeOutMillisecs);
            if (success)
                return _input;
            return null;
        }
    }
}