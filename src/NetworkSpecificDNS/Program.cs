using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Management;
using System.Threading;
using Newtonsoft.Json;

namespace NetworkSpecificDNS;

/// <summary>
/// Class holding the program entry point.
/// </summary>
public static class Program
{
    private const string DefaultConfigPath = "config.json";

    /// <summary>
    /// Program entry point.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static void Main(string[] args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.Length > 1)
        {
            Console.Error.WriteLine("Too many arguments provided. Please provide only a single path to a settings file.");
            Environment.Exit(11);
        }

        string path = args.Length == 0 ? DefaultConfigPath : args[0];
        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Couldn't find config ile '{fullPath}'.");
            Environment.Exit(12);
        }

        string content = File.ReadAllText(fullPath);
        Config? config = JsonConvert.DeserializeObject<Config>(content);

        if (config is null)
        {
            Console.Error.WriteLine($"Malformed config file '{fullPath}'.");
            Environment.Exit(13);
        }
        else
        {
            RunCheckLoop(config);
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "Wanted behaviour is catching all exceptions.")]
    private static void RunCheckLoop(Config config)
    {
        while (true)
        {
            //try
            {
                RunCheck(config);
            }
            //catch (Exception ex)
            {
                //Console.Error.WriteLine(ex);
            }

            Thread.Sleep(config.Interval);
        }
    }

    private static void RunCheck(Config config)
    {
        string query = "SELECT * FROM MSNDis_80211_BSSIList";
        ManagementObjectSearcher searcher = new ManagementObjectSearcher("root/WMI", query);
        ManagementObjectCollection moc = searcher.Get();
        ManagementObjectCollection.ManagementObjectEnumerator moe = moc.GetEnumerator();
        moe.MoveNext();
        ManagementBaseObject[] objarr = (ManagementBaseObject[])moe.Current.Properties["Ndis80211BSSIList"].Value;
        foreach (ManagementBaseObject obj in objarr)
        {
            uint u_rssi = (uint)obj["Ndis80211Rssi"];
            int rssi = (int)u_rssi;
            uint u_ssid = (uint)obj["Ndis80211Ssid"];
            int ssid = (int)u_ssid;
            Console.WriteLine("RSSI: " + rssi + " SSID: " + ssid);
            // .... then get other properties such as "Ndis80211MacAddress" and "Ndis80211Ssid"
        }
    }
}