using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEngine.GridBrushBase;
using Logger = BepInEx.Logging.Logger;

namespace TackleboxDbg
{
    [HarmonyPatch]
    internal class Patches
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Coin), "CollectRoutine", MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> TranspileMoveNext(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            int index = codes.FindLastIndex(c => c.opcode == OpCodes.Call);
            if (index == -1 || !codes[index].Calls(typeof(Coin).GetMethod(nameof(Coin.Collect))))
            {
                return instructions;
            }

            var stateField = codes.Find(c=>c.opcode == OpCodes.Ldfld)?.operand as FieldInfo;
            if(stateField == null)
            {
                return instructions;
            }

            var corType = stateField.DeclaringType;
            var setPosType = AccessTools.Method(typeof(Transform), "set_position");

            var startPos = AccessTools.Field(corType, "<StartPos>5__4");
            codes.Insert(index++, new CodeInstruction(OpCodes.Ldloc_1));
            codes.Insert(index++, new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ManagedBehaviour), "get__transform")));
            codes.Insert(index++, new CodeInstruction(OpCodes.Ldarg_0));
            codes.Insert(index++, new CodeInstruction(OpCodes.Ldfld, startPos));
            codes.Insert(index++, new CodeInstruction(OpCodes.Callvirt, setPosType));

            return codes.AsEnumerable();
        }

        static MethodInfo dynMethod = typeof(PlayerMachine).GetMethod("BestValidGroundFromHistory", BindingFlags.NonPublic | BindingFlags.Instance);
        [HarmonyPatch(typeof(CheckpointUI), nameof(Checkpoint.ManagedFixedUpdate))]
        [HarmonyPostfix]
        public static void ManagedFixedUpdate(CheckpointUI __instance)
        {
            if (!Plugin.OverrideDebugArrow.Value)
            {
                return;
            }

            var currentGround = __instance._artParent.gameObject;
            if (!Manager._instance._debugEnabled)
            {
                currentGround.SetActive(false);
                return;
            }
            if (Manager.GetMainMenu().IsMenuActive())
            {
                currentGround.SetActive(value: false);
                return;
            }
            if (Manager.GetCutsceneCoordinator()._cutscenePlaying.GetValue())
            {
                currentGround.SetActive(value: false);
                return;
            }
            PlayerMachine playerMachine = Manager.GetPlayerMachine();
            if (!playerMachine)
            {
                return;
            }
            MainCamera mainCamera = Manager.GetMainCamera();
            Vector3 difference;
            bool hit;
            RaycastHit info;
            if ((bool)mainCamera)
            {
                Vector3 vector = (Vector3)dynMethod.Invoke(playerMachine, new object[] { 0.5f });
                Vector3 a = vector + -playerMachine._gravityDirection * 4f;
                difference = a - mainCamera._transform.position;
                hit = Physics.Raycast(mainCamera._transform.position, difference.normalized, out info, float.PositiveInfinity, Manager.GetPhysicsConfig().Ground, QueryTriggerInteraction.Ignore);
                if (mainCamera.FacingTowards(vector) && (!hit || (info.distance >= difference.magnitude)))
                {
                    currentGround.SetActive(value: true);
                    __instance._artParent.anchoredPosition = Manager.GetUICamera().WorldSpaceToCanvasPosition(vector);
                }
                else
                {
                    currentGround.SetActive(value: false);
                }
            }
        }
    }

    [HarmonyPatch]
    internal class Patch_1240
    {
        static HashSet<Coin> assignedCoins = new();

        static readonly Version expVersion = new Version(1, 0, 9149, 40858);

        static ManualLogSource logSource = Logger.CreateLogSource(nameof(Patch_1240));
        [HarmonyPrepare]
        static bool PatchVersion()
        {
            var version = Plugin.GetVersion();
            if (version != expVersion)
            {
                logSource.LogWarning("Skipping patches for this version!");
                return false;
            }

            return true;
        }

        // Enemy coin respawn fix
        [HarmonyPatch(typeof(FiletNavAgent), nameof(FiletNavAgent.Respawn))]
        [HarmonyPostfix]
        public static void Respawn(FiletNavAgent __instance)
        {
            foreach (Collectible collectible in __instance._collectibles)
            {
                if (collectible && !collectible._collected && collectible._emittedCoin)
                {
                    assignedCoins.Remove(collectible._emittedCoin);
                    collectible._emittedCoin = null;
                }
            }
        }

        static bool Test(Coin coin)
        {
            logSource.LogWarning($"Called with active: {coin._isActiveAndEnabled}, in list: {assignedCoins.Contains(coin)}");
            return coin._isActiveAndEnabled || assignedCoins.Contains(coin);
        }

        static MethodInfo myTestMethod = SymbolExtensions.GetMethodInfo(() => Test(null));
        [HarmonyPatch(typeof(ObjectPool<Coin>), nameof(ObjectPool<Coin>.TryGetEntry))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TranspileTryGetEntry(IEnumerable<CodeInstruction> instructions)
        {
            return new CodeMatcher(instructions)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Box),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ManagedBehaviour), "get__isActiveAndEnabled")))
                    .Repeat(matcher =>
                        matcher.SetInstructionAndAdvance(
                            new CodeInstruction(OpCodes.Call, myTestMethod))
                        .RemoveInstruction())
                    .InstructionEnumeration();
        }

        [HarmonyPatch(typeof(CoinManager), "Emit", 
            [typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(CollectibleAsset), typeof(float), typeof(float), typeof(float),typeof(bool)])]
        [HarmonyPostfix]
        public static void Emit(ref Coin __result)
        {
            if(__result)
            {
                assignedCoins.Add(__result);
                logSource.LogWarning("Added coin to list: " + __result._name);
            }
        }

        [HarmonyPatch(typeof(CoinManager), "OnSceneLoaded")]
        [HarmonyPostfix]
        public static void OnSceneLoaded()
        {
            assignedCoins.Clear();
        }
    }
}
