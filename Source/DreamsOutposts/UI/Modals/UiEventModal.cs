using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DreamsOutposts
{
	/// <summary>
	/// 事件弹窗正文：描述 / 基础信息 / 超时自动选择 / 选项面板。
	/// 按需求去掉：事件分类后面的「（当前权重 x）」、超时自动选择下面的「结果：…」、
	/// 选项区的「点选一个选项，然后在左下角点「确认」。」提示、选中项上的「已选中」标记。
	/// </summary>
	public sealed class UiEventModalBody : IUiModalBody, IUiModalBodyHeader
	{
		private enum LayoutMode
		{
			MeasureAll,
			MeasureHeader,
			DrawAll,
			DrawHeader
		}

		private const float SectionTop = 16f;

		private const float SectionBottom = 9f;

		private const float KvMinCell = 190f;

		private const float KvGap = 8f;

		private const float KvPaddingH = 10f;

		private const float KvPaddingV = 8f;

		private const float BlockPaddingH = 13f;

		private const float BlockPaddingV = 12f;

		private const float BlockGap = 8f;

		private const float BlockSpacing = 8f;

		private const float RadioSize = 15f;

		private const float RadioInnerInset = 3f;

		private const float FailPaddingH = 9f;

		private const float FailPaddingV = 7f;

		private readonly Window_OutpostManage shell;

		private readonly UiEventView view;

		private string selectedOptionId;

		public UiEventModalBody(Window_OutpostManage shell, UiEventView view)
		{
			this.shell = shell;
			this.view = view;
			// 默认选中第一个可选项，省一次点击
			for (int i = 0; i < view.Options.Count; i++)
			{
				if (view.Options[i].Selectable)
				{
					selectedOptionId = view.Options[i].Id;
					break;
				}
			}
		}

		public UiEventOptionView SelectedOption
		{
			get
			{
				for (int i = 0; i < view.Options.Count; i++)
				{
					if (view.Options[i].Id == selectedOptionId)
					{
						return view.Options[i];
					}
				}
				return null;
			}
		}

		public float Height(float width)
		{
			return Layout(new Rect(0f, 0f, width, 0f), LayoutMode.MeasureAll);
		}

		public void Draw(Rect rect)
		{
			Layout(rect, LayoutMode.DrawAll);
		}

		// 固定头：描述 + 基础信息 + 超时自动选择（不滚动，也就不会被选项盖住）
		public float HeaderHeight(float width)
		{
			return Layout(new Rect(0f, 0f, width, 0f), LayoutMode.MeasureHeader);
		}

		public void DrawHeader(Rect rect)
		{
			UiDebug.Scope("modal.event.header", rect);
			Layout(rect, LayoutMode.DrawHeader);
		}

		// ---------------------------------------------------------------

		private float Layout(Rect rect, LayoutMode mode)
		{
			bool measure = (mode == LayoutMode.MeasureAll) || (mode == LayoutMode.MeasureHeader);
			bool withOptions = (mode == LayoutMode.MeasureAll) || (mode == LayoutMode.DrawAll);
			float y = rect.y;
			float width = rect.width;
			bool first = true;
			if (!string.IsNullOrEmpty(view.Description))
			{
				float height = UiText.Height(view.Description, UiFont.Body, width);
				if (!measure)
				{
					UiText.Draw(new Rect(rect.x, y, width, height), view.Description, UiFont.Body, UiPalette.Ink2, TextAnchor.UpperLeft, false, true);
				}
				y += height + 4f;
			}
			// 基础：剩余时间 / 事件分类（不显示当前权重）/ 持续时间
			List<KeyValuePair<string, string>> basics = new List<KeyValuePair<string, string>>();
			basics.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.RemainingTime".Translate().ToString(), view.RemainingText));
			if (!string.IsNullOrEmpty(view.CategoryLabel))
			{
				basics.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.Category".Translate().ToString(), view.CategoryLabel));
			}
			basics.Add(new KeyValuePair<string, string>("DreamsOutposts.Ui.Kv.Duration".Translate().ToString(), view.DurationText));
			y = SectionTitle(rect.x, y, width, "DreamsOutposts.Ui.Section.Basics".Translate(), measure, first);
			first = false;
			y = KvGrid(rect.x, y, width, basics, measure);
			// 超时自动选择（只显示会用哪个选项，不显示结果预览）
			if (view.HasDefault)
			{
				y = SectionTitle(rect.x, y, width, "DreamsOutposts.Ui.Kv.OnTimeout".Translate(), measure, false);
				float lineHeight = UiText.LineHeight(UiFont.Body);
				if (!measure)
				{
					UiText.Draw(new Rect(rect.x, y, width, lineHeight), view.DefaultOptionLabel, UiFont.Body, UiPalette.Ink,
						TextAnchor.UpperLeft, false, false, true);
				}
				y += lineHeight;
			}
			if (!withOptions)
			{
				return Mathf.Max(y - rect.y, 1f);
			}
			// 选项
			y = SectionTitle(rect.x, y, width, "DreamsOutposts.Options".Translate(), measure, false);
			if (view.Options.Count == 0)
			{
				float height = UiText.LineHeight(UiFont.Body);
				if (!measure)
				{
					UiText.Draw(new Rect(rect.x, y, width, height), "DreamsOutposts.Ui.EventNoOptions".Translate(), UiFont.Body, UiPalette.Ink2);
				}
				y += height;
			}
			else
			{
				for (int i = 0; i < view.Options.Count; i++)
				{
					// OptionBlock 返回的是「这张卡片的高度」，必须累加到 y 上。
					// 曾经写成 y = OptionBlock(...)：y 被直接设成高度，第二张卡就叠在第一张上了。
					float optionHeight = OptionBlock(rect.x, y, width, view.Options[i], i, measure);
					y += optionHeight + BlockSpacing;
				}
				y -= BlockSpacing;
			}
			return Mathf.Max(y - rect.y, 1f);
		}

		private float SectionTitle(float x, float y, float width, string text, bool measure, bool isFirst)
		{
			float top = isFirst ? 0f : SectionTop;
			float lineHeight = UiText.LineHeight(UiFont.Caption);
			if (!measure)
			{
				UiText.Draw(new Rect(x, y + top, width, lineHeight), text, UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, true, false, true);
			}
			return y + top + lineHeight + SectionBottom;
		}

		private float KvGrid(float x, float y, float width, List<KeyValuePair<string, string>> rows, bool measure)
		{
			if (rows == null || rows.Count == 0)
			{
				return y;
			}
			int columns = UiMetrics.GridColumns(width, KvMinCell, KvGap);
			float cellWidth = UiMetrics.GridCellWidth(width, columns, KvGap);
			float keyHeight = UiText.LineHeight(UiFont.Caption);
			float valueHeight = UiText.LineHeight(UiFont.Body);
			float cellHeight = KvPaddingV * 2f + keyHeight + 2f + valueHeight;
			int rowCount = Mathf.CeilToInt((float)rows.Count / columns);
			if (!measure)
			{
				for (int i = 0; i < rows.Count; i++)
				{
					int row = i / columns;
					int column = i % columns;
					Rect cell = new Rect(x + (cellWidth + KvGap) * column, y + (cellHeight + KvGap) * row, cellWidth, cellHeight);
					UiText.Draw(new Rect(cell.x + KvPaddingH, cell.y + KvPaddingV, cell.width - KvPaddingH * 2f, keyHeight),
						rows[i].Key, UiFont.Caption, UiPalette.Ink2, TextAnchor.UpperLeft, false, false, true);
					UiText.Draw(new Rect(cell.x + KvPaddingH, cell.y + KvPaddingV + keyHeight + 2f, cell.width - KvPaddingH * 2f, valueHeight),
						rows[i].Value, UiFont.Body, UiPalette.Ink, TextAnchor.UpperLeft, false, false, true);
				}
			}
			return y + rowCount * cellHeight + (rowCount - 1) * KvGap;
		}

		/// <summary>画一张选项卡。返回值是这张卡的高度（调用方需要自己累加到 y 上）。</summary>
		private float OptionBlock(float x, float y, float width, UiEventOptionView option, int index, bool measure)
		{
			bool selected = option.Selectable && option.Id == selectedOptionId;
			float height = OptionHeight(option, width);
			if (measure)
			{
				return height;
			}
			// 诊断用：选项卡片的真实位置与高度（先在「选项 → 快捷键 → 游戏」里绑定 UiDebug 的两个快捷键）
			UiDebug.Scope("modal.event.option[" + index + "]", new Rect(x, y, width, height));
			float innerX = x + BlockPaddingH;
			float innerWidth = Mathf.Max(width - BlockPaddingH * 2f, 30f);
			bool hovered = option.Selectable && Mouse.IsOver(new Rect(x, y, width, height));
			Color fill = selected ? UiPalette.BrandTint : UiPalette.Card;
			Color line = selected ? UiPalette.Brand : (hovered ? UiPalette.LineStrong : UiPalette.Line);
			if (hovered && !selected)
			{
				UiDraw.Shadow(new Rect(x, y, width, height), (int)UiMetrics.RadiusSm, 0.5f);
			}
			UiDraw.Box(new Rect(x, y, width, height), (int)UiMetrics.RadiusSm, fill, line);
			float cursor = y + BlockPaddingV;
			float headHeight = Mathf.Max(RadioSize, UiText.LineHeight(UiFont.Body));
			// 单选指示
			Rect radio = new Rect(innerX, cursor + (headHeight - RadioSize) * 0.5f, RadioSize, RadioSize);
			UiDraw.Box(radio, (int)UiMetrics.RadiusSm2, selected ? UiPalette.BrandTint : UiPalette.Raised, selected ? UiPalette.Brand : UiPalette.LineStrong);
			if (selected)
			{
				UiDraw.Box(new Rect(radio.x + RadioInnerInset, radio.y + RadioInnerInset, RadioSize - RadioInnerInset * 2f, RadioSize - RadioInnerInset * 2f),
					(int)UiMetrics.RadiusXs2, UiPalette.Brand);
			}
			float labelX = radio.xMax + 9f;
			float chipHeight = UiDraw.ChipHeight(true);
			float chipY = cursor + (headHeight - chipHeight) * 0.5f;
			float rightX = innerX + innerWidth;
			if (!option.PlayerSelectable)
			{
				UiChipView chip = new UiChipView("DreamsOutposts.Ui.Option.NotPlayerSelectable".Translate().ToString());
				chip.Small = true;
				float chipWidth = UiDraw.ChipWidth(chip);
				UiDraw.Chip(new Rect(rightX - chipWidth, chipY, chipWidth, chipHeight), chip);
				rightX -= chipWidth + 5f;
			}
			if (!option.RequirementsMet)
			{
				UiChipView chip = new UiChipView("DreamsOutposts.Ui.Option.RequirementsFailed".Translate().ToString(), UiChipKind.Bad);
				chip.Small = true;
				float chipWidth = UiDraw.ChipWidth(chip);
				UiDraw.Chip(new Rect(rightX - chipWidth, chipY, chipWidth, chipHeight), chip);
				rightX -= chipWidth + 5f;
			}
			UiText.Draw(new Rect(labelX, cursor, Mathf.Max(rightX - labelX, 30f), headHeight), option.Label,
				UiFont.Body, selected ? UiPalette.BrandText : UiPalette.Ink, TextAnchor.MiddleLeft, true, false, true);
			cursor += headHeight;
			// 效果预览
			if (option.EffectLines.Count > 0)
			{
				cursor += BlockGap;
				float lineHeight = UiText.LineHeight(UiFont.Caption);
				for (int i = 0; i < option.EffectLines.Count; i++)
				{
					DrawEffectLine(new Rect(innerX, cursor + lineHeight * i, innerWidth, lineHeight), option.EffectLines[i], option.EffectKinds[i]);
				}
				cursor += lineHeight * option.EffectLines.Count;
			}
			// 条件不满足的说明
			if (!option.RequirementsMet && !string.IsNullOrEmpty(option.FailureReason))
			{
				cursor += BlockGap;
				float textWidth = innerWidth - FailPaddingH * 2f;
				float textHeight = UiText.Height(option.FailureReason, UiFont.Caption, textWidth);
				Rect failRect = new Rect(innerX, cursor, innerWidth, textHeight + FailPaddingV * 2f);
				UiDebug.Scope("modal.event.option[" + index + "].fail", failRect);
				UiDraw.Box(failRect, (int)UiMetrics.RadiusSm, UiPalette.BadBg, UiPalette.BadLine);
				UiText.Draw(new Rect(failRect.x + FailPaddingH, failRect.y + FailPaddingV, textWidth, textHeight),
					option.FailureReason, UiFont.Caption, UiPalette.Bad, TextAnchor.UpperLeft, false, true);
			}
			// 整块可点（选中项不需要再显示「已选中」标记，高亮就够了）
			if (option.Selectable && Widgets.ButtonInvisible(new Rect(x, y, width, height)))
			{
				selectedOptionId = option.Id;
			}
			return height;
		}

		private float OptionHeight(UiEventOptionView option, float width)
		{
			float innerWidth = Mathf.Max(width - BlockPaddingH * 2f, 30f);
			float height = BlockPaddingV * 2f + Mathf.Max(RadioSize, UiText.LineHeight(UiFont.Body));
			if (option.EffectLines.Count > 0)
			{
				height += BlockGap + UiText.LineHeight(UiFont.Caption) * option.EffectLines.Count;
			}
			if (!option.RequirementsMet && !string.IsNullOrEmpty(option.FailureReason))
			{
				height += BlockGap + UiText.Height(option.FailureReason, UiFont.Caption, innerWidth - FailPaddingH * 2f) + FailPaddingV * 2f;
			}
			return height;
		}

		private static void DrawEffectLine(Rect rect, string text, string kind)
		{
			Color color = UiPalette.Ink2;
			if (kind == "add")
			{
				color = UiPalette.Good;
			}
			else if (kind == "remove")
			{
				color = UiPalette.Bad;
			}
			else if (kind == "schedule")
			{
				color = UiPalette.Info;
			}
			else if (kind == "skill")
			{
				color = UiPalette.Purple;
			}
			UiText.Draw(rect, text, UiFont.Caption, color, TextAnchor.MiddleLeft, false, false, true);
		}
	}
}
