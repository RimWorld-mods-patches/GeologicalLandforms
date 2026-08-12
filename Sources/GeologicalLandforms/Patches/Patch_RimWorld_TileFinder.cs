using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using LunarFramework.Patching;
using RimWorld.Planet;
#if !RW_1_6_OR_GREATER
using RimWorld.QuestGen;
#endif
using Verse;

namespace GeologicalLandforms.Patches;

/// <summary>
/// Allow settling on impassable tiles that carry a landform or a biome which permits it.
/// The result of IsValidTileForNewSettlement is cached per tile by FastTileFinder on 1.6+, so this
/// must depend only on the tile itself - never on the calling thread or on ambient quest state.
/// </summary>
[PatchGroup("Main")]
[HarmonyPatch(typeof(TileFinder))]
internal static class Patch_RimWorld_TileFinder
{
    internal static readonly Type Self = typeof(Patch_RimWorld_TileFinder);

    [HarmonyTranspiler]
    [HarmonyPatch("IsValidTileForNewSettlement")]
    [HarmonyPriority(Priority.Low)]
    private static IEnumerable<CodeInstruction> IsValidTileForNewSettlement_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var brtrueSkip = new CodeInstruction(OpCodes.Brtrue_S);

        var pattern = TranspilerPattern.Build("CanSettleOnTile")
            .MatchLdloc().Replace(OpCodes.Ldarg_0)
            .MatchLoad(typeof(Tile), "hilliness").Remove()
            .Match(OpCodes.Ldc_I4_5).Remove()
            .Match(OpCodes.Bne_Un_S).StoreOperandIn(brtrueSkip).Remove()
            .Insert(CodeInstruction.Call(Self, nameof(CanSettleOnTile)))
            .Insert(brtrueSkip);

        return TranspilerPattern.Apply(instructions, pattern);
    }

    #if RW_1_6_OR_GREATER

    /// <summary>
    /// Whether a settlement may be placed on this impassable tile. Depends on the tile alone:
    /// FastTileFinder stores this per tile in CachedTileData, so an answer that varied with the
    /// calling thread or with ambient quest state would be frozen into that cache and served to
    /// every later caller. Quest sites are kept off impassable tiles by the site query instead,
    /// see Patch_RimWorld_TileFinder_SiteQuery.
    /// </summary>
    private static bool CanSettleOnTile(PlanetTile tile)
    {
        var world = Find.World;

        if (world.grid[tile].hilliness != Hilliness.Impassable) return true;

        if (world.HasFinishedGenerating())
        {
            var tileInfo = WorldTileInfo.Get(tile);
            if (tileInfo.Biome.Properties().allowSettlementsOnImpassableTerrain) return true;
            if (tileInfo.HasLandforms() && tileInfo.Landforms.Any(lf => !lf.IsLayer)) return true;
        }

        return false;
    }

    #else

    /// <summary>
    /// Pre-1.6 variant. There is no FastTileFinder on these versions, so nothing caches this result
    /// and the quest check can stay here, where it has been since impassable tiles first became
    /// settleable. It remains a conflation of two concerns, kept only to avoid changing behaviour
    /// on versions the caching problem never affected.
    /// </summary>
    private static bool CanSettleOnTile(int tile)
    {
        var world = Find.World;

        if (world.grid[tile].hilliness != Hilliness.Impassable) return true;

        if (QuestGen.Working) return false; // prevent quest sites from spawning on impassable tiles

        if (world.HasFinishedGenerating())
        {
            var tileInfo = WorldTileInfo.Get(tile);
            if (tileInfo.Biome.Properties().allowSettlementsOnImpassableTerrain) return true;
            if (tileInfo.HasLandforms() && tileInfo.Landforms.Any(lf => !lf.IsLayer)) return true;
        }

        return false;
    }

    #endif
}
