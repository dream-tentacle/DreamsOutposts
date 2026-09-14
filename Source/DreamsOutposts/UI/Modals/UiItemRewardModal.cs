using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	public sealed class UiItemRewardView
	{
		public readonly ThingDef Def;
		public readonly string Label;
		public int Count;

		public UiItemRewardView(ThingDef def, string label)
		{
			Def = def;
			Label = label;
		}
	}

	public sealed class UiItemRewardModalBody : IUiModalBody
	{
		private const float IntroGap = 14f;
		private const float CardHeight = 58f;
		private const float CardGap = 8f;
		private const float CardMinWidth = 260f;
		private const float IconSize = 36f;
		private readonly List<UiItemRewardView> rewards;

		public UiItemRewardModalBody(List<UiItemRewardView> rewards)
		{
			this.rewards = rewards;
		}

		public float Height(float width)
		{
			int columns = UiMetrics.GridColumns(width, CardMinWidth, CardGap);
			int rows = Mathf.CeilToInt((float)rewards.Count / columns);
			return UiText.LineHeight(UiFont.Body) + IntroGap + rows * CardHeight + Mathf.Max(rows - 1, 0) * CardGap;
		}

		public void Draw(Rect rect)
		{
			float introHeight = UiText.LineHeight(UiFont.Body);
			UiText.Draw(new Rect(rect.x, rect.y, rect.width, introHeight),
				"DreamsOutposts.EventEffect.ItemsStoredText".Translate(), UiFont.Body, UiPalette.Ink2);
			float y = rect.y + introHeight + IntroGap;
			int columns = UiMetrics.GridColumns(rect.width, CardMinWidth, CardGap);
			float cardWidth = UiMetrics.GridCellWidth(rect.width, columns, CardGap);
			for (int i = 0; i < rewards.Count; i++)
			{
				int row = i / columns;
				int column = i % columns;
				Rect card = new Rect(rect.x + column * (cardWidth + CardGap), y + row * (CardHeight + CardGap), cardWidth, CardHeight);
				UiItemRewardView reward = rewards[i];
				UiDraw.Box(card, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line);
				Rect icon = new Rect(card.x + 11f, card.y + (card.height - IconSize) * 0.5f, IconSize, IconSize);
				Rect label = new Rect(icon.xMax + 10f, card.y, Mathf.Max(card.width - IconSize - 78f, 30f), card.height);
				Rect count = new Rect(label.xMax + 5f, card.y, Mathf.Max(card.xMax - label.xMax - 16f, 35f), card.height);
				UiDraw.ThingInfoLink(new Rect(icon.x, card.y, label.xMax - icon.x, card.height), icon, label,
					reward.Def, reward.Label, UiFont.Body, UiPalette.Ink, null, true);
				UiText.Draw(count, "×" + reward.Count, UiFont.Body, UiPalette.BrandText, TextAnchor.MiddleRight, true, false, true);
			}
		}
	}

	public static class UiItemRewardWindow
	{
		public static void Open(Outpost outpost, List<UiItemRewardView> rewards)
		{
			if (outpost == null || rewards.NullOrEmpty())
			{
				return;
			}
			Window_OutpostModal window = new Window_OutpostModal
			{
				TitleText = "DreamsOutposts.EventEffect.RandomItemsReceivedTitle".Translate(),
				SubText = outpost.LabelCap,
				PanelWidth = UiMetrics.ModalNormalWidth,
				Body = new UiItemRewardModalBody(rewards)
			};
			window.FooterDrawer = delegate(Rect rect)
			{
				string closeLabel = "DreamsOutposts.Ui.Close".Translate();
				float height = UiWidgets.ButtonHeight(UiButtonSize.Normal);
				float width = UiWidgets.ButtonWidth(closeLabel);
				Rect button = new Rect(rect.xMax - width, rect.y + (rect.height - height) * 0.5f, width, height);
				if (UiWidgets.Button(button, closeLabel))
				{
					window.Close();
				}
			};
			Find.WindowStack.Add(window);
		}
	}
}
