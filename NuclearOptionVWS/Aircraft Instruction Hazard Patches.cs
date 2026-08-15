using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;

namespace NuclearOptionVWS
{
    //For Fuel status and BINGO checks
    [HarmonyPatch(typeof(Aircraft), nameof(Aircraft.UseFuel))]
    internal static class BingoFuelAircraftPatch
    {
        static void Postfix(float fuelDrawn, Aircraft __instance)
        {
            Plugin.I.TriggerBINGOCheck(__instance, fuelDrawn);
        }
    }

    //For AoA
    [HarmonyPatch(typeof(AoADisplay), nameof(AoADisplay.Refresh))]
    internal static class AoAPatch
    {
        static void Postfix(float ___hornThreshold, float ___velocityThreshold)
        {
            Plugin.I.TriggerAoACheck(___hornThreshold,___velocityThreshold);
        }
    }

    //To reset BINGO and fuel statuses after refueling
    [HarmonyPatch(typeof(Aircraft),nameof(Aircraft.Refuel))]
    internal static class RefuelPatch
    {
        static void Postfix(Aircraft __instance)
        {
            Plugin.I.TriggerRefuelCheck(__instance);
        }
    }

    //To Kill all audio on player death
    [HarmonyPatch(typeof(Pilot), nameof(Pilot.ApplyDamage))]
    internal static class DamagePatch
    {
        static void Prefix(Pilot __instance, out bool __state)
        {
            __state = __instance.dead;
        }
        static void Postfix(Pilot __instance, bool __state)
        {
            if (__instance == null
                || __state || !GameManager.IsLocalPlayer<Player>(__instance.aircraft.Player)
                )
            {
                return;
            }
            else if (!__instance.dead)
            {
                Plugin.I.TriggerDamageCheck();
            }
            else
            {
                Plugin.I.TriggerDeathOutcome();
            }
        }
    }

}

