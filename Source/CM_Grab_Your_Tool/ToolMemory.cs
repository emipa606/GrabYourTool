using RimWorld;
using Verse;

namespace CM_Grab_Your_Tool;

public class ToolMemory : IExposable
{
    private SkillDef lastCheckedSkill;
    public Pawn pawn;
    private Thing previousEquipped;
    private bool? usingTool = false;

    public bool IsUsingTool => usingTool.HasValue && usingTool.Value;
    public Thing PreviousEquipped => previousEquipped;

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn");
        Scribe_Defs.Look(ref lastCheckedSkill, "lastCheckedSkill");
        Scribe_Values.Look(ref usingTool, "usingTool");
        Scribe_References.Look(ref previousEquipped, "previousEquipped");
    }

    public bool UpdateSkill(SkillDef skill)
    {
        if (lastCheckedSkill == skill)
        {
            return false;
        }

        lastCheckedSkill = skill;
        return true;
    }

    public void UpdateUsingTool(Thing equipped, bool isUsingTool)
    {
        if (previousEquipped == null)
        {
            previousEquipped = equipped;
        }

        usingTool = isUsingTool;
    }
}