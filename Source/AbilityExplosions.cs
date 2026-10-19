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

            if (projectileIndex < 0 || damageTypeIndex < 0)
            {
                Log.Error("[Ayameduki CE Patch] Unsupported vanilla explosion arguments.");
                return;
            }

            harmony.Patch(vanillaExplosion,
                prefix: new HarmonyMethod(typeof(AbilityExplosions), nameof(Prefix)));
        }

        private static bool Prefix(object[] __args)
        {
            ThingDef projectile = __args[projectileIndex] as ThingDef;
            if (projectile?.GetModExtension<AbilityExplosionExtension>() == null)
            {
                return true;
            }

            DamageDef damage = (DamageDef)__args[damageTypeIndex];
            object[] arguments = new object[ceParameters.Length];
            for (int index = 0; index < ceParameters.Length; index++)
            {
                ParameterInfo parameter = ceParameters[index];
                int originalIndex = Array.FindIndex(vanillaParameters, original =>
                    original.Name == parameter.Name && original.ParameterType == parameter.ParameterType);
                object value = originalIndex < 0 ? parameter.DefaultValue : __args[originalIndex];

                if (parameter.Name == "armorPenetration" && value is float penetration && penetration > 0f)
                {
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
