using System;
using System.Collections.Generic;
using BluePrinceQoL.Util;
using Il2CppHutongGames.PlayMaker;
using Il2CppHutongGames.PlayMaker.Actions;
using MelonLoader;
using UnityEngine;
using PmFsm = Il2CppHutongGames.PlayMaker.Fsm;

namespace BluePrinceQoL.Features;

/// <summary>
/// Keeps the crosshair visible while the player can move and click, when a
/// world object's FSM hides it for its own animation.
///
/// Vanilla: Global Manager draws Cursors[NewCursor] (0 = "0-INVIS", 2 = the
/// faint crosshair). Many world objects write NewCursor = 0 (often everyFrame)
/// while they animate without freezing the player, e.g. the Parlor box keyholes:
/// "Click" (0.6 s) and "State 2" (2.2 s) hide the cursor while the player can
/// still walk, look around and click the next keyhole blind.
///
/// What the feature does, per state entry (FsmHooks.BeforeEnter): in a state
/// with a literal SetFsmInt Global Manager "NewCursor" = 0, and only when the
/// player is free (player FSM idle in State 1/7/14/17, "currently frozen" false,
/// Camera.main = the player camera), that action's setValue is changed from 0 to
/// 2 for this entry. It is put back when the state is left, at its next entry,
/// as soon as the player stops being free (an everyFrame write then hides the
/// cursor again, as vanilla), on scene load and when the feature is off.
/// Nothing else is changed: no event, no transition, no other action.
///
/// Never touched (whole FSM): the player and Global Manager FSMs, DRAFT UI,
/// anything under UI OVERLAY CAM (cards, documents, menus, terminals), and any
/// FSM that sends a freeze-type event to the player (Freeze, QuickFreeze,
/// InvisFreeze, Freeze No Retract, PumpFreeze, Camfreeze, disablemove(foundation),
/// CamTarget, CamWarp; disable/enable/Update when sent to the player) or uses
/// SetMainCamera, CameraFadeIn/Out or SetMouseCursor2. Those hides belong to a
/// freeze, a camera move or a fade and stay vanilla.
///
/// Main menu (CursorGuard_MainMenu): its MOUSE MANAGER (scene root, FSM "FSM") hides the cursor on start and only
/// draws one when NewCursor differs from CurrentCursor (both start at 0). Other menu FSMs hide it again later without
/// changing NewCursor: "Main selector enabler" ("Enable", 2.3 s after the menu appears) and "Main Camera/Music"
/// ("Wait for Mouse"). The manager then sees no change and draws nothing until a hover asks for another cursor, so
/// the pointer was gone for several seconds after every main menu load. For MenuWatch seconds after the load, while
/// the manager sits in "Cursor Check" and the cursor is hidden, the feature sets CurrentCursor = -1 (and NewCursor = 1
/// if nothing was asked yet), so the manager redraws its own cursor the next frame. Gamepad input sends the manager
/// to "State 3" (cursor hidden for good), which is left alone. Polled, because at boot the hooks aren't installed yet.
/// The redraw also follows those two hide states (in case the hide doesn't show in Cursor.visible), and it runs with
/// CursorGuard_Enabled off: that switch is the crosshair part ("Keep crosshair"); this part has CursorGuard_MainMenu.
/// </summary>
public class CursorGuardFeature : Feature
{
    public override string Name => "CursorGuard";
    protected override string Description =>
        "Keep the crosshair visible while you can move (objects that hide it during their own animation, e.g. Parlor box keyholes)";

    private const string PlayerPath = "__SYSTEM/FPS Home/FPSController - Prince";
    private const string PlayerObject = "FPSController - Prince";
    private const string PlayerCamera = "FirstPersonCharacter";
    private const string GmObject = "Global Manager";
    private const string CursorVar = "NewCursor";
    private const int Hidden = 0, Crosshair = 2;

    // Player FSM states in which the player walks freely with the world clickable
    // (State 1 = start state before the first freeze of the day).
    private static readonly HashSet<string> PlayerFreeStates = new() { "State 1", "State 7", "State 14", "State 17" };

    private static readonly HashSet<string> FreezeEvents = new()
    {
        "Freeze", "QuickFreeze", "InvisFreeze", "Freeze No Retract", "PumpFreeze", "Camfreeze",
        "disablemove", "disablemovefoundation", "CamTarget", "CamWarp",
    };
    // Generic names that are freezes only when they go to the player.
    private static readonly HashSet<string> PlayerOnlyFreezeEvents = new() { "disable", "enable", "Update" };
    private static readonly HashSet<string> ExcludingActions = new()
        { "SetMainCamera", "CameraFadeOut", "CameraFadeIn", "SetMouseCursor2" };

    private MelonPreferences_Entry<bool> _log, _mainMenu;
    private const string MenuMouseManager = "MOUSE MANAGER";
    private const string MenuIdleState = "Cursor Check";
    private const int MenuPointerCursor = 1;
    private const float MenuWatch = 30f;
    private float _menuUntil = -1f;   // main menu: keep the pointer drawn until then
    private Il2Cpp.PlayMakerFSM _menuMouse;
    // Menu FSMs that hide the cursor without changing NewCursor: path, hide state, last state seen.
    private readonly (string path, string state)[] _menuHiders = { ("Menu System/Main selector enabler", "Enable"), ("Main Camera/Music", "Wait for Mouse") };
    private readonly Il2Cpp.PlayMakerFSM[] _hiderFsms = new Il2Cpp.PlayMakerFSM[2];
    private readonly string[] _hiderSeen = new string[2];
    private bool _redraw;
    private float _nextHiderSearch;

    private sealed class Plan
    {
        public FsmState State;          // keeps the native object alive
        public List<SetFsmInt> Hides;
    }
    private static readonly Plan Nothing = new();

    private sealed class Pending
    {
        public FsmState State;
        public PmFsm Fsm;
        public readonly List<FsmInt> Values = new();
    }

    private readonly Dictionary<IntPtr, Plan> _plans = new();
    private readonly Dictionary<IntPtr, bool> _fsmExcluded = new();
    private readonly Dictionary<IntPtr, Pending> _pending = new();
    private readonly List<IntPtr> _tmp = new();
    private PmFsm _playerFsm;
    private float _nextPlayerSearch;
    private int _swaps;
    private bool _warnedPlan;

    protected override void CreatePrefs()
    {
        _mainMenu = Core.Prefs.CreateEntry("CursorGuard_MainMenu", true, "CursorGuard: main menu pointer",
            "Show the mouse pointer as soon as the main menu loads (the game hides it until the menu has faded in)");
        _log = Core.Prefs.CreateEntry("CursorGuard_Log", false, "CursorGuard: log",
            "Log every hidden-cursor write that is turned into the crosshair (the first 3 are always logged)");
        FsmHooks.BeforeEnter += OnBeforeEnter;
    }

    public override void OnSceneLoaded(string sceneName)
    {
        RestoreAll();
        _plans.Clear();
        _fsmExcluded.Clear();
        _playerFsm = null;
        _nextPlayerSearch = 0f;
        _menuUntil = sceneName == "Main Menu" ? Time.unscaledTime + MenuWatch : -1f;
        _menuMouse = null;
        _redraw = false;
        _nextHiderSearch = 0f;
        for (int i = 0; i < _hiderFsms.Length; i++) { _hiderFsms[i] = null; _hiderSeen[i] = null; }
    }

    // ------------------------------------------------------------------ hooks

    private void OnBeforeEnter(FsmState state)
    {
        var ptr = state.Pointer;
        if (_pending.Count > 0 && _pending.TryGetValue(ptr, out var old)) Restore(ptr, old);

        if (!Enabled)
        {
            if (_pending.Count > 0) RestoreAll();
            return;
        }

        if (!_plans.TryGetValue(ptr, out var plan))
        {
            try { plan = BuildPlan(state); }
            catch (Exception e)
            {
                plan = Nothing;
                if (!_warnedPlan) Core.Log.Warning($"[{Name}] could not analyse state '{state.Name}': {e.Message}");
                _warnedPlan = true;
            }
            _plans[ptr] = plan;
        }
        if (plan == Nothing) return;

        var fsm = state.Fsm;
        if (fsm == null || !PlayerFree()) return;

        Pending pend = null;
        foreach (var a in plan.Hides)
        {
            if (!a.Enabled) continue;
            var v = a.setValue;
            if (v == null || v.Value != Hidden) continue;
            var go = fsm.GetOwnerDefaultTarget(a.gameObject);   // resolved now: usually the "Global Manager" variable
            if (go == null || go.name != GmObject) continue;
            v.Value = Crosshair;
            (pend ??= new Pending { State = state, Fsm = fsm }).Values.Add(v);
        }
        if (pend == null) return;
        _pending[ptr] = pend;

        if (_log.Value || _swaps < 3)
            Core.Log.Msg($"[{Name}] keeping the crosshair: {FsmHooks.PathOf(fsm.GameObject)} :: {fsm.Name} :: {state.Name}");
        _swaps++;
    }

    public override bool UpdateWhenDisabled => true;  // the main menu pointer has its own switch

    public override void OnUpdate()
    {
        if (_menuUntil >= 0f) MenuPointer();
        if (!Enabled) return;
        if (_pending.Count == 0) return;
        bool free = PlayerFree();
        _tmp.Clear();
        foreach (var kv in _pending)
        {
            var p = kv.Value;
            var active = p.Fsm?.ActiveState;
            // State over, or the player got frozen / a camera move started: back to vanilla.
            // An everyFrame hide then writes 0 again on its next update.
            if (!free || active == null || active.Pointer != p.State.Pointer) _tmp.Add(kv.Key);
        }
        foreach (var k in _tmp) Restore(k, _pending[k]);
    }

    private void MenuPointer()
    {
        if (!_mainMenu.Value || Time.unscaledTime > _menuUntil) { _menuUntil = -1f; _menuMouse = null; return; }
        if (_menuMouse == null)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null && g.name == MenuMouseManager) { _menuMouse = FsmUtil.Get(g); break; }
            if (_menuMouse == null) return;
        }
        WatchHiders();
        if (_menuMouse.ActiveStateName != MenuIdleState) return;  // still starting, or gamepad (hidden for good)
        if (Cursor.visible && !_redraw) return;
        _redraw = false;
        var want = FsmUtil.Int(_menuMouse, "NewCursor");
        var shown = FsmUtil.Int(_menuMouse, "CurrentCursor");
        if (want == null || shown == null) { _menuUntil = -1f; return; }
        if (want.Value <= 0) want.Value = MenuPointerCursor;
        shown.Value = -1;  // differs from NewCursor: the manager's "Cursor Update" draws it and unhides the cursor
        if (_log.Value) Core.Log.Msg($"[{Name}] main menu pointer redrawn (cursor {want.Value})");
    }

    // Notes when a menu FSM enters its hide state; the pointer is redrawn after it.
    private void WatchHiders()
    {
        bool search = Time.unscaledTime >= _nextHiderSearch;
        if (search) _nextHiderSearch = Time.unscaledTime + 0.25f;
        for (int i = 0; i < _menuHiders.Length; i++)
        {
            if (_hiderFsms[i] == null)
            {
                if (!search) continue;
                _hiderFsms[i] = FsmUtil.Get(GameObject.Find(_menuHiders[i].path));
                if (_hiderFsms[i] == null) continue;
            }
            var st = _hiderFsms[i].ActiveStateName;
            if (st == _hiderSeen[i]) continue;
            _hiderSeen[i] = st;
            if (st == _menuHiders[i].state) _redraw = true;
        }
    }

    // ------------------------------------------------------------- restore

    private void Restore(IntPtr ptr, Pending p)
    {
        _pending.Remove(ptr);
        foreach (var v in p.Values)
            try { if (v.Value == Crosshair) v.Value = Hidden; } catch { /* object gone */ }
    }

    private void RestoreAll()
    {
        if (_pending.Count == 0) return;
        _tmp.Clear();
        _tmp.AddRange(_pending.Keys);
        foreach (var k in _tmp) Restore(k, _pending[k]);
    }

    // ------------------------------------------------------------- player

    /// <summary>The player walks freely and can click the world.</summary>
    private bool PlayerFree()
    {
        var pl = PlayerFsm();
        if (pl == null) return false;
        var s = pl.ActiveStateName;
        if (s == null || !PlayerFreeStates.Contains(s)) return false;
        var frozen = pl.Variables?.GetFsmBool("currently frozen");
        if (frozen == null || frozen.Value) return false;
        var cam = Camera.main;
        return cam != null && cam.gameObject.name == PlayerCamera;
    }

    private PmFsm PlayerFsm()
    {
        if (_playerFsm != null) return _playerFsm;
        if (Time.unscaledTime < _nextPlayerSearch) return null;
        _nextPlayerSearch = Time.unscaledTime + 1f;
        return _playerFsm = FsmUtil.Find(PlayerPath)?.Fsm;
    }

    // ------------------------------------------------------------ analysis

    private Plan BuildPlan(FsmState state)
    {
        var fsm = state.Fsm;
        var actions = state.Actions;
        if (fsm == null || actions == null) return Nothing;

        List<SetFsmInt> hides = null;
        foreach (var a in actions)
        {
            if (a == null || !a.Enabled) continue;
            var s = a.TryCast<SetFsmInt>();
            if (s == null || s.variableName?.Value != CursorVar) continue;
            if (s.fsmName != null && !string.IsNullOrEmpty(s.fsmName.Value) && s.fsmName.Value != "FSM") continue;
            var v = s.setValue;
            // Literal 0 only (a shared FSM variable is never changed).
            if (v == null || (v.UseVariable && !string.IsNullOrEmpty(v.Name)) || v.Value != Hidden) continue;
            (hides ??= new()).Add(s);
        }
        if (hides == null || FsmExcluded(fsm)) return Nothing;
        return new Plan { State = state, Hides = hides };
    }

    private bool FsmExcluded(PmFsm fsm)
    {
        var ptr = fsm.Pointer;
        if (_fsmExcluded.TryGetValue(ptr, out var ex)) return ex;
        ex = ComputeExcluded(fsm);
        _fsmExcluded[ptr] = ex;
        return ex;
    }

    private static bool ComputeExcluded(PmFsm fsm)
    {
        var go = fsm.GameObject;
        if (go == null) return true;
        if (go.name == PlayerObject || go.name == GmObject || go.name == "DRAFT UI") return true;
        for (var t = go.transform; t != null; t = t.parent)
            if (t.name == "UI OVERLAY CAM") return true;

        var states = fsm.States;
        if (states == null) return true;
        foreach (var s in states)
        {
            if (s?.Actions == null) continue;
            foreach (var a in s.Actions)
            {
                if (a == null || !a.Enabled) continue;
                var tn = a.GetIl2CppType().Name;
                if (ExcludingActions.Contains(tn)) return true;
                if (tn != "SendEvent") continue;
                var se = a.Cast<SendEvent>();
                var ev = se.sendEvent?.Name;
                if (ev == null) continue;
                if (FreezeEvents.Contains(ev)) return true;
                if (PlayerOnlyFreezeEvents.Contains(ev) && MaybePlayer(fsm, se.eventTarget)) return true;
            }
        }
        return false;
    }

    // True when the target is (or might be) the player: unresolved targets count as the player.
    private static bool MaybePlayer(PmFsm fsm, FsmEventTarget et)
    {
        if (et == null) return false;
        if (et.target == FsmEventTarget.EventTarget.BroadcastAll) return true;
        if (et.target != FsmEventTarget.EventTarget.GameObject && et.target != FsmEventTarget.EventTarget.GameObjectFSM)
            return false;
        GameObject go;
        try { go = fsm.GetOwnerDefaultTarget(et.gameObject); } catch { return true; }
        return go == null || go.name.StartsWith("FPSController", StringComparison.Ordinal);
    }
}
