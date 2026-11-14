using System;
using System.Collections.Generic;
using Xunit;

namespace PersistentCity.Tests;

public sealed class PythonInterpreterTests
{
    private const string Store = @"C:\Users\t\AppData\Local\Microsoft\WindowsApps\python3.exe";

    private static string[] Resolve(bool windows, Dictionary<string, string> found, params string[] working)
    {
        return PythonInterpreter.Resolve(windows,
            name => found.TryGetValue(name, out var path) ? path : null,
            command => Array.IndexOf(working, string.Join(" ", command)) >= 0);
    }

    [Fact]
    public void WindowsPrefersTheLauncherThenPythonAndSkipsTheStorePlaceholder()
    {
        var all = new Dictionary<string, string> { ["py"] = @"C:\Windows\py.exe", ["python"] = @"C:\Python312\python.exe", ["python3"] = Store };
        Assert.Equal(new[] { "py", "-3" }, Resolve(true, all, "py -3", "python"));
        Assert.Equal(new[] { "python" }, Resolve(true, all, "python"));
        var onlyPlaceholder = new Dictionary<string, string> { ["python3"] = Store };
        var error = Assert.Throws<InvalidOperationException>(() => Resolve(true, onlyPlaceholder));
        Assert.Contains("Store placeholder", error.Message);
        Assert.Equal(new[] { "python3" }, Resolve(true, new Dictionary<string, string> { ["python3"] = @"D:\Py\python3.exe" }, "python3"));
        Assert.Throws<InvalidOperationException>(() => Resolve(true, new Dictionary<string, string>(), "py -3", "python", "python3"));
    }

    [Fact]
    public void ElsewhereItIsPython3()
    {
        Assert.Equal(new[] { "python3" }, Resolve(false, new Dictionary<string, string>()));
        Assert.True(PythonInterpreter.IsStorePlaceholder(Store));
        Assert.False(PythonInterpreter.IsStorePlaceholder(@"C:\Python312\python.exe"));
    }

    [Fact]
    public void TheResolvedInterpreterRunsHere()
    {
        Assert.NotEmpty(PythonInterpreter.Command);
        Assert.False(string.IsNullOrWhiteSpace(PythonInterpreter.Shell));
    }
}
