using System;
using PersistentCity.Core;

namespace PersistentCity.Runtime
{
    // Runtime presentation only. Deliberately absent from CityState and every save DTO.
    internal readonly struct TravelVisibility
    {
        public readonly bool FastForward, Accelerated, Loading, Inside, Parking, TimeMachine;
        public TravelVisibility(bool fastForward, bool accelerated, bool loading, bool inside, bool parking, bool timeMachine)
        { FastForward = fastForward; Accelerated = accelerated; Loading = loading; Inside = inside; Parking = parking; TimeMachine = timeMachine; }
        public bool OtherHidden => FastForward || Accelerated || Loading || Parking || TimeMachine;
        public bool Hidden(bool interiorActive) => OtherHidden || Inside && !interiorActive;
        public bool StopPedestrian(bool interiorActive, bool paused) => Loading || Inside && !interiorActive || paused;
        public bool NativeReady(bool interiorActive, bool trafficReady, bool timeRunning) => trafficReady && (!Inside || interiorActive) && timeRunning;
    }

    internal enum InteriorViewFallback { None, Uncertain, NotShop, Elevator, Height, Exception }

    internal readonly struct InteriorViewEligibility
    {
        public readonly Position Anchor;
        public readonly InteriorViewFallback Fallback;
        public InteriorViewEligibility(Position anchor, InteriorViewFallback fallback) { Anchor = anchor; Fallback = fallback; }
        public static InteriorViewEligibility Check(bool shop, bool elevator, Position door, double floorHeight)
        {
            var reason = !shop ? InteriorViewFallback.NotShop : elevator ? InteriorViewFallback.Elevator
                : !door.IsFinite || !Position.Finite(floorHeight) ? InteriorViewFallback.Uncertain
                : Math.Abs(floorHeight - door.Y) > 3 ? InteriorViewFallback.Height : InteriorViewFallback.None;
            return new InteriorViewEligibility(door, reason);
        }
    }

    internal sealed class InteriorViewState
    {
        private object? entry;
        private double nextCheck;
        private bool faulted;
        public bool Active { get; private set; }
        public Position Anchor { get; private set; }
        public InteriorViewFallback Fallback { get; private set; }
        public int Revision { get; private set; }

        // Cheap identity/hidden checks may run every frame; geometry is read on entry and at most
        // once per two seconds thereafter. A failed read latches closed until exit/new entry.
        public void Refresh(bool enabled, TravelVisibility travel, object? currentEntry, double now, Func<InteriorViewEligibility> read)
        {
            if (!enabled || !travel.Inside || currentEntry == null) { Reset(); return; }
            if (!ReferenceEquals(entry, currentEntry)) { Reset(); entry = currentEntry; }
            if (travel.OtherHidden) { Suspend(); return; }
            if (faulted || now < nextCheck) return;
            nextCheck = now + 2;
            try
            {
                var result = read();
                Fallback = result.Fallback;
                bool ready = result.Fallback == InteriorViewFallback.None && result.Anchor.IsFinite;
                if (ready != Active) Revision++;
                Active = ready;
                Anchor = ready ? result.Anchor : default;
            }
            catch { FailClosed(); }
        }

        public void Suspend()
        {
            if (Active) Revision++;
            Active = false; Anchor = default; nextCheck = 0;
        }
        public void FailClosed() { Suspend(); faulted = true; Fallback = InteriorViewFallback.Exception; }
        public void Reset() { Suspend(); entry = null; faulted = false; Fallback = InteriorViewFallback.None; }
        public Position Focus(Position player) => Active ? Anchor : player;
        public double Radius(double streetRadius, double interiorRadius) => Active ? Math.Min(streetRadius, interiorRadius) : streetRadius;
        public int Pedestrians(int global, int interior) => Active ? Math.Min(global, Math.Min(48, interior)) : global;
        public int Vehicles(int global, int interior) => Active ? Math.Min(global, Math.Min(24, interior)) : global;
        public double ReleaseRadius(double streetRadius, double interiorRadius) => Active ? Radius(streetRadius, interiorRadius) : streetRadius * 1.4;
    }
}
