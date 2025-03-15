using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Anzu
{
    internal class LogToWindows
    {
        public static void SaveToWindowsEventLogs(string message)
        {
            string source = "AnzuETW";
            string logName = "AnzuETW";

            // Check if the event source exists. If not, create it.
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, logName);
                Console.WriteLine($"Created event source '{source}'. Please restart the application to complete the process.");
                return;
            }

            // Write an entry to the event log using the specified source.
            EventLog.WriteEntry(source, message, EventLogEntryType.Information);

        }
    }
}
