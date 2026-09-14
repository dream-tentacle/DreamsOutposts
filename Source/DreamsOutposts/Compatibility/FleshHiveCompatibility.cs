using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace DreamsOutposts
{
	public sealed class FleshHiveCompatibility : IOutpostCompatibility
	{
		private const string RelayOutpostDefName = "DreamsOutposts_FleshHiveRelay";
		private const string RelayNodeDefName = "DreamsOutposts_NerveExpansionSlot";

		public string PackageId => "HaiLuan.FleshHive";

		public void Apply(Harmony harmony)
		{
			Type mapComponentType = AccessTools.TypeByName("FleshHive.MapComponent_FleshHive");
			if (mapComponentType == null)
			{
				Log.Error("[DreamsOutposts] Flesh Hive compatibility could not find MapComponent_FleshHive; relay outposts will not add capacity.");
				return;
			}
			MethodInfo capacityGetter = AccessTools.PropertyGetter(mapComponentType, "HiveGroupCostLimit");
			MethodInfo postfix = AccessTools.Method(typeof(FleshHiveCompatibility), nameof(AddRelayCapacity));
			if (capacityGetter == null || postfix == null)
			{
				Log.Error("[DreamsOutposts] Flesh Hive compatibility could not find HiveGroupCostLimit; relay outposts will not add capacity.");
				return;
			}

			harmony.Patch(capacityGetter, postfix: new HarmonyMethod(postfix));
		}

		private static void AddRelayCapacity(ref int __result)
		{
			WorldObjectsHolder worldObjects = Find.WorldObjects;
			if (worldObjects == null)
			{
				return;
			}

			int bonus = 0;
			foreach (WorldObject worldObject in worldObjects.AllWorldObjects)
			{
				Outpost outpost = worldObject as Outpost;
				if (outpost?.Faction != Faction.OfPlayer || outpost.outpostTypeDef?.defName != RelayOutpostDefName)
				{
					continue;
				}

				bonus += 3 + 2 * Math.Max(0, outpost.level - 1);
				foreach (OutpostFacility facility in outpost.OperationalFacilities)
				{
					if (facility?.def?.defName == RelayNodeDefName)
					{
						bonus += 3;
					}
				}
			}

			__result += bonus;
		}
	}
}
