using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PersistentCity.Core
{
    public enum FoodAlternativeKind { Managed, External }

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
    /// <summary>Publication version changes only when supported identities/places/eligibility change.
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
            public Entry(string businessId,string placeId,Position position,FoodAlternativeKind kind=FoodAlternativeKind.Managed)
            { BusinessId=businessId;PlaceId=placeId;Position=position;Kind=kind; }
            internal Entry(FoodScope scope,Position position) : this(scope.BusinessId,scope.PlaceId,position) {Scope=scope;}
            public FoodAlternativeKind Kind { get; }
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
            foreach(var scope in scopes)
            {
                var point=position(scope.PlaceId);
                if(point.HasValue && point.Value.IsFinite) yield return new Entry(scope,point.Value);
            }
        }
        public FoodAwarenessCatalogue(IEnumerable<FoodScope> scopes,Func<string,Position?> position) : this(Project(scopes,position)) { }
        public FoodAwarenessCatalogue(IEnumerable<Entry> alternatives)
        {
            var list=new List<Entry>();
            foreach(var entry in alternatives)
            {
                if(list.Count==MaximumAlternatives) throw new ArgumentException("Awareness catalogue exceeds place bound.");
                if(entry==null || string.IsNullOrWhiteSpace(entry.BusinessId) || entry.BusinessId.Length>512
                    || string.IsNullOrWhiteSpace(entry.PlaceId) || entry.PlaceId.Length>256 || !entry.Position.IsFinite
                    || (uint)entry.Kind>1) throw new ArgumentException("Invalid awareness alternative.");
                list.Add(entry);
            }
            entries=list.OrderBy(e=>e.BusinessId,StringComparer.Ordinal).ToArray();
            byId=entries.ToDictionary(e=>e.BusinessId,StringComparer.Ordinal);
            byPlace=entries.ToDictionary(e=>e.PlaceId,StringComparer.Ordinal);
            // Stream canonical binary metadata through a bounded buffer instead of formatting and
            // retaining a catalogue-sized text/string/UTF-8 triplicate. BinaryWriter is little-endian;
            // strings use its explicit UTF-8 length prefix, coordinates retain exact binary64 bits.
            using(var sha=SHA256.Create())
            using(var crypto=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write))
            using(var buffer=new BufferedStream(crypto,65536))
            using(var writer=new BinaryWriter(buffer,Encoding.UTF8,true))
            {
                writer.Write("pc:awareness-catalogue:1");
                foreach(var e in entries)
                {
                    writer.Write(e.BusinessId);writer.Write(e.PlaceId);writer.Write((int)e.Kind);
                    writer.Write(e.Position.X);writer.Write(e.Position.Y);writer.Write(e.Position.Z);
                }
                writer.Flush();buffer.Flush();crypto.FlushFinalBlock();
                Revision=Convert.ToBase64String(sha.Hash!);
            }
            levels=new SpatialLevel[Scales.Length];
            for(int level=0;level<Scales.Length;level++)
            {
                var counts=new Dictionary<(int,int),int>();
                for(int i=0;i<entries.Length;i++)
                {
                    var cell=Cell(entries[i].Position,Scales[level]);
                    counts.TryGetValue(cell,out int count);counts[cell]=count+1;
                }
                // One ordinal array per scale, rather than one array/object for each dense or sparse
                // cell. Ranges still expose bucket mass and direct indexed slots, never a cell walk.
                var ranges=new Dictionary<(int,int),Bucket>(counts.Count);
                int start=0,ordinal=0;
                foreach(var pair in counts)
                {
                    ranges.Add(pair.Key,new Bucket(start,pair.Value,ordinal++));start+=pair.Value;
                }
                var next=new int[counts.Count];var slots=new int[entries.Length];
                for(int i=0;i<entries.Length;i++)
                {
                    var bucket=ranges[Cell(entries[i].Position,Scales[level])];
                    slots[bucket.Start+next[bucket.Ordinal]++]=i;
                }
                levels[level]=new SpatialLevel(ranges,slots);
            }
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
            IFoodAwarenessProvider? campaign=null,FoodAwarenessDump? diagnostic=null)
        {
            var image=new FoodAwarenessImage {CatalogueRevision=Revision};
            void Add(Entry? entry,FoodAwarenessSource source,int slot,bool exposed,string reason)
            {
                bool admitted=entry!=null && exposed && eligible(entry);
                if(diagnostic!=null) diagnostic.Samples.Add(new FoodAwarenessSample {Source=source,Slot=slot,BusinessId=entry?.BusinessId ?? "",Result=admitted ? "eligible" : reason});
                if(entry==null) return;
                foreach(var old in image.Union)
                    if(old.BusinessId==entry.BusinessId) {old.Sources|=source;old.Eligible|=admitted;return;}
                image.Union.Add(new FoodAwarenessCandidate {BusinessId=entry.BusinessId,Sources=source,Eligible=admitted});
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
                for(int i=0;i<Math.Max(0,Math.Min(4,settings.DrawsPerAnchor));i++)
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
        public Entry[] Rank(FoodAwarenessImage image,Position origin,ResidentTraits traits,FoodChoiceSettings settings,FoodDrawContext draw,double pace)
        {
            if(image.CatalogueRevision!=Revision || image.Union.Count>MaximumSourceSlots) throw new ArgumentException("Invalid awareness image.");
            var selected=new Entry[8];var ranks=new double[8];int count=0;
            var preference=new FoodChoicePolicy.DistancePreference(traits,settings);
            foreach(var candidate in image.Union)
            {
                if(!candidate.Eligible) continue;
                var entry=Find(candidate.BusinessId);if(entry==null) continue;
                double rank=FoodDrawContext.Rank(draw.ShopHash(entry.BusinessId),preference.Weight(Position.Distance(origin,entry.Position)/pace,true));
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
