using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace TackleboxDbg
{
    class SaveStateManager
    {
        private readonly string saveStatesPath = Application.persistentDataPath + "/SaveStates";
        private MainMenu MainMenu => Manager.GetMainMenu();
        private PlayerMachine Player => Manager.GetPlayerMachine();
        private SaveManager SaveManager => Manager.GetSaveManager();
        private PlayerCamera PlayerCamera => Manager.GetPlayerCamera();

        public bool IsInGame => 
            MainMenu._currentState == MainMenu._inGameState || 
            MainMenu._currentState == MainMenu._gameplayState;

        public int CurrentIndex;
        private string[] SaveStateFiles =>
            [.. Directory.GetFiles(saveStatesPath).Where(x => x.EndsWith(".json"))];
        public string[] SaveStateNames => 
            [.. SaveStateFiles.Select(Path.GetFileNameWithoutExtension)];

        private SaveStateData CurrentData => new()
        {
            playerPos = Player._position,
            lookDir = PlayerCamera._lookDirection,
            facingDir = Player._currentFacingDirection,
            health = Player._currentHealth,

            SaveData = SaveManager._currentSaveData
        };

        public SaveStateManager()
        {
            Directory.CreateDirectory(saveStatesPath);
        }

        public void SaveCurrentDataToNewFile()
        {
            if (!IsInGame) return;
            
            int i = 1;
            string name = "SaveState";

            while(File.Exists($"{saveStatesPath}/{name}.json"))
            {
                i++;
                name = "SaveState" + i.ToString();
            }

            CurrentData.WriteToFile($"{saveStatesPath}/{name}.json");
            SetCurrentIndexByName(name);
        }

        public void SaveCurrentDataToQuickSlot()
        {
            string path = saveStatesPath + "/(quickslot).json";
            CurrentData.WriteToFile(path);
            SetCurrentIndexByName("(quickslot)");
        }

        public void LoadCurrent()
        {
            string path = Directory.GetFiles(saveStatesPath).ElementAt(CurrentIndex);
            if (IsInGame)
                Manager._instance.StartCoroutine(LoadSaveStateRoutine(path));
        }

        public void DeleteCurrent()
        {
            File.Delete(SaveStateFiles[CurrentIndex]);
            if(CurrentIndex == SaveStateFiles.Length) CurrentIndex--;
        }

        public void OpenSaveStateFolder()
        {
            Process.Start(saveStatesPath);
        }

        private void SetCurrentIndexByName(string name)
        {
            int i = Array.IndexOf(SaveStateNames, name);
            if(i != -1) CurrentIndex = i;
        }

        private IEnumerator LoadSaveStateRoutine(string path)
        {
            if (!File.Exists(path)) yield break;

            SaveStateData data = new(path);

            SaveManager.PrepareGameState(data.SaveData);
            MainMenu.LoadGameScene();
            yield return new WaitUntil(() => IsInGame);
            
            // The game doesn't load the checkpoint rods properly, so I'm doing it manually here.
            var zipList = GameObject.FindObjectsByType<ZipRing>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var zip in zipList)
            {
                BoolVariableReference actRef = zip._activatedReference;
                Dictionary<Guid, object> varRefs = data.SaveData._variableReferences;
                if (actRef is null || !varRefs.ContainsKey(actRef._guid)) continue;

                if ((bool)varRefs[actRef._guid]) zip.Activate(true);
                else zip.Deactivate(true);
            }
            
            if(data.playerPos != Vector3.zero) 
                Player.Teleport(data.playerPos);
            if(data.lookDir != Vector3.zero)
                PlayerCamera.SetLookDirection(data.lookDir);
            if(data.facingDir != Vector3.zero)
            {
                Player._targetFacingDirection = data.facingDir;
                Player._currentFacingDirection = data.facingDir;
            }

            yield return new WaitUntil(() => Player._currentHealth == 3);
            if (0 < data.health && data.health < 3) Player.SetCurrentHealth(data.health);
        }
    }

    [Serializable]
    public class SaveStateData
    {
        public Vector3 playerPos;
        public Vector3 lookDir;
        public Vector3 facingDir;
        public int health;

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
            if (!File.Exists(path)) return;

            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), this);
        }

        public void WriteToFile(string path)
        {
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }
    }
}