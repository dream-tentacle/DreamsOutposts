using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>设施详情弹窗正文（描述 / 基础 / 生产规则 / 炮击）。</summary>
	public sealed class UiDetailsModalBody : IUiModalBody
	{
		private const float SectionTitleTop = 16f;

		private const float SectionTitleBottom = 9f;

		private readonly UiDetailsView details;

		public UiDetailsModalBody(UiDetailsView details)
		{
			this.details = details;
		}

		public float Height(float width)
		{
			return Layout(new Rect(0f, 0f, width, 0f), true);
		}

		public void Draw(Rect rect)
		{
			Layout(rect, false);
		}

		private float Layout(Rect rect, bool measure)
		{
			float y = rect.y;
			float width = rect.width;
			bool firstSection = true;
			if (!string.IsNullOrEmpty(details.Description))
			{
				float height = UiText.Height(details.Description, UiFont.Body, width);
				if (!measure)
				{
					UiText.Draw(new Rect(rect.x, y, width, height), details.Description, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, false, true);
				}
				y += height + 4f;
			}
			// 「基础」段已整段移除；下面第一个真正出现的段落用 firstSection 顶到最上面
			if (details.Rules.Count > 0)
			{
				y = SectionTitle(rect.x, y, width, "DreamsOutposts.Ui.Section.Production".Translate(), measure, firstSection);
				firstSection = false;
				for (int i = 0; i < details.Rules.Count; i++)
				{
					y = RuleCard(rect.x, y, width, details.Rules[i], measure);
					y += 8f;
				}
			}
			if (details.Bombardment.Count > 0)
			{
				y = SectionTitle(rect.x, y, width, "DreamsOutposts.Ui.Section.Bombardment".Translate(), measure, firstSection);
				firstSection = false;
				y = KvGrid(rect.x, y, width, details.Bombardment, measure);
			}
			return Mathf.Max(y - rect.y, 1f);
		}

		private float SectionTitle(float x, float y, float width, string text, bool measure, bool isFirst)
		{
			float top = isFirst ? 0f : SectionTitleTop;
			float lineHeight = UiText.LineHeight(UiFont.Body);
			if (!measure)
			{
				UiText.Draw(new Rect(x, y + top, width, lineHeight), text, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, true, false, true);
			}
			return y + top + lineHeight + SectionTitleBottom;
		}

		private float KvGrid(float x, float y, float width, List<KeyValuePair<string, string>> rows, bool measure)
		{
			if (rows == null || rows.Count == 0)
			{
				return y;
			}
			int columns = UiMetrics.GridColumns(width, UiMetrics.KvGridMinCell, UiMetrics.KvGridGap);
			float cellWidth = UiMetrics.GridCellWidth(width, columns, UiMetrics.KvGridGap);
			float keyHeight = UiText.LineHeight(UiFont.Body);
			float valueHeight = UiText.LineHeight(UiFont.Body);
			float cellHeight = UiMetrics.KvPaddingV * 2f + keyHeight + 2f + valueHeight;
			int rowCount = Mathf.CeilToInt((float)rows.Count / columns);
			for (int i = 0; i < rows.Count; i++)
			{
				int row = i / columns;
				int column = i % columns;
				Rect cell = new Rect(x + (cellWidth + UiMetrics.KvGridGap) * column,
					y + (cellHeight + UiMetrics.KvGridGap) * row, cellWidth, cellHeight);
				if (!measure)
				{
					UiText.Draw(new Rect(cell.x + UiMetrics.KvPaddingH, cell.y + UiMetrics.KvPaddingV, cell.width - UiMetrics.KvPaddingH * 2f, keyHeight),
						rows[i].Key, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
					UiText.Draw(new Rect(cell.x + UiMetrics.KvPaddingH, cell.y + UiMetrics.KvPaddingV + keyHeight + 2f, cell.width - UiMetrics.KvPaddingH * 2f, valueHeight),
						rows[i].Value, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, false, false, true);
				}
			}
			return y + rowCount * cellHeight + (rowCount - 1) * UiMetrics.KvGridGap;
		}

		private float RuleCard(float x, float y, float width, UiRuleView rule, bool measure)
		{
			float innerX = x + UiMetrics.RuleCardPaddingH;
			float innerWidth = width - UiMetrics.RuleCardPaddingH * 2f;
			float innerY = y + UiMetrics.RuleCardPaddingV;
			float headHeight = UiText.LineHeight(UiFont.Body);
			if (!measure)
			{
				Rect head = new Rect(innerX, innerY, innerWidth, headHeight);
				float iconSize = UiMetrics.MatIconSize;
				if (rule.Product != null)
				{
					Rect iconRect = new Rect(head.x, head.y + (head.height - iconSize) * 0.5f, iconSize, iconSize);
					Rect labelRect = new Rect(head.x + iconSize + 9f, head.y, innerWidth - iconSize - 9f, head.height);
					UiDraw.ThingInfoLink(head, iconRect, labelRect, rule.Product, rule.ProductLabel, UiFont.Body, UiPalette.Ink, null, true);
				}
				else
				{
					UiText.Draw(head, rule.ProductLabel, UiFont.Body, UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
				}
			}
			float cursor = innerY + headHeight + UiMetrics.RuleCardGap;
			// 事实 chips
			List<UiChipView> chips = new List<UiChipView>();
			for (int i = 0; i < rule.Facts.Count; i++)
			{
				UiChipKind kind = UiChipKind.Neutral;
				string hint = (i < rule.FactKinds.Count) ? rule.FactKinds[i] : "neutral";
				if (hint == "good")
				{
					kind = UiChipKind.Good;
				}
				else if (hint == "warn")
				{
					kind = UiChipKind.Warn;
				}
				else if (hint == "bad")
				{
					kind = UiChipKind.Bad;
				}
				chips.Add(new UiChipView(rule.Facts[i], kind));
			}
			if (chips.Count > 0)
			{
				float chipsHeight = UiDraw.ChipsHeight(chips, innerWidth, true);
				if (!measure)
				{
					UiDraw.Chips(new Rect(innerX, cursor, innerWidth, chipsHeight), chips, true);
				}
				cursor += chipsHeight + UiMetrics.RuleCardGap;
			}
			// 每单位消耗
			if (rule.Inputs.Count > 0)
			{
				float rowHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body));
				for (int i = 0; i < rule.Inputs.Count; i++)
				{
					UiCostLine line = rule.Inputs[i];
					if (!measure)
					{
						Rect row = new Rect(innerX, cursor, innerWidth, rowHeight);
						Rect iconRect = new Rect(row.x, row.y + (row.height - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
						string name = (line.Thing != null) ? line.Thing.label : "-";
						float nameX = iconRect.xMax + 6f;
						float nameWidth = Mathf.Min(UiText.Width(name, UiFont.Body) + 2f, Mathf.Max(row.width * 0.45f, 20f));
						Rect nameRect = new Rect(nameX, row.y, nameWidth, row.height);
						UiDraw.ThingInfoLink(new Rect(iconRect.x, row.y, nameRect.xMax - iconRect.x, row.height), iconRect, nameRect,
							line.Thing, name, UiFont.Body, line.Ok ? UiPalette.Good : UiPalette.Bad);
						string status = line.Have + " / " + line.Need + "  " + "DreamsOutposts.Ui.Rule.PerUnit".Translate();
						UiText.Draw(new Rect(nameRect.xMax + 5f, row.y, Mathf.Max(row.xMax - nameRect.xMax - 5f, 10f), row.height),
							status, UiFont.Body, line.Ok ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleLeft, false, false, true);
					}
					cursor += rowHeight;
				}
				cursor += UiMetrics.RuleCardGap;
			}
			if (!string.IsNullOrEmpty(rule.MaxCraftableText))
			{
				float height = UiText.LineHeight(UiFont.Body);
				if (!measure)
				{
					UiText.Draw(new Rect(innerX, cursor, innerWidth, height), rule.MaxCraftableText, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
				}
				cursor += height;
			}
			float total = cursor - y + UiMetrics.RuleCardPaddingV;
			if (!measure)
			{
				UiDebug.Scope("details.rule", new Rect(x, y, width, total));
			}
			return y + total;
		}

	}

	/// <summary>安装设施弹窗正文（候选卡片栅格）。</summary>
	public sealed class UiInstallModalBody : IUiModalBody
	{
		private const int TagTabsPerRow = 5;
		private const float TagTabRowHeight = 38f;
		private const float TagTabGap = 3f;

		private readonly Window_OutpostManage shell;

		private readonly OutpostSlot slot;
		private OutpostFacilityCategoryDef selectedCategory;

		/// <summary>当前选中的分类。默认停在排序后的第一个分类。</summary>
		private OutpostFacilityCategoryDef SelectedCategory
		{
			get
			{
				if (selectedCategory == null)
				{
					IReadOnlyList<OutpostFacilityCategoryDef> categories = OutpostFacilityCategoryUtility.AllInOrder;
					selectedCategory = (categories.Count > 0) ? categories[0] : null;
				}
				return selectedCategory;
			}
		}

		public UiInstallModalBody(Window_OutpostManage shell, OutpostSlot slot)
		{
			this.shell = shell;
			this.slot = slot;
		}

		/// <summary>正文高度（只影响滚动范围，面板大小由 UseMaxHeight 固定）。</summary>
		public float Height(float width)
		{
			float tagTabsHeight = GetTagTabsHeight();
			return tagTabsHeight + UiMetrics.InstallGridGap + MeasureGrid(width, FilteredCards());
		}

		private List<UiInstallCardView> FilteredCards()
		{
			List<UiInstallCardView> source = shell.Cache.InstallCandidates(slot, shell.InstallOnlyAvailable);
			OutpostFacilityCategoryDef category = SelectedCategory;
			return source.FindAll(card => card?.Def != null && card.Def.Category == category);
		}

		private static float MeasureGrid(float width, List<UiInstallCardView> cards)
		{
			if (cards.Count == 0)
			{
				return UiText.LineHeight(UiFont.Body) * 2f;
			}
			int columns = UiMetrics.GridColumns(width, UiMetrics.InstallGridMinCell, UiMetrics.InstallGridGap);
			float cellWidth = UiMetrics.GridCellWidth(width, columns, UiMetrics.InstallGridGap);
			float total = 0f;
			int rowCount = Mathf.CeilToInt((float)cards.Count / columns);
			for (int row = 0; row < rowCount; row++)
			{
				float rowHeight = 0f;
				for (int column = 0; column < columns; column++)
				{
					int index = row * columns + column;
					if (index >= cards.Count)
					{
						break;
					}
					rowHeight = Mathf.Max(rowHeight, UiInstallCardRenderer.Measure(cards[index], cellWidth));
				}
				total += rowHeight + UiMetrics.InstallGridGap;
			}
			return Mathf.Max(total - UiMetrics.InstallGridGap, 1f);
		}

		public void Draw(Rect rect)
		{
			float tagTabsHeight = GetTagTabsHeight();
			DrawTagTabs(new Rect(rect.x, rect.y, rect.width, tagTabsHeight));
			Rect gridRect = new Rect(rect.x, rect.y + tagTabsHeight + UiMetrics.InstallGridGap, rect.width,
				Mathf.Max(rect.height - tagTabsHeight - UiMetrics.InstallGridGap, 0f));
			List<UiInstallCardView> cards = FilteredCards();
			if (cards.Count == 0)
			{
				string text = shell.InstallOnlyAvailable
					? "DreamsOutposts.Ui.Install.NoneAvailable".Translate().ToString()
					: "DreamsOutposts.NoFacilityInstallable".Translate().ToString();
				UiText.Draw(new Rect(gridRect.x, gridRect.y, gridRect.width, UiText.LineHeight(UiFont.Body) * 2f), text, UiFont.Body, UiPalette.Ink2);
				return;
			}
			int columns = UiMetrics.GridColumns(gridRect.width, UiMetrics.InstallGridMinCell, UiMetrics.InstallGridGap);
			float cellWidth = UiMetrics.GridCellWidth(gridRect.width, columns, UiMetrics.InstallGridGap);
			float y = gridRect.y;
			int rowCount = Mathf.CeilToInt((float)cards.Count / columns);
			for (int row = 0; row < rowCount; row++)
			{
				float rowHeight = 0f;
				for (int column = 0; column < columns; column++)
				{
					int index = row * columns + column;
					if (index >= cards.Count)
					{
						break;
					}
					rowHeight = Mathf.Max(rowHeight, UiInstallCardRenderer.Measure(cards[index], cellWidth));
				}
				for (int column = 0; column < columns; column++)
				{
					int index = row * columns + column;
					if (index >= cards.Count)
					{
						break;
					}
					Rect cardRect = new Rect(gridRect.x + (cellWidth + UiMetrics.InstallGridGap) * column, y, cellWidth, rowHeight);
					UiInstallCardRenderer.Draw(cardRect, cards[index], delegate(UiInstallCardView card)
					{
						shell.TryInstall(slot, card);
					}, delegate(UiInstallCardView card)
					{
						shell.TryForceInstall(slot, card);
					});
				}
				y += rowHeight + UiMetrics.InstallGridGap;
			}
		}

		private void DrawTagTabs(Rect rect)
		{
			IReadOnlyList<OutpostFacilityCategoryDef> categories = OutpostFacilityCategoryUtility.AllInOrder;
			OutpostFacilityCategoryDef selected = SelectedCategory;
			UiDraw.Box(rect, (int)UiMetrics.RadiusSm, UiPalette.Raised, UiPalette.Line);
			Rect inner = rect.ContractedBy(4f);
			int rowCount = Mathf.Max(Mathf.CeilToInt((float)categories.Count / TagTabsPerRow), 1);
			float rowHeight = (inner.height - TagTabGap * (rowCount - 1)) / rowCount;
			for (int row = 0; row < rowCount; row++)
			{
				int firstIndex = row * TagTabsPerRow;
				int columns = Mathf.Min(TagTabsPerRow, categories.Count - firstIndex);
				if (columns <= 0)
				{
					break;
				}
				float width = (inner.width - TagTabGap * (columns - 1)) / columns;
				for (int column = 0; column < columns; column++)
				{
					OutpostFacilityCategoryDef category = categories[firstIndex + column];
					Rect button = new Rect(inner.x + column * (width + TagTabGap), inner.y + row * (rowHeight + TagTabGap), width, rowHeight);
					if (UiWidgets.SegmentTab(button, category.LabelCap.ToString(), category == selected)) selectedCategory = category;
				}
			}
		}

		private static float GetTagTabsHeight()
		{
			int rowCount = Mathf.CeilToInt((float)OutpostFacilityCategoryUtility.AllInOrder.Count / TagTabsPerRow);
			return Mathf.Max(rowCount, 1) * TagTabRowHeight;
		}
	}

	/// <summary>安装候选卡的测量与绘制（同一套布局代码）。</summary>
	internal static class UiInstallCardRenderer
	{
		private const float FooterBottomPadding = 5f;

		public static float Measure(UiInstallCardView card, float width)
		{
			return Layout(new Rect(0f, 0f, width, 0f), card, true, null);
		}

		public static void Draw(Rect rect, UiInstallCardView card, Action<UiInstallCardView> onBuild, Action<UiInstallCardView> onForceBuild)
		{
			Layout(rect, card, false, onBuild, onForceBuild);
		}

		private static float Layout(Rect rect, UiInstallCardView card, bool measure, Action<UiInstallCardView> onBuild, Action<UiInstallCardView> onForceBuild = null)
		{
			if (!measure)
			{
				UiDraw.Panel(rect, (int)UiMetrics.RadiusSm, UiPalette.Card, UiPalette.Line, false);
				UiDebug.Scope("install.card", rect);
			}
			float innerX = rect.x + UiMetrics.InstallCardPadding;
			float innerWidth = Mathf.Max(rect.width - UiMetrics.InstallCardPadding * 2f, 20f);
			float y = rect.y + UiMetrics.InstallCardPadding;
			float headHeight = UiText.LineHeight(UiFont.Body);
			float iconSize = UiMetrics.MatIconSize;
			if (!measure)
			{
				Rect head = new Rect(innerX, y, innerWidth, headHeight);
				bool headHovered = Mouse.IsOver(head);
				UiDraw.Icon(new Rect(head.x, head.y + (head.height - iconSize) * 0.5f, iconSize, iconSize), card.Icon,
					headHovered ? UiPalette.BrandText : UiPalette.Ink);
				UiText.Draw(new Rect(head.x + iconSize + 9f, head.y, innerWidth - iconSize - 9f, head.height), card.Label,
					UiFont.Body, headHovered ? UiPalette.BrandText : UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
				UiWidgets.Tip(head, card.Tooltip, card.TooltipId);
				// 点名称/图标 → 原版信息面板
				if (card.Def != null && Widgets.ButtonInvisible(head))
				{
					Find.WindowStack.Add(new Dialog_InfoCard(card.Def));
				}
			}
			y += headHeight + UiMetrics.InstallCardGap;
			// 造价
			float rowHeight = Mathf.Max(UiMetrics.MatIconSize, UiText.LineHeight(UiFont.Body)) + UiMetrics.CostRowPaddingV * 2f;
			if (card.Cost.Count == 0)
			{
				if (!measure)
				{
					UiText.Draw(new Rect(innerX, y, innerWidth, rowHeight), "DreamsOutposts.NoMaterialsNeeded".Translate(), UiFont.Body, UiPalette.Ink2, TextAnchor.MiddleLeft);
				}
				y += rowHeight + UiMetrics.InstallCardGap;
			}
			else
			{
				for (int i = 0; i < card.Cost.Count; i++)
				{
					UiCostLine line = card.Cost[i];
					if (!measure)
					{
						Rect row = new Rect(innerX, y, innerWidth, rowHeight);
						Rect iconRect = new Rect(row.x + UiMetrics.CostRowPaddingH, row.y + (row.height - UiMetrics.MatIconSize) * 0.5f, UiMetrics.MatIconSize, UiMetrics.MatIconSize);
						float textX = row.x + UiMetrics.CostRowPaddingH + UiMetrics.MatIconSize + UiMetrics.CostRowGap;
						string number = line.Have + " / " + line.Need;
						float numberWidth = UiText.Width(number, UiFont.Body, true);
						Rect nameRect = new Rect(textX, row.y, Mathf.Max(row.width - textX + row.x - numberWidth - 6f, 20f), row.height);
						UiDraw.ThingInfoLink(new Rect(iconRect.x, row.y, nameRect.xMax - iconRect.x, row.height), iconRect, nameRect,
							line.Thing, (line.Thing != null) ? line.Thing.label : "-", UiFont.Body, UiPalette.Ink2);
						UiText.Draw(new Rect(row.xMax - UiMetrics.CostRowPaddingH - numberWidth, row.y, numberWidth, row.height), number,
							UiFont.Body, line.Ok ? UiPalette.Good : UiPalette.Bad, TextAnchor.MiddleRight, true);
					}
					y += rowHeight + UiMetrics.CostListGap;
				}
				y += UiMetrics.InstallCardGap - UiMetrics.CostListGap;
			}
			// chips
			if (card.Chips.Count > 0)
			{
				float chipsHeight = UiDraw.ChipsHeight(card.Chips, innerWidth, true);
				if (!measure)
				{
					UiDraw.Chips(new Rect(innerX, y, innerWidth, chipsHeight), card.Chips, true);
				}
				y += chipsHeight + UiMetrics.InstallCardGap;
			}
			// 修正行
			if (card.ModLines.Count > 0)
			{
				float lineHeight = UiText.LineHeight(UiFont.Body);
				for (int i = 0; i < card.ModLines.Count; i++)
				{
					if (!measure)
					{
						UiText.Draw(new Rect(innerX, y + lineHeight * i, innerWidth, lineHeight), card.ModLines[i], UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
					}
				}
				y += lineHeight * card.ModLines.Count + UiMetrics.InstallCardGap;
			}
			// 底部：只有可建造时才显示建造按钮。尺寸与详情弹窗的「拆除」按钮同一套算法
			float buttonHeight = UiWidgets.ButtonHeight(UiButtonSize.Normal);
			float footerHeight = buttonHeight + 8f;
			if (!measure)
			{
				float footerY = rect.yMax - FooterBottomPadding - buttonHeight;
				UiDraw.Divider(new Rect(rect.x + UiMetrics.InstallCardPadding, footerY - 8f, innerWidth, 1f), UiPalette.Line);
				string buildLabel = "DreamsOutposts.Build".Translate();
				Texture2D buildTexture = UiTex.PositiveButtonTexture();
				float buildWidth = UiWidgets.TexturedButtonWidth(buttonHeight, buildTexture, buildLabel);
				Rect buildRect = new Rect(rect.xMax - UiMetrics.InstallCardPadding - buildWidth, footerY, buildWidth, buttonHeight);
				if (card.Allowed && UiWidgets.TexturedPrimaryButton(buildRect, buildLabel, buildTexture,
					420f, 300f, UiButtonSize.Normal,
					"DreamsOutposts.Ui.Install.PayFromStock".Translate().ToString()) && onBuild != null)
				{
					onBuild(card);
				}
				if (!card.Allowed)
				{
					Rect reasonRect = new Rect(innerX, footerY, innerWidth, buttonHeight);
					UiText.Draw(reasonRect, card.Reason, UiFont.Body, UiPalette.Bad,
						TextAnchor.MiddleRight, false, false, true);
					UiWidgets.Tip(reasonRect, card.Reason,
						GenText.StableStringHash("install-disabled-reason-" + (card.Def?.defName ?? "null")));
				}
				if (DebugSettings.godMode)
				{
					string forceLabel = "DreamsOutposts.ForceBuild".Translate();
					float forceWidth = Mathf.Max(UiWidgets.ButtonWidth(forceLabel, UiButtonSize.Small), 64f);
					Rect forceRect = new Rect(buildRect.x - UiMetrics.ModalFootGap - forceWidth, footerY, forceWidth, buttonHeight);
					if (UiWidgets.Button(forceRect, forceLabel, UiButtonKind.Danger, true, null, UiButtonSize.Small) && onForceBuild != null)
					{
						onForceBuild(card);
					}
				}
			}
			// 分隔线是从底边反推出来的（footerY - 8f），所以这里把正文与线之间的留白算进总高，
			// 否则最高的那张卡正文底边会正好压在线上面（空隙为 0）
			float total = y - rect.y + UiMetrics.InstallCardFootGap + footerHeight + FooterBottomPadding - UiMetrics.InstallCardGap;
			return total;
		}
	}

	/// <summary>拆除确认弹窗正文。</summary>
	public sealed class UiDemolishModalBody : IUiModalBody
	{
		private readonly UiFacilityView view;

		public UiDemolishModalBody(UiFacilityView view)
		{
			this.view = view;
		}

		private string Text()
		{
			return "DreamsOutposts.ConfirmDemolish".Translate(view.Label, view.RefundLabel).ToString();
		}

		public float Height(float width)
		{
			// 只报正文自己的高度（面板会在它之外再加上下留白）；额外留一点，确认框不至于挤成一坨
			return UiText.Height(Text(), UiFont.Body, width) + 12f;
		}

		public void Draw(Rect rect)
		{
			UiText.Draw(rect, Text(), UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
		}
	}
}
