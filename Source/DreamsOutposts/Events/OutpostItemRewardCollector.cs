using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 一次事件选项中的物品奖励收集器。Effect 只把物品放进来；
	/// 事件框架在所有 effect 完成后统一入库并显示结果。
	/// </summary>
	public sealed class OutpostItemRewardCollector
	{
		private readonly Outpost outpost;
		private readonly List<Thing> things = new List<Thing>();
		private bool committed;

		public OutpostItemRewardCollector(Outpost outpost)
		{
			this.outpost = outpost;
		}

		public void Add(ThingDef thingDef, int count)
		{
			if (thingDef == null || count <= 0 || committed)
			{
				return;
			}
			int remaining = count;
			int stackLimit = Mathf.Max(thingDef.stackLimit, 1);
			while (remaining > 0)
			{
				Thing thing = ThingMaker.MakeThing(thingDef, thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null);
				thing.stackCount = Mathf.Min(remaining, stackLimit);
				remaining -= thing.stackCount;
				things.Add(thing);
			}
		}

		public void Add(Thing thing)
		{
			if (thing != null && !thing.Destroyed && thing.stackCount > 0 && !committed)
			{
				things.Add(thing);
			}
		}

		public void AddRange(IEnumerable<Thing> rewards)
		{
			if (rewards == null)
			{
				return;
			}
			foreach (Thing thing in rewards)
			{
				Add(thing);
			}
		}

		public void Commit(bool showWindow = true)
		{
			if (committed)
			{
				return;
			}
			committed = true;
			List<UiItemRewardView> received = BuildViews();
			if (received.Count == 0)
			{
				return;
			}
			OutpostProductionUtility.StoreInOutpostInventory(outpost, things);
			if (showWindow) UiItemRewardWindow.Open(outpost, received);
		}

		private List<UiItemRewardView> BuildViews()
		{
			List<UiItemRewardView> result = new List<UiItemRewardView>();
			Dictionary<string, UiItemRewardView> byLabel = new Dictionary<string, UiItemRewardView>();
			for (int i = 0; i < things.Count; i++)
			{
				Thing thing = things[i];
				if (thing == null || thing.Destroyed || thing.stackCount <= 0 || thing.def == null)
				{
					continue;
				}
				string label = thing.LabelCapNoCount;
				string key = thing.def.defName + "\n" + label;
				if (!byLabel.TryGetValue(key, out UiItemRewardView view))
				{
					view = new UiItemRewardView(thing.def, label);
					byLabel.Add(key, view);
					result.Add(view);
				}
				view.Count += thing.stackCount;
			}
			return result;
		}
	}

	public static class OutpostItemRewardUtility
	{
		public static void Add(OutpostEventContext context, ThingDef thingDef, int count)
		{
			RequireCollector(context).Add(thingDef, count);
		}

		public static void Add(OutpostEventContext context, Thing thing)
		{
			RequireCollector(context).Add(thing);
		}

		public static void AddRange(OutpostEventContext context, IEnumerable<Thing> things)
		{
			RequireCollector(context).AddRange(things);
		}

		private static OutpostItemRewardCollector RequireCollector(OutpostEventContext context)
		{
			if (context?.itemRewards == null)
			{
				throw new InvalidOperationException("An item reward effect ran without an initialized event reward collector.");
			}
			return context.itemRewards;
		}
	}
}
