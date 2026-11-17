#if TOOLS
using Godot;

namespace Timers;

/// <summary>Editor plugin: registers the TimerManager autoload that advances the timers every frame.</summary>
[Tool]
public partial class TimersPlugin : EditorPlugin
{
    private const string AutoloadName = "TimerManager";
    private const string AutoloadPath = "res://addons/Timers/TimerManager/TimerManager.cs";

    public override void _EnablePlugin()
    {
        AddAutoloadSingleton(AutoloadName, AutoloadPath);
    }

    public override void _DisablePlugin()
    {
        RemoveAutoloadSingleton(AutoloadName);
    }
}
#endif
