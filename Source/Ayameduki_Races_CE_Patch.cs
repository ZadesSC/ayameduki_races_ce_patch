using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace zades.ayamedukiracece
{

    [StaticConstructorOnStartup]
    public static class Aya_CE_Patch
    {
        private static readonly Type TargetType = AccessTools.TypeByName("HAR_IH_Comp_Specification");
        private static readonly PropertyInfo PawnProperty = TargetType?.GetProperty("pawn", BindingFlags.Public | BindingFlags.Instance);
        private static readonly FieldInfo ExSkillCountField = TargetType?.GetField("EX_Skill_Count", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Harmony initializer
        static Aya_CE_Patch()
        {
            // Check if the type from the other mod exists
            Type targetType = AccessTools.TypeByName("HAR_IH_Comp_Specification");
            if (targetType != null)
            {
                // Create a Harmony instance and patch the method
                var harmony = new Harmony("your.mod.id");
                harmony.Patch(
                    original: AccessTools.Method(targetType, "CompTick"), // Specify the method to patch
                    prefix: new HarmonyMethod(typeof(Aya_CE_Patch), nameof(Prefix))
                );
                Log.Message("[Ayameduki CE Patch] Harmony patches applied for Idhale mod.");
            }
            // else
            // {
            //     Log.Warning("[Ayameduki CE Patch] Idhale mod not found, skipping Harmony patches.");
            // }
            
            // var harmony = new Harmony("zades.ayamedukiracece");
            //
            // // Patch if the Idhale mod exists, otherwise, do not patch.
            // if (ModsConfig.IsActive("ayameduki.haridhale"))
            // {
            //     harmony.PatchAll();
            //     Log.Message("[Ayameduki CE Patch] Harmony patches applied for Idhale mod.");
            // }
        }
        
        // Original Ayameduki code has this set at >=6, which I think is a bug since it cannot reach 6 or higher,
        // Instead of overhauling the code massively, we will simply increment the number to 6 if it is at 5,
        // So that the original code for killing the boss and dropping loot runs as intended.
        public static bool Prefix(object __instance)
        {
            if (TargetType == null || PawnProperty == null || ExSkillCountField == null)
            {
                Log.Warning("[Ayameduki CE Patch] HAR_IH_Comp_Specification type or members not found. Skipping patch.");
                return true;
            }

            // Access the pawn
            Pawn pawn = PawnProperty.GetValue(__instance) as Pawn;
            if (pawn == null || !pawn.Downed)
            {
                return true; // Let the original method run
            }

            // Access and modify EX_Skill_Count
            int exSkillCount = (int)ExSkillCountField.GetValue(__instance);
            if (exSkillCount == 5)
            {
                ExSkillCountField.SetValue(__instance, 6);
                Log.Message("[Ayameduki CE Patch] EX_Skill_Count set to 6.");
            }

            return true; // Let the original method run
        }

    }
    
    // [HarmonyPatch(typeof(HAR_IH_Comp_Specification), nameof(HAR_IH_Comp_Specification.CompTick))]
    // public static class Patch_HAR_IH_Comp_Specification
    // {
    //     public static bool Prefix(HAR_IH_Comp_Specification __instance)
    //     {
    //         Pawn pawn = __instance.pawn;
    //         
    //         // Access the `EX_Skill_Count` variable from the original class
    //         int EX_Skill_Count = __instance.EX_Skill_Count;
    //
    //         if (!pawn.Downed || pawn == null)
    //         {
    //             return true;
    //         }
    //         if (EX_Skill_Count == 5)
    //         {
    //             //Original Ayameduki code has this set at >=6, which I think is a bug since it cannot reach 6 or higher,
    //             // Instead of overhauling the code massively, we will simply increment the number to 6 if it is at 5,
    //             // So that the original code for killing the boss and dropping loot runs as intended.
    //
    //             __instance.EX_Skill_Count = 6;
    //         }
    //         return true; // Let the original method run
    //     }
    //}
}