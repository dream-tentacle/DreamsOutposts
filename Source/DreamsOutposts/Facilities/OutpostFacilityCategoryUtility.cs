using System;
using System.Collections.Generic;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 设施分类的唯一查询入口：安装页的标签页列表，以及「设施没写 category 时归哪个分类」都在这里。
	/// 分类数据全部来自 DefDatabase；缓存用 DefCount 判断失效，DevMode 重载 Def 后会自动重建。
	/// </summary>
	public static class OutpostFacilityCategoryUtility
	{
		/// <summary>兜底分类的 defName：设施没写 category 时归到这里。</summary>
		public const string FallbackDefName = "DreamsOutposts_CategoryOther";

		private static readonly List<OutpostFacilityCategoryDef> ordered = new List<OutpostFacilityCategoryDef>();

		private static OutpostFacilityCategoryDef fallback;

		private static int cachedDefCount = -1;

		/// <summary>
		/// 兜底分类（其他）。万一同名 Def 被改名或删除，就退回排序后的第一个分类，并只报一次错误，
		/// 这样即使 Defs 被第三方改动，分类功能也不会连锁崩掉。
		/// </summary>
		public static OutpostFacilityCategoryDef Fallback
		{
			get
			{
				EnsureCache();
				return fallback;
			}
		}

		/// <summary>安装页标签页顺序：order 升序，相同 order 时按显示名。</summary>
		public static IReadOnlyList<OutpostFacilityCategoryDef> AllInOrder
		{
			get
			{
				EnsureCache();
				return ordered;
			}
		}

		/// <summary>设施的分类；没写 category 时返回兜底分类（其他）。</summary>
		public static OutpostFacilityCategoryDef Resolve(OutpostFacilityDef def)
		{
			return def?.category ?? Fallback;
		}

		private static void EnsureCache()
		{
			int count = DefDatabase<OutpostFacilityCategoryDef>.DefCount;
			if (count == cachedDefCount && fallback != null)
			{
				return;
			}
			cachedDefCount = count;
			ordered.Clear();
			ordered.AddRange(DefDatabase<OutpostFacilityCategoryDef>.AllDefsListForReading);
			ordered.Sort(Compare);
			fallback = DefDatabase<OutpostFacilityCategoryDef>.GetNamedSilentFail(FallbackDefName);
			if (fallback == null && count > 0)
			{
				fallback = ordered[0];
				Log.ErrorOnce("[DreamsOutposts] Facility category def " + FallbackDefName + " is missing; facilities without a category fall back to " + fallback.defName + " instead.", GenText.StableStringHash("DreamsOutposts.MissingFallbackCategory"));
			}
		}

		private static int Compare(OutpostFacilityCategoryDef a, OutpostFacilityCategoryDef b)
		{
			int byOrder = a.order.CompareTo(b.order);
			if (byOrder != 0)
			{
				return byOrder;
			}
			return string.Compare(a.label, b.label, StringComparison.OrdinalIgnoreCase);
		}
	}
}
