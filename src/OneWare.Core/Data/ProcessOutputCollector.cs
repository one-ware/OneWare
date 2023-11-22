using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Threading;
using OneWare.Core.Events;
using Prism.Ioc;
using OneWare.SDK.Services;

namespace OneWare.Core.Data
{
    internal class ProcessOutputCollector
    {
        private readonly DispatcherTimer _dispatcherTimer;
        private readonly ConcurrentQueue<string> _errorCollector = new();
        private readonly ConcurrentQueue<string> _outputCollector = new();

        private int _timeout;

        /// <summary>
        ///     Provides a reliable way to collect external process data
        ///     MAKE SURE TO CREATE FROM CORRECT THREAD
        /// </summary>
        public ProcessOutputCollector(Process process, TimeSpan timespan, string processName)
        {
            Process = process;
            ProcessName = processName;
            process.EnableRaisingEvents = true;

            process.ErrorDataReceived += (o, i) =>
            {
                if (i.Data != null) _errorCollector.Enqueue(i.Data);
            };

            process.OutputDataReceived += (o, i) =>
            {
                if (i.Data != null) _outputCollector.Enqueue(i.Data);
            };

            process.Exited += (o, i) => { Exited?.Invoke(this, EventArgs.Empty); };

            _dispatcherTimer = new DispatcherTimer(timespan, DispatcherPriority.Normal, CollectorCallback);
        }

        private Process Process { get; }
        private string ProcessName { get; }

        public event EventHandler<TextEventArgs>? ErrorReceived;
        public event EventHandler<TextEventArgs>? OutputReceived;
        public event EventHandler<EventArgs>? Exited;

        public void Start()
        {
            _dispatcherTimer.Start();
            try
            {
                Process.Start();
                Process.BeginOutputReadLine();
                Process.BeginErrorReadLine();
            }
            catch (Exception e)
            {
                ContainerLocator.Container.Resolve<ILogger>()?.Error(ProcessName + " has crashed!\n" + e.Message, e, true, true);
                Exited?.Invoke(this, EventArgs.Empty);
            }
        }

        private void CollectorCallback(object? sender, EventArgs e)
        {
            if (_outputCollector.Count > 0)
            {
                var deq = _outputCollector.TryDequeue(out var r);
                if (r == null) return;
                OutputReceived?.Invoke(this, new TextEventArgs(r));
            }
            else if (_errorCollector.Count > 0)
            {
                var deq = _errorCollector.TryDequeue(out var r);
                if (r == null) return;
                ErrorReceived?.Invoke(this, new TextEventArgs(r));
            }
            else
            {
                try
                {
                    if (Process.HasExited) _timeout++;
                }
                catch
                {
                    _timeout = 101;
                }

                if (_timeout > 100)
                {
                    _dispatcherTimer.Stop();
                    Process?.Dispose();
                }
            }
        }
    }
}