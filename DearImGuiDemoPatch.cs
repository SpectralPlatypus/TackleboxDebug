using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace TackleboxDbg
{
    [HarmonyPatch(typeof(DearImGuiDemo), "<OnLayout>g__SaveButton|57_30")]
    public static class DearImGuiDemoPatch
    {
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
    }
}
