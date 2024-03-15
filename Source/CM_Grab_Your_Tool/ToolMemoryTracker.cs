using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace CM_Grab_Your_Tool;

public class ToolMemoryTracker(World world) : WorldComponent(world)
{
    private List<ToolMemory> toolMemories = [];

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            toolMemories = toolMemories.Where(memory =>
                memory is { pawn: { Dead: false, Destroyed: false, Spawned: true } }).ToList();
        }

        Scribe_Collections.Look(ref toolMemories, "toolMemories", LookMode.Deep);

        CheckToolMemories();
    }

    // Users somehow ending up with null memory, and it's unfeasible to test get their save
    private void CheckToolMemories()
    {
        if (toolMemories == null)
        {
            toolMemories = [];
        }
    }

    public ToolMemory GetMemory(Pawn pawn)
    {
        CheckToolMemories();

        var toolMemory = toolMemories.Find(tm => tm != null && tm.pawn == pawn);
        if (toolMemory != null)
        {
            return toolMemory;
        }

        toolMemory = new ToolMemory
        {
            pawn = pawn
        };

        toolMemories.Add(toolMemory);

        return toolMemory;
    }

    public void ClearMemory(Pawn pawn)
    {
        CheckToolMemories();

        var toolMemory = toolMemories.Find(tm => tm != null && tm.pawn == pawn);
        if (toolMemory == null)
        {
            return;
        }

        var previouslyEquipped = toolMemory.PreviousEquipped;
        if (previouslyEquipped != null && pawn.inventory?.GetDirectlyHeldThings() != null &&
            pawn.inventory.GetDirectlyHeldThings().Contains(previouslyEquipped))
        {
            TryEquipWeapon(pawn, previouslyEquipped as ThingWithComps);
        }

        toolMemories.Remove(toolMemory);
    }


    public static bool EquipAppropriateWeapon(Pawn pawn, SkillDef skill)
    {
        if (pawn == null || skill == null)
        {
            return false;
        }

        ////Log.Message("TryActuallyStartNextToil - EquipAppropriateWeapon");
        var heldThingsOwner = pawn.inventory.GetDirectlyHeldThings();
        var weaponsHeld = heldThingsOwner.Where(thing => thing.def.IsWeapon).ToList();

        var maxEffectivness = 0f;
        ThingWithComps thingToEquip = null;
        foreach (var weapon in weaponsHeld)
        {
            if (!HasReleventStatModifiers(weapon, skill, pawn, out var effectivness))
            {
                continue;
            }

            if (effectivness <= maxEffectivness)
            {
                continue;
            }

            maxEffectivness = effectivness;
            thingToEquip = weapon as ThingWithComps;
        }

        return maxEffectivness > 0 && TryEquipWeapon(pawn, thingToEquip);
    }

    public static bool HasReleventStatModifiers(Thing weapon, SkillDef skill, Pawn pawn, out float effectivness)
    {
        effectivness = 0;
        if (weapon == null)
        {
            return false;
        }

        var statModifiers = weapon.def.equippedStatOffsets;
        if (skill == null || statModifiers == null)
        {
            return false;
        }

        //Log.Message("Found relevantSkills...");
        foreach (var statModifier in statModifiers)
        {
            var skillNeedOffsets = statModifier.stat.skillNeedOffsets;
            var skillNeedFactors = statModifier.stat.skillNeedFactors;

            if (skillNeedOffsets != null)
            {
                //Log.Message("Found skillNeedOffsets...");
                foreach (var skillNeed in skillNeedOffsets)
                {
                    if (skill != skillNeed.skill)
                    {
                        continue;
                    }

                    effectivness = skillNeed.ValueFor(pawn);
                    //Log.Message($"{weapon.Label} has {statModifier.stat.label}, relevant to {skillNeed.skill}");
                    return true;
                }
            }

            if (skillNeedFactors == null)
            {
                continue;
            }

            foreach (var skillNeed in skillNeedFactors)
            {
                if (skill != skillNeed.skill)
                {
                    continue;
                }

                effectivness = skillNeed.ValueFor(pawn);
                //Log.Message($"{weapon.Label} has {statModifier.stat.label}, relevant to {skillNeed.skill}");
                return true;
            }
        }

        return false;
    }

    public static bool TryEquipWeapon(Pawn pawn, ThingWithComps weapon)
    {
        if (pawn == null || weapon == null)
        {
            return false;
        }

        var currentWeapon = pawn.equipment.Primary;

        var transferSuccess = true;

        if (currentWeapon != null)
        {
            transferSuccess = pawn.inventory.innerContainer.TryAddOrTransfer(currentWeapon);
        }

        if (transferSuccess)
        {
            if (weapon.stackCount > 1)
            {
                weapon = (ThingWithComps)weapon.SplitOff(1);
            }

            weapon.holdingOwner?.Remove(weapon);

            pawn.equipment.AddEquipment(weapon);

            return true;
        }

        Log.Warning("CM_Grab_Your_Tool: Unable to transfer equipped weapon to inventory");

        return false;
    }
}