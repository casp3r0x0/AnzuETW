using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using static AnzuService.LogToWindows;

namespace AnzuService
{
    public partial class Service1 : ServiceBase
    {
        private const string PluginNamespace = "PluginNamespace"; // Change if needed
        private const string PluginClass = "PluginMain";           // Change if needed
        private const string PluginMethod = "Execute";             // Change if needed

        //private static string pluginDirectory = Directory.GetCurrentDirectory();
        private static string pluginDirectory = "C:\\anzu\\plguins\\";

        private Thread workerThread;
        private volatile bool shouldStop = false;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            save("Anzu Service is started developed by @casp3r0x0 hassan al-khafaji");

            // Check if arguments were passed and use the first one as plugin directory.
            

            // Start a worker thread to handle plugin loading and watching.
            workerThread = new Thread(Run);
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        protected override void OnStop()
        {
            save("Anzu Service is stopped developed by @casp3r0x0 hassan al-khafaji");
            shouldStop = true;
            if (workerThread != null && workerThread.IsAlive)
            {
                workerThread.Join(5000); // Wait up to 5 seconds for thread to finish.
            }
        }

        private void Run()
        {
            try
            {
                // Load existing plugins.
                LoadAllPlugins();

                // Set up a FileSystemWatcher to monitor the plugin directory for new DLLs.
                using (FileSystemWatcher watcher = new FileSystemWatcher(pluginDirectory, "*.dll"))
                {
                    watcher.Created += (s, e) =>
                    {
                        // Allow some time for the file to be fully written.
                        Thread.Sleep(1000);
                        LoadPlugin(e.FullPath);
                    };
                    watcher.EnableRaisingEvents = true;

                    // Run until the service is stopped.
                    while (!shouldStop)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                save("Error in worker thread: " + ex.Message);
            }
        }

        static void LoadAllPlugins()
        {
            try
            {
                foreach (string dll in Directory.GetFiles(pluginDirectory, "*.dll"))
                {
                    LoadPlugin(dll);
                }
            }
            catch (Exception ex)
            {
                save("Error loading plugins: " + ex.Message);
            }
        }

        static void LoadPlugin(string path)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                Type type = assembly.GetTypes()
                                    .FirstOrDefault(t => t.Namespace == PluginNamespace && t.Name == PluginClass);
                if (type != null)
                {
                    MethodInfo method = type.GetMethod(PluginMethod, BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        // Start the plugin's method in a new background thread.
                        Thread pluginThread = new Thread(() =>
                        {
                            // Save the original console output.
                            var originalOut = Console.Out;
                            // Redirect output to our custom writer which writes to Windows Event Log.
                            Console.SetOut(new SaveTextWriter(originalOut));
                            try
                            {
                                // Invoke the plugin method.
                                method.Invoke(null, null);
                            }
                            catch (Exception ex)
                            {
                                save("Plugin error: " + ex.Message);
                            }
                            finally
                            {
                                // Restore original console output.
                                Console.SetOut(originalOut);
                            }
                        });
                        pluginThread.IsBackground = true;
                        pluginThread.Start();

                        save($"Started plugin {PluginNamespace}.{PluginClass}.{PluginMethod} from {path}");
                    }
                    else
                    {
                        save($"Method {PluginMethod} not found in {path}");
                    }
                }
                else
                {
                    save($"Class {PluginClass} not found in {path}");
                }
            }
            catch (Exception ex)
            {
                save($"Failed to load plugin {path}: {ex.Message}");
            }
        }

        // Custom TextWriter that buffers console output until a newline is encountered.
        public class SaveTextWriter : TextWriter
        {
            private readonly TextWriter original;
            private readonly StringBuilder buffer = new StringBuilder();

            public SaveTextWriter(TextWriter original)
            {
                this.original = original;
            }

            public override Encoding Encoding => original.Encoding;

            public override void Write(char value)
            {
                buffer.Append(value);
                original.Write(value);

                if (value == '\n')
                {
                    FlushBuffer();
                }
            }

            public override void Write(string value)
            {
                if (value == null)
                    return;

                foreach (char c in value)
                {
                    Write(c);
                }
            }

            public override void WriteLine(string value)
            {
                Write(value + Environment.NewLine);
            }

            public override void Flush()
            {
                FlushBuffer();
                original.Flush();
            }

            private void FlushBuffer()
            {
                if (buffer.Length > 0)
                {
                    // Remove trailing newlines/carriage returns.
                    string message = buffer.ToString().TrimEnd('\r', '\n');
                    if (!string.IsNullOrEmpty(message))
                    {
                        save(message);
                    }
                    buffer.Clear();
                }
            }
        }

        public static void save(string message)
        {
            SaveToWindowsEventLogs(message);
        }
    }
}
