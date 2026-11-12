using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace PersistentCity.Tests;

public sealed class MarkdownBanTests : IDisposable
{
    readonly string repo = Path.Combine(Path.GetTempPath(), "pc-md-ban-" + Guid.NewGuid().ToString("N"));

    public MarkdownBanTests()
    {
        Directory.CreateDirectory(repo);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "t@example.com");
        Git("config", "user.name", "t");
        Write("docs/old.md", "# Old\n\nGrandfathered text that is long enough to be found as a rename.\n");
        Write("notes.txt", "Plain text that is long enough to be found as a rename source.\n");
        Git("add", "-A");
        Git("commit", "-q", "-m", "base");
        Git("branch", "base");
    }

    public void Dispose()
    {
        try { Directory.Delete(repo, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void TheBranchAddsNoMarkdown()
    {
        string? pr = Environment.GetEnvironmentVariable("PC_MD_BAN_PR");
        var (code, output) = string.IsNullOrEmpty(pr)
            ? Run(Root(), "origin/codex/persistent-city")
            : Run(Root(), "origin/codex/persistent-city", "--pr", pr);
        Assert.True(code == 0, output);
    }

    [Theory]
    [InlineData("new.md")]
    [InlineData("docs/Page.MDX")]
    public void ANewMarkdownFileFails(string path)
    {
        Write(path, "x\n");
        Commit();
        AssertFails(path);
    }

    [Fact]
    public void AnUntrackedMarkdownFileFails()
    {
        Write("loose.md", "x\n");
        AssertFails("loose.md");
    }

    [Fact]
    public void ACopyFails()
    {
        File.Copy(Path.Combine(repo, "docs/old.md"), Path.Combine(repo, "docs/copy.md"));
        Write("docs/old.md", File.ReadAllText(Path.Combine(repo, "docs/old.md")) + "More.\n");
        Commit();
        AssertFails("docs/copy.md");
    }

    [Fact]
    public void ARenameFromPlainTextFails()
    {
        Git("mv", "notes.txt", "notes.md");
        Commit();
        AssertFails("notes.md");
    }

    [Fact]
    public void ARenameOfMarkdownPasses()
    {
        Git("mv", "docs/old.md", "docs/renamed.md");
        Commit();
        AssertPasses();
    }

    [Fact]
    public void EditsAndDeletionsPass()
    {
        Write("docs/old.md", "# Old\n\nEdited.\n");
        File.Delete(Path.Combine(repo, "notes.txt"));
        Write("Core/A.cs", "class A { }\n");
        Commit();
        AssertPasses();
    }

    [Fact]
    public void APullRequestNeedsTheLabel()
    {
        string bin = Path.Combine(repo, ".fake");
        Directory.CreateDirectory(bin);
        string fake = Path.Combine(bin, "gh.py");
        string labels = Path.Combine(bin, "labels");
        File.WriteAllText(fake, "import sys\nif sys.argv[1] == 'pr':\n    sys.stdout.write(open(r'" + labels + "').read())\nelse:\n    print('added\\tdocs/new.md\\t')\n    print('modified\\tdocs/old.md\\t')\n");
        string gh = "python3 '" + fake + "'";

        File.WriteAllText(labels, "bug\n");
        var (code, output) = Run(repo, "base", "--pr", "7", gh);
        Assert.Equal(1, code);
        Assert.StartsWith("markdown ban:", output);
        Assert.Contains("docs/new.md", output);

        File.WriteAllText(labels, "bug\ndocs-approved\n");
        (code, output) = Run(repo, "base", "--pr", "7", gh);
        Assert.True(code == 0, output);
    }

    [Fact]
    public void AMissingBaseSuggestsAFetch()
    {
        var (code, output) = Run(repo, "origin/missing");
        Assert.Equal(1, code);
        Assert.Contains("git fetch origin codex/persistent-city", output);
    }

    void AssertFails(string path)
    {
        var (code, output) = Run(repo, "base");
        Assert.Equal(1, code);
        Assert.StartsWith("markdown ban:", output);
        Assert.Contains(path, output);
    }

    void AssertPasses()
    {
        var (code, output) = Run(repo, "base");
        Assert.True(code == 0, output);
    }

    void Write(string path, string text)
    {
        string full = Path.Combine(repo, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
    }

    void Commit()
    {
        Git("add", "-A");
        Git("commit", "-q", "-m", "change");
    }

    void Git(params string[] args)
    {
        var (code, output) = Exec("git", repo, null, args);
        Assert.True(code == 0, output);
    }

    static (int, string) Run(string root, string @base, string? pr = null, string? number = null, string? gh = null)
    {
        string script = Path.Combine(Root(), "tools", "md-ban.py");
        var args = pr == null
            ? new[] { script, "--root", root, "--base", @base }
            : new[] { script, "--root", root, "--base", @base, pr, number! };
        return Exec("python3", root, gh, args);
    }

    static (int, string) Exec(string file, string cwd, string? gh, string[] args)
    {
        var start = new ProcessStartInfo(file)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) start.ArgumentList.Add(a);
        if (gh != null) start.Environment["PC_MD_BAN_GH"] = gh;
        using var process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }

    static string Root()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root != null && !File.Exists(Path.Combine(root.FullName, "comment-excuses.txt"))) root = root.Parent;
        Assert.NotNull(root);
        return root!.FullName;
    }
}
