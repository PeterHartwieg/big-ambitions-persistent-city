using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PersistentCity.Tests;

public static class PythonInterpreter
{
    private static readonly Lazy<string[]> current = new(() => Resolve(OperatingSystem.IsWindows(), Locate, Works));

    public static IReadOnlyList<string> Command => current.Value;

    public static string Shell => string.Join(" ", current.Value);

    public static string[] Resolve(bool windows, Func<string, string?> locate, Func<string[], bool> works)
    {
        if (!windows) return new[] { "python3" };
        var candidates = new[] { new[] { "py", "-3" }, new[] { "python" }, new[] { "python3" } };
        var tried = new List<string>();
        foreach (var candidate in candidates)
        {
            string? path = locate(candidate[0]);
            if (path != null && works(candidate)) return candidate;
            tried.Add(candidate[0] + "=" + (path == null ? "missing" : IsStorePlaceholder(path) ? "Store placeholder " + path : "not Python 3 " + path));
        }
        throw new InvalidOperationException("No working Python 3 interpreter (" + string.Join(", ", tried) + ")");
    }

    public static bool IsStorePlaceholder(string path) =>
        path.Replace('/', '\\').IndexOf("\\Microsoft\\WindowsApps\\", StringComparison.OrdinalIgnoreCase) >= 0;

    private static string? Locate(string name)
    {
        string[] extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (string ext in extensions)
            {
                string full;
                try { full = Path.Combine(dir.Trim('"'), name + ext); }
                catch (ArgumentException) { continue; }
                if (File.Exists(full)) return full;
            }
        return null;
    }

    private static bool Works(string[] candidate)
    {
        var start = new ProcessStartInfo(candidate[0]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in candidate.Skip(1)) start.ArgumentList.Add(a);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys; print(sys.version_info[0])");
        try
        {
            using var process = Process.Start(start)!;
            string output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000)) { process.Kill(); return false; }
            return process.ExitCode == 0 && output.Trim() == "3";
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
}
