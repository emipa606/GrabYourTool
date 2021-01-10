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

        public Dictionary<Pawn, GrabYourToolMemory> pawnMemory = new Dictionary<Pawn, GrabYourToolMemory>();

        public GrabYourToolMod(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("CM_Grab_Your_Tool");
            harmony.PatchAll();

            _instance = this;
        }

        public bool IsPawnUsingTool(Pawn pawn)
        {
            return (pawnMemory.ContainsKey(pawn) && pawnMemory[pawn].IsUsingTool);
        }

        public GrabYourToolMemory GetMemory(Pawn pawn)
        {
            if (!pawnMemory.ContainsKey(pawn))
                pawnMemory.Add(pawn, new GrabYourToolMemory());

            return pawnMemory[pawn];
        }

        public void ClearMemory(Pawn pawn)
        {
            if (pawnMemory.ContainsKey(pawn))
                pawnMemory.Remove(pawn);
        }
    }

    public class GrabYourToolMemory
    {
        private SkillDef lastCheckedSkill = null;
        private bool? usingTool = false;

        public bool IsUsingTool => (usingTool.HasValue ? usingTool.Value : false);

        public bool UpdateSkill(SkillDef skill)
        {
            if (lastCheckedSkill != skill)
            {
                lastCheckedSkill = skill;
                return true;
            }

            return false;
        }

        public void UpdateUsingTool(bool isUsingTool)
        {
            usingTool = isUsingTool;
        }
    }

    [StaticConstructorOnStartup]
    public static class ToilPatches
    {
        [HarmonyPatch(typeof(Toil))]
        [HarmonyPatch(MethodType.Constructor)]
        public static class Toil_Constructor
        {
            [HarmonyPostfix]
            public static void Postfix(Toil __instance)
            {
                if (__instance == null)
                    return;

                __instance.AddPreInitAction(delegate
                {
                    Pawn pawn = __instance.GetActor();

                    if (pawn == null || pawn.Dead || pawn.equipment == null || pawn.inventory == null || !pawn.RaceProps.Humanlike)
                        return;

                    if (pawn.Drafted)
                    {
                        GrabYourToolMod.Instance.ClearMemory(pawn);
                        return;
                    }

                    SkillDef activeSkill = pawn.CurJob?.RecipeDef?.workSkill;
                    if (__instance.activeSkill != null && __instance.activeSkill() != null)
                        activeSkill = __instance.activeSkill();

                    if (activeSkill != null)
                    {
                        GrabYourToolMemory memory = GrabYourToolMod.Instance.GetMemory(pawn);

                        if (!memory.UpdateSkill(activeSkill))
                            return;

                        // Don't do it if this job uses weapons (i.e. hunting)
                        if (activeSkill == SkillDefOf.Shooting || activeSkill == SkillDefOf.Melee)
                            memory.UpdateUsingTool(false);
                        // Check currently equipped item
                        else if (pawn.equipment.Primary != null && HasReleventStatModifiers(pawn.equipment.Primary, activeSkill))
                            memory.UpdateUsingTool(true);
                        // Try and find something else in inventory
                        else
                            memory.UpdateUsingTool(EquipAppropriateWeapon(pawn, activeSkill));
                    }
                    else
                    {
                        GrabYourToolMod.Instance.ClearMemory(pawn);
                    }
                });
            }

            private static bool EquipAppropriateWeapon(Pawn pawn, SkillDef skill)
            {
                //Log.Message("TryActuallyStartNextToil - EquipAppropriateWeapon");
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
                if (!__result && GrabYourToolMod.Instance.IsPawnUsingTool(___pawn))
                    __result = true;
            }
        }
    }
}
