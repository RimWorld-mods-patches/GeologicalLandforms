#if RW_1_6_OR_GREATER

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace GeologicalLandforms.Patches;

/// <summary>
/// Keeps quest sites off impassable tiles by capping maxHilliness on the site query, rather than by
/// making CanSettleOnTile answer differently while a quest is being generated. The query reads the
/// tile's raw hilliness, so the cap costs nothing in determinism; CanSettleOnTile feeds a per-tile
/// cache and must not depend on when it is called.
/// </summary>
[PatchGroup("Main")]
[HarmonyPatch]
internal static class Patch_RimWorld_TileFinder_SiteQuery
{
    private static readonly Type Self = typeof(Patch_RimWorld_TileFinder_SiteQuery);

    [HarmonyTargetMethod]
    private static MethodBase TargetMethod()
    {
        // Two public overloads exist, so the name alone is ambiguous. The shorter one forwards to
        // this one, so patching it covers both.
        return AccessTools.Method(typeof(TileFinder), nameof(TileFinder.TryFindNewSiteTile),
        [
            typeof(PlanetTile).MakeByRefType(), typeof(PlanetTile), typeof(int), typeof(int), typeof(bool),
            typeof(List<LandmarkDef>), typeof(float), typeof(bool), typeof(TileFinderMode), typeof(bool),
            typeof(bool), typeof(PlanetLayer), typeof(Predicate<PlanetTile>)
        ]);
    }

    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Low)]
    private static IEnumerable<CodeInstruction> TryFindNewSiteTile_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // new TileQueryParams(nearTile, minDist, maxDist, landmarkMode, reachable: true,
        //     Hilliness.Undefined, Hilliness.Undefined, checkBiome: true, validSettlement: true, ...)
        // Anchored on the constructor so the run of constants cannot be matched elsewhere, and
        // limited to a single application so a changed signature fails loudly instead of silently
        // rewriting a different argument.
        var pattern = TranspilerPattern.Build("SiteQueryMaxHilliness")
            .Match(OpCodes.Ldc_I4_1).Keep()                     // reachable: true
            .Match(OpCodes.Ldc_I4_0).Keep()                     // minHilliness: Undefined
            .Match(OpCodes.Ldc_I4_0).Remove()                   // maxHilliness: Undefined
            .Insert(CodeInstruction.Call(Self, nameof(MaxHillinessForSiteQuery)))
            .Match(OpCodes.Ldc_I4_1).Keep()                     // checkBiome: true
            .Match(OpCodes.Ldc_I4_1).Keep()                     // validSettlement: true
            .MatchAny().Keep()                                  // comboLandmarks argument
            .MatchNewobj(typeof(FastTileFinder.TileQueryParams)).Keep()
            .Greedy(1, 1);

        return TranspilerPattern.Apply(instructions, pattern);
    }

    private static Hilliness MaxHillinessForSiteQuery()
    {
        return Patch_RimWorld_QuestNode_SiteTile.InQuestSiteSelection ? Hilliness.Mountainous : Hilliness.Undefined;
    }
}

#endif
