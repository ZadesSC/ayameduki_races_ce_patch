using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace zades.ayamedukiracece
{
    public sealed class AbilityExplosionExtension : DefModExtension
    {
    }

    internal static class AbilityExplosions
    {
        private static MethodInfo ceExplosion;
        private static ParameterInfo[] vanillaParameters;
        private static ParameterInfo[] ceParameters;
        private static int projectileIndex;
        private static int damageTypeIndex;
        private static int damageAmountIndex;

        [ThreadStatic]
        private static ThingDef detonatingSource;

        public static void ApplyPatches(Harmony harmony)
        {
            Type ceType = AccessTools.TypeByName("CombatExtended.GenExplosionCE");
            if (ceType == null)
            {
                return;
            }

            MethodInfo vanillaExplosion = AccessTools.Method(typeof(GenExplosion), "DoExplosion");
            ceExplosion = AccessTools.Method(ceType, "DoExplosion");
            vanillaParameters = vanillaExplosion.GetParameters();
            ceParameters = ceExplosion.GetParameters();
            projectileIndex = Array.FindIndex(vanillaParameters, parameter => parameter.Name == "projectile");
            damageTypeIndex = Array.FindIndex(vanillaParameters, parameter => parameter.Name == "damType");
            damageAmountIndex = Array.FindIndex(vanillaParameters, parameter => parameter.Name == "damAmount");

            // The two APIs share their explosion arguments. CE adds optional blast settings.
            foreach (ParameterInfo parameter in ceParameters)
            {
                bool shared = vanillaParameters.Any(original =>
                    original.Name == parameter.Name && original.ParameterType == parameter.ParameterType);
                if (!shared && !parameter.HasDefaultValue)
                {
                    Log.Error($"[Ayameduki CE Patch] Unsupported CE explosion argument: {parameter.Name}.");
                    return;
                }
            }

            if (projectileIndex < 0 || damageTypeIndex < 0 || damageAmountIndex < 0)
            {
                Log.Error("[Ayameduki CE Patch] Unsupported vanilla explosion arguments.");
                return;
            }

            harmony.Patch(vanillaExplosion,
                prefix: new HarmonyMethod(typeof(AbilityExplosions), nameof(Prefix)));
            harmony.Patch(AccessTools.Method(typeof(RimWorld.CompExplosive), "Detonate"),
                prefix: new HarmonyMethod(typeof(AbilityExplosions), nameof(BeginDetonation)),
                finalizer: new HarmonyMethod(typeof(AbilityExplosions), nameof(EndDetonation)));
        }

        private static void BeginDetonation(RimWorld.CompExplosive __instance, out ThingDef __state)
        {
            __state = detonatingSource;
            ThingDef source = __instance.parent.def;
            detonatingSource = source.GetModExtension<AbilityExplosionExtension>() != null ? source : null;
        }

        private static Exception EndDetonation(Exception __exception, ThingDef __state)
        {
            detonatingSource = __state;
            return __exception;
        }

        private static bool Prefix(object[] __args)
        {
            ThingDef projectile = __args[projectileIndex] as ThingDef;
            if (projectile?.GetModExtension<AbilityExplosionExtension>() == null && detonatingSource == null)
            {
                return true;
            }

            DamageDef damage = (DamageDef)__args[damageTypeIndex];
            int originalAmount = (int)__args[damageAmountIndex];
            int resolvedAmount = originalAmount < 0 ? Math.Max(1, damage.defaultDamage) : originalAmount;
            object[] arguments = new object[ceParameters.Length];
            for (int index = 0; index < ceParameters.Length; index++)
            {
                ParameterInfo parameter = ceParameters[index];
                int originalIndex = Array.FindIndex(vanillaParameters, original =>
                    original.Name == parameter.Name && original.ParameterType == parameter.ParameterType);
                object value = originalIndex < 0 ? parameter.DefaultValue : __args[originalIndex];

                if (parameter.Name == "damAmount" && value is int amount && amount < 0)
                {
                    value = resolvedAmount;
                }

                if (parameter.Name == "armorPenetration" && value is float penetration)
                {
                    if (originalAmount < 0)
                    {
                        penetration = damage.defaultArmorPenetration;
                    }
                    if (penetration < 0f)
                    {
                        penetration = resolvedAmount * 0.015f;
                    }
                    value = penetration;

                    // Convert vanilla armor units using the same scale as the race patches.
                    if (damage.armorCategory?.defName == "Sharp")
                    {
                        value = penetration * 16f;
                    }
                    else if (damage.armorCategory?.defName == "Blunt")
                    {
                        value = penetration * 40f;
                    }
                }

                arguments[index] = value;
            }

            ceExplosion.Invoke(null, arguments);
            return false;
        }
    }
}
