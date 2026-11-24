using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public sealed class MergedBodyLedger<TBody, TMesh> where TBody : class where TMesh : class
    {
        private readonly HashSet<TBody> owned = new HashSet<TBody>();
        private readonly Dictionary<TBody, TMesh> merged = new Dictionary<TBody, TMesh>();
        private readonly List<TBody> stale = new List<TBody>();
        private readonly Queue<TBody> deferred = new Queue<TBody>();
        private readonly HashSet<TBody> deferredSet = new HashSet<TBody>();
        private readonly Func<TBody, bool> bodyAlive;
        private readonly Func<TMesh, bool> meshAlive;
        private readonly Func<TMesh, int> vertexCount;
        private readonly Action<TMesh>? destroyOrphan;
        private readonly Func<TBody, TMesh, bool>? stillShown;

        public MergedBodyLedger(Func<TBody, bool> bodyAlive, Func<TMesh, bool> meshAlive, Func<TMesh, int> vertexCount, Action<TMesh>? destroyOrphan = null,
            Func<TBody, TMesh, bool>? stillShown = null)
        {
            this.bodyAlive = bodyAlive ?? throw new ArgumentNullException(nameof(bodyAlive));
            this.meshAlive = meshAlive ?? throw new ArgumentNullException(nameof(meshAlive));
            this.vertexCount = vertexCount ?? throw new ArgumentNullException(nameof(vertexCount));
            this.destroyOrphan = destroyOrphan;
            this.stillShown = stillShown;
        }

        public int Owned => owned.Count;
        public long ReleasedMeshes { get; private set; }
        public long ReleasedVertices { get; private set; }

        public bool Owns(TBody body) => owned.Contains(body);

        public void Own(TBody? body) { if (body != null && bodyAlive(body)) owned.Add(body); }

        public void Merged(TBody body, TMesh mesh) => merged[body] = mesh;

        public TMesh? Forget(TBody? body)
        {
            if (body == null) return null;
            owned.Remove(body);
            return merged.Remove(body, out var mesh) ? mesh : null;
        }

        public void PruneOwned()
        {
            foreach (var body in owned) if (!bodyAlive(body)) stale.Add(body);
            foreach (var body in stale) owned.Remove(body);
            stale.Clear();
        }

        public void Prune() => Sweep(false);

        public void ReleaseOrphan(TBody? body)
        {
            if (body == null || bodyAlive(body)) return;
            owned.Remove(body);
            if (!merged.TryGetValue(body, out var mesh)) return;
            if (!meshAlive(mesh)) { merged.Remove(body); return; }
            if (destroyOrphan == null) return;
            if (stillShown != null && stillShown(body, mesh)) { if (deferredSet.Add(body)) deferred.Enqueue(body); return; }
            merged.Remove(body);
            CountReleased(mesh);
            destroyOrphan(mesh);
        }

        public int Deferred => deferred.Count;

        public void RetryDeferred(int limit)
        {
            for (int i = Math.Min(limit, deferred.Count); i > 0; i--)
            {
                var body = deferred.Dequeue();
                deferredSet.Remove(body);
                if (merged.ContainsKey(body)) ReleaseOrphan(body);
            }
        }

        public void ReleaseOrphans() => Sweep(true);

        private void Sweep(bool release)
        {
            foreach (var pair in merged)
            {
                if (!meshAlive(pair.Value)) stale.Add(pair.Key);
                else if (bodyAlive(pair.Key)) continue;
                else if (destroyOrphan == null || release && (stillShown == null || !stillShown(pair.Key, pair.Value))) stale.Add(pair.Key);
            }
            try
            {
                foreach (var body in stale)
                {
                    merged.Remove(body, out var mesh);
                    if (bodyAlive(body)) continue;
                    owned.Remove(body);
                    if (destroyOrphan == null || mesh == null || !meshAlive(mesh)) continue;
                    CountReleased(mesh);
                    destroyOrphan(mesh);
                }
            }
            finally { stale.Clear(); }
            for (int i = deferred.Count; i > 0; i--)
            {
                var body = deferred.Dequeue();
                if (merged.ContainsKey(body)) deferred.Enqueue(body); else deferredSet.Remove(body);
            }
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

        public int OrphanMeshes(out long vertices)
        {
            Prune();
            int orphans = 0;
            vertices = 0;
            foreach (var pair in merged)
                if (!bodyAlive(pair.Key)) { orphans++; vertices += vertexCount(pair.Value); }
            return orphans;
        }

        public void CountMergedByOwner(Func<TBody, int> owner, int[] meshes, long[] vertices)
        {
            Prune();
            Array.Clear(meshes, 0, meshes.Length);
            Array.Clear(vertices, 0, vertices.Length);
            foreach (var pair in merged)
            {
                int kind = owner(pair.Key);
                meshes[kind]++;
                vertices[kind] += vertexCount(pair.Value);
            }
        }

        public void CountReleased(TMesh mesh)
        {
            ReleasedMeshes++;
            ReleasedVertices += vertexCount(mesh);
        }
    }
}
