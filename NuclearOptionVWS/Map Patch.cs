using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace NuclearOptionVWS
{
    [HarmonyPatch(typeof(UnitMapIcon),nameof(UnitMapIcon.UpdateIcon))]
    internal static class MapIconPatch
    {
        static void Postfix(float mapDisplayFactor, float mapInverseScale, Transform mapTransform, bool mapMaximized, UnitMapIcon __instance)
        {
            Plugin.I.ObserveUnitBearingFromMapIcon(__instance.unit);
        }
    }
}
