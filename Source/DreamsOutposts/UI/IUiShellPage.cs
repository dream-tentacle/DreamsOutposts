using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 新外壳页面的标准契约：由 Window_OutpostManage 负责页头、滚动条与内边距，
	/// 页面只回答「内容多高」和「怎么画」。
	/// 老页面（沿用旧样式）不实现该接口，依旧走 DoContents。
	/// </summary>
	public interface IUiShellPage
	{
		/// <summary>侧栏摘要文字，null 表示不显示。</summary>
		string NavSummary { get; }

		/// <summary>页头描述，null 表示不显示（默认不再自动使用 Def 的 description）。</summary>
		string HeadDescription { get; }

		/// <summary>页头描述后面的补充提示，null 表示不加。</summary>
		string HeadHint { get; }

		/// <summary>正文（不含页头）的高度。availableHeight 是可视区高度，用来让栏目撑满到底部。</summary>
		float BodyHeight(float width, float availableHeight);

		/// <summary>绘制正文。rect.y 已按滚动偏移处理，availableHeight 与 BodyHeight 收到的是同一个值。</summary>
		void DrawBody(Rect rect, float availableHeight);
	}

	/// <summary>图标名 → 图标枚举，以及页面/设施的默认图标推断。</summary>
	public static class UiIconMap
	{
		public static UiIcon Parse(string name, UiIcon fallback)
		{
			if (string.IsNullOrEmpty(name))
			{
				return fallback;
			}
			try
			{
				UiIcon parsed = (UiIcon)Enum.Parse(typeof(UiIcon), name.Trim(), true);
				return (parsed == UiIcon.None) ? fallback : parsed;
			}
			catch (Exception)
			{
				Log.Warning("DreamsOutposts UI: unknown uiIcon \"" + name + "\"; falling back to " + fallback + ".");
				return fallback;
			}
		}

		public static UiIcon ForPage(OutpostManagePageDef def, Type pageClass)
		{
			if (def != null && !string.IsNullOrEmpty(def.uiIcon))
			{
				return Parse(def.uiIcon, UiIcon.Grid);
			}
			if (pageClass == typeof(Page_OutpostFacilities))
			{
				return UiIcon.Grid;
			}
			if (pageClass == typeof(Page_OutpostDefense))
			{
				return UiIcon.Shield;
			}
			if (pageClass == typeof(Page_OutpostInventory))
			{
				return UiIcon.Crate;
			}
			if (pageClass == typeof(Page_OutpostEvents))
			{
				return UiIcon.Bell;
			}
			if (pageClass == typeof(Page_OutpostTavern))
			{
				return UiIcon.Person;
			}
			return UiIcon.Dot;
		}

		public static UiIcon ForFacility(OutpostFacilityDef def)
		{
			if (def == null)
			{
				return UiIcon.Crate;
			}
			if (!string.IsNullOrEmpty(def.uiIcon))
			{
				return Parse(def.uiIcon, UiIcon.Crate);
			}
			string haystack = ((def.defName ?? string.Empty) + " " + (def.label ?? string.Empty)).ToLowerInvariant();
			UiIcon byKeyword = MatchKeywords(haystack);
			if (byKeyword != UiIcon.None)
			{
				return byKeyword;
			}
			if (def.Productions != null)
			{
				for (int i = 0; i < def.Productions.Count; i++)
				{
					ThingDef product = def.Productions[i]?.product;
					if (product == null)
					{
						continue;
					}
					UiIcon byProduct = MatchKeywords(((product.defName ?? string.Empty) + " " + (product.label ?? string.Empty)).ToLowerInvariant());
					if (byProduct != UiIcon.None)
					{
						return byProduct;
					}
				}
			}
			return UiIcon.Crate;
		}

		private static UiIcon MatchKeywords(string haystack)
		{
			if (string.IsNullOrEmpty(haystack))
			{
				return UiIcon.None;
			}
			if (Contains(haystack, "mining") || Contains(haystack, "mine") || Contains(haystack, "ore") || Contains(haystack, "drill") || Contains(haystack, "quarry"))
			{
				return UiIcon.Mine;
			}
			if (Contains(haystack, "log") || Contains(haystack, "timber") || Contains(haystack, "forest") || Contains(haystack, "wood"))
			{
				return UiIcon.Tree;
			}
			if (Contains(haystack, "farm") || Contains(haystack, "crop") || Contains(haystack, "field") || Contains(haystack, "greenhouse") || Contains(haystack, "ranch"))
			{
				return UiIcon.Farm;
			}
			if (Contains(haystack, "mortar") || Contains(haystack, "turret") || Contains(haystack, "artillery") || Contains(haystack, "defen") || Contains(haystack, "gun") || Contains(haystack, "ammo"))
			{
				return UiIcon.Turret;
			}
			if (Contains(haystack, "refin") || Contains(haystack, "sort") || Contains(haystack, "smelt") || Contains(haystack, "chem") || Contains(haystack, "process"))
			{
				return UiIcon.Refinery;
			}
			if (Contains(haystack, "workshop") || Contains(haystack, "craft") || Contains(haystack, "smith") || Contains(haystack, "machin") || Contains(haystack, "fab"))
			{
				return UiIcon.Workshop;
			}
			if (Contains(haystack, "power") || Contains(haystack, "generat") || Contains(haystack, "reactor") || Contains(haystack, "solar") || Contains(haystack, "battery"))
			{
				return UiIcon.Generator;
			}
			if (Contains(haystack, "store") || Contains(haystack, "storage") || Contains(haystack, "warehouse") || Contains(haystack, "depot") || Contains(haystack, "cache") || Contains(haystack, "silo"))
			{
				return UiIcon.Store;
			}
			if (Contains(haystack, "barrack") || Contains(haystack, "housing") || Contains(haystack, "hospital") || Contains(haystack, "clinic") || Contains(haystack, "shelter"))
			{
				return UiIcon.Store;
			}
			if (Contains(haystack, "plant") || Contains(haystack, "factory") || Contains(haystack, "works"))
			{
				return UiIcon.Factory;
			}
			return UiIcon.None;
		}

		private static bool Contains(string haystack, string needle)
		{
			return haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
		}
	}

	/// <summary>
	/// 侧栏摘要。新页面自己实现 IUiShellPage.NavSummary；
	/// 老页面（防卫 / 仓库 / 事件）由这里按类型兜底，每 tick 只算一次。
	/// </summary>
	public static class UiPageSummary
	{
		private static Outpost cachedOutpost;

		private static int cachedTick = -1;

		private static string facilitiesText;

		private static string defenseText;

		private static string warehouseText;

		private static string eventsText;

		private static int eventsBadge;

		public static string For(OutpostManagePage page, Outpost outpost)
		{
			if (page == null)
			{
				return null;
			}
			if (page is IUiShellPage shellPage)
			{
				return shellPage.NavSummary;
			}
			Ensure(outpost);
			if (page is Page_OutpostDefense)
			{
				return defenseText;
			}
			if (page is Page_OutpostInventory)
			{
				return warehouseText;
			}
			if (page is Page_OutpostEvents)
			{
				return eventsText;
			}
			return null;
		}

		public static int BadgeFor(OutpostManagePage page, Outpost outpost)
		{
			Ensure(outpost);
			return (page is Page_OutpostEvents) ? eventsBadge : 0;
		}

		private static void Ensure(Outpost outpost)
		{
			int tick = (Find.TickManager != null) ? Find.TickManager.TicksGame : 0;
			if (outpost == cachedOutpost && tick == cachedTick)
			{
				return;
			}
			cachedOutpost = outpost;
			cachedTick = tick;
			if (outpost == null)
			{
				facilitiesText = null;
				defenseText = null;
				warehouseText = null;
				eventsText = null;
				eventsBadge = 0;
				return;
			}
			int used = 0;
			int total = outpost.extensionSlots?.Count ?? 0;
			for (int i = 0; i < total; i++)
			{
				if (outpost.extensionSlots[i] != null && !outpost.extensionSlots[i].IsEmpty)
				{
					used++;
				}
			}
			facilitiesText = "DreamsOutposts.Ui.Nav.Slots".Translate(used, total).ToString();
			defenseText = "DreamsOutposts.Ui.Nav.Defense".Translate(outpost.Defense.ToString("0.#")).ToString();
			HashSet<ThingDef> kinds = new HashSet<ThingDef>();
			List<Thing> items = outpost.InventoryItems;
			for (int i = 0; i < items.Count; i++)
			{
				if (items[i] != null && !items[i].Destroyed && items[i].def != null)
				{
					kinds.Add(items[i].def);
				}
			}
			warehouseText = "DreamsOutposts.Ui.Nav.Items".Translate(kinds.Count).ToString();
			eventsBadge = outpost.events?.Count ?? 0;
			eventsText = "DreamsOutposts.Ui.Nav.Events".Translate(eventsBadge).ToString();
		}
	}
}
