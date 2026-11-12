using System.Linq;

namespace PersistentCity.Runtime
{
    public sealed partial class PopulationRuntime
    {
        public object? Weather(string residentId)
        {
            var state=simulation?.State;if(state==null) return null;
            var person=simulation!.FindCitizen(residentId);if(person==null) return null;
            return new {state.Minute,state.WeatherStartsDay,ResidentId=residentId,
                OutingOrQueue=person.W,UnknownMeaning="unknown weather is neutral",
                Consumers=state.Food?.Residents.Where(m=>m.HostId==residentId).Select(m=>new {m.ResidentId,
                    CapturedWeather=m.Memory.Weather,m.ContextAttempts,Held=m.Decision==null ? null : new {
                        m.Decision.ContextKey,m.Decision.OriginAt,m.Decision.Weather,m.Decision.WeatherTravelMultiplier,m.Decision.QuotesDone}}).ToArray()};
        }
    }
}
