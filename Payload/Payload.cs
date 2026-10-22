// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using HarmonyLib;
using Il2CppGame;
using Il2CppGame.Shop;
using Il2CppGame.Furniture;
using Il2CppGame.PopupSystem;
using Il2CppGame.Workers;
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
    MelonLogger.Msg("Payload.Unload invoked.");
  }
}
