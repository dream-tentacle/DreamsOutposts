using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	public class OutpostTypeDef : Def
	{
		public Type workerClass = typeof(OutpostWorker);

		public WorldObjectDef worldObjectDef;

		public OutpostFacilityDef coreFacility;

		public List<OutpostLevelProperties> levels = new List<OutpostLevelProperties>();

		[Unsaved(false)]
		private OutpostWorker worker;

		public int MaxLevel => levels.NullOrEmpty() ? 1 : levels.Count;

		public OutpostWorker Worker
		{
			get
			{
				if (worker == null)
				{
					worker = (OutpostWorker)Activator.CreateInstance(workerClass ?? typeof(OutpostWorker));
				}
				return worker;
			}
		}

		public OutpostLevelProperties GetLevel(int level)
		{
			if (levels.NullOrEmpty() || level < 1 || level > levels.Count)
			{
				return null;
			}
			return levels[level - 1];
		}

		public int GetSlotCount(int level)
		{
			return GetLevel(level)?.slotCount ?? 0;
		}

		public override void ResolveReferences()
		{
			base.ResolveReferences();
			if (worldObjectDef == null)
			{
				worldObjectDef = DefDatabase<WorldObjectDef>.GetNamed("DreamsOutposts_Outpost");
			}
		}

		public override IEnumerable<string> ConfigErrors()
		{
			foreach (string item in base.ConfigErrors())
			{
				yield return item;
			}
			if (coreFacility == null)
			{
				yield return "coreFacility is required: every outpost type must declare the facility that defines it.";
			}
			if (levels.NullOrEmpty())
			{
				yield return "levels is empty: every outpost type needs at least level 1 (inherit DreamsOutposts_OutpostBase, or write <levels Inherit=\"False\"> for a custom table).";
			}
			else
			{
				for (int i = 0; i < levels.Count; i++)
				{
					if (levels[i] == null)
					{
						yield return "levels[" + i + "] is null.";
						continue;
					}
					foreach (string item2 in levels[i].ConfigErrors(i))
					{
						yield return item2;
					}
				}
			}
			if (coreFacility != null && !coreFacility.IsAllowedIn(this))
			{
				yield return "coreFacility " + coreFacility.defName + " is not allowed in this outpost type; check its allowedOutpostTypes / disallowedOutpostTypes / whitelistOnly.";
			}
		}
	}
}
