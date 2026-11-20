using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace PersistentCity.Tests;

public sealed class RigHelperScriptTests
{
    [Fact]
    public async Task RigHelperPythonTestsPass()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "comment-excuses.txt"))) root = root.Parent;
        Assert.NotNull(root);
        string helpers = Path.Combine(root!.FullName, "tools", "rig-helpers");
        var python = PythonInterpreter.Command;
        var start = new ProcessStartInfo(python[0])
        {
            WorkingDirectory = helpers, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
        };
        foreach (var a in python.Skip(1).Concat(new[] { "-m", "unittest", "discover", "-s", helpers, "-p", "test_*.py" })) start.ArgumentList.Add(a);
        using var process = Process.Start(start)!;
        var errorRead = process.StandardError.ReadToEndAsync();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await errorRead;
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, output + error);
        Assert.Contains("OK", error);
    }
}
