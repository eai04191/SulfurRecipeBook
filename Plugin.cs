using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;

namespace SulfurRecipeBook;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[SuppressMessage(
    "Class Declaration",
    "BepInEx002:Classes with BepInPlugin attribute must inherit from BaseUnityPlugin"
)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(RecipeBook));
        Harmony.CreateAndPatchAll(typeof(ReversePatch));
    }
}
