using System.Collections;
using UnityEngine;

namespace TackleboxDbg
{
    static class SaveStateManager
    {
        private static readonly string saveStatesPath = Application.persistentDataPath + "/SaveStates";
        private static SaveStateData data = new();
        private static MainMenu MainMenu => Manager.GetMainMenu();
        private static PlayerMachine Player => Manager.GetPlayerMachine();
        private static SaveManager SaveManager => Manager.GetSaveManager();
        private static PlayerCamera PlayerCamera => Manager.GetPlayerCamera();
        private static bool IsInGame => MainMenu._currentState == MainMenu._inGameState;

        public static void Init()
        {
            Directory.CreateDirectory(saveStatesPath);
            data = new SaveStateData(saveStatesPath + "/savestate.json");
        }

        public static void SaveCurrentData()
        {
            if(!IsInGame) return;

            data = GetCurrentData();
            data.WriteToFile(saveStatesPath+ "/savestate.json");
        }

        public static void LoadSavedData()
        {
            if(!IsInGame) return;
            Manager._instance.StartCoroutine(LoadSaveStateRoutine());
        }

        private static SaveStateData GetCurrentData()
        {
            SaveStateData data = new();

            // The game either doesn't save or load the checkpoint rods properly, so I'm doing it manually here.
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;
                if(ar is null) continue;
                
                data.AddToZipRings(ar._guid, ar.GetValue());
            }
        
            data.spawnTransformPos = Player._spawnTransform.position;
            data.playerPos = Player._position;
            data.lookDir = Manager.GetPlayerCamera()._lookDirection;
            data.facingDir = Player._currentFacingDirection;
            data.health = Player._currentHealth;
            
            data.SaveData = SaveManager._currentSaveData;

            return data;
        }

        private static IEnumerator LoadSaveStateRoutine()
        {
            if(data.SaveDataString is null) yield break;

            SaveManager.PrepareGameState(data.SaveData);
            Manager.GetMainMenu().LoadGameScene();
            yield return new WaitUntil(() => IsInGame);
            
            // Load the saved zip ring values.
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;

                if(ar is null) continue;

                Dictionary<Guid, bool> dict = data.ZipRings;

                if(!dict.ContainsKey(ar._guid)) continue;

                if(dict[ar._guid]) zip.Activate(true);
                else zip.Deactivate(true);
            }

            Player._spawnTransform.position = data.spawnTransformPos;
            Player.Teleport(data.playerPos);
            PlayerCamera.SetLookDirection(data.lookDir);
            Player._targetFacingDirection = data.facingDir;
            Player._currentFacingDirection = data.facingDir;

            yield return new WaitUntil(() => Player._currentHealth == 3);
            if(0 < data.health && data.health < 3) Player.SetCurrentHealth(data.health);
        }
    }
    
    [Serializable]
    public class SaveStateData
    {
        public Vector3 spawnTransformPos;
        public Vector3 playerPos;
        public Vector3 lookDir;
        public Vector3 facingDir;
        public int health;
        public Dictionary<Guid, bool> ZipRings => ZipRingGuids
            .Zip(ZipRingBools, (k, v) => new {k, v})
            .ToDictionary(x => new Guid(x.k), x => x.v);
        
        // Dictionaries aren't serializable for some reason so we're using two lists, which are.
        public List<string> ZipRingGuids = [];
        public List<bool> ZipRingBools = [];

        public string SaveDataString;
        public SaveData SaveData
        {
            get
            {
                SaveData data = new();
                JsonUtility.FromJsonOverwrite(SaveDataString, data);
                // May want to use a slot that is either in use or not in use, depending on how we want to implement it.
                // Just going to make a temporary save for now.
                data.name = "tempSave";
                return data;
            }
            set
            {
                SaveDataString = JsonUtility.ToJson(value);
            }
        }
        
        public SaveStateData() { }

        /// <summary>
        /// Creates a new SaveStateData from an already existing file.
        /// </summary>
        /// <param name="path">The path of the .json file to read from.</param>
        public SaveStateData(string path)
        {
            if(!File.Exists(path)) return;

            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), this);
        }

        public void WriteToFile(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
        
        public void AddToZipRings(Guid guid, bool activated)
        {
            ZipRingGuids.Add(guid.ToString());
            ZipRingBools.Add(activated);
        }
    }
}