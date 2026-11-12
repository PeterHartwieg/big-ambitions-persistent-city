using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BAModAPI;
using BigAmbitions.DayNightCycle;
using PersistentCity.Core;
using UnityEngine;

namespace PersistentCity.Runtime
{
    internal sealed partial class PhysicalPopulation
    {
        private bool CarCommand(PhysicalVehicleBody body, out Binding binding, out PhysicalJourneySample own, out double cap)
        {
            own = default; cap = 0;
            if (!bindings.TryGetValue(body.CitizenId, out binding!) || !ReferenceEquals(binding.Car, body) || binding.Journey == null) return false;
            own = WalkerFollow.Sample(binding.Journey, binding.BodyMinutes);
            if (own.Mode != TravelMode.Car || !own.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
            cap = CarFollow.Cap(binding.Journey, binding.BodyMinutes, LogicalCarMinutes(binding.Citizen, binding.Journey));
            return true;
        }
        private double LogicalCarMinutes(CitizenState citizen, Route route)
        {
            if (citizen.Phase != CitizenPhase.Travelling || !ReferenceEquals(citizen.Journey, route)) return route.Minutes;
            return simulation.TryGetTrafficPermission(citizen.Identity.Id, out var permission) && permission.LinkId.HasValue && !permission.IsCompleted
                ? permission.MaximumNominalMinutes : simulation.SampleJourney(citizen).ElapsedMinutes;
        }
        private static readonly Position HoldDirection = new Position(1, 0, 0);
        private PhysicalTrafficCommandLimit CommandLimit(PhysicalVehicleBody body)
        {
            if (!CarCommand(body, out var binding, out var own, out double cap) || own.Stationary)
                return new PhysicalTrafficCommandLimit(own.Stationary ? HoldDirection : own.Direction, 0, true);
            if (!float.IsNaN(binding.FadeOutSince) || binding.CornerHandoff.MustHold(own, cap))
                return new PhysicalTrafficCommandLimit(own.Direction, 0, true);
            var leg = own;
            if (binding.ReachedCarLeg != own.LegIndex && body.TryPeekReached(own.To, out _))
            {
                var next = WalkerFollow.Sample(binding.Journey!, own.ElapsedMinutes + own.RemainingLegMinutes);
                if (next.Mode != TravelMode.Car || next.Stationary || next.LegIndex == own.LegIndex)
                    return new PhysicalTrafficCommandLimit(own.Direction, 0, true);
                leg = next;
            }
            return new PhysicalTrafficCommandLimit(leg.Direction, PhysicalTrafficMotion.MaximumAdvance(leg, body.Position, cap), false);
        }
        private bool NativeTargetPermission(PhysicalVehicleBody body, int nativeTarget)
        {
            if (!CarCommand(body, out _, out var own, out double cap)) return false;
            return own.ServiceId.EndsWith(":" + nativeTarget.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && PhysicalTrafficCornerPermission.CanReachEndpoint(own, cap);
        }

        private bool Observe(Binding binding)
        {
            binding.BoundPace = 0;
            if (binding.Person != null && !binding.Person.Valid || binding.Car != null && !binding.Car.Valid) return false;
            if (binding.VendorOpening > 0) return ObserveVendorQueue(binding);
            if (binding.Optional != null) return ObserveOptional(binding);
            if (binding.Person != null && binding.Journey != null) return ObserveWalker(binding);
            if (binding.Car != null) return binding.Journey != null && ObserveCar(binding);
            if (TryJoinOptional(binding)) return ObserveOptional(binding);
            if (binding.AwaitingArrival) return ObserveArrival(binding);
            if (binding.Queue.HasValue) return ObserveQueue(binding);
            if (!Outdoor(binding.Citizen) || binding.Citizen.Phase == CitizenPhase.Travelling) return false;
            binding.Person!.Stop(true); return true;
        }

        private bool ObserveWalker(Binding binding)
        {
            var body = binding.Person!; var citizen = binding.Citizen; var route = binding.Journey!;
            bool following = citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, route);
            if (!following && !binding.Tail)
            {
                if ((citizen.Phase != CitizenPhase.AtPlace && citizen.Phase != CitizenPhase.WaitingForOpening)
                    || !TryBodyPlace(citizen.PlaceId, out var place)
                    || Position.Distance(place.Position, route.Legs[route.Legs.Count - 1].To) > 3) return false;
                binding.Tail = true; binding.TailSince = followClock; binding.HurrySince = float.NaN; binding.Fading = false;
            }
            PhysicalJourneySample logical = following ? simulation.SampleJourney(citizen) : WalkerFollow.Sample(route, route.Minutes);
            if (!float.IsNaN(binding.FadeOutSince)) return FadeOut(binding, logical.Position);
            binding.BodyMinutes = WalkerFollow.Advance(route, binding.BodyMinutes, logical.ElapsedMinutes, body.Position);
            double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
            var own = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (binding.LagFrame != Time.frameCount)
            {
                binding.LagFrame = Time.frameCount; Telemetry.RecordLag(lag);
                if (following) Telemetry.RecordAhead(WalkerFollow.AheadMetres(own, logical, body.Position));
            }
            if (binding.Tail) return ObserveArrivalTail(binding, own, lag);
            if (own.Mode != TravelMode.Walk) { binding.Handoff = true; Telemetry.Handoffs++; return false; }
            if (!WalkerFollow.NeedsCorrection(lag, logical.Mode)) { binding.HurrySince = float.NaN; binding.Fading = false; }
            else if (!CorrectWalker(binding, logical, own, lag)) return false;
            else if (!float.IsNaN(binding.FadeOutSince)) return FadeOut(binding, logical.Position);
            else if (float.IsNaN(binding.HurrySince))
            {
                own = WalkerFollow.Sample(route, binding.BodyMinutes);
                lag = 0;
            }
            return WalkLeg(binding, own, lag, logical);
        }

        private bool CorrectWalker(Binding binding, PhysicalJourneySample logical, PhysicalJourneySample own, double lag)
        {
            var body = binding.Person!; string id = binding.Citizen.Identity.Id;
            Position before = body.Position;
            if (!body.OnScreen)
            {
                if (logical.Mode == TravelMode.Walk && body.Warp(logical.Position))
                {
                    Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.Move, before, body.Position, false);
                    binding.BodyMinutes = logical.ElapsedMinutes; binding.TargetLeg = -1;
                    binding.HurrySince = float.NaN; binding.Fading = false;
                    return true;
                }
                binding.Handoff = logical.Mode != TravelMode.Walk;
                if (binding.Handoff && lag <= WalkerFollow.CorrectionMetres) { Telemetry.Handoffs++; return false; }
                Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.LagRelease, before, logical.Position, false);
                return false;
            }
            if (float.IsNaN(binding.HurrySince)) binding.HurrySince = followClock;
            float hurried = followClock - binding.HurrySince;
            if (!binding.Fading && hurried >= WalkerFollow.HurrySeconds) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
            if (!binding.Fading) return true;
            if ((own.LegIndex != binding.FadeLeg || own.Stationary || Position.Distance(before, own.To) <= 0.3) && body.CanFade)
                BeginFade(binding, true);
            return true;
        }

        private void BeginFade(Binding binding, bool onScreen)
        {
            binding.FadeOutSince = followClock; binding.FadeOnScreen = onScreen; binding.FadeFrom = binding.Person!.Position;
            binding.Person.Pace(0);
        }

        private bool FadeOut(Binding binding, Position after)
        {
            var body = binding.Person!;
            body.Pace(0);
            float alpha = 1 - (followClock - binding.FadeOutSince) / (float)WalkerFollow.FadeOutSeconds;
            if (alpha > 0) { body.SetFade(alpha); return true; }
            body.SetFade(0);
            Telemetry.Record(Time.frameCount, binding.Citizen.Identity.Id, WalkerCorrectionKind.Fade, binding.FadeFrom, after, binding.FadeOnScreen);
            fadedUntilHidden[binding.Citizen.Identity.Id] = (binding.Citizen, binding.Journey, binding.Tail ? binding.Citizen.PlaceId : null);
            return false;
        }

        private bool WalkLeg(Binding binding, PhysicalJourneySample own, double lag, PhysicalJourneySample logical)
        {
            var body = binding.Person!;
            double pace = float.IsNaN(binding.HurrySince) ? WalkerFollow.PaceFactor(lag)
                : lag > WalkerFollow.StopMetres ? WalkerFollow.MaximumPaceFactor : 0;
            if (own.Stationary || own.AtEnd) { body.Pace(0); return true; }
            bool retarget = WalkerFollow.NeedsPath(own, logical, lag, binding.TargetLeg, binding.TargetIsPose, binding.TargetPoint,
                body.HasPath, out bool atPose);
            if (retarget)
            {
                bool previous = binding.TargetLeg >= 0 && binding.TargetLeg <= own.LegIndex;
                if (Time.unscaledTime < binding.NextTargetAttempt) { if (!previous) { body.Pace(0); return true; } }
                else
                {
                    bool aimed = false, poseAim = atPose;
                    Position goal = atPose ? logical.Position : own.To;
                    if (!atPose) { Telemetry.PathRequests++; aimed = body.SetTarget(goal, NativeTravelPace.WalkingMetresPerSecond); }
                    if (!aimed && WalkerFollow.PoseOnLeg(own, logical) && lag > WalkerFollow.StopMetres
                        && Position.Distance(logical.Position, own.To) > 1e-6)
                    {
                        poseAim = true; goal = logical.Position;
                        if (body.TryProjectPose(goal, out var projected) && WalkerFollow.NotAheadOnLeg(logical.From, logical.To, goal, projected))
                        { Telemetry.PathRequests++; aimed = body.SetTarget(projected, NativeTravelPace.WalkingMetresPerSecond); }
                        if (!aimed) { Telemetry.PathRequests++; aimed = body.SetTarget(goal, NativeTravelPace.WalkingMetresPerSecond); }
                    }
                    if (aimed) { binding.TargetLeg = own.LegIndex; binding.TargetIsPose = poseAim; binding.TargetPoint = goal; binding.NextTargetAttempt = 0; }
                    else
                    {
                        binding.NextTargetAttempt = Time.unscaledTime + 1; blockedSamples++;
                        if (!previous) { body.Stop(true); return true; }
                    }
                }
            }
            body.Pace(WalkerFollow.CommandSpeed(NativeTravelPace.WalkingMetresPerSecond * pace, lag, Time.deltaTime));
            binding.BoundPace = NativeTravelPace.WalkingMetresPerSecond * pace; binding.BoundMinutes = logical.ElapsedMinutes;
            return true;
        }

        private void BoundWalkers()
        {
            if (!active || disposed) return;
            float seconds = Time.deltaTime;
            foreach (var binding in bindings.Values)
            {
                var body = binding.Person; var route = binding.Journey;
                if (body == null || route == null || !(binding.BoundPace > 0) || !body.Valid) continue;
                double progress = WalkerFollow.Advance(route, binding.BodyMinutes, binding.BoundMinutes, body.Position);
                body.Pace(WalkerFollow.CommandSpeed(binding.BoundPace, WalkerFollow.RouteMetres(route, progress, binding.BoundMinutes), seconds));
            }
        }

        private bool ObserveArrivalTail(Binding binding, PhysicalJourneySample own, double lag)
        {
            var body = binding.Person!; var citizen = binding.Citizen; var route = binding.Journey!;
            Position door = route.Legs[route.Legs.Count - 1].To;
            var next = citizen.Phase == CitizenPhase.Travelling && citizen.Journey != null && !ReferenceEquals(citizen.Journey, route)
                ? citizen.Journey : null;
            bool fromDoor = next != null && next.Legs.Count > 0 && Position.Distance(next.Legs[0].From, door) <= 3;
            bool atDoor = own.AtEnd || own.Mode != TravelMode.Walk;
            if (atDoor)
            {
                if (fromDoor) { Follow(binding, next!, 0); return true; }
                if (next != null) return false;
                binding.Tail = false; binding.Journey = null;
                binding.AwaitingArrival = true; binding.AwaitMinute = simulation.View.Minute; binding.AwaitJourney = route;
                return ObserveArrival(binding);
            }
            bool expired = followClock - binding.TailSince >= WalkerFollow.ArrivalTailSeconds;
            if ((expired || next != null && !fromDoor) && !body.OnScreen)
            {
                Telemetry.Record(Time.frameCount, citizen.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, door, false);
                return false;
            }
            if (expired && body.CanFade)
            {
                if (!binding.Fading) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
                bool corner = own.LegIndex != binding.FadeLeg
                    || own.LegIndex < route.Legs.Count - 1 && (own.Stationary || Position.Distance(body.Position, own.To) <= 0.3);
                if (corner) { BeginFade(binding, true); return FadeOut(binding, door); }
            }
            return WalkLeg(binding, own, Math.Max(lag, WalkerFollow.StopMetres * 2), WalkerFollow.Sample(route, route.Minutes));
        }

        internal const int CarSyncFrames = 4;
        private bool ObserveCar(Binding binding)
        {
            var body = binding.Car!; var citizen = binding.Citizen; var route = binding.Journey!;
            bool following = citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, route);
            var logical = following ? simulation.SampleJourney(citizen) : WalkerFollow.Sample(route, route.Minutes);
            if (!float.IsNaN(binding.FadeOutSince)) return FadeOutCar(binding, logical.Position);
            if (!following && !body.OnScreen)
            {
                Telemetry.Record(Time.frameCount, citizen.Identity.Id, WalkerCorrectionKind.LagRelease, body.Position, logical.Position, false, car: true);
                return false;
            }
            if (following && !body.OnScreen && (long)Time.frameCount - binding.CarSyncFrame < CarSyncFrames
                && Time.frameCount >= binding.CarSyncFrame) return true;
            binding.CarSyncFrame = Time.frameCount;
            var permission = default(TrafficVehicleSnapshot);
            bool admitted = following && simulation.TryGetTrafficPermission(citizen.Identity.Id, out permission)
                && permission.LinkId.HasValue && !permission.IsCompleted;
            double logicalMinutes = admitted ? permission.MaximumNominalMinutes : logical.ElapsedMinutes;
            var own = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (own.Mode == TravelMode.Car && !own.Stationary && binding.ReachedCarLeg != own.LegIndex
                && body.TryPeekReached(own.To, out var marker) && body.ConsumeReached(own.To))
            { binding.ReachedCarLeg = own.LegIndex; binding.ReachedCarMarker = marker; }
            binding.BodyMinutes = CarFollow.Advance(route, binding.BodyMinutes, CarFollow.Cap(route, binding.BodyMinutes, logicalMinutes),
                body.Position, binding.ReachedCarLeg);
            var after = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (after.LegIndex == own.LegIndex + 1 && binding.ReachedCarLeg == own.LegIndex)
                binding.CornerHandoff.Begin(own, after, binding.ReachedCarMarker);
            double lag = WalkerFollow.RouteMetres(route, binding.BodyMinutes, logical.ElapsedMinutes);
            if (binding.LagFrame != Time.frameCount)
            {
                binding.LagFrame = Time.frameCount; Telemetry.RecordCarLag(lag);
                if (lag > CarFollow.CatchUpMetres) Telemetry.CarCatchUpFrames++;
            }
            if (after.AtEnd || !CarFollow.IsRoad(after))
            { binding.Handoff = true; Telemetry.Handoffs++; Telemetry.CarHandoffs++; return false; }
            ObservePhase(binding, following, admitted, permission, lag);
            if (!CarFollow.NeedsCorrection(lag, following && CarFollow.SameCarRun(route, binding.BodyMinutes, logical.ElapsedMinutes)))
            { binding.HurrySince = float.NaN; binding.Fading = false; return true; }
            return CorrectCar(binding, logical, after, lag);
        }

        private bool CorrectCar(Binding binding, PhysicalJourneySample logical, PhysicalJourneySample own, double lag)
        {
            var body = binding.Car!; var citizen = binding.Citizen; string id = citizen.Identity.Id;
            if (!body.OnScreen)
            {
                binding.Handoff = true;
                if (logical.Mode != TravelMode.Car && lag <= CarFollow.CorrectionMetres)
                {
                    Telemetry.Handoffs++; Telemetry.CarHandoffs++;
                    if (lag > CarFollow.CatchUpMetres) fadedUntilHidden[id] = (citizen, binding.Journey, null);
                    return false;
                }
                Telemetry.Record(Time.frameCount, id, WalkerCorrectionKind.LagRelease, body.Position, logical.Position, false, car: true);
                fadedUntilHidden[id] = (citizen, binding.Journey, null);
                return false;
            }
            if (float.IsNaN(binding.HurrySince)) binding.HurrySince = followClock;
            if (!binding.Fading && followClock - binding.HurrySince >= CarFollow.HurrySeconds) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
            if (binding.Fading && body.CanFade
                && (own.LegIndex != binding.FadeLeg || Position.Distance(body.Position, own.To) <= CarFollow.CornerMetres))
            { binding.FadeOutSince = followClock; binding.FadeOnScreen = true; binding.FadeFrom = body.Position; }
            return true;
        }

        private bool FadeOutCar(Binding binding, Position after)
        {
            var body = binding.Car!;
            float alpha = 1 - (followClock - binding.FadeOutSince) / (float)CarFollow.FadeOutSeconds;
            if (alpha > 0) { body.SetFade(alpha); return true; }
            body.SetFade(0);
            Telemetry.Record(Time.frameCount, binding.Citizen.Identity.Id, WalkerCorrectionKind.Fade, binding.FadeFrom, after, binding.FadeOnScreen, car: true);
            var citizen = binding.Citizen;
            bool arrived = !(citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, binding.Journey))
                && (citizen.Phase == CitizenPhase.AtPlace || citizen.Phase == CitizenPhase.WaitingForOpening);
            fadedUntilHidden[citizen.Identity.Id] = (citizen, binding.Journey, arrived ? citizen.PlaceId : null);
            return false;
        }

        private void ObservePhase(Binding binding, bool following, bool admitted, TrafficVehicleSnapshot permission, double lag)
        {
            var body = binding.Car!;
            bool stopped = body.Speed < CarFollow.StoppedMetresPerSecond;
            bool? red = stopped ? vehicles.NativeRed(body) : null;
            bool held = admitted && !permission.SignalGreen && !permission.CanCrossBoundary
                && permission.NominalMinutes >= permission.LinkEndNominalMinutes - 1e-9;
            bool green = red == true && following
                && simulation.TryGetTrafficSignalGreen(binding.Citizen, binding.BodyMinutes, out bool atBody) && atBody;
            var kind = CarFollow.Classify(stopped, red, lag, held, green);
            Telemetry.RecordPhase(kind, binding.Phase, followDelta);
            binding.Phase = kind;
        }

        private const float PickHalfWidthPixels = 8f;
        public void PickWalker(Camera camera, float mouseX, float mouseY, ref ResidentPickAccumulator best)
        {
            if (!active || disposed || camera == null) return;
            foreach (var binding in bindings.Values)
            {
                var body = binding.Person;
                if (body == null || binding.Car != null || !body.Valid || body.IdentityId != binding.Citizen.Identity.Id
                    || binding.Citizen.IsVisitor || ambient.IsReserved(body.IdentityId)
                    || !Project(camera, body, out float footX, out float footY, out float headX, out float headY, out float depth)) continue;
                if (ResidentPickAccumulator.Hit(mouseX, mouseY, footX, footY, headX, headY, PickHalfWidthPixels, out float score))
                    best.Consider(body.IdentityId, body.Generation, score, depth);
            }
        }
        public bool TryWalkerRect(Camera camera, string identityId, long generation, out Rect rect)
        {
            rect = default;
            if (!active || disposed || camera == null || identityId == null || !bindings.TryGetValue(identityId, out var binding)) return false;
            var body = binding.Person;
            if (body == null || binding.Car != null || body.Generation != generation || !body.Valid || ambient.IsReserved(identityId)
                || !Project(camera, body, out float footX, out float footY, out float headX, out float headY, out _)) return false;
            float height = footY - headY, half = Math.Max(PickHalfWidthPixels, height * 0.3f);
            float centre = (footX + headX) * 0.5f;
            rect = new Rect(centre - half, headY - height * 0.1f, half * 2, height * 1.2f);
            return true;
        }
        private static bool Project(Camera camera, PhysicalPedestrianBody body, out float footX, out float footY, out float headX, out float headY, out float depth)
        {
            footX = footY = headX = headY = depth = 0;
            var transform = body.Character.transform;
            Vector3 feet = transform.position;
            var capsule = body.Capsule;
            float height = capsule != null ? Math.Max(1.2f, capsule.height * transform.lossyScale.y) : 1.8f;
            Vector3 foot = camera.WorldToScreenPoint(feet), head = camera.WorldToScreenPoint(feet + Vector3.up * height);
            if (foot.z <= 0 || head.z <= 0) return false;
            footX = foot.x; footY = Screen.height - foot.y; headX = head.x; headY = Screen.height - head.y; depth = foot.z;
            return true;
        }

        private void ObservePlayerContact(Binding binding, in PlayerPose player)
        {
            var citizen = binding.Citizen; var route = binding.Journey;
            if (citizen.IsVisitor) return;
            if (route == null || binding.Tail || !float.IsNaN(binding.FadeOutSince) || citizen.Phase != CitizenPhase.Travelling
                || !ReferenceEquals(citizen.Journey, route)) return;
            bool car = binding.Car != null;
            Position at = binding.Person?.Position ?? binding.Car!.Position;
            if (!PlayerContact.InReach(at, player, car) && !contact.Tracking(citizen.Identity.Id)) return;
            double minute = simulation.View.Minute;
            var own = WalkerFollow.Sample(route, Math.Min(route.Minutes, binding.BodyMinutes));
            double report = contact.Observe(citizen.Identity.Id, route, car, minute - citizen.DepartureMinute, binding.BodyMinutes,
                at, own.From, own.To, player, minute, Time.frameCount);
            if (report > 0)
                contact.Settle(citizen.Identity.Id, simulation.HoldForPlayer(citizen.Identity.Id, report, car ? PlayerHoldCause.PlayerVehicle : PlayerHoldCause.PlayerFoot), simulation.State);
        }
    }
}
