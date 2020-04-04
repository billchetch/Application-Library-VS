using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Chetch.Application
{
    /*
     * For managing threads
     * 
     */

    public class ThreadExecutionManager<T>
    {
        class ThreadExecution
        {
            public Action<ThreadExecution> OnStart { get; set; } = null;
            public Action<ThreadExecution, List<Exception>> OnComplete { get; set; } = null;
            public Action<ThreadExecution> OnRepeat { get; set; } = null;

            public String ID { get; internal set; } = null;
            public Action<T> SimpleAction { get; internal set; } = null;
            public Action<String, T> CommandAction { get; internal set; } = null;
            public String Command { get; internal set; } = null;
            public T Arguments { get; internal set; } = default(T);
            public int Repeat { get; set; } = 1;
            public int Delay { get; set; } = 0;

            public ThreadExecution(String id, Action<String, T> action, String commandName, T arguments = default(T))
            {
                ID = id;
                CommandAction = action;
                SimpleAction = null;
                Command = commandName;
                Arguments = arguments;
            }

            public ThreadExecution(String id, Action<T> action, T arguments = default(T))
            {
                ID = id;
                SimpleAction = action;
                CommandAction = null;
                Command = null;
                Arguments = arguments;
            }

            public void Execute()
            {

                OnStart?.Invoke(this);

                List<Exception> exceptions = new List<Exception>();
                for (int i = 0; i < Repeat; i++)
                {
                    try
                    {
                        CommandAction?.Invoke(Command, Arguments);
                        SimpleAction?.Invoke(Arguments);
                        if (Delay > 0)
                        {
                            Thread.Sleep(Delay);
                        }
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }

                OnComplete?.Invoke(this, exceptions);
            }
        } //end ThreadExecution class


        static private Dictionary<String, Queue<ThreadExecution>> ExecutionQueues { get; set; } = new Dictionary<string, Queue<ThreadExecution>>();
        static public int MaxQueueSize { get; set; } = 1;


        static void Started(ThreadExecution x)
        {
            //Added in case of future need
        }

        static void Completed(ThreadExecution x, List<Exception> exceptions)
        {
            Dequeue(x.ID);

            ThreadExecution next = Peek(x.ID);
            if (next != null)
            {
                Execute(next);
            }
        }

        static ThreadExecution Peek(String executionId)
        {
            if (ExecutionQueues.ContainsKey(executionId) && ExecutionQueues[executionId].Count > 0)
            {
                return ExecutionQueues[executionId].Peek();
            }
            else
            {
                return null;
            }
        }
        static ThreadExecution Dequeue(String executionId)
        {
            if (ExecutionQueues.ContainsKey(executionId) && ExecutionQueues[executionId].Count > 0)
            {
                ThreadExecution x = ExecutionQueues[executionId].Dequeue();
                return x;
            }
            else
            {
                return null;
            }
        }
        static void Enqueue(String executionId, ThreadExecution x)
        {
            if (!CanEnqueue(executionId)) throw new Exception("Not can enqueue " + executionId);
            if (!ExecutionQueues.ContainsKey(executionId))
            {
                ExecutionQueues[executionId] = new Queue<ThreadExecution>();
            }
            ExecutionQueues[executionId].Enqueue(x);
        }

        static public bool CanEnqueue(String executionId)
        {
            return (!ExecutionQueues.ContainsKey(executionId) || ExecutionQueues[executionId].Count < MaxQueueSize);
        }

        static public bool Execute(String executionId, Action<String, T> action, String command, T args = default(T))
        {
            return Execute(executionId, 1, 0, action, command, args);
        }

        static public bool Execute(String executionId, int repeat, int delay, Action<String, T> action, String command, T args = default(T))
        {
            if (!CanEnqueue(executionId)) return false;

            ThreadExecution x = new ThreadExecution(executionId, action, command, args);
            x.OnStart = Started;
            x.OnComplete = Completed;
            x.Repeat = repeat;
            x.Delay = delay;

            Enqueue(x.ID, x);
            if (Peek(x.ID) == x)
            {
                Execute(x);
            }
            return true;
        }

        static private void Execute(ThreadExecution x)
        {
            Thread t = new Thread(x.Execute);
            t.Start();
        }
    }
}
