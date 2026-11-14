using System;
using System.Collections.Generic;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal sealed class AppearanceProof
    {
        private struct Element { public AppearanceElementType Type; public string? Variant, Colour; }
        private struct Shape { public string? Name; public float Value; }
        private readonly List<Element> elements = new List<Element>(16);
        private readonly List<Shape> shapes = new List<Shape>(16);
        private CharacterData? data;
        private List<AppearanceElementData>? elementList;
        private List<FacialBlendshape>? shapeList;
        private string? name;
        private int ageInDays;
        private Gender gender;
        private float strength, fatness;
        private Color32 color, eyesColor;

        public void Capture(CharacterData value)
        {
            data = value ?? throw new ArgumentNullException(nameof(value));
            name = value.name; ageInDays = value.ageInDays; gender = value.gender; strength = value.strength; fatness = value.fatness;
            color = value.color; eyesColor = value.eyesColor; elementList = value.elements; shapeList = value.blendshapes;
            elements.Clear(); shapes.Clear();
            if (value.elements != null)
                foreach (var element in value.elements)
                    elements.Add(element == null ? new Element { Type = (AppearanceElementType)(-1) } : new Element { Type = element.type, Variant = element.variantId, Colour = element.colorId });
            if (value.blendshapes != null)
                foreach (var shape in value.blendshapes)
                    shapes.Add(shape == null ? new Shape { Value = float.NaN } : new Shape { Name = shape.name, Value = shape.value });
        }

        public bool Same(CharacterData value)
        {
            if (value == null || !ReferenceEquals(value, data) || !ReferenceEquals(value.elements, elementList) || !ReferenceEquals(value.blendshapes, shapeList)
                || !string.Equals(value.name, name, StringComparison.Ordinal) || value.ageInDays != ageInDays || value.gender != gender
                || !Same(value.strength, strength) || !Same(value.fatness, fatness) || !Same(value.color, color) || !Same(value.eyesColor, eyesColor)) return false;
            if ((value.elements?.Count ?? 0) != elements.Count || (value.blendshapes?.Count ?? 0) != shapes.Count) return false;
            for (int i = 0; i < elements.Count; i++)
            {
                var element = value.elements![i];
                var known = elements[i];
                if (element == null ? (int)known.Type != -1
                    : element.type != known.Type || !string.Equals(element.variantId, known.Variant, StringComparison.Ordinal)
                        || !string.Equals(element.colorId, known.Colour, StringComparison.Ordinal)) return false;
            }
            for (int i = 0; i < shapes.Count; i++)
            {
                var shape = value.blendshapes![i];
                var known = shapes[i];
                if (shape == null ? !float.IsNaN(known.Value)
                    : !string.Equals(shape.name, known.Name, StringComparison.Ordinal) || !Same(shape.value, known.Value)) return false;
            }
            return true;
        }

        private static bool Same(float a, float b) => a.Equals(b);
        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
