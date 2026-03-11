using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace zades.ayamedukiracece
{
    [StaticConstructorOnStartup]
    public static class Aya_CE_Patch
    {
        private const string HarmonyId = "zades.ayamedukiracece";

        private static readonly Type IdhaleType = AccessTools.TypeByName("HAR_IH_Comp_Specification");
        private static readonly Type CeMeleeVerbType = AccessTools.TypeByName("CombatExtended.Verb_MeleeAttackCE");
        private static readonly Type OutermGrowingWeaponCompType = AccessTools.TypeByName("Outerm.HAR_OT_Comp_GrowingWeapon");
        private static readonly FieldInfo OutermExtraDamageField = OutermGrowingWeaponCompType != null
            ? AccessTools.Field(OutermGrowingWeaponCompType, "extraDamage")
            : null;

        static Aya_CE_Patch()
        {
            var harmony = new Harmony(HarmonyId);

            TryPatchIdhale(harmony);
            TryPatchOutermGrowingWeapons(harmony);
        }

        private static void TryPatchIdhale(Harmony harmony)
        {
            try
            {
                if (IdhaleType == null)
                {
                    Log.Warning("[Ayameduki CE Patch] HAR_IH_Comp_Specification type not found. Skipping Idhale patch.");
                    return;
                }

                MethodInfo compTick = AccessTools.Method(IdhaleType, "CompTick");
                if (compTick == null)
                {
                    Log.Warning("[Ayameduki CE Patch] HAR_IH_Comp_Specification.CompTick not found. Skipping Idhale patch.");
                    return;
                }

                harmony.Patch(
                    original: compTick,
                    prefix: new HarmonyMethod(typeof(Aya_CE_Patch), nameof(IdhaleCompTickPrefix))
                );

                Log.Message("[Ayameduki CE Patch] Harmony patches applied for Idhale mod.");
            }
            catch (Exception ex)
            {
                Log.Error($"[Ayameduki CE Patch] Error applying Idhale patch: {ex}");
            }
        }

        private static void TryPatchOutermGrowingWeapons(Harmony harmony)
        {
            try
            {
                if (CeMeleeVerbType == null)
                {
                    Log.Warning("[Ayameduki CE Patch] CombatExtended.Verb_MeleeAttackCE type not found. Skipping Outerm CE melee patch.");
                    return;
                }

                if (OutermGrowingWeaponCompType == null || OutermExtraDamageField == null)
                {
                    Log.Warning("[Ayameduki CE Patch] Outerm growing weapon comp not found. Skipping Outerm CE melee patch.");
                    return;
                }

                MethodInfo damageInfosToApply = AccessTools.Method(
                    CeMeleeVerbType,
                    "DamageInfosToApply",
                    new[] { typeof(LocalTargetInfo), typeof(bool) }
                ) ?? AccessTools.Method(CeMeleeVerbType, "DamageInfosToApply");

                if (damageInfosToApply == null)
                {
                    Log.Warning("[Ayameduki CE Patch] Verb_MeleeAttackCE.DamageInfosToApply not found. Skipping Outerm CE melee patch.");
                    return;
                }

                harmony.Patch(
                    original: damageInfosToApply,
                    postfix: new HarmonyMethod(typeof(Aya_CE_Patch), nameof(OutermGrowingWeaponDamageInfosPostfix))
                );

                Log.Message("[Ayameduki CE Patch] Harmony patch applied for Outerm growing weapons under CE melee.");
            }
            catch (Exception ex)
            {
                Log.Error($"[Ayameduki CE Patch] Error applying Outerm CE melee patch: {ex}");
            }
        }

        // Original Ayameduki code has this set at >=6, which I think is a bug since it cannot reach 6 or higher.
        // Instead of overhauling the code massively, increment the number to 6 if it is at 5 so the original
        // code for killing the boss and dropping loot runs as intended.
        public static bool IdhaleCompTickPrefix(ref int ___EX_Skill_Count)
        {
            if (___EX_Skill_Count == 5)
            {
                ___EX_Skill_Count = 6;
                Log.Message("[Ayameduki CE Patch] EX_Skill_Count set to 6.");
            }

            return true;
        }

        public static void OutermGrowingWeaponDamageInfosPostfix(object __instance, ref IEnumerable<DamageInfo> __result)
        {
            if (__result == null)
            {
                return;
            }

            float extraDamage = GetOutermGrowingWeaponDamage(__instance as Verb);
            if (extraDamage <= 0f)
            {
                return;
            }

            __result = ApplyOutermGrowingWeaponDamage(__result, extraDamage);
        }

        private static float GetOutermGrowingWeaponDamage(Verb verb)
        {
            ThingWithComps equipment = verb?.EquipmentSource;
            if (equipment?.AllComps == null)
            {
                return 0f;
            }

            foreach (ThingComp comp in equipment.AllComps)
            {
                if (!OutermGrowingWeaponCompType.IsInstanceOfType(comp))
                {
                    continue;
                }

                object extraDamage = OutermExtraDamageField.GetValue(comp);
                if (extraDamage is float damage)
                {
                    return damage;
                }

                break;
            }

            return 0f;
        }

        private static IEnumerable<DamageInfo> ApplyOutermGrowingWeaponDamage(IEnumerable<DamageInfo> damageInfos, float extraDamage)
        {
            bool applied = false;

            foreach (DamageInfo damageInfo in damageInfos)
            {
                if (!applied)
                {
                    DamageInfo adjustedDamageInfo = damageInfo;
                    adjustedDamageInfo.SetAmount(adjustedDamageInfo.Amount + extraDamage);
                    yield return adjustedDamageInfo;
                    applied = true;
                    continue;
                }

                yield return damageInfo;
            }
        }
    }
}
