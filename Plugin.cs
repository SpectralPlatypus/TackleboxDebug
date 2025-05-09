﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ImGuiNET;
using System.Reflection;
using UnityEngine;

namespace TackleboxDbg
{
    static class ModInfo
    {
        public const string PLUGIN_GUID = "TackleboxDbg";
        public const string PLUGIN_NAME = "TackleboxDbg";
        public const string PLUGIN_VERSION = "1.3.0";
    }

    [BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
    [BepInProcess("The Big Catch Tacklebox.exe")]
    public class Plugin : BaseUnityPlugin
    {
        #region KEYBINDS
        ConfigEntry<KeyboardShortcut> ToggleDebugKey;
        ConfigEntry<KeyboardShortcut> RespawnCollectiblesKey;
        ConfigEntry<KeyboardShortcut> ClearShreddersKey;
        ConfigEntry<KeyboardShortcut> SaveStateKey;
        ConfigEntry<KeyboardShortcut> LoadStateKey;
        ConfigEntry<KeyboardShortcut> TeleportStateKey;
        ConfigEntry<KeyboardShortcut> GiveWhistleKey;
        ConfigEntry<KeyboardShortcut> ResetCheckpointsKey;
        public static ConfigEntry<bool> OverrideDebugArrow;
        #endregion

        #region REFLECTION
        static FieldInfo spewedCoin = AccessTools.DeclaredField(typeof(Collectible), "_spewedCoin");
        static FieldInfo activeShredders = AccessTools.DeclaredField(typeof(ShredderManager), "_activeShredders");
        #endregion

        SaveStateManager SSManager;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin {ModInfo.PLUGIN_NAME} is loaded!");
            Logger.LogInfo($"Detected Game version: {BuildDate.Version()}");

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
            GiveWhistleKey = Config.Bind("Inputs", "GiveWhistle", new KeyboardShortcut(KeyCode.F8), "Give the player the whistle");
            ResetCheckpointsKey = Config.Bind("Inputs", "ResetCheckpoints", new KeyboardShortcut(KeyCode.F7), "Give the player the whistle");
            TeleportStateKey = Config.Bind("Inputs", "TeleportState", new KeyboardShortcut(KeyCode.F5), "Teleport to the saved game data's position");
            LoadStateKey = Config.Bind("Inputs", "LoadState", new KeyboardShortcut(KeyCode.F4), "Load the saved game data");
            SaveStateKey = Config.Bind("Inputs", "SaveState", new KeyboardShortcut(KeyCode.F3), "Save the current game data");
            OverrideDebugArrow = Config.Bind("Misc", "OverrideDbgArrow", true, "Changes Debug Arrow behavior to display the respawn area");

            SSManager = new();
        }

        private void OnEnable()
        {
            Patches.Layout += OnLayout;
        }

        private void OnDisable()
        {
            Patches.Layout -= OnLayout;
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
                    ResetMookCoins();
                }
                if (ClearShreddersKey.Value.IsDown())
                {
                    BurrowShredders();
                }
                if(GiveWhistleKey.Value.IsDown())
                {
                    GiveWhistle();
                }
                if(ResetCheckpointsKey.Value.IsDown())
                {
                    ResetCheckpoints();
                }
                if(SaveStateKey.Value.IsDown())
                {
                    SSManager.SaveCurrentDataToQuickSlot();
                }
                if(LoadStateKey.Value.IsDown())
                {
                    SSManager.LoadCurrent();
                }
                if(TeleportStateKey.Value.IsDown())
                {
                    SSManager.LoadCurrentNonSaveData();
                }
            }
        }

        private void OnLayout()
        {
            if (ImGui.BeginTabItem("Tacklebox Debug"))
            {
                if (ImGui.CollapsingHeader("SaveStates"))
                {
                    if (SSManager.IsInGame)
                    {
                        if(ImGui.Button("Load"))
                        {
                            SSManager.LoadCurrent();
                        }
                        ImGui.SameLine();
                        if(ImGui.Button("Teleport"))
                        {
                            SSManager.LoadCurrentNonSaveData();
                        }
                    }
                    
                    ImGui.ListBox("", ref SSManager.CurrentIndex, SSManager.SaveStateNames, SSManager.SaveStateNames.Length, 10);

                    if (ImGui.Button("Delete"))
                    {
                        SSManager.DeleteCurrent();
                    }
                    if (SSManager.IsInGame)
                    {
                        ImGui.SameLine();
                        if(ImGui.Button("Create"))
                        {
                            SSManager.SaveCurrentDataToNewFile();
                        }
                    }
                    if (ImGui.Button("Open SaveStates Folder"))
                    {
                        SSManager.OpenSaveStateFolder();
                    }
                }
                ImGui.EndTabItem();
            }
        }

        void ResetCollectibles()
        {
            var currentCapt = Manager.GetPlayerMachine()._currentCapturingCapturable;
            var captList = FindObjectsByType<IntroWalkingFish>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
                        coinMgr._coinPoolSmall._entries.Contains(coin) || coinMgr._coinPoolMedium._entries.Contains(coin) || coinMgr._coinPoolLarge._entries.Contains(coin))
                    continue;
                coin._collectible._collected = false;
                coin._artObject.SetActive(true);
                coin._gameObject.SetActive(true);
            }

            coinMgr._coinPoolSmall.DisableAll();
            coinMgr._coinPoolMedium.DisableAll();
            coinMgr._coinPoolLarge.DisableAll();
        }
        
        void ResetCheckpoints()
        {
            var zipList = FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                if(zip._activatedReference is not null && zip._activatedReference.GetValue())
                    // The zips don't instant-deactivate properly if they're active.
                    zip.Deactivate(IsInstant: !zip._gameObject.activeInHierarchy);
            }
        }

        void BurrowShredders()
        {
            var shredMgr = FindFirstObjectByType<ShredderManager>();
            var activeShredderList = activeShredders.GetValue(shredMgr) as List<SandShredder>;
            foreach(var shredder in activeShredderList)
            {
                shredder.Burrow();
            }
        }

        void GiveWhistle()
        {
            Manager.GetPlayerMachine()._hasWhistle.SetValue(true);
        }

        void ResetMookCoins()
        {
            var mooks = FindObjectsByType<BasicMook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var turretMooks = FindObjectsByType<MortarDesertMook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var saveData = Manager.GetSaveManager()?._currentSaveData;
            if (saveData == null)
                return;

            foreach (var mook in mooks)
            {
                ResetAgentCollectibles(mook._agent, saveData);
            }

            foreach (var mook in turretMooks)
            {
                ResetAgentCollectibles(mook._headAgent, saveData);
            }
        }

        void ResetAgentCollectibles(FiletNavAgent agent, SaveData saveData)
        {
            foreach (var collectible in agent._collectibles)
            {
                collectible._collected = false;
                var guid = collectible._asset._guid;
                if ((saveData._collectibles?.Remove(guid)).GetValueOrDefault(false))
                {
                    Logger.LogDebug("Reset coins for: " + agent.ToString());
                }
                else
                {
                    var emittedCoin = spewedCoin.GetValue(collectible) as Coin;
                    if (emittedCoin != null && emittedCoin._isActiveAndEnabled)
                    {
                        Logger.LogDebug("Despawned coins for: " + agent.ToString());
                        emittedCoin._gameObject.SetActive(false);
                        spewedCoin.SetValue(collectible, null);
                    }
                }
            }
        }
    }
}
