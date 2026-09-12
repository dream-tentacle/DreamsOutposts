using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class CompProperties_RemotePowerReceiver : CompProperties_Power
	{
		public CompProperties_RemotePowerReceiver()
		{
			compClass = typeof(CompRemotePowerReceiver);
		}
	}

	public class CompRemotePowerReceiver : CompPowerPlant
	{
		private const int RefreshIntervalTicks = 1250;
		private readonly List<RemotePowerSource> cachedSources = new List<RemotePowerSource>();
		private float cachedPowerOutput;
		private int nextRefreshTick;

		protected override float DesiredPowerOutput
		{
			get
			{
				return cachedPowerOutput;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			RefreshCachedPower();
		}

		public override void UpdateDesiredPowerOutput()
		{
			if (Find.TickManager.TicksGame >= nextRefreshTick)
				RefreshCachedPower();
			base.UpdateDesiredPowerOutput();
		}

		private void RefreshCachedPower()
		{
			cachedSources.Clear();
			cachedPowerOutput = 0f;
			foreach (RemotePowerSource source in RemotePowerUtility.AllSources())
			{
				if (source.State.linkedReceiver != parent)
					continue;
				cachedSources.Add(source);
				cachedPowerOutput += RemotePowerUtility.PowerOutput(source, (Building)parent);
			}
			nextRefreshTick = Find.TickManager.TicksGame + RefreshIntervalTicks;
		}

		public void NotifySourceChanged()
		{
			RefreshCachedPower();
			base.UpdateDesiredPowerOutput();
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
				yield return gizmo;
			yield return new Command_Action
			{
				defaultLabel = "DreamsOutposts.RemotePower.Manage".Translate(),
				defaultDesc = "DreamsOutposts.RemotePower.ManageDesc".Translate(),
				action = OpenBindingMenu
			};
		}

		private void OpenBindingMenu()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>();
			foreach (RemotePowerSource source in RemotePowerUtility.AllSources())
			{
				RemotePowerSource captured = source;
				bool boundHere = source.State.linkedReceiver == parent;
				int distance = RemotePowerUtility.Distance(source, (Building)parent);
				float efficiency = RemotePowerUtility.Efficiency(distance);
				string marker = boundHere ? "[x] " : "[ ] ";
				string label = marker + source.Outpost.LabelCap + " — " + source.Facility.def.LabelCap + " (" + distance + ", " + efficiency.ToStringPercent() + ")";
				options.Add(new FloatMenuOption(label, delegate
				{
					Building oldReceiver = captured.State.linkedReceiver;
					captured.State.linkedReceiver = boundHere ? null : (Building)parent;
					if (oldReceiver != null && oldReceiver != parent)
						RemotePowerUtility.NotifyReceiver(oldReceiver);
					NotifySourceChanged();
				}));
			}
			if (options.Count == 0)
				options.Add(new FloatMenuOption("DreamsOutposts.RemotePower.NoSources".Translate(), null));
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public override string CompInspectStringExtra()
		{
			return "DreamsOutposts.RemotePower.LinkedCount".Translate(cachedSources.Count) + "\n" +
				"DreamsOutposts.RemotePower.TotalOutput".Translate(cachedPowerOutput.ToString("0")) + "\n" + base.CompInspectStringExtra();
		}
	}
}
