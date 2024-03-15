using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace CM_Grab_Your_Tool;

[StaticConstructorOnStartup]
public static class ToilPatches
{
    //[HarmonyPatch(typeof(Toil))]
    //[HarmonyPatch(MethodType.Constructor)]    
    [HarmonyPatch(typeof(JobDriver), "TryActuallyStartNextToil")]
    //public static class Toil_Constructor
    public static class JobDriver_TryActuallyStartNextToil
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ___pawn, JobDriver __instance)
        {
            if (___pawn == null || ___pawn.Dead || ___pawn.equipment == null || ___pawn.inventory == null ||
                !___pawn.RaceProps.Humanlike)
            {
                //Log.Message("Not a valid pawn");
                return;
            }

            if (___pawn.Drafted)
            {
                GrabYourToolMod.Instance.ClearMemory(___pawn);
                //Log.Message($"{___pawn} is drafted");
                return;
            }

            //Log.Message($"{___pawn} starting {__instance}");
            var activeSkill = ___pawn.CurJob?.RecipeDef?.workSkill;
            if (activeSkill == null && __instance.ActiveSkill != null)
            {
                activeSkill = __instance.ActiveSkill;
            }

            if (activeSkill != null)
            {
                var memory = GrabYourToolMod.Instance.GetMemory(___pawn);

                if (!memory.UpdateSkill(activeSkill))
                {
                    //Log.Message($"{___pawn} could not update memory");
                    return;
                }

                // Don't do it if this job uses weapons (i.e. hunting)
                if (activeSkill == SkillDefOf.Shooting || activeSkill == SkillDefOf.Melee)
                {
                    memory.UpdateUsingTool(null, false);
                    //Log.Message($"{___pawn} is using weapon");
                    return;
                }
                // Check currently equipped item

                if (___pawn.equipment.Primary != null &&
                    ToolMemoryTracker.HasReleventStatModifiers(___pawn.equipment.Primary, activeSkill, ___pawn, out _))
                {
                    memory.UpdateUsingTool(null, true);
                    //Log.Message($"{___pawn} primary tool already has modifier for {activeSkill}");
                    return;
                }
                // Try and find something else in inventory

                //Log.Message($"{___pawn} is looking for something else in the inventory");
                memory.UpdateUsingTool(___pawn.equipment.Primary,
                    ToolMemoryTracker.EquipAppropriateWeapon(___pawn, activeSkill));
                return;
            }

            GrabYourToolMod.Instance.ClearMemory(___pawn);
            //Log.Message("active skill is null");
        }
    }
}