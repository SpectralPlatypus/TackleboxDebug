using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Audio;

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
