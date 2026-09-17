using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public class Page_OutpostTavern : OutpostManagePage, IUiShellPage
	{
		private const float Gap = 14f;
		private const float CardHeight = 174f;
		private const float PortraitSize = 80f;
		private const float ControlsHeight = 82f;
		/// <summary>候选人卡右侧「招募 / 驱离」两个底图按钮的高度（宽度由底图长宽比反推）。</summary>
		private const float TavernActionButtonHeight = 34f;
		private const float ActionLabelSourceX = 420f;
		private const float ActionLabelSourceWidth = 300f;

		public override bool IsVisible => AdventurerRecruitUtility.IsAvailable(outpost);
		public override string Label => "DreamsOutposts.Tavern.Title".Translate();
		public string NavSummary => "DreamsOutposts.Tavern.Nav".Translate(outpost?.adventurerRecruitment?.offers.Count ?? 0, AdventurerRecruitUtility.MaxOffers);
		public string HeadDescription => null;
		public string HeadHint => null;

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
				: ("DreamsOutposts.Tavern.Next".Translate(Mathf.Max(outpost.adventurerRecruitment.nextRecruitTick - now, 0).ToStringTicksToPeriod())
					+ " · " + "DreamsOutposts.Tavern.Chance".Translate(AdventurerRecruitUtility.RecruitChance(outpost).ToStringPercent("F0"))).ToString();
			Rect timerRect = new Rect(rect.x + 164f + buttonWidth, rect.y + 2f, Mathf.Max(rect.width - 178f - buttonWidth, 30f), 54f);
			UiText.Draw(timerRect, timer, UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleRight, false, false, true);
			// 这行只放百分比，总和与除数交给悬停提示，免得窄窗口下关键数字被省略号截掉。
			UiWidgets.Tip(timerRect, "DreamsOutposts.Tavern.ChanceTip".Translate(
				AdventurerRecruitUtility.SocialSkillTotal(outpost), AdventurerRecruitUtility.RecruitChanceDivisor.ToString("0")),
				GenText.StableStringHash("tavern-chance"));
			DrawOdds(new Rect(rect.x + 14f, rect.y + 56f, rect.width - 28f, 22f));
		}

		/// <summary>
		/// 概率行：人口倾向 + 四个等级的概率。等级段用富文本 <color> 上色后拼成一条字符串，一次画完，
		/// 所以段与段之间的距离就是翻译里写的空格，不会因为分段绘制而多出空隙。
		/// </summary>
		private void DrawOdds(Rect rect)
		{
			AdventurerRarityProbabilities odds = AdventurerRecruitUtility.ProbabilitiesFor(outpost);
			StringBuilder builder = new StringBuilder();
			builder.Append("DreamsOutposts.Tavern.Odds.Population".Translate(
				AdventurerRecruitUtility.PopulationTendency(outpost).ToString("0.#")).ToString());
			AppendOddsEntry(builder, AdventurerRarity.Common, odds.Common);
			AppendOddsEntry(builder, AdventurerRarity.Excellent, odds.Excellent);
			AppendOddsEntry(builder, AdventurerRarity.Elite, odds.Elite);
			AppendOddsEntry(builder, AdventurerRarity.Epic, odds.Epic);
			UiText.Draw(rect, builder.ToString(), UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft, false, false, true);
		}

		private static void AppendOddsEntry(StringBuilder builder, AdventurerRarity rarity, float probability)
		{
			builder.Append(" · ");
			builder.Append(OddsEntry(rarity, probability).Colorize(OddsColor(rarity)));
		}

		private static string OddsEntry(AdventurerRarity rarity, float probability)
		{
			return "DreamsOutposts.Tavern.Odds.Entry".Translate(
				AdventurerRecruitUtility.RarityLabel(rarity),
				probability.ToStringPercent("F0")).ToString();
		}

		/// <summary>概率行的等级配色：普通是纯白正文色，之后是绿 / 蓝 / 紫。</summary>
		private static Color OddsColor(AdventurerRarity rarity)
		{
			switch (rarity)
			{
				case AdventurerRarity.Excellent: return UiPalette.Good;
				case AdventurerRarity.Elite: return UiPalette.Info;
				case AdventurerRarity.Epic: return UiPalette.Purple;
				default: return UiPalette.Ink;
			}
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

		/// <summary>
		/// 一张候选人卡片。整张卡片都可以点开原版信息卡，只有「招募」「驱离」两个按钮占的区域例外，
		/// 这样点按钮和点卡片不会互相抢事件。
		/// </summary>
		private void DrawOffer(Rect rect, AdventurerOffer offer)
		{
			Pawn pawn = offer?.pawn;
			if (pawn == null) return;
			AdventurerRarity rarity = AdventurerRecruitUtility.RarityFor(pawn);
			// 传说个体只换显示：边框是沿轮廓流动的炫彩描边，评级文字仍是金色。
			bool legendary = AdventurerRecruitUtility.IsLegendary(pawn);
			Color rarityColor = legendary ? UiPalette.Legendary : RarityColor(rarity);
			Color fill = Mouse.IsOver(rect) ? UiPalette.Hover : UiPalette.Card;
			if (legendary)
			{
				UiDraw.RainbowBox(rect, (int)UiMetrics.RadiusSm, fill);
			}
			else
			{
				UiDraw.Box(rect, (int)UiMetrics.RadiusSm, fill, rarityColor);
			}
			Rect portrait = new Rect(rect.x + 16f, rect.y + 16f, PortraitSize, PortraitSize);
			UiDraw.PawnPortrait(portrait, pawn);
			float textX = portrait.xMax + 16f;
			// 字号档位跟设施卡的「详情」按钮保持一致（都是 UiButtonSize.Normal → UiFont.Body），
			// 底图里 300px 的文字槽位比「详情」那颗还宽，所以放大后同样放得下。
			string recruitLabel = "DreamsOutposts.Tavern.Recruit".Translate();
			string driveAwayLabel = "DreamsOutposts.Tavern.Dismiss".Translate();
			Texture2D recruitTexture = UiTex.PositiveButtonTexture();
			Texture2D driveAwayTexture = UiTex.NegativeButtonTexture();
			float recruitWidth = UiWidgets.TexturedButtonWidth(TavernActionButtonHeight, recruitTexture, recruitLabel, UiButtonSize.Normal);
			float driveAwayWidth = UiWidgets.TexturedButtonWidth(TavernActionButtonHeight, driveAwayTexture, driveAwayLabel, UiButtonSize.Normal);
			float actionWidth = Mathf.Max(recruitWidth, driveAwayWidth);
			float textWidth = Mathf.Max(rect.xMax - 16f - actionWidth - 14f - textX, 100f);
			UiText.Draw(new Rect(textX, rect.y + 14f, textWidth, 28f), pawn.LabelCap, UiFont.Heading, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			UiText.Draw(new Rect(textX, rect.y + 44f, textWidth, 24f), RatingText(pawn, rarity), UiFont.Body, rarityColor, TextAnchor.MiddleLeft, true);
			List<SkillRecord> top = pawn.skills?.skills.Where(s => !s.TotallyDisabled).OrderByDescending(s => s.Level).Take(3).ToList() ?? new List<SkillRecord>();
			string skills = string.Join(" · ", top.Select(s => s.def.LabelCap + " " + s.Level + PassionText(s.passion)).ToArray());
			UiText.Draw(new Rect(textX, rect.y + 72f, textWidth, 42f), skills, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true, true);
			int left = Mathf.Max(offer.expireTick - Find.TickManager.TicksGame, 0);
			UiText.Draw(new Rect(textX, rect.y + 120f, textWidth, 24f), "DreamsOutposts.Tavern.Expires".Translate(left.ToStringTicksToPeriod()), UiFont.Body, UiPalette.Ink3, TextAnchor.MiddleLeft);
			float bx = rect.xMax - 16f - actionWidth;
			Rect recruitRect = new Rect(bx, rect.y + 15f, recruitWidth, TavernActionButtonHeight);
			Rect driveAwayRect = new Rect(bx, rect.y + 57f, driveAwayWidth, TavernActionButtonHeight);
			if (UiWidgets.TexturedButton(recruitRect, recruitLabel, recruitTexture, ActionLabelSourceX, ActionLabelSourceWidth,
				UiPalette.Brand, UiPalette.BrandHover, UiPalette.OnAccent, UiButtonSize.Normal))
			{
				AdventurerRecruitUtility.Recruit(outpost, offer);
			}
			if (UiWidgets.TexturedButton(driveAwayRect, driveAwayLabel, driveAwayTexture, ActionLabelSourceX, ActionLabelSourceWidth,
				UiPalette.Danger, UiPalette.DangerHover, UiPalette.OnAccent, UiButtonSize.Normal))
			{
				Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("DreamsOutposts.Tavern.DismissConfirm".Translate(pawn.LabelShortCap), () => AdventurerRecruitUtility.RemoveOffer(outpost, offer), destructive: true));
			}
			// 卡片本身也能点开信息卡。ButtonInvisible 不检查事件是否已被消费，所以必须先排除两个按钮占的
			// 区域，否则点在按钮上会同时触发按钮和这里。
			if (!Mouse.IsOver(recruitRect) && !Mouse.IsOver(driveAwayRect) && Widgets.ButtonInvisible(rect))
			{
				Find.WindowStack.Add(new Dialog_InfoCard(pawn));
			}
		}

		private static string PassionText(Passion passion) => passion == Passion.Major ? " ♥♥" : passion == Passion.Minor ? " ♥" : string.Empty;

		/// <summary>
		/// Rating line: the kind's rating and combat power, e.g. "Overall rating: Elite · strength 130".
		/// 战斗力超过 200 的个体这一档显示为「传说」。
		/// </summary>
		private static string RatingText(Pawn pawn, AdventurerRarity rarity)
		{
			float power = pawn.kindDef?.combatPower ?? 0f;
			return "DreamsOutposts.Tavern.Rarity".Translate(
				AdventurerRecruitUtility.RarityLabelFor(pawn, rarity),
				power.ToString("0"));
		}

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
