using System;
using System.Diagnostics;
using System.Security;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace PluginNamespace
{
    public class PluginMain
    {
        // This static method will be invoked by the PluginLoader.
        public static void Execute()
        {
            // This method must be run as an administrator.
            using (var session = new TraceEventSession("MySession"))
            {
                // Enable the kernel provider for process events.
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);

                //Console.WriteLine("Listening for process start events (XML formatted output)...");
                //Console.WriteLine("Press Ctrl+C to exit.");

                // Register a callback for process start events.
                session.Source.Kernel.ProcessStart += (ProcessTraceData data) =>
                {
                    string processName = data.ImageFileName;
                    int processId = data.ProcessID;
                    int parentProcessId = data.ParentID;
                    string parentProcessName = "N/A";

                    // Attempt to retrieve the parent's process name.
                    try
                    {
                        Process parentProcess = Process.GetProcessById(parentProcessId);
                        parentProcessName = parentProcess.ProcessName;
                    }
                    catch (Exception)
                    {
                        // If the parent process is unavailable, keep as "N/A".
                    }

                    // Escape XML special characters.
                    string safeProcessName = SecurityElement.Escape(processName);
                    string safeParentProcessName = SecurityElement.Escape(parentProcessName);
                    string safeCommandLine = SecurityElement.Escape(data.CommandLine);

                    // Output the event details in XML format.
                    Console.WriteLine($"<ProcessEvent>  <ProcessName>{safeProcessName}</ProcessName>   <PID>{processId}</PID>   <ParentProcessName>{safeParentProcessName}</ParentProcessName>  <ParentPID>{parentProcessId}</ParentPID>  <CommandLine>{safeCommandLine}</CommandLine> </ProcessEvent>");
                };

                // Begin processing events; this call blocks until the session is terminated.
                session.Source.Process();
            }
        }
    }
}
