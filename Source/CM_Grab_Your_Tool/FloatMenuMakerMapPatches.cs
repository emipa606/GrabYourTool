using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace CM_Grab_Your_Tool;

[StaticConstructorOnStartup]
public static class FloatMenuMakerMapPatches
{
    [HarmonyPatch(typeof(FloatMenuMakerMap))]
    [HarmonyPatch("AddHumanlikeOrders", MethodType.Normal)]
    public static class FloatMenuMakerMap_AddHumanlikeOrders
    {
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodBase from = AccessTools.PropertyGetter(typeof(Map), "IsPlayerHome");
            MethodBase to = AccessTools.Method(typeof(FloatMenuMakerMap_AddHumanlikeOrders), "alwaysfalse");
            var replaceCall = !GrabYourToolMod.UsingCombatExtended;
            foreach (var instruction in instructions)
            {
                if (replaceCall && instruction.operand as MethodBase == from)
                {
                    yield return new CodeInstruction(OpCodes.Call, to);
                    continue;
                }

                yield return instruction;
            }
        }

        private static bool alwaysfalse(Map map)
        {
            return false;
        }
    }
}