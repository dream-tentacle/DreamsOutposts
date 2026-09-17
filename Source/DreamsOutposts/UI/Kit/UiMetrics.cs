namespace DreamsOutposts
{
	/// <summary>
	/// 新 UI 的几何度量。莱茵生命方向：更平、更细、更克制；页签缩放动画保留。
	/// </summary>
	public static class UiMetrics
	{
		// ---------- 窗口 ----------
		public const float WindowMaxWidth = 1240f;
		public const float WindowMaxHeight = 860f;
		public const float WindowShadowMargin = 8f;
		/// <summary>windowRect 到屏幕左右边缘的安全留白；主体面板还会再内缩 WindowShadowMargin。</summary>
		public const float WindowScreenMarginH = 16f;
		/// <summary>windowRect 到屏幕上下边缘的安全留白；主体面板还会再内缩 WindowShadowMargin。</summary>
		public const float WindowScreenMarginV = 12f;
		public const float TitlebarPaddingTop = 14f;
		public const float TitlebarPaddingBottom = 14f;
		public const float TitlebarPaddingLeft = 18f;
		public const float TitlebarPaddingRight = 16f;
		public const float TitlebarGap = 18f;
		public const float EmblemSize = 42f;
		public const float EmblemIconSize = 22f;
		public const float TitleSubGap = 6f;

		// ---------- 侧栏 ----------
		public const float SidebarWidth = 300f;
		public const float SidebarPaddingH = 10f;
		public const float SidebarPaddingLeft = 32f;
		public const float SidebarPaddingV = 18f;
		public const float SidebarGap = 10f;

		/// <summary>保留原有页签缩放：未选中项缩小 10%。</summary>
		public const float NavInactiveShrink = 0.1f;
		public const float NavTileAnimSeconds = 0.14f;

		public const float NavItemPaddingH = 12f;
		public const float NavItemPaddingV = 10f;
		public const float NavItemGap = 10f;
		public const float NavIconSize = 17f;
		public const float NavLabelPaddingH = 10f;
		public const float NavLabelPaddingTop = 4f;
		public const float NavLabelPaddingBottom = 8f;
		public const float NavBadgeMinWidth = 20f;
		public const float NavBadgePaddingH = 6f;
		public const float NavActiveArcRadius = 10f;
		public const float NavActiveArcGap = 2f;

		// ---------- 内容区 ----------
		public const float ContentPaddingTop = 24f;
		public const float ManagementHeaderHeight = 92f;
		public const float ContentPaddingH = 24f;
		public const float ContentPaddingBottom = 28f;
		/// <summary>全屏管理界面里正文的最大有效宽度；其余横向空间交给背景与留白。</summary>
		public const float ContentMaxWidth = 1180f;
		public const float PageHeadMarginBottom = 18f;
		public const float PageHeadSubGap = 4f;
		public const float PageHeadTitleIndent = 0f;
		public const float PageHeadMaxTextWidth = 560f;
		public const float ContentFadeSeconds = 0.42f;
		public const float ContentSlideDistance = 40f;
		public const float SectionSpacing = 24f;
		public const float SectionHeadGap = 10f;
		public const float SectionHeadMarginBottom = 14f;

		// ---------- 滚动条 ----------
		public const float ScrollbarGutter = 10f;
		public const float ScrollbarWidth = 10f;
		public const float ScrollbarThumbWidth = 5f;
		public const float ScrollbarMinThumb = 24f;
		public const float ScrollWheelSpeed = 28f;

		// ---------- 通用 ----------
		public const float BorderWidth = 1f;
		public const float RadiusXs = 1f;
		public const float RadiusSm = 2f;
		public const float RadiusXs2 = 1f;
		public const float RadiusSm2 = 1f;

		public const float ButtonPaddingH = 15f;
		public const float ButtonPaddingV = 7f;
		public const float ButtonSmallPaddingH = 10f;
		public const float ButtonSmallPaddingV = 5f;
		public const float ButtonGap = 6f;
		public const float IconButtonSize = 28f;
		public const float CloseButtonSize = 30f;

		public const float ChipPaddingH = 8f;
		public const float ChipPaddingV = 2f;
		public const float ChipSmallPaddingH = 6f;
		public const float ChipSmallPaddingV = 1f;
		public const float ChipGap = 5f;
		public const float ChipGlyphSize = 12f;
		public const float ChipFlowGap = 7f;

		public const float BarHeight = 4f;
		public const float BarRadius = 0f;

		// ---------- 传说边框 ----------
		public const float LegendaryBorderWidth = 2f;
		public const float LegendaryBorderSpinPeriod = 5f;

		// ---------- 等级卡 ----------
		public const float LevelCardPaddingH = 20f;
		public const float LevelCardPaddingV = 18f;
		public const float LevelCardGap = 24f;
		public const float LevelRightWidth = 320f;
		public const float PipWidth = 48f;
		public const float PipHeight = 6f;
		public const float PipGap = 5f;
		public const float PipMarginBottom = 12f;
		public const float LevelFactsGap = 0f;
		public const float LevelFactsMarginTop = 18f;
		public const float LevelCurrentDigitHeight = 78f;
		public const float LevelMaxDigitHeight = 42f;
		public const float LevelDigitGap = 10f;
		public const float LevelDigitRowGap = 10f;
		public const float LevelDisplayGap = 8f;
		public const float LevelFactHeight = 58f;
		public const float LevelFactLineWidth = 3f;
		public const float LevelFactPaddingLeft = 12f;
		public const float LevelFactColumnGap = 10f;
		public const float LevelUpgradeSweepDuration = 0.35f;
		public const float LevelUpgradeFlashDuration = 0.9f;
		public const float LevelUpgradeFlashFillAlpha = 0.12f;
		public const float LevelUpgradeFlashBorderAlpha = 0.65f;
		public const float LevelUpgradeSweepHeight = 64f;
		public const float LevelUpgradeSweepAlpha = 0.12f;
		public const float ReqListGap = 7f;
		public const float ReqTickSize = 16f;
		public const float ReqListMarginBottom = 12f;
		public const float UpgradeButtonHeight = 36f;
		public const float BlockedPaddingH = 10f;
		public const float BlockedPaddingV = 7f;
		public const float BlockedMarginTop = 8f;

		// ---------- 设施卡 ----------
		public const float SlotGridMinCell = 300f;
		public const float SlotGridGap = 14f;
		public const float CardPaddingH = 16f;
		public const float CardPaddingTop = 15f;
		public const float CardPaddingBottom = 14f;
		public const float CardGap = 12f;
		public const float FacilityBodyMinHeight = 50f;

		/// <summary>旧图标尺寸仍供部分 UI 使用。</summary>
		public const float CardIconSize = 34f;
		public const float CardIconGlyph = 18f;
		/// <summary>设施档案卡左侧主视觉图尺寸。</summary>
		public const float FacilityImageSize = 72f;
		public const float FacilityImageGlyph = 22f;

		public const float CardDescMaxLines = 2f;
		public const float ProdListGap = 10f;
		public const float ProdPaddingH = 10f;
		public const float ProdPaddingV = 9f;
		public const float ProdGap = 6f;
		public const float ProdMetaGap = 8f;
		public const float EmptyCardMinHeight = 176f;
		public const float EmptyCardGap = 6f;
		public const float SlotTagTop = 12f;
		public const float SlotTagRight = 14f;

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
		public const float InstallCardFootGap = 8f;
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

		public static int GridColumns(float width, float minCell, float gap)
		{
			if (width <= 0f)
			{
				return 1;
			}

			int columns = (int)((width + gap) / (minCell + gap));
			return (columns < 1) ? 1 : columns;
		}

		public static float GridCellWidth(float width, int columns, float gap)
		{
			if (columns < 1)
			{
				columns = 1;
			}
			return (width - gap * (columns - 1)) / columns;
		}

		public const float PanelToContentWidth = SidebarWidth + ContentPaddingH * 2f + ScrollbarGutter;
		public const float StackBreakpoint = 1080f - PanelToContentWidth;
	}
}