#if RW_1_6_OR_GREATER

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;
using RimWorld.QuestGen;

namespace GeologicalLandforms.Patches;

/// <summary>
/// Marks the quest site selection entry points, so that the site query can exclude impassable tiles
/// without CanSettleOnTile having to read ambient quest state. Its result is cached per tile by
/// FastTileFinder and must stay a pure function of the tile.
/// </summary>
[PatchGroup("Main")]
[HarmonyPatch]
internal static class Patch_RimWorld_QuestNode_SiteTile
{
    private static bool _inQuestSiteSelection;

    internal static bool InQuestSiteSelection => _inQuestSiteSelection;

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var targets = new[]
        {
            AccessTools.Method(typeof(QuestNode_GetSiteTile), "TryFindTile"),
            AccessTools.Method(typeof(QuestNode_Root_Mission), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_Mission_AncientComplex), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_Hack_AncientComplex), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_Hack_WorshippedTerminal), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_Loot_AncientComplex), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_DistressCall), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_RelicHunt), "TryFindSiteTile"),
            AccessTools.Method(typeof(QuestNode_Root_ArchonexusVictory_ThirdCycle), "TryFindSiteTile"),

            // Runtime fallback: picks a tile when a quest reaches spawn time without one assigned.
            // QuestGen.Working is already false by then, so the old check never covered this path.
            //
            // QuestNode_Root_Site is deliberately absent: it defaults maxHilliness to Mountainous
            // and so caps itself, unless a def raises it, which is that def author's decision.
            AccessTools.Method(typeof(QuestPart_SpawnWorldObject), nameof(QuestPart_SpawnWorldObject.Notify_QuestSignalReceived))
        };

        // A renamed method yields null, which Harmony rejects. Skipping it loses coverage for that
        // one quest type rather than failing the whole patch group, so say which one was lost.
        for (var i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
                GeologicalLandformsAPI.Logger.Warn($"Quest site tile method #{i} not found, impassable tiles may be selected for that quest type.");
        }

        return targets.Where(m => m != null);
    }

    [HarmonyPrefix]
    private static void Prefix(ref bool __state)
    {
        // These nest: a patched method can run while another is still on the stack, so
        // restore the previous value instead of assuming the flag was clear on entry.
        __state = _inQuestSiteSelection;
        _inQuestSiteSelection = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(bool __state) => _inQuestSiteSelection = __state;
}

#endif
