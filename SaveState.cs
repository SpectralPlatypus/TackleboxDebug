using System.Collections;
using UnityEngine;

namespace TackleboxDbg {
    static class SaveState
    {
            static readonly string savestatePath = Application.persistentDataPath + "/savestate.json";
            static Vector3 spawnTransPos;
            static Vector3 playerPos;
            
            public static void SaveCurrentData()
            {
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
                Manager.GetSaveManager().SaveCurrentFile();
                string contents = JsonUtility.ToJson(Manager.GetSaveManager()._currentSaveData);
                File.WriteAllText(savestatePath, contents);
            }

            static void LoadSavestateFile()
            {
                string contents = File.ReadAllText(savestatePath);
                SaveData data = new();
                JsonUtility.FromJsonOverwrite(contents, data);
                Manager.GetSaveManager().PrepareGameState(data);
            }

            static IEnumerator ReloadSceneRoutine()
            {
                if(Manager.GetMainMenu()._currentState == Manager.GetMainMenu()._inGameState)
                {
                    LoadSavestateFile();

                    Manager.GetMainMenu().LoadGameScene();
                    yield return new WaitUntil(() => Manager.GetMainMenu()._currentState == Manager.GetMainMenu()._inGameState);

                    if(spawnTransPos != Vector3.zero) // probably find a better way of doing this?
                    {
                        Manager.GetPlayerMachine()._spawnTransform.position = spawnTransPos;
                    }
                    if(playerPos != Vector3.zero) // probably find a better way of doing this?
                    {
                        Manager.GetPlayerMachine().Teleport(playerPos);
                    }
                }
            }
    }
}