#if RW_1_6_OR_GREATER

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld.Planet;
using Verse;

namespace GeologicalLandforms.Patches;

/// <summary>
/// Keeps quest sites off impassable tiles by capping maxHilliness on the queries made while a quest
/// site is being selected, rather than by making CanSettleOnTile answer differently during quest
/// generation. The query compares the tile's own hilliness, so the cap costs nothing in
/// determinism; CanSettleOnTile feeds a per-tile cache and must not depend on when it is called.
/// </summary>
[PatchGroup("Main")]
[HarmonyPatch(typeof(FastTileFinder))]
internal static class Patch_RimWorld_FastTileFinder_SiteQuery
{
    private static readonly Type Self = typeof(Patch_RimWorld_FastTileFinder_SiteQuery);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(FastTileFinder.Query))]
    [HarmonyPriority(Priority.High)]
    private static void Query_Prefix(ref FastTileFinder.TileQueryParams query, ref FastTileFinder.TileQueryParams desperate)
    {
        ApplySiteCap(ref query, ref desperate);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(FastTileFinder.Closest))]
    [HarmonyPriority(Priority.High)]
    private static void Closest_Prefix(ref FastTileFinder.TileQueryParams query, ref FastTileFinder.TileQueryParams desperate)
    {
        ApplySiteCap(ref query, ref desperate);
    }

    private static void ApplySiteCap(ref FastTileFinder.TileQueryParams query, ref FastTileFinder.TileQueryParams desperate)
    {
        if (!Patch_RimWorld_QuestNode_SiteTile.InQuestSiteSelection) return;

        query = WithoutImpassable(query);

        // A desperate query left at its default is not Valid, and the query job skips it entirely.
        // Rebuilding one would make it Valid and add results that vanilla never asked for.
        if (desperate.Valid) desperate = WithoutImpassable(desperate);
    }

    private static FastTileFinder.TileQueryParams WithoutImpassable(FastTileFinder.TileQueryParams p)
    {
        // Undefined means no upper limit, so it has to be tightened as well. Any existing limit of
        // Mountainous or below already excludes impassable tiles and is left alone.
        if (p.maxHilliness != Hilliness.Undefined && p.maxHilliness <= Hilliness.Mountainous) return p;

        return new FastTileFinder.TileQueryParams(
            p.origin, p.minDistTiles, p.maxDistTiles, p.landmarkMode, p.reachable,
            p.minHilliness, Hilliness.Mountainous, p.checkBiome, p.validSettlement, p.comboLandmarks);
    }
}

#endif
