using MelonLoader;

[assembly: MelonInfo(typeof(MelonAssemblyLoader.AssemblyLoaderMod), "AssemblyLoader", "1.0.0", "MelonAssemblyLoader")]

namespace MelonAssemblyLoader;

/// <summary>
/// MelonLoader 0.6+ port of BepInEx.IL2CPP.AssemblyLoader.
/// Lives in Mods/. Hot-loads UserData/assembly/Payload.dll.
/// </summary>
public sealed class AssemblyLoaderMod : MelonMod
{
    private HotAssemblyLoader? loader;

    public override void OnInitializeMelon()
    {
        loader = new HotAssemblyLoader(new MelonLog(LoggerInstance));
        LoggerInstance.Msg("AssemblyLoader is loaded.");
        LoggerInstance.Msg($"Watching {loader.AssemblyPath}");
    }

    public override void OnLateInitializeMelon()
    {
        loader?.RequestInitialLoad();
        loader?.Tick();
    }

    public override void OnUpdate()
    {
        loader?.Tick();
    }

    public override void OnDeinitializeMelon()
    {
        loader?.Dispose();
        loader = null;
    }
}
