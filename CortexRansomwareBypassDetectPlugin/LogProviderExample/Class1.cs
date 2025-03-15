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
            Console.WriteLine("Monitoring UNC path requests containing 'users'... (Press Ctrl+C to exit)");

            // Check for administrative privileges.
            if ((bool)!TraceEventSession.IsElevated())
            {
                Console.WriteLine("This program must be run as Administrator.");
                return;
            }

            try
            {
                // Create an ETW session with a unique name.
                using (var session = new TraceEventSession($"UNCMonitorSession_{Guid.NewGuid()}"))
                {
                    // Ensure the session is cleaned up on exit.
                    Console.CancelKeyPress += (sender, e) => session.Dispose();

                    // Enable kernel file I/O events.
                    session.EnableKernelProvider(
                        KernelTraceEventParser.Keywords.FileIOInit |
                        KernelTraceEventParser.Keywords.FileIO);

                    // Subscribe to file creation events.
                    session.Source.Kernel.FileIOCreate += data =>
                    {
                        try
                        {
                            ProcessFilePath(data.FileName, data.ProcessID, data.ProcessName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Error] FileIOCreate event failed: {ex.Message}");
                        }
                    };

                    // Subscribe to read events.
                    session.Source.Kernel.FileIORead += data =>
                    {
                        try
                        {
                            ProcessFilePath(data.FileName, data.ProcessID, data.ProcessName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Error] FileIORead event failed: {ex.Message}");
                        }
                    };

                    // Subscribe to write events.
                    session.Source.Kernel.FileIOWrite += data =>
                    {
                        try
                        {
                            ProcessFilePath(data.FileName, data.ProcessID, data.ProcessName);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Error] FileIOWrite event failed: {ex.Message}");
                        }
                    };

                    // This call blocks while events are processed.
                    session.Source.Process();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Critical Error] ETW session encountered an issue: {ex.Message}");
            }
        }
        /// <summary>
        /// Processes the file path event, logs it as XML, and attempts to terminate the process safely.
        /// </summary>
        static void ProcessFilePath(string fileName, int processId, string processName)
        {
            try
            {
                if (!string.IsNullOrEmpty(fileName))
                {
                    // Check if the file path is a UNC path.
                    if (fileName.StartsWith(@"\\") || fileName.StartsWith(@"\\?\UNC\"))
                    {
                        // Check if the path contains "users" (case-insensitive).
                        if (fileName.IndexOf("users", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Escape XML special characters.
                            string escapedPath = SecurityElement.Escape(fileName);
                            string escapedProcessName = SecurityElement.Escape(processName);

                            // Format and print the event as an XML snippet.
                            string xml = $"<Event><Alert>Cortex EDR Ransomware Protection Bypass Detected</Alert><Path>{escapedPath}</Path><ProcessName>{escapedProcessName}</ProcessName><PID>{processId}</PID></Event>";
                            Console.WriteLine(xml);

                            // Attempt to kill the process.
                            KillProcess(processId, processName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to process file path event: {ex.Message}");
            }
        }

        /// <summary>
        /// Safely attempts to kill the process.
        /// </summary>
        static void KillProcess(int processId, string processName)
        {
            try
            {
                Process process = Process.GetProcessById(processId);
                if (process != null)
                {
                    Console.WriteLine($"[!] Terminating process: {processName} (PID: {processId})");
                    process.Kill();
                }
            }
            catch (ArgumentException)
            {
                Console.WriteLine($"[Warning] Process {processName} (PID: {processId}) does not exist.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Failed to terminate process {processName} (PID: {processId}): {ex.Message}");
            }
        }
    }
}
