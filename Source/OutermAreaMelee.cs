using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace zades.ayamedukiracece
{
    public sealed class OutermAreaMeleeExtension : DefModExtension
    {
        public float armorPenetrationSharp;
    }

    internal static class OutermAreaMelee
    {
        private static readonly AccessTools.StructFieldRef<DamageInfo, float> ArmorPenetration =
            AccessTools.StructFieldRefAccess<DamageInfo, float>("armorPenetrationInt");

        public static void ApplyPatches(Harmony harmony)
        {
            foreach (string typeName in new[]
            {
                "Outerm.HAR_OT_Apollyon_Verb_MeleeAreaAttack",
                "Outerm.HAR_OT_Idea_Verb_MeleeAreaAttack"
            })
            {
                Type type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    continue;
                }

                MethodInfo method = AccessTools.DeclaredMethod(type, "ApplyMeleeDamageToTarget");
                if (method == null)
                {
                    Log.Error($"[Ayameduki CE Patch] Missing area melee method on {typeName}.");
                    continue;
                }

                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(OutermAreaMelee), nameof(Transpiler)));
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo takeDamage = AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage));
            MethodInfo adjustDamage = AccessTools.Method(typeof(OutermAreaMelee), nameof(AdjustDamage));
            int replacements = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(takeDamage))
                {
                    // Keep the original area selection and special effects; convert only the damage packet.
                    var loadVerb = new CodeInstruction(OpCodes.Ldarg_0);
                    loadVerb.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    yield return loadVerb;
                    yield return new CodeInstruction(OpCodes.Call, adjustDamage);
                    replacements++;
                }

                yield return instruction;
            }

            if (replacements != 2)
            {
                Log.Error($"[Ayameduki CE Patch] Expected two area melee damage calls, found {replacements}.");
            }
        }

        public static DamageInfo AdjustDamage(DamageInfo damage, Verb verb)
        {
            ThingWithComps equipment = verb.EquipmentSource;
            OutermAreaMeleeExtension extension = equipment?.def.GetModExtension<OutermAreaMeleeExtension>();
            if (extension == null)
            {
                return damage;
            }

            StatDef penetrationFactor = DefDatabase<StatDef>.GetNamedSilentFail("MeleePenetrationFactor");
            float multiplier = penetrationFactor == null ? 1f : equipment.GetStatValue(penetrationFactor);
            ArmorPenetration(ref damage) = extension.armorPenetrationSharp * multiplier * PawnMeleePenetration.GetFactor(verb);
            return damage;
        }
    }
}
