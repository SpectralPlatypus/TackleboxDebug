using System;
using System.Collections;
using UnityEngine;

namespace TackleboxDbg {
    static class SaveState
    {
        static readonly string savestatePath = Application.persistentDataPath + "/savestate.json";
        static Vector3 spawnTransPos;
        static Vector3 playerPos;
        static Dictionary<Guid, bool> zipRingValues = [];
        
        public static void SaveCurrentData()
        {
            zipRingValues.Clear();
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;
                if(ar is not null) 
                {
                    zipRingValues.Add(ar._guid, ar.GetValue());
                }
            }

            spawnTransPos = Manager.GetPlayerMachine()._spawnTransform.position;
            playerPos = Manager.GetPlayerMachine()._position;
            SaveSavestateFile();
        }
        public static void LoadSavedData()
        {
            Manager._instance.StartCoroutine(ReloadSceneRoutine());
        }

        static void SaveSavestateFile()
        {
            string contents = JsonUtility.ToJson(Manager.GetSaveManager()._currentSaveData);
            File.WriteAllText(savestatePath, contents);
        }

        static void LoadSavestateFile(SaveData data)
        {
            string contents = File.ReadAllText(savestatePath);
            JsonUtility.FromJsonOverwrite(contents, data);
            data.name = "savestate";
            Manager.GetSaveManager().PrepareGameState(data);
        }

        static IEnumerator ReloadSceneRoutine()
        {
            if(Manager.GetMainMenu()._currentState == Manager.GetMainMenu()._inGameState)
            {
                SaveData data = new();
                LoadSavestateFile(data);

                Manager.GetMainMenu().LoadGameScene();
                yield return new WaitUntil(() => Manager.GetMainMenu()._currentState == Manager.GetMainMenu()._inGameState);
                
                var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach(var zip in zipList)
                {
                    var ar = zip._activatedReference;

                    if(ar is null) continue;
                    if(!zipRingValues.ContainsKey(ar._guid)) continue;

                    if(zipRingValues[ar._guid]) zip.Activate(true);
                    else zip.Deactivate(true);
                }

                if(spawnTransPos != Vector3.zero) // probably find a better way of doing this?
                {
                    Manager.GetPlayerMachine()._spawnTransform.position = spawnTransPos;
                }
                if(playerPos != Vector3.zero) // probably find a better way of doing this?
                {
                    Manager.GetPlayerMachine().Teleport(playerPos);
                }

                // Change back to our last selected save slot so the game doesn't continue writing over the savestate.json
                Manager.GetSaveManager().PrepareGameState(Manager.GetMainMenu()._lastSelectedSaveData.GetValue());
            }
        }
    }
}