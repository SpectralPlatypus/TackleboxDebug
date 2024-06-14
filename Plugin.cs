using BepInEx;
using HarmonyLib;
using System;

namespace TackleboxDbg
{
    static class ModInfo
    {
        public const string PLUGIN_GUID = "TackleboxDbg";
        public const string PLUGIN_NAME = "TackleboxDbg";
        public const string PLUGIN_VERSION = "1.1.0";
    }

    [BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
    [BepInProcess("The Big Catch Tacklebox.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {ModInfo.PLUGIN_NAME} is loaded!");

            var harmony = new Harmony(ModInfo.PLUGIN_GUID);
            try
            {
                harmony.PatchAll();
            }
            catch (Exception ex)
            {
                Logger.LogError((object)("Failed to patch: " + ex));
            }
        }

    }
}
