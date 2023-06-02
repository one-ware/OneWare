using System.Collections.Concurrent;

namespace OneWare.Core.Data
{
    public class PriorityScheduler : TaskScheduler
    {
        public static PriorityScheduler AboveNormal = new(ThreadPriority.AboveNormal);
        public static PriorityScheduler BelowNormal = new(ThreadPriority.BelowNormal);
        public static PriorityScheduler Lowest = new(ThreadPriority.Lowest);
        private readonly int _maximumConcurrencyLevel = Math.Max(1, Environment.ProcessorCount);
        private readonly ThreadPriority _priority;

        private readonly BlockingCollection<Task> _tasks = new();
        private Thread[]? _threads;

        public PriorityScheduler(ThreadPriority priority)
        {
            _priority = priority;
        }

        public override int MaximumConcurrencyLevel => _maximumConcurrencyLevel;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _tasks;
        }

        protected override void QueueTask(Task task)
        {
            _tasks.Add(task);

            if (_threads == null)
            {
                _threads = new Thread[_maximumConcurrencyLevel];
                for (var i = 0; i < _threads.Length; i++)
                {
                    var local = i;
                    _threads[i] = new Thread(() =>
                    {
                        foreach (var t in _tasks.GetConsumingEnumerable())
                            TryExecuteTask(t);
                    })
                    {
                        Name = string.Format("PriorityScheduler: ", i),
                        Priority = _priority,
                        IsBackground = true
                    };
                    _threads[i].Start();
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false; // we might not want to execute task that should schedule as high or low priority inline
        }
    }
}