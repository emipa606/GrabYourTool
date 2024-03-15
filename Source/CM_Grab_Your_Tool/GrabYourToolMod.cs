using System.Linq;
using HarmonyLib;
using Verse;

namespace CM_Grab_Your_Tool;

public class GrabYourToolMod : Mod
{
    public GrabYourToolMod(ModContentPack content) : base(content)
    {
        var harmony = new Harmony("CM_Grab_Your_Tool");
        harmony.PatchAll();

        Instance = this;
    }

    public static GrabYourToolMod Instance { get; private set; }

    public static bool UsingCombatExtended => ModsConfig.ActiveModsInLoadOrder.Any(m => m.Name == "Combat Extended");

    public ToolMemoryTracker ToolMemories => Current.Game.World.GetComponent<ToolMemoryTracker>();

    public bool IsPawnUsingTool(Pawn pawn)
    {
        return ToolMemories?.GetMemory(pawn)?.IsUsingTool ?? false;
    }

    public ToolMemory GetMemory(Pawn pawn)
    {
        return ToolMemories?.GetMemory(pawn);
    }

    public void ClearMemory(Pawn pawn)
    {
        ToolMemories?.ClearMemory(pawn);
    }
}