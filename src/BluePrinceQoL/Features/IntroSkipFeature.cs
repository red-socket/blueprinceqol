using System.Collections.Generic;
using BluePrinceQoL.Util;
using PmFsm = Il2CppHutongGames.PlayMaker.Fsm;
using Il2CppDG.Tweening;
using Il2CppHutongGames.PlayMaker;
using Il2CppHutongGames.PlayMaker.Actions;
using UnityEngine;

namespace BluePrinceQoL.Features;

/// <summary>
/// Removes the camera glide toward the house at the start of each day.
///
/// Vanilla flow (FSM "FSM" on the scene root object "DAY"):
///   ... -> CURSE CHECK 3 -> "Cameras On" (or "Cameras On Curse")
///        sends "activate" to DAY/Cam Intro 1, CameraFadeIn 5s, Wait 12s
///   -> "Mouse On" (player control, game clock, HUD, ambience ...).
/// DAY/Cam Intro 1 "State 5" teleports the player (FPSController - Prince) to
/// DAY/Cam Intro 1b and DOTweens it along Z (12s) and Y (5s delay, 4.6s) to
/// DAY/Cam Intro 2. That tween is the glide.
///
/// Skip: in "Cameras On" the fade-in and the 12s wait are shortened to HoldTime,
/// then "Mouse On" runs normally. The Cam Intro tween still runs (so the player
/// ends at exactly the vanilla spot) but is zeroed and completed on the spot.
/// No logic state is skipped.
/// The short hold matters: the teleport puts the player in the grounds tile in
/// front of the house, and its trigger fires on the next physics step. In
/// vanilla that happens during the glide, before "Mouse On" activates HUD/Steps
/// and HUD/Room Text, so it is free. If "Mouse On" ran first (the wait switched
/// off entirely, as in 1.0 to 2.4), the tile entry cost a step.
/// The day-1 "watched the whole intro video" path ("Cameras On -Day 1") is
/// left vanilla.
/// </summary>
public class IntroSkipFeature : Feature
{
    public override string Name => "IntroSkip";
    protected override string Description => "Skip the camera glide toward the house at the start of each day";

    private const string DayObject = "DAY";
    private const string CamIntroObject = "Cam Intro 1";
    private static readonly HashSet<string> CameraStates = new() { "Cameras On", "Cameras On Curse" };

    // Real seconds between the teleport and "Mouse On": long enough for the
    // physics steps that register the tile the player lands in.
    private const float HoldTime = 0.3f;
    private readonly StateDurations _durations = new();

    // Set while a skip is in progress: the next Cam Intro tween state is completed.
    private float _armedUntil = -1f;
    private FsmState _tweenState;
    private readonly List<(DotweenMoveTransformTo a, float dur, float delay)> _zeroed = new();

    // Fallbacks for the frames after a skip.
    private PmFsm _dayFsm;
    private string _dayStateName;
    private float _dayDeadline;
    private int _syncFrames;

    private bool _warnedCamIntro, _warnedNoActions, _warnedComplete, _warnedStuck;

    protected override void CreatePrefs()
    {
        FsmHooks.BeforeEnter += OnBeforeEnter;
        FsmHooks.AfterEnter += OnAfterEnter;
    }

    public override void OnSceneLoaded(string sceneName)
    {
        _zeroed.Clear();
        _tweenState = null;
        _dayFsm = null;
        _armedUntil = -1f;
        _syncFrames = 0;
        _durations.RestoreAll();
    }

    private void OnBeforeEnter(FsmState state)
    {
        _durations.OnEnter(state);
        var name = state.Name;
        if (CameraStates.Contains(name))
        {
            var go = state.Fsm?.GameObject;
            if (go == null || go.name != DayObject) return;
            if (!Enabled) return;
            SkipCameraState(state, go);
            return;
        }

        if (_armedUntil < 0f) return;
        if (Time.unscaledTime > _armedUntil) { _armedUntil = -1f; return; }
        if (!Enabled) return;
        var owner = state.Fsm?.GameObject;
        if (owner == null || owner.name != CamIntroObject) return;
        ZeroTweens(state);
    }

    private void OnAfterEnter(FsmState state)
    {
        if (_tweenState == null || state.Pointer != _tweenState.Pointer) return;
        _tweenState = null;
        _armedUntil = -1f;
        CompleteTweens(state);
    }

    // DAY "Cameras On": shorten the fade-in and the 12s wait for this entry.
    private void SkipCameraState(FsmState state, GameObject day)
    {
        // Only skip the wait if the glide itself can be finished; otherwise the
        // player would get control mid-glide.
        if (!CamIntroLooksRight(day))
        {
            if (!_warnedCamIntro)
                Core.Log.Warning($"[{Name}] '{DayObject}/{CamIntroObject}' or its DotweenMoveTransformTo actions not found; leaving the day-start glide alone");
            _warnedCamIntro = true;
            return;
        }

        _durations.Begin(state);
        foreach (var a in state.Actions)
        {
            if (a == null || !a.Enabled) continue;
            if (a.TryCast<Wait>() is { } w) _durations.Scale(w.time, 0f, HoldTime, "Wait");
            else if (a.TryCast<CameraFadeIn>() is { } fi) _durations.Scale(fi.time, 0f, HoldTime, "FadeIn");
        }
        if (_durations.Commit() == null && !_warnedNoActions)
        {
            Core.Log.Warning($"[{Name}] no Wait/CameraFadeIn to shorten in DAY '{state.Name}'; will force FINISHED if it waits");
            _warnedNoActions = true;
        }

        _armedUntil = Time.unscaledTime + 5f;
        _dayFsm = state.Fsm;
        _dayStateName = state.Name;
        _dayDeadline = Time.unscaledTime + HoldTime + 2f;
        Core.Log.Msg($"[{Name}] skipping day-start glide ({state.Name})");
    }

    private bool CamIntroLooksRight(GameObject day)
    {
        var t = day.transform.Find(CamIntroObject);
        if (t == null) return false;
        foreach (var pm in t.GetComponents<Il2Cpp.PlayMakerFSM>())
        {
            var states = pm.Fsm?.States;
            if (states == null) continue;
            foreach (var s in states)
            foreach (var a in s.Actions)
                if (a != null && a.TryCast<DotweenMoveTransformTo>() != null) return true;
        }
        return false;
    }

    // Cam Intro BeforeEnter: tween with zero duration/delay (the game's own
    // instant variant, Cam Intro 1 "State 9", does exactly this).
    private void ZeroTweens(FsmState state)
    {
        _zeroed.Clear();
        foreach (var a in state.Actions)
        {
            var tw = a?.TryCast<DotweenMoveTransformTo>();
            if (tw == null || !tw.Enabled) continue;
            var dur = tw.duration;
            var delay = tw.setDelay;
            _zeroed.Add((tw, dur?.Value ?? 0f, delay?.Value ?? 0f));
            if (dur != null) dur.Value = 0f;
            if (delay != null) delay.Value = 0f;
        }
        if (_zeroed.Count > 0) _tweenState = state;
    }

    // Cam Intro AfterEnter: tweens exist now; jump them to the end.
    private void CompleteTweens(FsmState state)
    {
        var completed = 0;
        var targets = new HashSet<System.IntPtr>();
        foreach (var (tw, dur, delay) in _zeroed)
        {
            if (tw.duration != null) tw.duration.Value = dur;
            if (tw.setDelay != null) tw.setDelay.Value = delay;
            var go = state.Fsm.GetOwnerDefaultTarget(tw.gameObject);
            if (go == null || !targets.Add(go.Pointer)) continue;
            completed += DOTween.Complete(go.transform, true);
        }
        _zeroed.Clear();

        // The player has a CharacterController: push the new position to
        // physics so its next Move() doesn't snap back.
        Physics.SyncTransforms();
        _syncFrames = 3;

        if (completed == 0 && !_warnedComplete)
        {
            Core.Log.Warning($"[{Name}] DOTween.Complete found no tweens on the Cam Intro target; relying on zero-duration tweens");
            _warnedComplete = true;
        }
    }

    public override void OnUpdate()
    {
        _durations.Tick();
        if (_syncFrames > 0)
        {
            _syncFrames--;
            Physics.SyncTransforms();
        }

        if (_dayFsm == null) return;
        if (_dayFsm.ActiveStateName != _dayStateName) { _dayFsm = null; return; }
        if (Time.unscaledTime < _dayDeadline) return;
        var fsm = _dayFsm;
        _dayFsm = null;
        // Normally the shortened wait has finished the state long ago; if
        // something else in it keeps it running, push it on to "Mouse On".
        if (fsm.ActiveStateName == _dayStateName)
        {
            if (!_warnedStuck)
                Core.Log.Warning($"[{Name}] DAY still in '{_dayStateName}' after skip; sending FINISHED");
            _warnedStuck = true;
            fsm.Event(FsmEvent.Finished);
        }
    }
}
