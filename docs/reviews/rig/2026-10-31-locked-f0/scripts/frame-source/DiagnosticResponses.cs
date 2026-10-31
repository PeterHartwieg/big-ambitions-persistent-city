using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FoodDemandAuditProbe
{
    // Responses are immutable acknowledgements. A helper restarted in the same
    // profile must not execute an already answered request again.
    internal static class DiagnosticResponses
    {
        // The rig has one helper/dispatcher. Serialize its publishers because
        // File.Move's destination check is not an atomic no-replace guarantee
        // on every supported runtime. This is not a cross-process lock.
        private static readonly object PublishGate = new object();
        private static string ResponsePath(string root, string id)
        {
            if (!Regex.IsMatch(id, "\\A[a-zA-Z0-9_-]{1,60}\\z"))
                throw new ArgumentException("Invalid diagnostic request ID.", nameof(id));
            return Path.Combine(root, "response-" + id + ".json");
        }

        internal static bool Exists(string root, string id) => File.Exists(ResponsePath(root, id));

        internal static bool Publish(string root, string id, string json)
        {
            lock (PublishGate) return PublishLocked(root, id, json);
        }

        private static bool PublishLocked(string root, string id, string json)
        {
            string path = ResponsePath(root, id);
            if (File.Exists(path)) return false;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json);
                try { File.Move(temporary, path); }
                catch (IOException) when (File.Exists(path)) { return false; }
                return true;
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
