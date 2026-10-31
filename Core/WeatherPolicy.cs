using System;
using System.Globalization;

namespace PersistentCity.Core
{
    public enum WeatherSample { Unknown, Dry, Rain }
    public interface IWeatherSource { WeatherSample ReadWeather(); }

    /// <summary>Lazy frame cache: a boundary asks, never a per-resident update. Failed/missing reads are neutral.</summary>
    public sealed class FrameWeatherSource : IWeatherSource
    {
        private readonly Func<long> frame;
        private readonly Func<bool?> rain;
        private long lastFrame;
        private bool captured;
        private WeatherSample sample;
        public FrameWeatherSource(Func<long> frame, Func<bool?> rain) { this.frame=frame;this.rain=rain; }
        public WeatherSample ReadWeather()
        {
            long current=frame();
            if(captured && current==lastFrame) return sample;
            sample=WeatherSample.Unknown;lastFrame=current;captured=true;
            try { var value=rain(); if(value.HasValue) sample=value.Value ? WeatherSample.Rain : WeatherSample.Dry; }
            catch(Exception) { } // unknown is deliberately neutral, including for this frame's other boundaries
            return sample;
        }
    }

    public sealed class WeatherSettings
    {
        public bool Enabled { get; set; }=true;
        private bool invalidEnabled;
        public void SetEnabled(bool? value) { Enabled=value ?? true;invalidEnabled=!value.HasValue; }
        public double RainOptionalOutingMultiplier { get; set; }=.7;
        public double RainTravelWeightMultiplier { get; set; }=1.3;
        public int QueueRainLeadMinutes { get; set; }=5;
        public void Clamp(Action<string>? log=null)
        {
            if(invalidEnabled) { log?.Invoke("WeatherEnabled defaulted to true");invalidEnabled=false; }
            double C(double v,double min,double max,double fallback,string name)
            {
                double result=double.IsNaN(v) ? fallback : Math.Max(min,Math.Min(max,v));
                if(result!=v) log?.Invoke(name+" clamped to "+result.ToString(CultureInfo.InvariantCulture));
                return result;
            }
            RainOptionalOutingMultiplier=C(RainOptionalOutingMultiplier,0,1,.7,"WeatherRainOptionalOutingMultiplier");
            RainTravelWeightMultiplier=C(RainTravelWeightMultiplier,1,2,1.3,"WeatherRainTravelWeightMultiplier");
            if(QueueRainLeadMinutes==int.MinValue) { log?.Invoke("WeatherQueueRainLeadMinutes defaulted to 5");QueueRainLeadMinutes=5; }
            QueueRainLeadMinutes=(int)C(QueueRainLeadMinutes,DwellPlanner.MinimumQueueLeadMinutes,Math.Min(10,DwellPlanner.MaximumQueueLeadMinutes),5,"WeatherQueueRainLeadMinutes");
        }
    }

    public static class WeatherPolicy
    {
        public static WeatherSample Sample(IWeatherSource? source)
        {
            try { var value=source?.ReadWeather() ?? WeatherSample.Unknown;return (uint)value<=2 ? value : WeatherSample.Unknown; }
            catch(Exception) { return WeatherSample.Unknown; }
        }
        internal static uint AdmissionDraw(string key,int seed) => StableRandom.Finish(StableRandom.Append(StableRandom.Append(StableRandom.Begin(seed),key),":weather-admission"));
        public static bool Admit(uint draw,WeatherSample weather,double rainMultiplier)
            => weather!=WeatherSample.Rain || (draw+.5)/4294967296.0<rainMultiplier;
    }

    /// <summary>One immutable resident capture: version, kind O/Q, weather, result/lead and exact boundary bits.
    /// 15 ASCII characters plus seven JSON field/separator characters = 22 of the E1 allocation's 24.</summary>
    public sealed class WeatherCommitment
    {
        public WeatherCommitment(double boundary,WeatherSample weather,bool admitted)
        { Boundary=boundary;Weather=weather;Value=admitted ? 1 : 0; }
        public WeatherCommitment(double boundary,WeatherSample weather,int queueLead)
        { Boundary=boundary;Weather=weather;Queue=true;Value=queueLead; }
        public double Boundary { get; }
        public WeatherSample Weather { get; }
        public bool Queue { get; }
        public int Value { get; }
        public string ToWire() => "1"+(Queue ? "Q" : "O")+(char)('0'+(int)Weather)+MemoryWire.Alphabet[Value]
            +Convert.ToBase64String(BitConverter.GetBytes(Boundary)).Substring(0,11);
        public static WeatherCommitment Parse(string wire)
        {
            if(wire.Length!=15 || wire[0]!='1' || (wire[1]!='O' && wire[1]!='Q') || wire[2]<'0' || wire[2]>'2') throw new FormatException("Invalid weather capture.");
            int value=MemoryWire.Alphabet.IndexOf(wire[3]);bool queue=wire[1]=='Q';
            if(value<0 || (queue ? value<2 || value>10 : value>1)) throw new FormatException("Invalid weather commitment.");
            if(!queue && wire[2]!='2' && value!=1) throw new FormatException("Nonneutral dry/unknown outing capture.");
            double at=BitConverter.ToDouble(Convert.FromBase64String(wire.Substring(4)+"="),0);
            if(!Position.Finite(at) || at<0 || at>1e9) throw new FormatException("Invalid weather boundary.");
            var capture=queue ? new WeatherCommitment(at,(WeatherSample)(wire[2]-'0'),value) : new WeatherCommitment(at,(WeatherSample)(wire[2]-'0'),value==1);
            if(capture.ToWire()!=wire) throw new FormatException("Noncanonical weather capture.");
            return capture;
        }
    }

    public sealed partial class CitySimulator
    {
        private IWeatherSource? weatherSource;
        private WeatherSettings weatherSettings=new WeatherSettings();
        public void AttachWeather(IWeatherSource source,WeatherSettings? settings=null)
        { weatherSource=source;weatherSettings=settings ?? new WeatherSettings();weatherSettings.Clamp(); }
        internal WeatherSample CaptureWeather(double at) => weatherSettings.Enabled && (int)(at/1440)>=State.WeatherStartsDay
            ? WeatherPolicy.Sample(weatherSource) : WeatherSample.Unknown;
        internal double RainTravelMultiplier(WeatherSample weather) => weather==WeatherSample.Rain ? weatherSettings.RainTravelWeightMultiplier : 1;
        private WeatherCommitment CaptureOuting(CitizenState person,double now,string key)
        {
            var capture=person.W;
            if(capture==null || capture.Queue || capture.Boundary!=now)
            {
                var sample=CaptureWeather(now);
                capture=new WeatherCommitment(now,sample,sample!=WeatherSample.Rain || WeatherPolicy.Admit(WeatherPolicy.AdmissionDraw(key,State.Seed),sample,weatherSettings.RainOptionalOutingMultiplier));
                // Events work on copies; publish the input before the first route yield so interrupted/reloaded events reuse it.
                citizenEvents[person.Identity.Id].Citizen.W=capture;
                person.W=capture;
            }
            return capture;
        }
        public Dwell? QueueAtOpening(CitizenState person,Place place,double now)
        {
            // The scheduled opening already identifies this commitment. No extra hours/dice pass on refresh.
            if(person.Phase!=CitizenPhase.WaitingForOpening || person.PlaceId!=place.Id || person.DestinationId!=place.Id
                || (person.DestinationActivity!=Activity.Shop && person.DestinationActivity!=Activity.Leisure)
                || now>=person.NextEventMinute) return null;
            var capture=person.W;
            if(capture==null || !capture.Queue || capture.Boundary!=person.NextEventMinute)
            {
                // Disabling E1 preserves native rain's existing queue rules, frozen only for this new commitment.
                var sample=WeatherPolicy.Sample(weatherSource);
                int lead=weatherSettings.Enabled && (int)(now/1440)>=State.WeatherStartsDay ? weatherSettings.QueueRainLeadMinutes : (int)DwellPlanner.RainQueueLeadMinutes;
                person.W=capture=new WeatherCommitment(person.NextEventMinute,sample,lead);
            }
            return DwellPlanner.QueueAtOpening(person,place,State.Seed,now,capture.Weather==WeatherSample.Rain,capture.Value);
        }
    }
}
