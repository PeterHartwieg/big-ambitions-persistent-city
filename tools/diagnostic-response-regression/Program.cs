using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FoodDemandAuditProbe;

static class Program
{
    static int checks;
    static void Require(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "pc-response-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Require(!DiagnosticResponses.Exists(root, "one"), "Unanswered request should be accepted.");
            Require(DiagnosticResponses.Publish(root, "one", "{\"value\":1}"), "First response publishes.");
            Require(DiagnosticResponses.Exists(root, "one"), "A restarted dispatcher can identify an answered request from disk.");
            Require(!DiagnosticResponses.Publish(root, "one", "{\"value\":2}"), "Duplicate response must not overwrite.");
            Require(File.ReadAllText(Path.Combine(root, "response-one.json")) == "{\"value\":1}", "Original bytes must survive.");
            File.WriteAllText(Path.Combine(root, "response-two.json.tmp"), "interrupted old writer");
            Require(DiagnosticResponses.Publish(root, "two", "{}"), "An abandoned old temp file must not block a response.");
            int winners = 0;
            Parallel.For(0, 16, i => { if (DiagnosticResponses.Publish(root, "race", i.ToString())) System.Threading.Interlocked.Increment(ref winners); });
            Require(winners == 1, "Exactly one concurrent publisher must win.");
            Require(int.TryParse(File.ReadAllText(Path.Combine(root, "response-race.json")), out int winner) && winner >= 0 && winner < 16, "Winner must be complete, never partial.");
            Require(Directory.GetFiles(root, "*.tmp").Select(Path.GetFileName).SequenceEqual(new[] { "response-two.json.tmp" }), "Writers clean only their own temporary files.");
            foreach (string id in new[] { "", "../escape", "line\n", new string('x', 61) })
            {
                bool rejected = false;
                try { DiagnosticResponses.Publish(root, id, "{}"); } catch (ArgumentException) { rejected = true; }
                Require(rejected, "Invalid ID rejected: " + id);
            }
            bool ioFailure = false;
            try { DiagnosticResponses.Publish(Path.Combine(root, "missing"), "one", "{}"); } catch (DirectoryNotFoundException) { ioFailure = true; }
            Require(ioFailure, "Unrelated IO failures must not be mislabeled duplicate success.");
            Console.WriteLine(checks + " diagnostic response invariants passed.");
        }
        finally { Directory.Delete(root, true); }
    }
}
