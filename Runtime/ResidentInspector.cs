using System;
using HarmonyLib;
using PersistentCity.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace PersistentCity.Runtime
{
    /// <summary>
    /// Resident inspector in the normal view: hovering an own walker highlights it, a click opens a
    /// small popup. Read-only: it changes no simulation, economy, schedule or save state, and makes
    /// no route or market call. Picking runs in Update (inside mod-work attribution) only after the
    /// cursor moved and at most every <see cref="ResidentPickThrottle.DefaultIntervalSeconds"/>;
    /// popup content is built on click and refreshed once per second while open.
    /// It only acts where the game itself would accept a world click: no open native menu, map, phone,
    /// placement, mini-game or screenshot mode, and no native UI element under the cursor. A press on
    /// the popup or a highlighted walker (current cursor position) makes that whole press-hold-release
    /// gesture the inspector's: a prefix on the game's MouseController.Run skips native world mouse
    /// handling for it, so an inspector click does not also move the player. Every other gesture stays
    /// native, including a hold-walk released over the popup or a walker (<see cref="InspectorPointerGesture"/>).
    /// </summary>
    internal sealed class ResidentInspector
    {
        private const float RefreshSeconds = 1f;
        private const float PopupWidth = 420, PopupHeight = 400;
        private enum Mode { Closed, Hover, Open }
        private readonly ResidentPickThrottle throttle = new ResidentPickThrottle();
        private Vector2 mouse = new Vector2(float.NaN, float.NaN);
        private string? hoveredId;
        private long hoveredGeneration;
        private string hoveredName = "";
        private Rect hoveredRect;
        private string? selectedId;
        private ResidentInsight? insight;
        private string body = "", personality = "", header = "", homeLabel = "", workLabel = "", mapProblem = "";
        private float nextRefresh;
        private Rect popup;
        private Vector2 scroll;
        private bool visible;
        private GUIStyle? text, link, title;
        // Timing per mode at the start of each call: Update runs inside PopulationRuntime.Update.
        private readonly long[] updateTicks = new long[3], updateCalls = new long[3], updateMaxTicks = new long[3];
        private readonly long[] drawTicks = new long[3], drawCalls = new long[3], drawMaxTicks = new long[3];
        private long builds, buildTicks, buildMaxTicks;
        private const string ClickGuardOwner = "persistentcity.resident-inspector-input";
        private static ResidentInspector? guarded;
        private Harmony? clickGuard;
        private Action<string>? warn;
        private bool mapFailureLogged, attachTried;
        private readonly InspectorPointerGesture gesture = new InspectorPointerGesture();
        private int decidedFrame = -1, ownedPressFrame = -1;
        private bool suppressThisFrame, guardFailed, wheelFailed;
        // Native uGUI raycasts miss inside the open popup, so what the player sees on top is what takes the click.
        private bool uiBlockInstalled, uiBlockFailed;
        private bool UiBlockActive => uiBlockInstalled && !uiBlockFailed;
        // Last frame Update ran. Input decisions require a fresh one, so a hidden or stale inspector
        // (e.g. while the city prepares routes and Update returns early) never claims a click or the wheel.
        private int updatedFrame = -10;
        // MouseController's private hold-walk flags: read before taking a gesture, cleared when an owned one ends.
        private static System.Reflection.FieldInfo? leftHeld, rightHeld;

        /// <summary>Install the native click guard. Failure leaves the inspector usable without it.</summary>
        public void Attach(Action<string> warning)
        {
            warn = warning;
            if (attachTried) return;
            attachTried = true;
            try
            {
                if (Harmony.HasAnyPatches(ClickGuardOwner)) throw new InvalidOperationException("Inspector input patch ID already owned.");
                var run = AccessTools.Method(typeof(MouseController), nameof(MouseController.Run), Type.EmptyTypes)
                    ?? throw new MissingMethodException("MouseController.Run()");
                leftHeld = AccessTools.Field(typeof(MouseController), "LeftButtonHeld");
                rightHeld = AccessTools.Field(typeof(MouseController), "RightButtonHeld");
                if (leftHeld?.FieldType != typeof(bool) || !leftHeld.IsStatic || rightHeld?.FieldType != typeof(bool) || !rightHeld.IsStatic)
                    throw new MissingFieldException("MouseController hold-walk flags changed.");
                var installing = new Harmony(ClickGuardOwner);
                installing.Patch(run, prefix: new HarmonyMethod(typeof(ResidentInspector), nameof(MouseRunPrefix)));
                clickGuard = installing; guarded = this;
            }
            catch (Exception error) { warning("Resident inspector click guard unavailable; inspector clicks may also reach the game: " + error.Message); }
            if (clickGuard == null) return;
            try
            {
                // The camera applies wheel zoom in its private UpdateInput. Restore its distance afterwards
                // while the cursor is on the popup; the shared blockCameraZoom flag is never written, so
                // native dialogs and windows keep sole ownership of it.
                var input = AccessTools.Method(typeof(CameraControllers.PedestrianCam), "UpdateInput", new[] { typeof(float) })
                    ?? throw new MissingMethodException("PedestrianCam.UpdateInput(float)");
                if (AccessTools.Field(typeof(CameraControllers.PedestrianCam), nameof(CameraControllers.PedestrianCam.distance))?.FieldType != typeof(float))
                    throw new MissingFieldException("PedestrianCam.distance changed.");
                clickGuard.Patch(input, prefix: new HarmonyMethod(typeof(ResidentInspector), nameof(CameraInputPrefix)),
                    postfix: new HarmonyMethod(typeof(ResidentInspector), nameof(CameraInputPostfix)));
            }
            catch (Exception error) { warning("Resident inspector scroll guard unavailable; scrolling the popup may also zoom the camera: " + error.Message); }
            try
            {
                // The popup is drawn on top of native uGUI (HUD, dialogs). Inside its rectangle, uGUI pointer
                // raycasts find nothing, so hidden native buttons cannot take a popup click and
                // IsPointerOverGameObject agrees with the popup. Keyboard and navigation input are untouched.
                var raycast = AccessTools.Method(typeof(UnityEngine.UI.GraphicRaycaster), nameof(UnityEngine.UI.GraphicRaycaster.Raycast),
                    new[] { typeof(PointerEventData), typeof(System.Collections.Generic.List<RaycastResult>) })
                    ?? throw new MissingMethodException("GraphicRaycaster.Raycast(PointerEventData, List<RaycastResult>)");
                clickGuard.Patch(raycast, prefix: new HarmonyMethod(typeof(ResidentInspector), nameof(UiRaycastPrefix)));
                uiBlockInstalled = true;
            }
            catch (Exception error) { warning("Resident inspector UI block unavailable; native UI under the popup keeps its clicks and the popup yields there: " + error.Message); }
        }

        private static bool UiRaycastPrefix(PointerEventData eventData)
        {
            var owner = guarded;
            if (owner == null || owner.uiBlockFailed || eventData == null) return true;
            try
            {
                Vector2 screen = eventData.position;
                return !(owner.visible && owner.Fresh && owner.selectedId != null
                    && owner.popup.Contains(new Vector2(screen.x, Screen.height - screen.y)));
            }
            catch (Exception error)
            {
                owner.uiBlockFailed = true;
                owner.warn?.Invoke("Resident inspector UI block failed and is disabled: " + error);
                return true;
            }
        }

        private static void CameraInputPrefix(CameraControllers.PedestrianCam __instance, out float __state) => __state = __instance.distance;

        private static void CameraInputPostfix(CameraControllers.PedestrianCam __instance, float __state)
        {
            var owner = guarded;
            if (owner == null || owner.wheelFailed) return;
            try { if (owner.PointerOnPopupNow()) __instance.distance = __state; }
            catch (Exception error)
            {
                owner.wheelFailed = true;
                owner.warn?.Invoke("Resident inspector scroll guard failed and is disabled: " + error);
            }
        }

        public void Detach()
        {
            if (ReferenceEquals(guarded, this)) guarded = null;
            gesture.Reset(); decidedFrame = ownedPressFrame = -1; suppressThisFrame = false;
            try { clickGuard?.UnpatchAll(ClickGuardOwner); }
            finally { clickGuard = null; attachTried = false; guardFailed = wheelFailed = uiBlockFailed = uiBlockInstalled = false; }
        }

        // Current native cursor in GUI coordinates (y grows downwards); the inspector draws with an identity GUI matrix.
        private static Vector2 CurrentMouse()
        {
            Vector3 position = Input.mousePosition;
            return new Vector2(position.x, Screen.height - position.y);
        }

        // Update ran this frame or the previous one (script order against the game's input is not fixed).
        private bool Fresh => updatedFrame >= Time.frameCount - 1;

        // Where an inspector click would land now: the popup, or a highlighted walker other than the open one.
        private bool PointerOnInspector(Vector2 at) => visible && Fresh && NormalView() && !NativeUiUnderPointer()
            && ((selectedId != null && popup.Contains(at)) || (hoveredId != null && hoveredId != selectedId && hoveredRect.Contains(at)));

        // The current cursor is on the open popup in the normal view.
        private bool PointerOnPopupNow() => visible && Fresh && selectedId != null && popup.Contains(CurrentMouse()) && NormalView();

        private static bool MouseRunPrefix()
        {
            var owner = guarded;
            if (owner == null || owner.guardFailed) return true;
            try { return !owner.SuppressNativeMouse(); }
            catch (Exception error)
            {
                // Runs inside the game's world-input path: fail open, once, and stay out of the way.
                owner.guardFailed = true; owner.gesture.Reset(); owner.suppressThisFrame = false; owner.ownedPressFrame = -1;
                owner.warn?.Invoke("Resident inspector click guard failed and is disabled; clicks go to the game: " + error);
                return true;
            }
        }

        // MouseController.Run can run more than once per frame; decide once, from current input.
        private bool SuppressNativeMouse()
        {
            int frame = Time.frameCount;
            if (frame == decidedFrame) return suppressThisFrame;
            decidedFrame = frame;
            // Left button only: the inspector has no right-click action, so right gestures stay native.
            bool pressed = Input.GetMouseButtonDown(0);
            bool released = Input.GetMouseButtonUp(0);
            bool anyDown = Input.GetMouseButton(0);
            bool hold = !gesture.Owned && pressed && ((bool)leftHeld!.GetValue(null) || (bool)rightHeld!.GetValue(null));
            bool onInspector = !gesture.Owned && pressed && !hold && PointerOnInspector(CurrentMouse());
            var action = gesture.Decide(pressed, released, anyDown, hold, onInspector);
            if (onInspector && action != NativeMouseAction.Allow) ownedPressFrame = frame;
            if (action == NativeMouseAction.SuppressAndReleaseHolds) { leftHeld!.SetValue(null, false); rightHeld!.SetValue(null, false); }
            suppressThisFrame = action != NativeMouseAction.Allow;
            return suppressThisFrame;
        }

        public void Reset()
        {
            ClearHover(); selectedId = null; insight = null; body = personality = header = homeLabel = workLabel = mapProblem = ""; throttle.Invalidate();
            gesture.Reset(); ownedPressFrame = -1;
        }

        /// <summary>The runtime skipped Update this frame (e.g. while routes prepare): hide the inspector's
        /// input claims until the next update. The selection itself is kept.</summary>
        public void Suspend()
        {
            visible = false; ClearHover(); updatedFrame = -10;
        }

        private Mode Current => selectedId != null ? Mode.Open : hoveredId != null ? Mode.Hover : Mode.Closed;

        private void ClearHover() { hoveredId = null; hoveredGeneration = 0; hoveredName = ""; }

        // The game's own conditions for world hover/clicks (see its EntityController hover and MouseController).
        private static bool NormalView()
        {
            if (CityMap.IsOpen || BuildingManager.IsInsideBuilding || BigAmbitions.PlacementSystem.PlacementSystem.IsInPlacementMode || UI.Smartphone.FullMenu.IsOpen || UI.MiniMenu.MiniMenu.IsOpen
                || PlayerActivity.PlayerActivityUI.IsPanelOpen || UI.InteriorDesigner.InteriorDesignerUI.IsOpen || SubwaySystem.IsRiding
                || GameManager.IsAnyMiniGameActive() || !ScreenshotController.uiIsVisible || ScreenshotController.isInFreeLookMode) return false;
            var screenshot = InstanceBehavior<UI.UIs>.Instance?.screenshot;
            return screenshot == null || !screenshot.enabled;
        }

        private static bool NativeUiUnderPointer()
        {
            var events = EventSystem.current;
            return events != null && events.IsPointerOverGameObject();
        }

        public void Update(CitySimulator simulation, PhysicalPopulation? physical, Rect blocked)
        {
            long started = Stopwatch.GetTimestamp();
            int mode = (int)Current;
            try
            {
                mouse = CurrentMouse();
                visible = NormalView();
                if (!visible || physical == null) { ClearHover(); }
                else
                {
                    var camera = GameManager.GetMainCamera();
                    bool overUi = (blocked.width > 0 && blocked.Contains(mouse)) || (selectedId != null && popup.Contains(mouse));
                    if (camera == null || overUi) ClearHover();
                    else
                    {
                        // A hovered walker can walk out from under a still cursor: one projection per frame.
                        // Native UI is checked only on hover or pick frames, never when closed and still.
                        if (hoveredId != null && (NativeUiUnderPointer() || !physical.TryWalkerRect(camera, hoveredId, hoveredGeneration, out hoveredRect) || !hoveredRect.Contains(mouse)))
                            ClearHover();
                        if (throttle.ShouldPick(Time.unscaledTime, mouse.x, mouse.y) && !NativeUiUnderPointer())
                        {
                            var best = new ResidentPickAccumulator();
                            physical.PickWalker(camera, mouse.x, mouse.y, ref best);
                            if (best.Found && (best.Id != hoveredId || best.Generation != hoveredGeneration))
                            {
                                hoveredId = best.Id; hoveredGeneration = best.Generation;
                                hoveredName = simulation.FindCitizen(best.Id!)?.Identity.Name ?? "";
                                if (!physical.TryWalkerRect(camera, hoveredId!, hoveredGeneration, out hoveredRect)) ClearHover();
                            }
                            else if (!best.Found) ClearHover();
                        }
                    }
                }
                if (selectedId != null && Time.unscaledTime >= nextRefresh) Refresh(simulation);
                updatedFrame = Time.frameCount;
            }
            finally
            {
                long elapsed = Stopwatch.GetTimestamp() - started;
                updateTicks[mode] += elapsed; updateCalls[mode]++; if (elapsed > updateMaxTicks[mode]) updateMaxTicks[mode] = elapsed;
            }
        }

        private void Refresh(CitySimulator simulation)
        {
            long started = Stopwatch.GetTimestamp();
            nextRefresh = Time.unscaledTime + RefreshSeconds;
            insight = simulation.Inspect(selectedId!, null, LocalizedStreet);
            if (insight == null) { body = "This resident has left the city."; personality = homeLabel = workLabel = ""; header = "Resident"; }
            else
            {
                body = insight.Text();
                personality = insight.Personality.Count == 0 ? "" : "Personality: " + string.Join(" · ", insight.Personality);
                header = insight.Name + (insight.Age > 0 ? ", " + insight.Age.ToString(System.Globalization.CultureInfo.InvariantCulture) : "");
                homeLabel = insight.Home == null ? "" : insight.Home.Label + (insight.Home.CanNavigate ? "  ↗" : "");
                workLabel = insight.Work == null ? "" : insight.Work.Label + (insight.Work.CanNavigate ? "  ↗" : "");
            }
            long elapsed = Stopwatch.GetTimestamp() - started;
            builds++; buildTicks += elapsed; if (elapsed > buildMaxTicks) buildMaxTicks = elapsed;
        }

        /// <summary>Called from OnGUI. Draws only cached strings; no picking or content building here.</summary>
        public void Draw()
        {
            long started = Stopwatch.GetTimestamp();
            int mode = (int)Current;
            try
            {
                var input = Event.current;
                if (!visible) return;
                if (hoveredId != null && hoveredId != selectedId)
                {
                    if (input.type == EventType.Repaint) DrawHighlight();
                    // With the guard installed, accept only the press it gave the inspector this frame, so the
                    // game never handles the same click; without it, apply the same checks directly.
                    else if (input.type == EventType.MouseDown && input.button == 0 && hoveredRect.Contains(input.mousePosition)
                        && !(selectedId != null && popup.Contains(input.mousePosition))
                        && (clickGuard != null ? ownedPressFrame == Time.frameCount : NormalView() && !NativeUiUnderPointer()))
                    {
                        Open(hoveredId, input.mousePosition); input.Use();
                    }
                }
                if (selectedId != null)
                {
                    // Without the UI block, native uGUI under the popup owns the pointer: the popup's controls
                    // then ignore that mouse event, so one click never acts on both.
                    if (!UiBlockActive && IsPointerEvent(input.type) && popup.Contains(input.mousePosition) && NativeUiUnderPointer())
                    {
                        if (input.type == EventType.MouseUp) GUIUtility.hotControl = 0;
                        input.Use();
                    }
                    DrawPopup();
                }
            }
            finally
            {
                long elapsed = Stopwatch.GetTimestamp() - started;
                drawTicks[mode] += elapsed; drawCalls[mode]++; if (elapsed > drawMaxTicks[mode]) drawMaxTicks[mode] = elapsed;
            }
        }

        private static bool IsPointerEvent(EventType type) => type == EventType.MouseDown || type == EventType.MouseUp
            || type == EventType.MouseDrag || type == EventType.ScrollWheel || type == EventType.ContextClick;

        private void Open(string id, Vector2 at)
        {
            selectedId = id; insight = null; body = "Loading…"; header = "Resident"; personality = homeLabel = workLabel = mapProblem = ""; nextRefresh = 0; scroll = Vector2.zero;
            float x = Mathf.Clamp(at.x + 24, 10, Math.Max(10, Screen.width - PopupWidth - 10));
            float y = Mathf.Clamp(at.y - PopupHeight * 0.5f, 10, Math.Max(10, Screen.height - PopupHeight - 10));
            popup = new Rect(x, y, PopupWidth, PopupHeight);
        }

        private void DrawHighlight()
        {
            var previous = GUI.color;
            GUI.color = new Color(1f, .85f, .2f, .9f);
            const float edge = 2;
            GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.y, hoveredRect.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.yMax - edge, hoveredRect.width, edge), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(hoveredRect.x, hoveredRect.y, edge, hoveredRect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(hoveredRect.xMax - edge, hoveredRect.y, edge, hoveredRect.height), Texture2D.whiteTexture);
            GUI.color = previous;
            if (hoveredName.Length > 0)
                GUI.Label(new Rect(hoveredRect.x - 60, hoveredRect.y - 22, hoveredRect.width + 120, 20), hoveredName, Title());
        }

        private GUIStyle Title() => title ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 14 };

        private void DrawPopup()
        {
            text ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14 };
            link ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 14, normal = { textColor = new Color(.45f, .75f, 1f) }, hover = { textColor = Color.white } };
            var previous = GUI.color;
            GUI.color = new Color(.075f, .085f, .105f, .97f);
            GUI.DrawTexture(popup, Texture2D.whiteTexture);
            GUI.color = previous;
            GUILayout.BeginArea(popup, GUI.skin.box);
            GUILayout.BeginHorizontal();
            GUILayout.Label(header, Title());
            if (GUILayout.Button("×", GUILayout.Width(28))) { selectedId = null; insight = null; GUILayout.EndHorizontal(); GUILayout.EndArea(); return; }
            GUILayout.EndHorizontal();
            if (insight != null)
            {
                AddressRow("Home", insight.Home, homeLabel);
                AddressRow(insight.WorkRole, insight.Work, workLabel);
                if (personality.Length > 0) GUILayout.Label(personality, text);
                if (mapProblem.Length > 0) GUILayout.Label(mapProblem, text);
            }
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label(body, text);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void AddressRow(string label, ResidentAddress? address, string shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, text, GUILayout.Width(100));
            if (address == null) GUILayout.Label("—", text);
            else if (!address.CanNavigate) GUILayout.Label(shown, text);
            else if (GUILayout.Button(shown, link))
            {
                try { mapProblem = NativeMap.TryFocus(address, out string problem) ? "" : problem; }
                catch (Exception error)
                {
                    // A native map failure must not escape OnGUI mid-layout.
                    mapProblem = "The map could not open.";
                    if (!mapFailureLogged) { mapFailureLogged = true; warn?.Invoke("Resident inspector map link failed: " + error); }
                }
            }
            GUILayout.EndHorizontal();
        }

        // Native street names are localization keys ("ba:street_..."); display the game's own text.
        private static readonly Func<string, string> LocalizedStreet = key =>
        {
            try { return Localizor.LocalizorManager.Localize(key, (object?)null).ToString() ?? key; }
            catch (Exception) { return key; }
        };

        private static double Milliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        private static object Modes(long[] ticks, long[] calls, long[] max) => new
        {
            closed = new { calls = calls[0], totalMilliseconds = Milliseconds(ticks[0]), maxMilliseconds = Milliseconds(max[0]) },
            hover = new { calls = calls[1], totalMilliseconds = Milliseconds(ticks[1]), maxMilliseconds = Milliseconds(max[1]) },
            open = new { calls = calls[2], totalMilliseconds = Milliseconds(ticks[2]), maxMilliseconds = Milliseconds(max[2]) }
        };

        /// <summary>Rig diagnostic: cumulative inspector cost by mode at call start (closed / hover / open).</summary>
        public object Timing
        {
            get
            {
                return new
                {
                    update = Modes(updateTicks, updateCalls, updateMaxTicks),
                    draw = Modes(drawTicks, drawCalls, drawMaxTicks),
                    picks = throttle.Picks,
                    builds, buildMilliseconds = Milliseconds(buildTicks), buildMaxMilliseconds = Milliseconds(buildMaxTicks),
                    hovered = hoveredId, selected = selectedId
                };
            }
        }

        public void ResetTiming()
        {
            Array.Clear(updateTicks, 0, 3); Array.Clear(updateCalls, 0, 3); Array.Clear(updateMaxTicks, 0, 3);
            Array.Clear(drawTicks, 0, 3); Array.Clear(drawCalls, 0, 3); Array.Clear(drawMaxTicks, 0, 3);
            builds = buildTicks = buildMaxTicks = 0;
            throttle.ResetPicks();
        }
    }
}
