using System;
using System.Diagnostics;
using System.Net;
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
            // Run as administrator to enable ETW session
            using (var session = new TraceEventSession("MyNetworkSession"))
            {
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
                session.EnableProvider("Microsoft-Windows-DNS-Client"); // Also monitor DNS queries

                // Capture TCP connections
                session.Source.Kernel.TcpIpConnect += (data) =>
                {
                    int pid = data.ProcessID;
                    string processName = GetProcessName(pid);

                    // Get remote IP and port
                    string remoteIp = data.daddr.ToString();
                    int remotePort = data.dport;

                    // Attempt to resolve IP to a hostname
                    string domain = ResolveDomain(remoteIp);

                    // Log network event
                    Console.WriteLine($"<NetworkConnection> <ProcessName>{SecurityElement.Escape(processName)}</ProcessName> <PID>{pid}</PID> <Domain>{SecurityElement.Escape(domain)}</Domain> <Port>{remotePort}</Port> </NetworkConnection>");
                };

                // Capture DNS Queries
                session.Source.AllEvents += (TraceEvent data) =>
                {
                    if (data.ProviderName == "Microsoft-Windows-DNS-Client" && (int)data.ID == 1)
                    {
                        int pid = data.ProcessID;
                        string processName = GetProcessName(pid);
                        string queriedDomain = data.PayloadString(0) ?? "Unknown";

                        Console.WriteLine($"<DNSQuery> <ProcessName>{SecurityElement.Escape(processName)}</ProcessName> <PID>{pid}</PID> <DomainQueried>{SecurityElement.Escape(queriedDomain)}</DomainQueried> </DNSQuery>");
                    }
                };

                // Start processing events
                session.Source.Process();
            }
        }


        // Retrieve process name safely
        static string GetProcessName(int pid)
        {
            try
            {
                return Process.GetProcessById(pid).ProcessName;
            }
            catch
            {
                return "Unknown";
            }
        }

        // Reverse resolve an IP to a domain name
        static string ResolveDomain(string ip)
        {
            try
            {
                IPHostEntry hostEntry = Dns.GetHostEntry(ip);
                return hostEntry.HostName;
            }
            catch
            {
                return ip; // Fallback to raw IP
            }
        }
    }
}
