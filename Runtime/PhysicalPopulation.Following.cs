// Walker and car follow: the native car command, corrections, fades, the phase count, inspector picking and player contact.
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
        /// <summary>F2d: the car's own leg and its cap, for the native command. The cap is the kernel's background
        /// coordinate (MaximumNominalMinutes while the vehicle holds a link, the logical pose after the kernel completed
        /// it), never beyond the car's own run of car legs. False when the car follows nothing (its follow ended).</summary>
        private bool CarCommand(PhysicalVehicleBody body, out Binding binding, out PhysicalJourneySample own, out double cap)
        {
            own = default; cap = 0;
            if (!bindings.TryGetValue(body.CitizenId, out binding!) || !ReferenceEquals(binding.Car, body) || binding.Journey == null) return false;
            own = WalkerFollow.Sample(binding.Journey, binding.BodyMinutes);
            if (own.Mode != TravelMode.Car || !own.ServiceId.StartsWith("native:road:v1:", StringComparison.Ordinal)) return false;
            cap = CarFollow.Cap(binding.Journey, binding.BodyMinutes, LogicalCarMinutes(binding.Citizen, binding.Journey));
            return true;
        }
        // The kernel's coordinate while the vehicle holds a link, the logical pose after the kernel completed it, and the
        // route's end once the journey itself has ended (the car then drives on only to its parking point).
        private double LogicalCarMinutes(CitizenState citizen, Route route)
        {
            if (citizen.Phase != CitizenPhase.Travelling || !ReferenceEquals(citizen.Journey, route)) return route.Minutes;
            return simulation.TryGetTrafficPermission(citizen.Identity.Id, out var permission) && permission.LinkId.HasValue && !permission.IsCompleted
                ? permission.MaximumNominalMinutes : simulation.SampleJourney(citizen).ElapsedMinutes;
        }
        private static readonly Position HoldDirection = new Position(1, 0, 0);
        /// <summary>F2d native command: Gley drives at its own pace toward the kernel's coordinate on the car's own leg and
        /// is braked so it never passes it. A corner Gley has reported is driven through on the next leg's limit; the
        /// corner handoff holds the turn while the kernel is still short of it, and a fading car stands.</summary>
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
            // A native loop must not pop a later corner before the kernel's coordinate reaches this one.
            return own.ServiceId.EndsWith(":" + nativeTarget.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                && PhysicalTrafficCornerPermission.CanReachEndpoint(own, cap);
        }

        private bool Observe(Binding binding)
        {
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

        /// <summary>
        /// F2c walker follow (plan Decision 2). Each frame: sample the logical pose, project the body onto its own leg,
        /// take the along-route lag, and walk at native pace up to 1.5 m behind, faster (up to 1.5x) beyond it, never past
        /// the logical pose. Beyond 8 m, or once the logical leg is a car, bus or subway, the body is corrected
        /// (<see cref="CorrectWalker"/>). A changed journey reference or phase ends the follow; the logical arrival of
        /// this journey starts the arrival tail instead. Reads only; nothing is written to the simulation.
        /// </summary>
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
            if (binding.LagFrame != Time.frameCount) { binding.LagFrame = Time.frameCount; Telemetry.RecordLag(lag); }
            var own = WalkerFollow.Sample(route, binding.BodyMinutes);
            if (binding.Tail) return ObserveArrivalTail(binding, own, lag);
            // The body reached the car, bus or subway it follows: hand over to a body of the new kind (or none).
            if (own.Mode != TravelMode.Walk) { binding.Handoff = true; Telemetry.Handoffs++; return false; }
            if (!WalkerFollow.NeedsCorrection(lag, logical.Mode)) { binding.HurrySince = float.NaN; binding.Fading = false; }
            else if (!CorrectWalker(binding, logical, own, lag)) return false;
            else if (!float.IsNaN(binding.FadeOutSince)) return FadeOut(binding, logical.Position);
            else if (float.IsNaN(binding.HurrySince))
            {
                // Moved to the logical pose off screen: follow on from there.
                own = WalkerFollow.Sample(route, binding.BodyMinutes);
                lag = 0;
            }
            return WalkLeg(binding, own, lag, logical);
        }

        /// <summary>
        /// Off screen, the body is moved to the logical pose (or released if the person is no longer walking). On screen
        /// the Q3 default applies: hurry for up to 10 s, then stop at the next corner and fade out there; it reappears at
        /// the logical pose once that pose is out of view. Until it reaches a corner it keeps hurrying, and if its
        /// materials cannot fade it keeps following until it is off screen and is moved then. Nobody jumps on screen.
        /// Every correction is recorded with its on-screen flag. False releases the body. Bounds run on the follow
        /// clock, which stops while the city clock does.
        /// </summary>
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
                // Close behind a person who has just boarded is an ordinary handoff, not a lag correction.
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

        /// <summary>The fade itself: the body stands and its renderers fade over <see cref="WalkerFollow.FadeOutSeconds"/>,
        /// then it is released. Its person reappears through selection only once the logical pose is out of view.</summary>
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

        /// <summary>Walk at the pace the lag asks for. The native destination is bounded by the logical pose: on the
        /// logical pose's leg it is that pose (refreshed every <see cref="WalkerFollow.RetargetMetres"/>), on an earlier
        /// leg the leg's corner, so the agent stops before the pose whatever the frame durations, even while the logical
        /// clock stands still. The speed is also capped at the lag per frame.</summary>
        private bool WalkLeg(Binding binding, PhysicalJourneySample own, double lag, PhysicalJourneySample logical)
        {
            var body = binding.Person!;
            double pace = float.IsNaN(binding.HurrySince) ? WalkerFollow.PaceFactor(lag)
                : lag > WalkerFollow.StopMetres ? WalkerFollow.MaximumPaceFactor : 0;
            if (own.Stationary || own.AtEnd) { body.Pace(0); return true; }
            bool pose = logical.Mode == TravelMode.Walk && !logical.Stationary && own.LegIndex == logical.LegIndex;
            Position goal = pose ? logical.Position : own.To;
            // A pose at the leg's own corner (the door at the end of the route) is always re-aimed, so a destination
            // cached a few decimetres short can never strand the body before the door.
            bool corner = Position.Distance(goal, own.To) <= 1e-6;
            bool retarget = binding.TargetLeg != own.LegIndex || binding.TargetIsPose != pose
                || pose && (Position.Distance(goal, binding.TargetPoint) >= WalkerFollow.RetargetMetres
                    || corner && Position.Distance(goal, binding.TargetPoint) > 1e-6);
            if (retarget && !(pose && lag <= WalkerFollow.StopMetres))
            {
                // The previous destination on this route (an earlier pose or corner) is never past the pose: while a
                // re-aim fails, or waits out its one-second retry, the body keeps walking to it.
                bool previous = binding.TargetLeg >= 0 && binding.TargetLeg <= own.LegIndex;
                if (Time.unscaledTime < binding.NextTargetAttempt) { if (!previous) { body.Pace(0); return true; } }
                else
                {
                    bool aimed = false;
                    // A pose between corners is projected onto the walkable surface first (a straight leg can cross a
                    // kerb or a crowned road without a NavMesh corner); a projection that lands ahead of the pose along
                    // the leg is not used. Then the pose is tried as given. Each SetTarget is one path calculation.
                    if (pose && !corner && body.TryProjectPose(goal, out var projected)
                        && WalkerFollow.NotAheadOnLeg(logical.From, logical.To, goal, projected))
                    { Telemetry.PathRequests++; aimed = body.SetTarget(projected, NativeTravelPace.WalkingMetresPerSecond); }
                    if (!aimed) { Telemetry.PathRequests++; aimed = body.SetTarget(goal, NativeTravelPace.WalkingMetresPerSecond); }
                    if (aimed) { binding.TargetLeg = own.LegIndex; binding.TargetIsPose = pose; binding.TargetPoint = goal; binding.NextTargetAttempt = 0; }
                    else
                    {
                        binding.NextTargetAttempt = Time.unscaledTime + 1; blockedSamples++;
                        if (!previous) { body.Stop(true); return true; }
                    }
                }
            }
            else if (retarget) { body.Pace(0); return true; }
            body.Pace(WalkerFollow.CommandSpeed(NativeTravelPace.WalkingMetresPerSecond * pace, lag, Time.deltaTime));
            return true;
        }

        /// <summary>
        /// Arrival tail: the logical arrival is processed, and the visible walker keeps walking to the door. At the door it
        /// joins a vendor line, an optional stop or the door queue (ObserveArrival), or goes in (released). The tail is
        /// bounded at 10 s on the follow clock: off screen it is then released as a lag release; on screen a fade-capable
        /// body stops at its next intermediate corner and fades out there, and on the last leg it still walks to the door.
        /// Nobody is moved on screen. A journey committed meanwhile is followed from the door; one that starts elsewhere
        /// ends the tail at once off screen, or at the door or a corner fade on screen.
        /// </summary>
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
            // On screen past the bound, the Q3 flow applies: a body whose materials can fade stops at the next
            // intermediate corner and fades out there. On the last leg it walks on to the door, so the door queue,
            // a vendor line or entry still take it. An opaque body keeps walking until it is out of view.
            if (expired && body.CanFade)
            {
                if (!binding.Fading) { binding.Fading = true; binding.FadeLeg = own.LegIndex; }
                bool corner = own.LegIndex != binding.FadeLeg
                    || own.LegIndex < route.Legs.Count - 1 && (own.Stationary || Position.Distance(body.Position, own.To) <= 0.3);
                if (corner) { BeginFade(binding, true); return FadeOut(binding, door); }
            }
            return WalkLeg(binding, own, Math.Max(lag, WalkerFollow.StopMetres * 2), WalkerFollow.Sample(route, route.Minutes));
        }

        /// <summary>
        /// F2d car follow (plan Decision 2). Each frame: sample the logical pose (the kernel's background coordinate),
        /// advance the car's own progress (projection on its own leg; a road corner only with Gley's reached evidence),
        /// take the along-route lag and count the phase state. Up to 5 m the car follows, beyond it catches up at Gley's
        /// pace, never past the kernel's coordinate (CommandLimit). Beyond 30 m, or once the kernel has completed the
        /// traffic leg while the car still drives, the car is corrected (<see cref="CorrectCar"/>). Reaching its parking
        /// point hands over to a walker at the logical pose. When the journey ends (arrival or replacement) while the car
        /// still drives, an on-screen car drives on to its parking point under the same correction rule and an
        /// off-screen one is released. Reads only; nothing is written to the simulation or the kernel.
        /// </summary>
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
            // The car reached its parking point (the end of its native road legs; the kerb connector and the parking pause
            // after it are not driven by a native car): a walker takes over at the logical pose.
            if (after.AtEnd || !CarFollow.IsRoad(after))
            { binding.Handoff = true; Telemetry.Handoffs++; Telemetry.CarHandoffs++; return false; }
            ObservePhase(binding, following, admitted, permission, lag);
            // A logical pose past the kernel's sections but still in this car's run (the trailing connector or the parking
            // pause) is the car's own journey: the car follows on to its parking point without a correction.
            if (!CarFollow.NeedsCorrection(lag, following && CarFollow.SameCarRun(route, binding.BodyMinutes, logical.ElapsedMinutes)))
            { binding.HurrySince = float.NaN; binding.Fading = false; return true; }
            return CorrectCar(binding, logical, after, lag);
        }

        /// <summary>
        /// Off screen, a native car cannot be placed: it is released and a body of the right kind is acquired at the
        /// logical pose at the next selection, once that pose is out of view (close behind a person who has just parked
        /// it is an ordinary handoff). On screen the Q3 default applies: the car keeps catching up for up to 10 s, then
        /// stops at its next corner and fades out there; it reappears at the logical pose once that pose is out of view.
        /// A car whose materials cannot fade keeps following until it is off screen. Nothing jumps on screen. Every
        /// correction is recorded with its on-screen flag. False releases the body.
        /// </summary>
        private bool CorrectCar(Binding binding, PhysicalJourneySample logical, PhysicalJourneySample own, double lag)
        {
            var body = binding.Car!; var citizen = binding.Citizen; string id = citizen.Identity.Id;
            if (!body.OnScreen)
            {
                binding.Handoff = true;
                if (logical.Mode != TravelMode.Car && lag <= CarFollow.CorrectionMetres)
                {
                    Telemetry.Handoffs++; Telemetry.CarHandoffs++;
                    // A walker some way ahead of the hidden car appears only where the player cannot see it appear.
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

        /// <summary>The car fade: it stands (CommandLimit holds it) and its renderers fade over
        /// <see cref="CarFollow.FadeOutSeconds"/>, then it is released. Its person reappears at the logical pose through
        /// selection only once that pose is out of view.</summary>
        private bool FadeOutCar(Binding binding, Position after)
        {
            var body = binding.Car!;
            float alpha = 1 - (followClock - binding.FadeOutSince) / (float)CarFollow.FadeOutSeconds;
            if (alpha > 0) { body.SetFade(alpha); return true; }
            body.SetFade(0);
            Telemetry.Record(Time.frameCount, binding.Citizen.Identity.Id, WalkerCorrectionKind.Fade, binding.FadeFrom, after, binding.FadeOnScreen, car: true);
            // A person whose journey ended (arrived) while the car still drove keeps the restriction at the place it
            // arrived at, as an arrival-tail walker fade does, so a door queue or an optional stop there gets no body in view.
            var citizen = binding.Citizen;
            bool arrived = !(citizen.Phase == CitizenPhase.Travelling && ReferenceEquals(citizen.Journey, binding.Journey))
                && (citizen.Phase == CitizenPhase.AtPlace || citizen.Phase == CitizenPhase.WaitingForOpening);
            fadedUntilHidden[citizen.Identity.Id] = (citizen, binding.Journey, arrived ? citizen.PlaceId : null);
            return false;
        }

        /// <summary>Decision 6: count a standing visible car held by the kernel at a native green, or stopped by the
        /// native light at a kernel green. Both compare the native and kernel phases now, at the same junction: the
        /// kernel vehicle's own red at its link end, or the kernel signal of the section the car itself is in. Measured
        /// from Gley's target waypoint and the kernel; it changes no signal phase and writes nothing logical.</summary>
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

        // Inspector picking: read-only, allocation-free projection of active own walkers. Cars,
        // ambient/RLO-reserved actors and released or reused bodies are never candidates.
        private const float PickHalfWidthPixels = 8f;
        public void PickWalker(Camera camera, float mouseX, float mouseY, ref ResidentPickAccumulator best)
        {
            if (!active || disposed || camera == null) return;
            foreach (var binding in bindings.Values)
            {
                var body = binding.Person;
                // F5i: visitor bodies are street presence only. CitySimulator.Inspect has no visitor view (plan F5 Q5), so a
                // pick would open the resident popup with "left the city".
                if (body == null || binding.Car != null || !body.Valid || body.IdentityId != binding.Citizen.Identity.Id
                    || binding.Citizen.IsVisitor || ambient.IsReserved(body.IdentityId)
                    || !Project(camera, body, out float footX, out float footY, out float headX, out float headY, out float depth)) continue;
                if (ResidentPickAccumulator.Hit(mouseX, mouseY, footX, footY, headX, headY, PickHalfWidthPixels, out float score))
                    best.Consider(body.IdentityId, body.Generation, score, depth);
            }
        }
        /// <summary>Screen rectangle of the selected walker if that exact body (identity and generation) is still active.</summary>
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
            // IMGUI coordinates grow downwards.
            footX = foot.x; footY = Screen.height - foot.y; headX = head.x; headY = Screen.height - head.y; depth = foot.z;
            return true;
        }

        /// <summary>
        /// F2e (plan Decision 4): the visible world's only write into Core. A following walker or car that the player blocks
        /// is reported to the simulator as a bounded, retroactive player hold, which Core applies at once; every applied hold
        /// is one Player.log stream line. Detection reads only the player's own pose (PlayerContact).
        /// </summary>
        private void ObservePlayerContact(Binding binding, in PlayerPose player)
        {
            var citizen = binding.Citizen; var route = binding.Journey;
            // F5i: Core refuses holds for visitors (PlayerHoldRefusal.Visitor), so a blocked visitor body is not reported;
            // it lags and is corrected like any other body.
            if (citizen.IsVisitor) return;
            if (route == null || binding.Tail || !float.IsNaN(binding.FadeOutSince) || citizen.Phase != CitizenPhase.Travelling
                || !ReferenceEquals(citizen.Journey, route)) return;
            bool car = binding.Car != null;
            Position at = binding.Person?.Position ?? binding.Car!.Position;
            // Out of reach and no open track: nothing to observe (the common case, one distance test per body).
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
