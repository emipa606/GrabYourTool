using HarmonyLib;
using Verse;

namespace CM_Grab_Your_Tool;

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
            {
                __result = true;
            }
        }
    }
}