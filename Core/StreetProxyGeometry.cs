using System;
using System.Collections.Generic;

namespace PersistentCity.Core
{
    // Diagnostic DTOs only: never part of CityState or a persistence contract.
    public readonly struct StreetFrame
    {
        public StreetFrame(Position origin, Position right, Position up, Position forward)
        { Origin = origin; Right = right; Up = up; Forward = forward; }
        public Position Origin { get; }
        // World matrix columns, including scale/shear. Do not normalize away bad evidence.
        public Position Right { get; }
        public Position Up { get; }
        public Position Forward { get; }
        internal static double Dot(Position a, Position b) => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
        internal static Position Add(Position a, Position b) => new Position(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
        internal static Position Sub(Position a, Position b) => new Position(a.X-b.X, a.Y-b.Y, a.Z-b.Z);
        public Position Rotate(Position p) => new Position(Right.X*p.X + Up.X*p.Y + Forward.X*p.Z,
            Right.Y*p.X + Up.Y*p.Y + Forward.Y*p.Z, Right.Z*p.X + Up.Z*p.Y + Forward.Z*p.Z);
        public Position Unrotate(Position p) => new Position(Dot(p, Right), Dot(p, Up), Dot(p, Forward));
        internal DoorProbeFailure Validate()
        {
            if (!Origin.IsFinite || !Right.IsFinite || !Up.IsFinite || !Forward.IsFinite) return DoorProbeFailure.Missing;
            if (Math.Abs(Dot(Right, Right)-1) > .0001 || Math.Abs(Dot(Up, Up)-1) > .0001
                || Math.Abs(Dot(Forward, Forward)-1) > .0001) return DoorProbeFailure.ScaleMismatch;
            double det = Right.X*(Up.Y*Forward.Z-Up.Z*Forward.Y) - Up.X*(Right.Y*Forward.Z-Right.Z*Forward.Y)
                + Forward.X*(Right.Y*Up.Z-Right.Z*Up.Y);
            return Math.Abs(Dot(Right, Up)) > .0001 || Math.Abs(Dot(Right, Forward)) > .0001
                || Math.Abs(Dot(Up, Forward)) > .0001 || Math.Abs(det-1) > .0001
                ? DoorProbeFailure.RotationMismatch : DoorProbeFailure.None;
        }
    }

    public readonly struct StreetDoorFrame
    {
        public StreetDoorFrame(int id, StreetFrame frame, bool elevator = false)
        { Id = id; Frame = frame; Elevator = elevator; }
        public int Id { get; }
        public StreetFrame Frame { get; }
        public bool Elevator { get; }
    }

    public readonly struct StreetEdgePair
    {
        public StreetEdgePair(string feature, string point, Position exterior, Position interior)
        { Feature = feature; Point = point; Exterior = exterior; Interior = interior; }
        public string Feature { get; }
        public string Point { get; }
        public Position Exterior { get; }
        public Position Interior { get; }
    }

    public readonly struct StreetRigidTransform
    {
        public StreetRigidTransform(StreetFrame exterior, StreetFrame interior)
        {
            Rotation = new StreetFrame(default, interior.Rotate(exterior.Unrotate(new Position(1,0,0))),
                interior.Rotate(exterior.Unrotate(new Position(0,1,0))), interior.Rotate(exterior.Unrotate(new Position(0,0,1))));
            Translation = StreetFrame.Sub(interior.Origin, Rotation.Rotate(exterior.Origin));
        }
        public StreetFrame Rotation { get; }
        public Position Translation { get; }
        public Position Apply(Position point) => StreetFrame.Add(Rotation.Rotate(point), Translation);
    }

    public enum DoorProbeFailure { None, Missing, Ambiguous, Elevator, ScaleMismatch, RotationMismatch, ResidualMismatch, InsufficientEdges, Cap }

    public sealed class StreetDoorDecision
    {
        public DoorProbeFailure Failure { get; internal set; }
        public bool UniqueIds { get; internal set; }
        public bool CandidateAvailable { get; internal set; }
        // A diagnostic consistency result, never permission to draw.
        public bool Consistent => Failure == DoorProbeFailure.None;
        public StreetRigidTransform Candidate { get; internal set; }
        public double[] DoorPositionResiduals { get; internal set; } = Array.Empty<double>();
        public double[] DoorRotationResidualDegrees { get; internal set; } = Array.Empty<double>();
        public double[] EdgeResiduals { get; internal set; } = Array.Empty<double>();
    }

    public static class StreetProxyGeometry
    {
        public const int MaximumDoors = 16, MaximumEdges = 32;
        public const double PositionToleranceMetres = .10, RotationToleranceDegrees = 1;

        public static StreetDoorDecision Evaluate(IReadOnlyList<StreetDoorFrame> exterior, IReadOnlyList<StreetDoorFrame> interior,
            bool multipleHeights, IReadOnlyList<StreetEdgePair>? edges = null)
        {
            var result = new StreetDoorDecision();
            StreetDoorDecision Fail(DoorProbeFailure failure) { result.Failure = failure; return result; }
            if (multipleHeights) return Fail(DoorProbeFailure.Elevator);
            if (exterior == null || interior == null || exterior.Count == 0 || interior.Count == 0) return Fail(DoorProbeFailure.Missing);
            if (exterior.Count > MaximumDoors || interior.Count > MaximumDoors || (edges?.Count ?? 0) > MaximumEdges) return Fail(DoorProbeFailure.Cap);
            foreach (var door in interior) if (door.Elevator) return Fail(DoorProbeFailure.Elevator);
            foreach (var list in new[] { exterior, interior })
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Id < 0) return Fail(DoorProbeFailure.Missing);
                    for (int j = 0; j < i; j++) if (list[j].Id == list[i].Id) return Fail(DoorProbeFailure.Ambiguous);
                    var validation = list[i].Frame.Validate();
                    if (validation != DoorProbeFailure.None) return Fail(validation);
                }
            if (exterior.Count != interior.Count) return Fail(DoorProbeFailure.Missing);
            int Match(int id) { for (int i = 0; i < interior.Count; i++) if (interior[i].Id == id) return i; return -1; }
            foreach (var door in exterior) if (Match(door.Id) < 0) return Fail(DoorProbeFailure.Missing);
            result.UniqueIds = true;
            result.Candidate = new StreetRigidTransform(exterior[0].Frame, interior[Match(exterior[0].Id)].Frame);
            result.CandidateAvailable = true;
            result.DoorPositionResiduals = new double[exterior.Count];
            result.DoorRotationResidualDegrees = new double[exterior.Count];
            for (int i = 0; i < exterior.Count; i++)
            {
                var from = exterior[i].Frame; var to = interior[Match(exterior[i].Id)].Frame;
                double distance = Position.Distance(result.Candidate.Apply(from.Origin), to.Origin);
                double trace = StreetFrame.Dot(result.Candidate.Rotation.Rotate(from.Right), to.Right)
                    + StreetFrame.Dot(result.Candidate.Rotation.Rotate(from.Up), to.Up)
                    + StreetFrame.Dot(result.Candidate.Rotation.Rotate(from.Forward), to.Forward);
                double angle = Math.Acos(Math.Max(-1, Math.Min(1, (trace-1)/2))) * 180 / Math.PI;
                result.DoorPositionResiduals[i] = distance; result.DoorRotationResidualDegrees[i] = angle;
            }
            // Retain every residual even on failure, so the rig can identify the offending pair.
            for (int i = 0; i < exterior.Count; i++)
            {
                if (result.DoorPositionResiduals[i] > PositionToleranceMetres) return Fail(DoorProbeFailure.ResidualMismatch);
                if (result.DoorRotationResidualDegrees[i] > RotationToleranceDegrees) return Fail(DoorProbeFailure.RotationMismatch);
            }
            if (edges == null || edges.Count == 0) return Fail(DoorProbeFailure.InsufficientEdges);
            result.EdgeResiduals = new double[edges.Count];
            bool distinctFeature = false, noncollinear = false;
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (string.IsNullOrWhiteSpace(edge.Feature) || string.IsNullOrWhiteSpace(edge.Point)
                    || !edge.Exterior.IsFinite || !edge.Interior.IsFinite) return Fail(DoorProbeFailure.Missing);
                for (int j = 0; j < i; j++) if (edge.Feature == edges[j].Feature && edge.Point == edges[j].Point) return Fail(DoorProbeFailure.Ambiguous);
                distinctFeature |= edge.Feature != edges[0].Feature;
                result.EdgeResiduals[i] = Position.Distance(result.Candidate.Apply(edge.Exterior), edge.Interior);
                for (int j = 1; j < i; j++)
                {
                    var a = StreetFrame.Sub(edges[j].Exterior, edges[0].Exterior);
                    var b = StreetFrame.Sub(edge.Exterior, edges[0].Exterior);
                    // Require non-degenerate spatial evidence (>1 cm squared cross-product magnitude).
                    double cross2 = StreetFrame.Dot(a,a)*StreetFrame.Dot(b,b) - Math.Pow(StreetFrame.Dot(a,b),2);
                    noncollinear |= cross2 > .0001;
                }
            }
            foreach (double residual in result.EdgeResiduals) if (residual > PositionToleranceMetres) return Fail(DoorProbeFailure.ResidualMismatch);
            return Fail(distinctFeature && noncollinear ? DoorProbeFailure.None : DoorProbeFailure.InsufficientEdges);
        }
    }

    public enum StreetRadiusClass { Invalid, Outside, Inside, Crossing }

    public readonly struct StreetBoundsDistances
    {
        public StreetBoundsDistances(Position anchor, Position minimum, Position maximum)
        {
            Valid = anchor.IsFinite && minimum.IsFinite && maximum.IsFinite && minimum.X <= maximum.X && minimum.Y <= maximum.Y && minimum.Z <= maximum.Z;
            NearestSquared = FarthestSquared = 0;
            if (!Valid) return;
            void Axis(double p, double min, double max, ref double near, ref double far)
            {
                double n = p < min ? min-p : p > max ? p-max : 0, f = Math.Max(Math.Abs(p-min), Math.Abs(p-max));
                near += n*n; far += f*f;
            }
            double nearest = 0, farthest = 0;
            Axis(anchor.X, minimum.X, maximum.X, ref nearest, ref farthest);
            Axis(anchor.Y, minimum.Y, maximum.Y, ref nearest, ref farthest);
            Axis(anchor.Z, minimum.Z, maximum.Z, ref nearest, ref farthest);
            NearestSquared = nearest; FarthestSquared = farthest;
            Valid = Position.Finite(nearest) && Position.Finite(farthest);
        }
        public bool Valid { get; }
        public double NearestSquared { get; }
        public double FarthestSquared { get; }
        public StreetRadiusClass Classify(double radius) => !Valid || !Position.Finite(radius) || radius <= 0 ? StreetRadiusClass.Invalid
            : NearestSquared > radius*radius ? StreetRadiusClass.Outside : FarthestSquared <= radius*radius ? StreetRadiusClass.Inside : StreetRadiusClass.Crossing;
    }

    public sealed class StreetInventoryBudget
    {
        public const int MaximumNodes = 65536, MaximumRows = 1024, MaximumRoots = 4096, MaximumComponents = 64,
            MaximumAncestry = 64, MaximumMaterials = 32, MaximumNameLength = 160, MaximumOwners = 4096,
            MaximumOwnerReferences = 65536, MaximumLods = 8, MaximumOwnersPerRenderer = 32;
        public int Nodes { get; private set; }
        public int Rows { get; private set; }
        public bool Capped { get; private set; }
        public bool VisitNode() { if (Nodes == MaximumNodes) { Capped = true; return false; } Nodes++; return true; }
        public bool AddRow() { if (Rows == MaximumRows) { Capped = true; return false; } Rows++; return true; }
    }
}
