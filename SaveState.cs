using System;
using System.Collections;
using UnityEngine;

namespace TackleboxDbg {
    static class SaveState
    {
        static readonly string savestatePath = Application.persistentDataPath + "/savestate.json";

        // Temporary values; these are not saved on game restart.
        // Should probably either delete the savestate.json on game close or find a way to save these values in savestate.json or otherwise.
        static Vector3 savedSpawnTransPos;
        static Vector3 savedPlayerPos;
        static Vector3 savedLookDir;
        static int savedHealth;
        static readonly Dictionary<Guid, bool> zipRingValues = [];
        
        public static void SaveCurrentData()
        {
            // The game either doesn't save or load the checkpoint rods properly, so I'm doing it manually here.
            zipRingValues.Clear();
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;
                if(ar is null) continue;
                
                zipRingValues.Add(ar._guid, ar.GetValue());
            }

            savedSpawnTransPos = Manager.GetPlayerMachine()._spawnTransform.position;
            savedPlayerPos = Manager.GetPlayerMachine()._position;
            savedLookDir = Manager.GetPlayerCamera()._lookDirection;
            savedHealth = Manager.GetPlayerMachine()._currentHealth;
            
            string contents = JsonUtility.ToJson(Manager.GetSaveManager()._currentSaveData);
            File.WriteAllText(savestatePath, contents);
        }
        public static void LoadSavedData()
        {
            Manager._instance.StartCoroutine(LoadSavestateRoutine());
        }

        private static IEnumerator LoadSavestateRoutine()
        {
            if(Manager.GetMainMenu()._currentState != Manager.GetMainMenu()._inGameState) yield break;
            
            SaveData data = new();
            string contents = File.ReadAllText(savestatePath);
            JsonUtility.FromJsonOverwrite(contents, data);
            // May want to use a slot that is either in use or not in use, depending on how we want to implement it. 
            // Just going to make a temporary save for now.
            data.name = "tempSave";
            Manager.GetSaveManager().PrepareGameState(data);

            Manager.GetMainMenu().LoadGameScene();
            yield return new WaitUntil(() => Manager.GetMainMenu()._currentState == Manager.GetMainMenu()._inGameState);

            // Load the saved zip ring values.
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;

                if(ar is null) continue;
                if(!zipRingValues.ContainsKey(ar._guid)) continue;

                if(zipRingValues[ar._guid]) zip.Activate(true);
                else zip.Deactivate(true);
            }

            if(savedSpawnTransPos != Vector3.zero) // Probably find a better way of doing this?
            {
                Manager.GetPlayerMachine()._spawnTransform.position = savedSpawnTransPos;
            }

            if(savedPlayerPos != Vector3.zero) // Probably find a better way of doing this?
            {
                Manager.GetPlayerMachine().Teleport(savedPlayerPos);
            }

            if(savedLookDir != Vector3.zero) // Probably find a better way of doing this?
            {
                Manager.GetPlayerCamera().SetLookDirection(savedLookDir);
            }

            if(savedHealth != 0)
            {
                Manager.GetPlayerMachine().SetCurrentHealth(savedHealth);
            }
            else Manager.GetPlayerMachine().SetCurrentHealth(3);
        }
    }
}