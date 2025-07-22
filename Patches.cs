using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace TackleboxDbg
{
    [HarmonyPatch]
    internal class PatchesCommon
    {
        public static event Action Layout;

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

            var stateField = codes.Find(c => c.opcode == OpCodes.Ldfld)?.operand as FieldInfo;
            if (stateField == null)
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
                Vector3 vector = (Vector3)dynMethod.Invoke(playerMachine, [0.5f]);
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


#if V1_1_0
    [HarmonyPatch]
    internal class Patch_110
    {
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(DearImGuiDemo), "<OnLayout>g__SaveButton|57_30")]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            int brOpCode = -1;
            int endFinallyOpCode = -1;
            Label return0255Label = il.DefineLabel();
            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_0)
                {
                    brOpCode = i;
                }
                else if (codes[i].opcode == OpCodes.Endfinally)
                {
                    endFinallyOpCode = i;
                    codes[endFinallyOpCode + 1].labels.Add(return0255Label);
                    break;
                }
            }
            if (brOpCode > -1 && endFinallyOpCode > -1)
            {
                codes[brOpCode] = new CodeInstruction(OpCodes.Br, return0255Label);
            }

            return codes.AsEnumerable();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(DearImGuiDemo), "OnLayout")]
        static IEnumerable<CodeInstruction> CustomTabPatch(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            return
                new CodeMatcher(instructions, il)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Call, typeof(ImGuiNET.ImGui).GetMethod("EndTabBar")))
                .SetAndAdvance(OpCodes.Ldsfld, typeof(PatchesCommon).GetField("Layout", BindingFlags.NonPublic | BindingFlags.Static))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, typeof(System.Action).GetMethod("Invoke")))
                .Insert(new CodeInstruction(OpCodes.Call, typeof(ImGuiNET.ImGui).GetMethod("EndTabBar")))
                .InstructionEnumeration();
        }
    }

#elif V1_2_4
    [HarmonyPatch]
    internal class Patch_124
    {
        static HashSet<Coin> assignedCoins = [];

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
            return coin._isActiveAndEnabled || assignedCoins.Contains(coin);
        }

        static MethodInfo myTestMethod = SymbolExtensions.GetMethodInfo(() => Test(null));
        [HarmonyPatch(typeof(ObjectPool<Coin>), "TryGetEntry")]
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
            [typeof(Vector3), typeof(Vector3), typeof(Vector3), typeof(CollectibleAsset), typeof(float), typeof(float), typeof(float), typeof(bool)])]
        [HarmonyPostfix]
        public static void Emit(ref Coin __result)
        {
            if (__result)
            {
                assignedCoins.Add(__result);
            }
        }

        [HarmonyPatch(typeof(CoinManager), "OnSceneLoaded")]
        [HarmonyPostfix]
        public static void ClearCoins()
        {
            assignedCoins.Clear();
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(DebugMenu), "OnLayout")]
        static IEnumerable<CodeInstruction> CustomTabPatch(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            return
                new CodeMatcher(instructions, il)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Call, typeof(ImGuiNET.ImGui).GetMethod("EndTabBar")))
                .SetAndAdvance(OpCodes.Ldsfld, typeof(PatchesCommon).GetField("Layout", BindingFlags.NonPublic | BindingFlags.Static))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, typeof(System.Action).GetMethod("Invoke")))
                .Insert(new CodeInstruction(OpCodes.Call, typeof(ImGuiNET.ImGui).GetMethod("EndTabBar")))
                .InstructionEnumeration();
        }
    }
#endif
}