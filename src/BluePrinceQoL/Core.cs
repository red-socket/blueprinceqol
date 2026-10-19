using System.Collections.Generic;
using BluePrinceQoL.Features;
using BluePrinceQoL.Util;
using MelonLoader;

[assembly: MelonInfo(typeof(BluePrinceQoL.Core), "Blue Prince QoL", "2.4.2", "Lucas Pospor")]
[assembly: MelonGame("Dogubomb", "BLUE PRINCE")]

namespace BluePrinceQoL;

public partial class Core : MelonMod
{
    internal static MelonLogger.Instance Log;
    internal static MelonPreferences_Category Prefs;

    private readonly List<Feature> _features = new();

    public override void OnInitializeMelon()
    {
        Log = LoggerInstance;
        Prefs = MelonPreferences.CreateCategory("BluePrinceQoL");

        _features.Add(new StateLoggerFeature());
        _features.Add(new HudFeature());
        _features.Add(new IntroSkipFeature());
        _features.Add(new PickupSkipFeature());
        _features.Add(new TerminalFeature());
        _features.Add(new CameraZoomFeature());
        _features.Add(new MoveSpeedFeature());
        _features.Add(new EndOfDayFeature());
        _features.Add(new RunStatsFeature());
        _features.Add(new DigSpeedFeature());
        _features.Add(new StairsWarpFeature());
        _features.Add(new BootSkipFeature());
        _features.Add(new ClickUnlockFeature());
        _features.Add(new DoorKeySpeedFeature());
        _features.Add(new MusicVolumeFeature());
        _features.Add(new CursorGuardFeature());
        _features.Add(new DareGameOverFeature());
        _features.Add(new SecretGardenFeature());
        _features.Add(new ElevatorSpeedFeature());
        _features.Add(new RotundaFeature());
        _features.Add(new ModsMenuFeature());
        _features.Add(new QuitWithoutSavingFeature());
        _features.Add(new WorkshopFeature());
        _features.Add(new SecurityVideoFeature());
        _features.Add(new OfficeCompartmentFeature());
        _features.Add(new DartsEquationFeature());

        AddDevFeatures(_features);

        foreach (var f in _features)
            f.Init();

        Log.Msg($"Loaded {_features.Count} feature(s)");
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        FsmHooks.OnSceneLoaded();
        foreach (var f in _features)
            Safe(f, () => f.OnSceneLoaded(sceneName));
    }

    public override void OnUpdate()
    {
        FsmHooks.Tick(HarmonyInstance);
        foreach (var f in _features)
            if (f.Enabled) Safe(f, f.OnUpdate);
    }

    public override void OnLateUpdate()
    {
        foreach (var f in _features)
            if (f.Enabled) Safe(f, f.OnLateUpdate);
    }

    public override void OnGUI()
    {
        foreach (var f in _features)
            if (f.Enabled) Safe(f, f.OnGUI);
    }

    // Testing helpers, only in builds that include them.
    static partial void AddDevFeatures(List<Feature> features);

    // One broken feature must not take the others (or the game) down with it.
    private static void Safe(Feature f, System.Action a)
    {
        try { a(); }
        catch (System.Exception e)
        {
            f.Errors++;
            if (f.Errors <= 3) Log.Error($"[{f.Name}] {e}");
            if (f.Errors == 3) Log.Error($"[{f.Name}] further errors suppressed");
        }
    }
}
