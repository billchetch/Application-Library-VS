using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Chetch.Application
{
    /// <summary>
    /// </summary>
    public class ThreadExecutionState
    {
        public enum ExecutionState
        {
            READY,
            STARTED,
            EXECUTING,
            COMPLETED,
            ABORTED
        }

        public String ID { get; internal set; }
        private Object _lock = new object();

        private ExecutionState _state;
        public ExecutionState State {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                switch (_state)
                {
                    case ExecutionState.STARTED:
                        Started = DateTime.Now.Ticks; break;

                    case ExecutionState.COMPLETED:
                    case ExecutionState.ABORTED:
                        Finished = DateTime.Now.Ticks; break;
                }
            }
        }
        public List<Exception> Exceptions { get; internal set; } = new List<Exception>();
        public int CurrentQueueSize { get; set; }
        public long Started { get; internal set; }
        public long Finished { get; internal set; }
        public bool IsFinished {
            get
            {
                return _state == ExecutionState.COMPLETED || _state == ExecutionState.ABORTED;
            }
        }
        private int _userState = -1; //start at -1 because enum by default starts at 0 and user might give enum values for this
        private Dictionary<int, long> _userStates = null;
        public int UserState
        {
            get
            {
                return _userState;
            }

            set
            {
                lock (_lock)
                {
                    if(_userStates == null)
                    {
                        _userStates = new Dictionary<int, long>();
                    }
                    _userState = value;
                    _userStates[_userState] = DateTime.Now.Ticks;
                }
            }
        }
        public Dictionary<int, long> UserStates { get { return _userStates; } }

        private Dictionary<String, Object> _stateValues = null;

        public ThreadExecutionState(String id)
        {
            ID = id;
            State = ExecutionState.READY;
        }

        public long UserStateDuration(int s1, int s2, long denominator = 1)
        {
            if(_userStates == null || !_userStates.ContainsKey(s1) || !_userStates.ContainsKey(s2))
            {
                throw new Exception("Invalid user state");
            }

            return (_userStates[s1] - _userStates[s2] ) / denominator;
        }

        public Object GetValue(String key)
        {
            lock (_lock)
            {
                if (_stateValues != null &&_stateValues.ContainsKey(key))
                {
                    return _stateValues[key];
                } else
                {
                    return null;
                }
            }
        }

        public void SetValue(String key, Object val)
        {
            lock (_lock)
            {
                if (_stateValues == null)
                {
                    _stateValues = new Dictionary<String, Object>();
                }
                _stateValues[key] = val;
            }
        }

    }

    
    /// <summary>
    ///  Provide a method with an argument type to be executed synchrnously if the user supplied thread ID is the same (adn the queue size allows)
    ///  or asynchronously if the thread ID id different.  Each method may also be repetedly executed with a delay.
    /// </summary>
    public class ThreadExecutionManager
    {
        public abstract class ThreadExecution
        {
            public String ID { get; internal set; } = null;
            public int Repeat { get; set; } = 1;
            public int Delay { get; set; } = 0;
            public List<Exception> Exceptions { get; internal set; } = new List<Exception>();

            abstract public void Execute(ThreadExecutionState executionState);
        }

        public class ThreadExecution<T> : ThreadExecution
        {
            public Action NoArgumentsAction { get; internal set; } = null;
            public Action<T> SimpleAction { get; internal set; } = null;
            public Action<String, T> CommandAction { get; internal set; } = null;
            public Action<T, ThreadExecutionState> StateAction { get; internal set; } = null;
            public String Command { get; internal set; } = null;
            public T Arguments { get; set; } = default(T);
            
            public ThreadExecution(String id, Action<String, T> action, String commandName, T arguments = default(T))
            {
                ID = id;
                CommandAction = action;
                SimpleAction = null;
                StateAction = null;
                NoArgumentsAction = null;
                Command = commandName;
                Arguments = arguments;
            }

            public ThreadExecution(String id, Action<T> action, T arguments = default(T))
            {
                ID = id;
                SimpleAction = action;
                CommandAction = null;
                StateAction = null;
                NoArgumentsAction = null;
                Command = null;
                Arguments = arguments;
            }

            public ThreadExecution(String id, Action<T> action)
            {
                ID = id;
                SimpleAction = action;
                CommandAction = null;
                StateAction = null;
                NoArgumentsAction = null;
                Command = null;
            }

            public ThreadExecution(String id, Action<T, ThreadExecutionState> action, T arguments = default(T))
            {
                ID = id;
                SimpleAction = null;
                CommandAction = null;
                StateAction = action;
                Command = null;
                Arguments = arguments;
            }

            public ThreadExecution(String id, Action action)
            {
                ID = id;
                SimpleAction = null;
                CommandAction = null;
                StateAction = null;
                NoArgumentsAction = action;
                Command = null;
            }

            override public void Execute(ThreadExecutionState executionState)
            {
                for (int i = 0; i < Repeat; i++)
                {
                    try
                    {
                        CommandAction?.Invoke(Command, Arguments);
                        SimpleAction?.Invoke(Arguments);
                        StateAction?.Invoke(Arguments, executionState);
                        NoArgumentsAction?.Invoke();
                        if (Delay > 0)
                        {
                            Thread.Sleep(Delay);
                        }
                    }
                    catch (Exception e)
                    {
                        Exceptions.Add(e);
                    }
                }
            }
        } //end ThreadExecution class


        class ThreadExecutionQueue : Queue<ThreadExecution>
        {
            String ID { get; set; }
            Thread ThreadHandle = null;
            public ThreadExecutionState ExecutionState;

            public ThreadExecutionQueue(String id)
            {
                ID = id;
                ExecutionState = new ThreadExecutionState(ID);
                ExecutionState.State = ThreadExecutionState.ExecutionState.READY;
            }

            public void Start()
            {
                //creates a new thread
                if (Count > 0)
                {
                    ThreadHandle = new Thread(this.ExecuteQueue);
                    ThreadHandle.Start();
                    ExecutionState.State = ThreadExecutionState.ExecutionState.STARTED;
                    ExecutionState.Exceptions.Clear();
                }
            }

            public void ExecuteQueue()
            {
                ExecutionState.State = ThreadExecutionState.ExecutionState.EXECUTING;
                while (Count > 0)
                {
                    ExecutionState.CurrentQueueSize = Count;

                    ThreadExecution x = Peek();
                    x.Execute(ExecutionState);
                    ExecutionState.Exceptions.AddRange(x.Exceptions);
                    Dequeue();
                }
                ExecutionState.State = ThreadExecutionState.ExecutionState.COMPLETED;
            }

            public void Terminate()
            {
                if (ExecutionState.State == ThreadExecutionState.ExecutionState.READY)
                {
                    throw new Exception("Cannot terminate as not yet started");
                }

                if(ExecutionState.State != ThreadExecutionState.ExecutionState.COMPLETED)
                {
                    ThreadHandle.Abort();
                    ExecutionState.State = ThreadExecutionState.ExecutionState.ABORTED;
                    Clear();
                }
            }
        }

        static private Dictionary<String, ThreadExecutionQueue> ExecutionQueues { get; set; } = new Dictionary<String, ThreadExecutionQueue>();
        static public int MaxQueueSize { get; set; } = 1;

        static public List<String> GetActiveExecutionIDs()
        {
            var list = new List<String>();
            foreach (var k in ExecutionQueues.Keys)
            {
                if (!IsEmpty(k))list.Add(k);
            }
            return list;
        }

        static public bool IsEmpty()
        {
            foreach (var k in ExecutionQueues.Keys)
            {
                if (!IsEmpty(k)) return false;
            }
            return true;
        }

        static public bool IsEmpty(String executionId)
        {
            return (!ExecutionQueues.ContainsKey(executionId) || ExecutionQueues[executionId].Count == 0);
        }

        static public bool CanEnqueue(String executionId)
        {
            return (!ExecutionQueues.ContainsKey(executionId) || ExecutionQueues[executionId].Count < MaxQueueSize);
        }

        static void Enqueue(ThreadExecution x)
        {
            if (!ExecutionQueues.ContainsKey(x.ID))
            {
                ExecutionQueues[x.ID] = new ThreadExecutionQueue(x.ID);
            }

            bool start = ExecutionQueues[x.ID].Count == 0;
            ExecutionQueues[x.ID].Enqueue(x);
            if(start)
            {
                ExecutionQueues[x.ID].Start();
            }
        }

        /// <summary>
        /// Execute method for 'command' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId, Action<String, T> action, String command, T args = default(T))
        {
            return Execute<T>(executionId, 1, 0, action, command, args);
        }

        /// <summary>
        /// Execute method for 'command' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId, int repeat, int delay, Action<String, T> action, String command, T args = default(T))
        {
            if (!CanEnqueue(executionId)) return GetExecutionState(executionId);

            ThreadExecution<T> x = new ThreadExecution<T>(executionId, action, command, args);
            x.Repeat = repeat;
            x.Delay = delay;
            Enqueue(x);
            return GetExecutionState(executionId);
        }

        /// <summary>
        /// Execute method for 'simple' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId,  Action<T> action, T args = default(T))
        {
            return Execute<T>(executionId, 1, 0, action, args);
        }

        /// <summary>
        /// Execute method for 'simple' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId, int repeat, int delay, Action<T> action, T args = default(T))
        {
            if (!CanEnqueue(executionId)) return null;

            ThreadExecution x = new ThreadExecution<T>(executionId, action, args);
            x.Repeat = repeat;
            x.Delay = delay;
            Enqueue(x);

            return GetExecutionState(executionId);
        }


        /// <summary>
        /// Execute method for special case 'simple' action
        /// </summary>
        static public ThreadExecutionState Execute(String executionId, Action<ThreadExecutionState> action)
        {
            return Execute(executionId, 1, 0, action);
        }

        /// <summary>
        /// Execute method for special case 'simple' action
        /// </summary>
        static public ThreadExecutionState Execute(String executionId, int repeat, int delay, Action<ThreadExecutionState> action)
        {
            if (!CanEnqueue(executionId)) return null;

            var x = new ThreadExecution<ThreadExecutionState>(executionId, action);
            x.Repeat = repeat;
            x.Delay = delay;
            Enqueue(x);
            ThreadExecutionState xs = GetExecutionState(executionId);
            x.Arguments = xs; 
            return xs;
        }


        /// <summary>
        /// Execute method for 'state' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId, Action<T, ThreadExecutionState> action, T args = default(T))
        {
            return Execute<T>(executionId, 1, 0, action, args);
        }

        /// <summary>
        /// Execute method for 'state' action
        /// </summary>
        static public ThreadExecutionState Execute<T>(String executionId, int repeat, int delay, Action<T, ThreadExecutionState> action, T args = default(T))
        {
            if (!CanEnqueue(executionId)) return null;

            ThreadExecution x = new ThreadExecution<T>(executionId, action, args);
            x.Repeat = repeat;
            x.Delay = delay;
            Enqueue(x);

            return GetExecutionState(executionId);
        }


        /// <summary>
        /// Execute method for 'no arguments' action
        /// </summary>
        static public ThreadExecutionState Execute(String executionId, Action action)
        {
            return Execute(executionId, 1, 0, action);
        }

        /// <summary>
        /// Execute method for 'no arguments' action
        /// </summary>
        static public ThreadExecutionState Execute(String executionId, int repeat, int delay, Action action)
        {
            if (!CanEnqueue(executionId)) return null;

            ThreadExecution x = new ThreadExecution<Object>(executionId, action);
            x.Repeat = repeat;
            x.Delay = delay;
            Enqueue(x);

            return GetExecutionState(executionId);
        }

        /// <summary>
        /// Get Execution state by execution ID
        /// </summary>
        static public ThreadExecutionState GetExecutionState(String executionId)
        {
            if (ExecutionQueues.ContainsKey(executionId))
            { 
                return ExecutionQueues[executionId].ExecutionState;
            } else
            {
                return null;
            }
        }

        /// <summary>
        /// Get Execution state by execution ID
        /// </summary>
        static public void Terminate(String executionId)
        {
            if (ExecutionQueues.ContainsKey(executionId))
            {
                ExecutionQueues[executionId].Terminate();
            }
        }
    }
}
