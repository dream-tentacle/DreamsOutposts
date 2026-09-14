using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public sealed class RemotePowerSource
	{
		public Outpost Outpost;
		public OutpostFacility Facility;
		public OutpostFacilityComp_PowerGenerator Comp;
	}

	public static class RemotePowerUtility
	{
		public static bool IsValidReceiver(Building receiver)
		{
			return receiver != null && !receiver.Destroyed && receiver.Spawned && receiver.TryGetComp<CompRemotePowerReceiver>() != null;
		}

		public static IEnumerable<RemotePowerSource> AllSources()
		{
			if (Find.WorldObjects == null)
				yield break;
			foreach (Outpost outpost in Find.WorldObjects.AllWorldObjects.OfType<Outpost>())
			{
				if (outpost.Destroyed || outpost.Faction != Faction.OfPlayer)
					continue;
				foreach (OutpostFacility facility in outpost.OperationalFacilities)
				{
					OutpostFacilityComp_PowerGenerator comp = facility.GetComp<OutpostFacilityComp_PowerGenerator>();
					if (comp != null) yield return new RemotePowerSource { Outpost = outpost, Facility = facility, Comp = comp };
				}
			}
		}

		public static int Distance(RemotePowerSource source, Building receiver)
		{
			if (source?.Outpost == null || receiver?.Map == null)
				return int.MaxValue;
			return Find.WorldGrid.TraversalDistanceBetween(source.Outpost.Tile, receiver.Map.Tile, true);
		}

		public static float Efficiency(int distance)
		{
			if (distance == int.MaxValue)
				return 0f;
			return distance <= 2 ? 0.95f : Mathf.Pow(0.95f, distance - 1);
		}

		public static float PowerOutput(RemotePowerSource source, Building receiver)
		{
			if (source?.Comp == null || source.Comp.linkedReceiver != receiver || !source.Comp.IsPoweredNow)
				return 0f;
			return source.Comp.Props.basePowerOutput * Efficiency(Distance(source, receiver));
		}

		public static void NotifyReceiver(Building receiver)
		{
			if (IsValidReceiver(receiver))
				receiver.TryGetComp<CompRemotePowerReceiver>().NotifySourceChanged();
		}

	}
}
