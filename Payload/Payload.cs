// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Drop Payload.dll into UserData/assembly. AssemblyLoader calls Main on load
/// and Unload before the next load. UnityEngine.Debug can AccessViolation on
/// IL2CPP — use MelonLogger.
/// </summary>
public static class Payload
{
    public static void Main()
    {
        MelonLogger.Msg("Payload.Main invoked.");
        DumpActiveSceneRootGameObjects();
    }

    public static void Unload()
    {
        // Drop statics, Destroy objects you created, and Harmony.UnpatchSelf()
        // before this returns. Otherwise the collectible load context cannot unload.
        MelonLogger.Msg("Payload.Unload invoked.");
    }

    private static void DumpActiveSceneRootGameObjects()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Il2CppReferenceArray<GameObject> objects = activeScene.GetRootGameObjects();
        MelonLogger.Msg("////////////////////////////");
        MelonLogger.Msg($"Active scene {activeScene.name} with {objects.Count} root objects.");
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            Il2CppArrayBase<Component> components = go.GetComponents<Component>();
            MelonLogger.Msg($"{i}. {go.name} with {components.Count} components.");
            for (int j = 0; j < components.Count; j++)
            {
                Component component = components[j];
                string typeName = component == null
                    ? "<missing>"
                    : component.GetIl2CppType().FullName;
                MelonLogger.Msg($"  {j}. {typeName}");
            }

            MelonLogger.Msg("////////////////////////////");
        }
    }
}
