using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostTavern : OutpostManagePage, IUiShellPage
	{
		private const float Gap = 14f;
		private const float CardHeight = 174f;
		private const float PortraitSize = 92f;
		private const float ControlsHeight = 82f;

		public override bool IsVisible => AdventurerRecruitUtility.IsAvailable(outpost);
		public override string Label => "DreamsOutposts.Tavern.Title".Translate();
		public string NavSummary => "DreamsOutposts.Tavern.Nav".Translate(outpost?.adventurerRecruitment?.offers.Count ?? 0, AdventurerRecruitUtility.MaxOffers);
		public string HeadDescription => "DreamsOutposts.Tavern.Description".Translate();
		public string HeadHint => "DreamsOutposts.Tavern.Hint".Translate();

		public override void DoContents(Rect rect) => DrawBody(rect, rect.height);

		public float BodyHeight(float width, float availableHeight)
		{
			int count = Mathf.Max(outpost?.adventurerRecruitment?.offers.Count ?? 0, 1);
			return ControlsHeight + Gap + count * (CardHeight + Gap);
		}

		public void DrawBody(Rect rect, float availableHeight)
		{
			if (outpost?.adventurerRecruitment == null) return;
			DrawControls(new Rect(rect.x, rect.y, rect.width, ControlsHeight));
			float y = rect.y + ControlsHeight + Gap;
			List<AdventurerOffer> offers = outpost.adventurerRecruitment.offers;
			if (offers.Count == 0)
			{
				UiDraw.Box(new Rect(rect.x, y, rect.width, CardHeight), (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
				UiText.Draw(new Rect(rect.x + 16f, y, rect.width - 32f, CardHeight), "DreamsOutposts.Tavern.Empty".Translate(), UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleCenter);
				return;
			}
			for (int i = 0; i < offers.Count; i++)
			{
				DrawOffer(new Rect(rect.x, y, rect.width, CardHeight), offers[i]);
				y += CardHeight + Gap;
			}
		}

		private void DrawControls(Rect rect)
		{
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
			SkillDef preferred = outpost.adventurerRecruitment.preferredSkill;
			string value = preferred?.LabelCap.ToString() ?? "DreamsOutposts.Tavern.AnySkill".Translate().ToString();
			float buttonWidth = Mathf.Min(250f, rect.width * 0.38f);
			UiText.Draw(new Rect(rect.x + 14f, rect.y + 2f, 136f, 54f), "DreamsOutposts.Tavern.Preference".Translate(), UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true);
			if (UiWidgets.Button(new Rect(rect.x + 150f, rect.y + 10f, buttonWidth, 38f), value, UiButtonKind.Secondary, true, null, UiButtonSize.Normal)) OpenSkillMenu();
			int now = Find.TickManager.TicksGame;
			string timer = outpost.adventurerRecruitment.offers.Count >= AdventurerRecruitUtility.MaxOffers
				? "DreamsOutposts.Tavern.Full".Translate().ToString()
				: "DreamsOutposts.Tavern.Next".Translate(Mathf.Max(outpost.adventurerRecruitment.nextRecruitTick - now, 0).ToStringTicksToPeriod()).ToString();
			UiText.Draw(new Rect(rect.x + 164f + buttonWidth, rect.y + 2f, Mathf.Max(rect.width - 178f - buttonWidth, 30f), 54f), timer, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleRight);
			AdventurerRarityProbabilities odds = AdventurerRecruitUtility.ProbabilitiesFor(outpost);
			string oddsText = "DreamsOutposts.Tavern.Odds".Translate(
				AdventurerRecruitUtility.PopulationTendency(outpost).ToString("0.#"),
				odds.Common.ToStringPercent("F0"), odds.Excellent.ToStringPercent("F0"),
				odds.Elite.ToStringPercent("F0"), odds.Epic.ToStringPercent("F0"));
			UiText.Draw(new Rect(rect.x + 14f, rect.y + 56f, rect.width - 28f, 22f), oddsText, UiFont.Caption, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
		}

		private void OpenSkillMenu()
		{
			List<FloatMenuOption> options = new List<FloatMenuOption>
			{
				new FloatMenuOption("DreamsOutposts.Tavern.AnySkill".Translate(), () => outpost.adventurerRecruitment.preferredSkill = null)
			};
			foreach (SkillDef skill in DefDatabase<SkillDef>.AllDefsListForReading.OrderBy(s => s.LabelCap.ToString()))
			{
				SkillDef captured = skill;
				options.Add(new FloatMenuOption(skill.LabelCap, () => outpost.adventurerRecruitment.preferredSkill = captured));
			}
			Find.WindowStack.Add(new FloatMenu(options));
		}

		private void DrawOffer(Rect rect, AdventurerOffer offer)
		{
			Pawn pawn = offer?.pawn;
			if (pawn == null) return;
			AdventurerRarity rarity = AdventurerRecruitUtility.RarityFor(pawn);
			Color rarityColor = RarityColor(rarity);
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, rarityColor);
			Rect portrait = new Rect(rect.x + 16f, rect.y + 16f, PortraitSize, PortraitSize);
			Widgets.ThingIcon(portrait, pawn);
			float textX = portrait.xMax + 16f;
			float actionWidth = 104f;
			float textWidth = Mathf.Max(rect.xMax - 16f - actionWidth - 14f - textX, 100f);
			UiText.Draw(new Rect(textX, rect.y + 14f, textWidth, 28f), pawn.LabelCap, UiFont.Heading, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(textX, rect.y + 44f, textWidth, 24f), "DreamsOutposts.Tavern.Rarity".Translate(RarityLabel(rarity)), UiFont.Body, rarityColor, TextAnchor.MiddleLeft, true);
			List<SkillRecord> top = pawn.skills?.skills.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).Take(3).ToList() ?? new List<SkillRecord>();
			string skills = string.Join(" · ", top.Select(s => s.def.LabelCap + " " + s.Level + PassionText(s.passion)).ToArray());
			UiText.Draw(new Rect(textX, rect.y + 72f, textWidth, 42f), skills, UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, true, true);
			int left = Mathf.Max(offer.expireTick - Find.TickManager.TicksGame, 0);
			UiText.Draw(new Rect(textX, rect.y + 120f, textWidth, 24f), "DreamsOutposts.Tavern.Expires".Translate(left.ToStringTicksToPeriod()), UiFont.Caption, UiPalette.Ink3, TextAnchor.MiddleLeft);
			float bx = rect.xMax - 16f - actionWidth;
			if (UiWidgets.Button(new Rect(bx, rect.y + 15f, actionWidth, 34f), "DreamsOutposts.Tavern.Recruit".Translate(), UiButtonKind.Primary, true, null, UiButtonSize.Small))
			{
				AdventurerRecruitUtility.Recruit(outpost, offer);
			}
			if (UiWidgets.Button(new Rect(bx, rect.y + 57f, actionWidth, 34f), "DreamsOutposts.Tavern.Dismiss".Translate(), UiButtonKind.Danger, true, null, UiButtonSize.Small))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("DreamsOutposts.Tavern.DismissConfirm".Translate(pawn.LabelShortCap), () => AdventurerRecruitUtility.RemoveOffer(outpost, offer), destructive: true));
			}
			Widgets.InfoCardButton(bx + (actionWidth - 24f) * 0.5f, rect.y + 108f, pawn);
		}

		private static string PassionText(Passion passion) => passion == Passion.Major ? " ♥♥" : passion == Passion.Minor ? " ♥" : string.Empty;
		private static string RarityLabel(AdventurerRarity rarity) => ("DreamsOutposts.Tavern.Rarity." + rarity).Translate();
		private static Color RarityColor(AdventurerRarity rarity)
		{
			switch (rarity)
			{
				case AdventurerRarity.Excellent: return UiPalette.Good;
				case AdventurerRarity.Elite: return UiPalette.Info;
				case AdventurerRarity.Epic: return UiPalette.Purple;
				default: return UiPalette.Ink2;
			}
		}
	}
}
