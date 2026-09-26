using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostIndoctrination : OutpostManagePage, IUiShellPage
	{
		private const float OverviewHeight = 88f;
		private const float SectionGap = 18f;
		private const float TargetRowHeight = 102f;
		private const float TargetRowGap = 8f;
		private const float PortraitSize = 52f;

		private const float ModernOverviewHeight = 116f;
		private const float ModernCardHeight = 136f;
		private const float ModernCardGap = 14f;
		private const float ModernPortraitSize = 44f;

		public override bool IsVisible =>
			ModsConfig.IdeologyActive
			&& Find.IdeoManager != null
			&& !Find.IdeoManager.classicMode
			&& HasComp();

		public string NavSummary
		{
			get
			{
				OutpostFacilityComp_Indoctrination comp = TryGetComp(out OutpostFacility facility);
				if (comp == null)
				{
					return null;
				}
				List<OutpostIndoctrinationState> states = comp.StatesForUi(outpost);
				string status = IsRunning(comp, facility, states)
					? "DreamsOutposts.Indoctrination.Panel.Running".Translate().ToString()
					: "DreamsOutposts.Indoctrination.Panel.Paused".Translate().ToString();
				return "DreamsOutposts.Indoctrination.Panel.NavSummary".Translate(states.Count, status).ToString();
			}
		}

		public string HeadDescription => null;
		public string HeadHint => null;

		public float BodyHeight(float width, float availableHeight)
		{
			OutpostFacilityComp_Indoctrination comp;
			OutpostFacility facility;
			List<OutpostIndoctrinationState> states = States(out comp, out facility);
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				float rows = Mathf.Max(states.Count, 1) * TargetRowHeight
					+ Mathf.Max(states.Count - 1, 0) * TargetRowGap;
				return OverviewHeight + SectionGap + 30f + rows;
			}

			bool twoColumns = width >= UiMetrics.StackBreakpoint;
			int columns = twoColumns ? 2 : 1;
			int rowCount = Mathf.Max(Mathf.CeilToInt((float)states.Count / columns), 1);
			float cardsHeight = rowCount * ModernCardHeight + Mathf.Max(rowCount - 1, 0) * ModernCardGap;
			return ModernOverviewHeight + SectionGap + 30f + SectionGap + cardsHeight;
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			OutpostFacilityComp_Indoctrination comp = TryGetComp(out OutpostFacility facility);
			if (comp == null)
			{
				return;
			}

			List<OutpostIndoctrinationState> states = comp.StatesForUi(outpost);
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				DrawVanillaBody(rect, comp, facility, states);
			}
			else
			{
				DrawModernBody(rect, comp, facility, states);
			}
		}

		private void DrawVanillaBody(Rect rect, OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states)
		{
			DrawVanillaOverview(new Rect(rect.x, rect.y, rect.width, OverviewHeight), comp, facility, states);

			float y = rect.y + OverviewHeight + SectionGap;
			UiText.Draw(new Rect(rect.x, y, rect.width, 28f),
				"DreamsOutposts.Indoctrination.Panel.ProgressHeading".Translate(),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			y += 30f;

			if (states.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, y, rect.width, TargetRowHeight),
					"DreamsOutposts.Indoctrination.Panel.NoTargets".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}

			bool running = IsRunning(comp, facility, states);
			for (int i = 0; i < states.Count; i++)
			{
				DrawVanillaTarget(
					new Rect(rect.x, y, rect.width, TargetRowHeight),
					comp,
					states[i],
					running);
				y += TargetRowHeight + TargetRowGap;
			}
		}

		private void DrawModernBody(Rect rect, OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states)
		{
			DrawModernOverview(new Rect(rect.x, rect.y, rect.width, ModernOverviewHeight), comp, facility, states);

			float y = rect.y + ModernOverviewHeight + SectionGap;
			UiText.Draw(new Rect(rect.x, y, rect.width, 30f),
				"DreamsOutposts.Indoctrination.Panel.ProgressHeading".Translate(),
				UiFont.Heading, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			y += 30f + SectionGap;

			if (states.Count == 0)
			{
				Rect empty = new Rect(rect.x, y, rect.width, ModernCardHeight);
				UiDraw.Box(empty, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
				UiText.Draw(empty.ContractedBy(16f),
					"DreamsOutposts.Indoctrination.Panel.NoTargets".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}

			bool twoColumns = rect.width >= UiMetrics.StackBreakpoint;
			float cardWidth = twoColumns ? (rect.width - ModernCardGap) * 0.5f : rect.width;
			bool running = IsRunning(comp, facility, states);

			for (int i = 0; i < states.Count; i++)
			{
				int column = twoColumns ? i % 2 : 0;
				int row = twoColumns ? i / 2 : i;
				Rect card = new Rect(
					rect.x + column * (cardWidth + ModernCardGap),
					y + row * (ModernCardHeight + ModernCardGap),
					cardWidth,
					ModernCardHeight);
				DrawModernTarget(card, comp, states[i], running);
			}
		}

		private void DrawVanillaOverview(Rect rect, OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states)
		{
			Ideo primary = comp.PrimaryIdeo;
			Pawn guide = comp.CurrentGuide(outpost);
			string status = StatusText(comp, facility, states, guide);

			float half = rect.width * 0.5f;
			UiText.Draw(new Rect(rect.x, rect.y, half, 28f),
				"DreamsOutposts.Indoctrination.TargetIdeo".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
			UiText.Draw(new Rect(rect.x + half, rect.y, half, 28f),
				primary?.name ?? "DreamsOutposts.None".Translate().ToString(),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleRight, true, false, true);

			float secondY = rect.y + 36f;
			UiText.Draw(new Rect(rect.x, secondY, half, 28f),
				"DreamsOutposts.Indoctrination.Panel.GuideStatus".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
			UiText.Draw(new Rect(rect.x + half, secondY, half, 28f),
				status,
				UiFont.Body,
				IsRunning(comp, facility, states) ? UiPalette.Good : UiPalette.Bad,
				TextAnchor.MiddleRight, true, false, true);

			Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width, Widgets.SeparatorLineColor);
		}

		private void DrawModernOverview(Rect rect, OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states)
		{
			Ideo primary = comp.PrimaryIdeo;
			Pawn guide = comp.CurrentGuide(outpost);
			bool running = IsRunning(comp, facility, states);

			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			UiDraw.InstrumentFrame(rect.ContractedBy(1f), UiPalette.WithAlpha(UiPalette.Accent, 0.42f), 14f);

			Rect inner = rect.ContractedBy(18f);
			float leftWidth = Mathf.Min(inner.width * 0.44f, 360f);
			Rect left = new Rect(inner.x, inner.y, leftWidth, inner.height);
			Rect right = new Rect(left.xMax + 24f, inner.y, Mathf.Max(inner.xMax - left.xMax - 24f, 60f), inner.height);

			UiText.Draw(new Rect(left.x, left.y, left.width, 22f),
				"DreamsOutposts.Indoctrination.TargetIdeo".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false);
			UiText.Draw(new Rect(left.x, left.y + 26f, left.width, 34f),
				primary?.name ?? "DreamsOutposts.None".Translate().ToString(),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(left.x, left.y + 68f, left.width, 22f),
				"DreamsOutposts.Indoctrination.Panel.TargetCount".Translate(states.Count),
				UiFont.Body, UiPalette.Ink3, TextAnchor.MiddleLeft, false);

			UiText.Draw(new Rect(right.x, right.y, right.width, 22f),
				"DreamsOutposts.Indoctrination.Panel.GuideStatus".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false);
			UiText.Draw(new Rect(right.x, right.y + 26f, right.width, 30f),
				StatusText(comp, facility, states, guide),
				UiFont.Body, running ? UiPalette.Good : UiPalette.Bad,
				TextAnchor.MiddleLeft, true, false, true);

			if (guide != null)
			{
				UiText.Draw(new Rect(right.x, right.y + 64f, right.width, 22f),
					"DreamsOutposts.Indoctrination.Panel.ConversionPower".Translate(
						guide.GetStatValue(StatDefOf.ConversionPower).ToStringPercent()),
					UiFont.Body, UiPalette.Ink3, TextAnchor.MiddleLeft, false, false, true);
			}
		}

		private void DrawVanillaTarget(Rect row, OutpostFacilityComp_Indoctrination comp, OutpostIndoctrinationState state, bool running)
		{
			Pawn pawn = state.pawn;
			Widgets.DrawHighlightIfMouseover(row);
			Widgets.DrawLineHorizontal(row.x, row.yMax - 1f, row.width, Widgets.SeparatorLineColor);

			float pad = 4f;
			Rect portrait = new Rect(row.x + pad, row.y + (row.height - PortraitSize) * 0.5f, PortraitSize, PortraitSize);
			UiDraw.PawnPortrait(portrait, pawn);

			const float infoSize = Widgets.InfoCardButtonSize;
			float infoX = row.xMax - pad - infoSize;
			Widgets.InfoCardButton(infoX, row.y + (row.height - infoSize) * 0.5f, pawn);

			float textX = portrait.xMax + 12f;
			float textWidth = Mathf.Max(infoX - textX - 10f, 80f);
			UiText.Draw(new Rect(textX, row.y + 5f, textWidth, 22f),
				pawn.LabelShortCap.ToString(), UiFont.Body, UiPalette.Ink,
				TextAnchor.MiddleLeft, true, false, true);

			string ideology = "DreamsOutposts.Indoctrination.Panel.CurrentIdeo".Translate(pawn.Ideo?.name ?? "-").ToString();
			string certainty = "DreamsOutposts.Indoctrination.Certainty".Translate(pawn.ideo?.Certainty.ToStringPercent() ?? "-").ToString();
			float certaintyWidth = Mathf.Min(170f, textWidth * 0.40f);
			UiText.Draw(new Rect(textX, row.y + 29f, textWidth - certaintyWidth - 8f, 20f),
				ideology, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
			UiText.Draw(new Rect(textX + textWidth - certaintyWidth, row.y + 29f, certaintyWidth, 20f),
				certainty, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);

			DrawVanillaProgress(new Rect(textX, row.y + 62f, textWidth, 18f), comp, state, running);
		}

		private void DrawModernTarget(Rect card, OutpostFacilityComp_Indoctrination comp, OutpostIndoctrinationState state, bool running)
		{
			Pawn pawn = state.pawn;
			bool hovered = Mouse.IsOver(card);
			UiDraw.Box(card, (int)UiMetrics.RadiusSm, hovered ? UiPalette.Hover : UiPalette.Card, hovered ? UiPalette.LineStrong : UiPalette.Line);

			Rect inner = card.ContractedBy(14f);
			Rect portrait = new Rect(inner.x, inner.y + 2f, ModernPortraitSize, ModernPortraitSize);
			UiDraw.PawnPortrait(portrait, pawn);

			float textX = portrait.xMax + 12f;
			float textWidth = Mathf.Max(inner.xMax - textX, 80f);
			UiText.Draw(new Rect(textX, inner.y, textWidth, 24f),
				pawn.LabelShortCap.ToString(), UiFont.Body, UiPalette.Ink,
				TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(textX, inner.y + 25f, textWidth, 20f),
				"DreamsOutposts.Indoctrination.Panel.CurrentIdeo".Translate(pawn.Ideo?.name ?? "-"),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);

			float metaY = inner.y + 54f;
			UiDraw.Divider(new Rect(inner.x, metaY, inner.width, 1f), UiPalette.Line);
			UiText.Draw(new Rect(inner.x, metaY + 10f, inner.width, 22f),
				"DreamsOutposts.Indoctrination.Certainty".Translate(pawn.ideo?.Certainty.ToStringPercent() ?? "-"),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);

			comp.ProgressFor(state, out float progress, out int remaining);
			float barY = metaY + 42f;
			UiDraw.Bar(new Rect(inner.x, barY, inner.width, UiMetrics.BarHeight),
				progress, running ? UiPalette.Accent : UiPalette.Ink3, UiPalette.Track);
			UiText.Draw(new Rect(inner.x, barY + 8f, inner.width, 20f),
				ProgressText(progress, remaining, running),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);

			if (hovered && Widgets.ButtonInvisible(card))
			{
				Find.WindowStack.Add(new Dialog_InfoCard(pawn));
			}
		}

		private void DrawVanillaProgress(Rect rect, OutpostFacilityComp_Indoctrination comp, OutpostIndoctrinationState state, bool running)
		{
			comp.ProgressFor(state, out float progress, out int remaining);
			Widgets.FillableBar(rect, progress);
			UiText.Draw(rect,
				ProgressText(progress, remaining, running),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleCenter, false, false, true);
		}

		private string ProgressText(float progress, int remaining, bool running)
		{
			int percent = Mathf.RoundToInt(progress * 100f);
			return running
				? "DreamsOutposts.Indoctrination.Panel.NextAttempt".Translate(
					percent,
					remaining.ToStringTicksToPeriod()).ToString()
				: "DreamsOutposts.Indoctrination.Panel.ProgressPaused".Translate(percent).ToString();
		}

		private string StatusText(OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states, Pawn guide)
		{
			AcceptanceReport report;
			if (facility == null)
			{
				report = false;
			}
			else
			{
				report = facility.CanOperate(outpost);
			}
			if (!report.Accepted)
			{
				return report.Reason;
			}
			if (guide == null)
			{
				return "DreamsOutposts.Indoctrination.NoGuide".Translate(comp.MinSocial).ToString();
			}
			if (states.Count == 0)
			{
				return "DreamsOutposts.Indoctrination.Panel.NoTargets".Translate().ToString();
			}
			return "DreamsOutposts.Indoctrination.Panel.GuideRunning".Translate(guide.LabelShortCap).ToString();
		}

		private bool IsRunning(OutpostFacilityComp_Indoctrination comp, OutpostFacility facility, List<OutpostIndoctrinationState> states)
		{
			return comp != null
				&& facility != null
				&& facility.CanOperate(outpost).Accepted
				&& comp.CurrentGuide(outpost) != null
				&& states.Count > 0;
		}

		private List<OutpostIndoctrinationState> States(out OutpostFacilityComp_Indoctrination comp, out OutpostFacility facility)
		{
			comp = TryGetComp(out facility);
			List<OutpostIndoctrinationState> states = comp?.StatesForUi(outpost) ?? new List<OutpostIndoctrinationState>();
			states.Sort((a, b) => string.Compare(
				a?.pawn?.LabelShortCap.ToString(),
				b?.pawn?.LabelShortCap.ToString(),
				StringComparison.OrdinalIgnoreCase));
			return states;
		}

		private OutpostFacilityComp_Indoctrination TryGetComp(out OutpostFacility facility)
		{
			facility = null;
			if (outpost == null)
			{
				return null;
			}
			foreach (OutpostFacility candidate in outpost.Facilities)
			{
				OutpostFacilityComp_Indoctrination comp = candidate?.GetComp<OutpostFacilityComp_Indoctrination>();
				if (comp != null)
				{
					facility = candidate;
					return comp;
				}
			}
			return null;
		}

		private bool HasComp()
		{
			OutpostFacility facility;
			return TryGetComp(out facility) != null;
		}
	}
}
