using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostSchool : OutpostManagePage, IUiShellPage
	{
		private const float OverviewHeight = 82f;
		private const float SectionGap = 18f;
		private const float StudentRowHeight = 108f;
		private const float StudentRowGap = 8f;
		private const float PortraitSize = 52f;

		private const float ModernOverviewHeight = 112f;
		private const float ModernSectionGap = 16f;
		private const float ModernCardGap = 14f;
		private const float ModernStudentCardHeight = 154f;
		private const float ModernStudentPortrait = 44f;
		private const float ModernTwoColumnMinWidth = UiMetrics.StackBreakpoint;

		public string NavSummary
		{
			get
			{
				return "DreamsOutposts.School.NavSummary".Translate(
					OutpostEducationUtility.StudentCount(outpost),
					OutpostEducationUtility.ConfiguredQuality(outpost).ToStringPercent("F0")).ToString();
			}
		}

		public string HeadDescription => null;
		public string HeadHint => null;

		public float BodyHeight(float width, float availableHeight)
		{
			List<Pawn> students = Students();
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				float rows = Mathf.Max(students.Count, 1) * StudentRowHeight
					+ Mathf.Max(students.Count - 1, 0) * StudentRowGap;
				return OverviewHeight + SectionGap + 30f + rows;
			}

			int columns = width >= ModernTwoColumnMinWidth ? 2 : 1;
			int rowCount = Mathf.Max(Mathf.CeilToInt((float)students.Count / columns), 1);
			float cardsHeight = rowCount * ModernStudentCardHeight + Mathf.Max(rowCount - 1, 0) * ModernCardGap;
			return ModernOverviewHeight + ModernSectionGap + 30f + ModernSectionGap + cardsHeight;
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			if (DreamsOutpostsMod.IsUiStyle(OutpostUiStyle.Vanilla))
			{
				DrawVanillaBody(rect);
				return;
			}
			DrawModernBody(rect);
		}

		private void DrawVanillaBody(Rect rect)
		{
			List<Pawn> students = Students();
			DrawVanillaOverview(new Rect(rect.x, rect.y, rect.width, OverviewHeight));

			float y = rect.y + OverviewHeight + SectionGap;
			UiText.Draw(new Rect(rect.x, y, rect.width, 28f),
				"DreamsOutposts.School.StudentGrowth".Translate(),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			y += 30f;

			if (students.Count == 0)
			{
				UiText.Draw(new Rect(rect.x, y, rect.width, StudentRowHeight),
					"DreamsOutposts.School.NoStudents".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}

			for (int i = 0; i < students.Count; i++)
			{
				DrawVanillaStudent(new Rect(rect.x, y, rect.width, StudentRowHeight), students[i]);
				y += StudentRowHeight + StudentRowGap;
			}
		}

		private void DrawModernBody(Rect rect)
		{
			List<Pawn> students = Students();
			DrawModernOverview(new Rect(rect.x, rect.y, rect.width, ModernOverviewHeight));

			float y = rect.y + ModernOverviewHeight + ModernSectionGap;
			UiText.Draw(new Rect(rect.x, y, rect.width, 30f),
				"DreamsOutposts.School.StudentGrowth".Translate(),
				UiFont.Heading, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			y += 30f + ModernSectionGap;

			if (students.Count == 0)
			{
				Rect empty = new Rect(rect.x, y, rect.width, ModernStudentCardHeight);
				UiDraw.Box(empty, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
				UiText.Draw(empty.ContractedBy(16f), "DreamsOutposts.School.NoStudents".Translate(),
					UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}

			bool twoColumns = rect.width >= ModernTwoColumnMinWidth;
			float cardWidth = twoColumns
				? (rect.width - ModernCardGap) * 0.5f
				: rect.width;

			for (int i = 0; i < students.Count; i++)
			{
				int column = twoColumns ? i % 2 : 0;
				int row = twoColumns ? i / 2 : i;
				Rect card = new Rect(
					rect.x + column * (cardWidth + ModernCardGap),
					y + row * (ModernStudentCardHeight + ModernCardGap),
					cardWidth,
					ModernStudentCardHeight);
				DrawModernStudent(card, students[i]);
			}
		}

		private void DrawVanillaOverview(Rect rect)
		{
			float quality = OutpostEducationUtility.ConfiguredQuality(outpost);
			AcceptanceReport report;
			bool operational = OutpostEducationUtility.IsSchoolOperational(outpost, out report);

			float half = rect.width * 0.5f;
			UiText.Draw(new Rect(rect.x, rect.y, half, 28f),
				"DreamsOutposts.School.EducationQuality".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
			UiText.Draw(new Rect(rect.x + half, rect.y, half, 28f),
				quality.ToStringPercent("F0"),
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleRight, true);

			float secondY = rect.y + 36f;
			UiText.Draw(new Rect(rect.x, secondY, half, 28f),
				"DreamsOutposts.School.CaretakerStatus".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
			UiText.Draw(new Rect(rect.x + half, secondY, half, 28f),
				SchoolStatusText(report, operational),
				UiFont.Body, operational ? UiPalette.Good : UiPalette.Bad,
				TextAnchor.MiddleRight, true, false, true);

			Widgets.DrawLineHorizontal(rect.x, rect.yMax - 1f, rect.width, Widgets.SeparatorLineColor);
		}

		private void DrawModernOverview(Rect rect)
		{
			float quality = OutpostEducationUtility.ConfiguredQuality(outpost);
			AcceptanceReport report;
			bool operational = OutpostEducationUtility.IsSchoolOperational(outpost, out report);

			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			UiDraw.InstrumentFrame(rect.ContractedBy(1f), UiPalette.WithAlpha(UiPalette.Accent, 0.42f), 14f);

			Rect inner = rect.ContractedBy(18f);
			float leftWidth = Mathf.Min(inner.width * 0.44f, 360f);
			Rect left = new Rect(inner.x, inner.y, leftWidth, inner.height);
			Rect right = new Rect(left.xMax + 24f, inner.y, Mathf.Max(inner.xMax - left.xMax - 24f, 60f), inner.height);

			UiText.Draw(new Rect(left.x, left.y, left.width, 22f),
				"DreamsOutposts.School.EducationQuality".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false);
			UiText.Draw(new Rect(left.x, left.y + 24f, left.width, 38f),
				quality.ToStringPercent("F0"),
				UiFont.Number, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			UiDraw.Bar(new Rect(left.x, left.y + 70f, left.width, UiMetrics.BarHeight),
				quality / OutpostEducationUtility.MaxQuality, UiPalette.Accent, UiPalette.Track);

			UiText.Draw(new Rect(right.x, right.y, right.width, 22f),
				"DreamsOutposts.School.CaretakerStatus".Translate(),
				UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false);
			UiText.Draw(new Rect(right.x, right.y + 26f, right.width, 30f),
				SchoolStatusText(report, operational),
				UiFont.Body, operational ? UiPalette.Good : UiPalette.Bad,
				TextAnchor.MiddleLeft, true, false, true);

			string studentCount = "DreamsOutposts.School.NavSummary".Translate(
				OutpostEducationUtility.StudentCount(outpost), quality.ToStringPercent("F0")).ToString();
			UiText.Draw(new Rect(right.x, right.y + 62f, right.width, 24f),
				studentCount, UiFont.Body, UiPalette.Ink3, TextAnchor.MiddleLeft, false, false, true);
		}

		private static string SchoolStatusText(AcceptanceReport report, bool operational)
		{
			if (operational)
			{
				return "DreamsOutposts.School.Running".Translate().ToString();
			}
			string reason = report.Reason;
			return "DreamsOutposts.School.NotRunning".Translate(
				string.IsNullOrEmpty(reason)
					? "DreamsOutposts.School.NoCaretaker".Translate().ToString()
					: reason).ToString();
		}

		private void DrawVanillaStudent(Rect row, Pawn pawn)
		{
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
			DrawVanillaStudentText(row, pawn, textX, textWidth);
		}

		private void DrawVanillaStudentText(Rect row, Pawn pawn, float textX, float textWidth)
		{
			Rect nameRect = new Rect(textX, row.y + 7f, textWidth, 22f);
			UiText.Draw(nameRect, pawn.LabelShortCap.ToString(), UiFont.Body, UiPalette.Ink,
				TextAnchor.MiddleLeft, true, false, true);

			string details = GrowthMomentText(pawn);
			string speedText = GrowthSpeedText(pawn);

			float speedWidth = Mathf.Min(170f, textWidth * 0.42f);
			Rect detailRect = new Rect(textX, row.y + 29f, textWidth - speedWidth - 8f, 20f);
			Rect speedRect = new Rect(textX + textWidth - speedWidth, row.y + 29f, speedWidth, 20f);
			UiText.Draw(detailRect, details, UiFont.Caption, UiPalette.Ink2,
				TextAnchor.MiddleLeft, false, false, true);
			UiText.Draw(speedRect, speedText, UiFont.Caption,
				pawn.ageTracker.canGainGrowthPoints ? UiPalette.Ink2 : UiPalette.Bad,
				TextAnchor.MiddleRight, false, false, true);

			GetGrowthDisplay(pawn, out string tierText, out float fraction, out string pointText);
			Rect tierRect = new Rect(textX, row.y + 54f, textWidth, 20f);
			UiText.Draw(tierRect, tierText, UiFont.Caption, UiPalette.Ink,
				TextAnchor.MiddleLeft, true, false, true);

			Rect barRect = new Rect(textX, row.y + 78f, textWidth, 18f);
			Widgets.FillableBar(barRect, fraction);
			UiText.Draw(barRect, pointText,
				UiFont.Body, UiPalette.Ink, TextAnchor.MiddleCenter, false, false, true);
		}

		private void DrawModernStudent(Rect card, Pawn pawn)
		{
			bool hovered = Mouse.IsOver(card);
			UiDraw.Box(card, (int)UiMetrics.RadiusSm, hovered ? UiPalette.Hover : UiPalette.Card, hovered ? UiPalette.LineStrong : UiPalette.Line);

			Rect inner = card.ContractedBy(14f);
			Rect portrait = new Rect(inner.x, inner.y + 2f, ModernStudentPortrait, ModernStudentPortrait);
			UiDraw.PawnPortrait(portrait, pawn);

			float textX = portrait.xMax + 12f;
			float textWidth = Mathf.Max(inner.xMax - textX, 80f);
			UiText.Draw(new Rect(textX, inner.y, textWidth, 24f),
				pawn.LabelShortCap.ToString(), UiFont.Body, UiPalette.Ink,
				TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(textX, inner.y + 25f, textWidth, 20f),
				GrowthMomentText(pawn), UiFont.Body, UiPalette.Ink2,
				TextAnchor.MiddleLeft, false, false, true);

			float metaY = inner.y + 54f;
			UiDraw.Divider(new Rect(inner.x, metaY, inner.width, 1f), UiPalette.Line);

			GetGrowthDisplay(pawn, out string tierText, out float fraction, out string pointText);
			float speedWidth = Mathf.Min(150f, inner.width * 0.42f);
			UiText.Draw(new Rect(inner.x, metaY + 10f, inner.width - speedWidth - 8f, 20f),
				tierText, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(inner.xMax - speedWidth, metaY + 10f, speedWidth, 20f),
				GrowthSpeedText(pawn), UiFont.Body,
				pawn.ageTracker.canGainGrowthPoints ? UiPalette.Ink2 : UiPalette.Bad,
				TextAnchor.MiddleRight, false, false, true);

			float barY = metaY + 42f;
			UiDraw.Bar(new Rect(inner.x, barY, inner.width, UiMetrics.BarHeight),
				fraction, UiPalette.Accent, UiPalette.Track);
			UiText.Draw(new Rect(inner.x, barY + 8f, inner.width, 20f),
				pointText, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);

			if (hovered && Widgets.ButtonInvisible(card))
			{
				Find.WindowStack.Add(new Dialog_InfoCard(pawn));
			}
		}

		private string GrowthMomentText(Pawn pawn)
		{
			int nextGrowthAge = NextGrowthMomentAge(pawn);
			string ageText = pawn.ageTracker.AgeBiologicalYearsFloat.ToStringApproxAge();
			return nextGrowthAge > 0
				? "DreamsOutposts.School.AgeAndNextGrowth".Translate(ageText, nextGrowthAge).ToString()
				: "DreamsOutposts.School.AgeOnly".Translate(ageText).ToString();
		}

		private string GrowthSpeedText(Pawn pawn)
		{
			if (!pawn.ageTracker.canGainGrowthPoints)
			{
				return "DreamsOutposts.School.GrowthPaused".Translate().ToString();
			}
			float growthPerDay = OutpostEducationUtility.GrowthPointsPerDay(pawn, outpost);
			return "DreamsOutposts.School.GrowthPerDay".Translate(growthPerDay.ToString("0.##")).ToString();
		}

		private static void GetGrowthDisplay(Pawn pawn, out string tierText, out float fraction, out string pointText)
		{
			int tier = pawn.ageTracker.GrowthTier;
			float maxRequirement = GrowthUtility.GrowthTiers[GrowthUtility.GrowthTiers.Length - 1].pointsRequirement;
			float target = pawn.ageTracker.AtMaxGrowthTier
				? maxRequirement
				: GrowthUtility.GrowthTiers[tier + 1].pointsRequirement;
			float points = Mathf.Max(0f, pawn.ageTracker.growthPoints);

			tierText = "DreamsOutposts.School.GrowthTier".Translate(tier).ToString();
			fraction = pawn.ageTracker.AtMaxGrowthTier ? 1f : pawn.ageTracker.PercentToNextGrowthTier;
			pointText = Mathf.FloorToInt(points) + " / " + Mathf.FloorToInt(target);
		}

		private List<Pawn> Students()
		{
			List<Pawn> result = new List<Pawn>();
			if (outpost?.pawns == null)
			{
				return result;
			}

			List<Pawn> pawns = outpost.pawns.InnerListForReading;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn pawn = pawns[i];
				if (pawn != null && !pawn.Dead && pawn.IsColonist && pawn.DevelopmentalStage.Child())
				{
					result.Add(pawn);
				}
			}
			result.Sort((a, b) => string.Compare(a.LabelShortCap, b.LabelShortCap, StringComparison.OrdinalIgnoreCase));
			return result;
		}

		private static int NextGrowthMomentAge(Pawn pawn)
		{
			if (pawn?.ageTracker == null)
			{
				return -1;
			}
			float age = pawn.ageTracker.AgeBiologicalYearsFloat;
			for (int i = 0; i < GrowthUtility.GrowthMomentAges.Length; i++)
			{
				if (GrowthUtility.GrowthMomentAges[i] > age)
				{
					return GrowthUtility.GrowthMomentAges[i];
				}
			}
			return -1;
		}
	}
}
