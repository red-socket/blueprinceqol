using System;
using System.Collections.Generic;
using BluePrinceQoL.Util;
using Il2Cpp;
using Il2CppBluePrince.Inputs;
using Il2CppBluePrince.UI;
using Il2CppHutongGames.PlayMaker.Actions;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace BluePrinceQoL.Features;

/// <summary>
/// A "MODS" entry in the Settings list (pause menu and main menu) that opens an options screen built from the game's own
/// Accessibility screen, so it has the game's fonts, colours, sounds, fades and mouse/keyboard/gamepad controls.
///
/// Built once per scene load (pause menu objects of scene Mount Holly Estate, all found under UI OVERLAY CAM/PAUSE):
/// - Screen: a copy of "ACCESSIBILITY SUBMENU" (UIMenuManager with a page list "ACCESSIBILITY SETTINGS" and pages under
///   "ACCESSIBILITY SUBMENUS"), made in an inactive holder and stripped before anything on it runs: every PlayMakerFSM,
///   LocalizedText (would overwrite our labels), the settings binders/managers (any type named *Settings*, e.g.
///   UIMotionAssistSettings) and EnableGameobjectOnSelection. The "MOTION ASSIST SETTINGS" page is the template for our
///   pages (title, intro, checkbox rows, description panel on the right); the other pages are removed.
/// - Page list buttons and checkboxes get our own UnityEvents / delegates. A page's Back still goes through the copy's
///   own UISubmenu.Close (page list), the page list's Back goes to our Close (Settings list again).
/// - Entry: a copy of SETTINGS/Vertical layout/VOLUME (FSM removed), placed before BACK in the list's UIVerticalLayout
///   and in the list's ControllerSelector.
/// Settings change the MelonPreferences entries directly and are saved right away; features read them live.
/// If anything is missing or a step throws, the half-built objects are removed and one warning is logged: the config
/// file and hotkeys keep working.
///
/// Main menu (scene Main Menu): the Settings list is MAIN MENU/SETTINGS LAYOUT (rows under "Layout script", a copy of
/// "Volume Click" becomes MODS, before "Return Click") and the screen is a copy of the scene-root ACCESSIBILITY SUBMENU.
/// The game's own open/close FSMs are replaced by code doing what they do: open = click sound, Settings list off, the UI
/// overlay camera becomes Camera.main (selectors raycast through it), screen on; close = black fade (UIMenuManager),
/// then Main Camera back, the menu frame lines and "Settings" line on, Settings list on (its FSM re-enables its
/// selector and BACK BUTTON), screen off.
///
/// Eight pages of checkboxes and sliders.
/// </summary>
public class ModsMenuFeature : Feature
{
    public override string Name => "ModsMenu";
    protected override string Description => "MODS entry in the pause menu's Settings with the mod's options";

    private const string ScreenName = "MODS SUBMENU";
    private const string EntryName = "MODS";
    private static readonly float[] BuildTries = { 1f, 3f, 6f, 10f };  // seconds after scene load
    // Row spacing, from the game's Motion Assist page.
    private const float ToggleSpacing = 0.89f, SliderSpacing = 1.38f, MixedSpacing = 1.07f;
    private const float SliderStartDrop = 0.96f;  // a slider's value text sits above it: start lower than a checkbox
    private const float SaveDelay = 0.5f;        // sliders change every frame while dragged
    private const float TitleScale = 1.5f, ListDrop = 0.35f, ListSpacingScale = 0.85f;  // page list

    private static readonly Color EntryNormal = new(0.851f, 0.851f, 0.851f, 1f);
    private static readonly Color EntryHover = new(0.412f, 0.796f, 1f, 1f);

    private sealed class Option
    {
        public string Label, Description, Pref;
        public bool IsSlider;
        public float Min, Max, Step;
        public string Format = "F1";
        public UIToggle Toggle;
        public UISlider Slider;
        public ControllerSelectable Selectable;
    }

    private static Option Check(string label, string pref, string description) =>
        new() { Label = label, Pref = pref, Description = description };

    private static Option Speed(string label, string pref, string description, float max = 10f) =>
        new() { Label = label, Pref = pref, Description = description, IsSlider = true, Min = 1f, Max = max, Step = 0.5f };

    private sealed class Page
    {
        public string Title, Intro;
        public List<Option> Options;
        public UISubmenu Submenu;
        public ControllerSelector Selector;
        public TMP_Text DescriptionText;
        public bool WasOpen;
    }

    private const string SpeedIntro = "Speed multipliers. 1.0 is the game's normal speed.";

    private readonly List<Page> _pages = new()
    {
        new Page
        {
            Title = "DISPLAY", Intro = "What the mod shows on screen.",
            Options = new()
            {
                Check("Clock HUD", "Hud_Visible",
                    "Shows the in-game day, date and time at the top of the screen. F8 also shows or hides it."),
                Check("24-hour clock", "Hud_Use24Hour", "Shows the time as 14:30 instead of 2:30 PM."),
                Check("Calendar date", "Hud_ShowDate", "Shows the date next to the day, for example Sat Nov 7."),
                Check("Run stats", "RunStats_Visible",
                    "Adds a line under the clock with today's rooms, rank, colours, dead ends, digs, items and doors. F10 also shows or hides it."),
                Check("Keep crosshair", "CursorGuard_Enabled",
                    "Keeps the crosshair visible while an object animates and you can still move, for example while a Parlor box's key turns."),
                Check("Dart equation", "DartsEquation_Enabled",
                    "Shows the Billiard Room dartboard puzzle as an equation at the bottom of the screen, with each sign in the colour of its sectors. The board's odd numbers turn slightly red."),
            },
        },
        new Page
        {
            Title = "SKIPS", Intro = "Animations and screens the mod skips.",
            Options = new()
            {
                Check("Boot logos", "BootSkip_Enabled",
                    "Skips the Dogubomb and Raw Fury logos when the game starts. Takes effect the next time you start the game."),
                Check("Day intro", "IntroSkip_Enabled",
                    "Skips the camera glide at the start of each day, so you start at the gate straight away."),
                Check("Pickup cards", "PickupSkip_Enabled",
                    "Skips item pickup animations and the You Found and You Bought cards. Items are granted as normal. F9 unfreezes you if you ever get stuck."),
                Check("Click unlock", "ClickUnlock_Enabled",
                    "Lets you click again right after pickups, menus and doors instead of waiting about a second."),
            },
        },
        new Page
        {
            Title = "ANIMATIONS", Intro = SpeedIntro,
            Options = new()
            {
                Speed("Camera zoom", "CameraZoom_Speed",
                    "Zooming in and out of terminals, wall safes, letter locks, the Dining Room plate and the Utility Closet fuse box. Also the safe door opening after the right code."),
                Speed("Door keys", "DoorKeySpeed_Speed", "The key animation when you unlock a door. You still spend the key only once."),
                Speed("Digging", "DigSpeed_Speed", "The fade to black when you dig."),
            },
        },
        new Page
        {
            Title = "ROOMS", Intro = SpeedIntro,
            Options = new()
            {
                Speed("Secret Garden", "SecretGarden_Speed", "Turning the cranks in the Secret Garden."),
                Speed("Rotunda", "Rotunda_Speed", "The Rotunda turning. Its sound fades out as the room stops."),
                Speed("Elevators", "ElevatorSpeed_Speed",
                    "Elevator rides in the Boiler Room, the Chamber of Mirrors, the Foundation and on the cliff.", 6f),
            },
        },
        new Page
        {
            Title = "MOVEMENT", Intro = "Walking and running speed. 1.00 is the game's normal speed.",
            Options = new()
            {
                new Option { Label = "Walk speed", Pref = "MoveSpeed_WalkMultiplier", IsSlider = true,
                    Min = 0.5f, Max = 3f, Step = 0.05f, Format = "F2",
                    Description = "Speed while you hold the stroll key. Step costs don't change." },
                new Option { Label = "Run speed", Pref = "MoveSpeed_RunMultiplier", IsSlider = true,
                    Min = 0.5f, Max = 3f, Step = 0.05f, Format = "F2",
                    Description = "Your normal speed, including running shoes. Step costs don't change." },
                Speed("Stairs warps", "StairsWarp_Speed",
                    "The black screen when you use Stairs Skip (Settings > Accessibility > Motion Assist). 1.0 is the game's normal speed."),
            },
        },
        new Page
        {
            Title = "END OF DAY", Intro = "The fades and diary between one day and the next.",
            Options = new()
            {
                Speed("Speed", "EndOfDay_Speed",
                    "How many times faster the end-of-day fades and diary run. 1.0 is the game's normal speed. Save & Continue still waits for you."),
                Check("Stats all at once", "EndOfDay_StatsAllAtOnce",
                    "The diary's stat rows appear together instead of one after another."),
                Check("Morning fade", "EndOfDay_MorningFade", "Also shortens the fade at the start of the next day."),
            },
        },
        new Page
        {
            Title = "CUTSCENES", Intro = "Story scenes and longer animations.",
            Options = new()
            {
                Speed("Workshop", "Workshop_Speed",
                    "The black screen while an item is assembled at the Workshop table. 1.0 is the game's normal speed."),
                Speed("Office safe", "OfficeCompartment_Speed",
                    "The bust in the Office rising to reveal the safe. 1.0 is the game's normal speed."),
                Check("Security scene", "SecurityVideo_Enabled",
                    "Skips the scene that plays the first time you enter the Security room. It only plays once per save."),
                Check("Skip Dare poem", "DareGameOver_AutoSkip",
                    "Always skips the poem in the Dare mode Game Over scene. Without this, press Space, Enter, Esc or click to skip it."),
            },
        },
        new Page
        {
            Title = "TERMINALS", Intro = "Text and keyboard controls on computer terminals.",
            Options = new()
            {
                Check("Instant text", "Terminal_InstantText", "Terminal text appears at once instead of being typed out."),
                Check("Number keys", "Terminal_NumberKeys", "Keys 1 to 9 pick a menu option."),
                Check("Option numbers", "Terminal_ShowNumbers", "Shows 1., 2., 3. in front of menu options."),
                Check("Back keys", "Terminal_BackKeys", "Esc and Backspace go back one screen."),
                Check("Esc closes", "Terminal_EscapeClosesTerminal", "Esc on a terminal's start screen closes the terminal."),
            },
        },
    };

    private enum Mode { None, Pause, Main }
    private Mode _mode;

    private GameObject _screen, _entry, _settings, _systemMenu;
    private TMP_Text _entryText;

    // Main menu only
    private const float MainCloseDelay = 0.6f;  // as the game's screens: black fade, then back to the menu
    private static readonly Color MainHover = new(0.764f, 0.764f, 0.764f, 1f);
    private const float MainFont = 2.5f, MainHoverFont = 2.8f, HoverVolume = 0.015f, ClickVolume = 0.15f;
    private GameObject _mainCam, _overlayCam, _lines, _settingsLine;
    private TMP_Text _lineText;
    private string _lineValue;
    private Color _mainNormal;
    private AudioClip _hoverClip, _clickClip;
    private float _mainCloseAt = -1f;
    private float _sceneAt;
    private int _try = -1;
    private int _closeFrame = -1;
    private bool _warned;
    private float _saveAt = -1f;

    public override void OnSceneLoaded(string sceneName)
    {
        if (_saveAt >= 0f) { _saveAt = -1f; MelonPreferences.Save(); }
        _screen = _entry = _settings = _systemMenu = null;
        _mainCam = _overlayCam = _lines = _settingsLine = null;
        _lineText = null;
        _mainCloseAt = -1f;
        _mode = sceneName switch { "Mount Holly Estate" => Mode.Pause, "Main Menu" => Mode.Main, _ => Mode.None };
        foreach (var p in _pages) { p.Submenu = null; p.Selector = null; p.DescriptionText = null; p.WasOpen = false; }
        _closeFrame = -1;
        _try = _mode != Mode.None ? 0 : -1;
        _sceneAt = Time.unscaledTime;
    }

    public override void OnUpdate()
    {
        if (_try >= 0 && _try < BuildTries.Length && Time.unscaledTime - _sceneAt >= BuildTries[_try])
        {
            var last = _try == BuildTries.Length - 1;
            _try++;
            if (_mode == Mode.Pause) TryBuild(last);
            else if (_mode == Mode.Main) TryBuildMain(last);
        }
        if (_screen == null) return;

        if (_mainCloseAt >= 0f && Time.unscaledTime >= _mainCloseAt) { _mainCloseAt = -1f; FinishMainClose(); }

        // Back from the page list: show the Settings list one frame later, so the same Back press can't reach it.
        if (_closeFrame >= 0 && Time.frameCount >= _closeFrame)
        {
            _closeFrame = -1;
            _screen.SetActive(false);
            if (_settings != null) _settings.SetActive(true);
        }

        // The pause menu was closed or reset (Esc menu's "RESET SYSTEM MENU" doesn't know our screen).
        if (_mode == Mode.Pause && _screen.activeSelf && _systemMenu != null && _systemMenu.activeInHierarchy)
            _screen.SetActive(false);

        foreach (var p in _pages) UpdatePage(p);

        if (_saveAt >= 0f && Time.unscaledTime >= _saveAt) { _saveAt = -1f; MelonPreferences.Save(); }
    }

    // ------------------------------------------------------------------ pages at runtime

    private void UpdatePage(Page p)
    {
        if (p.Selector == null) return;
        var open = p.Selector.gameObject.activeInHierarchy;
        if (open && !p.WasOpen) RefreshValues(p);
        p.WasOpen = open;
        if (!open || p.DescriptionText == null) return;

        var cur = p.Selector.CurrentSelection;
        string text = "";
        if (cur != null)
            foreach (var o in p.Options)
                if (o.Selectable != null && o.Selectable.Pointer == cur.Pointer) { text = o.Description; break; }
        if (p.DescriptionText.text != text) p.DescriptionText.text = text;
    }

    private static void RefreshValues(Page p)
    {
        foreach (var o in p.Options)
        {
            if (o.Toggle != null)
            {
                var v = GetBool(o);
                if (o.Toggle._isOn == v) continue;
                o.Toggle._isOn = v;     // silently: IsOn/SetState would fire OnValueChanged
                o.Toggle.RefreshUI();
            }
            else if (o.Slider != null)
            {
                var v = GetFloat(o);
                o.Slider._lastValue = v; // silently: the Value setter fires only when it differs from _lastValue
                o.Slider.Value = v;
            }
        }
    }

    private static bool GetBool(Option o)
    {
        var e = Core.Prefs.GetEntry<bool>(o.Pref);
        return e != null && e.Value;
    }

    private void SetBool(Option o, bool on)
    {
        var e = Core.Prefs.GetEntry<bool>(o.Pref);
        if (e == null) return;
        if (e.Value == on) return;
        e.Value = on;
        MelonPreferences.Save();
        Core.Log.Msg($"[{Name}] {o.Pref} = {on}");
    }

    private static float GetFloat(Option o)
    {
        var e = Core.Prefs.GetEntry<float>(o.Pref);
        var v = e != null ? e.Value : o.Min;
        return float.IsNaN(v) ? o.Min : Mathf.Clamp(v, o.Min, o.Max);
    }

    private void SetFloat(Option o, float raw)
    {
        var e = Core.Prefs.GetEntry<float>(o.Pref);
        if (e == null || float.IsNaN(raw)) return;
        // The slider passes the unclamped, unrounded value (and calls on every frame while dragged).
        var v = Mathf.Clamp(Mathf.Round(raw / o.Step) * o.Step, o.Min, o.Max);
        v = (float)Math.Round(v, 2);
        if (Mathf.Approximately(e.Value, v)) return;
        e.Value = v;
        _saveAt = Time.unscaledTime + SaveDelay;
    }

    private void Open()
    {
        if (_mode == Mode.Main) { OpenMain(); return; }
        if (_screen == null || _settings == null) return;
        if (_entryText != null) _entryText.color = EntryNormal;
        _screen.SetActive(true);
        _settings.SetActive(false);
    }

    private void Close()
    {
        if (_mode == Mode.Main) { CloseMain(); return; }
        _closeFrame = Time.frameCount + 1;
    }

    // ------------------------------------------------------------------ main menu open/close

    private void OpenMain()
    {
        if (_screen == null || _settings == null || _screen.activeSelf) return;
        Play(_clickClip, ClickVolume);
        if (_entryText != null) { _entryText.color = _mainNormal; _entryText.fontSize = MainFont; }
        if (_lineText != null) _lineValue = _lineText.text;  // the Settings list's frame; the game resets it on close
        _settings.SetActive(false);
        SetMainCamera(_overlayCam, _mainCam);
        _screen.SetActive(true);
    }

    private void CloseMain()
    {
        if (_screen == null || _mainCloseAt >= 0f) return;
        try { _screen.GetComponent<UIMenuManager>()?.RequestBlackFadeIn(); }
        catch (Exception e) { Core.Log.Msg($"[{Name}] black fade: {e.Message}"); }
        _mainCloseAt = Time.unscaledTime + MainCloseDelay;
    }

    private void FinishMainClose()
    {
        SetMainCamera(_mainCam, _overlayCam);
        if (_lines != null) _lines.SetActive(true);
        if (_lineText != null && _lineValue != null) _lineText.text = _lineValue;
        if (_settingsLine != null) _settingsLine.SetActive(true);
        if (_settings != null) _settings.SetActive(true);
        _screen.SetActive(false);
    }

    // PlayMaker's SetMainCamera: the old main camera loses the tag, the new one gets it.
    private static void SetMainCamera(GameObject to, GameObject from)
    {
        if (to == null) return;
        if (from != null && from.CompareTag("MainCamera")) from.tag = "Untagged";
        to.tag = "MainCamera";
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _entry == null) return;
        AudioSource.PlayClipAtPoint(clip, _entry.transform.position, volume);
    }

    // ------------------------------------------------------------------ build

    private void TryBuild(bool last)
    {
        var overlay = GameObject.Find("UI OVERLAY CAM");
        var pause = overlay != null ? overlay.transform.Find("PAUSE") : null;
        var settings = pause?.Find("SETTINGS");
        var layout = settings?.Find("Vertical layout");
        var volume = layout?.Find("VOLUME");
        var back = layout?.Find("BACK");
        var access = pause?.Find("ACCESSIBILITY SUBMENU");
        var system = pause?.Find("SYSTEM MENU");
        if (volume == null || back == null || access == null || system == null)
        {
            if (last) Warn("the pause menu's Settings list or Accessibility screen wasn't found");
            return;
        }
        _try = BuildTries.Length; // found: build now, never retry
        if (pause.Find(ScreenName) != null || layout.Find(EntryName) != null) return;

        GameObject screen = null, entry = null;
        try
        {
            screen = BuildScreen(access.gameObject, pause);
            entry = BuildEntry(volume.gameObject, back.gameObject, layout, settings.gameObject);
            _screen = screen;
            _entry = entry;
            _settings = settings.gameObject;
            _systemMenu = system.gameObject;
            Core.Log.Msg($"[{Name}] MODS added to the pause menu's Settings");
        }
        catch (Exception e)
        {
            if (entry != null) Object.Destroy(entry);
            if (screen != null) Object.Destroy(screen);
            _screen = _entry = null;
            Warn($"couldn't build the MODS screen: {e}");
        }
    }

    private void TryBuildMain(bool last)
    {
        GameObject Root(string name)
        {
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (g != null && g.name == name) return g;
            return null;
        }

        var menu = Root("MAIN MENU");
        var settings = menu != null ? menu.transform.Find("SETTINGS LAYOUT") : null;
        var layout = settings?.Find("Layout script");
        var volume = layout?.Find("Volume Click");
        var ret = layout?.Find("Return Click");
        var access = Root("ACCESSIBILITY SUBMENU");
        _mainCam = Root("Main Camera");
        _overlayCam = Root("UI OVERLAY CAM");
        if (volume == null || ret == null || access == null || _mainCam == null || _overlayCam == null)
        {
            if (last) Warn("the main menu's Settings list or Accessibility screen wasn't found");
            return;
        }
        _try = BuildTries.Length;
        if (layout.Find(EntryName + " Click") != null) return;

        _lines = menu.transform.Find("MAINMENU LAYOUT/Lines")?.gameObject;
        var line = menu.transform.Find("MAINMENU LAYOUT/Lines/Line");
        _lineText = line != null ? line.GetComponent<TMP_Text>() : null;
        _settingsLine = menu.transform.Find("VERSION LINE/Settings Text")?.gameObject;
        FindSounds(volume.gameObject);

        GameObject screen = null, entry = null;
        try
        {
            screen = BuildScreen(access, null);
            entry = BuildMainEntry(volume.gameObject, ret.gameObject, layout, settings.gameObject);
            _screen = screen;
            _entry = entry;
            _settings = settings.gameObject;
            Core.Log.Msg($"[{Name}] MODS added to the main menu's Settings");
        }
        catch (Exception e)
        {
            if (entry != null) Object.Destroy(entry);
            if (screen != null) Object.Destroy(screen);
            _screen = _entry = null;
            Warn($"couldn't build the main menu's MODS screen: {e}");
        }
    }

    /// <summary>The hover and click sounds of the game's row (PlaySound actions in its FSM), by volume.</summary>
    private void FindSounds(GameObject row)
    {
        try
        {
            foreach (var pm in row.GetComponents<PlayMakerFSM>())
            foreach (var st in pm.FsmStates)
            {
                if (st?.Actions == null) continue;
                foreach (var a in st.Actions)
                {
                    var ps = a?.TryCast<PlaySound>();
                    var clip = ps?.clip?.Value?.TryCast<AudioClip>();
                    if (clip == null) continue;
                    if (ps.volume.Value < 0.05f) _hoverClip ??= clip;
                    else _clickClip ??= clip;
                }
            }
        }
        catch (Exception e) { Core.Log.Msg($"[{Name}] menu sounds from FSM: {e.Message}"); }

        if (_hoverClip != null && _clickClip != null) return;
        foreach (var c in Resources.FindObjectsOfTypeAll<AudioClip>())  // fallback: the clips by name
        {
            if (c == null) continue;
            if (c.name == "Pickup&Clicks66 - Mono") _hoverClip ??= c;
            else if (c.name == "Pickup&Clicks02 - Mono") _clickClip ??= c;
        }
    }

    private GameObject BuildMainEntry(GameObject volume, GameObject ret, Transform layout, GameObject settings)
    {
        GameObject entry = null;
        CloneInactive(volume, EntryName + " Click", go =>
        {
            entry = go;
            Strip(go);
            TMP_Text label = null;
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true))
                if (t.name.EndsWith(" TEXT")) { label = t; break; }
            _entryText = label ?? throw new Exception("Volume Click has no label");
            _entryText.gameObject.name = "MODS TEXT";
            _entryText.text = EntryName;
            _mainNormal = _entryText.color;
            _entryText.fontSize = MainFont;

            var cs = go.GetComponent<ControllerSelectable>() ?? throw new Exception("Volume Click has no ControllerSelectable");
            cs.TargetFSM = null;
            if (cs.ExplicitNavigation != null) cs.ExplicitNavigation.UseExplicit = false;
            cs.OnSelect = Event(() =>
            {
                if (_entryText != null) { _entryText.color = MainHover; _entryText.fontSize = MainHoverFont; }
                Play(_hoverClip, HoverVolume);
            });
            cs.OnDeselect = Event(() =>
            {
                if (_entryText != null) { _entryText.color = _mainNormal; _entryText.fontSize = MainFont; }
            });
            cs.OnUse = Event(Open);

            go.transform.SetParent(layout, false);
            go.transform.SetSiblingIndex(ret.transform.GetSiblingIndex());
            go.SetActive(true);

            var vl = layout.GetComponent<UIVerticalLayout>();
            if (vl != null) { vl.Gameobjects = InsertBefore(vl.Gameobjects, go, ret); vl.RefreshLayout(); }

            var sel = settings.GetComponent<ControllerSelector>();
            var retSel = ret.GetComponent<ControllerSelectable>();
            if (sel != null && retSel != null) sel.Selectables = InsertBefore(sel.Selectables, cs, retSel);
            else Core.Log.Warning($"[{Name}] MODS can't be reached with keyboard/gamepad in the main menu; mouse only");
        });
        return entry;
    }

    private void Warn(string msg)
    {
        if (_warned) return;
        _warned = true;  // once per game session
        Core.Log.Warning($"[{Name}] {msg}. The options are still in UserData/MelonPreferences.cfg.");
    }

    /// <summary>Copy in an inactive holder, so nothing on the copy runs before it is stripped.</summary>
    private static GameObject CloneInactive(GameObject src, string name, Action<GameObject> prepare)
    {
        var holder = new GameObject("QoL clone holder");
        holder.SetActive(false);
        try
        {
            var go = Object.Instantiate(src, holder.transform);
            go.name = name;
            prepare(go);
            return go;
        }
        finally { Object.Destroy(holder); }
    }

    private static void Strip(GameObject go)
    {
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null) continue;
            var type = mb.GetIl2CppType().Name;
            if (mb.TryCast<PlayMakerFSM>() != null || type.Contains("Localiz") || type.Contains("Settings")
                || type == "EnableGameobjectOnSelection")
                Object.DestroyImmediate(mb);
        }
    }

    private static Transform Need(Transform parent, string path)
    {
        var t = parent.Find(path);
        if (t == null) throw new Exception($"'{path}' not found under '{parent.name}'");
        return t;
    }

    private static TMP_Text Text(Transform t) =>
        t.GetComponent<TMP_Text>() ?? throw new Exception($"'{t.name}' has no text");

    private static void Destroy(Transform t)
    {
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    private static UnityEvent Event(Action a)
    {
        var ev = new UnityEvent();
        ev.AddListener((UnityAction)a);
        return ev;
    }

    private GameObject BuildScreen(GameObject access, Transform parent)
    {
        GameObject root = null;
        CloneInactive(access, ScreenName, go =>
        {
            root = go;
            Strip(go);
            var list = Need(go.transform, "ACCESSIBILITY SETTINGS");
            var pagesRoot = Need(go.transform, "ACCESSIBILITY SUBMENUS");
            var template = Need(pagesRoot, "MOTION ASSIST SETTINGS");
            for (int i = pagesRoot.childCount - 1; i >= 0; i--)
                if (pagesRoot.GetChild(i) != template) Destroy(pagesRoot.GetChild(i));

            // Pages first (each a copy of the template), then the page list that opens them.
            foreach (var p in _pages)
            {
                var pt = Object.Instantiate(template.gameObject, pagesRoot).transform;
                pt.name = "MODS " + p.Title;
                BuildPage(p, pt);
            }
            Destroy(template);
            BuildPageList(list);

            go.transform.SetParent(parent, false);  // still inactive: opened by the MODS entry (null = scene root)
            go.transform.localPosition = access.transform.localPosition;
            go.SetActive(false);
        });
        return root;
    }

    private void BuildPage(Page p, Transform page)
    {
        p.Submenu = page.GetComponent<UISubmenu>() ?? throw new Exception("page has no UISubmenu");
        p.Selector = Need(page, "Motion settings selector").GetComponent<ControllerSelector>()
                     ?? throw new Exception("page has no ControllerSelector");

        Text(Need(page, "Title/Motion Assist Settings")).text = p.Title;

        // Two children are called "Descriptions": the intro text (child "motion assist") and the panel on the right.
        Transform panel = null;
        for (int i = 0; i < page.childCount; i++)
        {
            var c = page.GetChild(i);
            if (c.name != "Descriptions") continue;
            var intro = c.Find("motion assist");
            if (intro == null) { panel = c; continue; }
            var it = Text(intro);
            it.text = p.Intro;
            it.alignment = TextAlignmentOptions.Center;  // the game's is justified, which spreads out a short line
        }
        if (panel == null) throw new Exception("description panel not found");
        var keep = Need(panel, "Look Description");
        for (int i = panel.childCount - 1; i >= 0; i--)
            if (panel.GetChild(i) != keep) Destroy(panel.GetChild(i));
        keep.gameObject.SetActive(true);
        Destroy(keep.Find("Image?"));
        p.DescriptionText = Text(Need(keep, "Description"));
        p.DescriptionText.text = "";

        // Rows: copies of "Toggle (Smooth_look)" and "Slider (Look_sensitivity)", from where the first row was,
        // spaced like the game's Motion Assist page.
        var toggleTemplate = Need(page, "Toggle (Smooth_look)");
        var sliderTemplate = Need(page, "Slider (Look_sensitivity)");
        var y = Need(page, "Cursor Set (Link_button)").localPosition.y;
        Destroy(page.Find("Cursor Set (Link_button)"));
        Destroy(page.Find("Slider (FoV)"));
        Destroy(page.Find("Toggle (Stairs_skip)"));

        var selectables = new Il2CppReferenceArray<ControllerSelectable>(p.Options.Count);
        for (int i = 0; i < p.Options.Count; i++)
        {
            var o = p.Options[i];
            if (i == 0) { if (o.IsSlider) y -= SliderStartDrop; }
            else
            {
                var prev = p.Options[i - 1];
                y -= prev.IsSlider && o.IsSlider ? SliderSpacing : !prev.IsSlider && !o.IsSlider ? ToggleSpacing : MixedSpacing;
            }
            var template = o.IsSlider ? sliderTemplate : toggleTemplate;
            var row = Object.Instantiate(template.gameObject, page).transform;
            row.name = "QoL " + o.Label;
            var pos = template.localPosition;
            row.localPosition = new Vector3(pos.x, y, pos.z);
            Destroy(row.Find("Default text"));
            Text(Need(row, o.IsSlider ? "Sens label new/Headline Text 1" : "Toggle Label/Headline Text 1")).text = o.Label;

            var opt = o;
            if (o.IsSlider)
            {
                o.Slider = row.GetComponentInChildren<UISlider>(true) ?? throw new Exception("row has no UISlider");
                o.Slider.minimum = o.Min;
                o.Slider.maximum = o.Max;
                o.Slider.FormatValue = o.Format;
                o.Slider.NavigationStepIncrement = o.Step;
                var v = GetFloat(o);
                o.Slider.currentValue = v;
                o.Slider._lastValue = v;
                o.Slider.OnValueChanged = (Action<float>)(f => SetFloat(opt, f));
                o.Selectable = o.Slider.GetComponent<ControllerSelectable>();
            }
            else
            {
                o.Toggle = row.GetComponentInChildren<UIToggle>(true) ?? throw new Exception("row has no UIToggle");
                o.Toggle._isOn = GetBool(o);
                o.Toggle.OnValueChanged = (Action<bool>)(v => SetBool(opt, v));
                o.Selectable = o.Toggle.GetComponent<ControllerSelectable>();
            }
            o.Selectable ??= row.GetComponentInChildren<ControllerSelectable>(true)
                             ?? throw new Exception("row has no ControllerSelectable");
            o.Selectable.TargetFSM = null;
            if (o.Selectable.ExplicitNavigation != null) o.Selectable.ExplicitNavigation.UseExplicit = false;
            selectables[i] = o.Selectable;
        }
        Destroy(toggleTemplate);
        Destroy(sliderTemplate);
        p.Selector.Selectables = selectables;
    }

    private void BuildPageList(Transform list)
    {
        // The list's title is as big as its buttons; with seven buttons it reads as one of them. Make it larger and
        // move the buttons down and a little closer together.
        var title = list.Find("Title/MOUSE");
        if (title != null)
        {
            var tt = Text(title);
            tt.text = "MODS";
            tt.fontSize *= TitleScale;
        }

        var vl = Need(list, "Vertical Layout");
        var buttonTemplate = Need(vl, "BUTTON 5 - Motion Assist");
        var layout = vl.GetComponent<UIVerticalLayout>();
        var objects = new Il2CppReferenceArray<GameObject>(_pages.Count);
        var selectables = new Il2CppReferenceArray<ControllerSelectable>(_pages.Count);
        for (int i = 0; i < _pages.Count; i++)
        {
            var p = _pages[i];
            var b = Object.Instantiate(buttonTemplate.gameObject, vl).transform;
            b.name = "BUTTON - " + p.Title;
            var label = b.GetComponentInChildren<TMP_Text>(true);  // "... Text" (the arrows come after it)
            if (label != null) label.text = p.Title;
            var cs = b.GetComponent<ControllerSelectable>() ?? throw new Exception("page button has no ControllerSelectable");
            cs.TargetFSM = null;
            if (cs.ExplicitNavigation != null) cs.ExplicitNavigation.UseExplicit = false;
            var page = p;
            cs.OnUse = Event(() => page.Submenu?.Open());
            objects[i] = b.gameObject;
            selectables[i] = cs;
        }
        for (int i = vl.childCount - 1; i >= 0; i--)
            if (vl.GetChild(i).name.StartsWith("BUTTON ") && !vl.GetChild(i).name.StartsWith("BUTTON - ")) Destroy(vl.GetChild(i));
        vl.localPosition += Vector3.down * ListDrop;
        if (layout != null)
        {
            layout.Spacing *= ListSpacingScale;
            layout.Gameobjects = objects;
            layout.RefreshLayout();
        }

        var sel = Need(list, "Access settings selector").GetComponent<ControllerSelector>()
                  ?? throw new Exception("page list has no ControllerSelector");
        sel.Selectables = selectables;
        sel.OnClose = Event(Close);  // vanilla: the screen's ESC FSM (removed) -> Settings list
    }

    private GameObject BuildEntry(GameObject volume, GameObject back, Transform layout, GameObject settings)
    {
        GameObject entry = null;
        CloneInactive(volume, EntryName, go =>
        {
            entry = go;
            Strip(go);
            _entryText = go.GetComponent<TMP_Text>() ?? throw new Exception("VOLUME has no text");
            _entryText.text = EntryName;
            _entryText.color = EntryNormal;

            var cs = go.GetComponent<ControllerSelectable>() ?? throw new Exception("VOLUME has no ControllerSelectable");
            cs.TargetFSM = null;
            cs.OnSelect = Event(() => { if (_entryText != null) _entryText.color = EntryHover; });
            cs.OnDeselect = Event(() => { if (_entryText != null) _entryText.color = EntryNormal; });
            cs.OnUse = Event(Open);

            go.transform.SetParent(layout, false);
            go.transform.SetSiblingIndex(back.transform.GetSiblingIndex());
            go.SetActive(true);

            var vl = layout.GetComponent<UIVerticalLayout>();
            if (vl != null) { vl.Gameobjects = InsertBefore(vl.Gameobjects, go, back); vl.RefreshLayout(); }

            var sel = settings.GetComponent<ControllerSelector>();
            var backSel = back.GetComponent<ControllerSelectable>();
            if (sel != null && backSel != null) sel.Selectables = InsertBefore(sel.Selectables, cs, backSel);
            else Core.Log.Warning($"[{Name}] MODS can't be reached with keyboard/gamepad; mouse only");
        });
        return entry;
    }

    private static Il2CppReferenceArray<T> InsertBefore<T>(Il2CppReferenceArray<T> old, T item, T before)
        where T : Il2CppSystem.Object
    {
        var n = old?.Length ?? 0;
        var arr = new Il2CppReferenceArray<T>(n + 1);
        int j = 0;
        bool done = false;
        for (int i = 0; i < n; i++)
        {
            if (!done && old[i] != null && old[i].Pointer == before.Pointer) { arr[j++] = item; done = true; }
            arr[j++] = old[i];
        }
        if (!done) arr[j] = item;
        return arr;
    }
}
