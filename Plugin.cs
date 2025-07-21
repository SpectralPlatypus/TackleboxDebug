using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using MonoMod.Utils;
using System.Reflection;
using UnityEngine;

namespace TackleboxDbg
{
    public enum GameVersions
    {
        V1_1_0,
        V1_2_4
    }
    static class ModInfo
    {
        public const string PLUGIN_GUID = "TackleboxDbg";
        public const string PLUGIN_NAME = "TackleboxDbg";
        public const string PLUGIN_VERSION = "1.2.1";
    }

    [BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
    [BepInProcess("The Big Catch Tacklebox.exe")]
    public class Plugin : BaseUnityPlugin
    {

        #region KEYBINDS
        ConfigEntry<KeyboardShortcut> ToggleDebugKey;
        ConfigEntry<KeyboardShortcut> RespawnCollectiblesKey;
        ConfigEntry<KeyboardShortcut> ClearShreddersKey;
        ConfigEntry<KeyboardShortcut> GiveWhistleKey;
        public static ConfigEntry<bool> OverrideDebugArrow;
        #endregion
        static GameVersions currentVersion = GameVersion();
        static Type fishType = AccessTools.TypeByName(
            (currentVersion == GameVersions.V1_1_0) ? "IntroWalkingFish" : "CapturableWalkingFish");
        private void Awake()
        {           
            // Plugin startup logic
            Logger.LogInfo($"Plugin {ModInfo.PLUGIN_NAME} is loaded!");
            Logger.LogInfo($"Game Version: {GetVersion()}");

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
            GiveWhistleKey = Config.Bind("Inputs", "GiveWhistleKey", new KeyboardShortcut(KeyCode.F8), "Give the player the whistle");
            OverrideDebugArrow = Config.Bind("Misc", "OverrideDbgArrow", true, "Changes Debug Arrow behavior to display the respawn area");
        }

        public static Version GetVersion()
        {
            return BuildDate.Version();
        }
        public static GameVersions CurrentVersion() => currentVersion;

        private static GameVersions GameVersion()
        {
            var v = GetVersion();
            if (v.Equals(new Version(1, 0, 9149, 40858)))
            {
                return GameVersions.V1_2_4;
            }

            return GameVersions.V1_1_0;
        }

        private void Update()
        {
            if (ToggleDebugKey.Value.IsDown())
            {
                Manager._instance.ToggleDebugEnabled();
            }

            if (Manager._instance._debugEnabled)
            {
                if (RespawnCollectiblesKey.Value.IsDown())
                {
                    ResetCollectibles();
                }
                if (ClearShreddersKey.Value.IsDown())
                {
                    BurrowShredders();
                }
                if(GiveWhistleKey.Value.IsDown())
                {
                    GiveWhistle();
                }
            }
        }

        FieldInfo fishCaptField = AccessTools.Field(fishType, "_capturable");
        FieldInfo fishAgentField = AccessTools.Field(fishType, "_agent");
        MethodInfo fishPlayerRespawnedMethod = AccessTools.Method(fishType, "PlayerRespawned");
        MethodInfo fishGameObjectProp = AccessTools.PropertyGetter(fishType, "_gameObject");

         void ResetCollectibles()
        {
            var captList = FindObjectsByType(fishType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            var currentCapt = Manager.GetPlayerMachine()._currentCapturingCapturable;
            foreach (var fish in captList)
            {
                Capturable capt = (Capturable)fishCaptField.GetValue(fish);
                if (capt == currentCapt)
                    continue;
                if (capt._state == Capturable.State.Captured)
                {
                    var agent = (FiletNavAgent)fishAgentField.GetValue(fish);
                    agent.Enable();
                    capt._state = Capturable.State.NotCapturing;

                    fishPlayerRespawnedMethod.Invoke(fish, [null]);
                    ((GameObject)fishGameObjectProp.Invoke(fish, null)).SetActive(true);
                }
            }
            var coinList = FindObjectsByType<Coin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var coinMgr = Manager.GetCoinManager();
            foreach (var coin in coinList)
            {

                if (!coin._collectible._collected ||
                        coinMgr._coinPoolSmall._entries.Contains(coin) || coinMgr._coinPoolMedium._entries.Contains(coin) || coinMgr._coinPoolLarge._entries.Contains(coin))
                    continue;
                coin._collectible._collected = false;
                coin._artObject.SetActive(true);
                coin._gameObject.SetActive(true);
            }

            coinMgr._coinPoolSmall.DisableAll();
            coinMgr._coinPoolMedium.DisableAll();
            coinMgr._coinPoolLarge.DisableAll();
            
            var zipList = FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                if(!zip._startsActivated)
                {
                    // The zips don't instant-deactivate properly if they're active.
                    zip.Deactivate(IsInstant: !zip._gameObject.activeInHierarchy);
                }
            }
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

        static void GiveWhistle()
        {
            Manager.GetPlayerMachine()._hasWhistle.SetValue(true);
        }
    }
}
