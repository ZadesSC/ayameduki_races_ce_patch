using System;
using HarmonyLib;
using Verse;

namespace zades.ayamedukiracece
{
    public sealed class WallPassingProjectileExtension : DefModExtension
    {
    }

    internal static class WallPassingProjectiles
    {
        public static void ApplyPatches(Harmony harmony)
        {
            Type projectile = AccessTools.TypeByName("CombatExtended.ProjectileCE");
            if (projectile == null)
            {
                return;
            }

            harmony.Patch(AccessTools.Method(projectile, "CanCollideWith"),
                prefix: new HarmonyMethod(typeof(WallPassingProjectiles), nameof(CanCollideWithPrefix)));
            harmony.Patch(AccessTools.Method(projectile, "TryCollideWithRoof"),
                prefix: new HarmonyMethod(typeof(WallPassingProjectiles), nameof(RoofCollisionPrefix)));
        }

        private static bool CanCollideWithPrefix(Thing __instance, Thing thing,
            LocalTargetInfo ___intendedTarget, ref float dist, ref bool __result)
        {
            if (__instance.def.GetModExtension<WallPassingProjectileExtension>() == null ||
                thing is Pawn || thing == ___intendedTarget.Thing)
            {
                return true;
            }

            // The spear phases through intervening scenery, but still uses CE hits on its target.
            dist = -1f;
            __result = false;
            return false;
        }

        private static bool RoofCollisionPrefix(Thing __instance, ref bool __result)
        {
            if (__instance.def.GetModExtension<WallPassingProjectileExtension>() == null)
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}
