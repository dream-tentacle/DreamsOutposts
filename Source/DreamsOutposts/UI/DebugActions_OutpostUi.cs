using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 开发者菜单里的新 UI 测试数据入口：一键把当前据点填满 / 清空，
	/// 方便直接验收「满级满槽位」「空槽位缺材料」等边界排版。
	/// 只造数据，不走付费路径，也不影响正常游玩逻辑。
	/// </summary>
	public static class DebugActions_OutpostUi
	{
		private static readonly string[] TestThingDefNames = new string[4] { "Steel", "WoodLog", "ComponentIndustrial", "Plasteel" };

		[DebugAction("DreamsOutposts", "Fill outpost (UI test data)")]
		public static void FillOutpostTestData()
		{
			Outpost outpost = TargetOutpost();
			if (outpost == null)
			{
				Log.Message("DreamsOutposts UI: no outpost is selected and no manage window is open.");
				return;
			}
			outpost.SetLevel(outpost.MaxLevel);
			List<OutpostFacilityDef> installable = OutpostUtility.InstallableFacilities(outpost.outpostTypeDef);
			int installed = 0;
			if (installable != null && outpost.extensionSlots != null)
			{
				for (int i = 0; i < outpost.extensionSlots.Count; i++)
				{
					OutpostSlot slot = outpost.extensionSlots[i];
					if (slot == null || !slot.IsEmpty)
					{
						continue;
					}
					OutpostFacilityDef def = PickFacility(installable, i);
					if (def == null)
					{
						continue;
					}
					slot.facility = OutpostFacility.Create(def);
					installed++;
				}
			}
			int items = 0;
			for (int i = 0; i < TestThingDefNames.Length; i++)
			{
				ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(TestThingDefNames[i]);
				if (thingDef != null)
				{
					items += OutpostStockUtility.AddToStock(outpost, thingDef, 500);
				}
			}
			int events = 0;
			List<OutpostEventDef> eventDefs = DefDatabase<OutpostEventDef>.AllDefsListForReading;
			if (!eventDefs.NullOrEmpty())
			{
				int wanted = (outpost.events != null && outpost.events.Count > 0) ? 0 : 2;
				for (int i = 0; i < wanted && i < eventDefs.Count; i++)
				{
					if (outpost.AddEvent(eventDefs[i]) != null)
					{
						events++;
					}
				}
			}
			Log.Message("DreamsOutposts UI: filled " + outpost.Label + " — level " + outpost.level + "/" + outpost.MaxLevel
				+ ", slots " + outpost.extensionSlots.Count + " (installed " + installed + "), items added " + items + ", events added " + events + ".");
		}

		[DebugAction("DreamsOutposts", "Clear outpost (UI test data)")]
		public static void ClearOutpostTestData()
		{
			Outpost outpost = TargetOutpost();
			if (outpost == null)
			{
				Log.Message("DreamsOutposts UI: no outpost is selected and no manage window is open.");
				return;
			}
			if (outpost.extensionSlots != null)
			{
				for (int i = 0; i < outpost.extensionSlots.Count; i++)
				{
					if (outpost.extensionSlots[i] != null)
					{
						outpost.extensionSlots[i].facility = null;
					}
				}
			}
			outpost.events?.Clear();
			outpost.scheduledEvents?.Clear();
			outpost.SetLevel(1);
			Log.Message("DreamsOutposts UI: cleared " + outpost.Label + " — level 1, all extension slots empty, events removed.");
		}

		private static OutpostFacilityDef PickFacility(List<OutpostFacilityDef> installable, int index)
		{
			for (int i = 0; i < installable.Count; i++)
			{
				OutpostFacilityDef def = installable[(index + i) % installable.Count];
				if (def != null && def.IsResearchUnlocked)
				{
					return def;
				}
			}
			return null;
		}

		private static Outpost TargetOutpost()
		{
			Window_OutpostManage window = Window_OutpostManage.Current;
			if (window != null && window.Outpost != null && !window.Outpost.Destroyed)
			{
				return window.Outpost;
			}
			return Find.WorldSelector.SingleSelectedObject as Outpost;
		}
	}
}
