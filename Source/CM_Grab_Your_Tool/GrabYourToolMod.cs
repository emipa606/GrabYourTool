using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CM_Grab_Your_Tool
{
    public class GrabYourToolMod : Mod
    {
        private static GrabYourToolMod _instance;
        public static GrabYourToolMod Instance => _instance;

        public GrabYourToolModMemory memory = new GrabYourToolModMemory();

        public GrabYourToolMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("CM_Grab_Your_Tool");
            harmony.PatchAll();

            _instance = this;
        }
    }

    public class GrabYourToolModMemory
    {
        public Dictionary<Pawn, Toil> lastCheckedToil = new Dictionary<Pawn, Toil>();
        public Dictionary<Pawn, SkillDef> lastCheckedSkill = new Dictionary<Pawn, SkillDef>();
        public Dictionary<Pawn, bool> pawnUsingTool = new Dictionary<Pawn, bool>();

        public bool UpdateToilEntry(Pawn pawn, Toil toil)
        {
            if (!lastCheckedToil.ContainsKey(pawn))
                lastCheckedToil.Add(pawn, toil);
            else if (lastCheckedToil[pawn] != toil)
                lastCheckedToil[pawn] = toil;
            else
                return false;

            return true;
        }

        public bool UpdateSkillEntry(Pawn pawn, SkillDef skill)
        {
            if (!lastCheckedSkill.ContainsKey(pawn))
                lastCheckedSkill.Add(pawn, skill);
            else if (lastCheckedSkill[pawn] != skill)
                lastCheckedSkill[pawn] = skill;
            else
                return false;

            return true;
        }

        public bool IsPawnUsingTool(Pawn pawn)
        {
            return pawnUsingTool.ContainsKey(pawn) && pawnUsingTool[pawn];
        }

        public void ClearPawn(Pawn pawn)
        {
            if (lastCheckedSkill.ContainsKey(pawn))
                lastCheckedToil.Remove(pawn);
            if (lastCheckedSkill.ContainsKey(pawn))
                lastCheckedSkill.Remove(pawn);
            if (pawnUsingTool.ContainsKey(pawn))
                pawnUsingTool.Remove(pawn);
        }
    }

    [StaticConstructorOnStartup]
    public static class JobDriverPatches
    {
        [HarmonyPatch(typeof(JobDriver))]
        [HarmonyPatch("TryActuallyStartNextToil", MethodType.Normal)]
        public static class JobDriver_TryActuallyStartNextToil
        {
            [HarmonyPostfix]
            public static void Postfix(JobDriver __instance, List<Toil> ___toils)
            {
                Pawn pawn = __instance.pawn;

                if (pawn == null || pawn.Dead || pawn.equipment == null || pawn.inventory == null || !pawn.RaceProps.Humanlike)
                    return;

                //Log.Message("TryActuallyStartNextToil Postfix");

                Toil currentToil = null;
                if (__instance.CurToilIndex >= 0 && __instance.CurToilIndex < ___toils.Count && __instance.job != null)
                    currentToil = ___toils[__instance.CurToilIndex];

                GrabYourToolModMemory memory = GrabYourToolMod.Instance.memory;

                if (pawn.Drafted || currentToil == null)
                {
                    memory.ClearPawn(pawn);
                    return;
                }

                SkillDef activeSkill = __instance.ActiveSkill ?? __instance.job.RecipeDef?.workSkill;
                if (activeSkill != null)
                {
                    if (!memory.UpdateToilEntry(pawn, currentToil) || !memory.UpdateSkillEntry(pawn, activeSkill))
                        return;

                    if (pawn.equipment.Primary != null && HasReleventStatModifiers(pawn.equipment.Primary, activeSkill))
                    {
                        //Log.Message("TryActuallyStartNextToil - Primary is already good weapon");
                        memory.pawnUsingTool[pawn] = true;
                    }
                    else
                    {
                        //Log.Message("TryActuallyStartNextToil - Primary is null or not relevant");
                        memory.pawnUsingTool[pawn] = EquipAppropriateWeapon(pawn, activeSkill);
                    }
                }
                else
                {
                    memory.ClearPawn(pawn);
                }
            }

            private static bool EquipAppropriateWeapon(Pawn pawn, SkillDef skill)
            {
                //Log.Message("TryActuallyStartNextToil - EquipAppropriateWeapon");

                // Don't do it if this job uses weapons (i.e. hunting)
                if (skill == SkillDefOf.Shooting || skill == SkillDefOf.Melee)
                    return false;

                ThingOwner heldThingsOwner = pawn.inventory.GetDirectlyHeldThings();
                List<Thing> weaponsHeld = heldThingsOwner.Where(thing => thing.def.IsWeapon).ToList();

                foreach (Thing weapon in weaponsHeld)
                {
                    if (HasReleventStatModifiers(weapon, skill))
                    {
                        return TryEquipWeapon(pawn, weapon as ThingWithComps);
                    }
                }

                return false;
            }

            private static bool HasReleventStatModifiers(Thing weapon, SkillDef skill)
            {
                List<StatModifier> statModifiers = weapon.def.equippedStatOffsets;
                if (skill != null && statModifiers != null)
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
                                if (skill == skillNeed.skill)
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
                                if (skill == skillNeed.skill)
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

            private static bool TryEquipWeapon(Pawn pawn, ThingWithComps weapon)
            {
                if (weapon == null)
                    return false;

                ThingWithComps currentWeapon = pawn.equipment.Primary;

                bool transferSuccess = true;

                if (currentWeapon != null)
                    transferSuccess = pawn.inventory.innerContainer.TryAddOrTransfer(currentWeapon);

                if (transferSuccess)
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
                    return true;
                }
                else
                {
                    Log.Warning("CM_Grab_Your_Tool: Unable to transfer equipped weapon to inventory");
                }

                return false;
            }
        }
    }

    //PawnRenderer
    [StaticConstructorOnStartup]
    public static class PawnRendererPatches
    {
        [HarmonyPatch(typeof(PawnRenderer))]
        [HarmonyPatch("CarryWeaponOpenly", MethodType.Normal)]
        public static class PawnRenderer_CarryWeaponOpenly
        {
            [HarmonyPostfix]
            public static void Postfix(ref bool __result, Pawn ___pawn)
            {
                if (!__result && GrabYourToolMod.Instance.memory.IsPawnUsingTool(___pawn))
                    __result = true;
            }
        }
    }
}
