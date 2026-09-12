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
		public OutpostProductionProperties_Power Properties;
		public OutpostProductionState_Power State;
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
				foreach (OutpostFacility facility in outpost.Facilities)
				{
					foreach (OutpostProductionProperties production in facility.def?.productions ?? new List<OutpostProductionProperties>())
					{
						OutpostProductionProperties_Power props = production as OutpostProductionProperties_Power;
						OutpostProductionState_Power state = facility.GetProductionState(production.id) as OutpostProductionState_Power;
						if (props != null && state != null)
							yield return new RemotePowerSource { Outpost = outpost, Facility = facility, Properties = props, State = state };
					}
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
			if (source?.State == null || source.State.linkedReceiver != receiver || !source.State.IsPoweredNow)
				return 0f;
			return source.Properties.basePowerOutput * Efficiency(Distance(source, receiver));
		}

		public static void NotifyReceiver(Building receiver)
		{
			if (IsValidReceiver(receiver))
				receiver.TryGetComp<CompRemotePowerReceiver>().NotifySourceChanged();
		}

		public static void NotifyFacilityRemoved(OutpostFacility facility)
		{
			if (facility?.productionStates == null)
				return;
			foreach (OutpostProductionState state in facility.productionStates)
			{
				OutpostProductionState_Power powerState = state as OutpostProductionState_Power;
				if (powerState?.linkedReceiver == null)
					continue;
				Building receiver = powerState.linkedReceiver;
				powerState.linkedReceiver = null;
				NotifyReceiver(receiver);
			}
		}
	}
}
