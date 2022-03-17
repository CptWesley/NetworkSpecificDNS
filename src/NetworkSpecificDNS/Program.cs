using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace NetworkSpecificDNS;

/// <summary>
/// Class holding the program entry point.
/// </summary>
public static class Program
{
    private const string DefaultConfigPath = "config.json";

    private static readonly Regex NameRegex = new Regex(@"^\s*Name\s*: (.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BssidRegex = new Regex(@"^\s*BSSID\s*: (.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
            try
            {
                RunCheck(config, new Dictionary<string, string>());
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            Thread.Sleep(config.Interval);
        }
    }

    private static void RunCheck(Config config, Dictionary<string, string> prevBssids)
    {
        foreach ((string adapter, string bssid) in GetCurrentBSSIDs())
        {
            if (prevBssids.TryGetValue(adapter, out string prevBssid) && prevBssid == bssid)
            {
                continue;
            }

            prevBssids[adapter] = bssid;

            if (!config.Settings.TryGetValue(adapter, out IDictionary<string, IList<string>> adapterSettings))
            {
                continue;
            }

            IList<string> dnsList;
            if (!adapterSettings.TryGetValue(bssid, out dnsList))
            {
                if (!adapterSettings.TryGetValue("default", out dnsList))
                {
                    dnsList = new List<string>();
                }
            }

            if (dnsList.Count == 0)
            {
                RunNetSH($"interface ipv4 set dns name=\"{adapter}\" source=dhcp");
            }
            else
            {
                RunNetSH($"interface ipv4 delete dnsserver name=\"{adapter}\" all");
                for (int i = 0; i < dnsList.Count; i++)
                {
                    RunNetSH($"interface ipv4 add dnsserver name=\"{adapter}\" address={dnsList[i]} index={i + 1}");
                }
            }
        }
    }

    private static string RunNetSH(string argumentLine)
    {
        using Process process = new Process
        {
            StartInfo =
            {
                FileName = "netsh.exe",
                Arguments = argumentLine,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        return output;
    }

    private static IEnumerable<(string Adapter, string Network)> GetCurrentBSSIDs()
    {
        string output = RunNetSH("wlan show interfaces");
        string[] lines = output.Split('\n', '\r');

        string? curAdapter = null;

        foreach (string line in lines)
        {
            Match nameMatch = NameRegex.Match(line);
            if (nameMatch.Success)
            {
                curAdapter = nameMatch.Groups[1].Value;
            }
            else if (curAdapter is not null)
            {
                Match bssidMatch = BssidRegex.Match(line);
                if (bssidMatch.Success)
                {
                    string bssid = bssidMatch.Groups[1].Value;
                    yield return (curAdapter, bssid);
                    curAdapter = null;
                }
            }
        }
    }
}