using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class OutpostFacilityCompProperties_PowerGenerator : OutpostFacilityCompProperties
	{
		public ThingDef fuel;
		public int fuelPerCycle;
		public int cycleTicks = 60000;
		public float basePowerOutput = 1000f;

		public OutpostFacilityCompProperties_PowerGenerator()
		{
			compClass = typeof(OutpostFacilityComp_PowerGenerator);
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string error in base.ConfigErrors()) yield return error;
			if (fuel == null) yield return "fuel is required.";
			if (fuelPerCycle <= 0) yield return "fuelPerCycle must be positive.";
			if (cycleTicks <= 0) yield return "cycleTicks must be positive.";
			if (basePowerOutput <= 0f || float.IsNaN(basePowerOutput) || float.IsInfinity(basePowerOutput)) yield return "basePowerOutput must be finite and positive.";
		}
	}

	public class OutpostFacilityComp_PowerGenerator : OutpostFacilityComp
	{
		public int poweredUntilTick;
		public int nextFuelCheckTick;
		public Building linkedReceiver;

		public OutpostFacilityCompProperties_PowerGenerator Props => (OutpostFacilityCompProperties_PowerGenerator)props;
		public bool IsPoweredNow => poweredUntilTick > Find.TickManager.TicksGame;

		public override void Tick(Outpost outpost, int delta)
		{
			int now = Find.TickManager.TicksGame;
			if (now < nextFuelCheckTick) return;
			if (!RemotePowerUtility.IsValidReceiver(linkedReceiver))
			{
				linkedReceiver = null;
				nextFuelCheckTick = now + 1250;
				return;
			}
			if (poweredUntilTick > now)
			{
				nextFuelCheckTick = poweredUntilTick;
				return;
			}

			if (OutpostStockUtility.CountInStock(outpost, Props.fuel) >= Props.fuelPerCycle &&
				OutpostStockUtility.TakeFromStock(outpost, Props.fuel, Props.fuelPerCycle) == Props.fuelPerCycle)
			{
				poweredUntilTick = now + Props.cycleTicks;
				nextFuelCheckTick = poweredUntilTick;
			}
			else
			{
				nextFuelCheckTick = now + 1250;
			}
			RemotePowerUtility.NotifyReceiver(linkedReceiver);
		}

		public override void PreRemove(Outpost outpost)
		{
			Building receiver = linkedReceiver;
			linkedReceiver = null;
			RemotePowerUtility.NotifyReceiver(receiver);
		}

		public override void BuildUiSections(Outpost outpost, List<UiFacilitySectionView> output)
		{
			int now = Find.TickManager.TicksGame;
			bool active = IsPoweredNow && RemotePowerUtility.IsValidReceiver(linkedReceiver);
			int remaining = active ? poweredUntilTick - now : 0;
			float progress = active ? Mathf.Clamp01((float)remaining / Props.cycleTicks) : 0f;
			int distance = linkedReceiver == null ? int.MaxValue : Find.WorldGrid.TraversalDistanceBetween(outpost.Tile, linkedReceiver.Map.Tile, true);
			float efficiency = RemotePowerUtility.Efficiency(distance);
			float watts = active ? Props.basePowerOutput * efficiency : 0f;
			string status = linkedReceiver == null
				? "DreamsOutposts.RemotePower.StatusUnbound".Translate().ToString()
				: (active ? "DreamsOutposts.RemotePower.StatusActive".Translate(watts.ToString("0"), remaining.ToStringTicksToPeriod()).ToString()
					: "DreamsOutposts.RemotePower.StatusWaitingFuel".Translate(Props.fuel.LabelCap, Props.fuelPerCycle).ToString());
			UiFacilitySectionView section = new UiFacilitySectionView
			{
				Title = "DreamsOutposts.RemotePower.SectionTitle".Translate(),
				IconThing = Props.fuel,
				MainText = active ? watts.ToString("0") + " W" : string.Empty,
				LeftText = status,
				RightText = linkedReceiver == null ? string.Empty : "DreamsOutposts.RemotePower.DistanceEfficiency".Translate(distance, efficiency.ToStringPercent()).ToString(),
				ShowProgress = true,
				Progress = progress,
				ProgressKind = active ? UiChipKind.Good : UiChipKind.Warn,
				Tooltip = "DreamsOutposts.RemotePower.FuelCycle".Translate(Props.fuel.LabelCap, Props.fuelPerCycle, Props.cycleTicks.ToStringTicksToPeriod()).ToString()
			};
			output.Add(section);
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref poweredUntilTick, "poweredUntilTick", 0);
			Scribe_Values.Look(ref nextFuelCheckTick, "nextFuelCheckTick", 0);
			Scribe_References.Look(ref linkedReceiver, "linkedReceiver");
		}
	}
}
