using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
namespace CommandHarness
{
    static class NativeIl
    {
        public static void Check(string managed,string candidate)
        {
            AssemblyLoadContext.Default.Resolving += (_,name)=>
            {
                var path=Path.Combine(managed,name.Name+".dll");
                return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;
            };
            var native=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(managed,"ExternalPlugins.dll"));
            var traffic=native.GetType("GleyTrafficSystem.TrafficManager",true)!;
            var physics=traffic.GetMethod("FixedUpdate",BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance)!;
            var code=PatchProcessor.GetOriginalInstructions(physics);
            int force=code.FindIndex(x=>x.operand is MethodInfo m&&m.DeclaringType?.FullName=="UnityEngine.Rigidbody"&&m.Name=="AddForce");
            int rotation=code.FindIndex(x=>x.operand is MethodInfo m&&m.DeclaringType?.FullName=="UnityEngine.Rigidbody"&&m.Name=="MoveRotation");
            if(force<0||rotation<=force) throw new Exception("Native force/rotation absent or reordered");
            // Reviewed native zero-ground count branches past BOTH commands. Keep the native
            // assembly external: validate metadata in place and do not save game IL in the repo.
            var skip=code[force-25];
            if ((skip.opcode!=OpCodes.Brfalse&&skip.opcode!=OpCodes.Brfalse_S)
                ||skip.operand is not Label skipLabel||!code[rotation+1].labels.Contains(skipLabel)
                ||code[force-26].opcode!=code[force-16].opcode||!Equals(code[force-26].operand,code[force-16].operand))
                throw new Exception("Native grounded-count branch no longer skips the complete force/rotation pair");
            var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
            var hooks=assembly.GetType("PersistentCity.Runtime.PhysicalTrafficHooks",true)!;
            var transpiler=hooks.GetMethod("BodyForceTranspiler",BindingFlags.NonPublic|BindingFlags.Static)!;
            var rewritten=((IEnumerable)transpiler.Invoke(null,new object[]{code})!).Cast<CodeInstruction>().ToList();
            if(rewritten.Count(x=>x.operand is MethodInfo m&&m.DeclaringType==hooks&&m.Name=="ApplyBodyForce")!=1
                ||rewritten.Count(x=>x.operand is MethodInfo m&&m.DeclaringType==hooks&&m.Name=="ApplyBodyRotation")!=1)
                throw new Exception("Production transpiler did not replace one native force/rotation pair");
            void Reject(Action<System.Collections.Generic.List<CodeInstruction>> change,string name)
            {
                var invalid=PatchProcessor.GetOriginalInstructions(physics);change(invalid);
                try { _=((IEnumerable)transpiler.Invoke(null,new object[]{invalid})!).Cast<CodeInstruction>().ToList(); }
                catch(InvalidOperationException) { return; }
                throw new Exception("Production transpiler accepted "+name);
            }
            Reject(invalid=>invalid[force-1].opcode=OpCodes.Ldc_I4_1,"different native force mode");
            Reject(invalid=>invalid[force+3]=new CodeInstruction(OpCodes.Ldloc_0),"different rotation receiver slot");
            Reject(invalid=>invalid[force+1]=new CodeInstruction(OpCodes.Br,skipLabel),"branch between paired commands");
            Reject(invalid=>invalid.RemoveAt(rotation),"missing paired native rotation");
            var laterBodyReads=code.Skip(rotation+1).Where(x=>x.operand is MethodInfo m&&m.DeclaringType?.FullName=="UnityEngine.Rigidbody").ToList();
            if(laterBodyReads.Count!=0) throw new Exception("Native FixedUpdate added body reads after deferred pair; review required");
            Console.WriteLine("PASS read-only native IL: grounded skip, exact production pair preflight, four malformed-shape refusals, no later direct Rigidbody reads (no patch/game execution).");
        }
    }
}
