using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public enum FoodAwarenessAlternativeKind { Managed, External }

    [Flags]
    public enum FoodAwarenessSource { Memory=1, Home=2, Routine=4, Origin=8, Discovery=16, Campaign=32 }

    /// <summary>Complete generation identity, never a door/name or recycled ordinal. Implementations do
    /// O(1) lookup per slot. C2a supplies no memory writer or campaign evidence.</summary>
    public interface IFoodAwarenessProvider
    {
        string? Reference(string consumer, int day, int slot);
    }
    public sealed class EmptyFoodAwarenessProvider : IFoodAwarenessProvider
    {
        public static readonly EmptyFoodAwarenessProvider Instance = new EmptyFoodAwarenessProvider();
        public string? Reference(string consumer, int day, int slot) => null;
    }
    /// <summary>Publication version changes only when supported identities/places/menu change (not eligibility alone).
    /// The native adapter's existing bounded discovery ring publishes these changes.</summary>
    public interface IFoodAwarenessRevision { long AwarenessRevision { get; } }

    public sealed class FoodAwarenessSettings
    {
        public double RadiusMetres { get; set; } = 250;
        public int DrawsPerAnchor { get; set; } = 4;
        public double DiscoveryProbability { get; set; } = .02;
        public void Clamp(Action<string>? log=null)
        {
            double ClampValue(double v,double min,double max,double fallback,string key)
            {
                double result=double.IsNaN(v) ? fallback : Math.Max(min,Math.Min(max,v));
                if(result!=v) log?.Invoke(key+" clamped to "+result.ToString(CultureInfo.InvariantCulture));
                return result;
            }
            RadiusMetres=ClampValue(RadiusMetres,25,1000,250,"FoodMarketingNeighbourhoodMetres");
            DiscoveryProbability=ClampValue(DiscoveryProbability,.001,.20,.02,"FoodMarketingDiscoveryDailyProbability");
            if(DrawsPerAnchor==int.MinValue) { DrawsPerAnchor=4; log?.Invoke("FoodMarketingNeighbourhoodDrawsPerAnchor defaulted to 4"); }
            else DrawsPerAnchor=(int)ClampValue(DrawsPerAnchor,0,4,4,"FoodMarketingNeighbourhoodDrawsPerAnchor");
        }
    }
    public sealed class FoodAwarenessCandidate
    {
        public FoodAlternativeKind AlternativeKind { get; set; }
        public string BusinessId { get; set; } = "";
        public FoodAwarenessSource Sources { get; set; }
        public bool Eligible { get; set; }
    }
    /// <summary>Only the one held context carries this image. No resident/day awareness arrays.</summary>
    public sealed class FoodAwarenessImage
    {
        public string CatalogueRevision { get; set; } = "";
        public List<FoodAwarenessCandidate> Union { get; set; } = new List<FoodAwarenessCandidate>(24);
    }
    public sealed class FoodAwarenessSample
    {
        public FoodAwarenessSource Source { get; set; }
        public int Slot { get; set; }
        public string BusinessId { get; set; } = "";
        public string Result { get; set; } = "";
    }
    public sealed class FoodAwarenessDump
    {
        public string ConsumerId { get; set; } = "";
        public int Day { get; set; }
        public FoodAwarenessImage? HeldImage { get; set; }
        public string[] HeldShortlist { get; set; } = Array.Empty<string>();
        public IEnumerable<FoodAwarenessSample> Union => Samples.Where(s=>s.BusinessId.Length>0);
        public IReadOnlyList<FoodAwarenessCandidate> Deduplicated => Image.Union;
        public FoodAwarenessImage Image { get; set; } = new FoodAwarenessImage();
        public List<FoodAwarenessSample> Samples { get; } = new List<FoodAwarenessSample>();
        public string[] Shortlist { get; set; } = Array.Empty<string>();
    }

    /// <summary>Immutable ordinal generation-keyed catalogue, with six spatial scales. Build only on
    /// publication/place change. Sampling touches <=9 buckets/anchor and indexed slots, even in dense cells.
    /// All supported alternatives (including future rival kinds) compete through this same interface.</summary>
    public sealed class FoodAwarenessCatalogue
    {
        public const int MaximumAlternatives=20000, MaximumSourceSlots=32, CoreSourceSlots=24;
        public sealed class Entry
        {
            public Entry(string businessId,string placeId,Position position,FoodAwarenessAlternativeKind kind=FoodAwarenessAlternativeKind.Managed)
            { BusinessId=businessId;PlaceId=placeId;Position=position;Kind=kind; }
            internal Entry(FoodScope scope,Position position) : this(scope.BusinessId,scope.PlaceId,position,scope.AlternativeKind == FoodAlternativeKind.Rival ? FoodAwarenessAlternativeKind.External : FoodAwarenessAlternativeKind.Managed) {Scope=scope;}
            public FoodAwarenessAlternativeKind Kind { get; }
            public string BusinessId { get; }
            public string PlaceId { get; }
            public Position Position { get; }
            internal FoodScope? Scope { get; }
        }
        private readonly Entry[] entries;
        private readonly Dictionary<string,Entry> byId;
        private readonly Dictionary<string,Entry> byPlace;
        private readonly SpatialLevel[] levels;
        private static readonly int[] Scales={32,64,128,256,512,1024};
        public string Revision { get; }
        public int Count => entries.Length;
        private static IEnumerable<Entry> Project(IEnumerable<FoodScope> scopes,Func<string,Position?> position)
        {
            int count=0;
            foreach(var scope in scopes)
            {
                if(++count>MaximumAlternatives) throw new ArgumentException("Awareness catalogue exceeds place bound.");
                var point=position(scope.PlaceId);
                // Yield once even for a missing place: an iterator must not hide a full invalid
                // catalogue scan inside one MoveNext of the bounded collector.
                yield return point.HasValue && point.Value.IsFinite ? new Entry(scope,point.Value) : null!;
            }
        }
        // Synchronous construction is for explicit offline/on-demand callers only. Simulation drives
        // BeginBuild/Continue: no scan, sort, hash or cell-level pass is hidden in a normal event.
        public FoodAwarenessCatalogue(IEnumerable<FoodScope> scopes,Func<string,Position?> position) : this(BuildAll(BeginBuild(scopes,position))) { }
        public FoodAwarenessCatalogue(IEnumerable<Entry> alternatives) : this(BuildAll(alternatives)) { }
        private FoodAwarenessCatalogue(FoodAwarenessCatalogue other)
        {entries=other.entries;byId=other.byId;byPlace=other.byPlace;levels=other.levels;Revision=other.Revision;}
        private FoodAwarenessCatalogue(Entry[] ordered,Dictionary<string,Entry> ids,Dictionary<string,Entry> places,SpatialLevel[] spatial,string revision)
        {entries=ordered;byId=ids;byPlace=places;levels=spatial;Revision=revision;}
        private static FoodAwarenessCatalogue BuildAll(IEnumerable<Entry> alternatives)
        {
            return BuildAll(BeginBuild(alternatives));
        }
        private static FoodAwarenessCatalogue BuildAll(Builder builder)
        {using(builder) {while(!builder.Continue()) {} return builder.Result;}}
        public static Builder BeginBuild(IEnumerable<Entry> alternatives) => new Builder(alternatives);
        internal static Builder BeginSparseBuild(IEnumerable<Entry> alternatives) => new Builder(alternatives,true);
        public static Builder BeginBuild(IEnumerable<FoodScope> scopes,Func<string,Position?> position) => new Builder(Project(scopes,position),true);
        public enum BuildStage { Collect, Copy, Sort, Hash, CountCells, Ranges, Fill, Publish, Complete }
        /// <summary>Private coherent build. Each Continue performs at most 64 input/merge/hash/cell
        /// operations. Array allocations are bounded by 20k; no Array.Sort, ToDictionary or final
        /// whole-list copy occurs. Result is unavailable until the complete six-level image publishes.</summary>
        public sealed class Builder : IDisposable
        {
            public const int MaximumWorkPerUnit=64;
            private readonly IEnumerator<Entry> input;
            private readonly bool skipMissing;
            private readonly List<Entry> collected=new List<Entry>();
            private readonly Dictionary<string,Entry> ids=new Dictionary<string,Entry>(StringComparer.Ordinal);
            private readonly Dictionary<string,Entry> places=new Dictionary<string,Entry>(StringComparer.Ordinal);
            private Entry[] ordered=Array.Empty<Entry>(),scratch=Array.Empty<Entry>();
            private readonly SpatialLevel[] spatial=new SpatialLevel[Scales.Length];
            private int cursor,width=1,mergeStart,left,right,leftEnd,rightEnd,write,level,start,ordinal;
            private bool merging;
            private SHA256? sha;
            private CryptoStream? crypto;
            private BufferedStream? buffer;
            private BinaryWriter? writer;
            private string revision="";
            private Dictionary<(int,int),int>? counts;
            private Dictionary<(int,int),Bucket>? ranges;
            private IEnumerator<KeyValuePair<(int,int),int>>? cells;
            private int[] slots=Array.Empty<int>(),next=Array.Empty<int>();
            private FoodAwarenessCatalogue? result;
            public BuildStage Stage { get; private set; }
            public int LastWorkItems { get; private set; }
            public FoodAwarenessCatalogue Result => result ?? throw new InvalidOperationException("Awareness image is not published.");
            internal Builder(IEnumerable<Entry> alternatives,bool skipMissing=false) {input=alternatives.GetEnumerator();this.skipMissing=skipMissing;}
            public bool Continue()
            {
                LastWorkItems=0;
                switch(Stage)
                {
                    case BuildStage.Collect:
                        while(LastWorkItems<MaximumWorkPerUnit)
                        {
                            if(!input.MoveNext())
                            {
                                input.Dispose();ordered=new Entry[collected.Count];scratch=new Entry[collected.Count];
                                cursor=0;Stage=BuildStage.Copy;break;
                            }
                            LastWorkItems++;var e=input.Current;
                            if(e==null && skipMissing) continue;
                            if(collected.Count==MaximumAlternatives) throw new ArgumentException("Awareness catalogue exceeds place bound.");
                            if(e==null || string.IsNullOrWhiteSpace(e.BusinessId) || e.BusinessId.Length>512
                                || string.IsNullOrWhiteSpace(e.PlaceId) || e.PlaceId.Length>256 || !e.Position.IsFinite || (uint)e.Kind>1)
                                throw new ArgumentException("Invalid awareness alternative.");
                            ids.Add(e.BusinessId,e);places.Add(e.PlaceId,e);collected.Add(e);
                        }
                        break;
                    case BuildStage.Copy:
                        while(cursor<ordered.Length && LastWorkItems<MaximumWorkPerUnit) {ordered[cursor]=collected[cursor];cursor++;LastWorkItems++;}
                        if(cursor==ordered.Length) Stage=BuildStage.Sort;
                        break;
                    case BuildStage.Sort:
                        while(LastWorkItems<MaximumWorkPerUnit && width<ordered.Length)
                        {
                            if(mergeStart>=ordered.Length)
                            {var swap=ordered;ordered=scratch;scratch=swap;width*=2;mergeStart=0;merging=false;continue;}
                            if(!merging)
                            {
                                left=mergeStart;leftEnd=Math.Min(left+width,ordered.Length);right=leftEnd;
                                rightEnd=Math.Min(right+width,ordered.Length);write=mergeStart;merging=true;
                            }
                            if(left<leftEnd && (right>=rightEnd || StringComparer.Ordinal.Compare(ordered[left].BusinessId,ordered[right].BusinessId)<=0))
                                scratch[write++]=ordered[left++];
                            else scratch[write++]=ordered[right++];
                            LastWorkItems++;
                            if(write==rightEnd) {mergeStart=rightEnd;merging=false;}
                        }
                        if(width>=ordered.Length)
                        {
                            sha=SHA256.Create();crypto=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write);
                            buffer=new BufferedStream(crypto,65536);writer=new BinaryWriter(buffer,Encoding.UTF8,true);
                            writer.Write("pc:awareness-catalogue:1");cursor=0;Stage=BuildStage.Hash;
                        }
                        break;
                    case BuildStage.Hash:
                        while(cursor<ordered.Length && LastWorkItems<MaximumWorkPerUnit)
                        {
                            var e=ordered[cursor++];LastWorkItems++;
                            writer!.Write(e.BusinessId);writer.Write(e.PlaceId);writer.Write((int)e.Kind);
                            writer.Write(e.Position.X);writer.Write(e.Position.Y);writer.Write(e.Position.Z);
                        }
                        if(cursor==ordered.Length)
                        {
                            writer!.Flush();buffer!.Flush();crypto!.FlushFinalBlock();revision=Convert.ToBase64String(sha!.Hash!);
                            CloseHash();counts=new Dictionary<(int,int),int>();cursor=0;Stage=BuildStage.CountCells;
                        }
                        break;
                    case BuildStage.CountCells:
                        while(cursor<ordered.Length && LastWorkItems<MaximumWorkPerUnit)
                        {
                            var cell=Cell(ordered[cursor++].Position,Scales[level]);LastWorkItems++;
                            counts!.TryGetValue(cell,out int count);counts[cell]=count+1;
                        }
                        if(cursor==ordered.Length)
                        {
                            ranges=new Dictionary<(int,int),Bucket>(counts!.Count);cells=counts.GetEnumerator();
                            start=ordinal=0;Stage=BuildStage.Ranges;
                        }
                        break;
                    case BuildStage.Ranges:
                        while(LastWorkItems<MaximumWorkPerUnit && cells!.MoveNext())
                        {
                            var pair=cells.Current;LastWorkItems++;
                            ranges!.Add(pair.Key,new Bucket(start,pair.Value,ordinal++));start+=pair.Value;
                        }
                        if(ordinal==counts!.Count)
                        {cells!.Dispose();cells=null;next=new int[counts.Count];slots=new int[ordered.Length];cursor=0;Stage=BuildStage.Fill;}
                        break;
                    case BuildStage.Fill:
                        while(cursor<ordered.Length && LastWorkItems<MaximumWorkPerUnit)
                        {
                            var bucket=ranges![Cell(ordered[cursor].Position,Scales[level])];LastWorkItems++;
                            slots[bucket.Start+next[bucket.Ordinal]++]=cursor++;
                        }
                        if(cursor==ordered.Length)
                        {
                            spatial[level]=new SpatialLevel(ranges!,slots);level++;cursor=0;counts=null;ranges=null;
                            if(level==Scales.Length) Stage=BuildStage.Publish;
                            else {counts=new Dictionary<(int,int),int>();Stage=BuildStage.CountCells;}
                        }
                        break;
                    case BuildStage.Publish:
                        result=new FoodAwarenessCatalogue(ordered,ids,places,spatial,revision);Stage=BuildStage.Complete;break;
                }
                return Stage==BuildStage.Complete;
            }
            private void CloseHash()
            {writer?.Dispose();buffer?.Dispose();crypto?.Dispose();sha?.Dispose();writer=null;buffer=null;crypto=null;sha=null;}
            public void Dispose() {input.Dispose();cells?.Dispose();CloseHash();}
        }
        private readonly struct Bucket
        {
            internal Bucket(int start,int count,int ordinal) {Start=start;Count=count;Ordinal=ordinal;}
            internal int Start { get; }
            internal int Count { get; }
            internal int Ordinal { get; }
        }
        private sealed class SpatialLevel
        {
            internal SpatialLevel(Dictionary<(int,int),Bucket> cells,int[] slots) {Cells=cells;Slots=slots;}
            internal Dictionary<(int,int),Bucket> Cells { get; }
            internal int[] Slots { get; }
        }
        private static (int,int) Cell(Position p,int scale) => ((int)Math.Floor(p.X/scale),(int)Math.Floor(p.Z/scale));
        public bool ContainsPlace(string id) => byPlace.ContainsKey(id);
        internal FoodScope? ScopeAtPlace(string id) => byPlace.TryGetValue(id,out var entry) ? entry.Scope : null;
        public Entry? Find(string id) => byId.TryGetValue(id,out var e) ? e : null;
        private static uint Prefix(int seed,string consumer,int day,FoodAwarenessSource source,bool gate)
        {
            uint h=StableRandom.Append(StableRandom.Begin(seed),gate ? "pc:awareness:v1:gate" : "pc:awareness:v1:slot");
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),consumer.Length);h=StableRandom.Append(StableRandom.Append(h,':'),consumer);
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),day);
            h=StableRandom.AppendDecimal(StableRandom.Append(h,'|'),(int)source);
            return h;
        }
        private uint Key(uint prefix,int slot)
        {
            uint h=StableRandom.AppendDecimal(StableRandom.Append(prefix,'|'),slot);
            return StableRandom.Finish(StableRandom.Append(StableRandom.Append(h,'|'),Revision));
        }
        /// <summary>At most 24 draws here; campaign's eight reserved slots always return unknown in C2a.
        /// Rejection/duplicates consume slots. The optional diagnostic is populated only when requested.</summary>
        public FoodAwarenessImage Sample(int seed,string consumer,int day,Position? home,Position? routine,Position? origin,
            FoodAwarenessSettings settings,Func<Entry,bool> eligible,IFoodAwarenessProvider? memory=null,
            IFoodAwarenessProvider? campaign=null,FoodAwarenessDump? diagnostic=null,string? presentPlace=null)
        {
            var image=new FoodAwarenessImage {CatalogueRevision=Revision};
            void Add(Entry? entry,FoodAwarenessSource source,int slot,bool exposed,string reason)
            {
                bool admitted=entry!=null && exposed && eligible(entry);
                if(diagnostic!=null) diagnostic.Samples.Add(new FoodAwarenessSample {Source=source,Slot=slot,BusinessId=entry?.BusinessId ?? "",Result=admitted ? "eligible" : reason});
                if(entry==null) return;
                foreach(var old in image.Union)
                    if(old.BusinessId==entry.BusinessId) {old.Sources|=source;old.Eligible|=admitted;return;}
                image.Union.Add(new FoodAwarenessCandidate {AlternativeKind=entry.Kind==FoodAwarenessAlternativeKind.External ? FoodAlternativeKind.Rival : FoodAlternativeKind.Player,BusinessId=entry.BusinessId,Sources=source,Eligible=admitted});
            }
            for(int i=0;i<4;i++)
            {
                string? id=(memory ?? EmptyFoodAwarenessProvider.Instance).Reference(consumer,day,i);
                Add(id==null ? null : Find(id),FoodAwarenessSource.Memory,i,true,"missing generation or ineligible");
            }
            void Local(Position? anchor,FoodAwarenessSource source)
            {
                uint prefix=Prefix(seed,consumer,day,source,false);
                int level=0;while(level<Scales.Length-1 && Scales[level]<settings.RadiusMetres) level++;
                var buckets=new Bucket[9];int mass=0,n=0;
                if(anchor.HasValue && anchor.Value.IsFinite)
                {
                    var cell=Cell(anchor.Value,Scales[level]);
                    for(int x=-1;x<=1;x++) for(int z=-1;z<=1;z++)
                        if(levels[level].Cells.TryGetValue((cell.Item1+x,cell.Item2+z),out var bucket)) {buckets[n++]=bucket;mass+=bucket.Count;}
                }
                bool present=source==FoodAwarenessSource.Origin && presentPlace!=null;
                if(present)
                {
                    byPlace.TryGetValue(presentPlace!,out var shop);
                    Add(shop,source,0,true,"present shop missing or ineligible");
                }
                for(int i=present ? 1 : 0;i<Math.Max(0,Math.Min(4,settings.DrawsPerAnchor));i++)
                {
                    Entry? entry=null;
                    if(mass>0)
                    {
                        int draw=(int)(Key(prefix,i)%(uint)mass);
                        for(int b=0;b<n;b++) {if(draw<buckets[b].Count) {entry=entries[levels[level].Slots[buckets[b].Start+draw]];break;}draw-=buckets[b].Count;}
                    }
                    bool inside=entry!=null && Position.Distance(anchor!.Value,entry.Position)<=settings.RadiusMetres;
                    Add(entry,source,i,inside,"outside radius, missing or ineligible");
                }
            }
            Local(home,FoodAwarenessSource.Home);Local(routine,FoodAwarenessSource.Routine);Local(origin,FoodAwarenessSource.Origin);
            uint discoveryGate=Prefix(seed,consumer,day,FoodAwarenessSource.Discovery,true);
            uint discoverySlot=Prefix(seed,consumer,day,FoodAwarenessSource.Discovery,false);
            for(int i=0;i<8;i++)
            {
                bool exposed=(Key(discoveryGate,i)+.5)/4294967296.0<settings.DiscoveryProbability;
                Entry? entry=entries.Length==0 ? null : entries[Key(discoverySlot,i)%(uint)entries.Length];
                // Failed exposure does not create awareness or a saved candidate identity.
                Add(exposed ? entry : null,FoodAwarenessSource.Discovery,i,exposed,"not discovered");
                if(diagnostic!=null && !exposed) diagnostic.Samples[diagnostic.Samples.Count-1].BusinessId=entry?.BusinessId ?? "";
            }
            // Do not ask a provider for native campaign data in C2a. This reserved seam admits zero.
            if(diagnostic!=null) for(int i=0;i<8;i++) diagnostic.Samples.Add(new FoodAwarenessSample {Source=FoodAwarenessSource.Campaign,Slot=i,Result="unknown"});
            image.Union.Sort((a,b)=>StringComparer.Ordinal.Compare(a.BusinessId,b.BusinessId));
            if(diagnostic!=null) diagnostic.Image=image;
            return image;
        }
        public Entry[] Rank(FoodAwarenessImage image,Position origin,ResidentTraits traits,FoodChoiceSettings settings,FoodDrawContext draw,double pace,Func<string,double>? memoryContribution=null,double rivalWeight=1,double weatherTravelMultiplier=1)
        {
            if(image.CatalogueRevision!=Revision || image.Union.Count>MaximumSourceSlots) throw new ArgumentException("Invalid awareness image.");
            var selected=new Entry[8];var ranks=new double[8];int count=0;
            var preference=new FoodChoicePolicy.DistancePreference(traits,settings,weatherTravelMultiplier);
            foreach(var candidate in image.Union)
            {
                if(!candidate.Eligible) continue;
                var entry=Find(candidate.BusinessId);if(entry==null || entry.Kind==FoodAwarenessAlternativeKind.External && rivalWeight==0) continue;
                double rank=FoodDrawContext.Rank(draw.ShopHash(entry.BusinessId),FoodChoicePolicy.ApplyMemory(preference.Weight(Position.Distance(origin,entry.Position)/pace,true),memoryContribution?.Invoke(entry.BusinessId) ?? 0,settings.FoodChoiceWeightFloor)*(entry.Kind==FoodAwarenessAlternativeKind.External ? rivalWeight : 1));
                int at=0;while(at<count && (ranks[at]<rank || ranks[at]==rank && StringComparer.Ordinal.Compare(selected[at].BusinessId,entry.BusinessId)<0)) at++;
                if(at==8) continue;
                for(int j=Math.Min(count,7);j>at;j--) {selected[j]=selected[j-1];ranks[j]=ranks[j-1];}
                selected[at]=entry;ranks[at]=rank;count=Math.Min(count+1,8);
            }
            if(count!=8) Array.Resize(ref selected,count);
            return selected;
        }
    }
}
