using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 据点防卫页面。数值全部从 OutpostDefenseUtility 读取，本页不做任何计算，也不修改任何状态。
	/// </summary>
	public class Page_OutpostDefense : OutpostManagePage
	{
		private const float SummaryHeight = 26f;

		private const float ColumnHeaderHeight = 26f;

		private const float RowHeight = 30f;

		private const float ColumnGap = 14f;

		private const float ScrollBarWidth = 16f;

		private const float ValueWidth = 44f;

		private const float IconSize = 26f;

		private readonly List<Pawn> pawns = new List<Pawn>();

		private readonly List<OutpostFacility> facilities = new List<OutpostFacility>();

		private Vector2 pawnsScroll;

		private Vector2 facilitiesScroll;

		public override string Label => "DreamsOutposts.Defense".Translate();

		public override void DoContents(Rect inRect)
		{
			RefreshContents();
			Widgets.Label(new Rect(0f, 0f, inRect.width, SummaryHeight), "DreamsOutposts.Defense".Translate() + ": " + outpost.Defense.ToString("0.#"));
			float y = SummaryHeight + 10f;
			Rect columnsRect = new Rect(0f, y, inRect.width, Mathf.Max(inRect.height - y, 0f));
			float availableWidth = Mathf.Max(columnsRect.width - ColumnGap, 0f);
			float pawnWidth = Mathf.Floor(availableWidth * 0.5f);
			DrawPawnColumn(new Rect(columnsRect.x, columnsRect.y, pawnWidth, columnsRect.height));
			DrawFacilityColumn(new Rect(columnsRect.x + pawnWidth + ColumnGap, columnsRect.y, availableWidth - pawnWidth, columnsRect.height));
		}

		private void RefreshContents()
		{
			pawns.Clear();
			facilities.Clear();
			if (outpost?.pawns != null)
			{
				List<Pawn> allPawns = outpost.pawns.InnerListForReading;
				for (int i = 0; i < allPawns.Count; i++)
				{
					if (allPawns[i] != null)
					{
						pawns.Add(allPawns[i]);
					}
				}
				// 从最能打的往下排，方便一眼看出防卫是谁撑起来的。
				pawns.Sort(ComparePawnsByDefense);
			}
			if (outpost != null)
			{
				// 保持核心设施在前、扩展槽按槽位顺序，和据点设施页一致。
				foreach (OutpostFacility facility in outpost.Facilities)
				{
					facilities.Add(facility);
				}
			}
		}

		private static int ComparePawnsByDefense(Pawn a, Pawn b)
		{
			return OutpostDefenseUtility.PawnDefense(b).CompareTo(OutpostDefenseUtility.PawnDefense(a));
		}

		private void DrawPawnColumn(Rect outRect)
		{
			int total = OutpostDefenseUtility.PawnDefenseTotal(outpost);
			Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, ColumnHeaderHeight), "DreamsOutposts.DefenseFromPawns".Translate(total.ToString("0.#")) + " (" + pawns.Count + ")");
			Rect viewRect = new Rect(outRect.x, outRect.y + ColumnHeaderHeight, outRect.width, Mathf.Max(outRect.height - ColumnHeaderHeight, 0f));
			Rect contentRect = new Rect(0f, 0f, Mathf.Max(viewRect.width - ScrollBarWidth, 0f), (float)pawns.Count * RowHeight);
			Widgets.BeginScrollView(viewRect, ref pawnsScroll, contentRect);
			if (pawns.Count == 0)
			{
				Widgets.Label(new Rect(0f, 0f, contentRect.width, RowHeight), "DreamsOutposts.None".Translate());
			}
			else
			{
				for (int i = 0; i < pawns.Count; i++)
				{
					DrawPawnRow(new Rect(0f, (float)i * RowHeight, contentRect.width, RowHeight), pawns[i], i);
				}
			}
			Widgets.EndScrollView();
		}

		private void DrawFacilityColumn(Rect outRect)
		{
			float total = OutpostDefenseUtility.FacilityDefenseTotal(outpost);
			Widgets.Label(new Rect(outRect.x, outRect.y, outRect.width, ColumnHeaderHeight), "DreamsOutposts.DefenseFromFacilities".Translate(total.ToString("0.#")) + " (" + facilities.Count + ")");
			Rect viewRect = new Rect(outRect.x, outRect.y + ColumnHeaderHeight, outRect.width, Mathf.Max(outRect.height - ColumnHeaderHeight, 0f));
			Rect contentRect = new Rect(0f, 0f, Mathf.Max(viewRect.width - ScrollBarWidth, 0f), (float)facilities.Count * RowHeight);
			Widgets.BeginScrollView(viewRect, ref facilitiesScroll, contentRect);
			if (facilities.Count == 0)
			{
				Widgets.Label(new Rect(0f, 0f, contentRect.width, RowHeight), "DreamsOutposts.None".Translate());
			}
			else
			{
				for (int i = 0; i < facilities.Count; i++)
				{
					DrawFacilityRow(new Rect(0f, (float)i * RowHeight, contentRect.width, RowHeight), facilities[i], i);
				}
			}
			Widgets.EndScrollView();
		}

		private static void DrawPawnRow(Rect rowRect, Pawn pawn, int index)
		{
			if (index % 2 == 1)
			{
				Widgets.DrawLightHighlight(rowRect);
			}
			Widgets.ThingIcon(new Rect(rowRect.x + 2f, rowRect.y + (rowRect.height - IconSize) * 0.5f, IconSize, IconSize), pawn);
			float labelX = rowRect.x + IconSize + 10f;
			float labelWidth = Mathf.Max(rowRect.xMax - ValueWidth - 6f - labelX, 0f);
			TextAnchor previousAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(labelX, rowRect.y, labelWidth, rowRect.height), pawn.LabelCap);
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(new Rect(rowRect.xMax - ValueWidth - 4f, rowRect.y, ValueWidth, rowRect.height), OutpostDefenseUtility.PawnDefense(pawn).ToString());
			Text.Anchor = previousAnchor;
			TooltipHandler.TipRegion(rowRect, new TipSignal(PawnTooltip(pawn), pawn.thingIDNumber));
		}

		private static void DrawFacilityRow(Rect rowRect, OutpostFacility facility, int index)
		{
			if (index % 2 == 1)
			{
				Widgets.DrawLightHighlight(rowRect);
			}
			float labelX = rowRect.x + 6f;
			float labelWidth = Mathf.Max(rowRect.xMax - ValueWidth - 6f - labelX, 0f);
			TextAnchor previousAnchor = Text.Anchor;
			Text.Anchor = TextAnchor.MiddleLeft;
			Widgets.Label(new Rect(labelX, rowRect.y, labelWidth, rowRect.height), facility?.def?.LabelCap ?? "DreamsOutposts.UnknownFacility".Translate());
			Text.Anchor = TextAnchor.MiddleRight;
			Widgets.Label(new Rect(rowRect.xMax - ValueWidth - 4f, rowRect.y, ValueWidth, rowRect.height), (facility?.def?.defense ?? 0f).ToString("0.#"));
			Text.Anchor = previousAnchor;
		}

		/// <summary>
		/// 单个 Pawn 的防卫来源。不可用的技能按原版技能界面的习惯显示成「-」。
		/// </summary>
		private static string PawnTooltip(Pawn pawn)
		{
			StringBuilder stringBuilder = new StringBuilder();
			AppendSkillLine(stringBuilder, SkillDefOf.Shooting, OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Shooting));
			AppendSkillLine(stringBuilder, SkillDefOf.Melee, OutpostDefenseUtility.AvailableSkillLevel(pawn, SkillDefOf.Melee));
			return stringBuilder.ToString().TrimEndNewlines();
		}

		private static void AppendSkillLine(StringBuilder stringBuilder, SkillDef skillDef, int level)
		{
			stringBuilder.AppendLine(skillDef.LabelCap + ": " + ((level < 0) ? (string)"DreamsOutposts.SkillDisabled".Translate() : level.ToString()));
		}
	}
}
