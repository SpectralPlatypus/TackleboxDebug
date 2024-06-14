using HarmonyLib;
using UnityEngine;

namespace TackleboxDbg
{
    [HarmonyPatch]
    internal class Patches
    {
        [HarmonyPatch(typeof(Manager), "Update")]
        [HarmonyPostfix]
        public static void Update(Manager __instance)
        {
            if (Input.GetKeyDown(KeyCode.F11))
            {
                __instance.ToggleDebugEnabled();
            }

        }
    }
}
