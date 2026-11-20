using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class MergedBodyLedger<TBody, TMesh> where TBody : class where TMesh : class
    {
        private readonly HashSet<TBody> owned = new HashSet<TBody>();
        private readonly Dictionary<TBody, TMesh> merged = new Dictionary<TBody, TMesh>();
        private readonly List<TBody> stale = new List<TBody>();
        private readonly Func<TBody, bool> bodyAlive;
        private readonly Func<TMesh, bool> meshAlive;
        private readonly Func<TMesh, int> vertexCount;

        public MergedBodyLedger(Func<TBody, bool> bodyAlive, Func<TMesh, bool> meshAlive, Func<TMesh, int> vertexCount)
        {
            this.bodyAlive = bodyAlive ?? throw new ArgumentNullException(nameof(bodyAlive));
            this.meshAlive = meshAlive ?? throw new ArgumentNullException(nameof(meshAlive));
            this.vertexCount = vertexCount ?? throw new ArgumentNullException(nameof(vertexCount));
        }

        public int Owned => owned.Count;
        public long ReleasedMeshes { get; private set; }
        public long ReleasedVertices { get; private set; }

        public bool Owns(TBody body) => owned.Contains(body);

        public void Own(TBody? body) { if (body != null && bodyAlive(body)) owned.Add(body); }

        public void Merged(TBody body, TMesh mesh) => merged[body] = mesh;

        public void Forget(TBody? body)
        {
            if (body == null) return;
            owned.Remove(body);
            merged.Remove(body);
        }

        public void PruneOwned()
        {
            foreach (var body in owned) if (!bodyAlive(body)) stale.Add(body);
            foreach (var body in stale) owned.Remove(body);
            stale.Clear();
        }

        public void Prune()
        {
            foreach (var pair in merged) if (!bodyAlive(pair.Key) || !meshAlive(pair.Value)) stale.Add(pair.Key);
            foreach (var body in stale) merged.Remove(body);
            stale.Clear();
        }

        public int Unmerged
        {
            get
            {
                Prune();
                int unmerged = 0;
                foreach (var body in owned) if (bodyAlive(body) && !merged.ContainsKey(body)) unmerged++;
                return unmerged;
            }
        }

        public int LiveMeshes(out long vertices)
        {
            Prune();
            vertices = 0;
            foreach (var mesh in merged.Values) vertices += vertexCount(mesh);
            return merged.Count;
        }

        public void CountReleased(TMesh mesh)
        {
            ReleasedMeshes++;
            ReleasedVertices += vertexCount(mesh);
        }
    }
}
