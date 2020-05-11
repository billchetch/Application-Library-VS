using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.ServiceProcess;


namespace Chetch.Application.Services
{
    public class ServiceLog
    {
        public const int EVENT_LOG = 1;
        public const int CONSOLE = 2;

        private EventLog _log;
        public int Options { get; set; }
        public String Source
        {
            get
            {
                return _log.Source;
            }
            set
            {
                _log.Source = value;
            }
        }
        public String Log
        {
            get
            {
                return _log.Log;
            }
            set
            {
                _log.Log = value;
            }
        }

        public ServiceLog(int logOptions = EVENT_LOG)
        {
            Options = logOptions;
            _log = new EventLog();
        }

        public void WriteEntry(String entry, EventLogEntryType eventType)
        {
            if ((Options & EVENT_LOG) > 0)
            {
                _log.WriteEntry(entry, eventType);
            }

            if ((Options & CONSOLE) > 0)
            {
                Console.WriteLine(eventType + ": " + entry);
            }
        }

        public void WriteError(String entry)
        {
            WriteEntry(entry, EventLogEntryType.Error);
        }
        public void WriteInfo(String entry)
        {
            WriteEntry(entry, EventLogEntryType.Information);
        }
        public void WriteWarning(String entry)
        {
            WriteEntry(entry, EventLogEntryType.Warning);
        }
    }

    abstract public class Service : ServiceBase
    {
        protected ServiceLog Log { get; set; }

        public Service()
        {
            Log = new ServiceLog();
            Log.Options = ServiceLog.EVENT_LOG;
        }
    }
}
