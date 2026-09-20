using System.Collections.Generic;
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
		private static readonly Dictionary<PlanetTile, Outpost> OutpostsByTile = new Dictionary<PlanetTile, Outpost>();
		private static World cachedWorld;

		private static void EnsureOutpostIndex()
		{
			World world = Find.World;
			if (cachedWorld == world) return;
			cachedWorld = world;
			OutpostsByTile.Clear();
			List<WorldObject> worldObjects = Find.WorldObjects?.AllWorldObjects;
			for (int i = 0; i < (worldObjects?.Count ?? 0); i++)
			{
				if (worldObjects[i] is Outpost outpost) Register(outpost);
			}
		}

		private static void Register(Outpost outpost)
		{
			if (outpost?.Tile.Valid == true && !OutpostsByTile.ContainsKey(outpost.Tile))
			{
				OutpostsByTile.Add(outpost.Tile, outpost);
			}
		}

		public static void NotifyOutpostAdded(Outpost outpost)
		{
			EnsureOutpostIndex();
			Register(outpost);
		}

		public static void NotifyOutpostRemoved(Outpost outpost)
		{
			EnsureOutpostIndex();
			if (outpost != null && OutpostsByTile.TryGetValue(outpost.Tile, out Outpost indexed) && indexed == outpost)
			{
				OutpostsByTile.Remove(outpost.Tile);
			}
		}

		public static float MovementFactorAt(PlanetTile tile, out bool hasWaystation, out float temporaryFactor)
		{
			hasWaystation = false;
			temporaryFactor = 1f;
			if (!tile.Valid) return 1f;
			EnsureOutpostIndex();
			if (!OutpostsByTile.TryGetValue(tile, out Outpost outpost)) return 1f;

			if (outpost.Faction == Faction.OfPlayer)
			{
				foreach (OutpostFacility facility in outpost.OperationalFacilities)
				{
					if (facility?.def?.defName == WaystationDefName)
					{
						hasWaystation = true;
						break;
					}
				}
			}
			temporaryFactor = OutpostTemporaryEffectUtility.MovementCostFactor(outpost);
			return (hasWaystation ? MovementCostFactor : 1f) * temporaryFactor;
		}
	}

	[HarmonyPatch(typeof(WorldGrid), nameof(WorldGrid.GetRoadMovementDifficultyMultiplier))]
	public static class WorldGrid_GetRoadMovementDifficultyMultiplier_WaystationPatch
	{
		public static void Postfix(PlanetTile fromTile, PlanetTile toTile, StringBuilder explanation, ref float __result)
		{
			PlanetTile destination = toTile.Valid ? toTile : fromTile;
			float movementFactor = OutpostWaystationUtility.MovementFactorAt(destination, out bool hasWaystation, out float temporaryFactor);
			if (!hasWaystation && temporaryFactor == 1f) return;
			__result *= movementFactor;
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
