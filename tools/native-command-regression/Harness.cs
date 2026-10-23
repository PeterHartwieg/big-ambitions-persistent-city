using System;
using System.Collections.Generic;
using PersistentCity.Core;
using UnityEngine;
namespace UnityEngine
{
    struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 zero => new Vector3(0,0,0);
        public static Vector3 up => new Vector3(0,1,0);
        public float sqrMagnitude => x*x+y*y+z*z;
        public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    }
    struct Quaternion
    {
        public float x,y,z,w;
        public Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        public static Quaternion identity => new Quaternion(0,0,0,1);
        private System.Numerics.Quaternion Value => new System.Numerics.Quaternion(x,y,z,w);
        private static Quaternion From(System.Numerics.Quaternion q) => new Quaternion(q.X,q.Y,q.Z,q.W);
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.Value));
        public static Quaternion AngleAxis(float degrees,Vector3 axis) => From(System.Numerics.Quaternion.CreateFromAxisAngle(new System.Numerics.Vector3(axis.x,axis.y,axis.z), degrees*(float)Math.PI/180));
        public static Quaternion operator *(Quaternion a,Quaternion b) => From(a.Value*b.Value);
        public static Vector3 operator *(Quaternion a,Vector3 b) { var v=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(b.x,b.y,b.z),a.Value); return new Vector3(v.X,v.Y,v.Z); }
    }
    enum ForceMode { VelocityChange=2, Force=0 }
    static class Time { public static float fixedTime=1, fixedDeltaTime=.02f; }
    sealed class Transform { public Vector3 position; }
    sealed class Rigidbody
    {
        public Vector3 velocity,angularVelocity,position;
        public Quaternion rotation=Quaternion.identity, pendingRotation=Quaternion.identity;
        public Vector3 pendingForce;
        public int Forces,Rotations;
        public void AddForce(Vector3 force,ForceMode mode) { if(mode!=ForceMode.VelocityChange) throw new Exception("wrong mode"); pendingForce=new Vector3(pendingForce.x+force.x,pendingForce.y+force.y,pendingForce.z+force.z); Forces++; }
        public void MoveRotation(Quaternion target) { pendingRotation=target; Rotations++; }
        public void Integrate() { velocity=new Vector3(velocity.x+pendingForce.x,velocity.y+pendingForce.y,velocity.z+pendingForce.z); pendingForce=Vector3.zero; if(Rotations>0) rotation=pendingRotation; }
    }
}
namespace CommandHarness
{
    sealed partial class PhysicalVehicleBody
    {
        public bool Valid=true, PendingNativeForce,NativeStepPaired,NativeStepPairValid;
        public Quaternion NativeRotation;
        public Vector3 NativeForce;
        public float NativeForceFixedTime;
        public long NativeForceStep;
        public Rigidbody NativeRigidbody=new Rigidbody();
        public Transform FrontMarker=new Transform();
        private readonly Queue<(Position Corner,Position Marker)> reachedCorners=new Queue<(Position,Position)>();
        private int lastReachedNode=-1;
    }
    sealed partial class PhysicalVehicles
    {
        private bool nativeStepActive;
        private long nativeStep;
        internal string recoveryError="";
        public bool Active=true;
        internal Func<PhysicalVehicleBody,PhysicalTrafficCommandLimit>? CommandLimit;
        internal Dictionary<int,PhysicalVehicleBody> bodies=new Dictionary<int,PhysicalVehicleBody>();
        internal Dictionary<Rigidbody,PhysicalVehicleBody> forceBodies=new Dictionary<Rigidbody,PhysicalVehicleBody>();
        internal static Position Point(Vector3 v)=>new Position(v.x,v.y,v.z);
        internal static Vector3 Vector(Position v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        public void Add(PhysicalVehicleBody b) { bodies.Add(bodies.Count,b);forceBodies.Add(b.NativeRigidbody,b); }
    }
    partial class PhysicalTrafficHooks
    {
        static PhysicalVehicles? current;
        public static void Set(PhysicalVehicles v) { current=v; }
        public static bool Begin() { FixedStepPrefix(out bool state); return state; }
        public static void End(bool state) { FixedStepPostfix(state); }
        public static Exception? Fail(Exception error,bool state) => FixedStepFinalizer(error,state);
        public static void Force(Rigidbody body,Vector3 force) { ApplyBodyForce(body,force,ForceMode.VelocityChange); }
        public static void Rotate(Rigidbody body,Quaternion rotation) { ApplyBodyRotation(body,rotation); }
    }
    static class Program
    {
        static int checks;
        static void Check(bool value,string name) { if(!value)throw new Exception(name);checks++; }
        static bool Near(float a,float b)=>Math.Abs(a-b)<.00001;
        static (PhysicalVehicles,PhysicalVehicleBody,Rigidbody) Setup(bool hold=false,double advance=100)
        {
            var b=new PhysicalVehicleBody();var rb=b.NativeRigidbody;
            rb.velocity=new Vector3(8,3,6);rb.angularVelocity=new Vector3(1,2,3);
            rb.position=Vector3.zero;b.FrontMarker.position=new Vector3(0,0,2);
            var v=new PhysicalVehicles();v.Add(b);v.CommandLimit=_=>new PhysicalTrafficCommandLimit(new Position(0,0,1),advance,hold);
            return(v,b,rb);
        }
        static void Pair(PhysicalVehicles v,Rigidbody rb,Vector3 force,Quaternion rotation)
        { Check(v.DeferNativeForce(rb,force,ForceMode.VelocityChange),"owned force deferred"); Check(rb.Forces==0,"defer did not submit force");Check(v.ApplyNativeRotation(rb,rotation),"owned rotation handled"); Check(rb.Forces==0&&rb.Rotations==0,"both commands deferred until native checks finish"); }
        static void Main(string[] args)
        {
            {
                var(v,b,rb)=Setup(); v.BeginNativeFixedStep();v.EndNativeFixedStep();rb.Integrate();
                Check(Near(rb.velocity.x,0)&&Near(rb.velocity.z,0),"zero-ground skip stopped drift");Check(Near(rb.velocity.y,3),"zero-ground preserved vertical velocity");Check(rb.angularVelocity.sqrMagnitude==0,"zero-ground stopped residual spin");Check(v.recoveryError=="","expected no-ground skip not contract failure");Check(rb.Forces==1&&Near(rb.pendingRotation.y,0),"no-ground submitted only braking/rotation hold");
                v.EndNativeFixedStep();Check(rb.Forces==1,"end idempotent");
            }
            {
                var(v,b,rb)=Setup(hold:true);v.BeginNativeFixedStep();Pair(v,rb,new Vector3(2,-4,3),Quaternion.AngleAxis(45,Vector3.up));v.EndNativeFixedStep();rb.Integrate();
                Check(Near(rb.velocity.x,0)&&Near(rb.velocity.z,0),"hold stops all horizontal translation");Check(Near(rb.velocity.y,-1),"hold preserves native vertical force");Check(Near(rb.rotation.y,0),"hold rejects all commanded yaw");Check(rb.angularVelocity.sqrMagnitude==0,"hold rejects residual spin");Check(rb.Forces==1&&rb.Rotations==1,"paired command applied exactly once");Check(v.recoveryError=="","valid hold healthy");
            }
            {
                var(v,b,rb)=Setup(advance:.1);v.BeginNativeFixedStep();Pair(v,rb,new Vector3(-8,-1,-6),Quaternion.identity);v.EndNativeFixedStep();rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0,"stronger native stop preserved");Check(rb.velocity.y==2,"native stop vertical preserved");
            }
            {
                var(v,b,rb)=Setup(advance:.1);v.BeginNativeFixedStep();Pair(v,rb,new Vector3(2,0,3),Quaternion.identity);v.EndNativeFixedStep();rb.Integrate();
                Check(rb.velocity.x*rb.velocity.x+rb.velocity.z*rb.velocity.z<=25.00001,"step budget limits surviving velocity");Check(rb.Forces==1,"postfix does not append braking to valid paired queued force");
            }
            {
                var(v,b,rb)=Setup();v.BeginNativeFixedStep();v.DeferNativeForce(rb,new Vector3(2,-2,3),ForceMode.VelocityChange);v.EndNativeFixedStep();rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0,"missing rotation fail closed");Check(rb.velocity.y==1,"missing rotation vertical retained");Check(v.recoveryError.Contains("unpaired"),"missing rotation surfaces failure");
            }
            {
                var(v,b,rb)=Setup();v.BeginNativeFixedStep();b.Valid=false;v.EndNativeFixedStep();Check(rb.Forces==0&&rb.Rotations==0,"ownership loss leaves foreign body untouched");Check(!v.DeferNativeForce(new Rigidbody(),Vector3.zero,ForceMode.VelocityChange),"foreign force untouched");Check(!v.ApplyNativeRotation(new Rigidbody(),Quaternion.identity),"foreign rotation untouched");
            }
            {
                var(v,b,rb)=Setup();v.BeginNativeFixedStep();Pair(v,rb,new Vector3(2,0,3),Quaternion.identity);
                v.CommandLimit=body=>{body.Valid=false;return new PhysicalTrafficCommandLimit(new Position(0,0,1),100);};
                v.EndNativeFixedStep();
                Check(!b.Valid&&rb.Forces==0&&rb.Rotations==0,"ownership lost inside permission callback prevents final force and rotation submission");
            }
            {
                var(v,b,rb)=Setup();v.BeginNativeFixedStep();v.EndNativeFixedStep(failed:true);rb.Integrate();Check(v.recoveryError.Contains("failed"),"exception guard retains failure");Check(rb.velocity.x==0&&rb.velocity.z==0,"native exception stops drift");
            }
            {
                var(v,b,rb)=Setup();var corner=new Position(0,0,10);b.FrontMarker.position=new Vector3(0,0,9);b.RecordReached(5,corner);b.FrontMarker.position=new Vector3(0,0,11);
                Check(b.TryPeekReached(corner,out var measured)&&measured.Z==9,"peek preserves marker at native evidence time");Check(b.TryPeekReached(corner,out _),"peek does not consume");Check(!b.TryPeekReached(new Position(0,0,12),out _),"peek rejects other endpoint");Check(b.ConsumeReached(corner)&&!b.ConsumeReached(corner),"native evidence consumed once");
            }
            {
                var(v,b,rb)=Setup(hold:true);PhysicalTrafficHooks.Set(v);bool state=PhysicalTrafficHooks.Begin();
                PhysicalTrafficHooks.Force(rb,new Vector3(1,-2,3));Check(rb.Forces==0,"hook defers owned force");
                PhysicalTrafficHooks.Rotate(rb,Quaternion.AngleAxis(30,Vector3.up));PhysicalTrafficHooks.End(state);rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0&&rb.velocity.y==1,"hook paired hold obeys XZ/vertical contract");Check(rb.Forces==1&&rb.Rotations==1,"hook submits paired command once");
                var foreign=new Rigidbody();PhysicalTrafficHooks.Force(foreign,new Vector3(1,2,3));PhysicalTrafficHooks.Rotate(foreign,Quaternion.identity);foreign.Integrate();
                Check(foreign.Forces==1&&foreign.Rotations==1&&foreign.velocity.x==1&&foreign.velocity.y==2&&foreign.velocity.z==3,"hook passes foreign native commands unchanged");
            }
            {
                var(v,b,rb)=Setup();PhysicalTrafficHooks.Set(v);bool state=PhysicalTrafficHooks.Begin();var original=new Exception("native");
                Check(ReferenceEquals(PhysicalTrafficHooks.Fail(original,state),original),"finalizer preserves native exception");rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0&&v.recoveryError.Contains("failed"),"finalizer closes skipped commands");
            }
            {
                var(v,b,rb)=Setup();var corner=new Position(0,0,10);
                v.CommandLimit=body=>new PhysicalTrafficCommandLimit(new Position(0,0,1),100,body.TryPeekReached(corner,out _));
                v.BeginNativeFixedStep();Pair(v,rb,new Vector3(4,-2,5),Quaternion.AngleAxis(45,Vector3.up));
                b.FrontMarker.position=new Vector3(0,0,9.5f);b.RecordReached(6,corner);
                Check(rb.Forces==0&&rb.Rotations==0,"late receipt precedes all native command submission");
                v.EndNativeFixedStep();rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0&&rb.velocity.y==1,"late receipt freezes same-step translation preserving vertical force");
                Check(rb.rotation.y==0&&rb.angularVelocity.sqrMagnitude==0,"late receipt freezes same-step yaw and spin");
                Check(rb.Forces==1&&rb.Rotations==1,"late receipt command submitted once");
                Check(b.TryPeekReached(corner,out var marker)&&marker.Z==9.5,"step guard preserves immutable unconsumed receipt");
            }
            {
                var(v,b,rb)=Setup();v.BeginNativeFixedStep();Pair(v,rb,new Vector3(4,-2,5),Quaternion.AngleAxis(45,Vector3.up));
                v.EndNativeFixedStep(failed:true);rb.Integrate();
                Check(rb.velocity.x==0&&rb.velocity.z==0&&rb.rotation.y==0,"native exception after pair cannot apply queued propulsion/yaw");
            }
            if(args.Length==2) NativeIl.Check(args[0],args[1]);
            Console.WriteLine($"PASS {checks} native command/step checks; actual production method source, deferred physics test doubles (not game evidence).");
        }
    }
}
