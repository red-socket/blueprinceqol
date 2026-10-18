using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using BluePrinceQoL.Util;
using Il2Cpp;
using Il2CppBluePrince.UI;
using Il2CppHutongGames.PlayMaker;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace BluePrinceQoL.Features;

/// <summary>
/// Shows the Billiard Room dartboard puzzle as an equation at the bottom of the screen while you are zoomed in on it,
/// with each operation sign in the colour of its sectors: +4 - 1 × 2.
///
/// The board is a DartsStageManager ("Dartboard System/Dartboard New") loaded with a code string per stage
/// (CurrentStage.RawString), e.g. "14BE,04AA,10AA". Codes (checked against the answer stored in each of the 791 codes
/// in the Billiard Room prefab, see notes/darts.md):
///   first code = bullseye: 2-digit answer, colour (A/S/M/D = after the add/subtract/multiply/divide rings, B = none),
///     shape (E none, S square, D square twice, V reverse digits, O/T/H round to 1/10/100, and combinations);
///   other codes = number, ring (A inner .. D outer, E = the number ring), type: A add (teal), S subtract (yellow),
///     M multiply (pink), D divide (violet), 3 / 1 add / subtract a third (teal / yellow);
///     on ring E: X crossed out (the number is ignored), W double, H half, V reverse digits.
/// Rings are worked from the inside out, in code order within a ring; the bullseye shape applies after every ring
/// that has an operation of the bullseye's colour (crossed-out numbers don't count).
/// Shown only while the "Darts - First Collider" FSM is zoomed in (Camera Move 2 / Wait for Exit 2) and no menu is open.
///
/// Also tints the odd numbers around the board slightly red ("Dartboard System/<n>holder/<n>", FSM "Billiards", seen when
/// it enters a state). Their shader (Bequest Standard) has no _Color; its _RampColor2 (white by default) is the colour of
/// the light end of the shading ramp, set through a MaterialPropertyBlock so the hover material swap keeps it.
/// </summary>
public class DartsEquationFeature : Feature
{
    public override string Name => "DartsEquation";
    protected override string Description => "Shows the Billiard Room dartboard puzzle as an equation at the bottom of the screen, and tints the board's odd numbers red";
    protected override bool DefaultEnabled => false;

    private const string Collider = "Darts - First Collider";
    private const string BoardPath = "Dartboard New";
    private static readonly Regex SectorRe = new(@"^(\d+)([A-Z])(.+)$");

    private MelonPreferences_Entry<float> _fontScale, _posY;
    private MelonPreferences_Entry<string> _fontPref;
    private MelonPreferences_Entry<bool> _redOdd;

    private const string RampProp = "_RampColor2";
    private static readonly Color OddTint = new(1f, 0.7f, 0.7f, 1f);
    private readonly List<Renderer> _numbers = new();   // odd board numbers seen this scene
    private readonly HashSet<IntPtr> _numberSeen = new();
    private bool _tinted;                                 // state applied to _numbers
    private MaterialPropertyBlock _block;

    private GameObject _collider;      // the zoomed-in dartboard's collider (null = not zoomed in)
    private DartsStageManager _board;
    private string _raw;               // code the text was built from
    private string _text = "";

    private GameObject _root;
    private TextMeshProUGUI _tmp;
    private RectTransform _panel;
    private string _layoutText;
    private float _layoutSize, _layoutWidth;
    private int _layoutLines = 1;
    private bool _gaveUp, _menuCheckBroken;
    private int _attempts;
    private float _nextTry;
    private string _shownText;

    // Glyphs, replaced with ASCII when the font lacks them.
    private string _times = "×", _divide = "÷";

    // Display colours of the game's darts colour data (fallbacks: the values in DartsColorData's ColorData assets).
    private string _colAdd = "#76CEE5", _colSub = "#FFF51E", _colMul = "#B47097", _colDiv = "#7E69C5";
    private const string ColCrossed = "#9A9A9A";

    protected override void CreatePrefs()
    {
        var p = Core.Prefs;
        _fontScale = p.CreateEntry($"{Name}_FontScale", 0.045f, "DartsEquation: font scale", "Text size as a fraction of screen height.");
        _posY = p.CreateEntry($"{Name}_PosY", 0.935f, "DartsEquation: position",
            "Vertical position of the equation as a fraction of the screen height from the top.");
        _fontPref = p.CreateEntry($"{Name}_Font", "TypeNeat SDF", "DartsEquation: font",
            "A game TextMeshPro font asset name. The default has the × and ÷ signs; other fonts fall back to x and /.");
        _redOdd = p.CreateEntry($"{Name}_RedOddNumbers", true, "DartsEquation: red odd numbers",
            "Tints the odd numbers around the Billiard Room dartboard slightly red.");
        FsmHooks.AfterEnter += OnAfterEnter;
    }

    public override void OnSceneLoaded(string sceneName)
    {
        _collider = null;
        _board = null;
        _raw = null;
        _numbers.Clear();
        _numberSeen.Clear();
        _tinted = false;
    }

    private void OnAfterEnter(FsmState state)
    {
        var go = state.Fsm?.GameObject;
        if (go == null) return;
        if (state.Fsm.Name == "Billiards") { SeeNumber(go); return; }
        if (go.name != Collider || state.Fsm.Name != "FSM") return;
        bool zoomed = state.Name is "Camera Move 2" or "Wait for Exit 2";
        if (zoomed && _collider != go)
        {
            _board = null;
            _raw = null;
        }
        _collider = zoomed ? go : null;
    }

    // A board number ("<n>holder/<n>" under "Dartboard System"): remember the odd ones and tint them if enabled.
    private void SeeNumber(GameObject go)
    {
        if (!_numberSeen.Add(go.Pointer)) return;
        var holder = go.transform.parent;
        if (holder == null || holder.parent == null || holder.parent.name != "Dartboard System" ||
            !holder.name.EndsWith("holder", StringComparison.Ordinal) || !int.TryParse(go.name, out int n) || n % 2 == 0)
            return;
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        _numbers.Add(r);
        if (_tinted) Tint(r, true);
    }

    private void SyncTint()
    {
        bool want = Enabled && _redOdd.Value;
        if (want == _tinted) return;
        _tinted = want;
        foreach (var r in _numbers) Tint(r, want);
    }

    private void Tint(Renderer r, bool on)
    {
        try
        {
            if (r == null) return;
            _block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(_block);
            var m = r.sharedMaterial;
            var normal = m != null && m.HasProperty(RampProp) ? m.GetColor(RampProp) : Color.white;
            _block.SetColor(RampProp, on ? normal * OddTint : normal);
            r.SetPropertyBlock(_block);
        }
        catch (Exception e) { Core.Log.Warning($"[{Name}] can't tint a dartboard number: {e.Message}"); }
    }

    public override void OnUpdate()
    {
        SyncTint();
        bool show = Enabled && _collider != null && _collider.activeInHierarchy && !MenuOpen() && UpdateText();
        Sync(show);
    }

    private bool UpdateText()
    {
        if (_board == null)
        {
            var t = _collider.transform.parent != null ? _collider.transform.parent.Find(BoardPath) : null;
            _board = t != null ? t.GetComponent<DartsStageManager>() : null;
            if (_board == null) return false;
            ReadColors();
            // In case a number's FSM started before the hooks were installed.
            var system = _collider.transform.parent;
            for (int i = 0; i < system.childCount; i++)
            {
                var holder = system.GetChild(i);
                if (holder.childCount > 0 && holder.name.EndsWith("holder", StringComparison.Ordinal))
                    SeeNumber(holder.GetChild(0).gameObject);
            }
        }
        string raw = _board.CurrentStage?.RawString;
        if (string.IsNullOrEmpty(raw)) return false;
        if (raw != _raw)
        {
            _raw = raw;
            try { _text = Build(raw); }
            catch (Exception e)
            {
                _text = "";
                Core.Log.Warning($"[{Name}] can't read dart puzzle '{raw}': {e.Message}");
            }
        }
        return _text.Length > 0;
    }

    private void ReadColors()
    {
        try
        {
            var c = _board.TargetGraphics != null ? _board.TargetGraphics.DartsColors : null;
            if (c == null) return;
            _colAdd = Hex(c.Color_Add, _colAdd);
            _colSub = Hex(c.Color_Subtract, _colSub);
            _colMul = Hex(c.Color_Multiply, _colMul);
            _colDiv = Hex(c.Color_Divide, _colDiv);
        }
        catch (Exception e) { Core.Log.Warning($"[{Name}] darts colours not found, using defaults: {e.Message}"); }
    }

    private static string Hex(ColorData d, string fallback)
    {
        if (d == null) return fallback;
        var c = d.DisplayColor;
        return $"#{Byte(c.r):X2}{Byte(c.g):X2}{Byte(c.b):X2}";
        static int Byte(float v) => Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
    }

    // ------------------------------------------------------------------ equation

    private readonly struct Sector
    {
        public readonly int Number;
        public readonly char Ring;
        public readonly string Type;
        public Sector(int n, char ring, string type) { Number = n; Ring = ring; Type = type; }
    }

    private string Build(string raw)
    {
        var codes = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (codes.Length < 2) return "";
        string bull = codes[0];
        char bullColor = bull.Length >= 3 ? bull[2] : 'B';
        string bullShape = bull.Length >= 4 ? bull.Substring(3) : "E";

        var marks = new Dictionary<int, string>();
        var rings = new SortedDictionary<char, List<Sector>>();
        for (int i = 1; i < codes.Length; i++)
        {
            var m = SectorRe.Match(codes[i]);
            if (!m.Success) throw new FormatException($"sector '{codes[i]}'");
            var s = new Sector(int.Parse(m.Groups[1].Value), m.Groups[2].Value[0], m.Groups[3].Value);
            if (s.Ring == 'E') marks[s.Number] = s.Type;
            else
            {
                if (!rings.TryGetValue(s.Ring, out var list)) rings[s.Ring] = list = new List<Sector>();
                list.Add(s);
            }
        }

        var sb = new StringBuilder();
        bool first = true;
        foreach (var ring in rings.Values)
        {
            bool bullHere = false;
            foreach (var s in ring)
            {
                var (sign, color, group) = Op(s.Type);
                marks.TryGetValue(s.Number, out var mark);
                string num = Number(s.Number, mark);
                if (s.Type is "3" or "1") num += "/3";
                var term = new StringBuilder();
                if (first && group is 'M' or 'D') term.Append("0 ");
                if (!first) term.Append(' ');
                term.Append($"<color={color}>{sign}</color>");
                if (!first) term.Append(' ');
                term.Append(num);
                first = false;
                if (mark == "X")
                {
                    sb.Append($"<color={ColCrossed}><s>").Append(StripColors(term.ToString())).Append("</s></color>");
                    continue;
                }
                sb.Append(term);
                if (group == bullColor) bullHere = true;
            }
            if (bullHere) Bullseye(sb, bullShape, ColorOf(bullColor));
        }
        return sb.ToString();
    }

    private (string sign, string color, char group) Op(string type) => type switch
    {
        "A" or "3" => ("+", _colAdd, 'A'),
        "S" or "1" => ("-", _colSub, 'S'),
        "M" => (_times, _colMul, 'M'),
        "D" => (_divide, _colDiv, 'D'),
        _ => ("?", ColCrossed, '?'),
    };

    private string ColorOf(char group) => group switch
    {
        'A' => _colAdd,
        'S' => _colSub,
        'M' => _colMul,
        'D' => _colDiv,
        _ => ColCrossed,
    };

    // A number with its number-ring mark: W double, H half, V reverse digits (X is drawn crossed out by the caller).
    private string Number(int n, string mark) => mark switch
    {
        "W" => $"({n}{_times}2)",
        "H" => $"({n}/2)",
        "V" => $"rev({n})",
        null or "X" => n.ToString(),
        _ => $"{n}<sup>?</sup>",
    };

    private static string StripColors(string s) => Regex.Replace(s, "</?color[^>]*>", "");

    // Bullseye shape, applied to everything so far: steps in the order the game applies them.
    private static readonly Dictionary<string, string[]> Shapes = new()
    {
        ["S"] = new[] { "sq" }, ["D"] = new[] { "sq4" }, ["V"] = new[] { "rev" },
        ["O"] = new[] { "r1" }, ["T"] = new[] { "r10" }, ["H"] = new[] { "r100" },
        ["W"] = new[] { "sq", "rev" }, ["Q"] = new[] { "rev", "sq" },
        ["1"] = new[] { "r1", "sq" }, ["2"] = new[] { "r10", "sq" }, ["3"] = new[] { "r100", "sq" },
        ["4"] = new[] { "rev", "r1" }, ["5"] = new[] { "rev", "r10" }, ["6"] = new[] { "rev", "r100" },
        ["7"] = new[] { "r1", "rev" }, ["8"] = new[] { "r10", "rev" }, ["9"] = new[] { "r100", "rev" },
        ["A"] = new[] { "sq", "r1" }, ["B"] = new[] { "sq", "r10" }, ["C"] = new[] { "sq", "r100" },
    };

    private static void Bullseye(StringBuilder sb, string shape, string color)
    {
        if (shape == "E") return;
        if (!Shapes.TryGetValue(shape, out var steps)) steps = new[] { "?" };
        foreach (var step in steps)
        {
            var (open, close) = step switch
            {
                "sq" => ("(", ")<sup>2</sup>"),
                "sq4" => ("(", ")<sup>4</sup>"),
                "rev" => ("rev(", ")"),
                "r1" => ("round(", ")"),
                "r10" => ("round<sub>10</sub>(", ")"),
                "r100" => ("round<sub>100</sub>(", ")"),
                _ => ("(", ")<sup>?</sup>"),
            };
            sb.Insert(0, $"<color={color}>{open}</color>").Append($"<color={color}>{close}</color>");
        }
    }

    private bool MenuOpen()
    {
        if (_menuCheckBroken) return false;
        try { return UIScreen.AnyOpened; }
        catch (Exception e)
        {
            _menuCheckBroken = true;
            Core.Log.Warning($"[{Name}] menu detection failed, disabling it: {e.Message}");
            return false;
        }
    }

    // ------------------------------------------------------------------ rendering

    private void Sync(bool show)
    {
        if (_tmp == null)
        {
            if (_root != null) { UnityEngine.Object.Destroy(_root); _root = null; }
            if (!show || _gaveUp || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 1f;
            if (!TryCreate()) return;
        }
        if (!show)
        {
            if (_root.activeSelf) _root.SetActive(false);
            return;
        }
        if (!_root.activeSelf) _root.SetActive(true);

        if (_shownText != _text)
        {
            _tmp.text = _text;
            _shownText = _text;
        }
        float size = Mathf.Max(10f, Screen.height * Mathf.Clamp(_fontScale.Value, 0.01f, 0.2f));
        _tmp.fontSize = size;
        // Dark strip sized to the (possibly wrapped) text. Its height comes from the line count, not the font's line
        // height, which has a lot of room above and below the glyphs.
        float padX = size * 0.6f;
        if (_layoutText != _text || !Mathf.Approximately(_layoutSize, size))
        {
            _layoutText = _text;
            _layoutSize = size;
            // Measured at the full width: the text rect still has the previous equation's width here.
            var pref = _tmp.GetPreferredValues(_text, Screen.width * 0.9f, 0f);
            float line = _tmp.GetPreferredValues("+0", 0f, 0f).y;
            _layoutWidth = Mathf.Min(pref.x, Screen.width * 0.9f);
            _layoutLines = line > 0f ? Mathf.Max(1, Mathf.RoundToInt(pref.y / line)) : 1;
        }
        float w = _layoutWidth, h = _layoutLines * size * 0.95f + size * 0.3f;
        var anchor = new Vector2(0.5f, 1f - Mathf.Clamp01(_posY.Value));
        _panel.anchorMin = anchor;
        _panel.anchorMax = anchor;
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.anchoredPosition = Vector2.zero;
        _panel.sizeDelta = new Vector2(w + 2 * padX, h);
        var rt = _tmp.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, 0f);
        rt.offsetMax = new Vector2(-padX, 0f);
    }

    private bool TryCreate()
    {
        var want = (_fontPref.Value ?? "").Trim();
        TMP_FontAsset font = null, fallback = null;
        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (f == null) continue;
            if (f.name.Equals(want, StringComparison.OrdinalIgnoreCase)) { font = f; break; }
            if (f.name == "Versica SDF") fallback = f;
        }
        if (font == null && ++_attempts < 10) return false;  // fonts load with the rooms; wait a little
        font ??= fallback;
        if (font == null)
        {
            _gaveUp = true;
            Core.Log.Warning($"[{Name}] font '{want}' not found; the equation can't be shown");
            return false;
        }
        _times = font.HasCharacter('×', true) ? "×" : "x";
        _divide = font.HasCharacter('÷', true) ? "÷" : "/";
        _raw = null;  // rebuild with these glyphs

        _root = new GameObject("BluePrinceQoL DartsEquation");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 502;

        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(_root.transform, false);
        _panel = panelGo.AddComponent<RectTransform>();
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);
        bg.raycastTarget = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(panelGo.transform, false);
        _tmp = textGo.AddComponent<TextMeshProUGUI>();
        _tmp.font = font;
        _tmp.color = new Color(1f, 0.97f, 0.88f, 1f);
        _tmp.fontStyle = FontStyles.Bold;
        _tmp.textWrappingMode = TextWrappingModes.Normal;
        _tmp.overflowMode = TextOverflowModes.Overflow;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.raycastTarget = false;
        _tmp.richText = true;
        _shownText = null;
        Core.Log.Msg($"[{Name}] using game font '{font.name}'");
        return true;
    }
}
