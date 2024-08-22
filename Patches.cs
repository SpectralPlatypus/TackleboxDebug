using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

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
}
