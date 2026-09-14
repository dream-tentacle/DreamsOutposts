using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public static class OutpostWaystationUtility
	{
		public const float MovementCostFactor = 0.25f;

		private const string WaystationDefName = "DreamsOutposts_Waystation";

		public static bool HasWaystationAt(PlanetTile tile)
		{
			if (!tile.Valid || Find.WorldObjects == null)
			{
				return false;
			}
			Outpost outpost = Find.WorldObjects.WorldObjectAt<Outpost>(tile);
			if (outpost == null || outpost.Faction != Faction.OfPlayer)
			{
				return false;
			}
			foreach (OutpostFacility facility in outpost.OperationalFacilities)
			{
				if (facility?.def?.defName == WaystationDefName)
				{
					return true;
				}
			}
			return false;
		}

		public static float TemporaryMovementFactorAt(PlanetTile tile)
		{
			if (!tile.Valid || Find.WorldObjects == null) return 1f;
			Outpost outpost = Find.WorldObjects.WorldObjectAt<Outpost>(tile);
			return outpost == null ? 1f : OutpostTemporaryEffectUtility.MovementCostFactor(outpost);
		}
	}

	[HarmonyPatch(typeof(WorldGrid), nameof(WorldGrid.GetRoadMovementDifficultyMultiplier))]
	public static class WorldGrid_GetRoadMovementDifficultyMultiplier_WaystationPatch
	{
		public static void Postfix(PlanetTile fromTile, PlanetTile toTile, StringBuilder explanation, ref float __result)
		{
			PlanetTile destination = toTile.Valid ? toTile : fromTile;
			bool hasWaystation = OutpostWaystationUtility.HasWaystationAt(destination);
			float temporaryFactor = OutpostWaystationUtility.TemporaryMovementFactorAt(destination);
			if (!hasWaystation && temporaryFactor == 1f) return;
			if (hasWaystation) __result *= OutpostWaystationUtility.MovementCostFactor;
			__result *= temporaryFactor;
			if (explanation != null)
			{
				if (explanation.Length > 0)
				{
					explanation.AppendLine();
				}
				if (hasWaystation) explanation.Append("DreamsOutposts.WaystationMovementFactor".Translate(OutpostWaystationUtility.MovementCostFactor.ToStringPercent()));
				if (temporaryFactor != 1f)
				{
					if (hasWaystation) explanation.AppendLine();
					explanation.Append("DreamsOutposts.TemporaryMovementFactor".Translate(temporaryFactor.ToStringPercent()));
				}
			}
		}
	}
}
