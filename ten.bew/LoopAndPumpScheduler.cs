using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ten.bew
{
    public class LoopAndPumpScheduler : System.Threading.Tasks.TaskScheduler
    {
        public const int TRACEEVENT_ERROR = 0;

        private static long _count;
        private List<Task> _tasks = new List<Task>();
        private List<IEnumerable<Task>> _pumpTasks = new List<IEnumerable<Task>>();

        private Thread _loopThread;
        private List<Thread> _pumpThreads;
        private readonly object _loopLockObject = new object();
        private readonly object _pumpLockObject = new object();
        private TraceSource _tracingLoopAndPumpSource = new TraceSource("loopAndPump");

        public LoopAndPumpScheduler()
        {
            var instance = Interlocked.Increment(ref _count);

            _loopThread = new Thread(Loop);
            _loopThread.Name = string.Format("Pump{0}", instance);
            _loopThread.Start();

            _pumpThreads = new List<Thread>();

            for (int i = 0; i < 50; i++)
            {
                var pumpThread = new Thread(Pump);
                _pumpThreads.Add(pumpThread);
                pumpThread.Name = string.Format("Loop{0}-{1}", instance, i);
                pumpThread.Start();
            }
        }

        private void Loop()
        {
            Task[] toExecute = null;

            while (true)
            {
                try
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
                catch (Exception ex)
                {
                    _tracingLoopAndPumpSource.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
                }
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

        public int Count
        {
            get
            {
                lock (_pumpLockObject)
                {
                    return _pumpTasks.Count; // not synchronized - exact count unimportant
                }
            }
        }

        private void Pump()
        {
            while(true)
            {
                try
                {
                    IEnumerable<Task> toExecute = null;

                    lock (_pumpLockObject)
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
                catch(Exception ex)
                {
                    _tracingLoopAndPumpSource.TraceData(TraceEventType.Error, TRACEEVENT_ERROR, ex);
                }
            }
        }

        private void Execute(IEnumerable<Task> toExecute)
        {
            foreach (var task in toExecute)
            {
                _tracingLoopAndPumpSource.TraceEvent(TraceEventType.Verbose, 1, "Start");
                base.TryExecuteTask(task);
                _tracingLoopAndPumpSource.TraceEvent(TraceEventType.Verbose, 1, "Stop");
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
