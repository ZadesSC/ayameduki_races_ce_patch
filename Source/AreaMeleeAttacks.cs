using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace zades.ayamedukiracece
{
    public sealed class AreaMeleeExtension : DefModExtension
    {
        public float radius;
    }

    internal static class AreaMeleeAttacks
    {
        private static MethodInfo applyMeleeDamage;

        public static void ApplyPatches(Harmony harmony)
        {
            Type melee = AccessTools.TypeByName("CombatExtended.Verb_MeleeAttackCE");
            if (melee == null)
            {
                return;
            }

            applyMeleeDamage = AccessTools.Method(melee, "ApplyMeleeDamageToTarget");
            harmony.Patch(AccessTools.Method(melee, "TryCastShot"),
                postfix: new HarmonyMethod(typeof(AreaMeleeAttacks), nameof(AfterAttack)));
        }

        private static void AfterAttack(Verb __instance, bool __result)
        {
            AreaMeleeExtension extension = __instance.EquipmentSource?.def.GetModExtension<AreaMeleeExtension>();
            Pawn caster = __instance.CasterPawn;
            Thing primaryTarget = __instance.CurrentTarget.Thing;
            if (!__result || extension == null || caster?.Map == null || !(primaryTarget is Pawn))
            {
                return;
            }

            foreach (Pawn target in SecondaryTargets(caster, primaryTarget, extension.radius))
            {
                applyMeleeDamage.Invoke(__instance, new object[] { new LocalTargetInfo(target) });
            }
        }

        internal static IEnumerable<Pawn> SecondaryTargets(Pawn caster, Thing primaryTarget, float radius)
        {
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(caster.Position, radius, true))
            {
                if (!cell.InBounds(caster.Map))
                {
                    continue;
                }

                // Damage can destroy pawns or their equipment, so snapshot each cell before applying it.
                foreach (Pawn pawn in cell.GetThingList(caster.Map).OfType<Pawn>().ToList())
                {
                    if (pawn != caster && pawn != primaryTarget && pawn.Spawned && !pawn.Dead && pawn.HostileTo(caster))
                    {
                        yield return pawn;
                    }
                }
            }
        }
    }
}
