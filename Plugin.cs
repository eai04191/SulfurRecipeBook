using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace SulfurRecipeBook;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    public static ConfigEntry<bool> ConfigShowHealPerSlot;

    private void Awake()
    {
        // Plugin startup logic
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Config
        ConfigShowHealPerSlot = Config.Bind(
            "General",
            "ShowHealPerSlot",
            false,
            "Show heal per slot in item description"
        );

        // Harmony patching
        Harmony.CreateAndPatchAll(typeof(RecipeBook));
        Harmony.CreateAndPatchAll(typeof(ReversePatch));
    }
}
