namespace DreamsOutposts
{
	/// <summary>
	/// 新 UI 的几何度量，逐值对应 temp/styles.css 的尺寸与间距。
	/// 改版式只改这里，页面代码不动。
	/// </summary>
	public static class UiMetrics
	{
		// ---------- 窗口 ----------
		public const float WindowMaxWidth = 1240f;
		public const float WindowMaxHeight = 860f;
		public const float WindowShadowMargin = 18f;   // windowRect 比面板大出来的阴影留白
		public const float TitlebarPaddingTop = 14f;
		public const float TitlebarPaddingBottom = 14f;
		public const float TitlebarPaddingLeft = 18f;
		public const float TitlebarPaddingRight = 16f;
		public const float TitlebarGap = 18f;
		public const float EmblemSize = 42f;
		public const float EmblemIconSize = 22f;
		public const float TitleSubGap = 6f;

		// ---------- 侧栏 ----------
		public const float SidebarWidth = 218f;
		public const float SidebarPaddingH = 12f;
		public const float SidebarPaddingV = 14f;
		public const float SidebarGap = 4f;
		public const float NavItemPaddingH = 11f;
		public const float NavItemPaddingV = 9f;
		public const float NavItemGap = 10f;
		public const float NavIconSize = 17f;
		public const float NavLabelPaddingH = 10f;
		public const float NavLabelPaddingTop = 4f;
		public const float NavLabelPaddingBottom = 8f;
		public const float NavBadgeMinWidth = 20f;
		public const float NavBadgePaddingH = 6f;
		public const float NavActiveArcRadius = 7f;
		public const float NavActiveArcGap = 2f;

		// ---------- 内容区 ----------
		public const float ContentPaddingTop = 18f;
		public const float ContentPaddingH = 20f;
		public const float ContentPaddingBottom = 26f;
		public const float PageHeadMarginBottom = 16f;
		public const float PageHeadSubGap = 4f;
		public const float PageHeadMaxTextWidth = 560f;   // CSS max-width:68ch 的近似
		public const float SectionSpacing = 22f;
		public const float SectionHeadGap = 10f;
		public const float SectionHeadMarginBottom = 10f;

		// ---------- 滚动条 ----------
		public const float ScrollbarGutter = 10f;    // scrollbar-gutter: stable，恒定预留
		public const float ScrollbarWidth = 10f;
		public const float ScrollbarThumbWidth = 6f;
		public const float ScrollbarMinThumb = 24f;
		public const float ScrollWheelSpeed = 28f;

		// ---------- 通用 ----------
		public const float BorderWidth = 1f;
		public const float RadiusXs = 6f;
		public const float RadiusSm = 8f;
		public const float RadiusXs2 = 2f;   // pip / 进度条内芯
		public const float RadiusSm2 = 4f;   // 进度条 / tick 方块

		public const float ButtonPaddingH = 14f;
		public const float ButtonPaddingV = 7f;
		public const float ButtonSmallPaddingH = 10f;
		public const float ButtonSmallPaddingV = 5f;
		public const float ButtonGap = 6f;
		public const float IconButtonSize = 28f;
		public const float CloseButtonSize = 32f;

		public const float ChipPaddingH = 9f;
		public const float ChipPaddingV = 2f;
		public const float ChipSmallPaddingH = 7f;
		public const float ChipSmallPaddingV = 1f;
		public const float ChipGap = 5f;
		public const float ChipGlyphSize = 12f;
		public const float ChipFlowGap = 8f;

		public const float BarHeight = 8f;
		public const float BarRadius = 4f;

		// ---------- 等级卡 ----------
		public const float LevelCardPaddingH = 18f;
		public const float LevelCardPaddingV = 16f;
		public const float LevelCardGap = 20f;
		public const float LevelRightWidth = 330f;
		public const float PipWidth = 34f;
		public const float PipHeight = 6f;
		public const float PipGap = 6f;
		public const float PipMarginBottom = 10f;
		public const float LevelFactsGap = 8f;
		public const float LevelFactsMarginTop = 12f;
		public const float LevelUpgradeSweepDuration = 0.35f;
		public const float LevelUpgradeFlashDuration = 0.9f;
		public const float LevelUpgradeFlashFillAlpha = 0.14f;
		public const float LevelUpgradeFlashBorderAlpha = 0.75f;
		public const float LevelUpgradeSweepHeight = 64f;
		public const float LevelUpgradeSweepAlpha = 0.1375f;
		public const float ReqListGap = 7f;
		public const float ReqTickSize = 16f;
		public const float ReqListMarginBottom = 12f;
		public const float BlockedPaddingH = 10f;
		public const float BlockedPaddingV = 7f;
		public const float BlockedMarginTop = 8f;

		// ---------- 设施卡 ----------
		public const float SlotGridMinCell = 300f;
		public const float SlotGridGap = 14f;
		public const float CardPaddingH = 14f;
		public const float CardPaddingTop = 13f;
		public const float CardPaddingBottom = 12f;
		public const float CardGap = 10f;
		public const float CardIconSize = 34f;
		public const float CardIconGlyph = 18f;
		public const float CardDescMaxLines = 2f;
		public const float ProdListGap = 10f;
		public const float ProdPaddingH = 10f;
		public const float ProdPaddingV = 9f;
		public const float ProdGap = 6f;
		public const float ProdMetaGap = 8f;
		public const float FootGap = 8f;
		public const float EmptyCardMinHeight = 168f;
		public const float EmptyCardGap = 4f;
		public const float SlotTagTop = 10f;
		public const float SlotTagRight = 12f;

		// ---------- 弹窗 ----------
		public const float ModalNormalWidth = 880f;
		public const float ModalWideWidth = 1040f;
		public const float ModalNarrowWidth = 520f;
		public const float ModalMaxHeightRatio = 0.86f;
		public const float ModalHeadPaddingH = 16f;
		public const float ModalHeadPaddingTop = 15f;
		public const float ModalHeadPaddingBottom = 13f;
		public const float ModalHeadGap = 12f;
		public const float ModalBodyPadding = 16f;
		public const float ModalFootPaddingH = 16f;
		public const float ModalFootPaddingV = 12f;
		public const float ModalFootGap = 10f;
		public const float InstallGridMinCell = 310f;
		public const float InstallGridGap = 13f;
		public const float InstallCardPadding = 13f;
		public const float InstallCardGap = 9f;
		public const float InstallCardFootGap = 8f;   // 卡片底部：正文与分隔线之间的留白
		public const float CostListGap = 5f;
		public const float CostRowPaddingH = 8f;
		public const float CostRowPaddingV = 4f;
		public const float CostRowGap = 8f;
		public const float MatIconSize = 18f;
		public const float KvGridMinCell = 190f;
		public const float KvGridGap = 8f;
		public const float KvPaddingH = 10f;
		public const float KvPaddingV = 8f;
		public const float RuleCardPaddingH = 11f;
		public const float RuleCardPaddingV = 10f;
		public const float RuleCardGap = 7f;

		/// <summary>CSS auto-fill minmax(minCell, 1fr) 的等价列数。</summary>
		public static int GridColumns(float width, float minCell, float gap)
		{
			if (width <= 0f)
			{
				return 1;
			}
			int columns = (int)((width + gap) / (minCell + gap));
			return (columns < 1) ? 1 : columns;
		}

		/// <summary>栅格单元宽度（1fr 均分余量）。</summary>
		public static float GridCellWidth(float width, int columns, float gap)
		{
			if (columns < 1)
			{
				columns = 1;
			}
			return (width - gap * (columns - 1)) / columns;
		}

		// ---------- 断点 ----------
		/// <summary>面板宽度换算成正文宽度时要扣掉的：侧栏 + 内容左右内边距 + 滚动条槽位。</summary>
		public const float PanelToContentWidth = SidebarWidth + ContentPaddingH * 2f + ScrollbarGutter;

		/// <summary>
		/// 原型里 <c>@media (max-width: 1080px)</c> 的断点：那是**视口**宽度（浏览器里的整页），
		/// 而页面拿到手的是正文宽度，所以要减去侧栏与内边距再比较，否则三栏/两栏会永远塌成单栏。
		/// </summary>
		public const float StackBreakpoint = 1080f - PanelToContentWidth;
	}
}
