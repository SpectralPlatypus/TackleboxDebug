using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace TackleboxDbg
{
    static class ModInfo
    {
        public const string PLUGIN_GUID = "TackleboxDbg";
        public const string PLUGIN_NAME = "TackleboxDbg";
        public const string PLUGIN_VERSION = "1.2.0";
    }

    [BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
    [BepInProcess("The Big Catch Tacklebox.exe")]
    public class Plugin : BaseUnityPlugin
    {

        #region KEYBINDS
        ConfigEntry<KeyboardShortcut> ToggleDebugKey;
        ConfigEntry<KeyboardShortcut> RespawnCollectiblesKey;
        ConfigEntry<KeyboardShortcut> ClearShreddersKey;
        public static ConfigEntry<bool> OverrideDebugArrow;
        #endregion

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

            ToggleDebugKey = Config.Bind("Inputs", "ToggleDebug", new KeyboardShortcut(KeyCode.F11), "The key for toggling Debug HUD");
            RespawnCollectiblesKey = Config.Bind("Inputs", "RespawnCollectibles", new KeyboardShortcut(KeyCode.F10), "Respawn Coins/Fish");
            ClearShreddersKey = Config.Bind("Inputs", "ClearShredders", new KeyboardShortcut(KeyCode.F9), "Despawn untethered shredders");
            OverrideDebugArrow = Config.Bind("Misc", "OverrideDbgArrow", true, "Changes Debug Arrow behavior to display the respawn area");
        }

        private void Update()
        {
            if (ToggleDebugKey.Value.IsDown())
            {
                Manager._instance.ToggleDebugEnabled();
            }
            if (RespawnCollectiblesKey.Value.IsDown())
            {
                ResetCollectibles();
            }
            if(ClearShreddersKey.Value.IsDown())
            {
                BurrowShredders();
            }
        }

        static void ResetCollectibles()
        {
            var captList = FindObjectsByType<IntroWalkingFish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var currentCapt = Manager.GetPlayerMachine()._currentCapturingCapturable;
            foreach (var fish in captList)
            {
                if (fish._capturable == currentCapt)
                    continue;
                if (fish._capturable._state == Capturable.State.Captured)
                {
                    fish._agent.Enable();
                    fish._capturable._state = Capturable.State.NotCapturing;
                    fish.PlayerRespawned(null);
                    fish._gameObject.SetActive(true);
                }
            }
            var coinList = FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var coinMgr = Manager.GetCoinManager();
            foreach (var coin in coinList)
            {

                if (!coin._collectible._collected ||
                        coinMgr._coinPoolMedium._entries.Contains(coin) || coinMgr._coinPoolMedium._entries.Contains(coin) || coinMgr._coinPoolLarge._entries.Contains(coin))
                    continue;
                coin._collectible._collected = false;
                coin._artObject.SetActive(true);
                coin._gameObject.SetActive(true);
            }

            coinMgr._coinPoolSmall.DisableAll();
            coinMgr._coinPoolMedium.DisableAll();
            coinMgr._coinPoolLarge.DisableAll();
        }

        static FieldInfo activeShredders = AccessTools.DeclaredField(typeof(ShredderManager), "_activeShredders");
        static void BurrowShredders()
        {
            var shredMgr = FindFirstObjectByType<ShredderManager>();
            var activeShredderList = activeShredders.GetValue(shredMgr) as List<SandShredder>;
            foreach(var shredder in activeShredderList)
            {
                shredder.Burrow();
            }
        }
    }
}
