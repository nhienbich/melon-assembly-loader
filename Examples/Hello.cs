// #name Hello
// #author melon
// #desc Logs a line when the script loads.
// #ref ${Managed}/UnityEngine.dll
// #proc_filter YourGame.exe

using MelonLoader;
using UnityEngine;

public static class Hello
{
    public static void Main()
    {
        MelonLogger.Msg("Hello from " + Application.productName);
    }

    public static void Unload()
    {
        // Drop statics, Destroy objects, Harmony.UnpatchSelf().
        // Mono cannot unload the old assembly. Cleanup is what makes reload safe.
        MelonLogger.Msg("Hello unloaded.");
    }
}
