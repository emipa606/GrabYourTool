using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CM_Grab_Your_Tool
{
    [StaticConstructorOnStartup]
    public static class Pawn_InventoryTrackerPatches
    {
        [HarmonyPatch(typeof(Pawn_JobTracker))]
        [HarmonyPatch("StartJob", MethodType.Normal)]
        public static class Pawn_JobTracker_StartJob
        {
            [HarmonyPostfix]
            public static void Postfix(Job newJob, Pawn_JobTracker __instance, Pawn ___pawn)
            {
                if (___pawn != null && !___pawn.Drafted && newJob.workGiverDef != null && newJob.workGiverDef.workType != null && newJob.workGiverDef.workType.relevantSkills != null)
                {
                    List<SkillDef> relevantSkills = new List<SkillDef>(newJob.workGiverDef.workType.relevantSkills);

                    // Don't do it if this job uses weapons (i.e. hunting)
                    if (relevantSkills.Contains(SkillDefOf.Shooting) || relevantSkills.Contains(SkillDefOf.Melee))
                        return;

                    ThingOwner heldThingsOwner = ___pawn.inventory.GetDirectlyHeldThings();
                    List<Thing> weaponsHeld = heldThingsOwner.Where(thing => thing.def.IsWeapon).ToList();

                    foreach(Thing weapon in weaponsHeld)
                    {
                        if (HasReleventStatModifiers(weapon, relevantSkills))
                        {
                            TryEquipWeapon(___pawn, weapon as ThingWithComps);
                            return;
                        }
                    }
                }
            }

            private static void TryEquipWeapon(Pawn pawn, ThingWithComps weapon)
            {
                if (weapon == null)
                    return;

                ThingWithComps currentWeapon = pawn.equipment.Primary;
                if (currentWeapon != null && pawn.inventory.innerContainer.TryAddOrTransfer(currentWeapon))
                {
                    if (weapon.stackCount > 1)
                    {
                        weapon = (ThingWithComps)weapon.SplitOff(1);
                    }
                    if (weapon.holdingOwner != null)
                    {
                        weapon.holdingOwner.Remove(weapon);
                    }
                    pawn.equipment.AddEquipment(weapon);
                    weapon.def.soundInteract?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
            }

            private static bool HasReleventStatModifiers(Thing weapon, List<SkillDef> relevantSkills)
            {
                List<StatModifier> statModifiers = weapon.def.equippedStatOffsets;
                if (relevantSkills != null && statModifiers != null)
                {
                    //Logger.MessageFormat(this, "Found relevantSkills...");
                    foreach (StatModifier statModifier in statModifiers)
                    {
                        List<SkillNeed> skillNeedOffsets = statModifier.stat.skillNeedOffsets;
                        List<SkillNeed> skillNeedFactors = statModifier.stat.skillNeedFactors;

                        if (skillNeedOffsets != null)
                        {
                            //Logger.MessageFormat(this, "Found skillNeedOffsets...");
                            foreach (SkillNeed skillNeed in skillNeedOffsets)
                            {
                                if (relevantSkills.Contains(skillNeed.skill))
                                {
                                    //Logger.MessageFormat(this, "{0} has {1}, relevant to {2}", weapon.Label, statModifier.stat.label, skillNeed.skill);
                                    return true;
                                }
                            }
                        }

                        if (skillNeedFactors != null)
                        {
                            foreach (SkillNeed skillNeed in skillNeedFactors)
                            {
                                if (relevantSkills.Contains(skillNeed.skill))
                                {
                                    //Logger.MessageFormat(this, "{0} has {1}, relevant to {2}", weapon.Label, statModifier.stat.label, skillNeed.skill);
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }
    }
}
