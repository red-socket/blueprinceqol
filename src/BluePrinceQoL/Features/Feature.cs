using MelonLoader;

namespace BluePrinceQoL.Features;

public abstract class Feature
{
    public abstract string Name { get; }
    protected virtual string Description => "";
    protected virtual bool DefaultEnabled => true;
    internal int Errors;

    private MelonPreferences_Entry<bool> _enabled;
    public bool Enabled => _enabled.Value;
    /// <summary>OnUpdate also runs while the feature is off (it then has to check Enabled itself).</summary>
    public virtual bool UpdateWhenDisabled => false;

    public void Init()
    {
        _enabled = Core.Prefs.CreateEntry($"{Name}_Enabled", DefaultEnabled, $"{Name}: enabled", Description);
        CreatePrefs();
    }

    protected virtual void CreatePrefs() { }
    public virtual void OnSceneLoaded(string sceneName) { }
    public virtual void OnUpdate() { }
    public virtual void OnLateUpdate() { }
    public virtual void OnGUI() { }
}
