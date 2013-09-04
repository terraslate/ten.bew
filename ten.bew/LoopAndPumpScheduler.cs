using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ten.bew
{
    public class LoopAndPumpScheduler : System.Threading.Tasks.TaskScheduler
    {
        static long _count;
        List<Task> _tasks = new List<Task>();
        List<IEnumerable<Task>> _pumpTasks = new List<IEnumerable<Task>>();

        Thread _loopThread;
        List<Thread> _pumpThreads;
        readonly object _loopLockObject = new object();
        readonly object _pumpLockObject = new object();

        public LoopAndPumpScheduler()
        {
            var instance = Interlocked.Increment(ref _count);

            _loopThread = new Thread(Loop);
            _loopThread.Name = string.Format("Pump{0}", instance);
            _loopThread.Start();

            _pumpThreads = new List<Thread>();

            for (int i = 0; i < 100; i++)
            {
                var pumpThread = new Thread(Pump);
                _pumpThreads.Add(pumpThread);
                pumpThread.Name = string.Format("Loop{0}", instance);
                pumpThread.Start();
            }
        }

        private void Loop()
        {
            Task[] toExecute = null;

            while (true)
            {
                lock (_loopLockObject)
                {
                    if (toExecute != null && toExecute.Length > 0)
                    {
                        foreach (var task in toExecute)
                        {
                            _tasks.Remove(task);
                        }
                    }

                    if (_tasks.Count == 0)
                    {
                        Monitor.Wait(_loopLockObject);
                        Monitor.Pulse(_loopLockObject);
                    }

                    toExecute = _tasks.ToArray();
                }

                AddToPump(toExecute);
            }
        }

        private void AddToPump(IEnumerable<Task> toExecute)
        {
            lock(_pumpLockObject)
            {
                _pumpTasks.Add(toExecute);
                Monitor.Pulse(_pumpLockObject);
            }
        }

        private void Pump()
        {
            while(true)
            {
                IEnumerable<Task> toExecute = null;

                lock(_pumpLockObject)
                {
                    if (_pumpTasks.Count > 0)
                    {
                        toExecute = _pumpTasks[0];
                        _pumpTasks.RemoveAt(0);

                        Monitor.Pulse(_pumpLockObject);
                    }
                    else
                    {
                        Monitor.Wait(_pumpLockObject);
                        continue;
                    }
                }

                Execute(toExecute);
            }
        }

        private void Execute(IEnumerable<Task> toExecute)
        {
            foreach (var task in toExecute)
            {
                Console.WriteLine("Start task");
                base.TryExecuteTask(task);
                Console.WriteLine("Stop task");
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_loopLockObject)
            {
                foreach (var task in _tasks)
                {
                    yield return task;
                }
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_loopLockObject)
            {
                _tasks.Add(task);
                Monitor.Pulse(_loopLockObject);
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return false;
        }

        protected override bool TryDequeue(Task task)
        {
            bool rv = false;

            lock (_loopLockObject)
            {
                rv = _tasks.Contains(task);

                if (rv)
                {
                    _tasks.Remove(task);
                }
            }

            return rv;
        }
    }
}
