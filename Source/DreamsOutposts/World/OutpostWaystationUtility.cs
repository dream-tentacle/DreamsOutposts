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
			foreach (OutpostFacility facility in outpost.Facilities)
			{
				if (facility?.def?.defName == WaystationDefName)
				{
					return true;
				}
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(WorldGrid), nameof(WorldGrid.GetRoadMovementDifficultyMultiplier))]
	public static class WorldGrid_GetRoadMovementDifficultyMultiplier_WaystationPatch
	{
		public static void Postfix(PlanetTile fromTile, PlanetTile toTile, StringBuilder explanation, ref float __result)
		{
			PlanetTile destination = toTile.Valid ? toTile : fromTile;
			if (!OutpostWaystationUtility.HasWaystationAt(destination))
			{
				return;
			}
			__result *= OutpostWaystationUtility.MovementCostFactor;
			if (explanation != null)
			{
				if (explanation.Length > 0)
				{
					explanation.AppendLine();
				}
				explanation.Append("DreamsOutposts.WaystationMovementFactor".Translate(OutpostWaystationUtility.MovementCostFactor.ToStringPercent()));
			}
		}
	}
}
