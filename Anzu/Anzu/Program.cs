using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Anzu.LogToWindows;
namespace Anzu
{
    internal class Program
    {
        private const string PluginNamespace = "PluginNamespace"; // Change if needed
        private const string PluginClass = "PluginMain";           // Change if needed
        private const string PluginMethod = "Execute";             // Change if needed

        private static string pluginDirectory = Directory.GetCurrentDirectory();

        static void Main()
        {
            Console.WriteLine("Monitoring directory for new DLLs...");
            LoadAllPlugins();

            // Monitor for new DLLs
            FileSystemWatcher watcher = new FileSystemWatcher(pluginDirectory, "*.dll");
            watcher.Created += (s, e) =>
            {
                // Allow some time for the file to be fully written
                Thread.Sleep(1000);
                LoadPlugin(e.FullPath);
            };
            watcher.EnableRaisingEvents = true;

            // Keep the main thread alive
            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        static void LoadAllPlugins()
        {
            foreach (string dll in Directory.GetFiles(pluginDirectory, "*.dll"))
            {
                LoadPlugin(dll);
            }
        }

        static void LoadPlugin(string path)
        {
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                Type type = assembly.GetTypes().FirstOrDefault(t => t.Namespace == PluginNamespace && t.Name == PluginClass);
                if (type != null)
                {
                    MethodInfo method = type.GetMethod(PluginMethod, BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        // Start the plugin's method in a new thread
                        Thread pluginThread = new Thread(() =>
                        {
                            // Save the original console output
                            var originalOut = Console.Out;
                            // Redirect output to our custom writer which calls save() for every line
                            Console.SetOut(new SaveTextWriter(originalOut));
                            try
                            {
                                // Invoke the plugin method (ETW monitoring that never ends)
                                method.Invoke(null, null);
                            }
                            catch (Exception ex)
                            {
                                save("Plugin error: " + ex.Message);
                            }
                            finally
                            {
                                // In case the method ever ends, restore the original output
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

        // Custom TextWriter that buffers console output until a newline is encountered
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
                    // Remove any trailing newlines or carriage returns for a clean message
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
