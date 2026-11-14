using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    public readonly struct BlendshapeBakeStep
    {
        public BlendshapeBakeStep(int source, int shape, int offset, int vertexCount, float scale)
        { Source = source; Shape = shape; Offset = offset; VertexCount = vertexCount; Scale = scale; }
        public int Source { get; }
        public int Shape { get; }
        public int Offset { get; }
        public int VertexCount { get; }
        public float Scale { get; }
    }

    public sealed class BlendshapeBakePlan
    {
        private readonly struct Source
        {
            public Source(int vertexCount, int firstShape) { VertexCount = vertexCount; FirstShape = firstShape; }
            public int VertexCount { get; }
            public int FirstShape { get; }
        }
        private readonly struct Shape
        {
            public Shape(string name, float weight) { Name = name; Weight = weight; }
            public string Name { get; }
            public float Weight { get; }
        }
        private readonly List<Source> sources = new List<Source>();
        private readonly List<Shape> shapes = new List<Shape>();
        private readonly List<BlendshapeBakeStep> steps = new List<BlendshapeBakeStep>();
        private readonly Dictionary<string, float> weights = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<BlendshapeBakeStep> Steps => steps;
        public int SourceVertices { get; private set; }

        public void Clear() { sources.Clear(); shapes.Clear(); steps.Clear(); weights.Clear(); SourceVertices = 0; }

        public void AddSource(int vertexCount)
        {
            if (vertexCount < 0) throw new ArgumentOutOfRangeException(nameof(vertexCount));
            sources.Add(new Source(vertexCount, shapes.Count));
        }

        public void AddShape(string name, float weight)
        {
            if (sources.Count == 0) throw new InvalidOperationException("No source mesh.");
            shapes.Add(new Shape(name ?? throw new ArgumentNullException(nameof(name)), weight));
        }

        public bool Build(int mergedVertices, float multiplier)
        {
            steps.Clear(); weights.Clear(); SourceVertices = 0;
            for (int s = 0; s < sources.Count; s++)
                for (int j = sources[s].FirstShape; j < End(s); j++)
                    if (!weights.ContainsKey(shapes[j].Name)) weights.Add(shapes[j].Name, shapes[j].Weight);
            int offset = 0;
            for (int s = 0; s < sources.Count; s++)
            {
                int count = sources[s].VertexCount;
                if (count > mergedVertices - offset) { steps.Clear(); return false; }
                for (int j = sources[s].FirstShape; j < End(s); j++)
                {
                    string name = shapes[j].Name;
                    if (OverwrittenLater(s, j)) continue;
                    float weight = weights[name];
                    if (!(weight > 0) || float.IsInfinity(weight)) continue;
                    float scale = multiplier * weight / 100f;
                    if (scale == 0 || float.IsNaN(scale) || float.IsInfinity(scale)) continue;
                    steps.Add(new BlendshapeBakeStep(s, FirstIndex(s, name) - sources[s].FirstShape, offset, count, scale));
                }
                offset += count;
            }
            SourceVertices = offset;
            return true;
        }

        private int End(int source) => source + 1 < sources.Count ? sources[source + 1].FirstShape : shapes.Count;

        private bool OverwrittenLater(int source, int shape)
        {
            for (int j = shape + 1; j < End(source); j++)
                if (string.Equals(shapes[j].Name, shapes[shape].Name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private int FirstIndex(int source, string name)
        {
            for (int j = sources[source].FirstShape; j < End(source); j++)
                if (string.Equals(shapes[j].Name, name, StringComparison.Ordinal)) return j;
            return -1;
        }
    }
}
