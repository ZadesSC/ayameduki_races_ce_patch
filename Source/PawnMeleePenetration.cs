using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace zades.ayamedukiracece
{
    internal static class PawnMeleePenetration
    {
        public static void ApplyPatches(Harmony harmony)
        {
            Type melee = AccessTools.TypeByName("CombatExtended.Verb_MeleeAttackCE");
            if (melee == null)
            {
                return;
            }

            foreach (string property in new[] { "ArmorPenetrationSharp", "ArmorPenetrationBlunt" })
            {
                harmony.Patch(AccessTools.PropertyGetter(melee, property),
                    postfix: new HarmonyMethod(typeof(PawnMeleePenetration), nameof(ApplyPawnFactor)));
            }
        }

        private static void ApplyPawnFactor(Verb __instance, ref float __result)
        {
            // CE already applies the equipment factor. Hediffs and worn apparel modify the pawn instead.
            __result *= GetFactor(__instance);
        }

        public static float GetFactor(Verb verb)
        {
            Pawn pawn = verb?.CasterPawn;
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");
            return pawn == null || stat == null ? 1f : Math.Max(0f, pawn.GetStatValue(stat));
        }
    }
}
