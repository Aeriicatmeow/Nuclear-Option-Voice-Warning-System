using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionVWS
{
    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.UseFuel))]
    internal static class BingoFuelAircraftPatch
    {
        static void Postfix(float fuelDrawn, Aircraft __instance)
        {
            Plugin.I.TriggerBINGOCheck(__instance, fuelDrawn);
        }
    }

    [HarmonyPatch(typeof(AoADisplay), nameof(AoADisplay.Refresh))]
    internal static class AoAPatch
    {
        static void Postfix(float ___hornThreshold, float ___velocityThreshold)
        {
            Plugin.I.TriggerAoACheck(___hornThreshold,___velocityThreshold);
        }
    }
    [HarmonyPatch(typeof(Aircraft),nameof(Aircraft.Refuel))]
    internal static class RefuelPatch
    {
        static void Postfix(Aircraft __instance)
        {
            Plugin.I.TriggerRefuelCheck(__instance);
        }
    }


}

