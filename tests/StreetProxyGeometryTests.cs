using System;
using System.Linq;
using PersistentCity.Core;
using Xunit;

namespace PersistentCity.Tests
{
    public class StreetProxyGeometryTests
    {
        private static Position P(double x = 0, double y = 0, double z = 0) => new Position(x,y,z);
        private static StreetFrame Frame(Position p = default, double yaw = 0, double scale = 1)
        {
            double a = yaw*Math.PI/180, c = Math.Cos(a)*scale, s = Math.Sin(a)*scale;
            return new StreetFrame(p, P(c,0,-s), P(0,scale,0), P(s,0,c));
        }
        private static StreetDoorFrame Door(int id = 0, Position p = default, double yaw = 0, double scale = 1, bool elevator = false)
            => new StreetDoorFrame(id, Frame(p,yaw,scale),elevator);
        private static StreetEdgePair[] Edges(double error = 0) => new[] {
            new StreetEdgePair("left-jamb", "bottom", P(0,0,0), P(10,0,0)),
            new StreetEdgePair("left-jamb", "top", P(0,2,0), P(10,2,0)),
            new StreetEdgePair("right-jamb", "top", P(2,2,0), P(12+error,2,0)) };

        [Fact] public void UniqueSemanticIdsAndIndependentEdgesSupportCandidateOnly()
        {
            var d = StreetProxyGeometry.Evaluate(new[] { Door(7) }, new[] { Door(7,P(10)) },false,Edges());
            Assert.True(d.Consistent); Assert.True(d.UniqueIds); Assert.True(d.CandidateAvailable);
            Assert.Equal(0,d.EdgeResiduals.Max(),8); Assert.Equal(0,d.DoorPositionResiduals[0],8);
        }
        [Fact] public void OneDoorOriginAndRepeatedCollinearEvidenceCannotValidateAnAperture()
        {
            var a = new[] { Door() };
            Assert.Equal(DoorProbeFailure.InsufficientEdges,StreetProxyGeometry.Evaluate(a,a,false).Failure);
            var edges = new[] { new StreetEdgePair("a","0",P(),P()), new StreetEdgePair("b","1",P(1),P(1)),new StreetEdgePair("c","2",P(2),P(2)) };
            Assert.Equal(DoorProbeFailure.InsufficientEdges,StreetProxyGeometry.Evaluate(a,a,false,edges).Failure);
            Assert.Equal(DoorProbeFailure.Ambiguous,StreetProxyGeometry.Evaluate(a,a,false,new[] { edges[0], edges[0],edges[2] }).Failure);
        }
        [Theory]
        [InlineData(false)] [InlineData(true)]
        public void DuplicateIdsOnEitherSideRefuseEvenWithSameTransforms(bool inside)
        {
            var single = new[] { Door(3) }; var duplicate = new[] { Door(3),Door(3) };
            var d = StreetProxyGeometry.Evaluate(inside ? single : duplicate,inside ? duplicate : single,false);
            Assert.Equal(DoorProbeFailure.Ambiguous,d.Failure); Assert.False(d.CandidateAvailable);
        }
        [Fact] public void MissingIdsNeverUseCardinalityPrimaryOrClosestFallback()
        {
            Assert.Equal(DoorProbeFailure.Missing,StreetProxyGeometry.Evaluate(new[] { Door(1) },new[] { Door(2) },false).Failure);
            Assert.Equal(DoorProbeFailure.Missing,StreetProxyGeometry.Evaluate(new[] { Door(-1) },new[] { Door(-1) },false).Failure);
            Assert.Equal(DoorProbeFailure.Missing,StreetProxyGeometry.Evaluate(Array.Empty<StreetDoorFrame>(),new[] { Door() },false).Failure);
        }
        [Theory] [InlineData(false)] [InlineData(true)]
        public void ElevatorOrMultipleHeightAlwaysRefuses(bool multiple)
        {
            Assert.Equal(DoorProbeFailure.Elevator,StreetProxyGeometry.Evaluate(new[] { Door() },new[] { Door(elevator:!multiple) },multiple).Failure);
        }
        [Theory] [InlineData(1.1)] [InlineData(0)]
        public void ScaleCannotBeNormalizedAway(double scale)
        { Assert.Equal(DoorProbeFailure.ScaleMismatch,StreetProxyGeometry.Evaluate(new[] { Door(scale:scale) },new[] { Door() },false).Failure); }
        [Fact] public void ReflectionAndShearRefuse()
        {
            foreach (var frame in new[] { new StreetFrame(P(),P(-1),P(0,1),P(0,0,1)),new StreetFrame(P(),P(1),P(1),P(0,0,1)) })
                Assert.Equal(DoorProbeFailure.RotationMismatch,StreetProxyGeometry.Evaluate(new[] { new StreetDoorFrame(0,frame) },new[] { Door() },false).Failure);
        }
        [Theory] [InlineData(.099,true)] [InlineData(.101,false)]
        public void EdgeResidualThresholdIsMeasuredInWorldMetres(double error, bool pass)
        {
            var d = StreetProxyGeometry.Evaluate(new[] { Door() },new[] { Door(p:P(10)) },false,Edges(error));
            Assert.Equal(pass,d.Consistent); Assert.Equal(error,d.EdgeResiduals[2],8);
        }
        [Theory] [InlineData(.9,true)] [InlineData(1.1,false)]
        public void SecondDoorRotationMustAgreeWithFirst(double degrees, bool candidate)
        {
            var d = StreetProxyGeometry.Evaluate(new[] { Door(0),Door(1,P(2)) },new[] { Door(0),Door(1,P(2),degrees) },false);
            Assert.Equal(candidate ? DoorProbeFailure.InsufficientEdges : DoorProbeFailure.RotationMismatch,d.Failure);
            Assert.Equal(degrees,d.DoorRotationResidualDegrees[1],6);
        }
        [Fact] public void PairedFramesUseFullRigidRotationAndDetectIndependentOriginError()
        {
            var outside = new[] { Door(1,P(5,0,3),-90),Door(2,P(5,0,5),-90) };
            var inside = new[] { Door(2,P(12,0,0)),Door(1,P(10,0,0)) };
            var d = StreetProxyGeometry.Evaluate(outside,inside,false);
            Assert.All(d.DoorPositionResiduals,n=>Assert.Equal(0,n,8));
            inside[0] = Door(2,P(12.2,0,0));
            Assert.Equal(DoorProbeFailure.ResidualMismatch,StreetProxyGeometry.Evaluate(outside,inside,false).Failure);
        }
        [Fact] public void NonfiniteGeometryAndCapsRefuse()
        {
            Assert.Equal(DoorProbeFailure.Missing,StreetProxyGeometry.Evaluate(new[] { Door(p:P(double.NaN)) },new[] { Door() },false).Failure);
            Assert.Equal(DoorProbeFailure.Cap,StreetProxyGeometry.Evaluate(Enumerable.Range(0,17).Select(i=>Door(i)).ToArray(),new[] { Door() },false).Failure);
            Assert.Equal(DoorProbeFailure.Cap,StreetProxyGeometry.Evaluate(new[] { Door() },new[] { Door() },false,new StreetEdgePair[33]).Failure);
        }
        [Theory] [InlineData(30)] [InlineData(60)] [InlineData(120)]
        public void BoundsCrossingRadiusCannotBeSelectedByCenter(double radius)
        {
            Assert.Equal(StreetRadiusClass.Crossing,new StreetBoundsDistances(P(),P(radius-1,-1,-1),P(radius+1,1,1)).Classify(radius));
            Assert.Equal(StreetRadiusClass.Crossing,new StreetBoundsDistances(P(),P(radius,-1,-1),P(radius+2,1,1)).Classify(radius));
            Assert.Equal(StreetRadiusClass.Inside,new StreetBoundsDistances(P(),P(radius-2),P(radius)).Classify(radius));
            Assert.Equal(StreetRadiusClass.Outside,new StreetBoundsDistances(P(),P(radius+.001),P(radius+3)).Classify(radius));
            Assert.Equal(StreetRadiusClass.Crossing,new StreetBoundsDistances(P(),P(-1000,-1,-1),P(1000,1,1)).Classify(radius));
        }
        [Fact] public void InvalidBoundsAndRadiusNeverMeanInside()
        {
            Assert.Equal(StreetRadiusClass.Invalid,new StreetBoundsDistances(P(),P(2),P(1)).Classify(30));
            Assert.Equal(StreetRadiusClass.Invalid,new StreetBoundsDistances(P(double.NaN),P(),P()).Classify(30));
            Assert.Equal(StreetRadiusClass.Invalid,new StreetBoundsDistances(P(),P(),P()).Classify(double.PositiveInfinity));
        }
        [Fact] public void InventoryCapsAreExactAndLatch()
        {
            var b = new StreetInventoryBudget();
            for (int i = 0; i < StreetInventoryBudget.MaximumNodes; i++) Assert.True(b.VisitNode());
            Assert.False(b.Capped); Assert.False(b.VisitNode()); Assert.False(b.VisitNode()); Assert.True(b.Capped);
            Assert.Equal(StreetInventoryBudget.MaximumNodes,b.Nodes);
            for (int i = 0; i < StreetInventoryBudget.MaximumRows; i++) Assert.True(b.AddRow());
            Assert.False(b.AddRow()); Assert.Equal(StreetInventoryBudget.MaximumRows,b.Rows);
        }
    }
}
