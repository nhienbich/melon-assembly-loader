// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
/// <summary>
/// Drop Payload.dll into UserData/assembly. AssemblyLoader calls Main on load
/// and Unload before the next load. UnityEngine.Debug can AccessViolation on
/// IL2CPP — use MelonLogger.
/// </summary>
public static class Payload
{
    internal static HarmonyLib.Harmony Harmony;
    public static void Main()
    {
        Harmony = new HarmonyLib.Harmony("com.mod.script");
        Harmony.PatchAll();
        MelonLogger.Msg("Payload.Main invoked.");
    }

    public static void Unload()
    {
        Harmony?.UnpatchSelf();
        // Drop statics, Destroy objects you created, and Harmony.UnpatchSelf()
        // before this returns. Otherwise the collectible load context cannot unload.
        MelonLogger.Msg("Payload.Unload invoked.");
    }
}
