using System.Collections;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace TackleboxDbg
{
    static class SaveStateManager
    {
        private static readonly string saveStatesPath = Application.persistentDataPath + "/SaveStates";
        private static MainMenu MainMenu => Manager.GetMainMenu();
        private static PlayerMachine Player => Manager.GetPlayerMachine();
        private static SaveManager SaveManager => Manager.GetSaveManager();
        private static PlayerCamera PlayerCamera => Manager.GetPlayerCamera();
        private static bool IsInGame => MainMenu._currentState == MainMenu._inGameState;
        public static void Init()
        {
            Directory.CreateDirectory(saveStatesPath);
        }

        public static void SaveCurrentData()
        {
            if(!IsInGame) return;

            GetCurrentData().WriteToFile(saveStatesPath + "/savestate.json");
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
                
                data.ZipRings.Add(ar._guid, ar.GetValue());
            }
        
            data.playerPos = Player._position;
            data.lookDir = PlayerCamera._lookDirection;
            data.facingDir = Player._currentFacingDirection;
            data.health = Player._currentHealth;
            
            data.SaveData = SaveManager._currentSaveData;

            return data;
        }

        private static IEnumerator LoadSaveStateRoutine()
        {
            string currentSavePath = saveStatesPath + "/savestate.json";
            if(!File.Exists(currentSavePath)) yield break;

            SaveStateData data = new(currentSavePath);

            SaveManager.PrepareGameState(data.SaveData);
            MainMenu.LoadGameScene();
            yield return new WaitUntil(() => IsInGame);

            // Load the saved zip ring values.
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach(var zip in zipList)
            {
                var ar = zip._activatedReference;

                if(ar is null || !data.ZipRings.ContainsKey(ar._guid)) continue;

                if(data.ZipRings[ar._guid]) zip.Activate(true);
                else zip.Deactivate(true);
            }

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
        public Vector3 playerPos;
        public Vector3 lookDir;
        public Vector3 facingDir;
        public int health;

        // Dictionaries don't get serialized properly so we're using lists instead.
        [SerializeField]
        private List<string> ZipRingKeys = [];
        [SerializeField]
        private List<bool> ZipRingValues = [];
        public Dictionary<Guid, bool> ZipRings = [];

        [SerializeField]
        private string SaveDataString;
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

            ZipRings = ZipRingKeys
                .Zip(ZipRingValues, (k, v) => new {k, v})
                .ToDictionary(x => new Guid(x.k), x => x.v);
        }

        public void WriteToFile(string path)
        {
            ZipRingKeys = [.. ZipRings.Keys.Select(k => k.ToString())];
            ZipRingValues = [.. ZipRings.Values];
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}